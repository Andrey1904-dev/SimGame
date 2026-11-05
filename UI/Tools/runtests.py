"""Прогон тестов UI без Unity (Roslyn + стабы + coreclr).

Запуск из корня репозитория:  python3 UI/Tools/runtests.py
"""
import sys, glob
sys.path.insert(0, __import__('os').path.dirname(__import__('os').path.abspath(__file__)))
from run import compile_to_image, load_and_invoke
core = sorted(glob.glob('UI/Core/*.cs')) + sorted(glob.glob('City/Quarter/*.cs')) + sorted(glob.glob('City/Quarter/DistrictMap/*.cs'))
tests = sorted(glob.glob('UI/Tests/*.cs'))
files = core + tests
ok, errs, warns, ms = compile_to_image(files)
for e in errs[:60]:
    loc = e.Location.GetLineSpan()
    print("ERROR", e.Id, e.GetMessage(), "|", loc.Path.rsplit('/',1)[-1], "line", loc.StartLinePosition.Line+1)
print("COMPILE:", "OK" if ok else "FAILED", "errors=%d" % len(errs), "files=%d" % len(files))
if not ok:
    sys.exit(1)
try:
    rc = load_and_invoke(ms, "LifeFromZero.UI.Tests.Program", "Main", [None])
    print("EXIT CODE:", rc)
    sys.exit(0 if int(rc) == 0 else 1)
except Exception as ex:
    print("RUNTIME ERROR:", ex)
    sys.exit(2)
