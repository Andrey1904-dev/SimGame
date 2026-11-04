// Life From Zero — Phone notifications (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Notifications

using System;
using LifeFromZero.Phone.Apps;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Notifications
{
    /// <summary>
    /// Куда именно попадает уведомление: иконка приложения, баннер на экране
    /// блокировки, список «все уведомления».
    /// </summary>
    public enum PhoneNotificationChannel
    {
        /// <summary>Только в общий список уведомлений.</summary>
        Inbox = 0,

        /// <summary>На иконку соответствующего приложения + в список.</summary>
        AppBadge = 1,

        /// <summary>Всплывающий баннер + в список.</summary>
        Banner = 2
    }

    /// <summary>
    /// Внешний фасад для игровых систем, которым нужно «показать уведомление»:
    /// работа, банк, долги, сюжет. Телефон при этом НЕ меняет деньги, заказы
    /// и сюжетные флаги — только складывает уведомление к себе.
    /// </summary>
    public interface IPhoneNotifier
    {
        /// <summary>
        /// Создаёт уведомление и опционально увеличивает счётчик непрочитанных
        /// у соответствующего приложения.
        /// </summary>
        PhoneResult Push(
            string id,
            string source,
            PhoneNotificationType type,
            string title,
            string text,
            string appId,
            PhoneNotificationChannel channel,
            IPhoneClock clock);

        IPhoneNotificationService Notifications { get; }
    }

    /// <summary>Фасад над сервисом уведомлений и реестром приложений.</summary>
    public sealed class PhoneNotificationHub : IPhoneNotifier
    {
        private readonly IPhoneNotificationService _notifications;
        private readonly IPhoneAppRegistry _apps;

        public PhoneNotificationHub(IPhoneNotificationService notifications, IPhoneAppRegistry apps)
        {
            _notifications = notifications ?? throw new PhoneIntegrationException(
                "PhoneNotificationHub требует сервис уведомлений (IPhoneNotificationService).");

            _apps = apps;
        }

        public IPhoneNotificationService Notifications => _notifications;

        /// <summary>
        /// Увеличивает бейдж приложения, подавляя любые исключения реестра.
        /// Кастомный реестр может упасть, в том числе упаковав ошибку в
        /// <see cref="AggregateException"/> (например, при вызове через Task/UniTask):
        /// без перехвата уведомление уже создано, но вызывающий код получил бы исключение
        /// и мог бы попытаться повторить Push (двойной бейдж).
        /// </summary>
        private void FailSafeAddUnread(string appId, int delta)
        {
            try
            {
                _apps.AddUnread(appId, delta);
            }
            catch (Exception)
            {
                // Бейдж вспомогательный: состояние приложения его не теряет.
            }
        }

        public PhoneResult Push(
            string id,
            string source,
            PhoneNotificationType type,
            string title,
            string text,
            string appId,
            PhoneNotificationChannel channel,
            IPhoneClock clock)
        {
            PhoneResult added = _notifications.Add(id, source, type, title, text, clock);

            if (!added.IsSuccess)
            {
                // Повтор ID не должен второй раз накручивать счётчик приложения.
                return added;
            }

            if (channel == PhoneNotificationChannel.Inbox || _apps == null || !PhoneId.IsValid(appId))
            {
                return added;
            }

            // Счётчик приложения — вспомогательный: уведомление уже создано.
            // Любая проблема с бейджем (нет приложения, переполнение, сбой) не отменяет
            // уведомление и не превращает успех в ошибку.
            // Результат намеренно не пробрасывается: бейдж вспомогательный.
            // TODO(Codex): при необходимости логировать код ответа реестра здесь.
            FailSafeAddUnread(appId, 1);

            return added;
        }
    }
}
