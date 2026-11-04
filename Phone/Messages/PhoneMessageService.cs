// Life From Zero — Phone messages (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Messages

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Messages
{
    /// <summary>
    /// Сервис сообщений: переписки с контактами, входящие/исходящие, история, прочтение,
    /// счётчики непрочитанных. Хранит данные в памяти, Singleton-ом не является.
    /// </summary>
    /// <remarks>
    /// Телефон не отправляет сообщения «в мир»: он только хранит переписку.
    /// Внешние системы (NPC, сюжет, работа) приходят через <see cref="PhoneInbox"/> или
    /// <see cref="ReceiveFromContact"/> и не имеют доступа к деньгам, заказам и флагам.
    /// </remarks>
    public interface IPhoneMessageService
    {
        /// <summary>
        /// Создаёт переписку с контактом. Пустой ID — InvalidArgument,
        /// повтор — DuplicateId (существующая переписка не сбрасывается).
        /// </summary>
        PhoneResult StartConversation(string contactId);

        /// <summary>Все переписки в порядке создания.</summary>
        IReadOnlyList<IPhoneConversation> Conversations { get; }

        /// <summary>Переписка по ID (ID контакта).</summary>
        PhoneResult<IPhoneConversation> GetConversation(string contactId);

        /// <summary>
        /// Добавляет входящее сообщение. Если переписки ещё нет, она создаётся автоматически.
        /// </summary>
        PhoneResult<PhoneMessage> ReceiveFromContact(string contactId, string text, IPhoneClock clock);

        /// <summary>Добавляет исходящее сообщение от игрока. Без существующей переписки — NotFound.</summary>
        PhoneResult<PhoneMessage> SendFromPlayer(string contactId, string text, IPhoneClock clock);

        /// <summary>История переписки. Для неизвестного контакта — NotFound и пустая история.</summary>
        PhoneResult<IReadOnlyList<PhoneMessage>> GetHistory(string contactId);

        /// <summary>Помечает сообщение прочитанным. Повторное прочтение — Success (идемпотентно).</summary>
        PhoneResult MarkRead(string contactId, string messageId);

        /// <summary>Помечает прочитанными все входящие переписки.</summary>
        PhoneResult MarkAllRead(string contactId);

        /// <summary>Количество непрочитанных по всем перепискам (с защитой от переполнения).</summary>
        int TotalUnread { get; }

        /// <summary>Снимок всех сообщений для будущего сохранения игры.</summary>
        PhoneMessagesSnapshot CreateSnapshot();
    }

    /// <summary>Реализация сервиса сообщений в памяти.</summary>
    public sealed class PhoneMessageService : IPhoneMessageService
    {
        private readonly Dictionary<string, PhoneConversation> _conversations =
            new Dictionary<string, PhoneConversation>(StringComparer.Ordinal);

        private readonly List<PhoneConversation> _order = new List<PhoneConversation>();

        private readonly HashSet<string> _messageIds = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<IPhoneConversation> Conversations
        {
            get
            {
                var list = new List<IPhoneConversation>(_order.Count);

                for (int i = 0; i < _order.Count; i++)
                {
                    list.Add(_order[i]);
                }

                return list.AsReadOnly();
            }
        }

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

        public PhoneResult StartConversation(string contactId)
        {
            if (!PhoneId.IsValid(contactId))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, contactId);
            }

            if (_conversations.ContainsKey(contactId))
            {
                return PhoneResult.Fail(PhoneResultCode.DuplicateId, contactId);
            }

            AddConversation(contactId);
            return PhoneResult.Ok(contactId);
        }

        public PhoneResult<IPhoneConversation> GetConversation(string contactId)
        {
            PhoneConversation conversation;

            if (!PhoneId.IsValid(contactId) || !_conversations.TryGetValue(contactId, out conversation))
            {
                return PhoneResult<IPhoneConversation>.Fail(PhoneResultCode.NotFound);
            }

            return PhoneResult<IPhoneConversation>.Ok(conversation);
        }

        public PhoneResult<PhoneMessage> ReceiveFromContact(string contactId, string text, IPhoneClock clock)
        {
            return Append(contactId, text, PhoneMessageDirection.Incoming, clock, true);
        }

        public PhoneResult<PhoneMessage> SendFromPlayer(string contactId, string text, IPhoneClock clock)
        {
            return Append(contactId, text, PhoneMessageDirection.Outgoing, clock, false);
        }

        public PhoneResult<IReadOnlyList<PhoneMessage>> GetHistory(string contactId)
        {
            PhoneConversation conversation;

            if (!PhoneId.IsValid(contactId) || !_conversations.TryGetValue(contactId, out conversation))
            {
                return PhoneResult<IReadOnlyList<PhoneMessage>>.Fail(PhoneResultCode.NotFound);
            }

            // Пустая история — это успех с пустым списком, а не ошибка.
            return PhoneResult<IReadOnlyList<PhoneMessage>>.Ok(conversation.Messages);
        }

        public PhoneResult MarkRead(string contactId, string messageId)
        {
            PhoneConversation conversation;

            if (!PhoneId.IsValid(contactId) || !PhoneId.IsValid(messageId))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, messageId);
            }

            if (!_conversations.TryGetValue(contactId, out conversation))
            {
                return PhoneResult.Fail(PhoneResultCode.NotFound, contactId);
            }

            // Повторное прочтение не является ошибкой: операция идемпотентна.
            return conversation.MarkRead(messageId)
                ? PhoneResult.Ok(messageId)
                : PhoneResult.Fail(PhoneResultCode.NotFound, messageId);
        }

        public PhoneResult MarkAllRead(string contactId)
        {
            PhoneConversation conversation;

            if (!PhoneId.IsValid(contactId))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, contactId);
            }

            if (!_conversations.TryGetValue(contactId, out conversation))
            {
                return PhoneResult.Fail(PhoneResultCode.NotFound, contactId);
            }

            conversation.MarkAllRead();
            return PhoneResult.Ok(contactId);
        }

        public PhoneMessagesSnapshot CreateSnapshot()
        {
            var entries = new List<PhoneConversationSnapshot>(_order.Count);

            for (int i = 0; i < _order.Count; i++)
            {
                PhoneConversation source = _order[i];
                var messages = new List<PhoneMessageSnapshot>(source.Messages.Count);

                for (int j = 0; j < source.Messages.Count; j++)
                {
                    PhoneMessage message = source.Messages[j];
                    messages.Add(new PhoneMessageSnapshot(
                        message.Id,
                        message.ConversationId,
                        message.Direction,
                        message.Text,
                        message.SentAt,
                        message.IsRead));
                }

                entries.Add(new PhoneConversationSnapshot(source.ConversationId, messages));
            }

            return new PhoneMessagesSnapshot(entries);
        }

        private PhoneResult<PhoneMessage> Append(
            string contactId,
            string text,
            PhoneMessageDirection direction,
            IPhoneClock clock,
            bool createIfMissing)
        {
            if (!PhoneId.IsValid(contactId))
            {
                return PhoneResult<PhoneMessage>.Fail(PhoneResultCode.InvalidArgument);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return PhoneResult<PhoneMessage>.Fail(PhoneResultCode.InvalidArgument);
            }

            PhoneConversation conversation;

            if (!_conversations.TryGetValue(contactId, out conversation))
            {
                if (!createIfMissing)
                {
                    // Игрок не может писать в переписку, которой нет.
                    return PhoneResult<PhoneMessage>.Fail(PhoneResultCode.NotFound);
                }

                conversation = AddConversation(contactId);
            }

            // Время читаем один раз и ровно здесь: сбой адаптера не должен терять сообщение.
            GameMinutes sentAt = PhoneClockReader.Read(clock);
            string messageId = BuildMessageId(conversation, sentAt);

            var message = new PhoneMessage(
                messageId,
                conversation.ConversationId,
                direction,
                text,
                sentAt,
                false);

            _messageIds.Add(messageId);
            conversation.Append(message);

            return PhoneResult<PhoneMessage>.Ok(message);
        }

        /// <summary>
        /// Детерминированный ID сообщения: контакт + номер в переписке.
        /// Уникальность гарантируется тем, что внутри переписки номера не повторяются,
        /// а ID контакта входит в строку.
        /// </summary>
        private string BuildMessageId(PhoneConversation conversation, GameMinutes sentAt)
        {
            int index = conversation.Messages.Count + 1;
            string id = conversation.ConversationId + "#" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

            while (_messageIds.Contains(id))
            {
                index++;
                id = conversation.ConversationId + "#" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return id;
        }

        private PhoneConversation AddConversation(string contactId)
        {
            var conversation = new PhoneConversation(contactId);
            _conversations.Add(contactId, conversation);
            _order.Add(conversation);
            return conversation;
        }

        /// <summary>
        /// Полностью заменяет содержимое сервиса готовыми переписками.
        /// Вызывается только <see cref="PhoneMessagesRestore"/> после собственной проверки
        /// снимка. Намеренно <c>internal</c>: внешний код не может подсунуть сюда
        /// несогласованный набор и обойти дедупликацию ID.
        /// </summary>
        internal void ReplaceAll(IReadOnlyList<PhoneConversation> conversations)
        {
            if (conversations == null)
            {
                throw new ArgumentNullException(nameof(conversations));
            }

            _conversations.Clear();
            _order.Clear();
            _messageIds.Clear();

            for (int i = 0; i < conversations.Count; i++)
            {
                PhoneConversation conversation = conversations[i];

                if (conversation == null || _conversations.ContainsKey(conversation.ConversationId))
                {
                    throw new PhoneIntegrationException(
                        "Повреждённый набор переписок при восстановлении: " + (conversation == null ? "<null>" : conversation.ConversationId));
                }

                _conversations.Add(conversation.ConversationId, conversation);
                _order.Add(conversation);

                for (int j = 0; j < conversation.Messages.Count; j++)
                {
                    // Индекс ID восстанавливается вместе с данными: иначе новая переписка
                    // получила бы ID, уже занятый загруженным сообщением.
                    _messageIds.Add(conversation.Messages[j].Id);
                }
            }
        }
    }
}
