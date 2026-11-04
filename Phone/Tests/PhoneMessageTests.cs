// Life From Zero — Phone tests (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Tests

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Core;
using LifeFromZero.Phone.Messages;
using LifeFromZero.Phone.Tests.Fakes;

namespace LifeFromZero.Phone.Tests
{
    /// <summary>Тесты сообщений и переписок.</summary>
    public static class PhoneMessageTests
    {
        private const string Anna = "anna";
        private const string Boris = "boris";

        public static void Register(TestSuite suite)
        {
            suite.Add("Сообщения: создание переписки", StartConversation);
            suite.Add("Сообщения: дубликат переписки отклоняется", DuplicateConversation);
            suite.Add("Сообщения: пустой ID контакта", InvalidConversationId);
            suite.Add("Сообщения: отправка сообщения игроком", SendMessage);
            suite.Add("Сообщения: отправка без переписки отклоняется", SendWithoutConversation);
            suite.Add("Сообщения: получение сообщения создаёт переписку", ReceiveCreatesConversation);
            suite.Add("Сообщения: пустой текст отклоняется", EmptyText);
            suite.Add("Сообщения: история переписки в порядке отправки", History);
            suite.Add("Сообщения: пустая история — успех с пустым списком", EmptyHistory);
            suite.Add("Сообщения: история неизвестного контакта", HistoryUnknownContact);
            suite.Add("Сообщения: прочтение сообщения", MarkRead);
            suite.Add("Сообщения: повторное прочтение идемпотентно", MarkReadTwice);
            suite.Add("Сообщения: прочтение неизвестного сообщения", MarkReadUnknown);
            suite.Add("Сообщения: счётчик непрочитанных", UnreadCounter);
            suite.Add("Сообщения: счётчик по нескольким контактам", UnreadTotalTwoContacts);
            suite.Add("Сообщения: прочитать всё в переписке", MarkAllRead);
            suite.Add("Сообщения: уникальность ID сообщений", UniqueMessageIds);
            suite.Add("Сообщения: время сообщения из адаптера часов", MessageTime);
            suite.Add("Сообщения: сбой часов не теряет сообщение", ClockFailureKeepsMessage);
            suite.Add("Сообщения: некорректное время часов", BadClockValue);
            suite.Add("Сообщения: исходящее всегда прочитано", OutgoingAlwaysRead);
            suite.Add("Сообщения: снимок для сохранения", Snapshot);
            suite.Add("Сообщения: PhoneInbox для внешних систем", Inbox);
            suite.Add("Сообщения: валидация модели сообщения", MessageModelValidation);
            suite.Add("Сообщения: входящие и исходящие различимы", DirectionBasics);

            // Регрессионные тесты аудита ТЗ №015 (восстановление из сохранения).
            suite.Add("Сообщения: восстановление из снимка", RestoreFromSnapshot);
            suite.Add("Сообщения: после восстановления ID сообщений не конфликтуют", RestoreKeepsIdSpace);
            suite.Add("Сообщения: пустой снимок восстанавливается", RestoreEmptySnapshot);
            suite.Add("Сообщения: дубли ID в снимке отклоняются", RestoreRejectsDuplicateIds);
            suite.Add("Сообщения: битый снимок не меняет сервис", RestoreRejectsBrokenSnapshot);
            suite.Add("Сообщения: снимок защищён от мутации вызывающим кодом", SnapshotIsDefensiveCopy);
        }

        private static IPhoneMessageService NewService()
        {
            return new PhoneMessageService();
        }

        private static void StartConversation()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(10d);

            MiniTestRunner.IsTrue(messages.StartConversation(Anna).IsSuccess, "переписка создана");
            MiniTestRunner.AreEqual(1, messages.Conversations.Count, "одна переписка");
            MiniTestRunner.IsTrue(messages.GetConversation(Anna).IsSuccess, "переписка найдена по ID");

            MiniTestRunner.IsTrue(messages.ReceiveFromContact(Anna, "Привет", clock).IsSuccess, "сообщение добавлено");
            MiniTestRunner.AreEqual(1, messages.GetHistory(Anna).Value.Count, "сообщение попало в историю");
        }

