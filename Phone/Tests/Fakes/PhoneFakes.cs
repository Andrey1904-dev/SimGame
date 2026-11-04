// Life From Zero — Phone tests (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Tests.Fakes
//
// Тестовые адаптеры внешних систем. Это НЕ GameClock/MoneyWallet/доставка игры:
// только управляемые значения и сбои.

using System;
using LifeFromZero.Phone.Core;
using LifeFromZero.Phone.Integrations;

namespace LifeFromZero.Phone.Tests.Fakes
{
    /// <summary>Управляемые игровые часы.</summary>
    public sealed class FakePhoneClock : IPhoneClock
    {
        public FakePhoneClock(double nowMinutes)
        {
            NowMinutes = nowMinutes;
        }

        public double NowMinutes { get; set; }

        public bool ThrowOnRead { get; set; }

        double IPhoneClock.NowMinutes
        {
            get
            {
                if (ThrowOnRead)
                {
                    throw new InvalidOperationException("Тестовый сбой часов.");
                }

                return NowMinutes;
            }
        }

        public void Advance(double minutes)
        {
            NowMinutes += minutes;
        }
    }

    /// <summary>Управляемый источник баланса.</summary>
    public sealed class FakeBalanceSource : IBalanceSource
    {
        public long Rubles { get; set; }

        public bool Available { get; set; } = true;

        public bool ThrowOnRead { get; set; }

        public bool TryGetBalanceRubles(out long rubles)
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("Тестовый сбой баланса.");
            }

            rubles = Available ? Rubles : 0L;
            return Available;
        }
    }

    /// <summary>Управляемый источник долгов.</summary>
    public sealed class FakeDebtsSource : IDebtsSource
    {
        public DebtDto[] Debts { get; set; } = new DebtDto[0];

        public bool Available { get; set; } = true;

        public bool ThrowOnRead { get; set; }

        public bool TryGetDebts(out DebtDto[] debts)
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("Тестовый сбой долгов.");
            }

            debts = Available ? Debts : new DebtDto[0];
            return Available;
        }
    }

    /// <summary>Управляемый источник заказов доставки.</summary>
    public sealed class FakeJobsSource : IDeliveryJobsSource
    {
        public DeliveryJobDto[] Orders { get; set; } = new DeliveryJobDto[0];

        public string ActiveOrderId { get; set; }

        public bool Available { get; set; } = true;

        public bool ThrowOnRead { get; set; }

        public bool TryGetOrders(out DeliveryJobDto[] orders)
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("Тестовый сбой заказов.");
            }

            orders = Available ? Orders : new DeliveryJobDto[0];
            return Available;
        }

        public bool TryGetActiveOrderId(out string orderId)
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("Тестовый сбой активного заказа.");
            }

            orderId = Available ? ActiveOrderId : null;
            return Available;
        }
    }

    /// <summary>Управляемый источник сюжетных записей.</summary>
    public sealed class FakeStorySource : IStorySource
    {
        public StoryEntryDto[] Entries { get; set; } = new StoryEntryDto[0];

        public bool ThrowOnRead { get; set; }

        public bool TryGetEntries(out StoryEntryDto[] entries)
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("Тестовый сбой сюжета.");
            }

            entries = Entries;
            return true;
        }
    }
}
