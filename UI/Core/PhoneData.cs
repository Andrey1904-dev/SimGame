// Life From Zero — UI/Core (ТЗ №042 §4)
// Namespace: LifeFromZero.UI.Core
//
// Данные телефона для UI: приложения, диалоги, звонки, банк, заказы.
//
// ВАЖНО: здесь НЕТ второй экономики, второго времени и второй доставки.
// Всё приходит адаптерами из существующих систем (Jobs.Delivery, Economy, Debts,
// Phone). UI-слой только раскладывает данные по экранам.
//
// Модуль без UnityEngine.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace LifeFromZero.UI.Core
{
    /// <summary>Идентификаторы приложений телефона (совпадают с референсом Phone.png).</summary>
    public static class PhoneAppIds
    {
        public const string Messages = "messages";
        public const string Calls = "calls";
        public const string Contacts = "contacts";
        public const string Bank = "bank";
        public const string Orders = "orders";
        /// <summary>Алиас модуля <c>LifeFromZero.Phone</c> (<c>PhoneAppRegistry</c> регистрирует «Работу» как <c>jobs</c>).</summary>
        public const string LegacyJobs = "jobs";
        public const string Tasks = "tasks";
        public const string Ads = "ads";
        public const string Education = "education";
        public const string Gallery = "gallery";
        public const string Settings = "settings";

        /// <summary>
        /// Приложения НА РАБОЧЕМ СТОЛЕ в порядке референса (сетка 3×4).
        /// Приложения «Карта» здесь нет и быть не может (ТЗ §2, §4.8):
        /// маршрут строится кнопкой «Построить маршрут» в «Заказах».
        /// </summary>
        public static IReadOnlyList<string> HomeOrder { get; } = new[]
        {
            Calls, Messages, Contacts,
            Bank, Orders, Tasks,
            Ads, Education, Settings,
            Gallery,
        }.AsReadOnly();

        /// <summary>Проверка запрета на приложение «Карта» в телефоне.</summary>
        public static bool IsForbiddenInPhone(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) return false;
            string id = appId.Trim().ToLowerInvariant();
            return id == "map" || id == "maps" || id == "карта";
        }

        /// <summary>
        /// Раскладывает плитки в порядке рабочего стола (<see cref="HomeOrder"/>),
        /// отбрасывая недопустимые приложения и дубликаты по нормализованному id.
        /// Лишние плитки (которых нет в референсе) идут в конце, а не теряются.
        /// </summary>
        public static List<PhoneAppTile> Sort(IReadOnlyList<PhoneAppTile> apps)
        {
            var result = new List<PhoneAppTile>();
            if (apps == null || apps.Count == 0) return result;

            var used = new bool[apps.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);

            IReadOnlyList<string> order = HomeOrder;
            for (int i = 0; i < order.Count; i++)
            {
                for (int j = 0; j < apps.Count; j++)
                {
                    if (used[j]) continue;
                    PhoneAppTile app = apps[j];
                    if (app == null) { used[j] = true; continue; }
                    if (IsForbiddenInPhone(app.Id)) { used[j] = true; continue; }
                    if (Normalize(app.Id) != order[i]) continue;

                    used[j] = true;
                    if (seen.Add(Normalize(app.Id))) result.Add(app);
                    break;
                }
            }

            for (int j = 0; j < apps.Count; j++)
            {
                if (used[j] || apps[j] == null) continue;
                if (IsForbiddenInPhone(apps[j].Id)) continue;
                if (!seen.Add(Normalize(apps[j].Id))) continue;
                result.Add(apps[j]);
            }

            return result;
        }

        /// <summary>Нормализует идентификатор приложения между модулем Phone (<c>jobs</c>) и UI (<c>orders</c>).</summary>
        public static string Normalize(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) return string.Empty;
            string id = appId.Trim().ToLowerInvariant();
            return id == LegacyJobs ? Orders : id;
        }
    }

    /// <summary>Плитка приложения на рабочем столе.</summary>
    public sealed class PhoneAppTile
    {
        public PhoneAppTile(string id, string title, int badge, bool available, string iconKind)
        {
            Id = id;
            Title = title ?? string.Empty;
            Badge = badge < 0 ? 0 : badge;
            Available = available;
            IconKind = iconKind ?? id;
        }

        public string Id { get; }

        public string Title { get; }

        /// <summary>Красный бейдж с числом непрочитанных (0 — не показывать).</summary>
        public int Badge { get; }

        public bool Available { get; }

        /// <summary>Имя процедурной иконки (см. UiIconKind).</summary>
        public string IconKind { get; }
    }

    /// <summary>Строка списка диалогов.</summary>
    public sealed class PhoneConversationRow
    {
        public PhoneConversationRow(string contactId, string title, string preview, string timeLabel,
            int unread, int avatarSeed, bool group)
        {
            ContactId = contactId;
            Title = title ?? string.Empty;
            Preview = preview ?? string.Empty;
            TimeLabel = timeLabel ?? string.Empty;
            Unread = unread < 0 ? 0 : unread;
            AvatarSeed = avatarSeed;
            IsGroup = group;
        }

        public string ContactId { get; }

        public string Title { get; }

        public string Preview { get; }

        public string TimeLabel { get; }

        public int Unread { get; }

        /// <summary>Детерминированный «аватар»: буква + цвет по этому числу.</summary>
        public int AvatarSeed { get; }

        public bool IsGroup { get; }
    }

    /// <summary>Строка переписки.</summary>
    public sealed class PhoneMessageRow
    {
        public PhoneMessageRow(string text, string timeLabel, bool incoming, bool read)
        {
            Text = text ?? string.Empty;
            TimeLabel = timeLabel ?? string.Empty;
            Incoming = incoming;
            Read = read;
        }

        public string Text { get; }

        public string TimeLabel { get; }

        public bool Incoming { get; }

        public bool Read { get; }
    }

    /// <summary>Состояние звонка.</summary>
    public enum CallState
    {
        None = 0,
        Incoming = 1,
        Outgoing = 2,
        Active = 3,
        Ended = 4,
    }

    /// <summary>Экран звонка.</summary>
    public sealed class CallView
    {
        public CallView(CallState state, string contactTitle, string subtitle, int avatarSeed)
        {
            State = state;
            ContactTitle = contactTitle ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            AvatarSeed = avatarSeed;
        }

        public CallState State { get; }

        public string ContactTitle { get; }

        /// <summary>«Вызов…», «Входящий вызов», «На связи 00:12».</summary>
        public string Subtitle { get; }

        public int AvatarSeed { get; }

        public bool CanAccept => State == CallState.Incoming;
        public bool CanDecline => State == CallState.Incoming || State == CallState.Outgoing;
        public bool CanHangUp => State == CallState.Active || State == CallState.Outgoing;
    }

    /// <summary>Строка истории операций банка (только чтение).</summary>
    public sealed class BankOperationRow
    {
        public BankOperationRow(string title, string amount, bool positive)
        {
            Title = title ?? string.Empty;
            Amount = amount ?? string.Empty;
            Positive = positive;
        }

        public string Title { get; }

        public string Amount { get; }

        public bool Positive { get; }
    }

    /// <summary>Экран «Банк». Данные приходят из существующего кошелька/долгов.</summary>
    public sealed class BankView
    {
        public long? Balance;
        public long? Debt;
        public long? DailyIncome;
        public IReadOnlyList<BankOperationRow> Operations = Array.Empty<BankOperationRow>();

        /// <summary>Источник операций реализован (иначе блок скрывается, а не заполняется примером).</summary>
        public bool OperationsAvailable;
    }

    /// <summary>Статус курьерского заказа так, как его видит игрок.</summary>
    public enum OrderStage
    {
        None = 0,
        Available = 1,
        Accepted = 2,
        PickedUp = 3,
        Completed = 4,
        Failed = 5,
        Cancelled = 6,
    }

    /// <summary>Заказ на экране «Заказы».</summary>
    public sealed class OrderView
    {
        public string OrderId;
        public string CargoTitle;
        public string DropOffTitle;

        /// <summary>Адрес доставки человеческим языком («Северный квартал, подъезд 2»), если есть.</summary>
        public string Address;

        public long? PaymentRubles;
        public OrderStage Stage;
        public bool HasDropOffCoordinates;

        /// <summary>Координаты доставки берутся из данных доставки, а не угадываются.</summary>
        public MapPoint? DropOffPosition;

        public string DeadlineLabel;

        /// <summary>Можно ли строить маршрут и почему нет (ТЗ §4.7).</summary>
        public bool CanRoute(out string reason)
        {
            if (string.IsNullOrEmpty(OrderId))
            {
                reason = "Активного заказа нет.";
                return false;
            }
            if (!HasDropOffCoordinates || !DropOffPosition.HasValue)
            {
                reason = "Координаты точки доставки недоступны — маршрут не построить.";
                return false;
            }
            if (Stage != OrderStage.Accepted && Stage != OrderStage.PickedUp)
            {
                reason = "Маршрут строится для принятого заказа.";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }

    /// <summary>Экран «Заказы»: активный заказ, выполнено, заработок, рейтинг.</summary>
    public sealed class OrdersView
    {
        public OrderView Active;
        public int CompletedToday;
        public long? EarnedTodayRubles;
        public float? Rating;
        public bool CourierShiftActive;
        public string ShiftTimeLabel;

        /// <summary>Список доступных заказов, если система доставки их отдаёт.</summary>
        public IReadOnlyList<OrderView> Available = Array.Empty<OrderView>();

        /// <summary>Причина, по которой раздел не работает (нет системы доставки и т. п.).</summary>
        public string UnavailableReason;
    }

    /// <summary>Запись сюжета/задания в телефоне.</summary>
    public sealed class StoryEntryRow
    {
        public StoryEntryRow(string title, string body, bool done, string progress)
        {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            Done = done;
            Progress = progress ?? string.Empty;
        }

        public string Title { get; }

        public string Body { get; }

        public bool Done { get; }

        public string Progress { get; }
    }

    /// <summary>Все данные телефона в одном снимке.</summary>
    public sealed class PhoneSnapshot
    {
        public IReadOnlyList<PhoneAppTile> Apps = Array.Empty<PhoneAppTile>();
        public IReadOnlyList<PhoneConversationRow> Conversations = Array.Empty<PhoneConversationRow>();
        public IReadOnlyList<PhoneMessageRow> Messages = Array.Empty<PhoneMessageRow>();
        public IReadOnlyList<StoryEntryRow> Tasks = Array.Empty<StoryEntryRow>();
        public BankView Bank = new BankView();
        public OrdersView Orders = new OrdersView();
        public CallView Call;
        public string ContactTitle = string.Empty;
        public string ContactSubtitle = string.Empty;

        /// <summary>Время на экране блокировки (игровое, не системное).</summary>
        public string LockTime = UiFormat.Dash;

        public string LockDate = string.Empty;

        /// <summary>Уведомление на экране блокировки: заголовок и текст.</summary>
        public IReadOnlyList<LockNotification> LockNotifications = Array.Empty<LockNotification>();

        public int UnreadMessages;
        public int UnreadNotifications;

        public PhoneAppTile FindApp(string id)
        {
            for (int i = 0; i < Apps.Count; i++)
                if (string.Equals(Apps[i].Id, id, StringComparison.Ordinal))
                    return Apps[i];
            return null;
        }
    }

    /// <summary>Карточка уведомления на экране блокировки.</summary>
    public sealed class LockNotification
    {
        public LockNotification(string appId, string title, string body, string timeLabel, int iconSeed)
        {
            AppId = appId ?? string.Empty;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            TimeLabel = timeLabel ?? string.Empty;
            IconSeed = iconSeed;
        }

        public string AppId { get; }

        public string Title { get; }

        public string Body { get; }

        public string TimeLabel { get; }

        public int IconSeed { get; }
    }

    /// <summary>Вспомогательные функции телефона (без состояния).</summary>
    public static class PhoneFormat
    {
        /// <summary>«08:37», «Вчера», «07:29» — как в референсе.</summary>
        public static string MessageTime(DateTime? sentAt, DateTime? now)
        {
            if (!sentAt.HasValue || !now.HasValue) return string.Empty;
            TimeSpan delta = now.Value - sentAt.Value;
            if (delta.TotalDays >= 1 && delta.TotalDays < 2) return "Вчера";
            if (delta.TotalDays >= 2) return ((int)delta.TotalDays) + " дн";
            return sentAt.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>Игровое время «08:42» из минут с начала суток.</summary>
        public static string GameClock(float minutesOfDay)
        {
            int total = (int)Math.Floor(minutesOfDay);
            total = ((total % 1440) + 1440) % 1440;
            return UiFormat.Time(total / 60, total % 60);
        }

        /// <summary>Бейдж: «9+» после девяти.</summary>
        public static string Badge(int count)
        {
            if (count <= 0) return string.Empty;
            return count > 9 ? "9+" : count.ToString(CultureInfo.InvariantCulture);
        }
    }
}
