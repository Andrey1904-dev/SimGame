// Life From Zero — Phone messages (ТЗ №015: восстановление из сохранения)
// Namespace: LifeFromZero.Phone.Messages
//
// Модуль не пишет на диск и не сериализует сам: он только принимает уже разобранный
// снимок и проверяет его. Так поддержка сохранения остаётся в ядре, но формат файла
// (JsonUtility, System.Text.Json, бинарный) по-прежнему выбирает Codex.
//
// Восстановление атомарно на уровне модели: либо все проверки прошли и данные заменены,
// либо не изменено ничего (включая пустой сервис). Напоминание: PhoneMessageService не
// является Singleton, поэтому «загрузка» — это создание нового сервиса с данными.

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Messages
{
    /// <summary>
    /// Восстановление переписок из <see cref="PhoneMessagesSnapshot"/> с валидацией.
    /// Ничего не удаляет из публичных контрактов: это дополнительная возможность,
    /// а не замена <see cref="IPhoneMessageService.CreateSnapshot"/>.
    /// </summary>
    public static class PhoneMessagesRestore
    {
        /// <summary>
        /// Проверяет снимок, не меняя сервис. Ошибка возвращается кодом, исключений нет.
        /// </summary>
        /// <param name="snapshot">Проверяемый снимок.</param>
        /// <param name="error">Описание первой найденной проблемы (null при успехе).</param>
        public static PhoneResult Validate(PhoneMessagesSnapshot snapshot, out string error)
        {
            error = null;

            if (snapshot == null || snapshot.Conversations == null)
            {
                error = "Снимок сообщений пуст (null).";
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, null);
            }

            var conversationIds = new HashSet<string>(StringComparer.Ordinal);
            var messageIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < snapshot.Conversations.Count; i++)
            {
                PhoneConversationSnapshot conversation = snapshot.Conversations[i];

                if (conversation == null || !PhoneId.IsValid(conversation.ConversationId))
                {
                    error = "Переписка #" + i + ": пустой идентификатор.";
                    return PhoneResult.Fail(PhoneResultCode.InvalidArgument, null);
                }

                if (!conversationIds.Add(conversation.ConversationId))
                {
                    error = "Дублирующийся идентификатор переписки: " + conversation.ConversationId;
                    return PhoneResult.Fail(PhoneResultCode.DuplicateId, conversation.ConversationId);
                }

                IReadOnlyList<PhoneMessageSnapshot> messages = conversation.Messages;

                if (messages == null)
                {
                    error = "Переписка " + conversation.ConversationId + ": список сообщений null.";
                    return PhoneResult.Fail(PhoneResultCode.InvalidArgument, conversation.ConversationId);
                }

                for (int j = 0; j < messages.Count; j++)
                {
                    PhoneMessageSnapshot message = messages[j];

                    if (message == null || !PhoneId.IsValid(message.Id))
                    {
                        error = "Переписка " + conversation.ConversationId + ": сообщение #" + j + " без ID.";
                        return PhoneResult.Fail(PhoneResultCode.InvalidArgument, conversation.ConversationId);
                    }

                    if (!messageIds.Add(message.Id))
                    {
                        error = "Дублирующийся идентификатор сообщения: " + message.Id;
                        return PhoneResult.Fail(PhoneResultCode.DuplicateId, message.Id);
                    }

                    if (!PhoneId.AreSame(message.ConversationId, conversation.ConversationId))
                    {
                        error = "Сообщение " + message.Id + " принадлежит другой переписке: " + message.ConversationId;
                        return PhoneResult.Fail(PhoneResultCode.InvalidArgument, message.Id);
                    }

                    if (string.IsNullOrWhiteSpace(message.Text))
                    {
                        error = "Сообщение " + message.Id + ": пустой текст.";
                        return PhoneResult.Fail(PhoneResultCode.InvalidArgument, message.Id);
                    }

                    // Исходящее сообщение не может быть непрочитанным (инвариант PhoneMessage),
                    // поэтому такой снимок считается испорченным, а не «исправляется» молча.
                    if (message.Direction == PhoneMessageDirection.Outgoing && !message.IsRead)
                    {
                        error = "Сообщение " + message.Id + ": исходящее помечено непрочитанным.";
                        return PhoneResult.Fail(PhoneResultCode.InvalidState, message.Id);
                    }

                    if ((int)message.Direction != (int)PhoneMessageDirection.Incoming
                        && (int)message.Direction != (int)PhoneMessageDirection.Outgoing)
                    {
                        error = "Сообщение " + message.Id + ": неизвестное направление.";
                        return PhoneResult.Fail(PhoneResultCode.InvalidArgument, message.Id);
                    }
                }
            }

            return PhoneResult.Ok(null);
        }

        /// <summary>
        /// Заменяет содержимое сервиса данными снимка. Если снимок невалиден, сервис
        /// остаётся нетронутым (никаких частично загруженных переписок).
        /// </summary>
        public static PhoneResult RestoreInto(PhoneMessageService target, PhoneMessagesSnapshot snapshot, out string error)
        {
            if (target == null)
            {
                throw new PhoneIntegrationException(
                    "PhoneMessagesRestore требует сервис сообщений (PhoneMessageService).");
            }

            PhoneResult validation = Validate(snapshot, out error);

            if (!validation.IsSuccess)
            {
                return validation;
            }

            var conversations = new List<PhoneConversation>(snapshot.Conversations.Count);

            for (int i = 0; i < snapshot.Conversations.Count; i++)
            {
                PhoneConversationSnapshot source = snapshot.Conversations[i];
                var conversation = new PhoneConversation(source.ConversationId);

                for (int j = 0; j < source.Messages.Count; j++)
                {
                    PhoneMessageSnapshot message = source.Messages[j];

                    // Конструктор PhoneMessage ещё раз проверяет инварианты (текст, ID,
                    // «исходящее всегда прочитано»). Исключение здесь возможно только при
                    // расхождении с Validate, поэтому оно превращается в код, а не в throw.
                    try
                    {
                        conversation.Append(new PhoneMessage(
                            message.Id,
                            message.ConversationId,
                            message.Direction,
                            message.Text,
                            message.SentAt,
                            message.IsRead));
                    }
                    catch (Exception exception)
                    {
                        error = "Сообщение " + message.Id + ": " + exception.Message;
                        return PhoneResult.Fail(PhoneResultCode.InvalidArgument, message.Id);
                    }
                }

                conversations.Add(conversation);
            }

            target.ReplaceAll(conversations);
            return PhoneResult.Ok(null);
        }
    }
}
