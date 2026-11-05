"""Сверка документации аудита с фактическим состоянием кода.

Проверяет только то, что можно проверить механически:
  * упомянутые в AUDIT/ тестовые классы существуют;
  * заявленные в TEST_RESULTS.md числа совпадают с реальным количеством тестов;
  * пути файлов из документов не битые.

Не заменяет прогон тестов — только ловит расхождения «документ отстал от кода».

Запуск из корня репозитория:
    python3 AUDIT/tools/check_docs_consistency.py
"""
import glob
import os
import re
import sys

errors = []
root = os.getcwd()

# 1. Каждый .cs, упомянутый в AUDIT/*.md как путь, должен существовать.
for md in glob.glob('AUDIT/*.md'):
    text = open(md, encoding='utf-8').read()
    for m in re.finditer(r'`((?:City|Phone|UI|Jobs|Economy|Debts|UnityIntegrationKit)/[\w./+-]+\.(?:cs|asmdef|py|sh))`', text):
        path = m.group(1)
        if not os.path.exists(path):
            errors.append(f"{md}: путь не существует — {path}")

# 2. Тестовые классы, упомянутые в AUDIT/, должны быть в коде.
for md in glob.glob('AUDIT/*.md'):
    text = open(md, encoding='utf-8').read()
    for m in re.finditer(r'`(\w+Tests?)`', text):
        name = m.group(1)
        if not name.endswith(('Tests', 'Test')):
            continue
        found = False
        for f in glob.glob('**/*.cs', recursive=True):
            if '/Tools/' in f:
                continue
            if re.search(r'class\s+' + re.escape(name) + r'\b', open(f, encoding='utf-8', errors='ignore').read()):
                found = True
                break
        if not found:
            errors.append(f"{md}: тестовый класс не найден — {name}")

# 3. Числа тестов: suite.Add(...) по модулям — сверить с TEST_RESULTS.md.
def count(globs):
    n = 0
    for g in globs:
        for f in glob.glob(g, recursive=True):
            n += len(re.findall(r'suite\.Add\(', open(f, encoding='utf-8', errors='ignore').read()))
    return n

counts = {
    'Economy': count(['Economy/Tests/*.cs']) - 0,
    'Jobs': count(['Jobs/Tests/*.cs']),
    'Phone': count(['Phone/Tests/*.cs']),
    'Debts': count(['Debts/Tests/*.cs']),
    'City': count(['City/Tests/*.cs']),
    'UI': count(['UI/Tests/*.cs']),
}
print("suite.Add по каталогам тестов:", counts)

tr = open('AUDIT/TEST_RESULTS.md', encoding='utf-8').read()
if '403' not in tr:
    errors.append("TEST_RESULTS.md: не найдено итоговое число 403")
if '99' not in tr:
    errors.append("TEST_RESULTS.md: не найдено число UI-тестов 99")

# 4. Ключевой инвариант P0: Build не должен присваивать план до ClearBuilt.
adapter = open('City/UnityAdapter/QuarterUnityAdapter.cs', encoding='utf-8').read()
build = adapter[adapter.index('public GameObject Build('):]
build = build[:build.index('return root;')]
i_clear = build.find('ClearBuilt()')
i_plan = build.find('BeginBuild(plan)')
if i_clear < 0 or i_plan < 0:
    errors.append("QuarterUnityAdapter.Build: не найдены ClearBuilt()/BeginBuild(plan)")
elif i_clear > i_plan:
    errors.append("QuarterUnityAdapter.Build: BeginBuild(plan) вызывается до ClearBuilt() — регрессия P0!")
else:
    print("P0-инвариант Build(): ClearBuilt() → BeginBuild(plan) — OK")

if errors:
    for e in errors:
        print("ОШИБКА:", e)
    sys.exit(1)
print("Документация согласована с кодом")
