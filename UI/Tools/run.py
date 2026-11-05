#!/usr/bin/env python3
"""Компиляция C# настоящим Roslyn + запуск тестов — БЕЗ Unity.

Зачем: Unity в CI/песочнице нет, а проверить, что код вообще собирается и что
логика тестов проходит, нужно каждую правку. Скрипт поднимает coreclr через
pythonnet, компилирует переданные файлы Roslyn'ом и (по желанию) вызывает
статический Main из полученной сборки.

Это НЕ замена компиляции Unity: UnityEngine здесь подменён стабами
(UI/Tools/unitystubs), а Editor API — UI/Tools/editorstubs. Зелёный прогон
означает «логика и синтаксис в порядке», а не «в Unity всё собралось».

Требования (один раз на машину):
  pip install pythonnet
  каталог с runtime .NET (по умолчанию /tmp/dotnet, переопределяется LFZ_DOTNET_ROOT)
  runtimeconfig.json для coreclr (по умолчанию /tmp/probe.runtimeconfig.json,
  переопределяется LFZ_CORECLR_CONFIG)
"""
import os, sys, json, glob
from pythonnet import load
_DOTNET_ROOT = os.environ.get("LFZ_DOTNET_ROOT", "/tmp/dotnet")
_CORECLR_CONFIG = os.environ.get("LFZ_CORECLR_CONFIG", "/tmp/probe.runtimeconfig.json")
load("coreclr", runtime_config=_CORECLR_CONFIG, dotnet_root=_DOTNET_ROOT)
import clr
clr.AddReference("Microsoft.CodeAnalysis")
clr.AddReference("Microsoft.CodeAnalysis.CSharp")

from Microsoft.CodeAnalysis.CSharp import CSharpSyntaxTree, CSharpCompilation, LanguageVersion, CSharpParseOptions
from Microsoft.CodeAnalysis import MetadataReference, OutputKind, OptimizationLevel, NullableContextOptions
from Microsoft.CodeAnalysis.CSharp import CSharpCompilationOptions
from Microsoft.CodeAnalysis.Emit import EmitResult
from System.Reflection import Assembly, BindingFlags
from System.IO import MemoryStream, File
from System import Array, Type as SysType, Object

def _framework_dir():
    shared = os.path.join(_DOTNET_ROOT, "shared", "Microsoft.NETCore.App")
    if not os.path.isdir(shared):
        raise SystemExit("Не найден runtime .NET: " + shared + " (задайте LFZ_DOTNET_ROOT)")
    versions = sorted(os.listdir(shared))
    return os.path.join(shared, versions[-1])

FW = _framework_dir()
FW_DLLS = [os.path.join(FW, f) for f in sorted(os.listdir(FW)) if f.endswith(".dll")]

_meta_cache = None
def references(extra=None):
    global _meta_cache
    if _meta_cache is None:
        _meta_cache = [MetadataReference.CreateFromFile(p) for p in FW_DLLS]
    refs = list(_meta_cache)
    for p in (extra or []):
        refs.append(MetadataReference.CreateFromFile(p))
    return refs

def compile_to_image(files, extra_refs=None, out=None, defines=None):
    trees = []
    for f in files:
        text = open(f, encoding="utf-8-sig").read()
        opts = CSharpParseOptions(LanguageVersion.CSharp9)
        trees.append(CSharpSyntaxTree.ParseText(text, opts, f))
    asmname = "Asm%d" % (abs(hash(tuple(files))) % 100000)
    from System.Collections.Generic import List
    from Microsoft.CodeAnalysis import SyntaxTree as STree
    from Microsoft.CodeAnalysis import MetadataReference as MRef
    tl = List[STree]()
    for t in trees: tl.Add(t)
    rl = List[MRef]()
    for r in references(extra_refs): rl.Add(r)
    comp = CSharpCompilation.Create(
        asmname,
        tl,
        rl,
        CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithOptimizationLevel(OptimizationLevel.Debug))
    ms = MemoryStream()
    res = comp.Emit(ms)
    diags = [d for d in res.Diagnostics if str(d.Severity) in ("Error", "Warning")]
    errs = [d for d in diags if str(d.Severity) == "Error"]
    if out:
        with open(out, "wb") as fh:
            fh.write(bytes(ms.ToArray()))
    return res.Success, errs, [d for d in diags if str(d.Severity) != "Error"], ms

def load_and_invoke(image_stream, type_name, method_name, args=None):
    from System.Runtime.Loader import AssemblyLoadContext
    from System.IO import SeekOrigin
    image_stream.Position = 0
    bytes_ = image_stream.ToArray()
    ctx = AssemblyLoadContext.Default
    ctx.Resolving += _resolver
    asm = ctx.LoadFromStream(MemoryStream(bytes_))
    t = asm.GetType(type_name)
    if t is None:
        raise RuntimeError("type not found: " + type_name + " in " + str([x.FullName for x in asm.GetTypes()][:40]))
    m = None
    for mi in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly):
        if mi.Name == method_name:
            m = mi
            break
    if m is None:
        raise RuntimeError("method not found: " + method_name)
    inst = None
    if not m.IsStatic:
        inst = m.DeclaringType.GetConstructor(SysType.EmptyTypes).Invoke(Array[Object]([]))
    return m.Invoke(inst, Array[Object](args or []))

def _resolver(ctx, name):
    from System.Reflection import AssemblyName
    from System.Runtime.Loader import AssemblyLoadContext
    cand = os.path.join(FW, str(name.Name) + ".dll")
    if os.path.exists(cand):
        return AssemblyLoadContext.Default.LoadFromAssemblyPath(cand)
    return None

if __name__ == "__main__":
    files = sys.argv[1].split(",")
    ok, errs, warns, ms = compile_to_image(files)
    for e in errs[:40]:
        print("ERROR", e.Id, e.GetMessage(), "|", e.Location.SourceTree.FilePath, e.Location.GetLineSpan().StartLinePosition.Line + 1)
    print("compile:", "OK" if ok else "FAILED", "errors=%d warnings=%d" % (len(errs), len(warns)))
