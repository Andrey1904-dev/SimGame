// Life From Zero — Phone notifications (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Notifications

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Notifications
{
    /// <summary>
    /// Сервис уведомлений телефона: создание, дедупликация по ID, прочтение, выборки.
    /// Хранит уведомления в памяти, Singleton-ом не является.
    /// </summary>
    public interface IPhoneNotificationService
    {
        /// <summary>
        /// Добавляет уведомление. Повторный ID не создаёт дубликат: возвращается
        /// DuplicateId, существующее уведомление не меняется.
        /// </summary>
        PhoneResult Add(PhoneNotification notification);

        /// <summary>
        /// Создаёт уведомление по полям. Время читается из адаптера (может быть Unknown).
        /// </summary>
        PhoneResult Add(
            string id,
            string source,
            PhoneNotificationType type,
            string title,
            string text,
            IPhoneClock clock);

        /// <summary>Все уведомления в порядке добавления (новые в конце).</summary>
        IReadOnlyList<PhoneNotification> Notifications { get; }

        /// <summary>Уведомления указанного типа.</summary>
        IReadOnlyList<PhoneNotification> GetByType(PhoneNotificationType type);

        /// <summary>Непрочитанные уведомления.</summary>
        IReadOnlyList<PhoneNotification> GetUnread();

        /// <summary>Уведомление по ID.</summary>
        PhoneResult<PhoneNotification> Get(string notificationId);

        /// <summary>Помечает уведомление прочитанным. Повторное прочтение — Success (идемпотентно).</summary>
        PhoneResult MarkRead(string notificationId);

        /// <summary>Помечает прочитанными все уведомления.</summary>
        PhoneResult MarkAllRead();

        /// <summary>Количество непрочитанных.</summary>
        int UnreadCount { get; }

        /// <summary>Снимок для будущего сохранения игры.</summary>
        PhoneNotificationsSnapshot CreateSnapshot();
    }

    /// <summary>Реализация сервиса уведомлений в памяти.</summary>
    public sealed class PhoneNotificationService : IPhoneNotificationService
    {
        private readonly Dictionary<string, PhoneNotification> _byId =
            new Dictionary<string, PhoneNotification>(StringComparer.Ordinal);

        private readonly List<PhoneNotification> _order = new List<PhoneNotification>();

        public IReadOnlyList<PhoneNotification> Notifications => _order.AsReadOnly();

        public int UnreadCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _order.Count; i++)
                {
                    if (!_order[i].IsRead)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public PhoneResult Add(PhoneNotification notification)
        {
            if (notification == null || !PhoneId.IsValid(notification.Id))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument,
                    notification == null ? null : notification.Id);
            }

            if (_byId.ContainsKey(notification.Id))
            {
                // Дедупликация: существующее уведомление не заменяется.
                return PhoneResult.Fail(PhoneResultCode.DuplicateId, notification.Id);
            }

            _byId.Add(notification.Id, notification);
            _order.Add(notification);

            return PhoneResult.Ok(notification.Id);
        }

        public PhoneResult Add(
            string id,
            string source,
            PhoneNotificationType type,
            string title,
            string text,
            IPhoneClock clock)
        {
            if (!PhoneId.IsValid(id))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, id);
            }

            if (_byId.ContainsKey(id))
            {
                return PhoneResult.Fail(PhoneResultCode.DuplicateId, id);
            }

            if (!PhoneId.IsValid(source)
                || string.IsNullOrWhiteSpace(title)
                || string.IsNullOrWhiteSpace(text))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, id);
            }

            GameMinutes createdAt = PhoneClockReader.Read(clock);

            return Add(new PhoneNotification(id, source, type, title, text, createdAt, false));
        }

        public IReadOnlyList<PhoneNotification> GetByType(PhoneNotificationType type)
        {
            var result = new List<PhoneNotification>();

            for (int i = 0; i < _order.Count; i++)
            {
                if (_order[i].Type == type)
                {
                    result.Add(_order[i]);
                }
            }

            return result.AsReadOnly();
        }

        public IReadOnlyList<PhoneNotification> GetUnread()
        {
            var result = new List<PhoneNotification>();

            for (int i = 0; i < _order.Count; i++)
            {
                if (!_order[i].IsRead)
                {
                    result.Add(_order[i]);
                }
            }

            return result.AsReadOnly();
        }

        public PhoneResult<PhoneNotification> Get(string notificationId)
        {
            PhoneNotification notification;

            if (!PhoneId.IsValid(notificationId) || !_byId.TryGetValue(notificationId, out notification))
            {
                return PhoneResult<PhoneNotification>.Fail(PhoneResultCode.NotFound);
            }

            return PhoneResult<PhoneNotification>.Ok(notification);
        }

        public PhoneResult MarkRead(string notificationId)
        {
            PhoneNotification notification;

            if (!PhoneId.IsValid(notificationId))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, notificationId);
            }

            if (!_byId.TryGetValue(notificationId, out notification))
            {
                return PhoneResult.Fail(PhoneResultCode.NotFound, notificationId);
            }

            if (!notification.IsRead)
            {
                PhoneNotification updated = notification.AsRead();
                _byId[notificationId] = updated;

                for (int i = 0; i < _order.Count; i++)
                {
                    if (PhoneId.AreSame(_order[i].Id, notificationId))
                    {
                        _order[i] = updated;
                        break;
                    }
                }
            }

            // Повторное прочтение — не ошибка.
            return PhoneResult.Ok(notificationId);
        }

        public PhoneResult MarkAllRead()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                if (!_order[i].IsRead)
                {
                    PhoneNotification updated = _order[i].AsRead();
                    _order[i] = updated;
                    _byId[updated.Id] = updated;
                }
            }

            return PhoneResult.Ok(null);
        }

        /// <summary>
        /// Полностью заменяет содержимое сервиса готовыми уведомлениями.
        /// Вызывается только <see cref="PhoneNotificationsRestore"/> после проверки снимка.
        /// Намеренно <c>internal</c>: обойти дедупликацию ID снаружи нельзя.
        /// </summary>
        internal void ReplaceAll(IReadOnlyList<PhoneNotification> notifications)
        {
            if (notifications == null)
            {
                throw new ArgumentNullException(nameof(notifications));
            }

            _byId.Clear();
            _order.Clear();

            for (int i = 0; i < notifications.Count; i++)
            {
                PhoneNotification notification = notifications[i];

                if (notification == null || _byId.ContainsKey(notification.Id))
                {
                    throw new PhoneIntegrationException(
                        "Повреждённый набор уведомлений при восстановлении: " + (notification == null ? "<null>" : notification.Id));
                }

                _byId.Add(notification.Id, notification);
                _order.Add(notification);
            }
        }

        public PhoneNotificationsSnapshot CreateSnapshot()
        {
            var items = new List<PhoneNotificationSnapshot>(_order.Count);

            for (int i = 0; i < _order.Count; i++)
            {
                PhoneNotification source = _order[i];
                items.Add(new PhoneNotificationSnapshot(
                    source.Id,
                    source.Source,
                    source.Type,
                    source.Title,
                    source.Text,
                    source.CreatedAt,
                    source.IsRead));
            }

            return new PhoneNotificationsSnapshot(items);
        }
    }
}
