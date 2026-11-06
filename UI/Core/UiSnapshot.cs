// Life From Zero — UI/Core (ТЗ №042 §8, §9.3, §9.5)
// Namespace: LifeFromZero.UI.Core
//
// Данные интерфейса: то, что HUD и меню паузы показывают игроку.
//
// Принцип (§9.5): числа не выдумываются. Всё, что неизвестно, остаётся null
// и отображается как «Нет данных» либо скрывается. Демонстрационные значения
// из референса сюда не попадают никогда — это гарантируется тестом.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace LifeFromZero.UI.Core
{
    /// <summary>Цель в меню паузы.</summary>
    public sealed class GoalView
    {
        public GoalView(string title, bool complete, string progress = null)
        {
            Title = title ?? string.Empty;
            Complete = complete;
            Progress = progress;
        }

        public string Title { get; }

        public bool Complete { get; }

        /// <summary>Например «0/3». Может быть null.</summary>
        public string Progress { get; }
    }

    /// <summary>Значение показателя 0..100 или «нет данных».</summary>
    public readonly struct PercentValue
    {
        public PercentValue(float? value)
        {
            Value = value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value)
                ? (float?)Math.Max(0f, Math.Min(100f, value.Value))
                : null;
        }

        public float? Value { get; }

        public bool HasValue => Value.HasValue;

        public float Ratio => Value.HasValue ? Value.Value / 100f : 0f;

        public static PercentValue Unknown => new PercentValue(null);

        public static PercentValue From(float? v) => new PercentValue(v);
    }

    /// <summary>Игровые показатели HUD и панели «Статус».</summary>
    public sealed class PlayerStatus
    {
        public PercentValue Health = PercentValue.Unknown;
        public PercentValue Satiety = PercentValue.Unknown;
        public PercentValue Energy = PercentValue.Unknown;
        public PercentValue Mood = PercentValue.Unknown;

        /// <summary>Хоть один показатель известен.</summary>
        public bool AnyKnown =>
            Health.HasValue || Satiety.HasValue || Energy.HasValue || Mood.HasValue;
    }

    /// <summary>Деньги, долг, доход.</summary>
    public sealed class FinanceView
    {
        /// <summary>Рубли. null — нет данных.</summary>
        public long? Money;

        public long? Debt;
        public long? DailyIncome;

        public bool AnyKnown => Money.HasValue || Debt.HasValue || DailyIncome.HasValue;
    }

    /// <summary>День/время/локация.</summary>
    public sealed class ClockView
    {
        /// <summary>«День 3 (Среда)».</summary>
        public string DayLabel;

        /// <summary>«18:42».</summary>
        public string TimeLabel;

        /// <summary>«Квартира №67».</summary>
        public string Location;

        /// <summary>«Северный квартал».</summary>
        public string LocationDetail;

        /// <summary>Час 0..23 и минута 0..59, если известны (для экрана блокировки телефона).</summary>
        public int? Hour;
        public int? Minute;

        /// <summary>Номер дня игры, если известен.</summary>
        public int? DayNumber;
    }

    /// <summary>Полный снимок данных для HUD и меню паузы.</summary>
    public sealed class UiSnapshot
    {
        public ClockView Clock = new ClockView();
        public PlayerStatus Status = new PlayerStatus();
        public FinanceView Finance = new FinanceView();
        public GoalView[] Goals = Array.Empty<GoalView>();

        /// <summary>Текущая цель навигации («Квартира №67», «Заказ №4827») или null.</summary>
        public string Objective;

        /// <summary>Подсказка взаимодействия («E — открыть дверь») или null.</summary>
        public string InteractionHint;

        /// <summary>Строка навигации: «Направо через 120 м», null если маршрута нет.</summary>
        public string NavigationLabel;

        /// <summary>Остаток маршрута в метрах, null если маршрута нет.</summary>
        public float? RemainingDistance;

        /// <summary>До ближайшего поворота, метры.</summary>
        public float? DistanceToManeuver;

        /// <summary>Сколько целей показывать в меню паузы.</summary>
        public const int MaxGoals = 3;

        /// <summary>Обрезает список целей до отображаемого количества.</summary>
        public GoalView[] VisibleGoals()
        {
            if (Goals == null || Goals.Length == 0) return Array.Empty<GoalView>();
            if (Goals.Length <= MaxGoals) return Goals;
            var trimmed = new GoalView[MaxGoals];
            Array.Copy(Goals, trimmed, MaxGoals);
            return trimmed;
        }
    }

    /// <summary>Форматирование значений для интерфейса. Один стиль на весь UI.</summary>
    public static class UiFormat
    {
        public const string Missing = "Нет данных";
        public const string Dash = "—";
        public const string Ruble = "₽";

        /// <summary>«12 450 ₽». null → «Нет данных».</summary>
        public static string Rubles(long? value)
        {
            if (!value.HasValue) return Missing;
            return Grouped(value.Value) + " " + Ruble;
        }

        /// <summary>«+4 200 ₽» / «−35 000 ₽» / «0 ₽».</summary>
        public static string SignedRubles(long? value)
        {
            if (!value.HasValue) return Missing;
            if (value.Value > 0) return "+" + Grouped(value.Value) + " " + Ruble;
            if (value.Value < 0) return "−" + Grouped(-value.Value) + " " + Ruble;
            return "0 " + Ruble;
        }

        /// <summary>Разряды через неразрывный пробел: 35000 → «35 000».</summary>
        public static string Grouped(long value)
        {
            bool negative = value < 0;
            string digits = Math.Abs(value).ToString(CultureInfo.InvariantCulture);
            var chars = new System.Text.StringBuilder(digits.Length + digits.Length / 3);
            int count = 0;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                chars.Insert(0, digits[i]);
                count++;
                if (count % 3 == 0 && i > 0) chars.Insert(0, ' ');
            }
            return negative ? "−" + chars : chars.ToString();
        }

        /// <summary>«100%» или «—».</summary>
        public static string Percent(PercentValue value) =>
            value.HasValue ? ((int)Math.Round(value.Value.Value)).ToString(CultureInfo.InvariantCulture) + "%" : Dash;

        /// <summary>Цели в одну строку: «Забрать заказы в ПВЗ (0/3)».</summary>
        public static string Goal(GoalView goal)
        {
            if (goal == null) return Missing;
            string title = string.IsNullOrWhiteSpace(goal.Title) ? Missing : goal.Title;
            if (string.IsNullOrWhiteSpace(goal.Progress)) return title;
            if (title.IndexOf(goal.Progress, StringComparison.Ordinal) >= 0) return title;
            return title + " (" + goal.Progress + ")";
        }

        /// <summary>Текст или «Нет данных».</summary>
        public static string Text(string value) =>
            string.IsNullOrWhiteSpace(value) ? Missing : value;

        /// <summary>«День 3 (Среда)», если день и номер известны; иначе то, что есть.</summary>
        public static string Day(string dayLabel, int? dayNumber, string weekday)
        {
            if (!string.IsNullOrWhiteSpace(dayLabel)) return dayLabel;
            if (dayNumber.HasValue && !string.IsNullOrWhiteSpace(weekday))
                return "День " + dayNumber.Value.ToString(CultureInfo.InvariantCulture) + " (" + weekday + ")";
            if (dayNumber.HasValue) return "День " + dayNumber.Value.ToString(CultureInfo.InvariantCulture);
            return Missing;
        }

        /// <summary>«18:42» из часа и минуты.</summary>
        public static string Time(int hour, int minute) =>
            hour.ToString("00", CultureInfo.InvariantCulture) + ":" + minute.ToString("00", CultureInfo.InvariantCulture);
    }
}
