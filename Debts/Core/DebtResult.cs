// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core

namespace LifeFromZero.Debts.Core
{
    /// <summary>Результат операции над долгом (создание, обновление времени, восстановление).</summary>
    public readonly struct DebtResult
    {
        public DebtResultCode Code { get; }

        /// <summary>ID долга, к которому относится операция (может быть null).</summary>
        public string DebtId { get; }

        /// <summary>Диагностическое сообщение для лога, не для UI.</summary>
        public string Message { get; }

        public bool IsSuccess => Code == DebtResultCode.Success;

        public DebtResult(DebtResultCode code, string debtId, string message)
        {
            Code = code;
            DebtId = debtId;
            Message = message;
        }

        public static DebtResult Ok(string debtId)
        {
            return new DebtResult(DebtResultCode.Success, debtId, null);
        }

        public static DebtResult Fail(DebtResultCode code, string debtId, string message)
        {
            return new DebtResult(code, debtId, message);
        }

        public override string ToString()
        {
            return Code + " (debt=" + (DebtId ?? "-") + ")"
                + (string.IsNullOrEmpty(Message) ? string.Empty : ": " + Message);
        }
    }
}
