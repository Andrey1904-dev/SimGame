#!/usr/bin/env bash
# Life From Zero — City/UnityIntegration/install_to_unity.sh (ТЗ #038)
#
# Безопасный установщик модулей City в СУЩЕСТВУЮЩИЙ Unity-проект.
# Ничего не удаляет, не перезаписывает отличающиеся файлы, не запускает Unity,
# не использует sudo и не меняет глобальные настройки macOS.
#
# Использование:
#   bash City/UnityIntegration/install_to_unity.sh "<путь к Unity-проекту>" [опции]
#
# Опции:
#   --dry-run     Только показать план (режим по умолчанию).
#   --apply       Выполнить копирование. НЕ разрешает перезапись конфликтов.
#   --overwrite   Явное разрешение перезаписать ОТЛИЧАЮЩИЕСЯ файлы. Использовать
#                 только после ручного разбора списка конфликтов и подтверждения
#                 владельцем проекта.
#   --help        Эта справка.
#
# Коды завершения:
#   0  успех (dry-run: конфликтов нет; --apply: скопировано без конфликтов)
#   1  ошибка копирования / внутренняя ошибка
#   2  ошибка аргументов
#   3  неверный путь к Unity-проекту (нет Assets/ или ProjectSettings/)
#   4  отсутствуют исходные модули/файлы в репозитории
#   5  обнаружены конфликты (ничего не скопировано)
#
# Копируются только *.cs и *.asmdef. Документация, *.csproj, Tools и Templates
# в Unity-проект не попадают. Существующие .meta и GUID не изменяются: уже
# существующие идентичные файлы пропускаются, новые копируются как новые файлы,
# а .meta для них создаёт сама Unity при импорте.

set -u
set -o pipefail

EXIT_OK=0
EXIT_ERROR=1
EXIT_USAGE=2
EXIT_PROJECT=3
EXIT_SOURCES=4
EXIT_CONFLICT=5

SCRIPT_SOURCE="${BASH_SOURCE[0]}"
SCRIPT_DIR="$(cd "$(dirname "$SCRIPT_SOURCE")" && pwd -P)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd -P)"

info() { printf '%s\n' "$*"; }
step() { printf '\n== %s\n' "$*"; }
ok()   { printf '  OK       %s\n' "$*"; }
skip() { printf '  SKIP     %s\n' "$*"; }
conf() { printf '  CONFLICT %s\n' "$*"; }
warn() { printf '  ВНИМАНИЕ %s\n' "$*"; }

die() {
    local code="$1"; shift
    printf '\nОШИБКА: %s\n' "$*" >&2
    exit "$code"
}

usage() {
    sed -n '2,30p' "$SCRIPT_SOURCE" | sed 's/^# \{0,1\}//'
    exit "$EXIT_OK"
}

# ---------- аргументы ----------
PROJECT_ARG=""
MODE="dry-run"
ALLOW_OVERWRITE="no"

while [ "$#" -gt 0 ]; do
    case "$1" in
        --dry-run)             MODE="dry-run" ;;
        --apply)               MODE="apply" ;;
        --overwrite)           ALLOW_OVERWRITE="yes" ;;
        --help|-h)             usage ;;
        -*)                    die "$EXIT_USAGE" "неизвестная опция: $1 (см. --help)" ;;
        *)
            if [ -n "$PROJECT_ARG" ]; then
                die "$EXIT_USAGE" "указано больше одного пути к проекту: '$PROJECT_ARG' и '$1'"
            fi
            PROJECT_ARG="$1"
            ;;
    esac
    shift
done

if [ -z "$PROJECT_ARG" ]; then
    die "$EXIT_USAGE" "не указан путь к Unity-проекту. Пример:
  bash City/UnityIntegration/install_to_unity.sh \"/Users/homepc/Projects/LifeFromZero\" --dry-run"
fi

# ---------- карта установки ----------
# "каталог в репозитории|каталог в Assets/ проекта"
DIR_MAP="City/Quarter|Assets/City/Quarter
City/UnityAdapter|Assets/City/UnityAdapter
City/Tests|Assets/City/Tests
City/PauseMenu|Assets/City/PauseMenu
City/PauseInputSystem|Assets/City/PauseInputSystem
City/PauseTests|Assets/City/PauseTests"

# "файл в репозитории|файл в Assets/ проекта"
FILE_MAP="City/UnityIntegration/Editor/LifeFromZeroSetup.cs|Assets/City/UnityIntegration/Editor/LifeFromZeroSetup.cs"

