// Life From Zero — Phone tests (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Tests

using System;
using LifeFromZero.Phone.Apps;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Tests
{
    /// <summary>Тесты реестра приложений.</summary>
    public static class PhoneAppRegistryTests
    {
        public static void Register(TestSuite suite)
        {
            suite.Add("Приложения: стартовый набор из пяти приложений", DefaultApps);
            suite.Add("Приложения: регистрация нового приложения", RegisterNewApp);
            suite.Add("Приложения: дублирование ID отклоняется", DuplicateApp);
            suite.Add("Приложения: пустой ID и название отклоняются", InvalidApp);
            suite.Add("Приложения: неизвестное приложение", UnknownApp);
            suite.Add("Приложения: доступность приложения", AppAvailability);
            suite.Add("Приложения: счётчик непрочитанных", AppUnreadCounter);
            suite.Add("Приложения: переполнение счётчика", AppUnreadOverflow);
            suite.Add("Приложения: отрицательный счётчик отклоняется", AppUnreadNegative);
            suite.Add("Приложения: суммарный счётчик непрочитанных", AppTotalUnread);
            suite.Add("Приложения: конструктор PhoneApp валидирует данные", AppModelValidation);
        }

        private static void DefaultApps()
        {
            IPhoneAppRegistry registry = PhoneAppRegistry.CreateDefault();

            MiniTestRunner.AreEqual(5, registry.Apps.Count, "пять стартовых приложений");

            string[] expected = { "messages", "jobs", "bank", "contacts", "settings" };
            string[] titles = { "Сообщения", "Работа", "Банк", "Контакты", "Настройки" };

            for (int i = 0; i < expected.Length; i++)
            {
                PhoneResult<PhoneApp> app = registry.Get(expected[i]);
                MiniTestRunner.IsTrue(app.IsSuccess, "приложение " + expected[i] + " найдено");
                MiniTestRunner.AreEqual(titles[i], app.Value.Title, "название " + expected[i]);
                MiniTestRunner.IsTrue(app.Value.IsAvailable, "приложение доступно");
            }

            MiniTestRunner.AreEqual(PhoneAppCategory.Communication, registry.Get("messages").Value.Category, "категория сообщений");
            MiniTestRunner.AreEqual(PhoneAppCategory.Work, registry.Get("jobs").Value.Category, "категория работы");
            MiniTestRunner.AreEqual(PhoneAppCategory.Finance, registry.Get("bank").Value.Category, "категория банка");
            MiniTestRunner.AreEqual(PhoneAppCategory.Contacts, registry.Get("contacts").Value.Category, "категория контактов");
            MiniTestRunner.AreEqual(PhoneAppCategory.System, registry.Get("settings").Value.Category, "категория настроек");
        }

        private static void RegisterNewApp()
        {
            var registry = new PhoneAppRegistry();
            MiniTestRunner.IsTrue(
                registry.Register(new PhoneApp("notes", "Заметки", PhoneAppCategory.Other, true, 0)).IsSuccess,
                "новое приложение зарегистрировано без изменения ядра");
            MiniTestRunner.AreEqual(1, registry.Apps.Count, "в реестре одно приложение");
        }

        private static void DuplicateApp()
        {
            var registry = new PhoneAppRegistry();
            var app = new PhoneApp("bank", "Банк", PhoneAppCategory.Finance, true, 0);

            MiniTestRunner.IsTrue(registry.Register(app).IsSuccess, "первая регистрация успешна");
            MiniTestRunner.AreEqual(PhoneResultCode.DuplicateId, registry.Register(app).Code, "дубликат отклонён");
            MiniTestRunner.AreEqual(1, registry.Apps.Count, "дубликат не добавился");
        }

        private static void InvalidApp()
        {
            var registry = new PhoneAppRegistry();

            // Два уровня контракта, и они разные:
            //   * PhoneApp — модель, валидирует в конструкторе и БРОСАЕТ
            //     ArgumentException (см. AppModelValidation);
            //   * PhoneAppRegistry — сервис, ВОЗВРАЩАЕТ PhoneResultCode.
            // Прежний тест ждал InvalidArgument от Register, передавая туда
            // объект, который конструктор создать не даёт: строка падала
            // ArgumentException ещё до вызова Register (аудит ТЗ №043).
            // Поэтому проверяем: конструктор не пропускает мусор, а сервис
            // отклоняет null и принимает валидный объект.
            MiniTestRunner.IsTrue(
                Throws<ArgumentException>(() => new PhoneApp("  ", "Пусто", PhoneAppCategory.Other, true, 0)),
                "конструктор не пропускает пробельный ID");

            var blank = new PhoneApp("id-blank", "Пусто", PhoneAppCategory.Other, true, 0);
            MiniTestRunner.AreEqual(
                PhoneResultCode.Success, registry.Register(blank).Code, "валидное приложение зарегистрировано");

            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument, registry.Register(null).Code, "null-приложение отклонено");
        }

        private static void UnknownApp()
        {
            var registry = new PhoneAppRegistry();

            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, registry.Get("nope").Code, "неизвестное приложение");
            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, registry.Get(null).Code, "null-ID");
            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, registry.AddUnread("nope", 1).Code, "счётчик у неизвестного");
            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, registry.SetAvailability("nope", false).Code, "доступность у неизвестного");
        }

        private static void AppAvailability()
        {
            PhoneAppRegistry registry = PhoneAppRegistry.CreateDefault();

            MiniTestRunner.IsTrue(registry.SetAvailability("bank", false).IsSuccess, "банк заблокирован");
            MiniTestRunner.IsFalse(registry.Get("bank").Value.IsAvailable, "банк недоступен");

            MiniTestRunner.IsTrue(registry.SetAvailability("bank", true).IsSuccess, "банк разблокирован");
            MiniTestRunner.IsTrue(registry.Get("bank").Value.IsAvailable, "банк доступен");
            MiniTestRunner.AreEqual(5, registry.Apps.Count, "количество приложений не изменилось");
        }

        private static void AppUnreadCounter()
        {
            PhoneAppRegistry registry = PhoneAppRegistry.CreateDefault();

            MiniTestRunner.IsTrue(registry.AddUnread("messages", 3).IsSuccess, "добавили 3");
            MiniTestRunner.AreEqual(3, registry.Get("messages").Value.UnreadCount, "счётчик равен 3");

            MiniTestRunner.IsTrue(registry.AddUnread("messages", -1).IsSuccess, "уменьшили на 1");
            MiniTestRunner.AreEqual(2, registry.Get("messages").Value.UnreadCount, "счётчик равен 2");

            MiniTestRunner.IsTrue(registry.ClearUnread("messages").IsSuccess, "сброс счётчика");
            MiniTestRunner.AreEqual(0, registry.Get("messages").Value.UnreadCount, "счётчик обнулён");
        }

        private static void AppUnreadOverflow()
        {
            var registry = new PhoneAppRegistry();
            registry.Register(new PhoneApp("huge", "Огромный", PhoneAppCategory.Other, true, int.MaxValue));

            PhoneResult result = registry.AddUnread("huge", 1);

            MiniTestRunner.AreEqual(PhoneResultCode.CounterOverflow, result.Code, "переполнение int обнаружено");
            MiniTestRunner.AreEqual(int.MaxValue, registry.Get("huge").Value.UnreadCount, "счётчик не завернулся");
        }

        private static void AppUnreadNegative()
        {
            PhoneAppRegistry registry = PhoneAppRegistry.CreateDefault();

            PhoneResult result = registry.AddUnread("messages", -5);

            MiniTestRunner.AreEqual(PhoneResultCode.InvalidArgument, result.Code, "уход в минус отклонён");
            MiniTestRunner.AreEqual(0, registry.Get("messages").Value.UnreadCount, "счётчик остался нулевым");
        }

        private static void AppTotalUnread()
        {
            PhoneAppRegistry registry = PhoneAppRegistry.CreateDefault();
            registry.AddUnread("messages", 2);
            registry.AddUnread("jobs", 3);
            registry.AddUnread("bank", 1);

            MiniTestRunner.AreEqual(6, registry.TotalUnread, "суммарно 6 непрочитанных");
        }

        private static void AppModelValidation()
        {
            MiniTestRunner.IsTrue(Throws<ArgumentException>(() => new PhoneApp("", "X", PhoneAppCategory.Other, true, 0)),
                "пустой ID приложения");
            MiniTestRunner.IsTrue(Throws<ArgumentException>(() => new PhoneApp("id", " ", PhoneAppCategory.Other, true, 0)),
                "пустое название приложения");
            MiniTestRunner.IsTrue(Throws<ArgumentOutOfRangeException>(() => new PhoneApp("id", "X", PhoneAppCategory.Other, true, -1)),
                "отрицательный счётчик");

            var app = new PhoneApp("id", "X", PhoneAppCategory.Other, true, 2);
            PhoneApp copy = app.With(false, 5);
            MiniTestRunner.IsFalse(copy.IsAvailable, "копия недоступна");
            MiniTestRunner.AreEqual(5, copy.UnreadCount, "счётчик копии");
            MiniTestRunner.AreEqual(2, app.UnreadCount, "исходный объект не мутирован");
        }

        internal static bool Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (TException)
            {
                return true;
            }
        }
    }
}
