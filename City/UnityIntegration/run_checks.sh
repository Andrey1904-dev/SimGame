#!/usr/bin/env bash
# Life From Zero — City/UnityIntegration/run_checks.sh (ТЗ #038 + интеграция UI PR #14 + PR #15)
#
# Поэтапные проверки интеграции District750 + Pause Menu + UI + доменных модулей.
# Каждый этап печатает ровно один итог: PASS / FAIL / NOT RUN.
# NOT RUN означает «проверка не выполнялась» и НЕ считается успехом.
#
# Использование:
#   bash City/UnityIntegration/run_checks.sh                       # только то, что доступно
#   bash City/UnityIntegration/run_checks.sh --unity-project "/Users/homepc/Projects/LifeFromZero"
#   bash City/UnityIntegration/run_checks.sh --unity-project "…" --unity "/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
#
# Опции:
#   --unity-project <path>  Путь к Unity-проекту (нужен для этапов 2, 4, 5, 6, 6b).
#   --unity <path>          Явный путь к бинарнику Unity (иначе ищется в Unity Hub).
#   --log <path>            Дополнительный лог Unity Console для этапа 7.
#   --report-dir <path>     Куда писать отчёты (по умолчанию City/UnityIntegration/reports/<время>).
#   --no-tests              Пропустить этапы Unity batchmode (4–6b).
#   --help                  Эта справка.
#
# Код завершения: 0 — ни одного FAIL; 1 — есть FAIL; 2 — ошибка аргументов.

set -u
set -o pipefail

SCRIPT_SOURCE="${BASH_SOURCE[0]}"
SCRIPT_DIR="$(cd "$(dirname "$SCRIPT_SOURCE")" && pwd -P)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd -P)"

UNITY_PROJECT=""
UNITY_BIN_ARG=""
EXTRA_LOG=""
REPORT_DIR=""
RUN_TESTS="yes"

while [ "$#" -gt 0 ]; do
    case "$1" in
        --unity-project|--unity|--log|--report-dir)
            if [ "$#" -lt 2 ]; then
                printf 'ОШИБКА: опция %s требует значение\n' "$1" >&2
                exit 2
            fi
            case "$1" in
                --unity-project) UNITY_PROJECT="$2" ;;
                --unity)         UNITY_BIN_ARG="$2" ;;
                --log)           EXTRA_LOG="$2" ;;
                --report-dir)    REPORT_DIR="$2" ;;
            esac
            shift 2 ;;
        --no-tests)      RUN_TESTS="no"; shift ;;
        --help|-h)       sed -n '2,23p' "$SCRIPT_SOURCE" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) printf 'ОШИБКА: неизвестный аргумент: %s\n' "$1" >&2; exit 2 ;;
    esac
done

STAMP="$(date -u '+%Y%m%dT%H%M%SZ')"
if [ -z "$REPORT_DIR" ]; then
    REPORT_DIR="$REPO_ROOT/City/UnityIntegration/reports/$STAMP"
fi
mkdir -p "$REPORT_DIR" 2>/dev/null || { printf 'ОШИБКА: не удалось создать каталог отчётов %s\n' "$REPORT_DIR" >&2; exit 2; }
REPORT_DIR="$(cd "$REPORT_DIR" && pwd -P)"

SUMMARY_FILE="$REPORT_DIR/summary.txt"
: >"$SUMMARY_FILE"

FAILS=0
NOTRUN=0
PASSES=0

record() {
    local status="$1" num="$2" name="$3" detail="${4:-}"
    printf '[%s] Этап %s. %s%s\n' "$status" "$num" "$name" "${detail:+ — $detail}"
    printf '%s\t%s\t%s\t%s\n' "$status" "$num" "$name" "$detail" >>"$SUMMARY_FILE"
    case "$status" in
        PASS)     PASSES=$((PASSES + 1)) ;;
        FAIL)     FAILS=$((FAILS + 1)) ;;
        NOT\ RUN) NOTRUN=$((NOTRUN + 1)) ;;
    esac
}

