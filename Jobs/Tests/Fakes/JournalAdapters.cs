// Life From Zero — Delivery jobs module (ТЗ №021)
// Namespace: LifeFromZero.Jobs.Tests.Fakes
//
// Начисление с журналом операций и инъекцией сбоев. Это не MoneyWallet игры:
// только модель, достаточная чтобы проверить идемпотентность выплаты и исходы сбоев.

using System;
using System.Collections.Generic;
using LifeFromZero.Jobs.Delivery;

namespace LifeFromZero.Jobs.Tests.Fakes
{
    /// <summary>Запись журнала выплат.</summary>
    public sealed class PayoutEntry
    {
        public string OperationId;
        public long Rubles;
        public int CreditCalls;
    }

    /// <summary>
    /// Идемпотентное начисление: повтор с тем же ID не начисляет деньги второй раз.
    /// </summary>
    public sealed class JournalPayouts : IIdempotentPayoutAdapter
    {
        private readonly Dictionary<string, PayoutEntry> _journal =
            new Dictionary<string, PayoutEntry>(StringComparer.Ordinal);

        public long Total { get; private set; }

        public int CreditCalls { get; private set; }

        public int QueryCalls { get; private set; }

        /// <summary>Начислить и бросить исключение (исход неизвестен вызывающему).</summary>
        public bool ThrowAfterCredit { get; set; }

        /// <summary>Бросить исключение до начисления.</summary>
        public bool ThrowBeforeCredit { get; set; }

        /// <summary>Начислить и вернуть false.</summary>
        public bool FalseAfterCredit { get; set; }

        /// <summary>Вернуть true, ничего не начислив (некорректный адаптер).</summary>
        public bool LieSuccess { get; set; }

        /// <summary>Журнал недоступен: WasCredited возвращает false.</summary>
        public bool QueryUnavailable { get; set; }

        public bool EntriesCount(out int count)
        {
            count = _journal.Count;
            return true;
        }

        /// <summary>Legacy-путь вызываться не должен: это ошибка интеграции, а не тест-кейс.</summary>
        public bool TryCredit(long rubles)
        {
            throw new InvalidOperationException("Legacy-начисление вызвано вместо идемпотентного.");
        }

        public bool TryCredit(string operationId, long rubles)
        {
            CreditCalls++;

            if (ThrowBeforeCredit)
            {
                throw new InvalidOperationException("Тестовый сбой начисления до записи.");
            }

            PayoutEntry existing;

            if (_journal.TryGetValue(operationId, out existing))
            {
                existing.CreditCalls++;
                return existing.Rubles == rubles; // конфликт суммы — отказ, повтора начисления нет
            }

            if (LieSuccess)
            {
                // Некорректный адаптер: отвечает успехом, но ничего не начисляет и не пишет.
                return true;
            }

            _journal[operationId] = new PayoutEntry { OperationId = operationId, Rubles = rubles, CreditCalls = 1 };
            Total += rubles;

            if (ThrowAfterCredit)
            {
                throw new InvalidOperationException("Тестовый сбой начисления после записи.");
            }

            if (FalseAfterCredit)
            {
                return false;
            }

            return true;
        }

        public bool WasCredited(string operationId, out bool credited)
        {
            QueryCalls++;

            if (QueryUnavailable)
            {
                credited = false;
                return false;
            }

            credited = !string.IsNullOrEmpty(operationId) && _journal.ContainsKey(operationId);
            return true;
        }

        public bool HasEntry(string operationId)
        {
            return !string.IsNullOrEmpty(operationId) && _journal.ContainsKey(operationId);
        }

        /// <summary>«Потерять» журнал: моделирует перезапуск процесса без сохранения.</summary>
        public void ClearJournal()
        {
            _journal.Clear();
        }
    }
}
