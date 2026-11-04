// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Events

namespace LifeFromZero.Debts.Events
{
    /// <summary>
    /// Приёмник событий долгов. Реализуется внешними системами (телефон, сюжет, лог).
    /// Сервис вызывает Publish только после фиксации состояния; исключение обработчика
    /// перехватывается и не отменяет операцию. Обработчик не должен бросать исключений,
    /// но модуль защищён и от этого.
    /// </summary>
    public interface IDebtEventSink
    {
        void Publish(DebtEvent debtEvent);
    }
}