section() { printf '\n--- %s\n' "$*"; }

printf 'Life From Zero — проверки интеграции (District750 + Pause + UI)\n'
printf 'Репозиторий: %s\nОтчёты: %s\n' "$REPO_ROOT" "$REPORT_DIR"

if [ -n "$UNITY_PROJECT" ]; then
    [ -d "$UNITY_PROJECT" ] || { printf 'ОШИБКА: каталог проекта не найден: %s\n' "$UNITY_PROJECT" >&2; exit 2; }
    UNITY_PROJECT="$(cd "$UNITY_PROJECT" && pwd -P)"
fi

# =====================================================================
section "Этап 1. Статические проверки репозитория (District750 + граф .asmdef + документация)"
if ! command -v python3 >/dev/null 2>&1; then
    record "NOT RUN" 1 "Статические проверки репозитория" "python3 не найден в PATH"
else
    LOG1="$REPORT_DIR/stage1-static.log"
    if (
        cd "$REPO_ROOT" &&
        python3 City/Tools/district_layout_check.py &&
        python3 AUDIT/tools/check_asmdef_graph.py &&
        python3 AUDIT/tools/check_docs_consistency.py
    ) >"$LOG1" 2>&1; then
        objects=$(grep -m 1 '^objects' "$LOG1" | awk '{print $2}')
        deliveries=$(grep -m 1 '^deliveries' "$LOG1" | awk '{print $2}')
        unreachable=$(grep -m 1 '^unreachable' "$LOG1" | awk '{print $2}')
        home=$(grep -m 1 '^player_home' "$LOG1" | sed 's/^player_home[[:space:]]*//')
        record "PASS" 1 "Статические проверки репозитория" "objects=$objects deliveries=$deliveries unreachable=$unreachable; asmdef=OK; docs=OK; дом: $home"
    else
        record "FAIL" 1 "Статические проверки репозитория" "см. $(basename "$LOG1")"
    fi
fi

# =====================================================================
section "Этап 2. Структура Unity-проекта и отсутствие дубликатов .asmdef"
if [ -z "$UNITY_PROJECT" ]; then
    record "NOT RUN" 2 "Структура Unity-проекта" "не передан --unity-project"
elif [ ! -d "$UNITY_PROJECT/Assets" ] || [ ! -d "$UNITY_PROJECT/ProjectSettings" ]; then
    record "FAIL" 2 "Структура Unity-проекта" "нет Assets/ или ProjectSettings/ в $UNITY_PROJECT"
else
    LOG2="$REPORT_DIR/stage2-project-structure.log"
    if python3 - "$UNITY_PROJECT" >"$LOG2" 2>&1 <<'PY'
import glob, json, os, sys
root = sys.argv[1]
assets = os.path.join(root, "Assets")

required_asmdefs = [
    "LifeFromZero.City.Quarter",
    "LifeFromZero.City.Quarter.UnityAdapter",
    "LifeFromZero.City.Pause",
    "LifeFromZero.UI.Core",
    "LifeFromZero.UI",
    "LifeFromZero.UI.Editor",
    "LifeFromZero.Economy",
    "LifeFromZero.Jobs.Delivery",
    "LifeFromZero.Phone",
    "LifeFromZero.Debts",
]
required_files = [
    "PauseKeyboardInput.cs",
    "LifeFromZeroSetup.cs",
    "LifeFromZeroDiscovery.cs",
    "LifeFromZeroGameIntegration.cs",
    "District750Link.cs",
    "PlayerHomeAnchor.cs",
    "UiInstaller.cs",
    "QuarterLocationProvider.cs",
    "AdapterBridges.cs",
    "PhoneUiBridge.cs",
]

by_asm = {}
for p in sorted(glob.glob(os.path.join(assets, "**/*.asmdef"), recursive=True)):
    name = json.load(open(p, encoding="utf-8")).get("name", "")
    if name:
        by_asm.setdefault(name, []).append(os.path.relpath(p, root))

