// Life From Zero — Phone messages (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Messages

using System;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Messages
{
    /// <summary>Направление сообщения относительно игрока.</summary>
    public enum PhoneMessageDirection
    {
        /// <summary>Игрок получил сообщение.</summary>
        Incoming = 0,

        /// <summary>Игрок отправил сообщение.</summary>
        Outgoing = 1
    }

    /// <summary>
    /// Сообщение переписки. Неизменяемая модель; статус прочтения меняется
    /// на уровне <see cref="PhoneConversation"/> заменой записи.
    /// </summary>
    public sealed class PhoneMessage
    {
        /// <summary>Уникальный ID сообщения (уникален в пределах всего телефона).</summary>
        public string Id { get; }

        /// <summary>ID переписки/контакта, к которой относится сообщение.</summary>
        public string ConversationId { get; }

        /// <summary>Направление: входящее или исходящее.</summary>
        public PhoneMessageDirection Direction { get; }

        /// <summary>Текст сообщения.</summary>
        public string Text { get; }

        /// <summary>Игровое время отправки.</summary>
        public GameMinutes SentAt { get; }

        /// <summary>Прочитано ли сообщение (для исходящих всегда true).</summary>
        public bool IsRead { get; }

        public PhoneMessage(
            string id,
            string conversationId,
            PhoneMessageDirection direction,
            string text,
            GameMinutes sentAt,
            bool isRead)
        {
            if (!PhoneId.IsValid(id))
            {
                throw new ArgumentException("Идентификатор сообщения не должен быть пустым.", nameof(id));
            }

            if (!PhoneId.IsValid(conversationId))
            {
                throw new ArgumentException("Идентификатор переписки не должен быть пустым.", nameof(conversationId));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Текст сообщения не должен быть пустым.", nameof(text));
            }

            Id = id;
            ConversationId = conversationId;
            Direction = direction;
            Text = text;
            SentAt = sentAt;

            // Исходящие сообщения не бывают непрочитанными по определению.
            IsRead = direction == PhoneMessageDirection.Outgoing || isRead;
        }

        /// <summary>Копия сообщения с изменённым статусом прочтения.</summary>
        public PhoneMessage AsRead()
        {
            return IsRead ? this : new PhoneMessage(Id, ConversationId, Direction, Text, SentAt, true);
        }

        public override string ToString()
        {
            return "PhoneMessage(" + Id + ", " + Direction + ", " + (IsRead ? "read" : "unread") + ")";
        }
    }
}
