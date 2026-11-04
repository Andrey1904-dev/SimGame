// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Persistence
//
// DTO снимка состояния. Плоские массивы и примитивы — БЕЗ словарей, интерфейсов
// и вложенных коллекций, чтобы снимок можно было сериализовать Unity JsonUtility
// ([Serializable] + публичные поля). Сам модуль на диск не пишет и JsonUtility не
// вызывает: формат файла выбирает Codex (JsonUtility / System.Text.Json / бинарный).

using System;

namespace LifeFromZero.Debts.Persistence
{
    /// <summary>Кредитор в снимке.</summary>
    [Serializable]
    public sealed class CreditorSnapshot
    {
        public string Id;
        public string Name;
    }

    /// <summary>Долг в снимке (все инварианты проверяет DebtSnapshotValidator).</summary>
    [Serializable]
    public sealed class DebtEntrySnapshot
    {
        public string Id;
        public string CreditorId;
        public string Title;

        public long PrincipalOriginal;
        public long PrincipalRemaining;
        public long PenaltiesAccrued;
        public long PenaltiesRemaining;
        public long PaidTotal;

        public long DueAtMinutes;
        public long GracePeriodMinutes;
        public long CreatedAtMinutes;
        public long ClosedAtMinutes;

        /// <summary>Стадия DebtStatus, сериализуется как int.</summary>
        public int Status;

        public int InterestRateBasisPoints;

        public bool PenaltyEnabled;
        public long PenaltyPeriodMinutes;
        public long PenaltyFixedRubles;
        public int PenaltyRateBasisPoints;
        public long PenaltyMaxTotalRubles;
        public long LastPenaltyPeriodIndex;

        public long ReminderAfterMinutes;
        public long CollectionAfterMinutes;
        public long LegalActionAfterMinutes;

        public bool DueEventFired;
        public bool OverdueEventFired;
    }

    /// <summary>Подтверждённый платёж в снимке.</summary>
    [Serializable]
    public sealed class PaymentRecordSnapshot
    {
        public string PaymentId;
        public string DebtId;
        public long AmountRubles;
        public long PenaltiesPartRubles;
        public long PrincipalPartRubles;
        public long AtMinutes;
    }

    /// <summary>Незавершённая операция в снимке (для безопасного восстановления).</summary>
    [Serializable]
    public sealed class PendingOperationSnapshot
    {
        public string PaymentId;
        public string DebtId;
        public long AmountRubles;

        /// <summary>Фаза DebtOperationPhase: 0 DebitPending, 1 DebitConfirmed, 2 RefundPending.</summary>
        public int Phase;

        public long CreatedAtMinutes;
    }

    /// <summary>Полный снимок состояния модуля долгов.</summary>
    [Serializable]
    public sealed class DebtSnapshot
    {
        /// <summary>Текущая версия схемы снимка.</summary>
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>Последнее обработанное игровое время (защита от повторных начислений после загрузки).</summary>
        public long LastNowMinutes;

        public CreditorSnapshot[] Creditors = new CreditorSnapshot[0];
        public DebtEntrySnapshot[] Debts = new DebtEntrySnapshot[0];
        public PaymentRecordSnapshot[] Payments = new PaymentRecordSnapshot[0];
        public PendingOperationSnapshot[] PendingOperations = new PendingOperationSnapshot[0];
    }
}
