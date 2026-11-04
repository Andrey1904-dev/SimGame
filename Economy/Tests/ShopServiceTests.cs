// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Tests
//
// Покрытие по п.6 ТЗ:
//  1. Успешная покупка.
//  2. Недостаток денег.
//  3. Покупка несуществующего товара.
//  4. Нулевое и отрицательное количество.
//  5. Покупка нескольких единиц.
//  6. Переполнение стоимости.
//  7. Отказ инвентаря принять товар.
//  8. Отсутствие частичного списания/начисления при ошибке.
//  9. Повторная покупка.
// Дополнительно: сбой/исключение адаптера, отсутствие компенсации без списания,
// неизменяемость каталога, контракт Money.

using System;
using LifeFromZero.Economy.Core;
using LifeFromZero.Economy.Shop;
using LifeFromZero.Economy.Tests.Fakes;

namespace LifeFromZero.Economy.Tests
{
    /// <summary>Тесты сервиса покупки.</summary>
    public static class ShopServiceTests
    {
        /// <summary>Собирает все тест-кейсы модуля.</summary>
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();

            suite.Add("Покупка: успешная покупка одного хлеба", SuccessfulPurchase);
            suite.Add("Покупка: недостаточно денег", InsufficientFunds);
            suite.Add("Покупка: несуществующий товар", ProductNotFound);
            suite.Add("Покупка: нулевое количество", ZeroQuantity);
            suite.Add("Покупка: отрицательное количество", NegativeQuantity);
            suite.Add("Покупка: несколько единиц", MultipleUnits);
            suite.Add("Покупка: переполнение стоимости", CostOverflow);
            suite.Add("Покупка: переполнение количества единиц", UnitsOverflow);
            suite.Add("Покупка: инвентарь отказал до списания", InventoryRejectedBeforeSpend);
            suite.Add("Покупка: инвентарь отказал после списания — деньги возвращены", InventoryRejectedAfterSpend);
            suite.Add("Покупка: исключение инвентаря при добавлении — деньги возвращены", InventoryExceptionAfterSpend);
            suite.Add("Покупка: исключение кошелька при списании — баланс не изменён", WalletExceptionOnSpend);
            suite.Add("Покупка: кошелёк вернул false — баланс не изменён", WalletRejectsSpend);
            suite.Add("Покупка: сбой возврата денег — InternalError", RefundFailureReportsInternalError);
            suite.Add("Покупка: повторная покупка списывает деньги дважды", RepeatedPurchase);
            suite.Add("Покупка: пустой идентификатор товара", EmptyProductId);
            suite.Add("Покупка: исключение проверки инвентаря — деньги не списаны", InventoryCheckException);
            suite.Add("Покупка: ноль рублей на кошельке", ZeroBalance);
            suite.Add("Покупка: точное совпадение с балансом", ExactBalance);
            suite.Add("Покупка: товар с упаковкой из нескольких единиц", MultiUnitPackage);
            suite.Add("Каталог: товары по умолчанию и их цены", DefaultCatalogPrices);
            suite.Add("Каталог: замена цены не мутирует каталог", CatalogPriceReplacement);
            suite.Add("Money: запрет отрицательной суммы", MoneyRejectsNegative);
            suite.Add("Money: умножение с переполнением", MoneyMultiplyOverflow);
            suite.Add("Сервис: без кошелька — ShopServiceException", ServiceRequiresWallet);

            // Регрессионные тесты аудита ТЗ №015 (найденные дефекты).
            suite.Add("Возврат: сбой возврата не теряет деньги — повтор закрывает долг", PendingRefundIsRetried);
            suite.Add("Возврат: исключение возврата попадает в журнал", RefundExceptionIsJournaled);
            suite.Add("Возврат: незакрытый долг блокирует новые покупки", PendingRefundBlocksPurchases);
            suite.Add("Возврат: долг закрывается до покупки в том же вызове", PendingRefundIsClosedBeforePurchase);
            suite.Add("Возврат: предметы не выдаются, если возврат денег не удался", NoItemsWithoutMoney);
            suite.Add("Покупка: исключение чтения баланса", BalanceReadException);
            suite.Add("Покупка: исключение каталога", CatalogThrows);
            suite.Add("Покупка: повторные попытки не дублируют деньги и предметы", ManyRepeatedPurchases);

