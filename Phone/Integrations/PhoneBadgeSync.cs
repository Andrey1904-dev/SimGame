// Life From Zero — Phone integrations (ТЗ №017: счётчик приложения «Сообщения»)
// Namespace: LifeFromZero.Phone.Integrations
//
// НАЙДЕННЫЙ РИСК (подтверждён). Доставка сообщения через IPhoneInbox увеличивала
// только счётчик непрочитанных переписки (IPhoneMessageService.TotalUnread).
// Бейдж приложения «messages» (PhoneAppRegistry) при этом не менялся: у сервиса
// сообщений нет и не должно быть ссылки на реестр приложений, а Push уведомления
// работает с другим счётчиком.
//
// ПОЧЕМУ НЕ АВТОИНКРЕМЕНТ. Если бы Inbox увеличивал бейдж на +1, то доставка
// сообщения и последующий Push с каналом AppBadge дали бы +2 за одно событие.
// Поэтому бейдж «Сообщения» не инкрементируется, а СИНХРОНИЗИРУЕТСЯ с фактическим
// числом непрочитанных сообщений: операция идемпотентна, повторный вызов ничего
// не ломает и не может «накрутить» счётчик.
//
// КОГДА ВЫЗЫВАТЬ: после доставки сообщения (или при открытии экрана телефона).
// Экран, который показывает бейдж, обязан либо звать SyncMessagesBadge, либо
// показывать IPhoneMessageService.TotalUnread напрямую.

using LifeFromZero.Phone.Apps;
using LifeFromZero.Phone.Core;
using LifeFromZero.Phone.Messages;

namespace LifeFromZero.Phone.Integrations
{
    /// <summary>Синхронизация бейджа приложения «Сообщения» с сервисом сообщений.</summary>
    public static class PhoneBadgeSync
    {
        /// <summary>ID стандартного приложения сообщений.</summary>
        public const string MessagesAppId = "messages";

        /// <summary>
        /// Приводит бейдж приложения «Сообщения» к числу непрочитанных сообщений.
        /// </summary>
        /// <returns>
        /// <c>Success</c> — бейдж синхронизирован; <c>NotFound</c> — приложение не
        /// зарегистрировано; <c>InvalidArgument</c> — не переданы зависимости;
        /// <c>CounterOverflow</c> — счётчик не помещается (значение не изменено частично:
        /// сначала сброс, затем точная установка).
        /// </returns>
        public static PhoneResult SyncMessagesBadge(IPhoneAppRegistry apps, IPhoneMessageService messages)
        {
            if (apps == null || messages == null)
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, null);
            }

            PhoneResult<PhoneApp> app = apps.Get(MessagesAppId);

            if (!app.IsSuccess)
            {
                // Телефон может работать без этого приложения (кастомный набор) —
                // это не ошибка интеграции, а отсутствие бейджа.
                return PhoneResult.Fail(PhoneResultCode.NotFound, MessagesAppId);
            }

            int unread = messages.TotalUnread;

            // Сброс + установка дают точное значение, а не «накопление».
            PhoneResult cleared = apps.ClearUnread(MessagesAppId);

            if (!cleared.IsSuccess)
            {
                return cleared;
            }

            if (unread == 0)
            {
                return PhoneResult.Ok(MessagesAppId);
            }

            return apps.AddUnread(MessagesAppId, unread);
        }
    }
}
