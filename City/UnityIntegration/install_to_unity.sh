#!/usr/bin/env bash
# Life From Zero — City/UnityIntegration/install_to_unity.sh (ТЗ #038 + интеграция UI PR #14 + PR #15)
#
# Безопасный установщик модулей SimGame (City + UI + Economy + Jobs.Delivery + Phone + Debts +
# UnityIntegrationKit) в СУЩЕСТВУЮЩИЙ проект Unity 6000.3.27f1.
# Ничего не удаляет в Assets/, не трогает сцены/префабы/ProjectSettings/Packages/.meta,
# не создаёт дубликатов .asmdef (при наличии одноимённой сборки в другом подкаталоге Assets/
# обновляет её по месту) и перед --overwrite сохраняет резервную копию заменяемых файлов в
# <проект>/LifeFromZeroIntegration/backups/<UTC-штамп>/.
#
# Использование:
#   bash City/UnityIntegration/install_to_unity.sh "<путь к Unity-проекту>" [опции]
#
# Опции:
#   --dry-run            Только показать план (режим по умолчанию).
#   --apply              Выполнить копирование. НЕ разрешает перезапись конфликтов.
#   --overwrite          Явное разрешение перезаписать ОТЛИЧАЮЩИЕСЯ файлы (с предварительным бэкапом).
#   --with-domain-tests  Дополнительно установить EditMode-тесты Economy/Jobs/Phone/Debts (по умолчанию выкл.).
#   --help               Эта справка.
#
# Коды завершения:
#   0  успех (dry-run: конфликтов нет; --apply: скопировано без конфликтов)
#   1  ошибка копирования / внутренняя ошибка
#   2  ошибка аргументов
#   3  неверный путь к Unity-проекту (нет Assets/ или ProjectSettings/)
#   4  отсутствуют исходные модули/файлы в репозитории
#   5  обнаружены конфликты или дубликаты .asmdef (ничего не скопировано)

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
    sed -n '2,28p' "$SCRIPT_SOURCE" | sed 's/^# \{0,1\}//'
    exit "$EXIT_OK"
}

# ---------- аргументы ----------
PROJECT_ARG=""
MODE="dry-run"
ALLOW_OVERWRITE="no"
WITH_DOMAIN_TESTS="no"

while [ "$#" -gt 0 ]; do
    case "$1" in
        --dry-run)             MODE="dry-run" ;;
        --apply)               MODE="apply" ;;
        --overwrite)           ALLOW_OVERWRITE="yes" ;;
        --with-domain-tests)   WITH_DOMAIN_TESTS="yes" ;;
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

# ---------- модули установки ----------
# Формат: ID|SRC_BASE|DEFAULT_DST_BASE|ASMDEF_OR_ANCHOR|SUBDIRS_CSV
# SUBDIRS_CSV = "." означает весь каталог (кроме Tools/bin/obj); иначе перечислены подкаталоги + корневой .asmdef.
MODULES="city_quarter|City/Quarter|Assets/City/Quarter|asmdef:LifeFromZero.City.Quarter|.
city_adapter|City/UnityAdapter|Assets/City/UnityAdapter|asmdef:LifeFromZero.City.Quarter.UnityAdapter|.
city_tests|City/Tests|Assets/City/Tests|asmdef:LifeFromZero.City.Quarter.Tests|.
city_pause|City/PauseMenu|Assets/City/PauseMenu|asmdef:LifeFromZero.City.Pause|.
city_pause_input|City/PauseInputSystem|Assets/City/PauseInputSystem|anchor:PauseKeyboardInput.cs|.
city_pause_tests|City/PauseTests|Assets/City/PauseTests|asmdef:LifeFromZero.City.Pause.Tests|.
city_int_editor|City/UnityIntegration/Editor|Assets/City/UnityIntegration/Editor|anchor:LifeFromZeroGameIntegration.cs|.
city_int_runtime|City/UnityIntegration/Runtime|Assets/City/UnityIntegration/Runtime|anchor:PlayerHomeAnchor.cs|.
ui_core|UI/Core|Assets/UI/Core|asmdef:LifeFromZero.UI.Core|.
ui_runtime|UI/Runtime|Assets/UI/Runtime|asmdef:LifeFromZero.UI|.
ui_editor|UI/Editor|Assets/UI/Editor|asmdef:LifeFromZero.UI.Editor|.
ui_tests|UI/Tests|Assets/UI/Tests|asmdef:LifeFromZero.UI.Tests|.
economy|Economy|Assets/_Project/SimGame/Economy|asmdef:LifeFromZero.Economy|Core,Shop
jobs_delivery|Jobs/Delivery|Assets/_Project/SimGame/Jobs/Delivery|asmdef:LifeFromZero.Jobs.Delivery|.
phone|Phone|Assets/_Project/SimGame/Phone|asmdef:LifeFromZero.Phone|Apps,Contacts,Core,Integrations,Messages,Notifications
debts|Debts|Assets/_Project/SimGame/Debts|asmdef:LifeFromZero.Debts|Core,Events,Payments,Penalties,Persistence
uikit|UnityIntegrationKit|Assets/_Project/SimGame/UnityIntegrationKit|anchor:AdapterBridges.cs|."

