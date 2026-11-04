// Life From Zero — Phone messages (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Messages
//
// Снимки (DTO) для будущего сохранения игры.
// Это простые данные без поведения: сериализуются любым удобным способом
// (JsonUtility, System.Text.Json, собственный формат) на стороне Codex.

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Messages
{
    /// <summary>Снимок одного сообщения.</summary>
    public sealed class PhoneMessageSnapshot
    {
        public string Id { get; }

        public string ConversationId { get; }

        public PhoneMessageDirection Direction { get; }

        public string Text { get; }

        /// <summary>Игровое время отправки; <see cref="GameMinutes.Unknown"/>, если время было недоступно.</summary>
        public GameMinutes SentAt { get; }

        public bool IsRead { get; }

        public PhoneMessageSnapshot(
            string id,
            string conversationId,
            PhoneMessageDirection direction,
            string text,
            GameMinutes sentAt,
            bool isRead)
        {
            Id = id;
            ConversationId = conversationId;
            Direction = direction;
            Text = text;
            SentAt = sentAt;
            IsRead = isRead;
        }
    }

    /// <summary>Снимок переписки с историей сообщений.</summary>
    public sealed class PhoneConversationSnapshot
    {
        public string ConversationId { get; }

        public IReadOnlyList<PhoneMessageSnapshot> Messages { get; }

        public PhoneConversationSnapshot(string conversationId, IReadOnlyList<PhoneMessageSnapshot> messages)
        {
            ConversationId = conversationId;

            // Защитная копия: снимок не должен меняться, если вызывающий код продолжит
            // редактировать свой список (иначе сохранение «поедет» после записи на диск).
            Messages = messages == null
                ? (IReadOnlyList<PhoneMessageSnapshot>)new List<PhoneMessageSnapshot>().AsReadOnly()
                : new List<PhoneMessageSnapshot>(messages).AsReadOnly();
        }
    }

    /// <summary>Снимок всех переписок телефона.</summary>
    public sealed class PhoneMessagesSnapshot
    {
        public IReadOnlyList<PhoneConversationSnapshot> Conversations { get; }

        public PhoneMessagesSnapshot(IReadOnlyList<PhoneConversationSnapshot> conversations)
        {
            Conversations = conversations == null
                ? (IReadOnlyList<PhoneConversationSnapshot>)new List<PhoneConversationSnapshot>().AsReadOnly()
                : new List<PhoneConversationSnapshot>(conversations).AsReadOnly();
        }
    }
}
