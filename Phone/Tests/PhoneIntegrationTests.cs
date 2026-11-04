// Life From Zero — Phone tests (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Tests

using System;
using LifeFromZero.Phone.Contacts;
using LifeFromZero.Phone.Core;
using LifeFromZero.Phone.Integrations;
using LifeFromZero.Phone.Messages;
using LifeFromZero.Phone.Notifications;
using LifeFromZero.Phone.Tests.Fakes;

namespace LifeFromZero.Phone.Tests
{
    /// <summary>Тесты интеграций: банк, работа, сюжет, время, фасад.</summary>
    public static class PhoneIntegrationTests
    {
        public static void Register(TestSuite suite)
        {
            suite.Add("Интеграция: ID сообщений и уведомлений независимы", IdSpacesIndependent);
            suite.Add("Интеграция: банк читает баланс", BankBalance);
            suite.Add("Интеграция: банк без источника", BankWithoutSource);
            suite.Add("Интеграция: банк при сбое адаптера", BankAdapterFailure);
            suite.Add("Интеграция: банк и долги", BankWithDebts);
            suite.Add("Интеграция: отрицательный баланс не показывается как деньги", BankNegativeBalance);
            suite.Add("Интеграция: работа читает заказы", WorkOrders);
            suite.Add("Интеграция: работа при сбое адаптера", WorkAdapterFailure);
            suite.Add("Интеграция: работа без источника", WorkWithoutSource);
            suite.Add("Интеграция: сюжетные записи", StoryEntries);
            suite.Add("Интеграция: сюжет при сбое адаптера", StoryAdapterFailure);
            suite.Add("Интеграция: игровое время — корректные и некорректные значения", GameMinutesValidation);
            suite.Add("Интеграция: фасад собирает все сервисы", FacadeWiring);
            suite.Add("Интеграция: фасад требует зависимости", FacadeRequiresDependencies);

            // Регрессионные тесты аудита ТЗ №015.
            suite.Add("Интеграция: пустые записи в ответах источников не доходят до UI", AdaptersReturnNullEntries);

            // Регрессионные тесты аудита ТЗ №017.
            suite.Add("Интеграция: бейдж «Сообщения» обновляется при доставке через Inbox", MessagesBadgeFollowsInbox);
            suite.Add("Интеграция: бейдж «Сообщения» идемпотентен и не накручивается", MessagesBadgeIsIdempotent);
            suite.Add("Интеграция: список долгов отдаёт стадию взыскания", DebtsExposePhase);
        }

        /// <summary>
        /// Дефект: доставка сообщения через IPhoneInbox увеличивала только счётчик
        /// переписки, а бейдж приложения «messages» оставался нулевым.
        /// </summary>
        private static void MessagesBadgeFollowsInbox()
        {
            var facade = new PhoneFacade(
                Apps.PhoneAppRegistry.CreateDefault(),
                new PhoneMessageService(),
                new PhoneBook(),
                new PhoneNotificationService());

            var clock = new FakePhoneClock(1d);

            MiniTestRunner.AreEqual(0, facade.UnreadApps, "бейджа нет до доставки");

            facade.Inbox.Deliver("anna", "Привет", clock);
            facade.Inbox.Deliver("anna", "Второе", clock);

            MiniTestRunner.AreEqual(2, facade.UnreadMessages, "два непрочитанных сообщения");
            MiniTestRunner.AreEqual(0, facade.UnreadApps, "без синхронизации бейдж не обновлён — это и был дефект");

            MiniTestRunner.IsTrue(facade.SyncMessageBadge().IsSuccess, "синхронизация выполнена");
            MiniTestRunner.AreEqual(2, facade.Apps.Get(PhoneBadgeSync.MessagesAppId).Value.UnreadCount, "бейдж равен числу непрочитанных");

            // Прочтение сообщений уменьшает бейдж, а не оставляет его «накопленным».
            facade.Messages.MarkAllRead("anna");
            MiniTestRunner.IsTrue(facade.SyncMessageBadge().IsSuccess, "повторная синхронизация");
            MiniTestRunner.AreEqual(0, facade.Apps.Get(PhoneBadgeSync.MessagesAppId).Value.UnreadCount, "бейдж сброшен");
        }