if [ "$WITH_DOMAIN_TESTS" = "yes" ]; then
    MODULES="$MODULES
economy_tests|Economy/Tests|Assets/_Project/SimGame/Economy/Tests|asmdef:LifeFromZero.Economy.Tests|.
jobs_tests|Jobs/Tests|Assets/_Project/SimGame/Jobs/Tests|asmdef:LifeFromZero.Jobs.Tests|.
phone_tests|Phone/Tests|Assets/_Project/SimGame/Phone/Tests|asmdef:LifeFromZero.Phone.Tests|.
debts_tests|Debts/Tests|Assets/_Project/SimGame/Debts/Tests|asmdef:LifeFromZero.Debts.Tests|."
fi

list_module_sources() {
    # $1 = src_base, $2 = subdirs_csv
    local src_base="$1" subdirs="$2"
    local src_abs="$REPO_ROOT/$src_base"
    if [ "$subdirs" = "." ]; then
        find "$src_abs" -type f \( -name '*.cs' -o -name '*.asmdef' \) \
            ! -path '*/Tools/*' ! -path '*/bin/*' ! -path '*/obj/*' | LC_ALL=C sort
    else
        find "$src_abs" -maxdepth 1 -type f -name '*.asmdef' | LC_ALL=C sort
        local IFS=','
        for sub in $subdirs; do
            [ -d "$src_abs/$sub" ] && find "$src_abs/$sub" -type f \( -name '*.cs' -o -name '*.asmdef' \) \
                ! -path '*/Tools/*' ! -path '*/bin/*' ! -path '*/obj/*'
        done | LC_ALL=C sort
    fi
}

# ---------- 1. исходники ----------
step "1. Проверка исходных модулей репозитория"
info "  Репозиторий: $REPO_ROOT"

missing_sources=0
while IFS='|' read -r mod_id src_rel dst_rel locator subdirs; do
    [ -n "$mod_id" ] || continue
    if [ ! -d "$REPO_ROOT/$src_rel" ]; then
        printf '  НЕТ КАТАЛОГА %s\n' "$src_rel"
        missing_sources=1
        continue
    fi
    count="$(list_module_sources "$src_rel" "$subdirs" | grep -c . || true)"
    if [ "${count:-0}" -eq 0 ]; then
        printf '  НЕТ ФАЙЛОВ  %s (*.cs / *.asmdef)\n' "$src_rel"
        missing_sources=1
    else
        ok "$src_rel ($count файлов .cs/.asmdef)"
    fi
done <<EOF
$MODULES
EOF

if [ "$missing_sources" -eq 1 ]; then
    die "$EXIT_SOURCES" "исходные модули не найдены в $REPO_ROOT. Запускайте скрипт из клона SimGame."
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
    warn "проект открыт в Unity (Temp/UnityLockfile). Дождитесь переимпорта перед запуском Play Mode."
