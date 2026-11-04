// Life From Zero — Phone contacts (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Contacts

using System;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Contacts
{
    /// <summary>
    /// Контакт телефонной книги. Неизменяемая модель.
    /// Номер телефона — условный идентификатор, реальных звонков модуль не делает.
    /// </summary>
    public sealed class PhoneContact
    {
        /// <summary>Уникальный ID контакта (используется и как ID переписки).</summary>
        public string Id { get; }

        /// <summary>Имя контакта.</summary>
        public string Name { get; }

        /// <summary>Условный номер телефона.</summary>
        public string PhoneNumber { get; }

        /// <summary>Доступен ли контакт игроку.</summary>
        public bool IsAvailable { get; }

        public PhoneContact(string id, string name, string phoneNumber, bool isAvailable)
        {
            if (!PhoneId.IsValid(id))
            {
                throw new ArgumentException("Идентификатор контакта не должен быть пустым.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Имя контакта не должно быть пустым.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                throw new ArgumentException("Номер телефона не должен быть пустым.", nameof(phoneNumber));
            }

            Id = id;
            Name = name;
            PhoneNumber = phoneNumber;
            IsAvailable = isAvailable;
        }

        /// <summary>Копия контакта с другой доступностью.</summary>
        public PhoneContact WithAvailability(bool isAvailable)
        {
            return new PhoneContact(Id, Name, PhoneNumber, isAvailable);
        }

        public override string ToString()
        {
            return "PhoneContact(" + Id + ", " + Name + ")";
        }
    }
}
