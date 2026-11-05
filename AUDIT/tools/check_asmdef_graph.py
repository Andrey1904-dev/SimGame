"""Проверка графа сборок (.asmdef) без Unity.

Зачем: каждая сборка Unity компилируется отдельно. Если .cs использует
namespace из другой сборки, а ссылки в .asmdef нет — в Unity будет CS0246,
даже когда «всё компилируется» одной кучей файлов (так и был пропущен дефект
UI/Core → City.Quarter в PR #14).

Запуск из корня репозитория:
    python3 AUDIT/tools/check_asmdef_graph.py

Выход: 0 — все ссылки на месте, 1 — найдены пропуски.
"""
import json, glob, os, re, sys
# Проверка графа asmdef: у каждой сборки должны быть ссылки на модули, чьи namespace встречаются в её .cs
asmdefs={}
for f in glob.glob('**/*.asmdef',recursive=True):
    d=json.load(open(f))
    asmdefs[d['name']]=(f,d)
dirs={}
for f in glob.glob('**/*.cs',recursive=True):
    if '/Tools/' in f or '/Tests/' in f or 'unitystubs' in f or '/Editor/' in f.lower() and 'UI/Editor' not in f: continue
    dirs.setdefault(os.path.dirname(f),set()).add(f)
# map namespace root -> asmdef name from rootNamespace
ns2asm={}
for name,(f,d) in asmdefs.items():
    ns2asm[d.get('rootNamespace',name)]=name
problems=[]
for name,(f,d) in asmdefs.items():
    refs=set(d.get('references',[]))
    folder=os.path.dirname(f)
    srcs=[]
    for dirpath,files in dirs.items():
        if dirpath==folder or dirpath.startswith(folder+'/'):
            srcs+=list(files)
    nsset=set()
    for s in srcs:
        for m in re.finditer(r'^using (LifeFromZero[\w.]*);',open(s,encoding='utf-8',errors='ignore').read(),re.M):
            nsset.add(m.group(1))
    for ns in sorted(nsset):
        target=None
        for root,a in sorted(ns2asm.items(),key=lambda kv:-len(kv[0])):
            if ns==root or ns.startswith(root+'.'):
                target=a; break
        if target and target!=name and target not in refs:
            problems.append(f"{name} ({f}) использует {ns}, но не ссылается на {target}")
if problems:
    for p in sorted(set(problems)): print("MISSING REF:", p)
    sys.exit(1)
print("asmdef graph OK")
