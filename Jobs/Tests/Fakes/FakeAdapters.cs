// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Tests.Fakes
//
// Тестовые адаптеры. Это НЕ GameClock, НЕ MoneyWallet и НЕ система перемещения игры:
// только управляемые значения и счётчики для проверки сервиса доставки.

using System;
using LifeFromZero.Jobs.Delivery;

namespace LifeFromZero.Jobs.Tests.Fakes
{
    /// <summary>Управляемые игровые часы.</summary>
    public sealed class FakeClock : IGameClockAdapter
    {
        public FakeClock(double nowMinutes)
        {
            NowMinutes = nowMinutes;
        }

        /// <summary>Текущее игровое время в минутах; тест двигает его вперёд вручную.</summary>
        public double NowMinutes { get; set; }

        /// <summary>Если true, чтение времени бросает исключение.</summary>
        public bool ThrowOnRead { get; set; }

        double IGameClockAdapter.NowMinutes
        {
            get
            {
                if (ThrowOnRead)
                {
                    throw new InvalidOperationException("Тестовый сбой адаптера времени.");
                }

                return NowMinutes;
            }
        }

        public void Advance(double minutes)
        {
            NowMinutes += minutes;
        }
    }

    /// <summary>Управляемое начисление денег.</summary>
    public sealed class FakeLedger : IPayoutAdapter
    {
        /// <summary>Сколько рублей начислено за всё время теста.</summary>
        public long Credited { get; private set; }

        /// <summary>Сколько раз вызывался <see cref="TryCredit"/>.</summary>
        public int CreditCalls { get; private set; }

        /// <summary>Если задано, <see cref="TryCredit"/> возвращает это значение, не меняя сумму.</summary>
        public bool? CreditOverride { get; set; }

        /// <summary>Если true, <see cref="TryCredit"/> бросает исключение.</summary>
        public bool ThrowOnCredit { get; set; }

        public bool TryCredit(long rubles)
        {
            CreditCalls++;

            if (ThrowOnCredit)
            {
                throw new InvalidOperationException("Тестовый сбой начисления денег.");
            }

            if (CreditOverride.HasValue)
            {
                return CreditOverride.Value;
            }

            if (rubles <= 0L)
            {
                return false;
            }

            Credited += rubles;
            return true;
        }
    }

    /// <summary>Управляемое местоположение игрока.</summary>
    public sealed class FakeLocation : ILocationProvider
    {
        public FakeLocation(string currentLocationId)
        {
            CurrentLocationId = currentLocationId;
        }

        public string CurrentLocationId { get; set; }

        /// <summary>Если true, чтение локации бросает исключение.</summary>
        public bool ThrowOnRead { get; set; }

        string ILocationProvider.CurrentLocationId
        {
            get
            {
                if (ThrowOnRead)
                {
                    throw new InvalidOperationException("Тестовый сбой провайдера местоположения.");
                }

                return CurrentLocationId;
            }
        }

        public void MoveTo(string locationId)
        {
            CurrentLocationId = locationId;
        }
    }
}
