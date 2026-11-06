// Life From Zero — Unity integration kit (PhoneFacade → UI IPhoneDataSource)
// Namespace: LifeFromZero.UnityIntegration
//
// Готовый мост между чистым модулем телефона (LifeFromZero.Phone.Integrations.PhoneFacade)
// и экранным контрактом UI (LifeFromZero.UI.Runtime.IPhoneDataSource).
//
// Устраняет расхождение идентификатора приложения курьерской работы:
//   * PhoneAppRegistry по умолчанию регистрирует плитку с Id = "jobs" («Работа»);
//   * UI/Core/PhoneData ожидает PhoneAppIds.Orders = "orders".
// Мост приводит "jobs" → "orders" без изменения PhoneAppRegistry и его тестов.

using System;
using System.Collections.Generic;
using LifeFromZero.Phone.Apps;
using LifeFromZero.Phone.Contacts;
using LifeFromZero.Phone.Core;
using LifeFromZero.Phone.Integrations;
using LifeFromZero.Phone.Messages;
using LifeFromZero.UI.Core;
using LifeFromZero.UI.Runtime;
using UnityEngine;

namespace LifeFromZero.UnityIntegration
{
    /// <summary>
    /// Необязательный владелец <see cref="PhoneFacade"/> для назначения в Inspector.
    /// Переопределите <see cref="Facade"/> или вызовите <see cref="PhoneFacadeUiBridge.SetFacade"/>.
    /// </summary>
    public abstract class PhoneFacadeHost : MonoBehaviour
    {
        public abstract PhoneFacade Facade { get; }
        public virtual BankSummaryService Bank => null;
        public virtual StorySummaryService Story => null;
    }

    /// <summary>
    /// Адаптер <see cref="PhoneFacade"/> → <see cref="IPhoneDataSource"/>.
    /// Назначьте в поле <c>DeliveryServiceBridge.externalPhoneDataSource</c>, чтобы объединить
    /// сообщения/контакты/банк/задачи из <c>PhoneFacade</c> с курьерскими заказами <c>Jobs.Delivery</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhoneFacadeUiBridge : MonoBehaviour, IPhoneDataSource
    {
        [Tooltip("Компонент-владелец PhoneFacade в сцене (необязательно, если фасад передан через SetFacade).")]
        [SerializeField] private PhoneFacadeHost facadeHost;

        private PhoneFacade _facade;
        private BankSummaryService _bank;
        private StorySummaryService _story;
        private string _activeContactId;

        /// <summary>Передать готовый <see cref="PhoneFacade"/> и (необязательно) сводные сервисы банка/сюжета.</summary>
        public void SetFacade(PhoneFacade facade, BankSummaryService bank = null, StorySummaryService story = null)
        {
            _facade = facade;
            _bank = bank;
            _story = story;
        }

        public PhoneFacade CurrentFacade => _facade ?? (facadeHost != null ? facadeHost.Facade : null);
        private BankSummaryService CurrentBank => _bank ?? (facadeHost != null ? facadeHost.Bank : null);
        private StorySummaryService CurrentStory => _story ?? (facadeHost != null ? facadeHost.Story : null);

        public PhoneSnapshot ReadPhone()
        {
            PhoneFacade facade = CurrentFacade;
            if (facade == null)
            {
                return new PhoneSnapshot
                {
                    Apps = PhoneAppCatalog.Build(false),
                };
            }

            var snapshot = new PhoneSnapshot
            {
                Apps = BuildApps(facade),
                Conversations = BuildConversations(facade),
                Messages = BuildMessages(facade, _activeContactId),
                Tasks = BuildTasks(CurrentStory),
                Bank = BuildBank(CurrentBank),
                UnreadMessages = facade.Messages.TotalUnread,
                UnreadNotifications = facade.Notifications.UnreadCount,
            };

            return snapshot;
        }

        public bool TryRun(string action, out string message)
        {
            message = null;
            if (string.IsNullOrEmpty(action)) return false;

            PhoneFacade facade = CurrentFacade;
            if (facade == null)
            {
                message = "Телефонный сервис (PhoneFacade) не подключён.";
                return false;
            }

            if (action.StartsWith(PhoneActions.ReadConversation, StringComparison.Ordinal))
            {
                string contactId = action.Substring(PhoneActions.ReadConversation.Length);
                _activeContactId = contactId;
                facade.Messages.MarkAllRead(contactId);
                return true;
            }

            message = "Раздел «" + action + "» пока не подключён к игре.";
            return false;
        }

        private static IReadOnlyList<PhoneAppTile> BuildApps(PhoneFacade facade)
        {
            IReadOnlyList<PhoneApp> apps = facade.Apps.Apps;
            var tiles = new List<PhoneAppTile>(apps.Count + 4);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < apps.Count; i++)
            {
                PhoneApp app = apps[i];
                if (app == null || PhoneAppIds.IsForbiddenInPhone(app.Id)) continue;
                string normalizedId = PhoneAppIds.Normalize(app.Id);
                string title = normalizedId == PhoneAppIds.Orders && app.Id == PhoneAppIds.LegacyJobs
                    ? "Заказы"
                    : app.Title;
                if (seen.Add(normalizedId))
                {
                    tiles.Add(new PhoneAppTile(normalizedId, title, app.UnreadCount, app.IsAvailable, normalizedId));
                }
            }

            IReadOnlyList<PhoneAppTile> defaults = PhoneAppCatalog.Build(seen.Contains(PhoneAppIds.Orders));
            for (int i = 0; i < defaults.Count; i++)
            {
                if (seen.Add(defaults[i].Id)) tiles.Add(defaults[i]);
            }

            return tiles;
        }