errors = []
for name, paths in sorted(by_asm.items()):
    if len(paths) > 1:
        errors.append(f"DUPLICATE_ASMDEF {name}: {paths}")
for req in required_asmdefs:
    if req not in by_asm:
        errors.append(f"MISSING_ASMDEF {req}")
    else:
        print(f"OK_ASMDEF {req} -> {by_asm[req][0]}")

all_cs = {os.path.basename(p): os.path.relpath(p, root) for p in glob.glob(os.path.join(assets, "**/*.cs"), recursive=True)}
for rf in required_files:
    if rf not in all_cs:
        errors.append(f"MISSING_FILE {rf}")
    else:
        print(f"OK_FILE {rf} -> {all_cs[rf]}")

for bad in ("UI/Tools", "City/Tools"):
    if os.path.isdir(os.path.join(assets, bad)):
        errors.append(f"FORBIDDEN_DIR Assets/{bad}")

if errors:
    for e in errors:
        print("ERR", e)
    sys.exit(1)
print(f"SUMMARY asmdefs={len(by_asm)} required_ok={len(required_asmdefs)} files_ok={len(required_files)}")
PY
    then
        summary_line=$(grep '^SUMMARY ' "$LOG2" | tail -n 1)
        record "PASS" 2 "Структура Unity-проекта" "$summary_line"
    else
        record "FAIL" 2 "Структура Unity-проекта" "см. $(basename "$LOG2")"
    fi
fi

# =====================================================================
section "Этап 3. Внедвижковая C#-компиляция (dotnet / Roslyn)"
PYTHON_ROSLYN=""
if [ -x "/tmp/pyvenv/bin/python3" ]; then
    PYTHON_ROSLYN="/tmp/pyvenv/bin/python3"
elif command -v python3 >/dev/null 2>&1 && python3 -c "import clr_loader" >/dev/null 2>&1; then
    PYTHON_ROSLYN="python3"
fi

if [ -n "$PYTHON_ROSLYN" ] && [ -f "$REPO_ROOT/AUDIT/tools/check_unity6_compile.py" ]; then
    LOG3="$REPORT_DIR/stage3-roslyn-unity6.log"
    if (
        cd "$REPO_ROOT" &&
        "$PYTHON_ROSLYN" AUDIT/tools/check_unity6_compile.py &&
        "$PYTHON_ROSLYN" UI/Tools/checkui.py &&
        "$PYTHON_ROSLYN" UI/Tools/checkeditor.py &&
        "$PYTHON_ROSLYN" UI/Tools/runtests.py
    ) >"$LOG3" 2>&1; then
        record "PASS" 3 "Внедвижковая C#-компиляция (Roslyn Unity 6)" "17 .asmdef + Assembly-CSharp + Assembly-CSharp-Editor + UI tests: 0 ошибок"
    else
        record "FAIL" 3 "Внедвижковая C#-компиляция (Roslyn Unity 6)" "см. $(basename "$LOG3")"
    fi
elif command -v dotnet >/dev/null 2>&1 && [ -f "$REPO_ROOT/City/Tests/Console.csproj" ]; then
    LOG3="$REPORT_DIR/stage3-dotnet-build.log"
    if ( cd "$REPO_ROOT" && dotnet build City/Tests/Console.csproj --nologo ) >"$LOG3" 2>&1; then
        record "PASS" 3 "C# build через dotnet" "сборка City/Tests/Console.csproj успешна"
    else
        record "FAIL" 3 "C# build через dotnet" "см. $(basename "$LOG3")"
    fi
else
    record "NOT RUN" 3 "Внедвижковая C#-компиляция" "ни dotnet SDK, ни pythonnet/Roslyn не найдены в окружении"
fi

