"""Скомпилировать и запустить произвольный диагностический файл против UI-ядра.

Запуск из корня репозитория:
    python3 UI/Tools/runtc.py path/to/diag.cs

Diag.cs должен содержать `public static class Diag { public static void Main() {...} }`.
Нужен, чтобы быстро проверять гипотезы о геометрии/маршрутах без Unity и без
пересборки тестов. Ничего не пишет в проект.
"""
import sys, glob, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from run import compile_to_image, load_and_invoke

if len(sys.argv) < 2:
    raise SystemExit("usage: python3 UI/Tools/runtc.py <diag.cs>")

extra = sys.argv[1]
files = (sorted(glob.glob('UI/Core/*.cs'))
         + sorted(glob.glob('City/Quarter/*.cs'))
         + sorted(glob.glob('City/Quarter/DistrictMap/*.cs'))
         + [extra])

ok, errs, warns, ms = compile_to_image(files)
for e in errs[:20]:
    print("ERROR", e.Id, e.GetMessage())
print("ok:", ok)
if ok:
    print("rc=", load_and_invoke(ms, "Diag", "Main", []))
