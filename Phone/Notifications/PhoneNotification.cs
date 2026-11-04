// Life From Zero — Phone notifications (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Notifications

using System;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Notifications
{
    /// <summary>
    /// Тип уведомления. Определяет, к какому приложению/категории оно относится
    /// и как его показывать.
    /// </summary>
    public enum PhoneNotificationType
    {
        /// <summary>Новое сообщение от NPC.</summary>
        Message = 0,

        /// <summary>Предложение работы или изменение статуса заказа.</summary>
        Job = 1,

        /// <summary>Банковское уведомление (баланс, начисления).</summary>
        Bank = 2,

        /// <summary>Напоминание о долге.</summary>
        Debt = 3,

        /// <summary>Сюжетное событие.</summary>
        Story = 4,

        /// <summary>Системное уведомление самого телефона.</summary>
        System = 5
    }

    /// <summary>
    /// Уведомление телефона. Неизменяемая модель: статус прочтения меняется
    /// через <see cref="PhoneNotificationService"/>.
    /// </summary>
    public sealed class PhoneNotification
    {
        /// <summary>Уникальный ID уведомления. Повторный ID не создаёт дубликат.</summary>
        public string Id { get; }

        /// <summary>Источник уведомления: ID контакта, приложения или игровой системы.</summary>
        public string Source { get; }

        /// <summary>Тип уведомления.</summary>
        public PhoneNotificationType Type { get; }

        /// <summary>Заголовок.</summary>
        public string Title { get; }

        /// <summary>Текст.</summary>
        public string Text { get; }

        /// <summary>Игровое время создания.</summary>
        public GameMinutes CreatedAt { get; }

        /// <summary>Прочитано ли уведомление.</summary>
        public bool IsRead { get; }

        public PhoneNotification(
            string id,
            string source,
            PhoneNotificationType type,
            string title,
            string text,
            GameMinutes createdAt,
            bool isRead)
        {
            if (!PhoneId.IsValid(id))
            {
                throw new ArgumentException("Идентификатор уведомления не должен быть пустым.", nameof(id));
            }

            if (!PhoneId.IsValid(source))
            {
                throw new ArgumentException("Источник уведомления не должен быть пустым.", nameof(source));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Заголовок уведомления не должен быть пустым.", nameof(title));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Текст уведомления не должен быть пустым.", nameof(text));
            }

            Id = id;
            Source = source;
            Type = type;
            Title = title;
            Text = text;
            CreatedAt = createdAt;
            IsRead = isRead;
        }

        /// <summary>Копия уведомления, помеченная прочитанным.</summary>
        public PhoneNotification AsRead()
        {
            return IsRead
                ? this
                : new PhoneNotification(Id, Source, Type, Title, Text, CreatedAt, true);
        }

        public override string ToString()
        {
            return "PhoneNotification(" + Id + ", " + Type + ", " + (IsRead ? "read" : "unread") + ")";
        }
    }
}
