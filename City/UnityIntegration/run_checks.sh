#!/usr/bin/env bash
# Life From Zero — City/UnityIntegration/run_checks.sh (ТЗ #038)
#
# Поэтапные проверки интеграции District750 + Pause Menu.
# Каждый этап печатает ровно один итог: PASS / FAIL / NOT RUN.
# NOT RUN означает «проверка не выполнялась» и НЕ считается успехом.
#
# Использование:
#   bash City/UnityIntegration/run_checks.sh                       # только то, что доступно
#   bash City/UnityIntegration/run_checks.sh --unity-project "/Users/homepc/Projects/LifeFromZero"
#   bash City/UnityIntegration/run_checks.sh --unity-project "…" --unity "/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
#
# Опции:
#   --unity-project <path>  Путь к Unity-проекту (нужен для этапов 2, 4, 5, 6).
#   --unity <path>          Явный путь к бинарнику Unity (иначе ищется в Unity Hub).
#   --log <path>            Дополнительный лог Unity Console для этапа 6.
#   --report-dir <path>     Куда писать отчёты (по умолчанию City/UnityIntegration/reports/<время>).
#   --no-tests              Пропустить этапы 4 и 5 (Unity Test Runner).
#   --help                  Эта справка.
#
# Ограничения, зашитые в скрипт:
#   * .NET SDK и Unity не устанавливаются; отсутствие инструмента даёт NOT RUN;
#   * вторая копия Unity Editor не запускается: если проект открыт (Temp/UnityLockfile),
#     этапы 4–6 получают NOT RUN;
#   * отчёты пишутся ВНЕ Assets (по умолчанию City/UnityIntegration/reports).
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
        --help|-h)       sed -n '2,28p' "$SCRIPT_SOURCE" | sed 's/^# \{0,1\}//'; exit 0 ;;
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
    # $1 = статус, $2 = номер этапа, $3 = имя, $4 = деталь
    local status="$1" num="$2" name="$3" detail="${4:-}"
    printf '[%s] Этап %s. %s%s\n' "$status" "$num" "$name" "${detail:+ — $detail}"
    printf '%s\t%s\t%s\t%s\n' "$status" "$num" "$name" "$detail" >>"$SUMMARY_FILE"
    case "$status" in
        PASS)    PASSES=$((PASSES + 1)) ;;
        FAIL)    FAILS=$((FAILS + 1)) ;;
        NOT\ RUN) NOTRUN=$((NOTRUN + 1)) ;;
    esac
}

section() { printf '\n--- %s\n' "$*"; }

printf 'Life From Zero — проверки интеграции (ТЗ #038)\n'
printf 'Репозиторий: %s\nОтчёты: %s\n' "$REPO_ROOT" "$REPORT_DIR"

if [ -n "$UNITY_PROJECT" ]; then
    [ -d "$UNITY_PROJECT" ] || { printf 'ОШИБКА: каталог проекта не найден: %s\n' "$UNITY_PROJECT" >&2; exit 2; }
    UNITY_PROJECT="$(cd "$UNITY_PROJECT" && pwd -P)"
fi

# =====================================================================
section "Этап 1. Python-генератор District750"
if ! command -v python3 >/dev/null 2>&1; then
    record "NOT RUN" 1 "Python-генератор District750" "python3 не найден в PATH"
elif [ ! -f "$REPO_ROOT/City/Tools/district_layout_check.py" ]; then
    record "NOT RUN" 1 "Python-генератор District750" "нет City/Tools/district_layout_check.py"
else
    LOG1="$REPORT_DIR/stage1-district_layout.log"
    if ( cd "$REPO_ROOT" && python3 City/Tools/district_layout_check.py ) >"$LOG1" 2>&1; then
        objects=$(grep -m 1 '^objects' "$LOG1" | awk '{print $2}')
        deliveries=$(grep -m 1 '^deliveries' "$LOG1" | awk '{print $2}')
        unreachable=$(grep -m 1 '^unreachable' "$LOG1" | awk '{print $2}')
        record "PASS" 1 "Python-генератор District750" "objects=$objects deliveries=$deliveries unreachable=$unreachable"
    else
        record "FAIL" 1 "Python-генератор District750" "см. $(basename "$LOG1")"
    fi
fi

# =====================================================================
section "Этап 2. Структура Unity-проекта"
if [ -z "$UNITY_PROJECT" ]; then
    record "NOT RUN" 2 "Структура Unity-проекта" "не передан --unity-project"
elif [ ! -d "$UNITY_PROJECT/Assets" ] || [ ! -d "$UNITY_PROJECT/ProjectSettings" ]; then
    record "FAIL" 2 "Структура Unity-проекта" "нет Assets/ или ProjectSettings/ в $UNITY_PROJECT"