fi

# ---------- 3. поиск уже существующих сборок в Assets/ (защита от дубликатов .asmdef) ----------
step "3. Поиск существующих .asmdef и модулей в Assets/"
PLAN_FILE="$(mktemp "${TMPDIR:-/tmp}/lfz_install_plan.XXXXXX")" || die "$EXIT_ERROR" "не удалось создать временный файл"
ASM_INDEX="$(mktemp "${TMPDIR:-/tmp}/lfz_asm_index.XXXXXX")" || die "$EXIT_ERROR" "не удалось создать временный файл"
trap 'rm -f "$PLAN_FILE" "$ASM_INDEX"' EXIT INT TERM

command -v python3 >/dev/null 2>&1 || die "$EXIT_SOURCES" "python3 необходим для проверки дубликатов"
if command -v python3 >/dev/null 2>&1; then
    python3 - "$PROJECT_ROOT" >"$ASM_INDEX" <<'PY'
import glob, json, os, sys
root = sys.argv[1]
assets = os.path.join(root, "Assets")
by_name = {}
for p in sorted(glob.glob(os.path.join(assets, "**/*.asmdef"), recursive=True)):
    try:
        name = json.load(open(p, encoding="utf-8")).get("name", "")
    except Exception as e:
        print(f"Невозможно прочитать asmdef {p}: {e}", file=sys.stderr)
        sys.exit(5)
    if not name:
        continue
    rel_dir = os.path.relpath(os.path.dirname(p), root).replace("\\", "/")
    by_name.setdefault(name, []).append(rel_dir)
for name, dirs in sorted(by_name.items()):
    print("ASM\t" + name + "\t" + "|".join(dirs))
for anchor in ("PauseKeyboardInput.cs", "LifeFromZeroGameIntegration.cs", "PlayerHomeAnchor.cs", "AdapterBridges.cs"):
    hits = [
        os.path.relpath(os.path.dirname(p), root).replace("\\", "/")
        for p in sorted(glob.glob(os.path.join(assets, "**/" + anchor), recursive=True))
    ]
    if hits:
        print("ANCHOR\t" + anchor + "\t" + "|".join(hits))
PY
    [ "$?" -eq 0 ] || die "$EXIT_CONFLICT" "не удалось проверить asmdef в проекте"
fi

DUP_ASM_ERRORS=0
resolve_target_dir() {
    # $1 = default_dst, $2 = locator (asmdef:... | anchor:...)
    local default_dst="$1" locator="$2"
    local kind="${locator%%:*}"
    local key="${locator#*:}"
    local tag="ASM"
    [ "$kind" = "anchor" ] && tag="ANCHOR"
    local line
    line="$(awk -F'\t' -v t="$tag" -v k="$key" '$1==t && $2==k {print $3; exit}' "$ASM_INDEX" 2>/dev/null || true)"
    if [ -z "$line" ]; then
        printf '%s' "$default_dst"
        return 0
    fi
    case "$line" in
        *"|"*)
            printf 'DUP:%s' "$line"
            return 1
            ;;
        *)
            printf '%s' "$line"
            return 0
            ;;
    esac
}

RESOLVED_MODULES=""
while IFS='|' read -r mod_id src_rel default_dst locator subdirs; do
    [ -n "$mod_id" ] || continue
    resolved="$(resolve_target_dir "$default_dst" "$locator")" || {
        conf "в Assets/ уже найдено несколько копий $locator: ${resolved#DUP:}"
        DUP_ASM_ERRORS=$((DUP_ASM_ERRORS + 1))
        continue
    }
    if [ "$resolved" != "$default_dst" ]; then
        warn "$mod_id: найден существующий каталог '$resolved' (вместо '$default_dst') — обновляем на месте без дубликата"
    else
        ok "$mod_id → $resolved"
    fi
    RESOLVED_MODULES="${RESOLVED_MODULES}${mod_id}|${src_rel}|${resolved}|${locator}|${subdirs}
"
done <<EOF
$MODULES
EOF

