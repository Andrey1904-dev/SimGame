// Life From Zero — Phone notifications (ТЗ №015: восстановление из сохранения)
// Namespace: LifeFromZero.Phone.Notifications
//
// Симметрично PhoneMessagesRestore: ядро принимает разобранный снимок, проверяет его
// и заменяет состояние атомарно. Формат файла остаётся задачей Codex.

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Notifications
{
    /// <summary>
    /// Восстановление уведомлений из <see cref="PhoneNotificationsSnapshot"/> с валидацией.
    /// </summary>
    public static class PhoneNotificationsRestore
    {
        /// <summary>
        /// Проверяет снимок, не меняя сервис.
        /// </summary>
        /// <param name="snapshot">Проверяемый снимок.</param>
        /// <param name="error">Описание первой найденной проблемы (null при успехе).</param>
        public static PhoneResult Validate(PhoneNotificationsSnapshot snapshot, out string error)
        {
            error = null;

            if (snapshot == null || snapshot.Notifications == null)
            {
                error = "Снимок уведомлений пуст (null).";
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, null);
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < snapshot.Notifications.Count; i++)
            {
                PhoneNotificationSnapshot notification = snapshot.Notifications[i];

                if (notification == null || !PhoneId.IsValid(notification.Id))
                {
                    error = "Уведомление #" + i + ": пустой идентификатор.";
                    return PhoneResult.Fail(PhoneResultCode.InvalidArgument, null);
                }

                if (!ids.Add(notification.Id))
                {
                    error = "Дублирующийся идентификатор уведомления: " + notification.Id;
                    return PhoneResult.Fail(PhoneResultCode.DuplicateId, notification.Id);
                }

                if (!PhoneId.IsValid(notification.Source))
                {
                    error = "Уведомление " + notification.Id + ": пустой источник.";
                    return PhoneResult.Fail(PhoneResultCode.InvalidArgument, notification.Id);
                }

                if (string.IsNullOrWhiteSpace(notification.Title) || string.IsNullOrWhiteSpace(notification.Text))
                {
                    error = "Уведомление " + notification.Id + ": пустой заголовок или текст.";
                    return PhoneResult.Fail(PhoneResultCode.InvalidArgument, notification.Id);
                }

                if (!IsKnownType(notification.Type))
                {
                    error = "Уведомление " + notification.Id + ": неизвестный тип " + (int)notification.Type + ".";
                    return PhoneResult.Fail(PhoneResultCode.InvalidArgument, notification.Id);
                }
            }

            return PhoneResult.Ok(null);
        }

        /// <summary>
        /// Заменяет содержимое сервиса данными снимка. Невалидный снимок ничего не меняет.
        /// </summary>
        public static PhoneResult RestoreInto(PhoneNotificationService target, PhoneNotificationsSnapshot snapshot, out string error)
        {
            if (target == null)
            {
                throw new PhoneIntegrationException(
                    "PhoneNotificationsRestore требует сервис уведомлений (PhoneNotificationService).");
            }

            PhoneResult validation = Validate(snapshot, out error);

            if (!validation.IsSuccess)
            {
                return validation;
            }

            var restored = new List<PhoneNotification>(snapshot.Notifications.Count);

            for (int i = 0; i < snapshot.Notifications.Count; i++)
            {
                PhoneNotificationSnapshot source = snapshot.Notifications[i];

                try
                {
                    restored.Add(new PhoneNotification(
                        source.Id,
                        source.Source,
                        source.Type,
                        source.Title,
                        source.Text,
                        source.CreatedAt,
                        source.IsRead));
                }
                catch (Exception exception)
                {
                    // Конструктор ещё раз проверяет инварианты; расхождение с Validate
                    // не должно вылетать наружу.
                    error = "Уведомление " + source.Id + ": " + exception.Message;
                    return PhoneResult.Fail(PhoneResultCode.InvalidArgument, source.Id);
                }
            }

            target.ReplaceAll(restored);
            return PhoneResult.Ok(null);
        }

        /// <summary>
        /// Известен ли тип уведомления. Проверка нужна, потому что enum из файла сохранения
        /// может содержать любое число: без неё UI получил бы «уведомление типа 42».
        /// </summary>
        private static bool IsKnownType(PhoneNotificationType type)
        {
            switch (type)
            {
                case PhoneNotificationType.Message:
                case PhoneNotificationType.Job:
                case PhoneNotificationType.Bank:
                case PhoneNotificationType.Debt:
                case PhoneNotificationType.Story:
                case PhoneNotificationType.System:
                    return true;
                default:
                    return false;
            }
        }
    }
}
