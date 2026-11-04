// Life From Zero — Economy module (ТЗ №021)
// Namespace: LifeFromZero.Economy.Tests
//
// Регрессионные тесты финансовых контрактов: идентификаторы операций,
// идемпотентность, неоднозначные исходы, сбой между списанием и выдачей,
// перезагрузка снимка и исключения обработчика Changed.

using System;
using System.Collections.Generic;
using LifeFromZero.Economy.Core;
using LifeFromZero.Economy.Shop;
using LifeFromZero.Economy.Tests.Fakes;

namespace LifeFromZero.Economy.Tests
{
    public static class ShopFinancialSafetyTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();

            // Идентификаторы операций.
            suite.Add("ID: списание и возврат выводятся из PurchaseId и не пересекаются с модулями",
                OperationIdsNamespace);
            suite.Add("ID: некорректный PurchaseId отклоняется без движения денег", InvalidPurchaseId);

            // Идемпотентность покупки и возврата.
            suite.Add("Повторная покупка с тем же ID не списывает и не выдаёт повторно", RepeatedPurchaseSameId);
            suite.Add("Повторный возврат по тому же ID не начисляет деньги дважды", RepeatedRefundSameId);
            suite.Add("Недостаток средств: повтор с тем же ID возвращает тот же отказ", InsufficientFundsReplay);
            suite.Add("Два разных ID при недостатке средств не создают денег", TwoIdsNoMoneyCreation);

            // Неоднозначный исход списания.
            suite.Add("Сбой: кошелёк списал и бросил исключение — исход восстановлен по журналу",
                WalletThrowsAfterSpendRecovered);
            suite.Add("Сбой: кошелёк списал и вернул false — исход восстановлен по журналу",
                WalletFalseAfterSpendRecovered);
            suite.Add("Сбой: журнал кошелька недоступен — OutcomeUnknown, деньги не двигаются",
                UnknownOutcomeBlocksAndKeepsJournal);
            suite.Add("Сбой: OutcomeUnknown не блокирует разрешение по журналу (TryResolveUnknown)",
                UnknownOutcomeResolvedLater);
            suite.Add("Сбой: повтор того же ID при неизвестном исходе НЕ списывает снова",
                UnknownOutcomeRepeatKeepsOneSpend);

            // Сбой между списанием и выдачей.
            suite.Add("Сбой между списанием и выдачей: восстановление доигрывает выдачу",
                CrashBetweenDebitAndGrantGrants);
            suite.Add("Сбой между списанием и выдачей: заполненный инвентарь — деньги возвращены",
                CrashBetweenDebitAndGrantRefunds);
            suite.Add("Сбой между списанием и выдачей: возврат идемпотентен при повторном восстановлении",
                CrashRecoveryRefundIdempotent);
            suite.Add("Сбой выдачи: инвентарь добавил и бросил — двойной выдачи нет",
                GrantUncertainNoDoubleGrant);

            // Исключение обработчика Changed.
            suite.Add("Событие Changed: исключение обработчика не ломает покупку", ChangedHandlerExceptionSafe);
            suite.Add("Событие Changed: обработчик не может запустить вторую покупку", ChangedCannotRecursePurchase);
            suite.Add("Событие Changed: успешная покупка уведомляет ровно один раз", ChangedRaisedOnce);

            // Снимок.
            suite.Add("Снимок: повторная загрузка снимка не возвращает деньги второй раз", SnapshotReloadNoDoubleRefund);
            suite.Add("Снимок: повторная загрузка не выдаёт товар второй раз", SnapshotReloadNoDoubleGrant);
            suite.Add("Снимок: валидатор отклоняет дубликат PurchaseId", SnapshotRejectsDuplicateId);
            suite.Add("Снимок: повреждённый снимок не меняет журнал", SnapshotBrokenKeepsState);
            suite.Add("Снимок: две незавершённые операции отклоняются", SnapshotRejectsTwoPending);