# ---------- 1. исходники ----------
step "1. Проверка исходных модулей репозитория"
info "  Репозиторий: $REPO_ROOT"

missing_sources=0
while IFS='|' read -r src_rel dst_rel; do
    [ -n "$src_rel" ] || continue
    if [ ! -d "$REPO_ROOT/$src_rel" ]; then
        printf '  НЕТ КАТАЛОГА %s\n' "$src_rel"
        missing_sources=1
        continue
    fi
    count=0
    while IFS= read -r f; do
        count=$((count + 1))
    done <<EOF
$(find "$REPO_ROOT/$src_rel" -type f \( -name '*.cs' -o -name '*.asmdef' \) -print)
EOF
    if [ "$count" -eq 0 ]; then
        printf '  НЕТ ФАЙЛОВ  %s (*.cs / *.asmdef)\n' "$src_rel"
        missing_sources=1
    else
        ok "$src_rel ($count файлов .cs/.asmdef)"
    fi
done <<EOF
$DIR_MAP
EOF

while IFS='|' read -r src_rel dst_rel; do
    [ -n "$src_rel" ] || continue
    if [ -f "$REPO_ROOT/$src_rel" ]; then
        ok "$src_rel"
    else
        printf '  НЕТ ФАЙЛА   %s\n' "$src_rel"
        missing_sources=1
    fi
done <<EOF
$FILE_MAP
EOF

if [ "$missing_sources" -eq 1 ]; then
    die "$EXIT_SOURCES" "исходные модули не найдены в $REPO_ROOT. Запускайте скрипт из клона репозитория SimGame: bash City/UnityIntegration/install_to_unity.sh ..."
fi

# ---------- 2. проект ----------
step "2. Проверка Unity-проекта"
[ -d "$PROJECT_ARG" ] || die "$EXIT_PROJECT" "каталог не найден: $PROJECT_ARG"
PROJECT_ROOT="$(cd "$PROJECT_ARG" && pwd -P)"
info "  Проект: $PROJECT_ROOT"

[ -d "$PROJECT_ROOT/Assets" ] || die "$EXIT_PROJECT" "нет каталога Assets/ в $PROJECT_ROOT — это не Unity-проект"
[ -d "$PROJECT_ROOT/ProjectSettings" ] || die "$EXIT_PROJECT" "нет каталога ProjectSettings/ в $PROJECT_ROOT — это не Unity-проект"
ok "Assets/ и ProjectSettings/ найдены"

if [ -f "$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt" ]; then
    info "  Версия Unity: $(head -n 1 "$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt")"
fi
if [ -e "$PROJECT_ROOT/Temp/UnityLockfile" ]; then
    warn "проект открыт в Unity (Temp/UnityLockfile). Копирование не ломает проект, но Unity переимпортирует файлы; сцену запускайте после импорта."
fi

# ---------- 3. план ----------
step "3. Планируемые операции (режим: $MODE)"

PLAN_FILE="$(mktemp "${TMPDIR:-/tmp}/lfz_install_plan.XXXXXX")" || die "$EXIT_ERROR" "не удалось создать временный файл"
trap 'rm -f "$PLAN_FILE"' EXIT INT TERM

ADD_COUNT=0
SKIP_COUNT=0
CONFLICT_COUNT=0

plan_entry() {
    # $1 = src, $2 = dst
    local src="$1" dst="$2"
    if [ -e "$dst" ]; then
        if [ -f "$dst" ] && cmp -s "$src" "$dst"; then
            skip "${dst#"$PROJECT_ROOT"/} (идентичен, .meta сохраняется)"
            printf 'SKIP\t%s\t%s\n' "$src" "$dst" >>"$PLAN_FILE"
            SKIP_COUNT=$((SKIP_COUNT + 1))
        else
            conf "${dst#"$PROJECT_ROOT"/} (отличается от версии репозитория)"
            printf 'CONFLICT\t%s\t%s\n' "$src" "$dst" >>"$PLAN_FILE"
            CONFLICT_COUNT=$((CONFLICT_COUNT + 1))
        fi
    else
        info "  ADD      ${dst#"$PROJECT_ROOT"/}"
        printf 'ADD\t%s\t%s\n' "$src" "$dst" >>"$PLAN_FILE"
        ADD_COUNT=$((ADD_COUNT + 1))
    fi
}

while IFS='|' read -r src_rel dst_rel; do
    [ -n "$src_rel" ] || continue
    src_abs="$REPO_ROOT/$src_rel"
    dst_abs="$PROJECT_ROOT/$dst_rel"
    while IFS= read -r f; do
        [ -n "$f" ] || continue
        rel="${f#"$src_abs"/}"
        plan_entry "$f" "$dst_abs/$rel"
    done <<EOF