if [ "$DUP_ASM_ERRORS" -gt 0 ]; then
    die "$EXIT_CONFLICT" "в проекте уже есть дубликаты .asmdef/скриптов ($DUP_ASM_ERRORS). Устраните дубликаты перед установкой."
fi

# ---------- 4. план файлов ----------
step "4. Планируемые операции (режим: $MODE)"

ADD_COUNT=0
SKIP_COUNT=0
CONFLICT_COUNT=0

plan_entry() {
    local src="$1" dst="$2"
    # Дополнительный предохранитель: никогда не пишем за пределы Assets/ и не трогаем .meta/.unity/.prefab
    case "$dst" in
        "$PROJECT_ROOT/Assets/"*.cs|"$PROJECT_ROOT/Assets/"*.asmdef) ;;
        *) die "$EXIT_ERROR" "внутренний предохранитель заблокировал запись вне разрешённых типов/путей: $dst" ;;
    esac
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

while IFS='|' read -r mod_id src_rel dst_rel locator subdirs; do
    [ -n "$mod_id" ] || continue
    src_abs="$REPO_ROOT/$src_rel"
    dst_abs="$PROJECT_ROOT/$dst_rel"
    while IFS= read -r f; do
        [ -n "$f" ] || continue
        rel="${f#"$src_abs"/}"
        plan_entry "$f" "$dst_abs/$rel"
    done <<EOF
$(list_module_sources "$src_rel" "$subdirs")
EOF
done <<EOF
$RESOLVED_MODULES
EOF

info ""
info "  добавить: $ADD_COUNT, пропустить (идентичные): $SKIP_COUNT, конфликтов: $CONFLICT_COUNT"

