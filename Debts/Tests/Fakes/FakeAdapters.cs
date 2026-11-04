// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Tests.Fakes
//
// Тестовые адаптеры. Это НЕ кошелёк и НЕ часы игры: только управляемые отказы
// и журнал для проверки финансовой логики сервиса долгов.

using System;
using System.Collections.Generic;
using LifeFromZero.Debts.Core;
using LifeFromZero.Debts.Events;
using LifeFromZero.Debts.Payments;

namespace LifeFromZero.Debts.Tests.Fakes
{
    /// <summary>Управляемые игровые часы.</summary>
    public sealed class FakeClock : IDebtClockAdapter
    {
        public long Minutes;

        public bool Throw;

        public long NowMinutes
        {
            get
            {
                if (Throw)
                {
                    throw new InvalidOperationException("Тестовый сбой часов.");
                }

                return Minutes;
            }
        }
    }

    /// <summary>
    /// Управляемый кошелёк с идемпотентным журналом списаний/возвратов по paymentId
    /// и инъекцией сбоев для проверки восстановления платежей.
    /// </summary>
    public sealed class FakeWallet : IDebtWalletAdapter
    {
        private long _balance;
        private readonly Dictionary<string, long> _debits = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly HashSet<string> _refunds = new HashSet<string>(StringComparer.Ordinal);

        public FakeWallet(long balance)
        {
            if (balance < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(balance));
            }

            _balance = balance;
        }

        public long Balance => _balance;

        public int DebitCalls { get; private set; }

        public int RefundCalls { get; private set; }

        public int QueryCalls { get; private set; }

        // --- Инъекция сбоев ---
        public bool ThrowOnBalance { get; set; }

        public bool BalanceUnavailable { get; set; }

        /// <summary>Бросить исключение ДО списания (деньги не тронуты).</summary>
        public bool ThrowOnDebit { get; set; }

        /// <summary>Списать, записать в журнал и БРОСИТЬ исключение (исход неизвестен вызывающему).</summary>
        public bool DebitThenThrow { get; set; }

        /// <summary>Списать, записать в журнал и вернуть false («лгущий» адаптер).</summary>
        public bool DebitThenReturnFalse { get; set; }

        public bool ThrowOnQuery { get; set; }

        /// <summary>Сколько ближайших запросов TryWasDebited вернут false (журнал недоступен).</summary>
        public int QueryFailuresRemaining { get; set; }

        public bool ThrowOnRefund { get; set; }

        /// <summary>Сколько ближайших возвратов вернут false.</summary>
        public int RefundFailuresRemaining { get; set; }

        public bool TryGetBalance(out long rubles)
        {
            if (ThrowOnBalance)
            {
                throw new InvalidOperationException("Тестовый сбой чтения баланса.");
            }

            if (BalanceUnavailable)
            {
                rubles = 0L;
                return false;
            }

            rubles = _balance;
            return true;
        }

        public bool TryDebit(string paymentId, long rubles)
        {
            DebitCalls++;

            if (ThrowOnDebit)
            {
                throw new InvalidOperationException("Тестовый сбой списания.");
            }

            // Идемпотентность по paymentId.
            if (_debits.ContainsKey(paymentId))
            {
                return true;
            }

            if (rubles > _balance)
            {
                return false;
            }

            _balance -= rubles;
            _debits[paymentId] = rubles;

            if (DebitThenThrow)
            {
                throw new InvalidOperationException("Тестовый сбой после списания.");
            }

            if (DebitThenReturnFalse)
            {
                return false;
            }

            return true;
        }

        public bool TryRefund(string paymentId, long rubles)
        {
            RefundCalls++;

            if (ThrowOnRefund)
            {
                throw new InvalidOperationException("Тестовый сбой возврата.");
            }

            if (RefundFailuresRemaining > 0)
            {
                RefundFailuresRemaining--;
                return false;
            }

            // Идемпотентность по paymentId.
            if (_refunds.Contains(paymentId))
            {
                return true;
            }

            if (!_debits.ContainsKey(paymentId))
            {
                return false;
            }

            _refunds.Add(paymentId);
            _balance += rubles;
            return true;
        }

        public bool TryWasDebited(string paymentId, out bool debited)
        {
            QueryCalls++;

            if (ThrowOnQuery)
            {
                throw new InvalidOperationException("Тестовый сбой запроса журнала.");
            }

            if (QueryFailuresRemaining > 0)
            {
                QueryFailuresRemaining--;
                debited = false;
                return false;
            }

            // Чистое состояние: списание было и не возвращено.
            debited = _debits.ContainsKey(paymentId) && !_refunds.Contains(paymentId);
            return true;
        }
    }

    /// <summary>
    /// Кошелёк-обёртка, который перед списанием вызывает внешний код.
    /// Имитирует реентрантность адаптера: сервис долгов вызывается повторно
    /// изнутри TryDebit (например, адаптер дёргает UpdateTime).
    /// </summary>
    public sealed class ReentrantWallet : IDebtWalletAdapter
    {
        private readonly FakeWallet _inner;
        private readonly Action _beforeDebit;

        public ReentrantWallet(FakeWallet inner, Action beforeDebit)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _beforeDebit = beforeDebit;
        }

        public long Balance => _inner.Balance;

        public int DebitCalls => _inner.DebitCalls;

        public bool TryGetBalance(out long rubles)
        {
            return _inner.TryGetBalance(out rubles);
        }

        public bool TryDebit(string paymentId, long rubles)
        {
            if (_beforeDebit != null)
            {
                _beforeDebit();
            }

            return _inner.TryDebit(paymentId, rubles);
        }

        public bool TryRefund(string paymentId, long rubles)
        {
            return _inner.TryRefund(paymentId, rubles);
        }

        public bool TryWasDebited(string paymentId, out bool debited)
        {
            return _inner.TryWasDebited(paymentId, out debited);
        }
    }

    /// <summary>Приёмник событий, запоминающий их для проверок.</summary>
    public sealed class RecordingEventSink : IDebtEventSink
    {
        public List<DebtEvent> Events { get; } = new List<DebtEvent>();

        public bool Throw { get; set; }

        public void Publish(DebtEvent debtEvent)
        {
            if (Throw)
            {
                throw new InvalidOperationException("Тестовый сбой обработчика событий.");
            }

            Events.Add(debtEvent);
        }

        public int CountOf(DebtEventType type)
        {
            int count = 0;
            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        public DebtEvent LastOf(DebtEventType type)
        {
            for (int i = Events.Count - 1; i >= 0; i--)
            {
                if (Events[i].Type == type)
                {
                    return Events[i];
                }
            }

            return null;
        }
    }
}
