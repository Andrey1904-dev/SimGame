# Задача: интеграция District750 + Pause Menu (ТЗ #038)

Локальный Unity-проект: `/Users/homepc/Projects/LifeFromZero` (Unity 6000.3.27f1, URP 17.3.0).
Полный пошаговый сценарий: **`City/CODEX_EXECUTE.md`** — выполняй шаги 1–15 по порядку.

Первая команда:

```bash
cd /Users/homepc/Projects/SimGame
bash City/UnityIntegration/install_to_unity.sh "/Users/homepc/Projects/LifeFromZero" --dry-run
```

Ограничения: не переписывать `Economy`, `Delivery`, `Debts`, `Phone`, `CityTest`,
`Apartment67`, планировку District750 и дизайн меню; не проводить повторный аудит;
не выдумывать результаты проверок; `--overwrite` — только с подтверждения владельца;
из клавиш только `Esc` и `P`.

Готово, когда фактически проверены на Mac: компиляция без `error CS`, зелёные EditMode и
PlayMode-тесты, город (974 объекта, 15 домов, 46 доставок), открытие/закрытие меню с
блокировкой и восстановлением управления, и заполнен отчёт шага 15.