            // Совместимость и режимы.
            suite.Add("Совместимость: старая перегрузка без ID работает как раньше", LegacyOverloadStillWorks);
            suite.Add("Режим A: повтор внутри сессии не применяет операцию второй раз",
                SessionModeHonestAboutRestart);
            suite.Add("Режим A: потеря журналов адаптеров не выдумывает исход операции",
                SessionModeUnknownAfterJournalLoss);

            return suite;
        }

        // ---------- helpers ----------

        private static readonly IShopCatalog Catalog = ShopCatalog.CreateDefault();

        private static PurchaseRequest Bread(int quantity)
        {
            return new PurchaseRequest("bread", quantity);
        }

        // ---------- 1. Идентификаторы ----------

        private static void OperationIdsNamespace()
        {
            MiniTestRunner.AreEqual("shop:buy-1", FinancialOperationId.ForPurchase("buy-1"), "ID списания");
            MiniTestRunner.AreEqual("shop-refund:buy-1", FinancialOperationId.ForRefund("buy-1"), "ID возврата");

            MiniTestRunner.IsTrue(FinancialOperationId.TryClassify("shop:buy-1", out FinancialOperationKind spend), "разбор списания");
            MiniTestRunner.AreEqual(FinancialOperationKind.Purchase, spend, "вид операции — списание");

            MiniTestRunner.IsTrue(FinancialOperationId.TryClassify("shop-refund:buy-1", out FinancialOperationKind refund), "разбор возврата");
            MiniTestRunner.AreEqual(FinancialOperationKind.Refund, refund, "вид операции — возврат");

            MiniTestRunner.IsFalse(FinancialOperationId.TryClassify("delivery:o-1", out _), "чужая операция не разбирается");
            MiniTestRunner.IsFalse(FinancialOperationId.TryClassify("debt:p-1", out _), "платёж долга не разбирается");
            MiniTestRunner.AreEqual("buy-1", FinancialOperationId.ExtractPurchaseId("shop-refund:buy-1"), "PurchaseId из возврата");
            MiniTestRunner.IsTrue(FinancialOperationId.IsForeignOperationId("debt:1"), "debt: — чужой префикс");
            MiniTestRunner.IsFalse(FinancialOperationId.IsForeignOperationId("shop:1"), "shop: — свой префикс");
        }

        private static void InvalidPurchaseId()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            PurchaseResult result = shop.Purchase(wallet, inventory, Bread(1), "bad id with spaces");

            MiniTestRunner.AreEqual(EconomyResultCode.InvalidArgument, result.Code, "некорректный ID отклонён");
            MiniTestRunner.AreEqual(0, wallet.SpendCalls, "деньги не трогались");
            MiniTestRunner.AreEqual(0, inventory.AddCalls, "товар не выдавался");
            MiniTestRunner.AreEqual(0, shop.PendingOperationCount, "журнал пуст");
        }

        // ---------- 2. Идемпотентность ----------

        private static void RepeatedPurchaseSameId()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            PurchaseResult first = shop.Purchase(wallet, inventory, Bread(2), "buy-A");
            PurchaseResult second = shop.Purchase(wallet, inventory, Bread(2), "buy-A");
            PurchaseResult third = shop.Purchase(wallet, inventory, Bread(2), "buy-A");

            MiniTestRunner.IsTrue(first.IsSuccess, "первая покупка успешна");
            MiniTestRunner.IsFalse(first.IsReplay, "первый ответ — не повтор");

            MiniTestRunner.IsTrue(second.IsSuccess, "повтор возвращает успех");
            MiniTestRunner.IsTrue(second.IsReplay, "второй ответ помечен как повтор");
            MiniTestRunner.IsTrue(third.IsReplay, "третий ответ — тоже повтор");

            MiniTestRunner.AreEqual(1, wallet.SpendCalls, "списание было ровно одно");
            MiniTestRunner.AreEqual(900L, wallet.BalanceRubles, "списано 100 ₽ один раз");
            MiniTestRunner.AreEqual(2, inventory.GetCount("bread"), "выдано ровно 2 хлеба");
            MiniTestRunner.AreEqual(1, inventory.AddCalls, "выдача была ровно одна");
            MiniTestRunner.AreEqual(0, shop.PendingOperationCount, "журнал пуст");
        }

        private static void RepeatedRefundSameId()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory { RejectAdd = true };
            var shop = new ShopService(Catalog);

            PurchaseResult first = shop.Purchase(wallet, inventory, Bread(1), "buy-R");
            MiniTestRunner.AreEqual(EconomyResultCode.InventoryRejected, first.Code, "товар не выдан, деньги возвращены");
            MiniTestRunner.AreEqual(1000L, wallet.BalanceRubles, "деньги вернулись");
            MiniTestRunner.AreEqual(0, shop.PendingOperationCount, "журнал пуст");

            // Второй и третий возврат по тому же идентификатору не должны начислить деньги.
            bool secondRefund = wallet.TryRefund(FinancialOperationId.ForRefund("buy-R"), Money.FromRubles(50L));
            bool thirdRefund = wallet.TryRefund(FinancialOperationId.ForRefund("buy-R"), Money.FromRubles(50L));

            MiniTestRunner.IsTrue(secondRefund, "идемпотентный возврат отвечает true");
            MiniTestRunner.IsTrue(thirdRefund, "и повторный — тоже true");
            MiniTestRunner.AreEqual(1000L, wallet.BalanceRubles, "баланс не вырос: ровно один возврат");

            PurchaseResult replay = shop.Purchase(wallet, inventory, Bread(1), "buy-R");
            MiniTestRunner.AreEqual(EconomyResultCode.InventoryRejected, replay.Code, "повтор покупки возвращает прежний исход");
            MiniTestRunner.IsTrue(replay.IsReplay, "ответ помечен как повтор");
            MiniTestRunner.AreEqual(1000L, wallet.BalanceRubles, "повтор ничего не начислил");
            MiniTestRunner.AreEqual(1, wallet.SpendCalls, "списание осталось одно");
        }

        private static void InsufficientFundsReplay()
        {
            var wallet = new JournalWallet(30L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            PurchaseResult first = shop.Purchase(wallet, inventory, Bread(1), "buy-N");
            PurchaseResult second = shop.Purchase(wallet, inventory, Bread(1), "buy-N");

            MiniTestRunner.AreEqual(EconomyResultCode.InsufficientFunds, first.Code, "первый отказ");
            MiniTestRunner.AreEqual(EconomyResultCode.InsufficientFunds, second.Code, "повтор — тот же отказ");
            MiniTestRunner.IsTrue(second.IsReplay, "ответ на повтор");
            MiniTestRunner.AreEqual(0, wallet.SpendCalls, "списания не было");
            MiniTestRunner.AreEqual(30L, wallet.BalanceRubles, "баланс цел");
        }

        private static void TwoIdsNoMoneyCreation()
        {
            var wallet = new JournalWallet(30L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            shop.Purchase(wallet, inventory, Bread(1), "buy-1");
            shop.Purchase(wallet, inventory, Bread(1), "buy-2");
            shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 100), "buy-3");

            MiniTestRunner.AreEqual(30L, wallet.BalanceRubles, "деньги не появились и не исчезли");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "товар не выдан");
            MiniTestRunner.AreEqual(0, shop.PendingOperationCount, "журнал пуст");
        }

        // ---------- 3. Неоднозначный исход списания ----------

        private static void WalletThrowsAfterSpendRecovered()
        {
            var wallet = new JournalWallet(1000L) { ThrowAfterSpend = true };
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            PurchaseResult result = shop.Purchase(wallet, inventory, Bread(1), "buy-C");

            MiniTestRunner.IsTrue(result.IsSuccess, "исход восстановлен по журналу: покупка успешна");
            MiniTestRunner.AreEqual(1, wallet.SpendCalls, "списание было ровно одно");
            MiniTestRunner.AreEqual(950L, wallet.BalanceRubles, "списано один раз");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "товар выдан");
            MiniTestRunner.AreEqual(0, shop.PendingOperationCount, "журнал пуст");
        }

        private static void WalletFalseAfterSpendRecovered()
        {
            var wallet = new JournalWallet(1000L) { ReturnFalseAfterSpend = true };
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            PurchaseResult result = shop.Purchase(wallet, inventory, Bread(1), "buy-F");

            MiniTestRunner.IsTrue(result.IsSuccess, "«лгущий» адаптер не обманул: журнал знает правду");
            MiniTestRunner.AreEqual(950L, wallet.BalanceRubles, "списано один раз");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "товар выдан один раз");
        }

        private static void UnknownOutcomeBlocksAndKeepsJournal()
        {
            var wallet = new JournalWallet(1000L) { ThrowAfterSpend = true, QueryUnavailable = true };
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            PurchaseResult result = shop.Purchase(wallet, inventory, Bread(1), "buy-U");

            MiniTestRunner.AreEqual(EconomyResultCode.OutcomeUnknown, result.Code, "исход неизвестен");
            MiniTestRunner.AreEqual(1, shop.PendingOperationCount, "запись осталась в журнале");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "товар пока не выдан");
            MiniTestRunner.AreEqual(950L, wallet.BalanceRubles, "деньги списаны и НЕ возвращены наугад");

            PurchaseResult other = shop.Purchase(wallet, inventory, Bread(1), "buy-U2");
            MiniTestRunner.AreEqual(EconomyResultCode.PendingOperation, other.Code, "новые покупки заблокированы");
            MiniTestRunner.AreEqual(1, wallet.SpendCalls, "второго списания не было");
        }

        private static void UnknownOutcomeResolvedLater()
        {
            var wallet = new JournalWallet(1000L) { ThrowAfterSpend = true, QueryUnavailable = true };
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            shop.Purchase(wallet, inventory, Bread(1), "buy-U");

            MiniTestRunner.IsFalse(shop.TryResolveUnknown(wallet, inventory, "buy-U"), "пока журнал закрыт — не разрешается");

            wallet.QueryUnavailable = false;
            MiniTestRunner.IsTrue(shop.TryResolveUnknown(wallet, inventory, "buy-U"), "журнал ответил — операция разрешена");
            MiniTestRunner.AreEqual(0, shop.PendingOperationCount, "журнал пуст");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "выдача доиграна");
            MiniTestRunner.AreEqual(950L, wallet.BalanceRubles, "деньги списаны ровно один раз");
        }

        private static void UnknownOutcomeRepeatKeepsOneSpend()
        {
            var wallet = new JournalWallet(1000L) { ThrowAfterSpend = true, QueryUnavailable = true };
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            shop.Purchase(wallet, inventory, Bread(1), "buy-U");
            PurchaseResult repeat = shop.Purchase(wallet, inventory, Bread(1), "buy-U");

            MiniTestRunner.AreEqual(EconomyResultCode.OutcomeUnknown, repeat.Code, "повтор честно сообщает неизвестный исход");
            MiniTestRunner.AreEqual(1, wallet.SpendCalls, "списание не повторилось");
            MiniTestRunner.AreEqual(950L, wallet.BalanceRubles, "баланс не изменился");
        }

        // ---------- 4. Сбой между списанием и выдачей ----------

        /// <summary>
        /// Моделирует падение процесса сразу после списания: деньги ушли, товар не выдан,
        /// состояние операции в памяти потеряно. Снимок журнала при этом был сохранён
        /// с состоянием Debited — именно так выглядит реальный сбой.
        /// </summary>
        private static void CrashBetweenDebitAndGrantGrants()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            // Падение: инвентарь бросает исключение после добавления товара,
            // журнал инвентаря сохраняет факт выдачи.
            inventory.ThrowAfterAdd = true;
            shop.Purchase(wallet, inventory, Bread(1), "buy-X");

            // «Перезапуск»: новый сервис, тот же кошелёк и инвентарь (журналы живы),
            // снимок с состоянием Debited.
            var snapshot = new ShopSnapshot
            {
                Operations = new[]
                {
                    new ShopOperationSnapshot
                    {
                        PurchaseId = "buy-X",
                        ProductId = "bread",
                        RequestedQuantity = 1,
                        Units = 1,
                        AmountRubles = 50L,
                        State = (int)ShopOperationState.Debited
                    }
                }
            };

            var restored = new ShopService(Catalog);
            string error;
            MiniTestRunner.IsTrue(restored.RestoreSnapshot(snapshot, out error), "снимок загружен: " + error);

            MiniTestRunner.IsTrue(restored.RetryPendingOperations(wallet, inventory), "операция закрыта");
            MiniTestRunner.AreEqual(0, restored.PendingOperationCount, "журнал пуст");
            MiniTestRunner.AreEqual(950L, wallet.BalanceRubles, "деньги списаны ровно один раз");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "товар выдан ровно один раз (без дубля)");
        }

        private static void CrashBetweenDebitAndGrantRefunds()
        {
            var wallet = new JournalWallet(1000L);
            // Инвентарь принимает товар на проверке, но не может выдать (отказ на TryAdd)
            // и журнал выдачи уже «потерян» — восстановление обязано вернуть деньги.
            var inventory = new JournalInventory { RejectAdd = true };

            // Сбой: деньги списаны, товар принять некуда, состояние Debited в снимке.
            wallet.TrySpend(FinancialOperationId.ForPurchase("buy-Y"), Money.FromRubles(50L));

            var snapshot = new ShopSnapshot
            {
                Operations = new[]
                {
                    new ShopOperationSnapshot
                    {
                        PurchaseId = "buy-Y",
                        ProductId = "bread",
                        RequestedQuantity = 1,
                        Units = 1,
                        AmountRubles = 50L,
                        State = (int)ShopOperationState.Debited
                    }
                }
            };

            var restored = new ShopService(Catalog);
            string error;
            MiniTestRunner.IsTrue(restored.RestoreSnapshot(snapshot, out error), "снимок загружен: " + error);

            MiniTestRunner.IsTrue(restored.RetryPendingOperations(wallet, inventory), "операция закрыта");
            MiniTestRunner.AreEqual(1000L, wallet.BalanceRubles, "деньги вернулись");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "товар не выдан");
            MiniTestRunner.IsTrue(wallet.IsRefunded(FinancialOperationId.ForRefund("buy-Y")), "возврат записан в журнал");
        }

        private static void CrashRecoveryRefundIdempotent()
        {
            var wallet = new JournalWallet(1000L) { RefundFailuresRemaining = 1 };
            var inventory = new JournalInventory { RejectAdd = true };

            wallet.TrySpend(FinancialOperationId.ForPurchase("buy-Z"), Money.FromRubles(50L));

            var snapshot = new ShopSnapshot
            {
                Operations = new[]
                {
                    new ShopOperationSnapshot
                    {
                        PurchaseId = "buy-Z",
                        ProductId = "bread",
                        RequestedQuantity = 1,
                        Units = 1,
                        AmountRubles = 50L,
                        State = (int)ShopOperationState.Debited
                    }
                }
            };

            var restored = new ShopService(Catalog);
            string error;
            restored.RestoreSnapshot(snapshot, out error);

            MiniTestRunner.IsFalse(restored.RetryPendingOperations(wallet, inventory), "первый возврат не удался");
            MiniTestRunner.AreEqual(1, restored.PendingOperationCount, "операция ждёт");
            MiniTestRunner.AreEqual(950L, wallet.BalanceRubles, "деньги ещё не вернулись");

            MiniTestRunner.IsTrue(restored.RetryPendingOperations(wallet, inventory), "повторный возврат успешен");
            MiniTestRunner.IsTrue(restored.RetryPendingOperations(wallet, inventory), "повторный вызов ничего не ломает");
            MiniTestRunner.AreEqual(1000L, wallet.BalanceRubles, "вернули ровно один раз");
            MiniTestRunner.AreEqual(0, restored.PendingOperationCount, "журнал пуст");
        }

        private static void GrantUncertainNoDoubleGrant()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory { ThrowAfterAdd = true };
            var shop = new ShopService(Catalog);

            PurchaseResult first = shop.Purchase(wallet, inventory, Bread(1), "buy-G");

            // Исключение после добавления: журнал инвентаря знает, что товар выдан,
            // поэтому сервис НЕ возвращает деньги и НЕ выдаёт товар второй раз.
            MiniTestRunner.IsTrue(first.IsSuccess, "исход выдачи восстановлен по журналу инвентаря");
            MiniTestRunner.AreEqual(950L, wallet.BalanceRubles, "деньги списаны один раз");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "товар выдан один раз");

            PurchaseResult repeat = shop.Purchase(wallet, inventory, Bread(1), "buy-G");
            MiniTestRunner.IsTrue(repeat.IsReplay, "повтор — ответ из журнала");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "второй выдачи нет");
        }

        // ---------- 5. Исключение обработчика Changed ----------

        private static void ChangedHandlerExceptionSafe()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            int calls = 0;
            shop.Changed += _ =>
            {
                calls++;
                throw new InvalidOperationException("Сбой обработчика Changed.");
            };

            PurchaseResult result = shop.Purchase(wallet, inventory, Bread(1), "buy-E");

            MiniTestRunner.IsTrue(result.IsSuccess, "исключение обработчика не отменяет покупку");
            MiniTestRunner.AreEqual(950L, wallet.BalanceRubles, "деньги списаны один раз");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "товар выдан");
            MiniTestRunner.IsTrue(calls > 0, "обработчик вызывался");
            MiniTestRunner.AreEqual(0, shop.PendingOperationCount, "журнал не «завис» из-за подписчика");
        }

        private static void ChangedCannotRecursePurchase()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            PurchaseResult nested = default(PurchaseResult);
            bool nestedCalled = false;

            shop.Changed += _ =>
            {
                if (nestedCalled)
                {
                    return;
                }

                nestedCalled = true;
                nested = shop.Purchase(wallet, inventory, Bread(1), "buy-nested");
            };

            PurchaseResult result = shop.Purchase(wallet, inventory, Bread(1), "buy-outer");

            MiniTestRunner.IsTrue(result.IsSuccess, "внешняя покупка успешна");
            MiniTestRunner.IsFalse(nested.IsSuccess, "вложенная покупка отклонена");
            MiniTestRunner.AreEqual(EconomyResultCode.PendingOperation, nested.Code, "причина — незавершённая операция");
            MiniTestRunner.AreEqual(1, wallet.SpendCalls, "списание осталось одно");
            MiniTestRunner.AreEqual(950L, wallet.BalanceRubles, "деньги не удвоились");
        }

        private static void ChangedRaisedOnce()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            var states = new List<ShopOperationState>();
            shop.Changed += operation => states.Add(operation.State);

            shop.Purchase(wallet, inventory, Bread(1), "buy-S");

            MiniTestRunner.AreEqual(2, states.Count, "два перехода: Debited и Granted");
            MiniTestRunner.AreEqual(ShopOperationState.Debited, states[0], "первый переход — списание");
            MiniTestRunner.AreEqual(ShopOperationState.Granted, states[1], "второй — выдача");
        }

        // ---------- 6. Снимок ----------

        private static void SnapshotReloadNoDoubleRefund()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory { RejectAdd = true };
            var shop = new ShopService(Catalog);

            shop.Purchase(wallet, inventory, Bread(1), "buy-S1");
            MiniTestRunner.AreEqual(1000L, wallet.BalanceRubles, "деньги вернулись в сессии");
            MiniTestRunner.AreEqual(0, shop.PendingOperationCount, "журнал пуст");

            ShopSnapshot snapshot = shop.CreateSnapshot();

            var reloaded = new ShopService(Catalog);
            string error;
            MiniTestRunner.IsTrue(reloaded.RestoreSnapshot(snapshot, out error), "снимок загружен: " + error);
            MiniTestRunner.IsTrue(reloaded.RetryPendingOperations(wallet, inventory), "восстановление ничего не ждёт");
            MiniTestRunner.AreEqual(1000L, wallet.BalanceRubles, "второго возврата нет");

            PurchaseResult replay = reloaded.Purchase(wallet, inventory, Bread(1), "buy-S1");
            MiniTestRunner.AreEqual(EconomyResultCode.InventoryRejected, replay.Code, "повтор вернул прежний исход");
            MiniTestRunner.AreEqual(1000L, wallet.BalanceRubles, "деньги не начислены повторно");
        }

        private static void SnapshotReloadNoDoubleGrant()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            shop.Purchase(wallet, inventory, Bread(2), "buy-S2");
            ShopSnapshot snapshot = shop.CreateSnapshot();

            var reloaded = new ShopService(Catalog);
            string error;
            reloaded.RestoreSnapshot(snapshot, out error);
            reloaded.RetryPendingOperations(wallet, inventory);

            PurchaseResult replay = reloaded.Purchase(wallet, inventory, Bread(2), "buy-S2");

            MiniTestRunner.IsTrue(replay.IsReplay, "повтор — ответ из журнала");
            MiniTestRunner.AreEqual(2, inventory.GetCount("bread"), "товар не выдан второй раз");
            MiniTestRunner.AreEqual(900L, wallet.BalanceRubles, "деньги списаны один раз");
        }

        private static void SnapshotRejectsDuplicateId()
        {
            var shop = new ShopService(Catalog);
            var snapshot = new ShopSnapshot
            {
                Operations = new[]
                {
                    new ShopOperationSnapshot { PurchaseId = "dup", ProductId = "bread", RequestedQuantity = 1, Units = 1, AmountRubles = 50L, State = (int)ShopOperationState.Granted },
                    new ShopOperationSnapshot { PurchaseId = "dup", ProductId = "bread", RequestedQuantity = 1, Units = 1, AmountRubles = 50L, State = (int)ShopOperationState.Granted }
                }
            };

            string error;
            MiniTestRunner.IsFalse(shop.RestoreSnapshot(snapshot, out error), "дубликат отклонён");
            MiniTestRunner.IsTrue(!string.IsNullOrEmpty(error), "есть описание ошибки");
        }

        private static void SnapshotBrokenKeepsState()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            shop.Purchase(wallet, inventory, Bread(1), "buy-K");
            int before = shop.CreateSnapshot().Operations.Length;

            var broken = shop.CreateSnapshot();
            broken.Operations[0].State = 99;

            string error;
            MiniTestRunner.IsFalse(shop.RestoreSnapshot(broken, out error), "повреждённый снимок отклонён");
            MiniTestRunner.AreEqual(before, shop.CreateSnapshot().Operations.Length, "журнал не изменился");

            PurchaseResult replay = shop.Purchase(wallet, inventory, Bread(1), "buy-K");
            MiniTestRunner.IsTrue(replay.IsReplay, "старая запись на месте");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "товар не выдан повторно");
        }

        private static void SnapshotRejectsTwoPending()
        {
            var shop = new ShopService(Catalog);
            var snapshot = new ShopSnapshot
            {
                Operations = new[]
                {
                    new ShopOperationSnapshot { PurchaseId = "p1", ProductId = "bread", RequestedQuantity = 1, Units = 1, AmountRubles = 50L, State = (int)ShopOperationState.Debited },
                    new ShopOperationSnapshot { PurchaseId = "p2", ProductId = "bread", RequestedQuantity = 1, Units = 1, AmountRubles = 50L, State = (int)ShopOperationState.RefundPending }
                }
            };

            string error;
            MiniTestRunner.IsFalse(shop.RestoreSnapshot(snapshot, out error), "две незавершённые операции отклонены");
        }

        // ---------- 7. Совместимость и режимы ----------

        private static void LegacyOverloadStillWorks()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            PurchaseResult first = shop.Purchase(wallet, inventory, Bread(1));
            PurchaseResult second = shop.Purchase(wallet, inventory, Bread(1));

            MiniTestRunner.IsTrue(first.IsSuccess, "покупка без ID");
            MiniTestRunner.IsTrue(second.IsSuccess, "вторая покупка без ID — это НОВАЯ покупка");
            MiniTestRunner.IsFalse(second.IsReplay, "ответ не помечен как повтор");
            MiniTestRunner.AreEqual(900L, wallet.BalanceRubles, "списано дважды по 50 ₽");
            MiniTestRunner.AreEqual(2, inventory.GetCount("bread"), "выдано два хлеба");
            MiniTestRunner.AreEqual(2, wallet.SpendCalls, "идентификатора нет — идемпотентность не обещается");
        }

        private static void SessionModeHonestAboutRestart()
        {
            var wallet = new JournalWallet(1000L);
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            MiniTestRunner.IsTrue(shop.IsDurable(wallet), "журнал тестового кошелька долговечен");

            shop.Purchase(wallet, inventory, Bread(1), "buy-M");
            ShopSnapshot snapshot = shop.CreateSnapshot();

            // «Перезапуск»: журналы адаптеров потеряны (в памяти процесса), снимок магазина есть.
            wallet.ClearJournal();
            inventory.ClearGrantJournal();

            var afterRestart = new ShopService(Catalog);
            string error;
            MiniTestRunner.IsTrue(afterRestart.RestoreSnapshot(snapshot, out error), "снимок загружен: " + error);

            // Повтор того же ID отвечает из журнала магазина: деньги и товар не двигаются.
            PurchaseResult replay = afterRestart.Purchase(wallet, inventory, Bread(1), "buy-M");
            MiniTestRunner.IsTrue(replay.IsReplay, "ответ на повтор берётся из журнала магазина");
            MiniTestRunner.AreEqual(1, wallet.SpendCalls, "списаний осталось одно (первое)");
            MiniTestRunner.AreEqual(1, inventory.AddCalls, "выдача осталась одна (первая)");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "товар не появился второй раз");
        }

        /// <summary>
        /// «Режим A»: завершённые покупки внутри сессии повторно не применяются,
        /// а незавершённая операция после ПОТЕРИ журналов адаптеров не разрешается
        /// выдуманным исходом — она остаётся и ждёт журнала.
        /// </summary>
        private static void SessionModeUnknownAfterJournalLoss()
        {
            var wallet = new JournalWallet(1000L) { ThrowAfterSpend = true, QueryUnavailable = true };
            var inventory = new JournalInventory();
            var shop = new ShopService(Catalog);

            PurchaseResult result = shop.Purchase(wallet, inventory, Bread(1), "buy-M3");
            MiniTestRunner.AreEqual(EconomyResultCode.OutcomeUnknown, result.Code, "исход неизвестен в сессии");

            ShopSnapshot snapshot = shop.CreateSnapshot();
            wallet.IsDurable = false; // «Режим A»: журнал живёт только в памяти процесса
            wallet.ClearJournal();
            inventory.ClearGrantJournal();
            wallet.QueryUnavailable = false;

            var afterRestart = new ShopService(Catalog);
            string error;
            afterRestart.RestoreSnapshot(snapshot, out error);

            MiniTestRunner.IsFalse(afterRestart.RetryPendingOperations(wallet, inventory), "журнал кошелька пуст — исход не установлен");
            MiniTestRunner.AreEqual(1, afterRestart.PendingOperationCount, "операция ждёт разрешения");
            MiniTestRunner.AreEqual(950L, wallet.BalanceRubles, "деньги не возвращены наугад");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "товар не выдан наугад");
        }
    }
}