# =====================================================================
find_unity() {
    if [ -n "$UNITY_BIN_ARG" ]; then
        [ -x "$UNITY_BIN_ARG" ] && { printf '%s' "$UNITY_BIN_ARG"; return 0; }
        return 1
    fi
    if [ -n "${UNITY_PATH:-}" ] && [ -x "${UNITY_PATH}" ]; then
        printf '%s' "$UNITY_PATH"; return 0
    fi
    local candidate=""
    for c in /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity; do
        [ -x "$c" ] && candidate="$c"
    done
    [ -n "$candidate" ] && { printf '%s' "$candidate"; return 0; }
    if command -v Unity >/dev/null 2>&1; then command -v Unity; return 0; fi
    return 1
}

UNITY_BIN=""
if UNITY_BIN="$(find_unity)"; then :; else UNITY_BIN=""; fi

unity_blocked_reason() {
    if [ -z "$UNITY_PROJECT" ]; then printf 'не передан --unity-project'; return 0; fi
    if [ -z "$UNITY_BIN" ]; then printf 'бинарник Unity не найден (--unity / UNITY_PATH / Unity Hub)'; return 0; fi
    if [ -e "$UNITY_PROJECT/Temp/UnityLockfile" ]; then printf 'проект открыт в Unity (Temp/UnityLockfile) — второй экземпляр не запускаем'; return 0; fi
    printf ''
}

run_unity_tests() {
    local platform="$1" num="$2" name="$3"
    if [ "$RUN_TESTS" != "yes" ]; then
        record "NOT RUN" "$num" "$name" "отключено флагом --no-tests"
        return
    fi
    local reason
    reason="$(unity_blocked_reason)"
    if [ -n "$reason" ]; then
        record "NOT RUN" "$num" "$name" "$reason"
        return
    fi
    local results="$REPORT_DIR/stage$num-$platform-results.xml"
    local log="$REPORT_DIR/stage$num-$platform.log"
    if "$UNITY_BIN" -batchmode -projectPath "$UNITY_PROJECT" -runTests -testPlatform "$platform" \
        -testResults "$results" -logFile "$log" -quit >/dev/null 2>&1; then
        local total failed
        total=$(grep -c '<test-case ' "$results" 2>/dev/null | tr -d ' ')
        failed=$(grep '<test-case ' "$results" 2>/dev/null | grep -c 'result="Failed"' | tr -d ' ')
        if [ "${failed:-0}" -gt 0 ]; then
            record "FAIL" "$num" "$name" "$failed из $total тестов упали (см. $(basename "$results"))"
        else
            record "PASS" "$num" "$name" "тестов: ${total:-0}, упавших: 0 ($platform)"
        fi
    else
        record "FAIL" "$num" "$name" "Unity завершился с ошибкой (см. $(basename "$log"))"
    fi
}

# =====================================================================
section "Этап 4. EditMode-тесты (City + UI)"
run_unity_tests "EditMode" 4 "EditMode-тесты (City + UI)"

# =====================================================================
section "Этап 5. PlayMode-тесты меню паузы"
run_unity_tests "PlayMode" 5 "PlayMode-тесты меню паузы"

# =====================================================================
section "Этап 6. Обнаружение стартовой сцены и read-only диагностика UI"
if [ "$RUN_TESTS" != "yes" ]; then
    record "NOT RUN" 6 "Обнаружение сцен и проверка UI" "отключено флагом --no-tests"
else
    reason="$(unity_blocked_reason)"
    if [ -n "$reason" ]; then
        record "NOT RUN" 6 "Обнаружение сцен и проверка UI" "$reason"
    else
        LOG6D="$REPORT_DIR/stage6-detect.log"
        LOG6U="$REPORT_DIR/stage6-ui-verify.log"
        if "$UNITY_BIN" -batchmode -quit -projectPath "$UNITY_PROJECT" \
            -executeMethod LifeFromZero.Setup.LifeFromZeroDiscovery.DetectBatch \
            -logFile "$LOG6D" >/dev/null 2>&1 &&
           "$UNITY_BIN" -batchmode -quit -projectPath "$UNITY_PROJECT" \
            -executeMethod LifeFromZero.UI.Editor.UiInstaller.RunBatchVerify \
            -logFile "$LOG6U" >/dev/null 2>&1; then
            chosen="$(grep -o "Стартовая сцена: .*" "$LOG6D" 2>/dev/null | head -n 1)"
            ui_summary="$(grep -o "BATCH_VERIFY scenes=.*" "$LOG6U" 2>/dev/null | head -n 1)"
            record "PASS" 6 "Обнаружение сцен и проверка UI" "${chosen:-detect=OK}; ${ui_summary:-ui_verify=OK}"
        else
            record "FAIL" 6 "Обнаружение сцен и проверка UI" "см. stage6-detect.log / stage6-ui-verify.log"
        fi
    fi