        private static void DuplicateConversation()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);

            messages.StartConversation(Anna);
            messages.ReceiveFromContact(Anna, "Первое", clock);

            PhoneResult duplicate = messages.StartConversation(Anna);

            MiniTestRunner.AreEqual(PhoneResultCode.DuplicateId, duplicate.Code, "дубликат переписки отклонён");
            MiniTestRunner.AreEqual(1, messages.GetHistory(Anna).Value.Count, "история не сброшена");
        }

        private static void InvalidConversationId()
        {
            IPhoneMessageService messages = NewService();

            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument, messages.StartConversation("  ").Code, "пробельный ID");
            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument, messages.StartConversation(null).Code, "null ID");
        }

        private static void SendMessage()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(42d);
            messages.StartConversation(Anna);

            PhoneResult<PhoneMessage> sent = messages.SendFromPlayer(Anna, "Я в пути", clock);

            MiniTestRunner.IsTrue(sent.IsSuccess, "сообщение отправлено");
            MiniTestRunner.AreEqual(PhoneMessageDirection.Outgoing, sent.Value.Direction, "направление Outgoing");
            MiniTestRunner.IsTrue(sent.Value.IsRead, "исходящее считается прочитанным");
            MiniTestRunner.AreEqual(42d, sent.Value.SentAt.Minutes, "время отправки");
            MiniTestRunner.AreEqual(0, messages.TotalUnread, "исходящее не увеличивает счётчик");
        }

        private static void SendWithoutConversation()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);

            PhoneResult<PhoneMessage> sent = messages.SendFromPlayer(Anna, "Привет", clock);

            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, sent.Code, "нельзя писать в несуществующую переписку");
            MiniTestRunner.AreEqual(0, messages.Conversations.Count, "переписка не создана");
        }

        private static void ReceiveCreatesConversation()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(5d);

            PhoneResult<PhoneMessage> received = messages.ReceiveFromContact(Anna, "Ты где?", clock);

            MiniTestRunner.IsTrue(received.IsSuccess, "сообщение получено");
            MiniTestRunner.AreEqual(1, messages.Conversations.Count, "переписка создана автоматически");
            MiniTestRunner.AreEqual(1, messages.TotalUnread, "входящее непрочитано");
        }

        private static void EmptyText()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);
            messages.StartConversation(Anna);

            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument, messages.SendFromPlayer(Anna, "  ", clock).Code, "пробельный текст");
            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument, messages.ReceiveFromContact(Anna, null, clock).Code, "null-текст");
            MiniTestRunner.AreEqual(0, messages.GetHistory(Anna).Value.Count, "история пуста");
        }

        private static void History()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);
            messages.StartConversation(Anna);

            messages.ReceiveFromContact(Anna, "Раз", clock);
            clock.Advance(2d);
            messages.SendFromPlayer(Anna, "Два", clock);
            clock.Advance(3d);
            messages.ReceiveFromContact(Anna, "Три", clock);

            IReadOnlyList<PhoneMessage> history = messages.GetHistory(Anna).Value;

            MiniTestRunner.AreEqual(3, history.Count, "три сообщения в истории");
            MiniTestRunner.AreEqual("Раз", history[0].Text, "первое сообщение");
            MiniTestRunner.AreEqual("Два", history[1].Text, "второе сообщение");
            MiniTestRunner.AreEqual("Три", history[2].Text, "третье сообщение");
            MiniTestRunner.AreEqual(0d, history[0].SentAt.Minutes, "время первого");
            MiniTestRunner.AreEqual(5d, history[2].SentAt.Minutes, "время третьего");
        }

        private static void EmptyHistory()
        {
            IPhoneMessageService messages = NewService();
            messages.StartConversation(Anna);

            PhoneResult<IReadOnlyList<PhoneMessage>> history = messages.GetHistory(Anna);

            MiniTestRunner.IsTrue(history.IsSuccess, "пустая история — не ошибка");
            MiniTestRunner.AreEqual(0, history.Value.Count, "история пуста");
        }

        private static void HistoryUnknownContact()
        {
            IPhoneMessageService messages = NewService();

            PhoneResult<IReadOnlyList<PhoneMessage>> history = messages.GetHistory("nobody");

            MiniTestRunner.AreEqual(PhoneResultCode.NotFound, history.Code, "неизвестный контакт");
            MiniTestRunner.IsNull(history.Value, "история не возвращается");
        }

        private static void MarkRead()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);
            messages.ReceiveFromContact(Anna, "Новое", clock);
            string messageId = messages.GetHistory(Anna).Value[0].Id;

            MiniTestRunner.AreEqual(1, messages.TotalUnread, "до прочтения 1 непрочитанное");
            MiniTestRunner.IsTrue(messages.MarkRead(Anna, messageId).IsSuccess, "сообщение прочитано");
            MiniTestRunner.AreEqual(0, messages.TotalUnread, "после прочтения 0");
            MiniTestRunner.IsTrue(messages.GetHistory(Anna).Value[0].IsRead, "флаг прочтения установлен");
        }

        private static void MarkReadTwice()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);
            messages.ReceiveFromContact(Anna, "Новое", clock);
            string messageId = messages.GetHistory(Anna).Value[0].Id;

            MiniTestRunner.IsTrue(messages.MarkRead(Anna, messageId).IsSuccess, "первое прочтение");
            MiniTestRunner.IsTrue(messages.MarkRead(Anna, messageId).IsSuccess, "повторное прочтение не ошибка");
            MiniTestRunner.AreEqual(0, messages.TotalUnread, "счётчик не ушёл в минус");
        }

        private static void MarkReadUnknown()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);
            messages.ReceiveFromContact(Anna, "Новое", clock);

            MiniTestRunner.AreEqual(
                PhoneResultCode.NotFound, messages.MarkRead(Anna, "nope").Code, "неизвестное сообщение");
            MiniTestRunner.AreEqual(
                PhoneResultCode.NotFound, messages.MarkRead("nobody", "nope").Code, "неизвестная переписка");
            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument, messages.MarkRead(Anna, " ").Code, "пустой ID сообщения");
        }

        private static void UnreadCounter()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);

            messages.ReceiveFromContact(Anna, "1", clock);
            messages.ReceiveFromContact(Anna, "2", clock);
            messages.SendFromPlayer(Anna, "3", clock);

            MiniTestRunner.AreEqual(2, messages.TotalUnread, "два непрочитанных входящих");

            IPhoneConversation conversation = messages.GetConversation(Anna).Value;
            MiniTestRunner.AreEqual(2, conversation.UnreadCount, "счётчик переписки");
        }

        private static void UnreadTotalTwoContacts()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);

            messages.ReceiveFromContact(Anna, "А", clock);
            messages.ReceiveFromContact(Boris, "Б", clock);
            messages.ReceiveFromContact(Boris, "В", clock);

            MiniTestRunner.AreEqual(2, messages.Conversations.Count, "две переписки");
            MiniTestRunner.AreEqual(3, messages.TotalUnread, "три непрочитанных суммарно");
        }

        private static void MarkAllRead()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);
            messages.ReceiveFromContact(Anna, "1", clock);
            messages.ReceiveFromContact(Boris, "2", clock);

            MiniTestRunner.IsTrue(messages.MarkAllRead(Anna).IsSuccess, "прочитано всё у Анны");
            MiniTestRunner.AreEqual(1, messages.TotalUnread, "у Бориса осталось непрочитанное");
            MiniTestRunner.AreEqual(1, messages.GetConversation(Boris).Value.UnreadCount, "счётчик Бориса");
        }

        private static void UniqueMessageIds()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);

            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < 50; i++)
            {
                messages.ReceiveFromContact(Anna, "msg " + i, clock);
                MiniTestRunner.IsTrue(
                    ids.Add(messages.GetHistory(Anna).Value[i].Id), "ID сообщения уникален: " + i);
            }

            messages.ReceiveFromContact(Boris, "другой контакт", clock);
            MiniTestRunner.IsTrue(
                ids.Add(messages.GetHistory(Boris).Value[0].Id), "ID уникален между переписками");
        }

        private static void MessageTime()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(100d);

            messages.ReceiveFromContact(Anna, "Сейчас", clock);
            clock.Advance(15d);
            messages.ReceiveFromContact(Anna, "Позже", clock);

            IReadOnlyList<PhoneMessage> history = messages.GetHistory(Anna).Value;

            MiniTestRunner.AreEqual(100d, history[0].SentAt.Minutes, "время первого сообщения");
            MiniTestRunner.AreEqual(115d, history[1].SentAt.Minutes, "время второго сообщения");
        }

        private static void ClockFailureKeepsMessage()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d) { ThrowOnRead = true };

            PhoneResult<PhoneMessage> received = messages.ReceiveFromContact(Anna, "Без времени", clock);

            MiniTestRunner.IsTrue(received.IsSuccess, "сообщение сохранено несмотря на сбой часов");
            MiniTestRunner.IsFalse(received.Value.SentAt.IsValid, "время помечено как неизвестное");
            MiniTestRunner.AreEqual(1, messages.GetHistory(Anna).Value.Count, "сообщение в истории");
        }

        private static void BadClockValue()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(double.NaN);

            PhoneResult<PhoneMessage> received = messages.ReceiveFromContact(Anna, "NaN", clock);

            MiniTestRunner.IsTrue(received.IsSuccess, "сообщение сохранено");
            MiniTestRunner.IsFalse(received.Value.SentAt.IsValid, "NaN превращён в Unknown");
            MiniTestRunner.AreEqual(0d, received.Value.SentAt.Minutes, "безопасное значение минут");
        }

        private static void OutgoingAlwaysRead()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(0d);
            messages.StartConversation(Anna);

            PhoneResult<PhoneMessage> sent = messages.SendFromPlayer(Anna, "Текст", clock);

            MiniTestRunner.IsTrue(sent.Value.IsRead, "исходящее всегда прочитано");
            MiniTestRunner.AreEqual(0, messages.TotalUnread, "непрочитанных нет");
        }

        private static void Snapshot()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(7d);
            messages.ReceiveFromContact(Anna, "Снимок", clock);
            messages.StartConversation(Boris);

            PhoneMessagesSnapshot snapshot = messages.CreateSnapshot();

            MiniTestRunner.AreEqual(2, snapshot.Conversations.Count, "две переписки в снимке");
            MiniTestRunner.AreEqual(Anna, snapshot.Conversations[0].ConversationId, "ID первой переписки");
            MiniTestRunner.AreEqual(1, snapshot.Conversations[0].Messages.Count, "одно сообщение");
            MiniTestRunner.AreEqual("Снимок", snapshot.Conversations[0].Messages[0].Text, "текст в снимке");
            MiniTestRunner.AreEqual(false, snapshot.Conversations[0].Messages[0].IsRead, "статус в снимке");
            MiniTestRunner.AreEqual(7d, snapshot.Conversations[0].Messages[0].SentAt.Minutes, "время в снимке");
            MiniTestRunner.AreEqual(0, snapshot.Conversations[1].Messages.Count, "пустая переписка в снимке");
        }

        private static void Inbox()
        {
            var messages = new PhoneMessageService();
            var inbox = new PhoneInbox(messages);
            var clock = new FakePhoneClock(3d);

            MiniTestRunner.IsTrue(inbox.Deliver(Anna, "Письмо от NPC", clock).IsSuccess, "внешняя система доставила сообщение");
            MiniTestRunner.AreEqual(1, messages.GetHistory(Anna).Value.Count, "сообщение в истории");
            MiniTestRunner.AreEqual(PhoneResultCode.InvalidArgument, inbox.Deliver("", "X", clock).Code, "пустой контакт");
        }

        private static void MessageModelValidation()
        {
            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<ArgumentException>(
                    () => new PhoneMessage("", Anna, PhoneMessageDirection.Incoming, "X", GameMinutes.Unknown, false)),
                "пустой ID сообщения");

            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<ArgumentException>(
                    () => new PhoneMessage("id", "", PhoneMessageDirection.Incoming, "X", GameMinutes.Unknown, false)),
                "пустая переписка");

            MiniTestRunner.IsTrue(
                PhoneAppRegistryTests.Throws<ArgumentException>(
                    () => new PhoneMessage("id", Anna, PhoneMessageDirection.Incoming, " ", GameMinutes.Unknown, false)),
                "пустой текст");

            var outgoing = new PhoneMessage("id", Anna, PhoneMessageDirection.Outgoing, "X", GameMinutes.Unknown, false);
            MiniTestRunner.IsTrue(outgoing.IsRead, "исходящее принудительно прочитано");
        }

        // ---------- Восстановление из сохранения (ТЗ №015) ----------

        private static void RestoreFromSnapshot()
        {
            var source = new PhoneMessageService();
            var clock = new FakePhoneClock(10d);
            source.ReceiveFromContact(Anna, "Первое", clock);
            clock.Advance(5d);
            source.SendFromPlayer(Anna, "Ответ", clock);
            source.ReceiveFromContact(Boris, "Второе", clock);
            source.MarkRead(Anna, source.GetHistory(Anna).Value[0].Id);

            PhoneMessagesSnapshot snapshot = source.CreateSnapshot();

            var target = new PhoneMessageService();
            string error;
            PhoneResult restored = PhoneMessagesRestore.RestoreInto(target, snapshot, out error);

            MiniTestRunner.IsTrue(restored.IsSuccess, "снимок восстановлен: " + error);
            MiniTestRunner.AreEqual(2, target.Conversations.Count, "две переписки");
            MiniTestRunner.AreEqual(3, target.GetHistory(Anna).Value.Count + target.GetHistory(Boris).Value.Count, "три сообщения");
            MiniTestRunner.AreEqual(1, target.TotalUnread, "непрочитанных ровно одно (входящее у Бориса)");

            IReadOnlyList<PhoneMessage> annaHistory = target.GetHistory(Anna).Value;
            MiniTestRunner.IsTrue(annaHistory[0].IsRead, "статус прочтения восстановлен");
            MiniTestRunner.AreEqual("Ответ", annaHistory[1].Text, "текст восстановлен");
            MiniTestRunner.AreEqual(PhoneMessageDirection.Outgoing, annaHistory[1].Direction, "направление восстановлено");
            MiniTestRunner.AreEqual(15d, annaHistory[1].SentAt.Minutes, "время восстановлено");
            MiniTestRunner.AreEqual(Anna, annaHistory[1].ConversationId, "привязка к переписке восстановлена");
        }

        /// <summary>
        /// После восстановления новые сообщения не должны получить ID, занятый загруженными:
        /// иначе MarkRead мог бы «прочитать» не то сообщение.
        /// </summary>
        private static void RestoreKeepsIdSpace()
        {
            var source = new PhoneMessageService();
            var clock = new FakePhoneClock(0d);
            source.ReceiveFromContact(Anna, "Старое", clock);

            var target = new PhoneMessageService();
            string error;
            PhoneMessagesRestore.RestoreInto(target, source.CreateSnapshot(), out error);

            target.ReceiveFromContact(Anna, "Новое", clock);
            IReadOnlyList<PhoneMessage> history = target.GetHistory(Anna).Value;

            MiniTestRunner.AreEqual(2, history.Count, "два сообщения");
            MiniTestRunner.IsTrue(history[0].Id != history[1].Id, "ID не совпадают после восстановления");
            MiniTestRunner.AreEqual(2, target.TotalUnread, "оба сообщения непрочитаны");
        }

        private static void RestoreEmptySnapshot()
        {
            var target = new PhoneMessageService();
            var clock = new FakePhoneClock(0d);
            target.ReceiveFromContact(Anna, "Будет стёрто", clock);

            string error;
            PhoneResult restored = PhoneMessagesRestore.RestoreInto(target, new PhoneMessagesSnapshot(null), out error);

            MiniTestRunner.IsTrue(restored.IsSuccess, "пустой снимок — валидный: " + error);
            MiniTestRunner.AreEqual(0, target.Conversations.Count, "сервис очищен");
            MiniTestRunner.AreEqual(0, target.TotalUnread, "счётчик обнулён");
        }

        private static void RestoreRejectsDuplicateIds()
        {
            var target = new PhoneMessageService();
            var clock = new FakePhoneClock(0d);
            target.ReceiveFromContact(Boris, "Существующее", clock);

            var duplicatedMessages = new List<PhoneMessageSnapshot>
            {
                new PhoneMessageSnapshot("m1", Anna, PhoneMessageDirection.Incoming, "Раз", GameMinutes.FromMinutes(1d), false),
                new PhoneMessageSnapshot("m1", Anna, PhoneMessageDirection.Incoming, "Два", GameMinutes.FromMinutes(2d), false)
            };

            var snapshot = new PhoneMessagesSnapshot(new List<PhoneConversationSnapshot>
            {
                new PhoneConversationSnapshot(Anna, duplicatedMessages)
            });

            string error;
            PhoneResult restored = PhoneMessagesRestore.RestoreInto(target, snapshot, out error);

            MiniTestRunner.AreEqual(PhoneResultCode.DuplicateId, restored.Code, "дубль ID сообщения отклонён");
            MiniTestRunner.IsTrue(error != null, "причина сообщена");
            MiniTestRunner.AreEqual(1, target.GetHistory(Boris).Value.Count, "сервис не изменился");

            var duplicatedConversations = new PhoneMessagesSnapshot(new List<PhoneConversationSnapshot>
            {
                new PhoneConversationSnapshot(Anna, new List<PhoneMessageSnapshot>()),
                new PhoneConversationSnapshot(Anna, new List<PhoneMessageSnapshot>())
            });

            MiniTestRunner.AreEqual(
                PhoneResultCode.DuplicateId,
                PhoneMessagesRestore.RestoreInto(target, duplicatedConversations, out error).Code,
                "дубль переписки отклонён");
        }

        private static void RestoreRejectsBrokenSnapshot()
        {
            var target = new PhoneMessageService();
            var clock = new FakePhoneClock(0d);
            target.ReceiveFromContact(Anna, "Живое сообщение", clock);

            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument,
                PhoneMessagesRestore.RestoreInto(target, null, out string errorNull).Code,
                "null-снимок отклонён");

            var wrongConversation = new PhoneMessagesSnapshot(new List<PhoneConversationSnapshot>
            {
                new PhoneConversationSnapshot(Anna, new List<PhoneMessageSnapshot>
                {
                    new PhoneMessageSnapshot("m1", Boris, PhoneMessageDirection.Incoming, "Текст", GameMinutes.Unknown, false)
                })
            });

            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument,
                PhoneMessagesRestore.RestoreInto(target, wrongConversation, out string errorWrong).Code,
                "сообщение из чужой переписки отклонено");

            var outgoingUnread = new PhoneMessagesSnapshot(new List<PhoneConversationSnapshot>
            {
                new PhoneConversationSnapshot(Anna, new List<PhoneMessageSnapshot>
                {
                    new PhoneMessageSnapshot("m1", Anna, PhoneMessageDirection.Outgoing, "Текст", GameMinutes.Unknown, false)
                })
            });

            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidState,
                PhoneMessagesRestore.RestoreInto(target, outgoingUnread, out string errorOutgoing).Code,
                "непрочитанное исходящее отклонено");

            var emptyText = new PhoneMessagesSnapshot(new List<PhoneConversationSnapshot>
            {
                new PhoneConversationSnapshot(Anna, new List<PhoneMessageSnapshot>
                {
                    new PhoneMessageSnapshot("m1", Anna, PhoneMessageDirection.Incoming, "   ", GameMinutes.Unknown, false)
                })
            });

            MiniTestRunner.AreEqual(
                PhoneResultCode.InvalidArgument,
                PhoneMessagesRestore.RestoreInto(target, emptyText, out string errorEmpty).Code,
                "пустой текст отклонён");

            // Ни один из отклонённых снимков не должен был тронуть живые данные.
            MiniTestRunner.AreEqual(1, target.Conversations.Count, "переписка не подменена");
            MiniTestRunner.AreEqual(1, target.GetHistory(Anna).Value.Count, "сообщение не потеряно");
            MiniTestRunner.AreEqual(1, target.TotalUnread, "счётчик не сброшен");
        }

        private static void SnapshotIsDefensiveCopy()
        {
            var list = new List<PhoneMessageSnapshot>
            {
                new PhoneMessageSnapshot("m1", Anna, PhoneMessageDirection.Incoming, "Текст", GameMinutes.FromMinutes(1d), false)
            };

            var snapshot = new PhoneMessagesSnapshot(new List<PhoneConversationSnapshot>
            {
                new PhoneConversationSnapshot(Anna, list)
            });

            list.Clear();

            MiniTestRunner.AreEqual(1, snapshot.Conversations[0].Messages.Count, "снимок не зависит от списка вызывающего кода");
        }

        private static void DirectionBasics()
        {
            IPhoneMessageService messages = NewService();
            var clock = new FakePhoneClock(1d);

            messages.ReceiveFromContact(Anna, "входящее", clock);
            messages.SendFromPlayer(Anna, "исходящее", clock);

            IReadOnlyList<PhoneMessage> history = messages.GetHistory(Anna).Value;

            MiniTestRunner.AreEqual(PhoneMessageDirection.Incoming, history[0].Direction, "первое — входящее");
            MiniTestRunner.AreEqual(PhoneMessageDirection.Outgoing, history[1].Direction, "второе — исходящее");
            MiniTestRunner.AreEqual(Anna, history[0].ConversationId, "ID переписки у сообщения");
        }
    }
}