            return suite;
        }

        // ---------- 1. Успешная покупка ----------

        private static void SuccessfulPurchase()
        {
            var wallet = new FakeWallet(100L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.IsTrue(result.IsSuccess, "покупка должна быть успешной");
            MiniTestRunner.AreEqual(EconomyResultCode.Success, result.Code, "код результата");
            MiniTestRunner.AreEqual(50L, result.TotalCost.Rubles, "итоговая стоимость");
            MiniTestRunner.AreEqual(1, result.PurchasedQuantity, "количество покупок");
            MiniTestRunner.AreEqual(1, result.UnitsGranted, "выдано единиц предмета");
            MiniTestRunner.AreEqual(50L, wallet.Balance.Rubles, "баланс после покупки");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "хлеб в инвентаре");
            MiniTestRunner.AreEqual(1, wallet.SpendCalls, "списание вызывалось один раз");
            MiniTestRunner.AreEqual(0, wallet.RefundCalls, "возврат не должен вызываться");
        }

        // ---------- 2. Недостаток денег ----------

        private static void InsufficientFunds()
        {
            var wallet = new FakeWallet(49L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.InsufficientFunds, result.Code, "код результата");
            MiniTestRunner.IsFalse(result.IsSuccess, "покупка не должна считаться успешной");
            MiniTestRunner.AreEqual(50L, result.TotalCost.Rubles, "стоимость известна даже при отказе");
            MiniTestRunner.AreEqual(49L, wallet.Balance.Rubles, "баланс не изменён");
            MiniTestRunner.AreEqual(0, wallet.SpendCalls, "списание не должно вызываться");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "инвентарь не изменён");
            MiniTestRunner.AreEqual(0, inventory.CanApproveCalls, "проверка инвентаря не требуется");
        }

        private static void ZeroBalance()
        {
            var wallet = new FakeWallet(0L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.InsufficientFunds, result.Code, "код результата");
            MiniTestRunner.AreEqual(0L, wallet.Balance.Rubles, "баланс остался нулевым");
        }

        private static void ExactBalance()
        {
            var wallet = new FakeWallet(220L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("prepared_meal", 1));

            MiniTestRunner.IsTrue(result.IsSuccess, "покупки ровно на весь баланс разрешена");
            MiniTestRunner.AreEqual(0L, wallet.Balance.Rubles, "баланс обнулён");
            MiniTestRunner.AreEqual(1, inventory.GetCount("prepared_meal"), "обед в инвентаре");
        }

        // ---------- 3. Несуществующий товар ----------

        private static void ProductNotFound()
        {
            var wallet = new FakeWallet(1000L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("caviar", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.ProductNotFound, result.Code, "код результата");
            MiniTestRunner.IsTrue(result.Product == null, "товар отсутствует в результате");
            MiniTestRunner.AreEqual(1000L, wallet.Balance.Rubles, "баланс не изменён");
            MiniTestRunner.AreEqual(0, wallet.SpendCalls, "списание не должно вызываться");
        }

        private static void EmptyProductId()
        {
            var wallet = new FakeWallet(1000L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult empty = shop.Purchase(wallet, inventory, new PurchaseRequest(string.Empty, 1));
            PurchaseResult blank = shop.Purchase(wallet, inventory, new PurchaseRequest("   ", 1));
            PurchaseResult missing = shop.Purchase(wallet, inventory, new PurchaseRequest(null, 1));

            MiniTestRunner.AreEqual(EconomyResultCode.ProductNotFound, empty.Code, "пустой id => товар не найден");
            MiniTestRunner.AreEqual(EconomyResultCode.ProductNotFound, blank.Code, "пробельный id => товар не найден");
            MiniTestRunner.AreEqual(EconomyResultCode.ProductNotFound, missing.Code, "null id => товар не найден");
            MiniTestRunner.AreEqual(1000L, wallet.Balance.Rubles, "баланс не изменён");
            MiniTestRunner.AreEqual(0, wallet.SpendCalls, "списание не вызывалось");
        }

        private static void InventoryCheckException()
        {
            var wallet = new FakeWallet(1000L);
            var inventory = new FakeInventory { ThrowOnCanApprove = true };
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.InternalError, result.Code, "исключение проверки => InternalError");
            MiniTestRunner.AreEqual(1000L, wallet.Balance.Rubles, "деньги не списаны");
            MiniTestRunner.AreEqual(0, wallet.SpendCalls, "списание не вызывалось");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "предметы не выданы");
        }

        // ---------- 4. Нулевое и отрицательное количество ----------

        private static void ZeroQuantity()
        {
            var wallet = new FakeWallet(1000L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("bread", 0));

            MiniTestRunner.AreEqual(EconomyResultCode.InvalidQuantity, result.Code, "код результата");
            MiniTestRunner.AreEqual(0L, result.TotalCost.Rubles, "стоимость нулевая");
            MiniTestRunner.AreEqual(1000L, wallet.Balance.Rubles, "баланс не изменён");
            MiniTestRunner.AreEqual(0, wallet.SpendCalls, "списание не должно вызываться");
        }

        private static void NegativeQuantity()
        {
            var wallet = new FakeWallet(1000L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("bread", -3));

            MiniTestRunner.AreEqual(EconomyResultCode.InvalidQuantity, result.Code, "код результата");
            MiniTestRunner.AreEqual(1000L, wallet.Balance.Rubles, "баланс не изменён");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "инвентарь не изменён");
        }

        // ---------- 5. Покупка нескольких единиц ----------

        private static void MultipleUnits()
        {
            var wallet = new FakeWallet(500L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("instant_noodles", 3));

            MiniTestRunner.IsTrue(result.IsSuccess, "покупка трёх упаковок успешна");
            MiniTestRunner.AreEqual(195L, result.TotalCost.Rubles, "65 ₽ × 3");
            MiniTestRunner.AreEqual(3, result.PurchasedQuantity, "количество покупок");
            MiniTestRunner.AreEqual(305L, wallet.Balance.Rubles, "баланс после покупки");
            MiniTestRunner.AreEqual(3, inventory.GetCount("instant_noodles"), "три упаковки в инвентаре");
        }

        private static void MultiUnitPackage()
        {
            var catalog = new ShopCatalog(new[]
            {
                new Product("egg_pack", "Упаковка яиц", Money.FromRubles(120L), ProductType.Food, 10, "egg")
            });

            var wallet = new FakeWallet(500L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(catalog);

            PurchaseResult result = shop.Purchase(wallet, inventory, new PurchaseRequest("egg_pack", 2));

            MiniTestRunner.IsTrue(result.IsSuccess, "покупка упаковок успешна");
            MiniTestRunner.AreEqual(240L, result.TotalCost.Rubles, "120 ₽ × 2 упаковки");
            MiniTestRunner.AreEqual(2, result.PurchasedQuantity, "две покупки");
            MiniTestRunner.AreEqual(20, result.UnitsGranted, "10 яиц в упаковке × 2");
            MiniTestRunner.AreEqual(20, inventory.GetCount("egg"), "яйца в инвентаре");
        }

        // ---------- 6. Переполнение стоимости ----------

        private static void CostOverflow()
        {
            var catalog = new ShopCatalog(new[]
            {
                new Product("ludicrous", "Абсурдно дорогой товар", Money.FromRubles(long.MaxValue), ProductType.NonFood, 1, "ludicrous")
            });

            var wallet = new FakeWallet(0L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(catalog);

            PurchaseResult result = shop.Purchase(wallet, inventory, new PurchaseRequest("ludicrous", 2));

            MiniTestRunner.AreEqual(EconomyResultCode.InvalidQuantity, result.Code, "переполнение стоимости обнаружено");
            MiniTestRunner.AreEqual(0L, wallet.Balance.Rubles, "баланс не изменён");
            MiniTestRunner.AreEqual(0, wallet.SpendCalls, "списание не должно вызываться");
            MiniTestRunner.AreEqual(0, inventory.TryAddCalls, "выдача не должна вызываться");
        }

        private static void UnitsOverflow()
        {
            var catalog = new ShopCatalog(new[]
            {
                new Product("bulk", "Огромная упаковка", Money.FromRubles(1L), ProductType.Food, int.MaxValue, "bulk")
            });

            var wallet = new FakeWallet(10L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(catalog);

            PurchaseResult result = shop.Purchase(wallet, inventory, new PurchaseRequest("bulk", 2));

            MiniTestRunner.AreEqual(EconomyResultCode.InvalidQuantity, result.Code, "переполнение int по единицам обнаружено");
            MiniTestRunner.AreEqual(10L, wallet.Balance.Rubles, "баланс не изменён");
        }

        // ---------- 7. Отказ инвентаря ----------

        private static void InventoryRejectedBeforeSpend()
        {
            var wallet = new FakeWallet(1000L);
            var inventory = new FakeInventory { CapacityPerItem = 0 };
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.InventoryRejected, result.Code, "код результата");
            MiniTestRunner.AreEqual(1000L, wallet.Balance.Rubles, "деньги не списаны");
            MiniTestRunner.AreEqual(0, wallet.SpendCalls, "списание не должно вызываться");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "инвентарь не изменён");
        }

        private static void InventoryRejectedAfterSpend()
        {
            var wallet = new FakeWallet(1000L);
            var inventory = new FakeInventory { TryAddOverride = false };
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("bread", 2));

            MiniTestRunner.AreEqual(EconomyResultCode.InventoryRejected, result.Code, "код результата");
            MiniTestRunner.AreEqual(1000L, wallet.Balance.Rubles, "деньги возвращены полностью");
            MiniTestRunner.AreEqual(1, wallet.SpendCalls, "было ровно одно списание");
            MiniTestRunner.AreEqual(1, wallet.RefundCalls, "ровно один возврат");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "предметы не выданы");
        }

        private static void InventoryExceptionAfterSpend()
        {
            var wallet = new FakeWallet(1000L);
            var inventory = new FakeInventory { ThrowOnAdd = true };
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("prepared_meal", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.InventoryRejected, result.Code, "исключение адаптера => отказ инвентаря");
            MiniTestRunner.AreEqual(1000L, wallet.Balance.Rubles, "деньги возвращены");
            MiniTestRunner.AreEqual(0, inventory.GetCount("prepared_meal"), "предметы не выданы");
        }

        // ---------- 8. Отсутствие частичных изменений ----------

        private static void WalletExceptionOnSpend()
        {
            var wallet = new FakeWallet(1000L) { ThrowOnSpend = true };
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.InternalError, result.Code, "исключение кошелька => внутренняя ошибка");
            MiniTestRunner.AreEqual(1000L, wallet.Balance.Rubles, "баланс не изменён");
            MiniTestRunner.AreEqual(0, wallet.RefundCalls, "компенсация не нужна: списания не было");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "предметы не выданы");
        }

        private static void WalletRejectsSpend()
        {
            var wallet = new FakeWallet(1000L) { SpendOverride = false };
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.InsufficientFunds, result.Code, "кошелёк отказал в списании");
            MiniTestRunner.AreEqual(1000L, wallet.Balance.Rubles, "баланс не изменён");
            MiniTestRunner.AreEqual(0, wallet.RefundCalls, "компенсация не нужна");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "предметы не выданы");
        }

        private static void RefundFailureReportsInternalError()
        {
            var wallet = new FakeWallet(1000L) { RefundOverride = false };
            var inventory = new FakeInventory { TryAddOverride = false };
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(
                wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.InternalError, result.Code, "неудачный возврат => InternalError");
            MiniTestRunner.AreEqual(1, wallet.RefundCalls, "попытка возврата была");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "предметы не выданы");
            // Документируем известный остаточный риск: если кошелёк не смог вернуть деньги,
            // списанная сумма остаётся списанной, а код сообщает о внутренней ошибке.
            MiniTestRunner.AreEqual(950L, wallet.Balance.Rubles, "деньги не вернулись — случай требует сверки");
        }

        // ---------- 9. Повторная покупка ----------

        private static void RepeatedPurchase()
        {
            // 120 ₽ хватает ровно на два хлеба по 50 ₽, третий должен быть отклонён.
            var wallet = new FakeWallet(120L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult first = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));
            PurchaseResult second = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));
            PurchaseResult third = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.IsTrue(first.IsSuccess, "первая покупка успешна");
            MiniTestRunner.IsTrue(second.IsSuccess, "вторая покупка успешна");
            MiniTestRunner.AreEqual(EconomyResultCode.InsufficientFunds, third.Code, "третьей покупки не хватает денег");
            MiniTestRunner.AreEqual(20L, wallet.Balance.Rubles, "120 - 50 - 50");
            MiniTestRunner.AreEqual(2, inventory.GetCount("bread"), "два хлеба в инвентаре");
            MiniTestRunner.AreEqual(2, wallet.SpendCalls, "списание вызывалось только для двух успешных покупок");
        }

        // ---------- Каталог и Money ----------

        private static void DefaultCatalogPrices()
        {
            IShopCatalog catalog = ShopCatalog.CreateDefault();

            MiniTestRunner.AreEqual(3, catalog.Products.Count, "три товара в начальном каталоге");

            MiniTestRunner.IsTrue(catalog.TryGet("bread", out Product bread), "хлеб найден");
            MiniTestRunner.AreEqual(50L, bread.Price.Rubles, "цена хлеба");
            MiniTestRunner.AreEqual("Хлеб", bread.Title, "название хлеба");
            MiniTestRunner.AreEqual(ProductType.Food, bread.Type, "тип хлеба");

            MiniTestRunner.IsTrue(catalog.TryGet("instant_noodles", out Product noodles), "лапша найдена");
            MiniTestRunner.AreEqual(65L, noodles.Price.Rubles, "цена лапши");

            MiniTestRunner.IsTrue(catalog.TryGet("prepared_meal", out Product meal), "обед найден");
            MiniTestRunner.AreEqual(220L, meal.Price.Rubles, "цена обеда");

            MiniTestRunner.IsFalse(catalog.TryGet("unknown", out _), "неизвестный товар не найден");
        }

        private static void CatalogPriceReplacement()
        {
            IShopCatalog catalog = ShopCatalog.CreateDefault();
            catalog.TryGet("bread", out Product bread);

            Product cheaper = ProductPrice.WithPrice(bread, Money.FromRubles(40L));

            MiniTestRunner.AreEqual(40L, cheaper.Price.Rubles, "новая цена применяется к копии");
            MiniTestRunner.AreEqual(50L, bread.Price.Rubles, "исходный товар не мутирован");
            MiniTestRunner.AreEqual(bread.Id, cheaper.Id, "идентификатор сохранён");
        }

        private static void MoneyRejectsNegative()
        {
            bool threw = false;
            try
            {
                Money.FromRubles(-1L);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }

            MiniTestRunner.IsTrue(threw, "отрицательная сумма запрещена");

            Money money;
            MiniTestRunner.IsFalse(Money.TryFromRubles(-5L, out money), "TryFromRubles вернул false");
            MiniTestRunner.AreEqual(0L, money.Rubles, "при отказе возвращается ноль");
        }

        private static void MoneyMultiplyOverflow()
        {
            Money price = Money.FromRubles(long.MaxValue);
            MiniTestRunner.IsFalse(price.TryMultiply(2, out Money total), "переполнение обнаружено");
            MiniTestRunner.AreEqual(0L, total.Rubles, "при переполнении стоимость нулевая");

            Money normal;
            MiniTestRunner.IsTrue(Money.FromRubles(65L).TryMultiply(3, out normal), "обычное умножение работает");
            MiniTestRunner.AreEqual(195L, normal.Rubles, "65 × 3");

            MiniTestRunner.IsFalse(Money.FromRubles(65L).TryMultiply(-1, out _), "отрицательное количество отклонено");
        }

        // ---------- Регрессия аудита ТЗ №015 ----------

        /// <summary>
        /// Дефект: при отказе возврата деньги «сгорали» навсегда — повторная покупка просто
        /// списывала новую сумму, а списанная ранее не возвращалась.
        /// Теперь сумма попадает в журнал и возвращается при следующем вызове.
        /// </summary>
        private static void PendingRefundIsRetried()
        {
            var wallet = new FakeWallet(1000L) { RefundFailuresRemaining = 1 };
            var inventory = new FakeInventory { TryAddOverride = false };
            var shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult first = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.InternalError, first.Code, "возврат не удался");
            MiniTestRunner.AreEqual(950L, wallet.Balance.Rubles, "деньги списаны и ещё не вернулись");
            MiniTestRunner.AreEqual(1, shop.PendingRefundCount, "долг зафиксирован в журнале");

            // Следующий вызов сначала закрывает долг, затем выполняет покупку.
            inventory.TryAddOverride = null;
            PurchaseResult second = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.IsTrue(second.IsSuccess, "покупка после закрытия долга проходит");
            MiniTestRunner.AreEqual(0, shop.PendingRefundCount, "журнал пуст");
            MiniTestRunner.AreEqual(950L, wallet.Balance.Rubles, "1000 - 50 (первая компенсирована, вторая списана)");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "выдан ровно один хлеб");
        }

        private static void RefundExceptionIsJournaled()
        {
            var wallet = new FakeWallet(500L) { RefundThrowsRemaining = 1 };
            var inventory = new FakeInventory { TryAddOverride = false };
            var shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult first = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));
            MiniTestRunner.AreEqual(EconomyResultCode.InternalError, first.Code, "исключение возврата => InternalError");
            MiniTestRunner.AreEqual(1, shop.PendingRefundCount, "исключение тоже попадает в журнал");

            MiniTestRunner.IsTrue(shop.RetryPendingRefunds(wallet), "повтор возврата успешен");
            MiniTestRunner.AreEqual(500L, wallet.Balance.Rubles, "баланс восстановлен полностью");
            MiniTestRunner.AreEqual(0, shop.PendingRefundCount, "журнал пуст");
        }

        private static void PendingRefundBlocksPurchases()
        {
            var wallet = new FakeWallet(1000L) { RefundFailuresRemaining = 1 };
            var inventory = new FakeInventory { TryAddOverride = false };
            var shop = new ShopService(ShopCatalog.CreateDefault());

            shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));

            // Пока долг висит, повторный TryRefund тоже отклоняется (RefundOverride).
            wallet.RefundOverride = false;
            PurchaseResult blocked = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.PendingRefund, blocked.Code, "покупки заблокированы до закрытия долга");
            MiniTestRunner.AreEqual(950L, wallet.Balance.Rubles, "новых списаний не было");
            MiniTestRunner.AreEqual(1, wallet.SpendCalls, "списание было только одно");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "предметы не выдавались");
        }

        private static void PendingRefundIsClosedBeforePurchase()
        {
            var wallet = new FakeWallet(1000L) { RefundFailuresRemaining = 1 };
            var inventory = new FakeInventory { TryAddOverride = false };
            var shop = new ShopService(ShopCatalog.CreateDefault());

            shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));
            inventory.TryAddOverride = null;

            PurchaseResult after = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.IsTrue(after.IsSuccess, "покупка после компенсации успешна");
            MiniTestRunner.AreEqual(50L, after.TotalCost.Rubles, "хлеб по 50 ₽");
            MiniTestRunner.AreEqual(950L, wallet.Balance.Rubles, "1000 - 50: первая покупка компенсирована, вторая списана");
            MiniTestRunner.AreEqual(1, inventory.GetCount("bread"), "выдан один хлеб");
            MiniTestRunner.AreEqual(0, shop.PendingRefundCount, "журнал пуст");
        }

        private static void NoItemsWithoutMoney()
        {
            var wallet = new FakeWallet(100L) { RefundFailuresRemaining = 5 };
            var inventory = new FakeInventory { TryAddOverride = false };
            var shop = new ShopService(ShopCatalog.CreateDefault());

            shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(50L, wallet.Balance.Rubles, "деньги списаны");
            MiniTestRunner.AreEqual(0, inventory.GetCount("bread"), "предметы не выданы");
            MiniTestRunner.AreEqual(1, shop.PendingRefundCount, "ровно один долг, не несколько");

            // Повторные неудачные попытки не размножают долг.
            shop.RetryPendingRefunds(wallet);
            shop.RetryPendingRefunds(wallet);
            MiniTestRunner.AreEqual(1, shop.PendingRefundCount, "долг не дублируется");
        }

        private static void BalanceReadException()
        {
            var wallet = new ThrowingBalanceWallet();
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());

            PurchaseResult result = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.InternalError, result.Code, "сбой чтения баланса => InternalError");
            MiniTestRunner.AreEqual(0, wallet.SpendCalls, "списание не выполнялось");
            MiniTestRunner.AreEqual(0, inventory.TryAddCalls, "выдача не выполнялась");
        }

        private static void CatalogThrows()
        {
            var wallet = new FakeWallet(1000L);
            var inventory = new FakeInventory();
            IShopService shop = new ShopService(new ThrowingCatalog());

            PurchaseResult result = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));

            MiniTestRunner.AreEqual(EconomyResultCode.InternalError, result.Code, "исключение каталога => InternalError");
            MiniTestRunner.AreEqual(1000L, wallet.Balance.Rubles, "баланс не изменён");
        }

        private static void ManyRepeatedPurchases()
        {
            var wallet = new FakeWallet(50L * 10L);
            var inventory = new FakeInventory();
            var shop = new ShopService(ShopCatalog.CreateDefault());

            int successes = 0;
            int failures = 0;

            for (int i = 0; i < 20; i++)
            {
                PurchaseResult result = shop.Purchase(wallet, inventory, new PurchaseRequest("bread", 1));

                if (result.IsSuccess)
                {
                    successes++;
                }
                else
                {
                    failures++;
                }
            }

            MiniTestRunner.AreEqual(10, successes, "ровно 10 успешных покупок");
            MiniTestRunner.AreEqual(10, failures, "остальные 10 отклонены");
            MiniTestRunner.AreEqual(0L, wallet.Balance.Rubles, "деньги израсходованы точно");
            MiniTestRunner.AreEqual(10, inventory.GetCount("bread"), "предметов ровно 10, без дублей");
            MiniTestRunner.AreEqual(0, shop.PendingRefundCount, "журнал компенсаций пуст");
        }

        /// <summary>Кошелёк, у которого чтение баланса всегда падает.</summary>
        private sealed class ThrowingBalanceWallet : IWalletAdapter
        {
            public int SpendCalls { get; private set; }

            public Money Balance => throw new InvalidOperationException("Тестовый сбой чтения баланса.");

            public bool TrySpend(Money amount)
            {
                SpendCalls++;
                return true;
            }

            public bool TryRefund(Money amount)
            {
                return true;
            }
        }

        /// <summary>Каталог, который падает на любом поиске.</summary>
        private sealed class ThrowingCatalog : IShopCatalog
        {
            public System.Collections.Generic.IReadOnlyList<Product> Products =>
                new System.Collections.Generic.List<Product>().AsReadOnly();

            public bool TryGet(string productId, out Product product)
            {
                throw new InvalidOperationException("Тестовый сбой каталога.");
            }
        }

        private static void ServiceRequiresWallet()
        {
            IShopService shop = new ShopService(ShopCatalog.CreateDefault());
            bool threw = false;

            try
            {
                shop.Purchase(null, new FakeInventory(), new PurchaseRequest("bread", 1));
            }
            catch (ShopServiceException)
            {
                threw = true;
            }

            MiniTestRunner.IsTrue(threw, "без кошелька сервис сообщает об ошибке интеграции");
        }
    }
}
