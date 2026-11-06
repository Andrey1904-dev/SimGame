// Life From Zero — UI/Runtime (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// Меню паузы — ПОЛНАЯ переделка `City/Pause/PauseView.cs`.
//
// Что не так со старым меню и что сделано иначе:
//   * старый PauseView рисовал 1920×1080 «на глаз» и съезжал на нестандартных
//     разрешениях. Здесь вся геометрия берётся из LifeFromZero.UI.Core.PauseLayout,
//     который измерен по `UI/References/Pause Menu.PNG` и проверяется тестом
//     (панель за кадром, пересечение кнопок, кегль меньше 18 px — падение теста);
//   * старые кнопки «Сохранить/Загрузить/Настройки» были картинками. Здесь это
//     настоящие Button с колбэком на UiWorldAdapter: недоступное действие гасится,
//     а не притворяется работающим;
//   * старый текст (12 450 ₽, «День 3») был захардкожен. Здесь всё приходит из
//     UiSnapshot; нет данных — пишется «Нет данных», а не ноль;
//   * своей паузы/времени здесь нет: состояние ведёт UiStateMachine, ввод — UiRouter,
//     остановку времени и курсор — один UiRouter, а не каждый экран по-своему.
//
// Масштабирование: CanvasScaler 1920×1080, режим Expand — 16:9, 21:9 и 1440p
// получают ту же раскладку без «догоняющих» правок.

