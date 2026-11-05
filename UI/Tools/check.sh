#!/bin/bash
# Компиляция произвольного набора файлов против стабов Unity.
#   ./UI/Tools/check.sh "UI/Core/MapCalibration.cs,UI/Runtime/UiRouter.cs"
# Запускать из корня репозитория.
set -u
if [ $# -lt 1 ]; then
  echo "usage: $0 \"file1.cs,file2.cs,...\""
  exit 2
fi
exec python3 - "$1" <<'PY'
import sys, os
sys.path.insert(0, os.path.join('UI', 'Tools'))
from run import compile_to_image
files = sys.argv[1].split(",")
ok, errs, warns, ms = compile_to_image(files)
for e in errs[:60]:
    loc = e.Location.GetLineSpan()
    print("ERROR", e.Id, e.GetMessage(), "|", loc.Path.rsplit('/', 1)[-1], "line", loc.StartLinePosition.Line + 1)
for w in warns[:10]:
    print("warn", w.Id, w.GetMessage())
print("COMPILE:", "OK" if ok else "FAILED", "errors=%d" % len(errs))
sys.exit(0 if ok else 1)
PY