fi

# =====================================================================
section "Этап 7. Критические ошибки Unity Console"
LOG7="$REPORT_DIR/stage7-console-scan.log"
: >"$LOG7"
scan_targets=""
for f in "$REPORT_DIR"/stage4-*.log "$REPORT_DIR"/stage5-*.log "$REPORT_DIR"/stage6-*.log; do
    [ -f "$f" ] && scan_targets="$scan_targets$f
"
done
[ -n "$EXTRA_LOG" ] && [ -f "$EXTRA_LOG" ] && scan_targets="$scan_targets$EXTRA_LOG
"

if [ -z "$scan_targets" ]; then
    record "NOT RUN" 7 "Критические ошибки Unity Console" "нет логов Unity (этапы 4–6 не запускались, --log не передан)"
else
    found=0
    while IFS= read -r f; do
        [ -n "$f" ] || continue
        printf '=== %s\n' "$f" >>"$LOG7"
        if grep -nE 'error CS[0-9]{4}|NullReferenceException|MissingReferenceException|Assembly has reference to non-existent assembly|Scripts have compiler errors' "$f" >>"$LOG7" 2>/dev/null; then
            found=1
        fi
    done <<EOF
$scan_targets
EOF
    if [ "$found" -eq 0 ]; then
        record "PASS" 7 "Критические ошибки Unity Console" "совпадений нет ($(basename "$LOG7"))"
    else
        record "FAIL" 7 "Критические ошибки Unity Console" "найдены совпадения, см. $(basename "$LOG7")"
    fi
fi

# =====================================================================
section "Этап 8. Сохранение результатов"
REPORT_MD="$REPORT_DIR/report.md"
{
    printf '# Life From Zero — отчёт проверок (%s)\n\n' "$STAMP"
    printf '* Репозиторий: `%s`\n' "$REPO_ROOT"
    if [ -n "$UNITY_PROJECT" ]; then
        printf '* Unity-проект: `%s`\n' "$UNITY_PROJECT"
    else
        printf '* Unity-проект: не указан\n'
    fi
    printf '* Unity: `%s`\n\n' "${UNITY_BIN:-не найден}"
    printf '| Статус | Этап | Проверка | Детали |\n|---|---|---|---|\n'
    while IFS=$'\t' read -r status num name detail; do
        printf '| %s | %s | %s | %s |\n' "$status" "$num" "$name" "$detail"
    done <"$SUMMARY_FILE"
    printf '\nPASS: %s, FAIL: %s, NOT RUN: %s\n' "$PASSES" "$FAILS" "$NOTRUN"
} >"$REPORT_MD" 2>/dev/null

if [ -f "$REPORT_MD" ]; then
    record "PASS" 8 "Сохранение результатов" "$REPORT_MD"
else
    record "FAIL" 8 "Сохранение результатов" "не удалось записать $REPORT_MD"
fi

# =====================================================================
printf '\n================ ИТОГ ================\n'
printf 'PASS: %s   FAIL: %s   NOT RUN: %s\n' "$PASSES" "$FAILS" "$NOTRUN"
printf 'Отчёт: %s\n' "$REPORT_MD"
if [ "$NOTRUN" -gt 0 ]; then
    printf 'ВНИМАНИЕ: NOT RUN — это НЕ успешная проверка. Незапущенные этапы нужно выполнить в локальном Unity 6000.3.27f1.\n'
fi
[ "$FAILS" -gt 0 ] && exit 1
exit 0
