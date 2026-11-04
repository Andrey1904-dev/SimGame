// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Tests.Fakes
//
// Тестовые реализации интерфейсов-адаптеров. Это НЕ кошелёк и НЕ инвентарь игры:
// только счётчики и управляемые отказы для проверки сервиса магазина.

using System;
using System.Collections.Generic;
using LifeFromZero.Economy.Core;

namespace LifeFromZero.Economy.Tests.Fakes
{
    /// <summary>Управляемый кошелёк для тестов.</summary>
    public sealed class FakeWallet : IWalletAdapter
    {
        private long _rubles;

        public FakeWallet(long rubles)
        {
            if (rubles < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(rubles));
            }

            _rubles = rubles;
        }

        public Money Balance => Money.FromRubles(_rubles);

        /// <summary>Сколько раз вызывался <see cref="TrySpend"/>.</summary>
        public int SpendCalls { get; private set; }

        /// <summary>Сколько раз вызывался <see cref="TryRefund"/>.</summary>
        public int RefundCalls { get; private set; }

        /// <summary>Если задано, <see cref="TrySpend"/> возвращает это значение, не меняя баланс.</summary>
        public bool? SpendOverride { get; set; }

        /// <summary>Если задано, <see cref="TryRefund"/> возвращает это значение, не меняя баланс.</summary>
        public bool? RefundOverride { get; set; }

        /// <summary>Если true, <see cref="TrySpend"/> бросает исключение.</summary>
        public bool ThrowOnSpend { get; set; }

        /// <summary>Если true, <see cref="TryRefund"/> бросает исключение.</summary>
        public bool ThrowOnRefund { get; set; }

        /// <summary>
        /// Сколько ближайших вызовов <see cref="TryRefund"/> должны вернуть false
        /// (имитация временного сбоя: следующий возврат проходит).
        /// </summary>
        public int RefundFailuresRemaining { get; set; }

        /// <summary>Сколько ближайших вызовов <see cref="TryRefund"/> должны бросить исключение.</summary>
        public int RefundThrowsRemaining { get; set; }

        public bool TrySpend(Money amount)
        {
            SpendCalls++;

            if (ThrowOnSpend)
            {
                throw new InvalidOperationException("Тестовый сбой кошелька при списании.");
            }

            if (SpendOverride.HasValue)
            {
                return SpendOverride.Value;
            }

            if (amount.Rubles > _rubles)
            {
                return false;
            }

            _rubles -= amount.Rubles;
            return true;
        }

        public bool TryRefund(Money amount)
        {
            RefundCalls++;

            if (RefundThrowsRemaining > 0)
            {
                RefundThrowsRemaining--;
                throw new InvalidOperationException("Тестовый сбой кошелька при возврате.");
            }

            if (ThrowOnRefund)
            {
                throw new InvalidOperationException("Тестовый сбой кошелька при возврате.");
            }

            if (RefundFailuresRemaining > 0)
            {
                RefundFailuresRemaining--;
                return false;
            }

            if (RefundOverride.HasValue)
            {
                return RefundOverride.Value;
            }

            _rubles += amount.Rubles;
            return true;
        }
    }

    /// <summary>Управляемый инвентарь для тестов.</summary>
    public sealed class FakeInventory : IInventoryAdapter
    {
        private readonly Dictionary<string, int> _items = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Максимум единиц одного предмета; <see cref="int.MaxValue"/> — без лимита.</summary>
        public int CapacityPerItem { get; set; } = int.MaxValue;

        /// <summary>Сколько раз вызывался <see cref="CanApprove"/>.</summary>
        public int CanApproveCalls { get; private set; }

        /// <summary>Сколько раз вызывался <see cref="TryAdd"/>.</summary>
        public int TryAddCalls { get; private set; }

        /// <summary>Если задано, <see cref="TryAdd"/> возвращает это значение, не меняя содержимое.</summary>
        public bool? TryAddOverride { get; set; }

        /// <summary>Если true, <see cref="TryAdd"/> бросает исключение.</summary>
        public bool ThrowOnAdd { get; set; }

        /// <summary>Если true, <see cref="CanApprove"/> бросает исключение.</summary>
        public bool ThrowOnCanApprove { get; set; }

        public int GetCount(string itemId)
        {
            return itemId != null && _items.TryGetValue(itemId, out int count) ? count : 0;
        }

        public bool CanApprove(string itemId, int quantity)
        {
            CanApproveCalls++;

            if (ThrowOnCanApprove)
            {
                throw new InvalidOperationException("Тестовый сбой инвентаря при проверке места.");
            }

            return WouldFit(itemId, quantity);
        }

        public bool TryAdd(string itemId, int quantity)
        {
            TryAddCalls++;

            if (ThrowOnAdd)
            {
                throw new InvalidOperationException("Тестовый сбой инвентаря при добавлении.");
            }

            if (TryAddOverride.HasValue)
            {
                return TryAddOverride.Value;
            }

            if (!WouldFit(itemId, quantity))
            {
                return false;
            }

            _items[itemId] = GetCount(itemId) + quantity;
            return true;
        }

        private bool WouldFit(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
            {
                return false;
            }

            long current = GetCount(itemId);
            return current + quantity <= CapacityPerItem;
        }
    }
}
