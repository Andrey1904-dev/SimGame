// Life From Zero — Phone core (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Core

namespace LifeFromZero.Phone.Core
{
    /// <summary>
    /// Игровое время для телефона. Реализуется адаптером поверх существующего GameClock.
    /// Телефон НЕ содержит собственных часов и не тикает сам.
    /// </summary>
    /// <remarks>
    /// Адаптер должен возвращать минуты с начала игры (дробные допустимы), монотонно
    /// не убывающие. Если прочитать время не удалось, телефон фиксирует
    /// <see cref="GameMinutes.Unknown"/> и продолжает работу — исключения наружу не выходят.
    /// </remarks>
    public interface IPhoneClock
    {
        /// <summary>Текущее игровое время в минутах.</summary>
        double NowMinutes { get; }
    }
}
