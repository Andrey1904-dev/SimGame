// Life From Zero — Phone messages (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Messages
//
// Точка входа для внешних систем (NPC, сюжет, работа), которые «пишут» игроку.
// Узкий интерфейс: только приём сообщений, без доступа к истории, прочтению
// и каким-либо игровым системам.

using System;
using LifeFromZero.Phone.Apps;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Messages
{
    /// <summary>
    /// Точка входа для внешних систем (NPC, сюжет, работа), которые «пишут» игроку.
    /// Узкий интерфейс: только приём сообщений, без доступа к истории, прочтению
    /// и каким-либо игровым системам.
    /// </summary>
    public interface IPhoneInbox
    {
        /// <summary>
        /// Доставляет входящее сообщение. Переписка создаётся автоматически.
        /// </summary>
        PhoneResult<PhoneMessage> Deliver(string contactId, string text, IPhoneClock clock);
    }

    /// <summary>
    /// Адаптер: внешние системы зависят только от <see cref="IPhoneInbox"/>,
    /// а не от всего сервиса сообщений.
    /// </summary>
    /// <remarks>
    /// Бейдж приложения «Сообщения» — вспомогательный счётчик. Он увеличивается
    /// при успешной доставке входящего сообщения, чтобы иконка приложения
    /// отражала новое сообщение. Сбой реестра приложений не отменяет доставку:
    /// сообщение уже сохранено, а бейдж можно пересчитать при открытии приложения.
    /// </remarks>
    public sealed class PhoneInbox : IPhoneInbox
    {
        private readonly IPhoneMessageService _messages;
        private readonly IPhoneAppRegistry _apps;

        public PhoneInbox(IPhoneMessageService messages)
            : this(messages, null)
        {
        }

        public PhoneInbox(IPhoneMessageService messages, IPhoneAppRegistry apps)
        {
            _messages = messages ?? throw new PhoneIntegrationException(
                "PhoneInbox требует сервис сообщений (IPhoneMessageService).");
            _apps = apps;
        }

        public PhoneResult<PhoneMessage> Deliver(string contactId, string text, IPhoneClock clock)
        {
            PhoneResult<PhoneMessage> result = _messages.ReceiveFromContact(contactId, text, clock);

            if (result.IsSuccess && _apps != null)
            {
                // Бейдж вспомогательный: сообщение уже доставлено, сбой реестра
                // не должен превращать успешную доставку в ошибку.
                try
                {
                    _apps.AddUnread(PhoneAppIds.Messages, 1);
                }
                catch (Exception)
                {
                    // Диагностику ведёт реестр/адаптер; доставка важнее бейджа.
                }
            }

            return result;
        }
    }
}
