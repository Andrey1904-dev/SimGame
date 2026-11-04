// Life From Zero — Phone integrations (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Integrations

using LifeFromZero.Phone.Apps;
using LifeFromZero.Phone.Contacts;
using LifeFromZero.Phone.Core;
using LifeFromZero.Phone.Messages;
using LifeFromZero.Phone.Notifications;

namespace LifeFromZero.Phone.Integrations
{
    /// <summary>
    /// Точка сборки телефона: одно место, где создаются все сервисы.
    /// Singleton-ом НЕ является — экземпляр создаётся на сцене/в загрузчике.
    /// Codex собирает её так:
    /// <code>
    /// var phone = new PhoneFacade(
    ///     PhoneAppRegistry.CreateDefault(),
    ///     new PhoneMessageService(),
    ///     new PhoneBook(),
    ///     new PhoneNotificationService());
    /// </code>
    /// </summary>
    public sealed class PhoneFacade
    {
        public PhoneFacade(
            IPhoneAppRegistry apps,
            IPhoneMessageService messages,
            IPhoneBook contacts,
            IPhoneNotificationService notifications)
        {
            Apps = apps ?? throw new PhoneIntegrationException("Не передан реестр приложений (IPhoneAppRegistry).");
            Messages = messages ?? throw new PhoneIntegrationException("Не передан сервис сообщений (IPhoneMessageService).");
            Contacts = contacts ?? throw new PhoneIntegrationException("Не передан телефонная книга (IPhoneBook).");
            Notifications = notifications ?? throw new PhoneIntegrationException(
                "Не передан сервис уведомлений (IPhoneNotificationService).");

            Inbox = new PhoneInbox(Messages);
            Notifier = new PhoneNotificationHub(Notifications, Apps);
        }

        /// <summary>Реестр приложений.</summary>
        public IPhoneAppRegistry Apps { get; }

        /// <summary>Сообщения и переписки.</summary>
        public IPhoneMessageService Messages { get; }

        /// <summary>Телефонная книга.</summary>
        public IPhoneBook Contacts { get; }

        /// <summary>Уведомления.</summary>
        public IPhoneNotificationService Notifications { get; }

        /// <summary>Узкий вход для внешних систем: «напиши игроку».</summary>
        public IPhoneInbox Inbox { get; }

        /// <summary>Узкий вход для внешних систем: «покажи уведомление».</summary>
        public IPhoneNotifier Notifier { get; }

        /// <summary>
        /// Сводный счётчик непрочитанных сообщений по всем перепискам.
        /// Внимание: это сообщения, а не уведомления — они считаются раздельно.
        /// </summary>
        public int UnreadMessages => Messages.TotalUnread;

        /// <summary>Счётчик непрочитанных уведомлений.</summary>
        public int UnreadNotifications => Notifications.UnreadCount;

        /// <summary>Суммарный бейдж по приложениям.</summary>
        public int UnreadApps => Apps.TotalUnread;

        /// <summary>
        /// Синхронизирует бейдж приложения «Сообщения» с числом непрочитанных сообщений.
        /// Без этого вызова бейдж не отражает доставку через <see cref="Inbox"/>:
        /// сервис сообщений не знает о реестре приложений (и не должен).
        /// Вызов идемпотентен; см. <see cref="PhoneBadgeSync"/>.
        /// </summary>
        public PhoneResult SyncMessageBadge()
        {
            return PhoneBadgeSync.SyncMessagesBadge(Apps, Messages);
        }
    }
}
