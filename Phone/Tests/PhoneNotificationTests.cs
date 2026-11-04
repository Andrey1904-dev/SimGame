// Life From Zero — Phone tests (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Tests

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Apps;
using LifeFromZero.Phone.Core;
using LifeFromZero.Phone.Contacts;
using LifeFromZero.Phone.Integrations;
using LifeFromZero.Phone.Messages;
using LifeFromZero.Phone.Notifications;
using LifeFromZero.Phone.Tests.Fakes;

namespace LifeFromZero.Phone.Tests
{
    /// <summary>Тесты уведомлений и фасада уведомлений.</summary>
    public static class PhoneNotificationTests
    {
        public static void Register(TestSuite suite)
        {
            suite.Add("Уведомления: создание уведомления", AddNotification);
            suite.Add("Уведомления: повторный ID не создаёт дубликат", DuplicateNotification);
            suite.Add("Уведомления: пустые поля отклоняются", InvalidNotification);
            suite.Add("Уведомления: прочтение уведомления", MarkRead);
            suite.Add("Уведомления: повторное прочтение", MarkReadTwice);
            suite.Add("Уведомления: прочтение неизвестного уведомления", MarkReadUnknown);
            suite.Add("Уведомления: прочитать все", MarkAllRead);
            suite.Add("Уведомления: фильтр по типу", FilterByType);
            suite.Add("Уведомления: список непрочитанных", UnreadList);
            suite.Add("Уведомления: снимок для сохранения", Snapshot);
            suite.Add("Уведомления: время из адаптера часов", CreatedAtFromClock);
            suite.Add("Уведомления: сбой часов не теряет уведомление", ClockFailure);
            suite.Add("Уведомления: хаб увеличивает счётчик приложения", HubBadge);
            suite.Add("Уведомления: хаб не дублирует счётчик", HubNoDuplicateBadge);
            suite.Add("Уведомления: хаб без приложения", HubWithoutApp);
            suite.Add("Уведомления: хаб только в общий список", HubInboxOnly);
            suite.Add("Уведомления: все шесть типов поддерживаются", AllTypes);
            suite.Add("Уведомления: сводные счётчики фасада", FacadeCounters);

            // Регрессионные тесты аудита ТЗ №015.
            suite.Add("Уведомления: восстановление из снимка", RestoreFromSnapshot);
            suite.Add("Уведомления: битый снимок не меняет сервис", RestoreRejectsBrokenSnapshot);
            suite.Add("Уведомления: повторный Push после отказа бейджа не дублирует", HubBadgeFailureKeepsSingleNotification);
            suite.Add("Уведомления: снимок защищён от мутации вызывающим кодом", SnapshotIsDefensiveCopy);
        }

        // ---------- Восстановление и устойчивость (ТЗ №015) ----------

        private static void RestoreFromSnapshot()
        {
            var source = new PhoneNotificationService();
            source.Add("n1", "bank", PhoneNotificationType.Bank, "Баланс", "Начисление", new FakePhoneClock(5d));
            source.Add("n2", "work", PhoneNotificationType.Job, "Заказ", "Новый заказ", new FakePhoneClock(6d));
            source.MarkRead("n2");

            var target = new PhoneNotificationService();
            string error;
            PhoneResult restored = PhoneNotificationsRestore.RestoreInto(target, source.CreateSnapshot(), out error);

            MiniTestRunner.IsTrue(restored.IsSuccess, "снимок восстановлен: " + error);
            MiniTestRunner.AreEqual(2, target.Notifications.Count, "два уведомления");
            MiniTestRunner.AreEqual(1, target.UnreadCount, "непрочитанное одно");
            MiniTestRunner.IsTrue(target.Get("n2").Value.IsRead, "статус прочтения восстановлен");
            MiniTestRunner.AreEqual(PhoneNotificationType.Bank, target.Get("n1").Value.Type, "тип восстановлен");
            MiniTestRunner.AreEqual(5d, target.Get("n1").Value.CreatedAt.Minutes, "время восстановлено");

            // Повторный Push с восстановленным ID не должен создать дубликат.
            PhoneResult duplicate = target.Add("n1", "bank", PhoneNotificationType.Bank, "Баланс", "Ещё", new FakePhoneClock(7d));
            MiniTestRunner.AreEqual(PhoneResultCode.DuplicateId, duplicate.Code, "дедупликация работает после восстановления");
            MiniTestRunner.AreEqual(2, target.Notifications.Count, "количество не изменилось");
        }