        private static IReadOnlyList<PhoneConversationRow> BuildConversations(PhoneFacade facade)
        {
            IReadOnlyList<IPhoneConversation> convs = facade.Messages.Conversations;
            if (convs == null || convs.Count == 0) return Array.Empty<PhoneConversationRow>();

            var rows = new List<PhoneConversationRow>(convs.Count);
            for (int i = 0; i < convs.Count; i++)
            {
                IPhoneConversation conv = convs[i];
                if (conv == null) continue;

                string title = conv.ConversationId;
                PhoneResult<PhoneContact> contact = facade.Contacts.GetById(conv.ConversationId);
                if (contact.IsSuccess && contact.Value != null && !string.IsNullOrEmpty(contact.Value.Name))
                    title = contact.Value.Name;

                string preview = string.Empty;
                string timeLabel = string.Empty;
                IReadOnlyList<PhoneMessage> msgs = conv.Messages;
                if (msgs != null && msgs.Count > 0)
                {
                    PhoneMessage last = msgs[msgs.Count - 1];
                    preview = last.Text ?? string.Empty;
                    if (last.SentAt.IsValid) timeLabel = PhoneFormat.GameClock((float)last.SentAt.Minutes);
                }

                int seed = conv.ConversationId != null ? Math.Abs(conv.ConversationId.GetHashCode()) : i;
                rows.Add(new PhoneConversationRow(conv.ConversationId, title, preview, timeLabel,
                    conv.UnreadCount, seed, false));
            }
            return rows;
        }

        private static IReadOnlyList<PhoneMessageRow> BuildMessages(PhoneFacade facade, string contactId)
        {
            if (string.IsNullOrEmpty(contactId)) return Array.Empty<PhoneMessageRow>();

            PhoneResult<IReadOnlyList<PhoneMessage>> history = facade.Messages.GetHistory(contactId);
            if (!history.IsSuccess || history.Value == null || history.Value.Count == 0)
                return Array.Empty<PhoneMessageRow>();

            var rows = new List<PhoneMessageRow>(history.Value.Count);
            for (int i = 0; i < history.Value.Count; i++)
            {
                PhoneMessage msg = history.Value[i];
                if (msg == null) continue;
                string timeLabel = msg.SentAt.IsValid ? PhoneFormat.GameClock((float)msg.SentAt.Minutes) : string.Empty;
                rows.Add(new PhoneMessageRow(
                    msg.Text,
                    timeLabel,
                    msg.Direction == PhoneMessageDirection.Incoming,
                    msg.IsRead));
            }
            return rows;
        }

        private static IReadOnlyList<StoryEntryRow> BuildTasks(StorySummaryService story)
        {
            if (story == null) return Array.Empty<StoryEntryRow>();
            PhoneResult<StoryEntryDto[]> entries = story.GetEntries();
            if (!entries.IsSuccess || entries.Value == null || entries.Value.Length == 0)
                return Array.Empty<StoryEntryRow>();

            var rows = new List<StoryEntryRow>(entries.Value.Length);
            for (int i = 0; i < entries.Value.Length; i++)
            {
                StoryEntryDto dto = entries.Value[i];
                if (dto == null) continue;
                rows.Add(new StoryEntryRow(dto.Title, dto.Text, false, string.Empty));
            }
            return rows;
        }

        private static BankView BuildBank(BankSummaryService bank)
        {
            var view = new BankView { OperationsAvailable = false };
            if (bank == null) return view;
            PhoneResult<BankSummary> summaryResult = bank.GetSummary();
            if (!summaryResult.IsSuccess) return view;

            BankSummary summary = summaryResult.Value;
            if (summary.BalanceAvailable) view.Balance = summary.BalanceRubles;
            if (summary.DebtsAvailable && summary.Debts != null)
            {
                long totalDebt = 0L;
                for (int i = 0; i < summary.Debts.Length; i++)
                {
                    DebtDto debt = summary.Debts[i];
                    if (debt == null || debt.Phase == DebtPhase.Closed || debt.AmountRubles <= 0L) continue;
                    totalDebt += debt.AmountRubles;
                }
                view.Debt = totalDebt;
            }
            return view;
        }
    }
}
