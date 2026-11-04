// Life From Zero — Economy module (ТЗ №021)
// Namespace: LifeFromZero.Economy.Tests.Fakes
//
// Тестовые адаптеры с НАСТОЯЩИМ журналом операций и инъекцией сбоев.
// Это не кошелёк и не инвентарь игры: только достаточная модель, чтобы проверить
// идемпотентность, неоднозначные исходы и восстановление после сбоя.
//
// Журнал можно «уронить» (ClearJournal) — так моделируется потеря памяти процесса
// и проверяется, что сервис не считает неизвестный исход отказом.

using System;
using System.Collections.Generic;
using LifeFromZero.Economy.Core;

namespace LifeFromZero.Economy.Tests.Fakes
{
    /// <summary>Запись журнала кошелька.</summary>
    public sealed class JournalEntry
    {
        public string OperationId;
        public WalletOperationKind Kind;
        public long AmountRubles;
        public WalletOperationStatus Status;
        public bool Refunded;
    }

    /// <summary>
    /// Идемпотентный кошелёк с журналом и управляемыми сбоями.
    /// </summary>
    public sealed class JournalWallet : IIdempotentWalletAdapter
    {
        private readonly Dictionary<string, JournalEntry> _journal =
            new Dictionary<string, JournalEntry>(StringComparer.Ordinal);

        private readonly Dictionary<string, JournalEntry> _refunds =
            new Dictionary<string, JournalEntry>(StringComparer.Ordinal);

        private long _balance;

        public JournalWallet(long balance)
        {
            if (balance < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(balance));
            }

            _balance = balance;
            IsDurable = true;
        }

        /// <summary>Журнал переживает «перезапуск» (по умолчанию true).</summary>
        public bool IsDurable { get; set; }

        public long BalanceRubles => _balance;

        public Money Balance => Money.FromRubles(_balance);

        public int SpendCalls { get; private set; }

        public int RefundCalls { get; private set; }

        public int QueryCalls { get; private set; }

        /// <summary>Списать и БРОСИТЬ исключение: исход неизвестен вызывающему, но журнал знает правду.</summary>
        public bool ThrowAfterSpend { get; set; }

        /// <summary>Бросить исключение ДО списания.</summary>
        public bool ThrowBeforeSpend { get; set; }

        /// <summary>Списать и вернуть false («лгущий» адаптер).</summary>
        public bool ReturnFalseAfterSpend { get; set; }

        /// <summary>Сколько ближайших возвратов вернут false.</summary>
        public int RefundFailuresRemaining { get; set; }

        /// <summary>Сколько ближайших возвратов бросят исключение.</summary>
        public int RefundThrowsRemaining { get; set; }

        /// <summary>Запрос журнала бросает исключение.</summary>
        public bool ThrowOnQuery { get; set; }

        /// <summary>Журнал недоступен: TryGetOperationState возвращает false.</summary>
        public bool QueryUnavailable { get; set; }

        /// <summary>«Потерять» журнал: моделирует перезапуск процесса без сохранения.</summary>
        public void ClearJournal()
        {
            _journal.Clear();
            _refunds.Clear();
        }

        public bool TrySpend(Money amount)
        {
            return TrySpend("legacy:" + SpendCalls, amount);
        }

        public bool TryRefund(Money amount)
        {
            return TryRefund("legacy-refund:" + RefundCalls, amount);
        }

        public bool TrySpend(string operationId, Money amount)
        {
            SpendCalls++;

            if (ThrowBeforeSpend)
            {
                throw new InvalidOperationException("Тестовый сбой кошелька до списания.");
            }

            JournalEntry existing;

            if (_journal.TryGetValue(operationId, out existing))
            {
                // Повтор: деньги не двигаем, исход прежний.
                return existing.Status == WalletOperationStatus.Succeeded;
            }

            if (amount.Rubles > _balance)
            {
                _journal[operationId] = new JournalEntry
                {
                    OperationId = operationId,
                    Kind = WalletOperationKind.Spend,
                    AmountRubles = amount.Rubles,
                    Status = WalletOperationStatus.Declined
                };

                return false;
            }

            _balance -= amount.Rubles;
            _journal[operationId] = new JournalEntry
            {
                OperationId = operationId,
                Kind = WalletOperationKind.Spend,
                AmountRubles = amount.Rubles,
                Status = WalletOperationStatus.Succeeded
            };

            if (ThrowAfterSpend)
            {
                throw new InvalidOperationException("Тестовый сбой кошелька после списания.");
            }

            if (ReturnFalseAfterSpend)
            {
                return false;
            }

            return true;
        }