$(find "$src_abs" -type f \( -name '*.cs' -o -name '*.asmdef' \) | LC_ALL=C sort)
EOF
done <<EOF
$DIR_MAP
EOF

while IFS='|' read -r src_rel dst_rel; do
    [ -n "$src_rel" ] || continue
    plan_entry "$REPO_ROOT/$src_rel" "$PROJECT_ROOT/$dst_rel"
done <<EOF
$FILE_MAP
EOF

info ""
info "  добавить: $ADD_COUNT, пропустить (идентичные): $SKIP_COUNT, конфликтов: $CONFLICT_COUNT"

# ---------- 4. границы сборок ----------
step "4. Проверка границ сборок (asmdef)"
asmdef_parent=""
for candidate in "$PROJECT_ROOT/Assets"/*.asmdef "$PROJECT_ROOT/Assets/City"/*.asmdef; do
    if [ -f "$candidate" ]; then
        asmdef_parent="$candidate"
        break
    fi
done
if [ -n "$asmdef_parent" ]; then
    warn "над Assets/City/* найден asmdef: ${asmdef_parent#"$PROJECT_ROOT"/}"
    warn "PauseKeyboardInput.cs попадёт в эту сборку, а не в Assembly-CSharp. Убедитесь, что она ссылается на LifeFromZero.City.Pause, иначе будет CS0246 на PauseController."
else
    ok "над Assets/City нет asmdef — PauseKeyboardInput.cs останется в Assembly-CSharp"
fi

# ---------- 5. конфликты ----------
if [ "$CONFLICT_COUNT" -gt 0 ] && [ "$ALLOW_OVERWRITE" != "yes" ]; then
    step "КОНФЛИКТЫ ($CONFLICT_COUNT) — установка остановлена"
    while IFS=$'\t' read -r status src dst; do
        [ "$status" = "CONFLICT" ] && printf '  %s\n' "${dst#"$PROJECT_ROOT"/}"
    done <"$PLAN_FILE"
    info ""
    info "Ничего не скопировано: файлы в проекте отличаются от версии репозитория."
    info "Варианты: (1) разобрать файлы вручную и привести их к версии репозитория;"
    info "          (2) запустить с --overwrite после явного подтверждения владельцем проекта."
    exit "$EXIT_CONFLICT"
elif [ "$CONFLICT_COUNT" -gt 0 ]; then
    warn "включён --overwrite: перечисленные файлы будут перезаписаны"
    while IFS=$'\t' read -r status src dst; do
        [ "$status" = "CONFLICT" ] && printf '  ПЕРЕЗАПИСЬ %s\n' "${dst#"$PROJECT_ROOT"/}"
    done <"$PLAN_FILE"
fi

# ---------- 6. dry-run ----------
if [ "$MODE" = "dry-run" ]; then
    step "ИТОГ (dry-run): ничего не изменено"
    info "  Для установки повторите команду с --apply."
    exit "$EXIT_OK"
fi

# ---------- 7. копирование ----------
step "5. Копирование"
copied=0
failed=0
while IFS=$'\t' read -r status src dst; do
    case "$status" in
        ADD|CONFLICT) ;;
        *) continue ;;
    esac
    dst_dir="$(dirname "$dst")"
    if ! mkdir -p "$dst_dir"; then
        printf '  ОШИБКА каталог: %s\n' "$dst_dir"
        failed=$((failed + 1))
        continue
    fi
    if cp "$src" "$dst" && cmp -s "$src" "$dst"; then
        copied=$((copied + 1))
        info "  записан  ${dst#"$PROJECT_ROOT"/}"
    else
        printf '  ОШИБКА копирования: %s\n' "$dst"
        failed=$((failed + 1))
    fi
done <"$PLAN_FILE"

step "ИТОГ (apply)"
info "  записано новых/обновлённых: $copied"
info "  пропущено идентичных:       $SKIP_COUNT"
info "  конфликтов:                 $CONFLICT_COUNT"
if [ "$failed" -ne 0 ]; then
    info "  ошибок:                     $failed"
    die "$EXIT_ERROR" "копирование завершилось с ошибками — проверьте права доступа и свободное место"
fi
info ""
info "Дальше: откройте проект в Unity 6000.3.27f1, дождитесь окончания импорта и"
info "запустите Tools → Life From Zero → Setup District750 & Pause."
info "Сцены проекта, настройки и локальные файлы этим скриптом не изменялись."
exit "$EXIT_OK"
