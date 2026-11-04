// Life From Zero — Phone apps (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Apps

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Apps
{
    /// <summary>
    /// Реестр приложений смартфона. Хранит приложения в памяти, не является Singleton.
    /// Новые приложения регистрируются через <see cref="Register"/> — ядро менять не нужно.
    /// </summary>
    public interface IPhoneAppRegistry
    {
        /// <summary>Регистрирует приложение. Пустой ID — InvalidArgument, повтор — DuplicateId.</summary>
        PhoneResult Register(PhoneApp app);

        /// <summary>Ищет приложение по ID.</summary>
        PhoneResult<PhoneApp> Get(string appId);

        /// <summary>Все зарегистрированные приложения в порядке регистрации.</summary>
        IReadOnlyList<PhoneApp> Apps { get; }

        /// <summary>Меняет доступность приложения.</summary>
        PhoneResult SetAvailability(string appId, bool isAvailable);

        /// <summary>Добавляет к счётчику непрочитанных <paramref name="delta"/> (может быть отрицательным).</summary>
        PhoneResult AddUnread(string appId, int delta);

        /// <summary>Сбрасывает счётчик непрочитанных в ноль.</summary>
        PhoneResult ClearUnread(string appId);

        /// <summary>Суммарное количество непрочитанных по всем приложениям (с защитой от переполнения).</summary>
        int TotalUnread { get; }
    }

    /// <summary>
    /// Реализация реестра приложений в памяти.
    /// </summary>
    public sealed class PhoneAppRegistry : IPhoneAppRegistry
    {
        private readonly Dictionary<string, PhoneApp> _apps =
            new Dictionary<string, PhoneApp>(StringComparer.Ordinal);

        private readonly List<PhoneApp> _order = new List<PhoneApp>();

        /// <summary>
        /// Стартовый набор приложений ТЗ: Сообщения, Работа, Банк, Контакты, Настройки.
        /// </summary>
        public static IReadOnlyList<PhoneApp> CreateDefaultApps()
        {
            return new List<PhoneApp>
            {
                new PhoneApp(PhoneAppIds.Messages, "Сообщения", PhoneAppCategory.Communication, true, 0),
                new PhoneApp(PhoneAppIds.Jobs, "Работа", PhoneAppCategory.Work, true, 0),
                new PhoneApp(PhoneAppIds.Bank, "Банк", PhoneAppCategory.Finance, true, 0),
                new PhoneApp(PhoneAppIds.Contacts, "Контакты", PhoneAppCategory.Contacts, true, 0),
                new PhoneApp(PhoneAppIds.Settings, "Настройки", PhoneAppCategory.System, true, 0)
            }.AsReadOnly();
        }

        /// <summary>Реестр с приложениями по умолчанию.</summary>
        public static PhoneAppRegistry CreateDefault()
        {
            var registry = new PhoneAppRegistry();

            foreach (PhoneApp app in CreateDefaultApps())
            {
                registry.Register(app);
            }

            return registry;
        }

        public IReadOnlyList<PhoneApp> Apps => _order.AsReadOnly();

        public int TotalUnread
        {
            get
            {
                long total = 0L;

                for (int i = 0; i < _order.Count; i++)
                {
                    total += _order[i].UnreadCount;
                }

                return total > int.MaxValue ? int.MaxValue : (int)total;
            }
        }

        public PhoneResult Register(PhoneApp app)
        {
            if (app == null || !PhoneId.IsValid(app.Id))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, app == null ? null : app.Id);
            }

            if (_apps.ContainsKey(app.Id))
            {
                return PhoneResult.Fail(PhoneResultCode.DuplicateId, app.Id);
            }

            _apps.Add(app.Id, app);
            _order.Add(app);

            return PhoneResult.Ok(app.Id);
        }

        public PhoneResult<PhoneApp> Get(string appId)
        {
            PhoneApp app;

            if (!PhoneId.IsValid(appId) || !_apps.TryGetValue(appId, out app))
            {
                return PhoneResult<PhoneApp>.Fail(PhoneResultCode.NotFound);
            }

            return PhoneResult<PhoneApp>.Ok(app);
        }

        public PhoneResult SetAvailability(string appId, bool isAvailable)
        {
            PhoneApp app;

            if (!PhoneId.IsValid(appId))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, appId);
            }

            if (!_apps.TryGetValue(appId, out app))
            {
                return PhoneResult.Fail(PhoneResultCode.NotFound, appId);
            }

            Replace(app.With(isAvailable, app.UnreadCount));
            return PhoneResult.Ok(appId);
        }

        public PhoneResult AddUnread(string appId, int delta)
        {
            PhoneApp app;

            if (!PhoneId.IsValid(appId))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, appId);
            }

            if (!_apps.TryGetValue(appId, out app))
            {
                return PhoneResult.Fail(PhoneResultCode.NotFound, appId);
            }

            // Сложение в long: int.MaxValue + 1 не должно заворачиваться в минимум.
            long next = (long)app.UnreadCount + delta;

            if (next < 0)
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, appId);
            }

            if (next > int.MaxValue)
            {
                return PhoneResult.Fail(PhoneResultCode.CounterOverflow, appId);
            }

            Replace(app.With(app.IsAvailable, (int)next));
            return PhoneResult.Ok(appId);
        }

        public PhoneResult ClearUnread(string appId)
        {
            PhoneApp app;

            if (!PhoneId.IsValid(appId))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, appId);
            }

            if (!_apps.TryGetValue(appId, out app))
            {
                return PhoneResult.Fail(PhoneResultCode.NotFound, appId);
            }

            Replace(app.With(app.IsAvailable, 0));
            return PhoneResult.Ok(appId);
        }

        private void Replace(PhoneApp updated)
        {
            _apps[updated.Id] = updated;

            for (int i = 0; i < _order.Count; i++)
            {
                if (PhoneId.AreSame(_order[i].Id, updated.Id))
                {
                    _order[i] = updated;
                    return;
                }
            }
        }
    }
}