        public bool TryRefund(string operationId, Money amount)
        {
            RefundCalls++;

            if (RefundThrowsRemaining > 0)
            {
                RefundThrowsRemaining--;
                throw new InvalidOperationException("Тестовый сбой возврата.");
            }

            if (RefundFailuresRemaining > 0)
            {
                RefundFailuresRemaining--;
                return false;
            }

            JournalEntry existing;

            if (_refunds.TryGetValue(operationId, out existing))
            {
                return true; // повторный возврат: деньги уже начислены, второй раз не начисляем
            }

            // Возврат допустим только по покупке, списание которой записано в журнале:
            // деньги из воздуха не появляются.
            string purchaseId = FinancialOperationId.ExtractPurchaseId(operationId);
            JournalEntry spend;

            if (purchaseId == null
                || !_journal.TryGetValue(FinancialOperationId.ForPurchase(purchaseId), out spend)
                || spend.Status != WalletOperationStatus.Succeeded)
            {
                return false;
            }

            _balance += amount.Rubles;
            _refunds[operationId] = new JournalEntry
            {
                OperationId = operationId,
                Kind = WalletOperationKind.Refund,
                AmountRubles = amount.Rubles,
                Status = WalletOperationStatus.Succeeded,
                Refunded = true
            };

            return true;
        }

        public bool TryGetOperationState(string operationId, out WalletOperationState result)
        {
            QueryCalls++;

            if (ThrowOnQuery)
            {
                throw new InvalidOperationException("Тестовый сбой запроса журнала.");
            }

            if (QueryUnavailable)
            {
                result = WalletOperationState.Unknown;
                return false;
            }

            JournalEntry entry;

            if (!string.IsNullOrEmpty(operationId) && _journal.TryGetValue(operationId, out entry))
            {
                result = new WalletOperationState(entry.Kind, Money.FromRubles(entry.AmountRubles), entry.Status);
                return true;
            }

            if (!string.IsNullOrEmpty(operationId) && _refunds.TryGetValue(operationId, out entry))
            {
                result = new WalletOperationState(entry.Kind, Money.FromRubles(entry.AmountRubles), entry.Status);
                return true;
            }

            result = new WalletOperationState(
                WalletOperationKind.None, Money.Zero, WalletOperationStatus.NotFound);
            return true;
        }

        public bool IsRefunded(string operationId)
        {
            return _refunds.ContainsKey(operationId);
        }

        public bool HasEntry(string operationId)
        {
            return _journal.ContainsKey(operationId) || _refunds.ContainsKey(operationId);
        }
    }

    /// <summary>
    /// Идемпотентный инвентарь: журнал выдачи по operationId, лимит на предмет,
    /// инъекция сбоев после добавления.
    /// </summary>
    public sealed class JournalInventory : IIdempotentInventoryAdapter
    {
        private readonly Dictionary<string, int> _items = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly Dictionary<string, InventoryGrantStatus> _grants =
            new Dictionary<string, InventoryGrantStatus>(StringComparer.Ordinal);

        public JournalInventory()
        {
            CapacityPerItem = int.MaxValue;
            IsDurable = true;
        }

        public int CapacityPerItem { get; set; }

        public bool IsDurable { get; set; }

        public int AddCalls { get; private set; }

        public int CanApproveCalls { get; private set; }

        /// <summary>Добавить и бросить исключение (исход неизвестен вызывающему).</summary>
        public bool ThrowAfterAdd { get; set; }

        /// <summary>Отказать в выдаче (без изменения состояния).</summary>
        public bool RejectAdd { get; set; }

        /// <summary>Журнал выдачи недоступен.</summary>
        public bool GrantJournalUnavailable { get; set; }

        public int GetCount(string itemId)
        {
            return itemId != null && _items.TryGetValue(itemId, out int count) ? count : 0;
        }

        public bool CanApprove(string itemId, int quantity)
        {
            CanApproveCalls++;
            return WouldFit(itemId, quantity);
        }

        public bool TryAdd(string itemId, int quantity)
        {
            return TryAdd("legacy:" + AddCalls, itemId, quantity);
        }

        public bool TryAdd(string operationId, string itemId, int quantity)
        {
            AddCalls++;

            if (RejectAdd || !WouldFit(itemId, quantity))
            {
                _grants[operationId] = InventoryGrantStatus.Declined;
                return false;
            }

            InventoryGrantStatus existing;

            if (_grants.TryGetValue(operationId, out existing) && existing == InventoryGrantStatus.Succeeded)
            {
                return true; // повтор: товар уже выдан, второй раз не добавляем
            }

            _items[itemId] = GetCount(itemId) + quantity;
            _grants[operationId] = InventoryGrantStatus.Succeeded;

            if (ThrowAfterAdd)
            {
                throw new InvalidOperationException("Тестовый сбой инвентаря после добавления.");
            }

            return true;
        }

        public bool TryGetGrantState(string operationId, out InventoryGrantStatus result)
        {
            if (GrantJournalUnavailable)
            {
                result = InventoryGrantStatus.Unknown;
                return false;
            }

            if (!string.IsNullOrEmpty(operationId) && _grants.TryGetValue(operationId, out result))
            {
                return true;
            }

            result = InventoryGrantStatus.NotFound;
            return true;
        }

        /// <summary>«Потерять» журнал выдачи: моделирует перезапуск процесса.</summary>
        public void ClearGrantJournal()
        {
            _grants.Clear();
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
