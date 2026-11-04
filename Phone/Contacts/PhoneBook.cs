// Life From Zero — Phone contacts (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Contacts

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Contacts
{
    /// <summary>
    /// Телефонная книга: хранение контактов, поиск по ID и имени, управление доступностью.
    /// Реальных звонков, голосовой связи и мобильной сети модуль не реализует.
    /// </summary>
    public interface IPhoneBook
    {
        /// <summary>Добавляет контакт. Пустые поля — InvalidArgument, повтор ID — DuplicateId.</summary>
        PhoneResult Add(PhoneContact contact);

        /// <summary>Поиск по ID контакта.</summary>
        PhoneResult<PhoneContact> GetById(string contactId);

        /// <summary>
        /// Поиск по имени: точное совпадение без учёта регистра. Если совпадений несколько,
        /// возвращается первый зарегистрированный и код DuplicateId (имя не уникально).
        /// </summary>
        PhoneResult<PhoneContact> GetByName(string name);

        /// <summary>
        /// Поиск по подстроке имени без учёта регистра. Пустая строка — все контакты.
        /// </summary>
        IReadOnlyList<PhoneContact> Search(string nameQuery);

        /// <summary>Все контакты в порядке добавления.</summary>
        IReadOnlyList<PhoneContact> Contacts { get; }

        /// <summary>Меняет доступность контакта.</summary>
        PhoneResult SetAvailability(string contactId, bool isAvailable);
    }

    /// <summary>Реализация телефонной книги в памяти.</summary>
    public sealed class PhoneBook : IPhoneBook
    {
        private readonly Dictionary<string, PhoneContact> _byId =
            new Dictionary<string, PhoneContact>(StringComparer.Ordinal);

        private readonly List<PhoneContact> _order = new List<PhoneContact>();

        public IReadOnlyList<PhoneContact> Contacts => _order.AsReadOnly();

        public PhoneResult Add(PhoneContact contact)
        {
            if (contact == null)
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, null);
            }

            if (!PhoneId.IsValid(contact.Id) || string.IsNullOrWhiteSpace(contact.Name))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, contact.Id);
            }

            if (_byId.ContainsKey(contact.Id))
            {
                return PhoneResult.Fail(PhoneResultCode.DuplicateId, contact.Id);
            }

            _byId.Add(contact.Id, contact);
            _order.Add(contact);

            return PhoneResult.Ok(contact.Id);
        }

        public PhoneResult<PhoneContact> GetById(string contactId)
        {
            PhoneContact contact;

            if (!PhoneId.IsValid(contactId) || !_byId.TryGetValue(contactId, out contact))
            {
                return PhoneResult<PhoneContact>.Fail(PhoneResultCode.NotFound);
            }

            return PhoneResult<PhoneContact>.Ok(contact);
        }

        public PhoneResult<PhoneContact> GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return PhoneResult<PhoneContact>.Fail(PhoneResultCode.InvalidArgument);
            }

            int matches = 0;
            PhoneContact first = null;

            for (int i = 0; i < _order.Count; i++)
            {
                if (string.Equals(_order[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (first == null)
                    {
                        first = _order[i];
                    }

                    matches++;
                }
            }

            if (first == null)
            {
                return PhoneResult<PhoneContact>.Fail(PhoneResultCode.NotFound);
            }

            // Имя не уникально: при нескольких совпадениях отдаём первый и сообщаем о неоднозначности.
            return matches > 1
                ? PhoneResult<PhoneContact>.Fail(PhoneResultCode.DuplicateId)
                : PhoneResult<PhoneContact>.Ok(first);
        }

        public IReadOnlyList<PhoneContact> Search(string nameQuery)
        {
            var result = new List<PhoneContact>();

            if (_order.Count == 0)
            {
                return result.AsReadOnly();
            }

            string query = nameQuery ?? string.Empty;

            for (int i = 0; i < _order.Count; i++)
            {
                if (query.Length == 0
                    || _order[i].Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(_order[i]);
                }
            }

            return result.AsReadOnly();
        }

        public PhoneResult SetAvailability(string contactId, bool isAvailable)
        {
            PhoneContact contact;

            if (!PhoneId.IsValid(contactId))
            {
                return PhoneResult.Fail(PhoneResultCode.InvalidArgument, contactId);
            }

            if (!_byId.TryGetValue(contactId, out contact))
            {
                return PhoneResult.Fail(PhoneResultCode.NotFound, contactId);
            }

            PhoneContact updated = contact.WithAvailability(isAvailable);
            _byId[contactId] = updated;

            for (int i = 0; i < _order.Count; i++)
            {
                if (PhoneId.AreSame(_order[i].Id, contactId))
                {
                    _order[i] = updated;
                    break;
                }
            }

            return PhoneResult.Ok(contactId);
        }

        public PhoneBookSnapshot CreateSnapshot()
        {
            var entries = new PhoneContactSnapshot[_order.Count];
            for (int i = 0; i < entries.Length; i++)
            {
                var c = _order[i];
                entries[i] = new PhoneContactSnapshot { Id = c.Id, Name = c.Name, PhoneNumber = c.PhoneNumber, IsAvailable = c.IsAvailable };
            }
            return new PhoneBookSnapshot { Version = 1, Contacts = entries };
        }

        public bool RestoreSnapshot(PhoneBookSnapshot snapshot, out string error)
        {
            error = "Invalid contacts snapshot";
            if (snapshot == null || snapshot.Version != 1 || snapshot.Contacts == null) return false;
            var staged = new PhoneBook();
            foreach (var c in snapshot.Contacts)
            {
                if (c == null || !PhoneId.IsValid(c.Id) || string.IsNullOrWhiteSpace(c.Name)
                    || string.IsNullOrWhiteSpace(c.PhoneNumber) || staged._byId.ContainsKey(c.Id)) return false;
                staged.Add(new PhoneContact(c.Id, c.Name, c.PhoneNumber, c.IsAvailable));
            }
            _byId.Clear(); _order.Clear();
            foreach (var c in staged._order) Add(c);
            error = null; return true;
        }

        /// <summary>
        /// Стартовые контакты-заглушки не создаются: реальные контакты приходят из игры.
        /// Метод оставлен как точка расширения для конфигурации.
        /// </summary>
        public static IReadOnlyList<PhoneContact> CreateEmptySet()
        {
            return new List<PhoneContact>().AsReadOnly();
        }
    }
}
