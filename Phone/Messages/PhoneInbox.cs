// Life From Zero — Phone messages (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Messages

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
    public sealed class PhoneInbox : IPhoneInbox
    {
        private readonly IPhoneMessageService _messages;

        public PhoneInbox(IPhoneMessageService messages)
        {
            _messages = messages ?? throw new PhoneIntegrationException(
                "PhoneInbox требует сервис сообщений (IPhoneMessageService).");
        }

        public PhoneResult<PhoneMessage> Deliver(string contactId, string text, IPhoneClock clock)
        {
            return _messages.ReceiveFromContact(contactId, text, clock);
        }
    }
}
