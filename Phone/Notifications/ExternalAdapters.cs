// Life From Zero — Phone notifications (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Integrations
//
// Интерфейсы-адаптеры к существующим системам Life From Zero.
// ВАЖНО: все они ТОЛЬКО ЧИТАЮТ данные. Телефон не меняет деньги, состояние
// заказов, долги и сюжетные флаги — он их отображает и уведомляет о них.

namespace LifeFromZero.Phone.Integrations
{
    /// <summary>
    /// Баланс для приложения «Банк». Реализуется адаптером поверх MoneyWallet.
    /// Телефон не списывает и не начисляет деньги.
    /// </summary>
    public interface IBalanceSource
    {
        /// <summary>
        /// Читает баланс в целых рублях. При ошибке возвращает <c>false</c>;
        /// телефон покажет «баланс недоступен» и не упадёт.
        /// </summary>
        bool TryGetBalanceRubles(out long rubles);
    }

    /// <summary>Краткое состояние курьерского заказа для витрины «Работа».</summary>
    public enum DeliveryJobPhase
    {
        /// <summary>Заказ доступен.</summary>
        Available = 0,

        /// <summary>Заказ принят.</summary>
        Accepted = 1,

        /// <summary>Груз получен.</summary>
        PickedUp = 2,

        /// <summary>Заказ завершён.</summary>
        Completed = 3,

        /// <summary>Заказ провален.</summary>
        Failed = 4,

        /// <summary>Заказ отменён.</summary>
        Cancelled = 5
    }

    /// <summary>Снимок заказа доставки (DTO): телефон не хранит и не меняет заказы.</summary>
    public sealed class DeliveryJobDto
    {
        public string OrderId { get; }

        public string CargoTitle { get; }

        public long PaymentRubles { get; }

        public DeliveryJobPhase Phase { get; }

        public DeliveryJobDto(string orderId, string cargoTitle, long paymentRubles, DeliveryJobPhase phase)
        {
            OrderId = orderId;
            CargoTitle = cargoTitle;
            PaymentRubles = paymentRubles;
            Phase = phase;
        }
    }

    /// <summary>
    /// Источник данных о курьерских заказах. Реализуется адаптером поверх модуля
    /// Jobs/Delivery. Телефон не принимает и не завершает заказы.
    /// </summary>
    public interface IDeliveryJobsSource
    {
        /// <summary>Список заказов для витрины. При ошибке — <c>false</c>.</summary>
        bool TryGetOrders(out DeliveryJobDto[] orders);

        /// <summary>ID активного заказа или null. При ошибке — <c>false</c>.</summary>
        bool TryGetActiveOrderId(out string orderId);
    }

    /// <summary>Краткое описание долга для приложения «Банк».</summary>
    public sealed class DebtDto
    {
        public string Id { get; }

        public string Title { get; }

        public long AmountRubles { get; }

        /// <summary>Игровое время, до которого нужно оплатить (в минутах), или -1, если срок не задан.</summary>
        public double DueAtMinutes { get; }

        public DebtDto(string id, string title, long amountRubles, double dueAtMinutes)
        {
            Id = id;
            Title = title;
            AmountRubles = amountRubles;
            DueAtMinutes = dueAtMinutes;
        }
    }

    /// <summary>
    /// Источник данных о долгах (будущая система долгов). Телефон только отображает
    /// и уведомляет; погашение долгов делает банковская система, не телефон.
    /// </summary>
    public interface IDebtsSource
    {
        bool TryGetDebts(out DebtDto[] debts);
    }

    /// <summary>Запись сюжетного журнала для телефона.</summary>
    public sealed class StoryEntryDto
    {
        public string Id { get; }

        public string Title { get; }

        public string Text { get; }

        public StoryEntryDto(string id, string title, string text)
        {
            Id = id;
            Title = title;
            Text = text;
        }
    }

    /// <summary>
    /// Источник сюжетных записей (будущая система сюжетных событий).
    /// Телефон не устанавливает и не сбрасывает сюжетные флаги.
    /// </summary>
    public interface IStorySource
    {
        bool TryGetEntries(out StoryEntryDto[] entries);
    }
}