        private static void MessagesBadgeIsIdempotent()
        {
            var facade = new PhoneFacade(
                Apps.PhoneAppRegistry.CreateDefault(),
                new PhoneMessageService(),
                new PhoneBook(),
                new PhoneNotificationService());

            facade.Inbox.Deliver("anna", "Привет", new FakePhoneClock(1d));

            for (int i = 0; i < 5; i++)
            {
                facade.SyncMessageBadge();
            }

            MiniTestRunner.AreEqual(1, facade.Apps.Get(PhoneBadgeSync.MessagesAppId).Value.UnreadCount, "бейдж не накручивается");

            // Без зарегистрированного приложения синхронизация не притворяется успешной.
            var bare = new PhoneFacade(
                new Apps.PhoneAppRegistry(),
                new PhoneMessageService(),
                new PhoneBook(),
                new PhoneNotificationService());

            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, bare.SyncMessageBadge().Code, "нет приложения — NotFound");
            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument,
                PhoneBadgeSync.SyncMessagesBadge(null, bare.Messages).Code,
                "без реестра — InvalidArgument");
        }

        /// <summary>
        /// Дефект: DebtDto нёс только остаток и срок, поэтому экран «Банк»
        /// не мог показать стадию взыскания (требование Phone ↔ Debts).
        /// </summary>
        private static void DebtsExposePhase()
        {
            var debts = new FakeDebtsSource
            {
                Debts = new[]
                {
                    new DebtDto("rent", "Аренда", 38400L, 1200d, DebtPhase.Collection),
                    new DebtDto("phone", "Связь", 300L, -1d, DebtPhase.Current),
                    new DebtDto("legacy", "Старый долг", 100L, -1d)
                }
            };

            BankSummary summary = new BankSummaryService(new FakeBalanceSource { Rubles = 10L }, debts).GetSummary().Value;

            MiniTestRunner.AreEqual(3, summary.Debts.Length, "три долга");
            MiniTestRunner.AreEqual(DebtPhase.Collection, summary.Debts[0].Phase, "стадия взыскания видна");
            MiniTestRunner.AreEqual(DebtPhase.Current, summary.Debts[1].Phase, "текущий долг");
            MiniTestRunner.AreEqual(DebtPhase.Unknown, summary.Debts[2].Phase, "старый конструктор даёт Unknown");
            MiniTestRunner.AreEqual(38400L, summary.Debts[0].AmountRubles, "остаток сохранён");
        }

        private static void IdSpacesIndependent()
        {
            var facade = new PhoneFacade(
                Apps.PhoneAppRegistry.CreateDefault(),
                new PhoneMessageService(),
                new PhoneBook(),
                new PhoneNotificationService());

            var clock = new FakePhoneClock(0d);

            MiniTestRunner.IsTrue(facade.Inbox.Deliver("anna", "Текст", clock).IsSuccess, "сообщение добавлено");
            MiniTestRunner.IsTrue(
                facade.Notifier.Push("anna#1", "anna", PhoneNotificationType.Message, "Заголовок", "Текст", null, PhoneNotificationChannel.Inbox, clock).IsSuccess,
                "уведомление с похожим ID добавлено");

            MiniTestRunner.AreEqual(1, facade.Messages.GetHistory("anna").Value.Count, "история сообщений не затронута");
            MiniTestRunner.AreEqual(1, facade.Notifications.Notifications.Count, "уведомление существует");
        }

        private static void BankBalance()
        {
            var balance = new FakeBalanceSource { Rubles = 1234L };
            var service = new BankSummaryService(balance, null);

            BankSummary summary = service.GetSummary().Value;

            MiniTestRunner.IsTrue(summary.BalanceAvailable, "баланс доступен");
            MiniTestRunner.AreEqual(1234L, summary.BalanceRubles, "баланс прочитан");
            MiniTestRunner.IsFalse(summary.DebtsAvailable, "долги недоступны без источника");
            MiniTestRunner.AreEqual(0, summary.Debts.Length, "долгов нет");
        }

        private static void BankWithoutSource()
        {
            var service = new BankSummaryService(null, null);

            BankSummary summary = service.GetSummary().Value;

            MiniTestRunner.IsFalse(summary.BalanceAvailable, "источника нет — баланс недоступен");
            MiniTestRunner.AreEqual(0L, summary.BalanceRubles, "безопасное значение");
        }

        private static void BankAdapterFailure()
        {
            var balance = new FakeBalanceSource { ThrowOnRead = true };
            var debts = new FakeDebtsSource { ThrowOnRead = true };
            var service = new BankSummaryService(balance, debts);

            PhoneResult<BankSummary> result = service.GetSummary();

            MiniTestRunner.IsTrue(result.IsSuccess, "сбой адаптера не превращается в исключение");
            MiniTestRunner.IsFalse(result.Value.BalanceAvailable, "баланс недоступен");
            MiniTestRunner.IsFalse(result.Value.DebtsAvailable, "долги недоступны");
        }

        private static void BankWithDebts()
        {
            var balance = new FakeBalanceSource { Rubles = 100L };
            var debts = new FakeDebtsSource
            {
                Debts = new[]
                {
                    new DebtDto("rent", "Аренда", 5000L, 1200d),
                    new DebtDto("phone", "Связь", 300L, -1d)
                }
            };

            BankSummary summary = new BankSummaryService(balance, debts).GetSummary().Value;

            MiniTestRunner.IsTrue(summary.DebtsAvailable, "долги доступны");
            MiniTestRunner.AreEqual(2, summary.Debts.Length, "два долга");
            MiniTestRunner.AreEqual(5000L, summary.Debts[0].AmountRubles, "сумма первого долга");
        }

        private static void BankNegativeBalance()
        {
            var balance = new FakeBalanceSource { Rubles = -500L };
            BankSummary summary = new BankSummaryService(balance, null).GetSummary().Value;

            MiniTestRunner.IsFalse(summary.BalanceAvailable, "отрицательный баланс считается недоступным");
            MiniTestRunner.AreEqual(0L, summary.BalanceRubles, "баланс обнулён");
        }

        private static void WorkOrders()
        {
            var jobs = new FakeJobsSource
            {
                ActiveOrderId = "order-1",
                Orders = new[]
                {
                    new DeliveryJobDto("order-1", "Хлеб", 150L, DeliveryJobPhase.PickedUp),
                    new DeliveryJobDto("order-2", "Лапша", 90L, DeliveryJobPhase.Available)
                }
            };

            WorkSummary summary = new WorkSummaryService(jobs).GetSummary().Value;

            MiniTestRunner.IsTrue(summary.JobsAvailable, "заказы доступны");
            MiniTestRunner.AreEqual(2, summary.Orders.Length, "два заказа");
            MiniTestRunner.AreEqual("order-1", summary.ActiveOrderId, "активный заказ");
            MiniTestRunner.AreEqual(DeliveryJobPhase.PickedUp, summary.Orders[0].Phase, "фаза заказа");
        }

        private static void WorkAdapterFailure()
        {
            var jobs = new FakeJobsSource { ThrowOnRead = true };
            WorkSummary summary = new WorkSummaryService(jobs).GetSummary().Value;

            MiniTestRunner.IsFalse(summary.JobsAvailable, "заказы недоступны");
            MiniTestRunner.AreEqual(0, summary.Orders.Length, "пустой список");
            MiniTestRunner.IsNull(summary.ActiveOrderId, "активного заказа нет");
        }

        private static void WorkWithoutSource()
        {
            WorkSummary summary = new WorkSummaryService(null).GetSummary().Value;

            MiniTestRunner.IsFalse(summary.JobsAvailable, "без источника данные недоступны");
            MiniTestRunner.AreEqual(0, summary.Orders.Length, "пустой список");
        }

        private static void StoryEntries()
        {
            var story = new FakeStorySource
            {
                Entries = new[] { new StoryEntryDto("s1", "Начало", "Вы проснулись в пустой квартире.") }
            };

            StoryEntryDto[] entries = new StorySummaryService(story).GetEntries().Value;

            MiniTestRunner.AreEqual(1, entries.Length, "одна запись");
            MiniTestRunner.AreEqual("Начало", entries[0].Title, "заголовок записи");
        }

        private static void StoryAdapterFailure()
        {
            var story = new FakeStorySource { ThrowOnRead = true };
            StoryEntryDto[] entries = new StorySummaryService(story).GetEntries().Value;

            MiniTestRunner.AreEqual(0, entries.Length, "при сбое пустой список, без исключения");
        }

        private static void GameMinutesValidation()
        {
            GameMinutes valid = GameMinutes.FromMinutes(90d);
            MiniTestRunner.IsTrue(valid.IsValid, "корректное время");
            MiniTestRunner.AreEqual(90d, valid.Minutes, "значение минут");

            MiniTestRunner.IsFalse(GameMinutes.FromMinutes(-1d).IsValid, "отрицательное время — Unknown");
            MiniTestRunner.IsFalse(GameMinutes.FromMinutes(double.NaN).IsValid, "NaN — Unknown");
            MiniTestRunner.IsFalse(GameMinutes.FromMinutes(double.PositiveInfinity).IsValid, "бесконечность — Unknown");

            GameMinutes parsed;
            MiniTestRunner.IsFalse(GameMinutes.TryFromMinutes(-5d, out parsed), "TryFromMinutes вернул false");
            MiniTestRunner.IsFalse(parsed.IsValid, "результат Unknown");

            MiniTestRunner.IsTrue(GameMinutes.Unknown == GameMinutes.Unknown, "Unknown равен Unknown");
            MiniTestRunner.IsFalse(GameMinutes.Unknown == GameMinutes.FromMinutes(0d), "Unknown не равен нулю");

            MiniTestRunner.AreEqual(GameMinutes.Unknown, PhoneClockReader.Read(null), "null-часы дают Unknown");
            MiniTestRunner.AreEqual(GameMinutes.Unknown, PhoneClockReader.Read(new FakePhoneClock(double.NaN)), "NaN от часов");
            MiniTestRunner.AreEqual(0d, GameMinutes.Unknown.Minutes, "безопасные минуты у Unknown");
        }

        private static void FacadeWiring()
        {
            var facade = new PhoneFacade(
                Apps.PhoneAppRegistry.CreateDefault(),
                new PhoneMessageService(),
                new PhoneBook(),
                new PhoneNotificationService());

            MiniTestRunner.AreEqual(5, facade.Apps.Apps.Count, "пять приложений");
            MiniTestRunner.IsTrue(facade.Contacts.Add(new PhoneContact("anna", "Анна", "+7", true)).IsSuccess, "контакт добавлен");
            MiniTestRunner.IsTrue(facade.Messages.StartConversation("anna").IsSuccess, "переписка создана");
            MiniTestRunner.IsTrue(
                facade.Inbox.Deliver("anna", "Привет от NPC", new FakePhoneClock(1d)).IsSuccess,
                "сообщение через inbox");
            MiniTestRunner.IsTrue(
                facade.Notifier.Push("n1", "anna", PhoneNotificationType.Message, "Новое сообщение", "Текст", "messages", PhoneNotificationChannel.Banner, new FakePhoneClock(1d)).IsSuccess,
                "уведомление через notifier");

            MiniTestRunner.AreEqual(1, facade.UnreadMessages, "счётчик сообщений");
            MiniTestRunner.AreEqual(1, facade.UnreadNotifications, "счётчик уведомлений");
        }

        /// <summary>
        /// Источники (адаптеры пишет Codex) могут вернуть массив с null-элементами.
        /// Телефон обязан отфильтровать их, а не отдавать null в UI.
        /// </summary>
        private static void AdaptersReturnNullEntries()
        {
            var jobs = new FakeJobsSource
            {
                Orders = new[]
                {
                    new DeliveryJobDto("order-1", "Хлеб", 150L, DeliveryJobPhase.Available),
                    null
                }
            };

            WorkSummary work = new WorkSummaryService(jobs).GetSummary().Value;
            MiniTestRunner.AreEqual(1, work.Orders.Length, "null-заказ отфильтрован");
            MiniTestRunner.AreEqual("order-1", work.Orders[0].OrderId, "реальный заказ сохранён");

            var debts = new FakeDebtsSource
            {
                Debts = new[] { new DebtDto("rent", "Аренда", 5000L, 100d), null }
            };

            BankSummary bank = new BankSummaryService(new FakeBalanceSource { Rubles = 10L }, debts).GetSummary().Value;
            MiniTestRunner.AreEqual(1, bank.Debts.Length, "null-долг отфильтрован");
            MiniTestRunner.AreEqual("rent", bank.Debts[0].Id, "реальный долг сохранён");
        }

        private static void FacadeRequiresDependencies()
        {
            var notifications = new PhoneNotificationService();
            var messages = new PhoneMessageService();
            var apps = Apps.PhoneAppRegistry.CreateDefault();
            var contacts = new PhoneBook();

            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<PhoneIntegrationException>(() => new PhoneFacade(null, messages, contacts, notifications)),
                "без реестра приложений");
            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<PhoneIntegrationException>(() => new PhoneFacade(apps, null, contacts, notifications)),
                "без сервиса сообщений");
            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<PhoneIntegrationException>(() => new PhoneFacade(apps, messages, null, notifications)),
                "без книги контактов");
            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<PhoneIntegrationException>(() => new PhoneFacade(apps, messages, contacts, null)),
                "без сервиса уведомлений");
            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<PhoneIntegrationException>(() => new PhoneInbox(null)),
                "PhoneInbox без сервиса");
            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<PhoneIntegrationException>(() => new PhoneNotificationHub(null, apps)),
                "PhoneNotificationHub без сервиса");
        }
    }
}