        private static void RestoreRejectsBrokenSnapshot()
        {
            var target = new PhoneNotificationService();
            target.Add("live", "bank", PhoneNotificationType.Bank, "Живое", "Текст", new FakePhoneClock(1d));

            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument,
                PhoneNotificationsRestore.RestoreInto(target, null, out string e1).Code,
                "null-снимок отклонён");

            var duplicate = new PhoneNotificationsSnapshot(new List<PhoneNotificationSnapshot>
            {
                new PhoneNotificationSnapshot("n1", "bank", PhoneNotificationType.Bank, "Раз", "Т", GameMinutes.Unknown, false),
                new PhoneNotificationSnapshot("n1", "bank", PhoneNotificationType.Bank, "Два", "Т", GameMinutes.Unknown, false)
            });

            MiniTestRunner.AreEqual(
                PhoneResultCode.DuplicateId,
                PhoneNotificationsRestore.RestoreInto(target, duplicate, out string e2).Code,
                "дубль ID отклонён");

            var unknownType = new PhoneNotificationsSnapshot(new List<PhoneNotificationSnapshot>
            {
                new PhoneNotificationSnapshot("n1", "bank", (PhoneNotificationType)42, "Заголовок", "Т", GameMinutes.Unknown, false)
            });

            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument,
                PhoneNotificationsRestore.RestoreInto(target, unknownType, out string e3).Code,
                "неизвестный тип отклонён");

