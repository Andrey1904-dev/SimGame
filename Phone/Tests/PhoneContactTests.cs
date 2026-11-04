// Life From Zero — Phone tests (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Tests

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Contacts;
using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Tests
{
    /// <summary>Тесты телефонной книги.</summary>
    public static class PhoneContactTests
    {
        public static void Register(TestSuite suite)
        {
            suite.Add("Контакты: добавление контакта", AddContact);
            suite.Add("Контакты: дубликат ID отклоняется", DuplicateContact);
            suite.Add("Контакты: некорректные данные контакта", InvalidContact);
            suite.Add("Контакты: поиск по ID", FindById);
            suite.Add("Контакты: поиск по имени без учёта регистра", FindByName);
            suite.Add("Контакты: неоднозначное имя", AmbiguousName);
            suite.Add("Контакты: поиск по подстроке", SearchSubstring);
            suite.Add("Контакты: пустая книга и пустой поиск", EmptyBook);
            suite.Add("Контакты: неизвестный контакт", UnknownContact);
            suite.Add("Контакты: доступность контакта", ContactAvailability);
            suite.Add("Контакты: пустое имя при поиске", EmptySearchName);
        }

        private static PhoneBook NewBook()
        {
            var book = new PhoneBook();
            book.Add(new PhoneContact("anna", "Анна", "+7-900-000-00-01", true));
            book.Add(new PhoneContact("boris", "Борис", "+7-900-000-00-02", true));
            book.Add(new PhoneContact("anna2", "Анна", "+7-900-000-00-03", false));
            book.Add(new PhoneContact("vera", "Вера Петровна", "+7-900-000-00-04", true));
            return book;
        }

        private static void AddContact()
        {
            var book = new PhoneBook();

            MiniTestRunner.IsTrue(
                book.Add(new PhoneContact("anna", "Анна", "+7-900-000-00-01", true)).IsSuccess,
                "контакт добавлен");
            MiniTestRunner.AreEqual(1, book.Contacts.Count, "в книге один контакт");
            MiniTestRunner.AreEqual("Анна", book.Contacts[0].Name, "имя сохранено");
        }

        private static void DuplicateContact()
        {
            var book = new PhoneBook();
            var contact = new PhoneContact("anna", "Анна", "+7", true);

            book.Add(contact);
            PhoneResult duplicate = book.Add(new PhoneContact("anna", "Другая Анна", "+8", true));

            MiniTestRunner.AreEqual(PhoneResultCode.DuplicateId, duplicate.Code, "дубликат ID отклонён");
            MiniTestRunner.AreEqual("Анна", book.Contacts[0].Name, "существующий контакт не заменён");
            MiniTestRunner.AreEqual(1, book.Contacts.Count, "количество контактов не изменилось");
        }

        private static void InvalidContact()
        {
            var book = new PhoneBook();

            MiniTestRunner.AreEqual(PhoneResultCode.InvalidArgument, book.Add(null).Code, "null-контакт");

            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<ArgumentException>(() => new PhoneContact("", "Анна", "+7", true)),
                "пустой ID контакта");
            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<ArgumentException>(() => new PhoneContact("anna", " ", "+7", true)),
                "пустое имя");
            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<ArgumentException>(() => new PhoneContact("anna", "Анна", "", true)),
                "пустой номер");
        }

        private static void FindById()
        {
            PhoneBook book = NewBook();

            PhoneResult<PhoneContact> found = book.GetById("boris");

            MiniTestRunner.IsTrue(found.IsSuccess, "контакт найден");
            MiniTestRunner.AreEqual("Борис", found.Value.Name, "верный контакт");
            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, book.GetById("nobody").Code, "неизвестный ID");
            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, book.GetById(null).Code, "null-ID");
        }

        private static void FindByName()
        {
            var book = new PhoneBook();
            book.Add(new PhoneContact("vera", "Вера Петровна", "+7", true));

            PhoneResult<PhoneContact> found = book.GetByName("вера петровна");

            MiniTestRunner.IsTrue(found.IsSuccess, "имя найдено без учёта регистра");
            MiniTestRunner.AreEqual("vera", found.Value.Id, "верный контакт");
        }

        private static void AmbiguousName()
        {
            PhoneBook book = NewBook();

            PhoneResult<PhoneContact> found = book.GetByName("Анна");

            MiniTestRunner.AreEqual(PhoneResultCode.DuplicateId, found.Code, "неоднозначное имя помечено");
        }

        private static void SearchSubstring()
        {
            PhoneBook book = NewBook();

            IReadOnlyList<PhoneContact> annas = book.Search("анн");
            MiniTestRunner.AreEqual(2, annas.Count, "две Анны по подстроке");

            IReadOnlyList<PhoneContact> all = book.Search(null);
            MiniTestRunner.AreEqual(4, all.Count, "пустой запрос возвращает всех");

            IReadOnlyList<PhoneContact> none = book.Search("Карл");
            MiniTestRunner.AreEqual(0, none.Count, "по несуществующему имени никого");
        }

        private static void EmptyBook()
        {
            var book = new PhoneBook();

            MiniTestRunner.AreEqual(0, book.Contacts.Count, "книга пуста");
            MiniTestRunner.AreEqual(0, book.Search("Анна").Count, "поиск в пустой книге");
            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, book.GetByName("Анна").Code, "поиск имени в пустой книге");
        }

        private static void UnknownContact()
        {
            PhoneBook book = NewBook();

            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, book.GetById("nobody").Code, "неизвестный ID");
            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, book.SetAvailability("nobody", false).Code, "доступность неизвестного");
            MiniTestRunner.AreEqual(PhoneResultCode.InvalidArgument, book.SetAvailability(" ", false).Code, "пустой ID");
        }

        private static void ContactAvailability()
        {
            PhoneBook book = NewBook();

            MiniTestRunner.IsTrue(book.SetAvailability("boris", false).IsSuccess, "контакт скрыт");
            MiniTestRunner.IsFalse(book.GetById("boris").Value.IsAvailable, "контакт недоступен");

            MiniTestRunner.IsTrue(book.SetAvailability("boris", true).IsSuccess, "контакт снова доступен");
            MiniTestRunner.IsTrue(book.GetById("boris").Value.IsAvailable, "контакт доступен");
            MiniTestRunner.AreEqual(4, book.Contacts.Count, "количество контактов не изменилось");
        }

        private static void EmptySearchName()
        {
            PhoneBook book = NewBook();

            MiniTestRunner.AreEqual(PhoneResultCode.InvalidArgument, book.GetByName("").Code, "пустое имя");
            MiniTestRunner.AreEqual(PhoneResultCode.InvalidArgument, book.GetByName("  ").Code, "пробельное имя");
        }
    }
}
