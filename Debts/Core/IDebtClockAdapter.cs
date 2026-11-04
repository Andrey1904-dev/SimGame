// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core

namespace LifeFromZero.Debts.Core
{
    /// <summary>
    /// Игровое время в целых минутах с начала игры. Реализуется адаптером поверх
    /// существующего GameClock; модуль НЕ содержит собственных часов.
    /// Целые минуты (а не типы с плавающей точкой) дают точные границы периодов
    /// и проверяемые скачки времени.
    /// Контракт: значение монотонно не убывает и неотрицательно.
    /// </summary>
    public interface IDebtClockAdapter
    {
        /// <summary>Текущее игровое время в минутах. Отрицательное значение считается сбоем адаптера.</summary>
        long NowMinutes { get; }
    }
}
