// Life From Zero — UI/Core (ТЗ №042 §3, §8.3)
// Namespace: LifeFromZero.UI.Core
//
// ЕДИНСТВЕННЫЙ источник состояния интерфейсов. Ни один контроллер не решает сам,
// можно ли открыть окно: он спрашивает у этой машины состояний.
//
// Разрешённые состояния (ТЗ §3):
//   Gameplay            — игра, HUD виден;
//   Phone               — телефон открыт из игры;
//   MapFromGameplay     — карта открыта по M из игры;
//   Pause               — меню паузы;
//   MapFromPause        — карта открыта из паузы (пауза не снимается);
//   PhoneFromPause      — телефон открыт из паузы (возврат в паузу).
//
// Правила переходов зашиты здесь и проверяются тестами. Любая попытка открыть
// второе окно поверх первого отклоняется (см. <see cref="TryOpen"/>).
//
// Модуль без UnityEngine: прогоняется вне Unity.

using System;
using System.Collections.Generic;

namespace LifeFromZero.UI.Core
{
    /// <summary>Основное состояние интерфейса.</summary>
    public enum UiScreen
    {
        Gameplay = 0,
        Phone = 1,
        MapFromGameplay = 2,
        Pause = 3,
        MapFromPause = 4,
        PhoneFromPause = 5,
    }

    /// <summary>Запрос на переход.</summary>
    public enum UiIntent
    {
        /// <summary>Клавиша P.</summary>
        Phone = 0,

        /// <summary>Клавиша M.</summary>
        Map = 1,

        /// <summary>Клавиша Esc.</summary>
        Back = 2,

        /// <summary>Кнопка «Карта» в меню паузы.</summary>
        OpenMapFromPause = 3,

        /// <summary>Кнопка «Телефон» в меню паузы.</summary>
        OpenPhoneFromPause = 4,

        /// <summary>Нажатие «Продолжить игру» / программное закрытие паузы.</summary>
        Resume = 5,
    }

    /// <summary>Что делать с обычным HUD.</summary>
    public enum HudVisibility
    {
        /// <summary>Показывать полностью.</summary>
        Visible = 0,

        /// <summary>Приглушить (телефон), чтобы не мешать, но сохранить ощущение игры.</summary>
        Muted = 1,

        /// <summary>Полностью скрыть (пауза, карта).</summary>
        Hidden = 2,
    }

    /// <summary>Результат попытки перехода.</summary>
    public readonly struct UiTransition
    {
        public UiTransition(bool accepted, UiScreen from, UiScreen to, string reason)
        {
            Accepted = accepted;
            From = from;
            To = to;
            Reason = reason ?? string.Empty;
        }

        public bool Accepted { get; }

        public UiScreen From { get; }

        public UiScreen To { get; }

        public string Reason { get; }

        public bool Changed => Accepted && From != To;

        public override string ToString() =>
            (Accepted ? "OK " : "ОТКАЗ ") + From + " -> " + To + (string.IsNullOrEmpty(Reason) ? "" : " (" + Reason + ")");
    }

    /// <summary>Машина состояний интерфейсов. Один экземпляр на игру.</summary>
    public sealed class UiStateMachine
    {
        private UiScreen _screen = UiScreen.Gameplay;

        public UiScreen Screen => _screen;

        /// <summary>Открыто ли модальное окно (всё, кроме Gameplay).</summary>
        public bool IsModalOpen => _screen != UiScreen.Gameplay;

        /// <summary>Игра на паузе с точки зрения UI (пауза или карта/телефон из паузы).</summary>
        public bool IsPausedContext =>
            _screen == UiScreen.Pause ||
            _screen == UiScreen.MapFromPause ||
            _screen == UiScreen.PhoneFromPause;

        /// <summary>
        /// Куда вернёт Esc из текущего состояния. Из игры Esc ОТКРЫВАЕТ паузу (ТЗ §3, §20),
        /// из любого модального окна — закрывает его на один уровень назад.
        /// </summary>
        public UiScreen BackTarget
        {
            get
            {
                switch (_screen)
                {
                    case UiScreen.Gameplay:
                        return UiScreen.Pause;
                    case UiScreen.Phone:
                    case UiScreen.MapFromGameplay:
                    case UiScreen.Pause:
                        return UiScreen.Gameplay;
                    case UiScreen.MapFromPause:
                    case UiScreen.PhoneFromPause:
                        return UiScreen.Pause;
                    default:
                        return UiScreen.Gameplay;
                }
            }
        }