# ---------- 5. границы родительских сборок ----------
step "5. Проверка родительских .asmdef над Assembly-CSharp модулями"
asmdef_parent=""
for candidate in \
    "$PROJECT_ROOT/Assets"/*.asmdef \
    "$PROJECT_ROOT/Assets/City"/*.asmdef \
    "$PROJECT_ROOT/Assets/_Project"/*.asmdef \
    "$PROJECT_ROOT/Assets/_Project/SimGame"/*.asmdef ; do
    if [ -f "$candidate" ]; then
        asmdef_parent="$candidate"
        break
    fi
done
if [ -n "$asmdef_parent" ]; then
    warn "найден родительский asmdef: ${asmdef_parent#"$PROJECT_ROOT"/}"
    warn "Скрипты без собственного .asmdef (PauseKeyboardInput, PlayerHomeAnchor, District750Link, UnityIntegrationKit) попадут в него вместо Assembly-CSharp. Проверьте ссылки этой сборки."
else
    ok "родительских .asmdef над Assembly-CSharp скриптами не найдено"
fi

# ---------- 6. конфликты ----------
if [ "$CONFLICT_COUNT" -gt 0 ] && [ "$ALLOW_OVERWRITE" != "yes" ]; then
    step "КОНФЛИКТЫ ($CONFLICT_COUNT) — установка остановлена"
    while IFS=$'\t' read -r status src dst; do
        [ "$status" = "CONFLICT" ] && printf '  %s\n' "${dst#"$PROJECT_ROOT"/}"
    done <"$PLAN_FILE"
    info ""
    info "Ничего не скопировано: файлы в проекте отличаются от версии репозитория."
    info "Варианты: (1) разобрать отличия вручную;"
    info "          (2) запустить с --apply --overwrite (перед записью будет создана резервная копия в LifeFromZeroIntegration/backups/)."
    exit "$EXIT_CONFLICT"
elif [ "$CONFLICT_COUNT" -gt 0 ]; then
    warn "включён --overwrite: перечисленные файлы будут сохранены в резервную копию и перезаписаны"
    while IFS=$'\t' read -r status src dst; do
        [ "$status" = "CONFLICT" ] && printf '  ПЕРЕЗАПИСЬ %s\n' "${dst#"$PROJECT_ROOT"/}"
    done <"$PLAN_FILE"
fi

# ---------- 7. dry-run ----------
if [ "$MODE" = "dry-run" ]; then
    step "ИТОГ (dry-run): ничего не изменено"
    info "  Для установки повторите команду с --apply."
    exit "$EXIT_OK"
fi

# Перед каждой записью обязательно выполнить отдельный dry-run с теми же параметрами.
# Это не заменяет проверку плана человеком и полного снимка проекта.
if [ -e "$PROJECT_ROOT/Temp/UnityLockfile" ]; then
    die "$EXIT_PROJECT" "закройте Unity перед копированием"
fi
args=("$PROJECT_ROOT" --dry-run)
[ "$WITH_DOMAIN_TESTS" = "yes" ] && args+=(--with-domain-tests)
[ "$ALLOW_OVERWRITE" = "yes" ] && args+=(--overwrite)
bash "$SCRIPT_SOURCE" "${args[@]}" || die "$EXIT_CONFLICT" "обязательный dry-run не пройден; запись отменена"

# Запрет симлинков в пути назначения: лексическая проверка Assets/ их не выявляет.
while IFS=$'\t' read -r status src dst; do
    [ "$status" = "ADD" ] || [ "$status" = "CONFLICT" ] || continue
    python3 - "$PROJECT_ROOT/Assets" "$dst" <<'PYSAFE' || die "$EXIT_CONFLICT" "симлинк в пути назначения: $dst"
import os, sys
root, path = sys.argv[1:]
p = path
while p != os.path.dirname(p):
    if os.path.islink(p): sys.exit(1)
    if p == root: break
    p = os.path.dirname(p)
else: sys.exit(1)
PYSAFE
done <"$PLAN_FILE"

# ---------- 8. резервная копия конфликтующих файлов (при --overwrite) ----------
BACKUP_DIR=""
if [ "$CONFLICT_COUNT" -gt 0 ]; then
    STAMP="$(date -u '+%Y%m%dT%H%M%SZ')"
    BACKUP_DIR="$PROJECT_ROOT/LifeFromZeroIntegration/backups/install-$STAMP"
    step "6. Резервная копия перед перезаписью: ${BACKUP_DIR#"$PROJECT_ROOT"/}"
    mkdir -p "$BACKUP_DIR" || die "$EXIT_ERROR" "не удалось создать каталог бэкапа $BACKUP_DIR"
    while IFS=$'\t' read -r status src dst; do
        [ "$status" = "CONFLICT" ] || continue
        rel="${dst#"$PROJECT_ROOT"/}"
        b_dst="$BACKUP_DIR/$rel"
        mkdir -p "$(dirname "$b_dst")" || die "$EXIT_ERROR" "не удалось создать каталог в бэкапе для $rel"
        cp -p "$dst" "$b_dst" || die "$EXIT_ERROR" "не удалось сохранить резервную копию $rel"
        if [ -f "$dst.meta" ]; then
            cp -p "$dst.meta" "$b_dst.meta" || die "$EXIT_ERROR" "не удалось сохранить резервную копию $rel.meta"
        fi
        ok "бэкап $rel"
    done <"$PLAN_FILE"
fi

# ---------- 9. копирование ----------
step "7. Копирование"
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
info "  конфликтов (перезаписано):  $CONFLICT_COUNT"
if [ -n "$BACKUP_DIR" ]; then
    info "  резервная копия:            $BACKUP_DIR"
fi
if [ "$failed" -ne 0 ]; then
    info "  ошибок:                     $failed"
    die "$EXIT_ERROR" "копирование завершилось с ошибками — проверьте права доступа и свободное место"
fi
info ""
info "Дальше в Unity 6000.3.27f1:"
info "  1. Дождитесь окончания импорта и компиляции скриптов (0 ошибок в Console)."
info "  2. Интеграция города и паузы: Tools → Life From Zero → Integrate Into Game"
info "  3. Установка UI в игровую сцену: Tools → Life From Zero → UI → Install UI into game"
info "Сцены проекта, ProjectSettings, Packages и существующие .meta этим скриптом не изменялись."
exit "$EXIT_OK"