            var emptySource = new PhoneNotificationsSnapshot(new List<PhoneNotificationSnapshot>
            {
                new PhoneNotificationSnapshot("n1", "  ", PhoneNotificationType.Bank, "Заголовок", "Т", GameMinutes.Unknown, false)
            });

            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument,
                PhoneNotificationsRestore.RestoreInto(target, emptySource, out string e4).Code,
                "пустой источник отклонён");

            MiniTestRunner.AreEqual(1, target.Notifications.Count, "живое уведомление не тронуто");
            MiniTestRunner.AreEqual(1, target.UnreadCount, "счётчик не сброшен");
        }

        /// <summary>
        /// Если реестр приложений падает, уведомление всё равно создано; повторный Push
        /// не должен ни создавать дубликат, ни «забыть» ошибку молча в виде исключения.
        /// </summary>
        private static void HubBadgeFailureKeepsSingleNotification()
        {
            var apps = new ThrowingAppRegistry();
            var notifications = new PhoneNotificationService();
            var hub = new PhoneNotificationHub(notifications, apps);
            var clock = new FakePhoneClock(0d);

            PhoneResult first = hub.Push(
                "job-1", "work", PhoneNotificationType.Job, "Заказ", "Текст",
                "jobs", PhoneNotificationChannel.AppBadge, clock);
            PhoneResult second = hub.Push(
                "job-1", "work", PhoneNotificationType.Job, "Заказ", "Текст",
                "jobs", PhoneNotificationChannel.AppBadge, clock);

            MiniTestRunner.IsTrue(first.IsSuccess, "исключение реестра не отменяет уведомление");
            MiniTestRunner.AreEqual(PhoneResultCode.DuplicateId, second.Code, "повтор отклонён");
            MiniTestRunner.AreEqual(1, notifications.Notifications.Count, "уведомление одно");
            MiniTestRunner.AreEqual(1, apps.AddUnreadCalls, "повторный Push не доходит до бейджа (дедупликация раньше)");
        }

        private static void SnapshotIsDefensiveCopy()
        {
            var notifications = new PhoneNotificationService();
            var list = new List<PhoneNotificationSnapshot>
            {
                new PhoneNotificationSnapshot("n1", "bank", PhoneNotificationType.Bank, "Заголовок", "Т", GameMinutes.Unknown, false)
            };

            var snapshot = new PhoneNotificationsSnapshot(list);
            list.Clear();

            MiniTestRunner.AreEqual(1, snapshot.Notifications.Count, "снимок не зависит от списка вызывающего кода");
        }

        /// <summary>Реестр приложений, который падает на любом изменении счётчика.</summary>
        private sealed class ThrowingAppRegistry : IPhoneAppRegistry
        {
            public int AddUnreadCalls { get; private set; }

            public System.Collections.Generic.IReadOnlyList<PhoneApp> Apps =>
                new System.Collections.Generic.List<PhoneApp>().AsReadOnly();

            public int TotalUnread => 0;

            public PhoneResult Register(PhoneApp app)
            {
                return PhoneResult.Ok(app == null ? null : app.Id);
            }

            public PhoneResult<PhoneApp> Get(string appId)
            {
                return PhoneResult<PhoneApp>.Fail(PhoneResultCode.NotFound);
            }

            public PhoneResult SetAvailability(string appId, bool isAvailable)
            {
                return PhoneResult.Fail(PhoneResultCode.NotFound, appId);
            }

            public PhoneResult AddUnread(string appId, int delta)
            {
                AddUnreadCalls++;
                throw new InvalidOperationException("Тестовый сбой реестра приложений.");
            }

            public PhoneResult ClearUnread(string appId)
            {
                return PhoneResult.Fail(PhoneResultCode.NotFound, appId);
            }
        }

        private static PhoneNotification NewNotification(string id, PhoneNotificationType type, bool isRead)
        {
            return new PhoneNotification(
                id, "source", type, "Заголовок", "Текст", GameMinutes.FromMinutes(10d), isRead);
        }

        private static void AddNotification()
        {
            var notifications = new PhoneNotificationService();

            PhoneResult added = notifications.Add(NewNotification("n1", PhoneNotificationType.Message, false));

            MiniTestRunner.IsTrue(added.IsSuccess, "уведомление добавлено");
            MiniTestRunner.AreEqual(1, notifications.Notifications.Count, "одно уведомление");
            MiniTestRunner.AreEqual(1, notifications.UnreadCount, "одно непрочитанное");
            MiniTestRunner.IsTrue(notifications.Get("n1").IsSuccess, "уведомление найдено по ID");
        }

        private static void DuplicateNotification()
        {
            var notifications = new PhoneNotificationService();
            notifications.Add(NewNotification("n1", PhoneNotificationType.Message, false));

            PhoneResult duplicate = notifications.Add(NewNotification("n1", PhoneNotificationType.Bank, true));

            MiniTestRunner.AreEqual(PhoneResultCode.DuplicateId, duplicate.Code, "дубликат отклонён");
            MiniTestRunner.AreEqual(1, notifications.Notifications.Count, "количество не изменилось");
            MiniTestRunner.AreEqual(PhoneNotificationType.Message, notifications.Get("n1").Value.Type, "тип не перезаписан");
            MiniTestRunner.AreEqual(1, notifications.UnreadCount, "статус не перезаписан");
        }

        private static void InvalidNotification()
        {
            var notifications = new PhoneNotificationService();

            MiniTestRunner.AreEqual(PhoneResultCode.InvalidArgument, notifications.Add(null).Code, "null-уведомление");
            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument,
                notifications.Add(new PhoneNotification("", "s", PhoneNotificationType.Message, "T", "X", GameMinutes.Unknown, false)).Code,
                "пустой ID");
        }

        private static void MarkRead()
        {
            var notifications = new PhoneNotificationService();
            notifications.Add(NewNotification("n1", PhoneNotificationType.Message, false));

            MiniTestRunner.IsTrue(notifications.MarkRead("n1").IsSuccess, "прочитано");
            MiniTestRunner.IsTrue(notifications.Get("n1").Value.IsRead, "флаг прочтения");
            MiniTestRunner.AreEqual(0, notifications.UnreadCount, "непрочитанных нет");
        }

        private static void MarkReadTwice()
        {
            var notifications = new PhoneNotificationService();
            notifications.Add(NewNotification("n1", PhoneNotificationType.Message, false));

            notifications.MarkRead("n1");
            PhoneResult second = notifications.MarkRead("n1");

            MiniTestRunner.IsTrue(second.IsSuccess, "повторное прочтение — не ошибка");
            MiniTestRunner.AreEqual(0, notifications.UnreadCount, "счётчик не ушёл в минус");
        }

        private static void MarkReadUnknown()
        {
            var notifications = new PhoneNotificationService();

            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, notifications.MarkRead("nope").Code, "неизвестное уведомление");
            MiniTestRunner.AreEqual(PhoneResultCode.InvalidArgument, notifications.MarkRead(" ").Code, "пустой ID");
        }

        private static void MarkAllRead()
        {
            var notifications = new PhoneNotificationService();
            notifications.Add(NewNotification("n1", PhoneNotificationType.Message, false));
            notifications.Add(NewNotification("n2", PhoneNotificationType.Bank, false));
            notifications.Add(NewNotification("n3", PhoneNotificationType.Story, true));

            MiniTestRunner.IsTrue(notifications.MarkAllRead().IsSuccess, "прочитано всё");
            MiniTestRunner.AreEqual(0, notifications.UnreadCount, "непрочитанных нет");
            MiniTestRunner.AreEqual(0, notifications.GetUnread().Count, "список непрочитанных пуст");
        }

        private static void FilterByType()
        {
            var notifications = new PhoneNotificationService();
            notifications.Add(NewNotification("n1", PhoneNotificationType.Message, false));
            notifications.Add(NewNotification("n2", PhoneNotificationType.Bank, false));
            notifications.Add(NewNotification("n3", PhoneNotificationType.Message, true));

            MiniTestRunner.AreEqual(2, notifications.GetByType(PhoneNotificationType.Message).Count, "два сообщения");
            MiniTestRunner.AreEqual(1, notifications.GetByType(PhoneNotificationType.Bank).Count, "один банк");
            MiniTestRunner.AreEqual(0, notifications.GetByType(PhoneNotificationType.Debt).Count, "долгов нет");
        }

        private static void UnreadList()
        {
            var notifications = new PhoneNotificationService();
            notifications.Add(NewNotification("n1", PhoneNotificationType.Message, false));
            notifications.Add(NewNotification("n2", PhoneNotificationType.Job, true));
            notifications.Add(NewNotification("n3", PhoneNotificationType.Debt, false));

            IReadOnlyList<PhoneNotification> unread = notifications.GetUnread();

            MiniTestRunner.AreEqual(2, unread.Count, "два непрочитанных");
            MiniTestRunner.AreEqual("n1", unread[0].Id, "порядок сохранён");
            MiniTestRunner.AreEqual("n3", unread[1].Id, "порядок сохранён");
        }

        private static void Snapshot()
        {
            var notifications = new PhoneNotificationService();
            notifications.Add(NewNotification("n1", PhoneNotificationType.Bank, false));
            notifications.MarkRead("n1");

            PhoneNotificationsSnapshot snapshot = notifications.CreateSnapshot();

            MiniTestRunner.AreEqual(1, snapshot.Notifications.Count, "одно уведомление в снимке");
            MiniTestRunner.IsTrue(snapshot.Notifications[0].IsRead, "статус в снимке");
            MiniTestRunner.AreEqual("source", snapshot.Notifications[0].Source, "источник в снимке");
            MiniTestRunner.AreEqual(10d, snapshot.Notifications[0].CreatedAt.Minutes, "время в снимке");
        }

        private static void CreatedAtFromClock()
        {
            var notifications = new PhoneNotificationService();
            var clock = new FakePhoneClock(250d);

            PhoneResult added = notifications.Add(
                "n1", "work", PhoneNotificationType.Job, "Заказ", "Доступен новый заказ", clock);

            MiniTestRunner.IsTrue(added.IsSuccess, "уведомление создано");
            MiniTestRunner.AreEqual(250d, notifications.Get("n1").Value.CreatedAt.Minutes, "время из часов");
        }

        private static void ClockFailure()
        {
            var notifications = new PhoneNotificationService();
            var clock = new FakePhoneClock(0d) { ThrowOnRead = true };

            PhoneResult added = notifications.Add(
                "n1", "work", PhoneNotificationType.Job, "Заказ", "Текст", clock);

            MiniTestRunner.IsTrue(added.IsSuccess, "уведомление сохранено несмотря на сбой часов");
            MiniTestRunner.IsFalse(notifications.Get("n1").Value.CreatedAt.IsValid, "время неизвестно");
        }

        private static void HubBadge()
        {
            PhoneAppRegistry apps = PhoneAppRegistry.CreateDefault();
            var notifications = new PhoneNotificationService();
            var hub = new PhoneNotificationHub(notifications, apps);
            var clock = new FakePhoneClock(5d);

            PhoneResult pushed = hub.Push(
                "job-1", "work", PhoneNotificationType.Job, "Заказ", "Новый заказ",
                "jobs", PhoneNotificationChannel.AppBadge, clock);

            MiniTestRunner.IsTrue(pushed.IsSuccess, "уведомление отправлено");
            MiniTestRunner.AreEqual(1, apps.Get("jobs").Value.UnreadCount, "счётчик приложения увеличен");
            MiniTestRunner.AreEqual(1, notifications.UnreadCount, "уведомление непрочитано");
        }

        private static void HubNoDuplicateBadge()
        {
            PhoneAppRegistry apps = PhoneAppRegistry.CreateDefault();
            var notifications = new PhoneNotificationService();
            var hub = new PhoneNotificationHub(notifications, apps);
            var clock = new FakePhoneClock(0d);

            hub.Push("job-1", "work", PhoneNotificationType.Job, "Заказ", "Текст", "jobs", PhoneNotificationChannel.Banner, clock);
            PhoneResult second = hub.Push(
                "job-1", "work", PhoneNotificationType.Job, "Заказ", "Текст", "jobs", PhoneNotificationChannel.Banner, clock);

            MiniTestRunner.AreEqual(PhoneResultCode.DuplicateId, second.Code, "повтор отклонён");
            MiniTestRunner.AreEqual(1, apps.Get("jobs").Value.UnreadCount, "счётчик приложения не удвоился");
            MiniTestRunner.AreEqual(1, notifications.Notifications.Count, "уведомление одно");
        }

        private static void HubWithoutApp()
        {
            PhoneAppRegistry apps = PhoneAppRegistry.CreateDefault();
            var notifications = new PhoneNotificationService();
            var hub = new PhoneNotificationHub(notifications, apps);
            var clock = new FakePhoneClock(0d);

            PhoneResult pushed = hub.Push(
                "n1", "sys", PhoneNotificationType.System, "Система", "Текст", "unknown_app", PhoneNotificationChannel.Banner, clock);

            MiniTestRunner.IsTrue(pushed.IsSuccess, "уведомление создано даже без приложения");
            MiniTestRunner.AreEqual(1, notifications.Notifications.Count, "уведомление есть");
        }

        private static void HubInboxOnly()
        {
            PhoneAppRegistry apps = PhoneAppRegistry.CreateDefault();
            var notifications = new PhoneNotificationService();
            var hub = new PhoneNotificationHub(notifications, apps);
            var clock = new FakePhoneClock(0d);

            hub.Push("n1", "bank", PhoneNotificationType.Bank, "Баланс", "Текст", "bank", PhoneNotificationChannel.Inbox, clock);

            MiniTestRunner.AreEqual(0, apps.Get("bank").Value.UnreadCount, "канал Inbox не трогает бейдж");
            MiniTestRunner.AreEqual(1, notifications.Notifications.Count, "уведомление в списке");
        }

        private static void AllTypes()
        {
            var notifications = new PhoneNotificationService();
            var types = (PhoneNotificationType[])Enum.GetValues(typeof(PhoneNotificationType));

            MiniTestRunner.AreEqual(6, types.Length, " шесть типов по ТЗ");

            for (int i = 0; i < types.Length; i++)
            {
                string id = "type-" + types[i];
                MiniTestRunner.IsTrue(
                    notifications.Add(NewNotification(id, types[i], false)).IsSuccess,
                    "тип поддерживается: " + types[i]);
            }

            MiniTestRunner.AreEqual(6, notifications.Notifications.Count, "все типы сохранены");
            MiniTestRunner.AreEqual(6, notifications.UnreadCount, "все непрочитаны");
        }

        private static void FacadeCounters()
        {
            var facade = new PhoneFacade(
                PhoneAppRegistry.CreateDefault(),
                new PhoneMessageService(),
                new PhoneBook(),
                new PhoneNotificationService());

            var clock = new FakePhoneClock(0d);
            facade.Messages.ReceiveFromContact("anna", "Привет", clock);
            facade.Notifier.Push("n1", "anna", PhoneNotificationType.Message, "Сообщение", "Текст", "messages", PhoneNotificationChannel.AppBadge, clock);

            MiniTestRunner.AreEqual(1, facade.UnreadMessages, "одно непрочитанное сообщение");
            MiniTestRunner.AreEqual(1, facade.UnreadNotifications, "одно непрочитанное уведомление");
            MiniTestRunner.AreEqual(1, facade.UnreadApps, "один бейдж приложения");
        }
    }
}
