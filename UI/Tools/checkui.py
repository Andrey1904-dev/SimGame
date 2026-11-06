"""Проверка компиляции UI и моста района без Unity (Roslyn + стабы).

Запуск из корня репозитория:  python3 UI/Tools/checkui.py
Ищет файлы относительно текущего каталога, поэтому запускать нужно из корня.

Это проверка СИНТАКСИСА И ТИПОВ против стабов, а не компиляция Unity.
"""
import sys, glob
sys.path.insert(0, __import__('os').path.dirname(__import__('os').path.abspath(__file__)))
from run import compile_to_image
files = (sorted(glob.glob('UI/Core/*.cs')) + sorted(glob.glob('UI/Runtime/*.cs'))
         + sorted(glob.glob('City/Quarter/*.cs')) + sorted(glob.glob('City/Quarter/DistrictMap/*.cs'))
         # City.Pause: UI использует существующую паузу проекта. Берём только контракты —
         # PauseView тянет uGUI, который в стабах не нужен.
         + ['City/PauseMenu/PauseContracts.cs']
         # Jobs.Delivery: чистый C#-модуль, UI подключается к нему мостом телефона.
         + sorted(glob.glob('Jobs/Delivery/*.cs'))
         + ['UI/Tools/unitystubs/UnityStubs.cs'])
ok, errs, warns, ms = compile_to_image(files)
seen=set()
for e in errs[:80]:
    loc = e.Location.GetLineSpan()
    key=(e.Id, e.GetMessage(), loc.Path.rsplit('/',1)[-1], loc.StartLinePosition.Line+1)
    if key in seen: continue
    seen.add(key)
    print("ERROR", e.Id, e.GetMessage(), "|", loc.Path.rsplit('/',1)[-1], "line", loc.StartLinePosition.Line+1)
print("COMPILE:", "OK" if ok else "FAILED", "errors=%d" % len(errs))
sys.exit(0 if ok else 1)