else
    LOG2="$REPORT_DIR/stage2-project-structure.log"
    : >"$LOG2"
    missing=0
    for rel in \
        "Assets/City/Quarter/City.Quarter.asmdef" \
        "Assets/City/Quarter/QuarterGenerator.cs" \
        "Assets/City/Quarter/DistrictMap/DistrictMapGenerator.cs" \
        "Assets/City/UnityAdapter/City.Quarter.UnityAdapter.asmdef" \
        "Assets/City/UnityAdapter/CityBootstrap.cs" \
        "Assets/City/UnityAdapter/QuarterUnityAdapter.cs" \
        "Assets/City/Tests/City.Quarter.Tests.asmdef" \
        "Assets/City/PauseMenu/LifeFromZero.City.Pause.asmdef" \
        "Assets/City/PauseMenu/PauseController.cs" \
        "Assets/City/PauseMenu/PauseView.cs" \
        "Assets/City/PauseMenu/PauseContracts.cs" \
        "Assets/City/PauseMenu/StandalonePauseIntegration.cs" \
        "Assets/City/PauseInputSystem/PauseKeyboardInput.cs" \
        "Assets/City/PauseTests/LifeFromZero.City.Pause.Tests.asmdef" \
        "Assets/City/PauseTests/PauseTests.cs" \
        "Assets/City/UnityIntegration/Editor/LifeFromZeroSetup.cs" ; do
        if [ -f "$UNITY_PROJECT/$rel" ]; then
            printf 'OK   %s\n' "$rel" >>"$LOG2"
        else
            printf 'MISS %s\n' "$rel" >>"$LOG2"
            missing=$((missing + 1))
        fi
    done
    if [ -f "$UNITY_PROJECT/ProjectSettings/ProjectVersion.txt" ]; then
        printf 'VERSION %s\n' "$(head -n 1 "$UNITY_PROJECT/ProjectSettings/ProjectVersion.txt")" >>"$LOG2"
    fi
    # .meta появляются после импорта в Unity — их отсутствие до открытия Editor не ошибка.
    meta_missing=0
    while IFS= read -r f; do
        [ -n "$f" ] || continue
        [ -f "$f.meta" ] || meta_missing=$((meta_missing + 1))
    done <<EOF
$(find "$UNITY_PROJECT/Assets/City" -name '*.cs' 2>/dev/null)
EOF
    if [ "$missing" -eq 0 ]; then
        record "PASS" 2 "Структура Unity-проекта" "все 16 файлов на месте; .cs без .meta: $meta_missing (Unity создаст при импорте)"
    else
        record "FAIL" 2 "Структура Unity-проекта" "отсутствует файлов: $missing (см. $(basename "$LOG2"))"
    fi
fi

# =====================================================================
section "Этап 3. C# build (dotnet)"
if ! command -v dotnet >/dev/null 2>&1; then
    record "NOT RUN" 3 "C# build через dotnet" "dotnet не найден в PATH (SDK не устанавливаем)"
elif [ ! -f "$REPO_ROOT/City/Tests/Console.csproj" ]; then
    record "NOT RUN" 3 "C# build через dotnet" "нет City/Tests/Console.csproj"
else
    LOG3="$REPORT_DIR/stage3-dotnet-build.log"
    if ( cd "$REPO_ROOT" && dotnet build City/Tests/Console.csproj --nologo ) >"$LOG3" 2>&1; then
        record "PASS" 3 "C# build через dotnet" "сборка City/Tests/Console.csproj успешна"
    else
        record "FAIL" 3 "C# build через dotnet" "см. $(basename "$LOG3")"
    fi
fi

# =====================================================================
# Поиск Unity
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
if UNITY_BIN="$(find_unity)"; then
    : # найден
else
    UNITY_BIN=""
fi

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
section "Этап 4. EditMode-тесты города"
run_unity_tests "EditMode" 4 "EditMode-тесты города"

# =====================================================================
section "Этап 5. PlayMode-тесты меню паузы"
run_unity_tests "PlayMode" 5 "PlayMode-тесты меню паузы"

# =====================================================================
section "Этап 6. Критические ошибки Unity Console"
LOG6="$REPORT_DIR/stage6-console-scan.log"
: >"$LOG6"
scan_targets=""
for f in "$REPORT_DIR"/stage4-*.log "$REPORT_DIR"/stage5-*.log; do
    [ -f "$f" ] && scan_targets="$scan_targets$f
"
done
[ -n "$EXTRA_LOG" ] && [ -f "$EXTRA_LOG" ] && scan_targets="$scan_targets$EXTRA_LOG
"

if [ -z "$scan_targets" ]; then
    record "NOT RUN" 6 "Критические ошибки Unity Console" "нет логов Unity (этапы 4–5 не запускались, --log не передан)"
else
    found=0
    while IFS= read -r f; do
        [ -n "$f" ] || continue
        printf '=== %s\n' "$f" >>"$LOG6"
        if grep -nE 'error CS[0-9]{4}|NullReferenceException|MissingReferenceException|Assembly has reference to non-existent assembly|Scripts have compiler errors' "$f" >>"$LOG6" 2>/dev/null; then
            found=1
        fi
    done <<EOF
$scan_targets
EOF
    if [ "$found" -eq 0 ]; then
        record "PASS" 6 "Критические ошибки Unity Console" "совпадений нет ($(basename "$LOG6"))"
    else
        record "FAIL" 6 "Критические ошибки Unity Console" "найдены совпадения, см. $(basename "$LOG6")"
    fi
fi

# =====================================================================
section "Этап 7. Сохранение результатов"
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
    record "PASS" 7 "Сохранение результатов" "$REPORT_MD"
else
    record "FAIL" 7 "Сохранение результатов" "не удалось записать $REPORT_MD"
fi

# =====================================================================
printf '\n================ ИТОГ ================\n'
printf 'PASS: %s   FAIL: %s   NOT RUN: %s\n' "$PASSES" "$FAILS" "$NOTRUN"
printf 'Отчёт: %s\n' "$REPORT_MD"
if [ "$NOTRUN" -gt 0 ]; then
    printf 'ВНИМАНИЕ: NOT RUN — это НЕ успешная проверка. Незапущенные этапы нужно выполнить вручную.\n'
fi
[ "$FAILS" -gt 0 ] && exit 1
exit 0
