// Life From Zero — Phone messages (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Messages

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Messages
{
    /// <summary>
    /// Переписка с одним контактом. Хранится в памяти, снимок для будущего сохранения
    /// игры возвращает <see cref="PhoneMessageService.CreateSnapshot"/>.
    /// </summary>
    public interface IPhoneConversation : IUnreadCounter
    {
        /// <summary>ID переписки (совпадает с ID контакта).</summary>
        string ConversationId { get; }

        /// <summary>История сообщений в порядке добавления.</summary>
        IReadOnlyList<PhoneMessage> Messages { get; }
    }

    /// <summary>Реализация переписки в памяти.</summary>
    public sealed class PhoneConversation : IPhoneConversation
    {
        private readonly List<PhoneMessage> _messages = new List<PhoneMessage>();

        public PhoneConversation(string conversationId)
        {
            if (!PhoneId.IsValid(conversationId))
            {
                throw new ArgumentException("Идентификатор переписки не должен быть пустым.", nameof(conversationId));
            }

            ConversationId = conversationId;
        }

        public string ConversationId { get; }

        public IReadOnlyList<PhoneMessage> Messages => _messages.AsReadOnly();

        public int UnreadCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _messages.Count; i++)
                {
                    if (!_messages[i].IsRead)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Добавляет сообщение в конец истории.</summary>
        public void Append(PhoneMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!PhoneId.AreSame(message.ConversationId, ConversationId))
            {
                throw new ArgumentException(
                    "Сообщение принадлежит другой переписке: " + message.ConversationId, nameof(message));
            }

            _messages.Add(message);
        }

        /// <summary>Помечает одно сообщение прочитанным. false, если сообщения нет.</summary>
        public bool MarkRead(string messageId)
        {
            if (!PhoneId.IsValid(messageId))
            {
                return false;
            }

            for (int i = 0; i < _messages.Count; i++)
            {
                if (PhoneId.AreSame(_messages[i].Id, messageId))
                {
                    _messages[i] = _messages[i].AsRead();
                    return true;
                }
            }

            return false;
        }

        /// <summary>Помечает прочитанными все входящие.</summary>
        public void MarkAllRead()
        {
            for (int i = 0; i < _messages.Count; i++)
            {
                if (!_messages[i].IsRead)
                {
                    _messages[i] = _messages[i].AsRead();
                }
            }
        }
    }
}
