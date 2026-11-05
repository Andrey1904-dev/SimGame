"""Проверка компиляции UI + Editor-установщика против стабов Unity/UnityEditor.

Запуск из корня репозитория:  python3 UI/Tools/checkeditor.py

UnityEditor компилируется только в редакторе, поэтому без этой проверки
установщик остался бы вовсе непроверенным до первого запуска Unity.
Стабы (UI/Tools/editorstubs, UI/Tools/unitystubs) покрывают ровно те API,
которые скрипт использует — если он начнёт использовать больше, проверка
честно упадёт, и это сигнал дописать стаб, а не «починить тест».
"""
import sys, glob, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from run import compile_to_image

files = (sorted(glob.glob('UI/Core/*.cs'))
         + sorted(glob.glob('UI/Runtime/*.cs'))
         + sorted(glob.glob('UI/Editor/*.cs'))
         + sorted(glob.glob('City/Quarter/*.cs'))
         + sorted(glob.glob('City/Quarter/DistrictMap/*.cs'))
         + ['UI/Tools/unitystubs/UnityStubs.cs', 'UI/Tools/editorstubs/EditorStubs.cs'])

ok, errs, warns, ms = compile_to_image(files)
seen = set()
for e in errs[:40]:
    loc = e.Location.GetLineSpan()
    key = (e.Id, e.GetMessage(), loc.Path.rsplit('/', 1)[-1], loc.StartLinePosition.Line + 1)
    if key in seen:
        continue
    seen.add(key)
    print("ERROR", e.Id, e.GetMessage(), "|", loc.Path.rsplit('/', 1)[-1], "line", loc.StartLinePosition.Line + 1)
print("COMPILE:", "OK" if ok else "FAILED", "errors=%d" % len(errs))
sys.exit(0 if ok else 1)
