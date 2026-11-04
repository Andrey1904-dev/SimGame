// Life From Zero — Phone apps (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Apps

using System;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Apps
{
    /// <summary>
    /// Категория приложения. Расширяется добавлением значений без изменения ядра.
    /// </summary>
    public enum PhoneAppCategory
    {
        /// <summary>Переписки, уведомления, сюжетные сообщения.</summary>
        Communication = 0,

        /// <summary>Работа, заказы, заработок.</summary>
        Work = 1,

        /// <summary>Банк, баланс, финансовые уведомления.</summary>
        Finance = 2,

        /// <summary>Контакты, телефонная книга.</summary>
        Contacts = 3,

        /// <summary>Настройки самого устройства.</summary>
        System = 4,

        /// <summary>Сюжет и повествовательные приложения.</summary>
        Story = 5,

        /// <summary>Прочее.</summary>
        Other = 6
    }

    /// <summary>
    /// Приложение смартфона. Неизменяемая модель: доступность и счётчик непрочитанных
    /// меняются через <see cref="PhoneAppRegistry"/>, а не мутацией объекта.
    /// </summary>
    public sealed class PhoneApp
    {
        /// <summary>Уникальный строковый ID приложения, например "messages".</summary>
        public string Id { get; }

        /// <summary>Название, отображаемое на экране.</summary>
        public string Title { get; }

        /// <summary>Категория приложения.</summary>
        public PhoneAppCategory Category { get; }

        /// <summary>Доступно ли приложение игроку (например, банк может быть заблокирован).</summary>
        public bool IsAvailable { get; }

        /// <summary>Количество непрочитанных уведомлений приложения.</summary>
        public int UnreadCount { get; }

        public PhoneApp(
            string id,
            string title,
            PhoneAppCategory category,
            bool isAvailable,
            int unreadCount)
        {
            if (!PhoneId.IsValid(id))
            {
                throw new ArgumentException("Идентификатор приложения не должен быть пустым.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Название приложения не должно быть пустым.", nameof(title));
            }

            if (unreadCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unreadCount), unreadCount, "Счётчик непрочитанных не может быть отрицательным.");
            }

            Id = id;
            Title = title;
            Category = category;
            IsAvailable = isAvailable;
            UnreadCount = unreadCount;
        }

        /// <summary>Копия приложения с другим счётчиком непрочитанных и/или доступностью.</summary>
        public PhoneApp With(bool isAvailable, int unreadCount)
        {
            return new PhoneApp(Id, Title, Category, isAvailable, unreadCount);
        }

        public override string ToString()
        {
            return Id + " (" + Category + (IsAvailable ? "" : ", unavailable") + ", unread=" + UnreadCount + ")";
        }
    }
}