using System;
using System.Collections.Generic;
using LifeFromZero.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Идентификаторы действий меню паузы (уходят в UiWorldAdapter).</summary>
    public static class PauseActions
    {
        public const string Resume = "resume";
        public const string Save = "save";
        public const string Load = "load";
        public const string Settings = "settings";
        public const string Controls = "controls";
        public const string MainMenu = "main-menu";
        public const string Quit = "quit";
        public const string Phone = "phone";
        public const string Inventory = "inventory";
        public const string Map = "map";
        public const string Tasks = "tasks";
    }

    /// <summary>Строка меню паузы: кнопка, подпись, иконка и признак «всегда доступна».</summary>
    public sealed class PauseMenuEntry
    {
        public Button Button;
        public Text Label;
        public Image Icon;
        public string Action;
        public bool AlwaysEnabled;
    }

    /// <summary>Меню паузы. Один Canvas, один экран, данные — из UiSnapshot.</summary>
    public sealed class PauseMenuScreen : MonoBehaviour
    {
        private const int SortingOrder = 300;

        private static readonly LayoutRect Frame = new LayoutRect(0f, 0f, PauseLayout.FrameWidth, PauseLayout.FrameHeight);

        private static readonly string[] MenuActions =
        {
            PauseActions.Resume, PauseActions.Save, PauseActions.Load, PauseActions.Settings,
            PauseActions.Controls, PauseActions.MainMenu, PauseActions.Quit,
        };

        private static readonly UiIconKind[] MenuIcons =
        {
            UiIconKind.Play, UiIconKind.Save, UiIconKind.Load, UiIconKind.Settings,
            UiIconKind.Controls, UiIconKind.Home, UiIconKind.Power,
        };

        private static readonly string[] StatusNames = { "Здоровье", "Сытость", "Энергия", "Настроение" };

        private static readonly UiIconKind[] StatusIcons =
        {
            UiIconKind.Heart, UiIconKind.ForkKnife, UiIconKind.Bolt, UiIconKind.Smile,
        };

        private static readonly Color[] StatusIconColors =
        {
            UiTheme.IconHeart, UiTheme.IconFork, UiTheme.IconBolt, UiTheme.IconSmile,
        };

        private static readonly Color[] StatusBarColors =
        {
            UiTheme.BarHealth, UiTheme.BarSatiety, UiTheme.BarEnergy, UiTheme.BarMood,
        };

        private Canvas _canvas;
        private RectTransform _root;
        private UiSpriteCache _sprites;

        private readonly List<PauseMenuEntry> _entries = new List<PauseMenuEntry>();
        private readonly List<Button> _quickLinks = new List<Button>();
        private readonly List<Text> _percentLabels = new List<Text>();

        private Text _dateLabel;
        private Text _timeLabel;
        private Text _locationLabel;
        private Text _locationDetailLabel;
        private RectTransform _locationBlock;

        private RectTransform _goalsContent;
        private Text _goalsEmpty;
        private readonly Image[] _bars = new Image[4];
        private readonly Image[] _barIcons = new Image[4];

        private Text _moneyValue;
        private Text _debtValue;
        private Text _incomeValue;

        private UiSnapshot _snapshot = new UiSnapshot();
        private NavigationService _navigation;
        private UiWorldAdapter _world;
        private UiRouter _router;
        private int _selected;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>Подмена обработчика намерений (используется тестами и роутером).</summary>
        public Func<UiIntent, UiTransition> IntentHandler { get; set; }

        // ------------------------------------------------------------------ создание

        private void Awake()
        {
            _sprites = new UiSpriteCache();
            Build();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_sprites != null) _sprites.Dispose();
        }

        private void Build()
        {
            _canvas = UiBuilder.CreateCanvas("Pause Canvas", transform, SortingOrder);
            _root = (RectTransform)_canvas.transform;

            // Затемнение: игра видна, но не мешает читать панели.
            UiBuilder.Fill(UiBuilder.Rect("Scrim", _root, 0f, 0f, 1f, 1f), new Color(0f, 0f, 0f, 0.55f), null, true);

            BuildLeftPanel();
            BuildRightPanel();
            BuildFooter();
        }

        private void BuildLeftPanel()
        {
            Image panel = UiBuilder.ImageAt("Left panel", _root, PauseLayout.LeftPanel, Frame,
                UiTheme.PausePanel, _sprites.Rounded(14), true);

            // Логотип: рисуем текст, а не вставляем картинку (ТЗ §1 — только uGUI).
            UiBuilder.LabelAt("Logo", panel.rectTransform, PauseLayout.Logo, PauseLayout.LeftPanel,
                "LIFE FROM", PauseLayout.FontLogo, UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.BoldItalic);
            var red = PauseLayout.Inset(PauseLayout.Logo, 168f, 0f, 0f, 0f);
            UiBuilder.LabelAt("LogoRed", panel.rectTransform, red, PauseLayout.LeftPanel,
                "ZERO", PauseLayout.FontLogo, UiTheme.AccentRed, TextAnchor.MiddleLeft, FontStyle.BoldItalic);
            var rule = new LayoutRect(PauseLayout.Logo.Left + 52f, PauseLayout.Logo.Bottom - 4f, 275f, 3f);
            UiBuilder.ImageAt("LogoRule", panel.rectTransform, rule, PauseLayout.LeftPanel, UiTheme.AccentRed);

            UiBuilder.LabelAt("Title", panel.rectTransform, PauseLayout.Title, PauseLayout.LeftPanel,
                "Пауза", PauseLayout.FontTitle, UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);

            BuildMenuButtons(panel.rectTransform);
        }

        private void BuildMenuButtons(RectTransform panel)
        {
            for (int i = 0; i < MenuActions.Length; i++)
            {
                LayoutRect buttonRect = PauseLayout.Button(i);
                RectTransform rect = UiBuilder.At("Menu " + MenuActions[i], panel, buttonRect, PauseLayout.LeftPanel);
                Image background = UiBuilder.Fill(rect, UiTheme.PauseButton, _sprites.Rounded(10), true);

                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                colors.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
                colors.selectedColor = Color.white;
                colors.disabledColor = new Color(0.62f, 0.62f, 0.62f, 0.85f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;

                string action = MenuActions[i];
                button.onClick.AddListener(() => OnMenuPressed(action));

                var iconRect = new LayoutRect(PauseLayout.ButtonIconLeft, buttonRect.Top + 20f,
                    PauseLayout.ButtonIconSize, PauseLayout.ButtonIconSize);
                Image icon = UiBuilder.Fill(
                    UiBuilder.At("Icon", rect, iconRect, buttonRect), UiTheme.TextPrimary,
                    _sprites.Icon(MenuIcons[i]));
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                var textRect = new LayoutRect(PauseLayout.ButtonTextLeft, buttonRect.Top,
                    PauseLayout.ButtonTextRight - PauseLayout.ButtonTextLeft, buttonRect.Height);
                Text label = UiBuilder.LabelAt("Label", rect, textRect, buttonRect, PauseLayout.MenuCaptions[i],
                    PauseLayout.FontButton, UiTheme.TextPrimary);

                _entries.Add(new PauseMenuEntry
                {
                    Button = button,
                    Label = label,
                    Icon = icon,
                    Action = action,
                    AlwaysEnabled = action == PauseActions.Resume || action == PauseActions.Settings ||
                                    action == PauseActions.Controls || action == PauseActions.Quit,
                });
            }
        }

        private void BuildRightPanel()
        {
            Image panel = UiBuilder.ImageAt("Right panel", _root, PauseLayout.RightPanel, Frame,
                UiTheme.PausePanel, _sprites.Rounded(14), true);
            RectTransform parent = panel.rectTransform;

            BuildHeader(parent);
            BuildGoals(parent);
            BuildStatus(parent);
            BuildFinance(parent);
            BuildQuickLinks(parent);
        }

        private void BuildHeader(RectTransform parent)
        {
            // Шапка: «день+время» и «адрес». Панельки внутри правой панели.
            Image dateBlock = UiBuilder.ImageAt("Header date", parent, PauseLayout.HeaderDate,
                PauseLayout.RightPanel, UiTheme.PauseSubPanel, _sprites.Rounded(12));
            RectTransform dateRect = dateBlock.rectTransform;

            UiBuilder.Fill(UiBuilder.At("Calendar", dateRect,
                    PauseLayout.Inset(PauseLayout.HeaderDateIcon, 0f, 0f, 0f, 0f), PauseLayout.HeaderDate),
                UiTheme.TextPrimary, _sprites.Icon(UiIconKind.Calendar)).preserveAspect = true;

            _dateLabel = UiBuilder.LabelAt("Date", dateRect,
                new LayoutRect(PauseLayout.HeaderDateIcon.Right + 14f, PauseLayout.HeaderDate.Top,
                    300f, PauseLayout.HeaderDate.Height),
                PauseLayout.HeaderDate, "—", PauseLayout.FontHeader, UiTheme.TextPrimary);

            var clockIcon = new LayoutRect(950f, 158f, 26f, 30f);
            UiBuilder.Fill(UiBuilder.At("Clock", dateRect, clockIcon, PauseLayout.HeaderDate),
                UiTheme.TextPrimary, _sprites.Icon(UiIconKind.Clock)).preserveAspect = true;

            _timeLabel = UiBuilder.LabelAt("Time", dateRect,
                new LayoutRect(clockIcon.Right + 12f, PauseLayout.HeaderDate.Top, 140f, PauseLayout.HeaderDate.Height),
                PauseLayout.HeaderDate, "—", PauseLayout.FontHeader, UiTheme.TextPrimary);

            Image addressBlock = UiBuilder.ImageAt("Header address", parent, PauseLayout.HeaderAddress,
                PauseLayout.RightPanel, UiTheme.PauseSubPanel, _sprites.Rounded(12));
            RectTransform addressRect = addressBlock.rectTransform;
            _locationBlock = addressRect;

            UiBuilder.Fill(UiBuilder.At("Home", addressRect,
                    new LayoutRect(PauseLayout.HeaderHomeIcon.Left, 0f, PauseLayout.HeaderHomeIcon.Width,
                        PauseLayout.HeaderAddress.Height),
                    PauseLayout.HeaderAddress),
                UiTheme.TextPrimary, _sprites.Icon(UiIconKind.Home)).preserveAspect = true;

            float textLeft = PauseLayout.HeaderHomeIcon.Right + 12f;
            _locationLabel = UiBuilder.LabelAt("Location", addressRect,
                new LayoutRect(textLeft, PauseLayout.HeaderAddress.Top + 10f,
                    PauseLayout.HeaderAddress.Right - textLeft - 12f, 30f),
                PauseLayout.HeaderAddress, "—", PauseLayout.FontHeader, UiTheme.TextPrimary);

            _locationDetailLabel = UiBuilder.LabelAt("LocationDetail", addressRect,
                new LayoutRect(textLeft, PauseLayout.HeaderAddress.Top + 38f,
                    PauseLayout.HeaderAddress.Right - textLeft - 12f, 22f),
                PauseLayout.HeaderAddress, string.Empty, PauseLayout.FontHeaderSub, UiTheme.TextMuted);
        }

        private void BuildGoals(RectTransform parent)
        {
            Image goals = UiBuilder.ImageAt("Goals", parent, PauseLayout.Goals, PauseLayout.RightPanel,
                UiTheme.PauseSubPanel, _sprites.Rounded(12));
            SectionTitle(goals.rectTransform, PauseLayout.Goals, "Цели", UiIconKind.Target);

            // Заголовок занимает верх блока; строки целей идут ниже.
            _goalsContent = UiBuilder.At("Goals content", goals.rectTransform,
                new LayoutRect(PauseLayout.Goals.Left + 16f, PauseLayout.Goals.Top + 86f,
                    PauseLayout.Goals.Width - 32f, PauseLayout.Goals.Bottom - PauseLayout.Goals.Top - 96f),
                PauseLayout.Goals);

            _goalsEmpty = UiBuilder.LabelAt("Goals empty", goals.rectTransform,
                new LayoutRect(PauseLayout.Goals.Left + 16f, PauseLayout.Goals.Top + 90f,
                    PauseLayout.Goals.Width - 32f, 40f),
                PauseLayout.Goals, "Целей нет", PauseLayout.FontBody, UiTheme.TextMuted);
        }

        private void BuildStatus(RectTransform parent)
        {
            Image status = UiBuilder.ImageAt("Status", parent, PauseLayout.Status, PauseLayout.RightPanel,
                UiTheme.PauseSubPanel, _sprites.Rounded(12));
            SectionTitle(status.rectTransform, PauseLayout.Status, "Статус", UiIconKind.Chart);

            for (int i = 0; i < 4; i++)
            {
                LayoutRect barRect = PauseLayout.Bar(i);

                // Иконка.
                var iconRect = new LayoutRect(PauseLayout.StatusIconX, barRect.Top - 4f, 24f, 24f);
                Image icon = UiBuilder.Fill(UiBuilder.At("Icon " + i, status.rectTransform, iconRect, PauseLayout.Status),
                    StatusIconColors[i], _sprites.Icon(StatusIcons[i]));
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                _barIcons[i] = icon;

                // Подпись.
                UiBuilder.LabelAt("Name " + i, status.rectTransform,
                    new LayoutRect(PauseLayout.StatusLabelX, barRect.Top - 6f,
                        barRect.Left - PauseLayout.StatusLabelX - 16f, 32f),
                    PauseLayout.Status, StatusNames[i], PauseLayout.FontBody, UiTheme.TextPrimary);

                // Полоса: фон + заполнение.
                Image track = UiBuilder.ImageAt("Track " + i, status.rectTransform, barRect, PauseLayout.Status,
                    UiTheme.BarTrack, _sprites.Rounded(10));
                track.raycastTarget = false;

                Image fill = UiBuilder.ImageAt("Fill " + i, status.rectTransform, barRect, PauseLayout.Status,
                    StatusBarColors[i], _sprites.Rounded(10));
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = 0;
                fill.fillAmount = 0f;
                fill.raycastTarget = false;
                _bars[i] = fill;

                // Процент.
                Text percent = UiBuilder.LabelAt("Percent " + i, status.rectTransform,
                    new LayoutRect(barRect.Right + 12f, barRect.Top - 8f,
                        PauseLayout.StatusPercentRight - barRect.Right - 12f, 36f),
                    PauseLayout.Status, UiFormat.Dash, PauseLayout.FontPercent, UiTheme.TextPrimary,
                    TextAnchor.MiddleRight);
                _percentLabels.Add(percent);
            }
        }

        private void BuildFinance(RectTransform parent)
        {
            Image finance = UiBuilder.ImageAt("Finance", parent, PauseLayout.Finance, PauseLayout.RightPanel,
                UiTheme.PauseSubPanel, _sprites.Rounded(12));
            SectionTitle(finance.rectTransform, PauseLayout.Finance, "Финансы", UiIconKind.Coins);

            string[] captions = { "Деньги:", "Долг:", "Доход за день:" };
            Color[] colors = { UiTheme.TextPrimary, UiTheme.Debt, UiTheme.Positive };
            var values = new Text[3];

            for (int i = 0; i < captions.Length; i++)
            {
                float top = PauseLayout.FinanceRowTops[i];
                UiBuilder.LabelAt("Caption " + i, finance.rectTransform,
                    new LayoutRect(PauseLayout.FinanceLabelX, top, 180f, 30f),
                    PauseLayout.Finance, captions[i], PauseLayout.FontBody, UiTheme.TextPrimary);

                values[i] = UiBuilder.LabelAt("Value " + i, finance.rectTransform,
                    new LayoutRect(PauseLayout.FinanceValueRight - 200f, top, 200f, 30f),
                    PauseLayout.Finance, UiFormat.Dash, PauseLayout.FontBody, colors[i], TextAnchor.MiddleRight);
            }

            _moneyValue = values[0];
            _debtValue = values[1];
            _incomeValue = values[2];
        }

        private void BuildQuickLinks(RectTransform parent)
        {
            Image links = UiBuilder.ImageAt("Quick links", parent, PauseLayout.QuickLinks, PauseLayout.RightPanel,
                UiTheme.PauseSubPanel, _sprites.Rounded(12));
            SectionTitle(links.rectTransform, PauseLayout.QuickLinks, "Быстрые ссылки", UiIconKind.Grid);

            string[] actions = { PauseActions.Phone, PauseActions.Inventory, PauseActions.Map, PauseActions.Tasks };
            string[] captions = { "Телефон", "Инвентарь", "Карта", "Задания" };
            string[] shortcuts = { "[P]", "[I]", "[M]", "[J]" };
            UiIconKind[] icons = { UiIconKind.Phone, UiIconKind.Bag, UiIconKind.Pin, UiIconKind.Tasks };

            for (int i = 0; i < actions.Length; i++)
            {
                LayoutRect rect = PauseLayout.QuickLinkButtons[i];
                RectTransform linkRect = UiBuilder.At("Link " + actions[i], links.rectTransform, rect,
                    PauseLayout.QuickLinks);
                Image background = UiBuilder.Fill(linkRect, UiTheme.PauseButton, _sprites.Rounded(10), true);

                var button = linkRect.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                string action = actions[i];
                button.onClick.AddListener(() => OnLinkPressed(action));
                _quickLinks.Add(button);

                UiBuilder.Fill(UiBuilder.At("Icon", linkRect, new LayoutRect(25f, 19f, 25f, 25f), rect),
                    UiTheme.TextPrimary, _sprites.Icon(icons[i])).preserveAspect = true;

                UiBuilder.LabelAt("Label", linkRect, new LayoutRect(72f, 0f, rect.Width - 72f - 52f, rect.Height),
                    rect, captions[i], PauseLayout.FontBody, UiTheme.TextPrimary);

                UiBuilder.LabelAt("Shortcut", linkRect, new LayoutRect(rect.Width - 56f, 0f, 44f, rect.Height),
                    rect, shortcuts[i], PauseLayout.FontSmall, UiTheme.TextMuted, TextAnchor.MiddleRight);
            }
        }

        private void SectionTitle(RectTransform parent, LayoutRect block, string title, UiIconKind icon)
        {
            var iconRect = new LayoutRect(block.Left + PauseLayout.SectionIconOffset, block.Top + 24f,
                PauseLayout.SectionIconSize, PauseLayout.SectionIconSize);
            UiBuilder.Fill(UiBuilder.At(title + " icon", parent, iconRect, block),
                UiTheme.TextPrimary, _sprites.Icon(icon)).preserveAspect = true;

            UiBuilder.LabelAt(title + " title", parent,
                new LayoutRect(block.Left + PauseLayout.SectionTitleOffset, block.Top + 18f,
                    block.Width - PauseLayout.SectionTitleOffset - 20f, 38f),
                block, title, PauseLayout.FontSection, UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildFooter()
        {
            UiBuilder.LabelAt("Version", _root, new LayoutRect(69f, 876f, 300f, 28f), Frame,
                "v0.2.0 (alpha)", PauseLayout.FontSmall, UiTheme.TextMuted);

            UiBuilder.LabelAt("Hints", _root, new LayoutRect(1220f, 868f, 420f, 30f), Frame,
                "Esc  Закрыть меню        Enter  Выбрать", PauseLayout.FontSmall, UiTheme.TextMuted,
                TextAnchor.MiddleRight);
        }

        // ------------------------------------------------------------------ поведение

        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            bool was = _root.gameObject.activeSelf;
            _root.gameObject.SetActive(visible);
            if (!visible)
            {
                _selected = 0;
                return;
            }

            if (!was) Select(0);
            if (_router == null) _router = UiRouter.Instance;
            if (_router != null) _router.RefreshSnapshot();
            else Render(_snapshot, _navigation, _world);
        }

        /// <summary>Перерисовать данные. Вызывается роутером; сама пауза ничего не считает.</summary>
        public void Render(UiSnapshot snapshot, NavigationService navigation, UiWorldAdapter world)
        {
            if (snapshot != null) _snapshot = snapshot;
            _navigation = navigation;
            _world = world;
            if (!IsVisible) return;

            RenderHeader();
            RenderGoals();
            RenderStatus();
            RenderFinance();
            UpdateAvailability();
        }

        private void RenderHeader()
        {
            ClockView clock = _snapshot.Clock ?? new ClockView();
            _dateLabel.text = string.IsNullOrEmpty(clock.DayLabel)
                ? UiFormat.Missing
                : UiFormat.Day(clock.DayLabel, clock.DayNumber, string.Empty).Trim();
            _timeLabel.text = string.IsNullOrEmpty(clock.TimeLabel) ? UiFormat.Dash : clock.TimeLabel;

            bool known = !string.IsNullOrEmpty(clock.Location);
            _locationBlock.gameObject.SetActive(known);
            if (!known) return;

            _locationLabel.text = clock.Location;
            _locationDetailLabel.text = clock.LocationDetail ?? string.Empty;
            _locationDetailLabel.gameObject.SetActive(!string.IsNullOrEmpty(_locationDetailLabel.text));
        }

        private void RenderGoals()
        {
            for (int i = _goalsContent.childCount - 1; i >= 0; i--)
            {
                UiBuilder.Destroy(_goalsContent.GetChild(i).gameObject);
            }

            GoalView[] goals = _snapshot.VisibleGoals();
            bool empty = goals == null || goals.Length == 0;
            _goalsEmpty.gameObject.SetActive(empty);
            if (empty) return;

            for (int i = 0; i < goals.Length; i++)
            {
                GoalView goal = goals[i];
                float top = 8f + i * 45f;
                var rowRect = new LayoutRect(8f, top, 400f, 32f);
                RectTransform row = UiBuilder.At(goal.Title, _goalsContent, rowRect,
                    new LayoutRect(0f, 0f, 425f, 152f));

                UiBuilder.Fill(UiBuilder.At("State", row, new LayoutRect(0f, 4f, 24f, 24f), rowRect),
                    goal.Complete ? UiTheme.Positive : UiTheme.TextMuted,
                    _sprites.Icon(goal.Complete ? UiIconKind.CheckDone : UiIconKind.CheckEmpty))
                    .preserveAspect = true;

                UiBuilder.LabelAt("Text", row, new LayoutRect(36f, 0f, rowRect.Width - 36f, rowRect.Height),
                    rowRect, UiFormat.Goal(goal), PauseLayout.FontBody, UiTheme.TextPrimary);
            }
        }

        private void RenderStatus()
        {
            PlayerStatus status = _snapshot.Status ?? new PlayerStatus();
            PercentValue[] values = { status.Health, status.Satiety, status.Energy, status.Mood };

            for (int i = 0; i < _bars.Length; i++)
            {
                PercentValue value = values[i];
                bool known = value.HasValue;

                // Неизвестное значение не притворяется нулём: полоса гаснет, подпись «Нет данных».
                _bars[i].fillAmount = known ? Mathf.Clamp01(value.Ratio) : 0f;
                _bars[i].color = known ? StatusBarColors[i] : UiTheme.BarTrack;
                _percentLabels[i].text = UiFormat.Percent(value);
                _percentLabels[i].color = known ? UiTheme.TextPrimary : UiTheme.TextDisabled;
                _barIcons[i].color = known ? StatusIconColors[i] : new Color(1f, 1f, 1f, 0.30f);
            }
        }

        private void RenderFinance()
        {
            FinanceView finance = _snapshot.Finance ?? new FinanceView();
            _moneyValue.text = finance.Money.HasValue ? UiFormat.Rubles(finance.Money) : UiFormat.Missing;
            _debtValue.text = finance.Debt.HasValue ? UiFormat.Rubles(finance.Debt) : UiFormat.Missing;
            _incomeValue.text = finance.DailyIncome.HasValue
                ? UiFormat.SignedRubles(finance.DailyIncome)
                : UiFormat.Missing;

            // Красным долг показывается только когда он реально есть.
            _debtValue.color = finance.Debt.HasValue && finance.Debt.Value > 0 ? UiTheme.Debt : UiTheme.TextMuted;
        }

        private void UpdateAvailability()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                PauseMenuEntry entry = _entries[i];
                bool available = entry.AlwaysEnabled || (_world != null && _world.IsMenuActionAvailable(entry.Action));
                entry.Button.interactable = available;
                entry.Label.color = available ? UiTheme.TextPrimary : UiTheme.TextDisabled;
                entry.Icon.color = available ? UiTheme.TextPrimary : new Color(1f, 1f, 1f, 0.32f);
            }

            for (int i = 0; i < _quickLinks.Count; i++) _quickLinks[i].interactable = true;
        }

        private void OnMenuPressed(string action)
        {
            if (action == PauseActions.Resume)
            {
                Apply(UiIntent.Resume);
                return;
            }
            Execute(action);
        }

        private void OnLinkPressed(string action)
        {
            switch (action)
            {
                case PauseActions.Phone:
                    Apply(UiIntent.OpenPhoneFromPause);
                    return;
                case PauseActions.Map:
                    Apply(UiIntent.OpenMapFromPause);
                    return;
                default:
                    Execute(action);
                    return;
            }
        }

        private void Apply(UiIntent intent)
        {
            if (IntentHandler != null)
            {
                IntentHandler(intent);
                return;
            }

            if (_router == null) _router = UiRouter.Instance;
            if (_router != null)
            {
                _router.RequestIntent(intent);
                return;
            }
            Debug.LogWarning("[PauseMenu] UiRouter не найден — намерение " + intent + " не применено.");
        }

        private void Execute(string action)
        {
            if (_world == null)
            {
                Debug.LogWarning("[PauseMenu] Нет UiWorldAdapter — действие «" + action + "» недоступно.");
                return;
            }
            if (!_world.IsMenuActionAvailable(action)) return;
            if (!_world.ExecuteMenuAction(action))
                Debug.LogWarning("[PauseMenu] Действие «" + action + "» не выполнено.");
        }

        /// <summary>Быстрые действия с клавиатуры (I/J) — вызывает роутер.</summary>
        public bool TryQuickAction(string action)
        {
            if (!IsVisible) return false;
            if (action == PauseActions.Inventory || action == PauseActions.Tasks)
            {
                Execute(action);
                return true;
            }
            return false;
        }

        /// <summary>Enter на открытом меню: активировать выбранную строку.</summary>
        public void Confirm()
        {
            if (!IsVisible || _entries.Count == 0) return;
            PauseMenuEntry entry = _entries[Mathf.Clamp(_selected, 0, _entries.Count - 1)];
            if (entry.Button.interactable) OnMenuPressed(entry.Action);
        }

        /// <summary>Перемещение выбора (стрелки/геймпад). Пропускает недоступные строки.</summary>
        public void MoveSelection(int delta)
        {
            if (!IsVisible || _entries.Count == 0) return;
            int index = _selected;
            for (int attempt = 0; attempt < _entries.Count; attempt++)
            {
                index = (index + delta + _entries.Count) % _entries.Count;
                if (_entries[index].Button.interactable) break;
            }
            Select(index);
        }

        /// <summary>Индекс выбранной строки — для тестов и навигации.</summary>
        public int SelectedIndex => _selected;

        private void Select(int index)
        {
            if (_entries.Count == 0) return;
            _selected = Mathf.Clamp(index, 0, _entries.Count - 1);
            for (int i = 0; i < _entries.Count; i++)
            {
                Image target = _entries[i].Button.targetGraphic as Image;
                if (target == null) continue;
                target.color = i == _selected ? UiTheme.PauseButtonHover : UiTheme.PauseButton;
            }
        }
    }
}
