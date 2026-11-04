// Life From Zero — Phone notifications (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Notifications
//
// Снимок (DTO) для будущего сохранения игры: только данные, без поведения.

using System.Collections.Generic;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Notifications
{
    /// <summary>Снимок одного уведомления.</summary>
    public sealed class PhoneNotificationSnapshot
    {
        public string Id { get; }

        public string Source { get; }

        public PhoneNotificationType Type { get; }

        public string Title { get; }

        public string Text { get; }

        public GameMinutes CreatedAt { get; }

        public bool IsRead { get; }

        public PhoneNotificationSnapshot(
            string id,
            string source,
            PhoneNotificationType type,
            string title,
            string text,
            GameMinutes createdAt,
            bool isRead)
        {
            Id = id;
            Source = source;
            Type = type;
            Title = title;
            Text = text;
            CreatedAt = createdAt;
            IsRead = isRead;
        }
    }

    /// <summary>Снимок всех уведомлений телефона.</summary>
    public sealed class PhoneNotificationsSnapshot
    {
        public IReadOnlyList<PhoneNotificationSnapshot> Notifications { get; }

        public PhoneNotificationsSnapshot(IReadOnlyList<PhoneNotificationSnapshot> notifications)
        {
            // Защитная копия: снимок иммутабелен по отношению к списку вызывающего кода.
            Notifications = notifications == null
                ? (IReadOnlyList<PhoneNotificationSnapshot>)new List<PhoneNotificationSnapshot>().AsReadOnly()
                : new List<PhoneNotificationSnapshot>(notifications).AsReadOnly();
        }
    }
}
