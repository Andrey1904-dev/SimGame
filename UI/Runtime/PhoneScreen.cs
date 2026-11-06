// Life From Zero — UI/Runtime (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// Экран телефона: рабочий стол, сообщения, переписка, заказы, заказ, банк, задания, звонок.
//
// Контракт с игрой намеренно узкий:
//   DataProvider  → PhoneSnapshot   (UI/Core/PhoneData.cs — уже готовые данные)
//   ActionHandler → строка действия, возвращает текст ошибки для игрока или null
//   RouteHandler  → строки «построить маршрут к заказу»
// Экономики, времени, доставки и телефонии здесь нет: только раскладка данных.

using System;
using System.Collections.Generic;
using LifeFromZero.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Разделы телефона.</summary>
    public enum PhonePage
    {
        Home = 0,
        Messages = 1,
        Conversation = 2,
        Orders = 3,
        Order = 4,
        Bank = 5,
        Tasks = 6,
        Call = 7,
    }

    /// <summary>
    /// Идентификаторы действий телефона. Строки уходят в <see cref="PhoneScreen.ActionHandler"/>;
    /// интегратор переводит их в вызовы существующих сервисов (DeliveryService, PhoneFacade…).
    /// </summary>
    public static class PhoneActions
    {
        public const string OpenApp = "app.";
        public const string ReadConversation = "messages.open:";
        public const string OrderAccept = "order.accept:";
        public const string OrderPickUp = "order.pickup:";
        public const string OrderComplete = "order.complete:";
        public const string OrderCancel = "order.cancel:";
        public const string OrderAbandon = "order.abandon:";
        public const string OrderRoute = "order.route:";
        public const string CallAccept = "call.accept";
        public const string CallDecline = "call.decline";
    }

    /// <summary>Экран телефона. Показывается/скрывается роутером, сам ввод не читает.</summary>
    public sealed class PhoneScreen : MonoBehaviour
    {
        [Header("Вид")]
        [Tooltip("Доля высоты экрана под корпус телефона (референс: 0.92).")]
        [SerializeField] private float heightRatio = 0.92f;

        [Tooltip("Сдвиг корпуса вправо от центра, доли ширины экрана.")]
        [SerializeField] private float horizontalOffset = 0.19f;

        // Корпус по референсу Phone.png: 470×900 при высоте кадра 941.
        private const float ReferenceBodyWidth = 500f;
        private const float ReferenceBodyHeight = 941f;
        private const int GridColumns = 4;

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _content;
        private Text _clockLabel;
        private Text _titleLabel;
        private Button _backButton;
        private UiSpriteCache _sprites;

        private PhoneSnapshot _data = new PhoneSnapshot();
        private PhonePage _page = PhonePage.Home;
        private string _openContactId;
        private string _openOrderId;
        private readonly List<GameObject> _built = new List<GameObject>();
        private bool _placeholderLogged;

        /// <summary>Поставщик данных телефона.</summary>
        public Func<PhoneSnapshot> DataProvider { get; set; }

        /// <summary>Обработчик действий; возвращает текст ошибки для игрока или null.</summary>
        public Func<string, string> ActionHandler { get; set; }

        /// <summary>Построение маршрута к заказу; возвращает сообщение игроку или null.</summary>
        public Func<string, string> RouteHandler { get; set; }

        public PhonePage Page => _page;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        // ------------------------------------------------------------------ создание

        private void Awake()
        {
            _sprites = new UiSpriteCache();
            BuildShell();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_sprites != null) _sprites.Dispose();
        }

        private void BuildShell()
        {
            _canvas = UiBuilder.CreateCanvas("Phone Canvas", transform, 200);
            _root = (RectTransform)_canvas.transform;

            Image dim = UiBuilder.Image("Dim", _root, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.45f), null, true);
            dim.raycastTarget = true;

            float height = Mathf.Clamp(heightRatio, 0.4f, 1f);
            float width = height * (ReferenceBodyWidth / ReferenceBodyHeight) * (1080f / 1920f);
            float cx = 0.5f + horizontalOffset;
            float x0 = cx - width * 0.5f;
            float x1 = cx + width * 0.5f;
            float y0 = (1f - height) * 0.5f;
            float y1 = y0 + height;

            RectTransform body = UiBuilder.Rect("Phone body", _root, x0, y0, x1, y1);
            Image frame = UiBuilder.Fill(body, UiTheme.PhoneBezel, _sprites.Rounded(40));
            frame.raycastTarget = true;

            RectTransform screen = UiBuilder.Rect("Screen", body, 0.035f, 0.012f, 0.965f, 0.988f);
            UiBuilder.Fill(screen, UiTheme.PhoneBody, _sprites.Rounded(34)).raycastTarget = true;

            _clockLabel = UiBuilder.Label("Clock", screen, "18:42", 0.07f, 0.950f, 0.45f, 0.998f,
                UiTheme.Px(15f), UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiBuilder.Label("Status", screen, "▮▮▮  82%", 0.60f, 0.950f, 0.93f, 0.998f,
                UiTheme.Px(13f), UiTheme.PhoneTextMuted, TextAnchor.MiddleRight);

            _backButton = UiBuilder.PillButton("Back", screen, _sprites, "‹", 0.02f, 0.880f, 0.14f, 0.938f,
                new Color(0f, 0f, 0f, 0f), UiTheme.TextPrimary, OnBackPressed, 8,
                UiTheme.Px(32f));

            _titleLabel = UiBuilder.Label("Title", screen, "Телефон", 0.16f, 0.880f, 0.96f, 0.938f,
                UiTheme.Px(24f), UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);

            _content = UiBuilder.Rect("Content", screen, 0.03f, 0.045f, 0.97f, 0.868f);
        }

        // ------------------------------------------------------------------ видимость

        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            bool was = _root.gameObject.activeSelf;
            _root.gameObject.SetActive(visible);

            if (!visible)
            {
                _page = PhonePage.Home;
                _openContactId = null;
                _openOrderId = null;
                ClearBuilt();
                return;
            }

            if (!was || _built.Count == 0) Refresh();
        }

        /// <summary>Перечитать данные и перерисовать.</summary>
        public void Refresh()
        {
            if (DataProvider != null)
            {
                try
                {
                    PhoneSnapshot fresh = DataProvider();
                    if (fresh != null) _data = fresh;
                }
                catch (Exception exception)
                {
                    Debug.LogError("[PhoneScreen] Источник данных телефона выбросил исключение: " + exception.Message);
                    ShowToast("Ошибка данных телефона.");
                }
            }
            Render();
        }

        public void ShowPage(PhonePage page)
        {
            _page = page;
            Render();
        }

        public void OpenOrder(string orderId)
        {
            _openOrderId = orderId;
            _page = PhonePage.Order;
            Render();
        }

        /// <summary>Enter/E на телефоне: действие страницы.</summary>
        public void Confirm()
        {
            switch (_page)
            {
                case PhonePage.Order:
                    RunPrimaryOrderAction();
                    break;
                case PhonePage.Home:
                    ShowPage(PhonePage.Messages);
                    break;
                case PhonePage.Call:
                    if (_data.Call != null && _data.Call.CanAccept) RunCallAction(PhoneActions.CallAccept);
                    else if (_data.Call != null && _data.Call.CanHangUp) RunCallAction(PhoneActions.CallDecline);
                    break;
            }
        }

        private void OnBackPressed()
        {
            switch (_page)
            {
                case PhonePage.Home:
                    if (UiRouter.Instance != null) UiRouter.Instance.RequestIntent(UiIntent.Back);
                    break;
                case PhonePage.Conversation:
                    _openContactId = null;
                    ShowPage(PhonePage.Messages);
                    break;
                case PhonePage.Order:
                    _openOrderId = null;
                    ShowPage(PhonePage.Orders);
                    break;
                case PhonePage.Call:
                    ShowPage(PhonePage.Home);
                    break;
                default:
                    ShowPage(PhonePage.Home);
                    break;
            }
        }

        private void ClearBuilt()
        {
            for (int i = 0; i < _built.Count; i++)
            {
                if (_built[i] != null) UiBuilder.Destroy(_built[i]);
            }
            _built.Clear();
        }

        private RectTransform NewLayer(string name)
        {
            RectTransform rect = UiBuilder.Rect(name, _content, 0f, 0f, 1f, 1f);
            _built.Add(rect.gameObject);
            return rect;
        }

        private void Track(GameObject go) => _built.Add(go);

        // ------------------------------------------------------------------ отрисовка

        private void Render()
        {
            if (_content == null || !IsVisible) return;
            ClearBuilt();

            _clockLabel.text = string.IsNullOrEmpty(_data.LockTime) || _data.LockTime == UiFormat.Dash
                ? "—"
                : _data.LockTime;
            _backButton.gameObject.SetActive(_page != PhonePage.Home);

            switch (_page)
            {
                case PhonePage.Home: RenderHome(); break;
                case PhonePage.Messages: RenderMessages(); break;
                case PhonePage.Conversation: RenderConversation(); break;
                case PhonePage.Orders: RenderOrders(); break;
                case PhonePage.Order: RenderOrderDetails(); break;
                case PhonePage.Bank: RenderBank(); break;
                case PhonePage.Tasks: RenderTasks(); break;
                case PhonePage.Call: RenderCall(); break;
            }
        }

        private void SetTitle(string title)
        {
            if (_titleLabel != null) _titleLabel.text = title;
        }

        // --- рабочий стол -----------------------------------------------------

        private void RenderHome()
        {
            SetTitle(string.IsNullOrEmpty(_data.LockDate) ? "Телефон" : _data.LockDate);
            RectTransform root = NewLayer("Home");

            // Что показать на рабочем столе — решает UI/Core (PhoneHomeState), а не экран:
            // так правило проверяется тестами. Если данных нет, вместо пустого экрана
            // показываются неактивные плитки + честное сообщение (ТЗ №045).
            PhoneHomeContent home = PhoneHomeState.Resolve(_data.Apps, DataProvider != null);
            IReadOnlyList<PhoneAppTile> ordered = home.Tiles;

            if (home.Placeholder && !_placeholderLogged)
            {
                _placeholderLogged = true;
                if (DataProvider == null)
                {
                    Debug.LogWarning("[PhoneScreen] Рабочий стол без данных: IPhoneDataSource не подключён. " +
                                     "Плитки телефона приходят из PhoneSnapshot.Apps (PhoneFacadeUiBridge / " +
                                     "DeliveryServiceBridge), а не из PhoneAppRegistry.CreateDefault().");
                }
                else
                {
                    Debug.LogWarning("[PhoneScreen] Источник данных телефона вернул пустой список приложений " +
                                     "(PhoneSnapshot.Apps): рабочий стол показывает заглушки.");
                }
            }

            if (home.IsEmpty)
            {
                UiBuilder.Label("Empty", root, home.Notice ?? PhoneHomeState.NoDataSourceNotice,
                    0.04f, 0.40f, 0.96f, 0.64f, UiTheme.Px(16f), UiTheme.PhoneTextMuted,
                    TextAnchor.MiddleCenter);
                return;
            }

            const float cellHeight = 0.190f;
            const float gridTop = 0.985f;
            for (int i = 0; i < ordered.Count && i < 20; i++)
            {
                PhoneAppTile app = ordered[i];
                int row = i / GridColumns;
                int column = i % GridColumns;
                float cellWidth = 1f / GridColumns;
                float x0 = column * cellWidth + 0.030f;
                float x1 = x0 + cellWidth - 0.060f;
                float y1 = gridTop - row * cellHeight;
                float y0 = y1 - cellHeight + 0.020f;

                RectTransform cell = UiBuilder.Rect("Tile " + app.Id, root, x0, y0, x1, y1);
                var button = cell.gameObject.AddComponent<Button>();
                Image hit = UiBuilder.Fill(cell, new Color(0f, 0f, 0f, 0.001f), null, true);
                button.targetGraphic = hit;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                string captured = app.Id;
                button.onClick.AddListener(() => OnAppPressed(captured));

                Color color = AppColor(app.Id);
                Image tile = UiBuilder.Image("Icon", cell, 0.13f, 0.30f, 0.87f, 0.92f, color, _sprites.Rounded(22));
                tile.raycastTarget = false;
                UiBuilder.Icon("Glyph", tile.rectTransform, _sprites, AppIcon(app.Id, app.IconKind),
                    0.22f, 0.22f, 0.78f, 0.78f, new Color(1f, 1f, 1f, 0.95f));

                UiBuilder.Label("Caption", cell, app.Title, 0f, 0.01f, 1f, 0.28f,
                    UiTheme.Px(13f), app.Available ? UiTheme.TextPrimary : UiTheme.TextDisabled,
                    TextAnchor.UpperCenter);

                if (app.Badge > 0)
                {
                    UiBuilder.Badge("Badge", tile.rectTransform, _sprites, PhoneFormat.Badge(app.Badge),
                        0.66f, 0.64f, 1.08f, 1.12f);
                }

                if (!app.Available)
                {
                    UiBuilder.Icon("Lock", tile.rectTransform, _sprites, UiIconKind.Lock,
                        0.36f, 0.36f, 0.64f, 0.64f, new Color(1f, 1f, 1f, 0.85f));
                }
            }

            if (!string.IsNullOrEmpty(home.Notice))
            {
                UiBuilder.Label("Notice", root, home.Notice, 0.04f, 0.128f, 0.96f, 0.200f,
                    UiTheme.Px(12f), UiTheme.PhoneTextMuted, TextAnchor.MiddleCenter);
            }

            // Нижняя панель с быстрыми приложениями (как в референсе).
            Image dock = UiBuilder.Image("Dock", root, 0.05f, 0.005f, 0.95f, 0.115f,
                new Color(0.14f, 0.14f, 0.15f, 0.90f), _sprites.Rounded(18));
            dock.raycastTarget = false;

            string[] dockIds = { PhoneAppIds.Calls, PhoneAppIds.Messages, PhoneAppIds.Bank, PhoneAppIds.Orders };
            for (int i = 0; i < dockIds.Length; i++)
            {
                PhoneAppTile app = FindApp(dockIds[i]);
                float x = 0.02f + i * 0.245f;
                Image slot = UiBuilder.Image("Dock " + dockIds[i], dock.rectTransform, x, 0.08f, x + 0.215f, 0.92f,
                    app != null && app.Available ? AppColor(dockIds[i]) : new Color(0.28f, 0.28f, 0.30f), 
                    _sprites.Rounded(16));
                slot.raycastTarget = false;
                UiBuilder.Icon("Glyph", slot.rectTransform, _sprites,
                    AppIcon(dockIds[i], app != null ? app.IconKind : null),
                    0.25f, 0.25f, 0.75f, 0.75f, new Color(1f, 1f, 1f, 0.95f));
            }
        }

        private PhoneAppTile FindApp(string id)
        {
            string normalized = PhoneAppIds.Normalize(id);
            IReadOnlyList<PhoneAppTile> apps = _data.Apps ?? Array.Empty<PhoneAppTile>();
            for (int i = 0; i < apps.Count; i++)
            {
                if (apps[i].Id == id || PhoneAppIds.Normalize(apps[i].Id) == normalized) return apps[i];
            }
            return null;
        }

        private void OnAppPressed(string appId)
        {
            if (PhoneAppIds.IsForbiddenInPhone(appId)) return;

            PhoneAppTile app = FindApp(appId);
            if (app != null && !app.Available)
            {
                ShowToast("Приложение пока недоступно.");
                return;
            }

            switch (PhoneAppIds.Normalize(appId))
            {
                case PhoneAppIds.Messages:
                    ShowPage(PhonePage.Messages);
                    return;
                case PhoneAppIds.Orders:
                    ShowPage(PhonePage.Orders);
                    return;
                case PhoneAppIds.Bank:
                    ShowPage(PhonePage.Bank);
                    return;
                case PhoneAppIds.Tasks:
                    ShowPage(PhonePage.Tasks);
                    return;
                case PhoneAppIds.Calls:
                    ShowPage(PhonePage.Call);
                    return;
            }

            string error = ActionHandler != null ? ActionHandler.Invoke(PhoneActions.OpenApp + appId) : null;
            if (!string.IsNullOrEmpty(error)) ShowToast(error);
            else ShowToast("Раздел «" + (app != null ? app.Title : appId) + "» открывается в телефоне игрока.");
        }

        // --- сообщения ---------------------------------------------------------

        private void RenderMessages()
        {
            SetTitle("Сообщения");
            RectTransform root = NewLayer("Messages");
            IReadOnlyList<PhoneConversationRow> rows = _data.Conversations ?? Array.Empty<PhoneConversationRow>();

            if (rows.Count == 0)
            {
                UiBuilder.Label("Empty", root, "Переписок пока нет.", 0.05f, 0.44f, 0.95f, 0.58f,
                    UiTheme.Px(16f), UiTheme.PhoneTextMuted, TextAnchor.MiddleCenter);
                return;
            }

            const float rowHeight = 0.150f;
            float y = 0.985f;
            for (int i = 0; i < rows.Count && y - rowHeight > 0f; i++)
            {
                PhoneConversationRow row = rows[i];
                float y0 = y - rowHeight;
                RectTransform item = UiBuilder.Rect("Conversation " + row.ContactId, root, 0.005f, y0, 0.995f,
                    y - 0.010f);
                Image background = UiBuilder.Fill(item, UiTheme.PhoneCard, _sprites.Rounded(14), true);
                var button = item.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                string captured = row.ContactId;
                button.onClick.AddListener(() => OpenConversation(captured));

                Color avatarColor = SeedColor(row.AvatarSeed);
                Image avatar = UiBuilder.Image("Avatar", item, 0.02f, 0.14f, 0.19f, 0.86f, avatarColor,
                    _sprites.Circle());
                avatar.raycastTarget = false;
                UiBuilder.Label("Initial", avatar.rectTransform, Initial(row.Title), 0.10f, 0.10f, 0.90f, 0.90f,
                    UiTheme.Px(20f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

                UiBuilder.Label("Title", item, row.Title, 0.23f, 0.50f, 0.76f, 0.93f,
                    UiTheme.Px(17f), UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiBuilder.Label("Preview", item, Shorten(row.Preview, 32), 0.23f, 0.08f, 0.80f, 0.50f,
                    UiTheme.Px(14f), UiTheme.PhoneTextMuted);
                UiBuilder.Label("Time", item, row.TimeLabel, 0.78f, 0.50f, 0.98f, 0.93f,
                    UiTheme.Px(13f), UiTheme.PhoneTextMuted, TextAnchor.MiddleRight);

                if (row.Unread > 0)
                {
                    UiBuilder.Badge("Unread", item, _sprites, PhoneFormat.Badge(row.Unread),
                        0.84f, 0.12f, 0.97f, 0.46f);
                }

                y = y0;
            }
        }

        private void OpenConversation(string contactId)
        {
            _openContactId = contactId;
            _page = PhonePage.Conversation;

            string error = ActionHandler != null
                ? ActionHandler.Invoke(PhoneActions.ReadConversation + contactId)
                : null;
            Refresh();
            _page = PhonePage.Conversation;
            Render();
            if (!string.IsNullOrEmpty(error)) ShowToast(error);
        }

        private void RenderConversation()
        {
            PhoneConversationRow header = FindConversation(_openContactId);
            SetTitle(header != null ? header.Title : "Сообщения");
            RectTransform root = NewLayer("Conversation");

            IReadOnlyList<PhoneMessageRow> messages = _data.Messages ?? Array.Empty<PhoneMessageRow>();
            if (messages.Count == 0)
            {
                UiBuilder.Label("Empty", root, "Сообщений нет.", 0.05f, 0.44f, 0.95f, 0.58f,
                    UiTheme.Px(16f), UiTheme.PhoneTextMuted, TextAnchor.MiddleCenter);
            }

            const float bubbleHeight = 0.105f;
            float y = 0.985f;
            for (int i = 0; i < messages.Count && y - bubbleHeight > 0.16f; i++)
            {
                PhoneMessageRow message = messages[i];
                float width = Mathf.Clamp(0.22f + message.Text.Length * 0.011f, 0.32f, 0.74f);
                bool outgoing = !message.Incoming;
                float x0 = outgoing ? 1f - width : 0f;
                float x1 = x0 + width;
                float y0 = y - bubbleHeight;

                Image bubble = UiBuilder.Image("Bubble", root, x0, y0, x1, y - 0.012f,
                    outgoing ? new Color(0.20f, 0.37f, 0.30f, 0.96f) : UiTheme.PhoneCardLight,
                    _sprites.Rounded(14));
                bubble.raycastTarget = false;
                UiBuilder.Label("Text", bubble.rectTransform, message.Text, 0.06f, 0.24f, 0.94f, 0.92f,
                    UiTheme.Px(15f), UiTheme.TextPrimary, TextAnchor.UpperLeft);
                UiBuilder.Label("Time", bubble.rectTransform, message.TimeLabel, 0.55f, 0.02f, 0.95f, 0.24f,
                    UiTheme.Px(12f), UiTheme.PhoneTextMuted, TextAnchor.MiddleRight);

                y = y0;
            }

            Image reply = UiBuilder.Image("ReplyBar", root, 0f, 0.015f, 1f, 0.135f,
                new Color(0.16f, 0.16f, 0.17f, 0.95f), _sprites.Rounded(20));
            reply.raycastTarget = false;
            UiBuilder.Label("ReplyHint", reply.rectTransform, "Ответить — через диалог", 0.06f, 0.10f, 0.88f, 0.90f,
                UiTheme.Px(15f), UiTheme.PhoneTextMuted);
            UiBuilder.Icon("Send", reply.rectTransform, _sprites, UiIconKind.Send, 0.88f, 0.24f, 0.96f, 0.76f,
                UiTheme.PhoneAccent);
        }

        private PhoneConversationRow FindConversation(string contactId)
        {
            if (string.IsNullOrEmpty(contactId)) return null;
            IReadOnlyList<PhoneConversationRow> rows = _data.Conversations ?? Array.Empty<PhoneConversationRow>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].ContactId == contactId) return rows[i];
            }
            return null;
        }

        // --- заказы ------------------------------------------------------------

        private void RenderOrders()
        {
            SetTitle("Заказы");
            RectTransform root = NewLayer("Orders");

            OrdersView orders = _data.Orders ?? new OrdersView();
            if (!string.IsNullOrEmpty(orders.UnavailableReason))
            {
                UiBuilder.Label("Unavailable", root, orders.UnavailableReason, 0.05f, 0.40f, 0.95f, 0.62f,
                    UiTheme.Px(15f), UiTheme.PhoneTextMuted, TextAnchor.MiddleCenter);
                return;
            }

            string earned = orders.EarnedTodayRubles.HasValue
                ? UiFormat.Rubles(orders.EarnedTodayRubles)
                : UiFormat.Dash;
            string rating = orders.Rating.HasValue
                ? orders.Rating.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                : UiFormat.Dash;

            UiBuilder.Label("Summary", root,
                "Выполнено: " + orders.CompletedToday + "   Заработано: " + earned + "   Рейтинг: " + rating,
                0f, 0.930f, 1f, 0.990f, UiTheme.Px(13f), UiTheme.PhoneTextMuted);

            float y = 0.915f;
            const float activeHeight = 0.215f;

            if (orders.Active != null)
            {
                RenderOrderCard(root, orders.Active, y - activeHeight, y - 0.008f, true);
                y -= activeHeight + 0.012f;
            }

            IReadOnlyList<OrderView> available = orders.Available ?? Array.Empty<OrderView>();
            if (available.Count > 0)
            {
                UiBuilder.Label("AvailableCaption", root, "Доступные заказы", 0f, y - 0.055f, 1f, y,
                    UiTheme.Px(14f), UiTheme.PhoneTextMuted);
                y -= 0.060f;
            }
            else if (orders.Active == null)
            {
                UiBuilder.Label("Empty", root, "Активных заказов нет.\nНовые появятся в течение дня.",
                    0.05f, 0.42f, 0.95f, 0.62f, UiTheme.Px(15f), UiTheme.PhoneTextMuted,
                    TextAnchor.MiddleCenter);
            }

            const float cardHeight = 0.165f;
            for (int i = 0; i < available.Count && y - cardHeight > 0f; i++)
            {
                RenderOrderCard(root, available[i], y - cardHeight, y - 0.006f, false);
                y -= cardHeight + 0.008f;
            }
        }

        private void RenderOrderCard(RectTransform root, OrderView order, float bottom, float top, bool active)
        {
            RectTransform card = UiBuilder.Rect("Order " + order.OrderId, root, 0.005f, bottom, 0.995f, top);
            Image background = UiBuilder.Fill(card,
                active ? new Color(0.16f, 0.14f, 0.10f, 0.96f) : UiTheme.PhoneCard, _sprites.Rounded(14), true);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            string captured = order.OrderId;
            button.onClick.AddListener(() =>
            {
                _openOrderId = captured;
                ShowPage(PhonePage.Order);
            });

            bool canRoute;
            string reason;
            canRoute = order.CanRoute(out reason);

            UiBuilder.Label("Title", card, string.IsNullOrEmpty(order.CargoTitle) ? "Заказ" : order.CargoTitle,
                0.04f, 0.60f, 0.70f, 0.95f, UiTheme.Px(16f), UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UiBuilder.Label("Destination", card,
                string.IsNullOrEmpty(order.DropOffTitle) ? UiFormat.Dash : order.DropOffTitle,
                0.04f, 0.32f, 0.70f, 0.62f, UiTheme.Px(14f), UiTheme.PhoneTextMuted);
            UiBuilder.Label("Deadline", card, order.DeadlineLabel, 0.04f, 0.05f, 0.70f, 0.32f,
                UiTheme.Px(13f), UiTheme.PhoneTextMuted);

            UiBuilder.Label("Payment", card,
                order.PaymentRubles.HasValue ? UiFormat.Rubles(order.PaymentRubles) : UiFormat.Dash,
                0.70f, 0.58f, 0.97f, 0.95f, UiTheme.Px(18f), UiTheme.Positive,
                TextAnchor.MiddleRight, FontStyle.Bold);

            UiBuilder.Label("Stage", card, StageLabel(order.Stage), 0.70f, 0.30f, 0.97f, 0.58f,
                UiTheme.Px(13f), canRoute ? UiTheme.PhoneAccent : UiTheme.PhoneTextMuted,
                TextAnchor.MiddleRight);
        }

        private void RenderOrderDetails()
        {
            OrderView order = FindOrder(_openOrderId);
            SetTitle(order != null && !string.IsNullOrEmpty(order.CargoTitle) ? order.CargoTitle : "Заказ");
            RectTransform root = NewLayer("OrderDetails");

            if (order == null)
            {
                UiBuilder.Label("Empty", root, "Заказ не найден или уже закрыт.", 0.05f, 0.42f, 0.95f, 0.60f,
                    UiTheme.Px(16f), UiTheme.PhoneTextMuted, TextAnchor.MiddleCenter);
                return;
            }

            RectTransform card = UiBuilder.Rect("Card", root, 0f, 0.30f, 1f, 0.99f);
            UiBuilder.Fill(card, UiTheme.PhoneCard, _sprites.Rounded(16));

            UiBuilder.Label("Stage", card, StageLabel(order.Stage), 0.05f, 0.90f, 0.95f, 0.99f,
                UiTheme.Px(14f), UiTheme.PhoneAccent);

            OrderRow(card, "Куда", order.DropOffTitle, 0.78f);
            OrderRow(card, "Адрес", order.Address, 0.66f);
            OrderRow(card, "Срок", order.DeadlineLabel, 0.54f);
            OrderRow(card, "Оплата",
                order.PaymentRubles.HasValue ? UiFormat.Rubles(order.PaymentRubles) : UiFormat.Dash, 0.42f);
            OrderRow(card, "Координаты",
                order.HasDropOffCoordinates ? "есть" : "нет — маршрут недоступен", 0.30f);

            bool canRoute;
            string reason;
            canRoute = order.CanRoute(out reason);

            // Кнопка «Маршрут» — единственный путь к навигации из телефона (ТЗ §4.8).
            Button routeButton = UiBuilder.PillButton("Route", root, _sprites, "Построить маршрут",
                0.02f, 0.175f, 0.98f, 0.285f,
                canRoute ? new Color(0.22f, 0.50f, 0.86f, 0.98f) : new Color(0.20f, 0.20f, 0.22f, 0.98f),
                canRoute ? Color.white : UiTheme.TextDisabled,
                () => OnRoutePressed(order.OrderId), 12, UiTheme.Px(18f));
            routeButton.interactable = canRoute;

            if (!canRoute && !string.IsNullOrEmpty(reason))
            {
                UiBuilder.Label("RouteReason", root, reason, 0.02f, 0.120f, 0.98f, 0.170f,
                    UiTheme.Px(13f), UiTheme.PhoneTextMuted, TextAnchor.MiddleCenter);
            }

            string[] actions = ActionsForStage(order.Stage);
            if (actions.Length == 0) return;

            float width = 1f / actions.Length;
            for (int i = 0; i < actions.Length; i++)
            {
                string actionId = actions[i];
                float x0 = i * width + 0.015f;
                float x1 = x0 + width - 0.030f;
                bool primary = i == actions.Length - 1;
                UiBuilder.PillButton("Action " + actionId, root, _sprites, StageActionLabel(actionId),
                    x0, 0.015f, x1, 0.110f,
                    primary ? new Color(0.86f, 0.62f, 0.16f, 0.98f) : new Color(0.20f, 0.20f, 0.22f, 0.98f),
                    primary ? new Color(0.08f, 0.08f, 0.08f) : UiTheme.TextPrimary,
                    () => RunOrderAction(order.OrderId, actionId), 12, UiTheme.Px(15f));
            }
        }

        private void OrderRow(RectTransform parent, string caption, string value, float v)
        {
            UiBuilder.Label("Caption " + caption, parent, caption, 0.05f, v, 0.36f, v + 0.105f,
                UiTheme.Px(14f), UiTheme.PhoneTextMuted);
            UiBuilder.Label("Value " + caption, parent,
                string.IsNullOrEmpty(value) ? UiFormat.Dash : value, 0.36f, v, 0.95f, v + 0.105f,
                UiTheme.Px(15f), UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private OrderView FindOrder(string orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return null;
            OrdersView orders = _data.Orders;
            if (orders == null) return null;
            if (orders.Active != null && orders.Active.OrderId == orderId) return orders.Active;

            IReadOnlyList<OrderView> available = orders.Available ?? Array.Empty<OrderView>();
            for (int i = 0; i < available.Count; i++)
            {
                if (available[i].OrderId == orderId) return available[i];
            }
            return null;
        }

        private static string[] ActionsForStage(OrderStage stage)
        {
            switch (stage)
            {
                case OrderStage.Available:
                    return new[] { PhoneActions.OrderAccept };
                case OrderStage.Accepted:
                    return new[] { PhoneActions.OrderAbandon, PhoneActions.OrderPickUp };
                case OrderStage.PickedUp:
                    return new[] { PhoneActions.OrderAbandon, PhoneActions.OrderComplete };
                case OrderStage.None:
                    return Array.Empty<string>();
                default:
                    return Array.Empty<string>();
            }
        }

        private static string StageActionLabel(string actionId)
        {
            if (actionId == PhoneActions.OrderAccept) return "Принять";
            if (actionId == PhoneActions.OrderPickUp) return "Забрать";
            if (actionId == PhoneActions.OrderComplete) return "Доставить";
            if (actionId == PhoneActions.OrderCancel) return "Отменить";
            if (actionId == PhoneActions.OrderAbandon) return "Отказаться";
            if (actionId == PhoneActions.OrderRoute) return "Маршрут";
            return actionId;
        }

        private static string StageLabel(OrderStage stage)
        {
            switch (stage)
            {
                case OrderStage.Available: return "Доступен";
                case OrderStage.Accepted: return "Принят";
                case OrderStage.PickedUp: return "В пути";
                case OrderStage.Completed: return "Доставлен";
                case OrderStage.Failed: return "Провален";
                case OrderStage.Cancelled: return "Отменён";
                default: return UiFormat.Dash;
            }
        }

        private void RunPrimaryOrderAction()
        {
            OrderView order = FindOrder(_openOrderId);
            if (order == null) return;
            string[] actions = ActionsForStage(order.Stage);
            if (actions.Length == 0) return;
            RunOrderAction(order.OrderId, actions[actions.Length - 1]);
        }

        private void OnRoutePressed(string orderId)
        {
            string message = RouteHandler != null ? RouteHandler.Invoke(orderId) : "Маршрут недоступен.";
            if (!string.IsNullOrEmpty(message))
            {
                // При ошибке построения маршрута телефон остаётся открытым, чтобы игрок увидел причину.
                ShowToast(message);
                return;
            }
            if (UiRouter.Instance != null) UiRouter.Instance.RequestIntent(UiIntent.Back);
        }

        private void RunOrderAction(string orderId, string actionId)
        {
            if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(actionId)) return;

            if (actionId == PhoneActions.OrderRoute)
            {
                OnRoutePressed(orderId);
                return;
            }

            string message = ActionHandler != null ? ActionHandler.Invoke(actionId + orderId) : null;
            Refresh();
            if (_page == PhonePage.Order)
            {
                OrderView updated = FindOrder(orderId);
                if (updated == null) ShowPage(PhonePage.Orders);
            }
            if (!string.IsNullOrEmpty(message)) ShowToast(message);
        }

        // --- банк --------------------------------------------------------------

        private void RenderBank()
        {
            SetTitle("Банк");
            RectTransform root = NewLayer("Bank");
            BankView bank = _data.Bank ?? new BankView();

            RectTransform balance = UiBuilder.Rect("Balance", root, 0f, 0.700f, 1f, 0.990f);
            UiBuilder.Fill(balance, new Color(0.20f, 0.10f, 0.10f, 0.96f), _sprites.Rounded(16));
            UiBuilder.Label("Caption", balance, "Доступно", 0.06f, 0.66f, 0.94f, 0.94f,
                UiTheme.Px(14f), UiTheme.PhoneTextMuted);
            UiBuilder.Label("Amount", balance,
                bank.Balance.HasValue ? UiFormat.Rubles(bank.Balance) : UiFormat.Missing,
                0.06f, 0.14f, 0.94f, 0.66f, UiTheme.Px(34f), UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);

            RectTransform debts = UiBuilder.Rect("Debts", root, 0f, 0.575f, 1f, 0.690f);
            UiBuilder.Fill(debts, UiTheme.PhoneCard, _sprites.Rounded(14));
            UiBuilder.Label("DebtCaption", debts, "Задолженность", 0.06f, 0.10f, 0.55f, 0.90f,
                UiTheme.Px(14f), UiTheme.PhoneTextMuted);
            UiBuilder.Label("DebtValue", debts,
                bank.Debt.HasValue ? UiFormat.Rubles(bank.Debt) : UiFormat.Missing,
                0.55f, 0.10f, 0.94f, 0.90f, UiTheme.Px(20f),
                bank.Debt.HasValue && bank.Debt.Value > 0 ? UiTheme.Debt : UiTheme.Positive,
                TextAnchor.MiddleRight, FontStyle.Bold);

            RectTransform income = UiBuilder.Rect("Income", root, 0f, 0.455f, 1f, 0.560f);
            UiBuilder.Fill(income, UiTheme.PhoneCard, _sprites.Rounded(14));
            UiBuilder.Label("IncomeCaption", income, "Доход за сутки", 0.06f, 0.10f, 0.55f, 0.90f,
                UiTheme.Px(14f), UiTheme.PhoneTextMuted);
            UiBuilder.Label("IncomeValue", income,
                bank.DailyIncome.HasValue ? UiFormat.SignedRubles(bank.DailyIncome) : UiFormat.Missing,
                0.55f, 0.10f, 0.94f, 0.90f, UiTheme.Px(20f), UiTheme.Positive,
                TextAnchor.MiddleRight, FontStyle.Bold);

            IReadOnlyList<BankOperationRow> operations = bank.Operations ?? Array.Empty<BankOperationRow>();
            if (!bank.OperationsAvailable)
            {
                UiBuilder.Label("OpsUnavailable", root, "Выписка недоступна.", 0.04f, 0.20f, 0.96f, 0.30f,
                    UiTheme.Px(13f), UiTheme.PhoneTextMuted, TextAnchor.MiddleCenter);
                return;
            }

            float y = 0.440f;
            for (int i = 0; i < operations.Count && y - 0.070f > 0f; i++)
            {
                BankOperationRow operation = operations[i];
                UiBuilder.Label("OpTitle " + i, root, operation.Title, 0.04f, y - 0.065f, 0.62f, y,
                    UiTheme.Px(14f), UiTheme.TextPrimary);
                UiBuilder.Label("OpAmount " + i, root, operation.Amount, 0.62f, y - 0.065f, 0.96f, y,
                    UiTheme.Px(14f),
                    operation.Positive ? UiTheme.Positive : UiTheme.Debt, TextAnchor.MiddleRight);
                y -= 0.072f;
            }
        }

        // --- задания -----------------------------------------------------------

        private void RenderTasks()
        {
            SetTitle("Задания");
            RectTransform root = NewLayer("Tasks");
            IReadOnlyList<StoryEntryRow> tasks = _data.Tasks ?? Array.Empty<StoryEntryRow>();

            if (tasks.Count == 0)
            {
                UiBuilder.Label("Empty", root, "Новых записей нет.", 0.05f, 0.44f, 0.95f, 0.58f,
                    UiTheme.Px(16f), UiTheme.PhoneTextMuted, TextAnchor.MiddleCenter);
                return;
            }

            float y = 0.985f;
            const float height = 0.185f;
            for (int i = 0; i < tasks.Count && y - height > 0f; i++)
            {
                StoryEntryRow task = tasks[i];
                float y0 = y - height;
                RectTransform card = UiBuilder.Rect("Task " + i, root, 0.005f, y0, 0.995f, y - 0.010f);
                UiBuilder.Fill(card, UiTheme.PhoneCard, _sprites.Rounded(14));

                UiBuilder.Icon("State", card, _sprites,
                    task.Done ? UiIconKind.CheckDone : UiIconKind.CheckEmpty,
                    0.03f, 0.70f, 0.11f, 0.93f,
                    task.Done ? UiTheme.Positive : UiTheme.PhoneTextMuted);
                UiBuilder.Label("Title", card, task.Title, 0.13f, 0.68f, 0.95f, 0.95f,
                    UiTheme.Px(16f), task.Done ? UiTheme.TextMuted : UiTheme.TextPrimary,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                UiBuilder.Label("Body", card, task.Body, 0.04f, 0.24f, 0.95f, 0.68f,
                    UiTheme.Px(13f), UiTheme.PhoneTextMuted);
                UiBuilder.Label("Progress", card, task.Progress, 0.04f, 0.02f, 0.95f, 0.24f,
                    UiTheme.Px(12f), UiTheme.PhoneAccent);

                y = y0;
            }
        }

        // --- звонок ------------------------------------------------------------

        private void RenderCall()
        {
            CallView call = _data.Call;
            SetTitle("Звонок");
            RectTransform root = NewLayer("Call");

            if (call == null)
            {
                UiBuilder.Label("Empty", root, "Нет активного звонка.", 0.05f, 0.44f, 0.95f, 0.58f,
                    UiTheme.Px(16f), UiTheme.PhoneTextMuted, TextAnchor.MiddleCenter);
                return;
            }

            Image avatar = UiBuilder.Image("Avatar", root, 0.30f, 0.62f, 0.70f, 0.92f,
                SeedColor(call.AvatarSeed), _sprites.Circle());
            avatar.raycastTarget = false;
            UiBuilder.Label("Initial", avatar.rectTransform, Initial(call.ContactTitle), 0.10f, 0.10f, 0.90f, 0.90f,
                UiTheme.Px(32f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            UiBuilder.Label("Name", root, call.ContactTitle, 0.05f, 0.50f, 0.95f, 0.60f,
                UiTheme.Px(22f), UiTheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiBuilder.Label("Subtitle", root, call.Subtitle, 0.05f, 0.42f, 0.95f, 0.50f,
                UiTheme.Px(15f), UiTheme.PhoneTextMuted, TextAnchor.MiddleCenter);

            if (call.CanAccept)
            {
                UiBuilder.PillButton("Accept", root, _sprites, "Принять", 0.10f, 0.20f, 0.48f, 0.32f,
                    UiTheme.PhoneGreen, Color.white, () => RunCallAction(PhoneActions.CallAccept), 30,
                    UiTheme.Px(16f));
            }

            if (call.CanDecline || call.CanHangUp)
            {
                UiBuilder.PillButton("HangUp", root, _sprites, call.CanHangUp ? "Завершить" : "Отклонить",
                    0.52f, 0.20f, 0.90f, 0.32f, UiTheme.PhoneRedButton, Color.white,
                    () => RunCallAction(PhoneActions.CallDecline), 30, UiTheme.Px(16f));
            }
        }

        private void RunCallAction(string actionId)
        {
            string error = ActionHandler != null ? ActionHandler.Invoke(actionId) : null;
            Refresh();
            if (!string.IsNullOrEmpty(error)) ShowToast(error);
            else if (_data.Call == null) ShowPage(PhonePage.Home);
        }

        // --- вспомогательное ---------------------------------------------------

        private void ShowToast(string message)
        {
            if (_content == null || string.IsNullOrEmpty(message)) return;
            RectTransform toast = UiBuilder.Rect("Toast", _content, 0.02f, 0.010f, 0.98f, 0.140f);
            Track(toast.gameObject);
            Image background = UiBuilder.Fill(toast, new Color(0.10f, 0.10f, 0.11f, 0.98f), _sprites.Rounded(14));
            background.raycastTarget = false;
            UiBuilder.Label("Text", toast, message, 0.05f, 0.08f, 0.95f, 0.92f,
                UiTheme.Px(14f), UiTheme.PhoneAccent, TextAnchor.MiddleCenter);
        }

        private static string Initial(string value)
        {
            if (string.IsNullOrEmpty(value)) return "?";
            return value.Substring(0, 1).ToUpperInvariant();
        }

        private static string Shorten(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }

        private static Color SeedColor(int seed)
        {
            int hue = Mathf.Abs(seed * 47 + 13) % 360;
            return Color.HSVToRGB(hue / 360f, 0.42f, 0.60f);
        }

        private static Color AppColor(string appId)
        {
            switch (appId)
            {
                case PhoneAppIds.Calls: return UiTheme.AppCalls;
                case PhoneAppIds.Messages: return UiTheme.AppMessages;
                case PhoneAppIds.Contacts: return UiTheme.AppContacts;
                case PhoneAppIds.Bank: return UiTheme.AppBank;
                case PhoneAppIds.Orders: return UiTheme.AppOrders;
                case PhoneAppIds.Tasks: return UiTheme.AppTasks;
                case PhoneAppIds.Ads: return UiTheme.AppAds;
                case PhoneAppIds.Education: return UiTheme.AppEducation;
                case PhoneAppIds.Gallery: return UiTheme.AppGallery;
                default: return UiTheme.AppSettings;
            }
        }

        private static UiIconKind AppIcon(string appId, string iconKind)
        {
            switch (appId)
            {
                case PhoneAppIds.Calls: return UiIconKind.Phone;
                case PhoneAppIds.Messages: return UiIconKind.Message;
                case PhoneAppIds.Contacts: return UiIconKind.ContactsBook;
                case PhoneAppIds.Bank: return UiIconKind.Wallet;
                case PhoneAppIds.Orders: return UiIconKind.Box;
                case PhoneAppIds.Tasks: return UiIconKind.Tasks;
                case PhoneAppIds.Ads: return UiIconKind.Megaphone;
                case PhoneAppIds.Education: return UiIconKind.Graduation;
                case PhoneAppIds.Gallery: return UiIconKind.Image;
                case PhoneAppIds.Settings: return UiIconKind.Settings;
            }

            if (!string.IsNullOrEmpty(iconKind))
            {
                UiIconKind parsed;
                if (Enum.TryParse(iconKind, true, out parsed)) return parsed;
            }
            return UiIconKind.Settings;
        }
    }
}
