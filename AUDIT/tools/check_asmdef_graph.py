"""Проверка графа сборок (.asmdef) без Unity.

Зачем: каждая сборка Unity компилируется отдельно. Если .cs использует
namespace из другой сборки (включая пакетные сборки вроде Unity.InputSystem
или UnityEngine.UI), а ссылки в .asmdef нет — в Unity будет CS0234/CS0246,
даже когда «всё компилируется» одной кучей файлов.

Проверяет:
  1. Отсутствие циклов между .asmdef;
  2. Наличие ссылок на все LifeFromZero.* сборки, чьи пространства имён используются;
  3. Наличие ссылки на Unity.InputSystem при прямом обращении к UnityEngine.InputSystem;
  4. Наличие ссылки на UnityEngine.UI при обращении к UnityEngine.UI / UnityEngine.EventSystems;
  5. Соблюдение noEngineReferences: true (отсутствие using UnityEngine / UnityEditor).

Запуск из корня репозитория:
    python3 AUDIT/tools/check_asmdef_graph.py

Выход: 0 — все ссылки на месте, 1 — найдены ошибки.
"""
import glob
import json
import os
import re
import sys

STRIP_COMMENTS_AND_STRINGS = re.compile(
    r'//.*?$|/\*.*?\*/|"(?:\\.|[^"\\])*"',
    re.DOTALL | re.MULTILINE,
)

asmdefs = {}
folder_to_asm = {}
for f in sorted(glob.glob("**/*.asmdef", recursive=True)):
    f_norm = f.replace("\\", "/")
    data = json.load(open(f_norm, encoding="utf-8"))
    name = data["name"]
    folder = os.path.dirname(f_norm)
    asmdefs[name] = (f_norm, data, folder)
    folder_to_asm[folder] = name

# Определяем ближайший родительский .asmdef для каждого .cs файла
asm_sources = {name: [] for name in asmdefs}
for cs in sorted(glob.glob("**/*.cs", recursive=True)):
    cs_norm = cs.replace("\\", "/")
    if "/Tools/" in cs_norm or "unitystubs" in cs_norm or "editorstubs" in cs_norm:
        continue
    best_folder = None
    d = os.path.dirname(cs_norm)
    while True:
        if d in folder_to_asm:
            best_folder = d
            break
        if not d or "/" not in d:
            break
        d = os.path.dirname(d)
    if best_folder is not None:
        asm_sources[folder_to_asm[best_folder]].append(cs_norm)

ns2asm = {}
for name, (_, data, _) in asmdefs.items():
    # Тестовые сборки не являются целью обычных ссылок и делят префиксы пространств имён
    if "TestAssemblies" in data.get("optionalUnityReferences", []):
        continue
    root_ns = data.get("rootNamespace") or name
    ns2asm[root_ns] = name

problems = []

# 1. Проверка ацикличности графа
visiting = set()
visited = set()


def dfs(node, stack):
    if node in visiting:
        cycle = " -> ".join(stack + [node])
        problems.append(f"ЦИКЛ СБОРОК: {cycle}")
        return
    if node in visited or node not in asmdefs:
        return
    visiting.add(node)
    for ref in asmdefs[node][1].get("references", []):
        dfs(ref, stack + [node])
    visiting.remove(node)
    visited.add(node)


for asm_name in sorted(asmdefs):
    dfs(asm_name, [])

# 2–5. Проверка ссылок и ограничений по исходникам каждой сборки
for name, (asm_path, data, _) in sorted(asmdefs.items()):
    refs = set(data.get("references", []))
    no_engine = bool(data.get("noEngineReferences", False))
    include_platforms = data.get("includePlatforms", [])
    is_editor = include_platforms == ["Editor"]

    used_lfz = set()
    uses_input_system = False
    uses_ugui = False
    uses_unity_engine = False
    uses_unity_editor = False

    for src in asm_sources.get(name, []):
        raw = open(src, encoding="utf-8", errors="ignore").read()
        clean = STRIP_COMMENTS_AND_STRINGS.sub("", raw)

        for m in re.finditer(r"\busing\s+(LifeFromZero[\w.]*)\s*;", clean):
            used_lfz.add(m.group(1))
        for m in re.finditer(r"\b(LifeFromZero(?:\.[A-Za-z_]\w*){1,4})\b", clean):
            used_lfz.add(m.group(1))

        if re.search(r"\bUnityEngine\.InputSystem\b", clean):
            uses_input_system = True
        if re.search(r"\bUnityEngine\.(?:UI|EventSystems)\b", clean):
            uses_ugui = True
        if re.search(r"\busing\s+UnityEngine\b|\bUnityEngine\.", clean):
            uses_unity_engine = True
        # Игнорируем блоки под #if UNITY_EDITOR в runtime-сборках
        no_editor_ifdef = re.sub(
            r"#if\s+UNITY_EDITOR\b.*?#endif", "", clean, flags=re.DOTALL
        )
        if re.search(r"\busing\s+UnityEditor\b|\bUnityEditor\.", no_editor_ifdef):
            uses_unity_editor = True

    for ns in sorted(used_lfz):
        target = None
        for root, cand_asm in sorted(ns2asm.items(), key=lambda kv: -len(kv[0])):
            if ns == root or ns.startswith(root + "."):
                target = cand_asm
                break
        if target and target != name and target not in refs:
            # Допускаем подпространства собственной тестовой сборки (например LifeFromZero.UI.Tests)
            own_root = data.get("rootNamespace") or name
            if ns == own_root or ns.startswith(own_root + "."):
                continue
            problems.append(f"{name} ({asm_path}) использует {ns}, но не ссылается на {target}")

    if uses_input_system and "Unity.InputSystem" not in refs:
        problems.append(
            f"{name} ({asm_path}) напрямую обращается к UnityEngine.InputSystem без ссылки на Unity.InputSystem"
        )

    if uses_ugui and "UnityEngine.UI" not in refs:
        problems.append(
            f"{name} ({asm_path}) обращается к UnityEngine.UI/EventSystems без ссылки на UnityEngine.UI"
        )

    if no_engine and (uses_unity_engine or uses_unity_editor):
        problems.append(
            f"{name} ({asm_path}) помечена noEngineReferences=true, но использует UnityEngine/UnityEditor"
        )

    if uses_unity_editor and not is_editor:
        problems.append(
            f"{name} ({asm_path}) использует UnityEditor вне #if UNITY_EDITOR, хотя не ограничена платформой Editor"
        )

if problems:
    for p in sorted(set(problems)):
        print("ASMDEF ERROR:", p)
    sys.exit(1)

print("asmdef graph OK")