        /// <summary>Политика HUD для текущего состояния (ТЗ §8.3).</summary>
        public HudVisibility Hud => HudPolicy(_screen);

        /// <summary>Нужен ли свободный курсор и заблокированный игровой ввод.</summary>
        public bool RequiresCursor => IsModalOpen;

        /// <summary>Политика HUD для конкретного состояния. Чистая функция — удобно тестировать.</summary>
        public static HudVisibility HudPolicy(UiScreen screen)
        {
            switch (screen)
            {
                case UiScreen.Gameplay:
                    return HudVisibility.Visible;
                case UiScreen.Phone:
                case UiScreen.PhoneFromPause:
                    return HudVisibility.Muted;
                default:
                    return HudVisibility.Hidden;
            }
        }

        /// <summary>Состояние, в котором HUD должен быть полностью выключен.</summary>
        public static bool HidesHud(UiScreen screen) => HudPolicy(screen) == HudVisibility.Hidden;

        /// <summary>
        /// Пробует выполнить намерение. Возвращает описание перехода; при отказе
        /// состояние не меняется и причина попадает в <see cref="UiTransition.Reason"/>.
        /// </summary>
        public UiTransition Apply(UiIntent intent)
        {
            UiScreen from = _screen;
            UiScreen to;
            switch (intent)
            {
                case UiIntent.Phone:
                    // Из игры — открыть телефон; из телефона — закрыть; из паузы — телефон из паузы.
                    if (from == UiScreen.Gameplay) to = UiScreen.Phone;
                    else if (from == UiScreen.Phone) to = UiScreen.Gameplay;
                    else if (from == UiScreen.Pause) to = UiScreen.PhoneFromPause;
                    else if (from == UiScreen.PhoneFromPause) to = UiScreen.Pause;
                    else return Reject(from, "P не работает, пока открыта карта");
                    break;

                case UiIntent.Map:
                    if (from == UiScreen.Gameplay) to = UiScreen.MapFromGameplay;
                    else if (from == UiScreen.MapFromGameplay) to = UiScreen.Gameplay;
                    else if (from == UiScreen.MapFromPause) to = UiScreen.Pause;
                    else if (from == UiScreen.Pause) to = UiScreen.MapFromPause;
                    else return Reject(from, "В телефоне приложения «Карта» нет (ТЗ §2)");
                    break;

                case UiIntent.Back:
                    to = BackTarget;
                    break;

                case UiIntent.OpenMapFromPause:
                    if (from == UiScreen.Pause) to = UiScreen.MapFromPause;
                    else if (from == UiScreen.MapFromPause) to = UiScreen.Pause;
                    else return Reject(from, "Карта из паузы открывается только из меню паузы");
                    break;

                case UiIntent.OpenPhoneFromPause:
                    if (from == UiScreen.Pause) to = UiScreen.PhoneFromPause;
                    else if (from == UiScreen.PhoneFromPause) to = UiScreen.Pause;
                    else return Reject(from, "Телефон из паузы открывается только из меню паузы");
                    break;

                case UiIntent.Resume:
                    if (IsPausedContext || from == UiScreen.MapFromGameplay) to = UiScreen.Gameplay;
                    else if (from == UiScreen.Phone) to = UiScreen.Gameplay;
                    else return Reject(from, "Нечего продолжать");
                    break;

                default:
                    return Reject(from, "Неизвестное намерение");
            }

            _screen = to;
            return new UiTransition(true, from, to, string.Empty);
        }

        /// <summary>
        /// Принудительно вернуть интерфейс в игру. Нужен при смене сцены и при
        /// аварийном закрытии: пауза не должна «залипать» между сценами.
        /// </summary>
        public UiTransition Reset()
        {
            UiScreen from = _screen;
            _screen = UiScreen.Gameplay;
            return new UiTransition(true, from, UiScreen.Gameplay, "reset");
        }

        private static UiTransition Reject(UiScreen from, string reason) =>
            new UiTransition(false, from, from, reason);

        /// <summary>Человекочитаемые названия состояний — для отчётов и диагностики.</summary>
        public static IReadOnlyList<UiScreen> AllScreens { get; } = new[]
        {
            UiScreen.Gameplay,
            UiScreen.Phone,
            UiScreen.MapFromGameplay,
            UiScreen.Pause,
            UiScreen.MapFromPause,
            UiScreen.PhoneFromPause,
        };
    }
}
