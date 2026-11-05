using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace LifeFromZero.City.Pause
{
    // Constructed once, retained across openings and scenes. All dimensions are relative
    // to an aspect-fitted 1920x1080 (16:9) reference area, scaling cleanly to 1600x900, 1920x1080 and 2560x1440.
    public sealed class PauseView : MonoBehaviour
    {
        private enum IconKind
        {
            Crown, Play, Save, Load, Settings, Controls, Home, Power,
            Calendar, Clock, Target, CheckEmpty, CheckDone,
            Chart, Heart, ForkKnife, Bolt, Smile,
            Coins, Grid, Phone, Bag, Pin, Tasks
        }

        private static readonly Color PanelColor = new Color(.07f, .07f, .07f, .92f);
        private static readonly Color SubColor = new Color(.11f, .11f, .11f, .88f);
        private static readonly Color ButtonColor = new Color(.14f, .14f, .14f, .96f);
        private static readonly Color BorderColor = new Color(.42f, .42f, .42f, .55f);
        private static readonly Color DividerColor = new Color(1f, 1f, 1f, .08f);
        private static readonly Color White = new Color(.96f, .96f, .96f, 1f);
        private static readonly Color Muted = new Color(.75f, .75f, .75f, 1f);
        private static readonly Color DisabledText = new Color(.58f, .58f, .58f, .72f);
        private static readonly Color LogoRed = new Color(.90f, .07f, .11f, 1f);
        private static readonly Color DebtRed = new Color(.94f, .20f, .22f, 1f);
        private static readonly Color IncomeGreen = new Color(.34f, .85f, .22f, 1f);

        private static readonly string[] MenuTitles =
        {
            "Продолжить игру",
            "Сохранить игру",
            "Загрузить игру",
            "Настройки",
            "Управление",
            "Выйти в главное меню",
            "Выйти из игры"
        };

        private static readonly string[] StatusNames = { "Здоровье", "Сытость", "Энергия", "Настроение" };
        private static readonly string[] FinanceNames = { "Деньги:", "Долг:", "Доход за день:" };
        private static readonly string[] QuickTitles = { "Телефон", "Инвентарь", "Карта", "Задания" };

        private Font font;
        private Sprite rounded;
        private Sprite pill;
        private Sprite checkEmptySprite;
        private Sprite checkDoneSprite;
        private readonly List<UnityEngine.Object> ownedAssets = new List<UnityEngine.Object>();

        private Text day, time, location, locationDetail;
        private readonly Text[] goals = new Text[3];
        private readonly Image[] goalChecks = new Image[3];
        private readonly Text[] statusValues = new Text[4];
        private readonly Text[] financeValues = new Text[3];
        private readonly Image[] bars = new Image[4];
        private readonly Button[] actions = new Button[9];
        private readonly Text[] actionTexts = new Text[9];
        private readonly Image[] actionIcons = new Image[9];
        private readonly Text[] quickShortcuts = new Text[4];
        private PauseController controller;

        public static PauseView Create(Transform parent, PauseController owner)
        {
            var go = new GameObject("Pause Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PauseView));
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<PauseView>();
            v.controller = owner;
            v.Build();
            go.SetActive(false);
            return v;
        }

        private void Build()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            rounded = MakeRounded(32, 32, 8);
            pill = MakeRounded(128, 24, 11);
            checkEmptySprite = MakeIcon(IconKind.CheckEmpty);
            checkDoneSprite = MakeIcon(IconKind.CheckDone);

            var root = Rect("Safe frame", transform, 0f, 0f, 1f, 1f);
            var fitter = root.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            // Light transparent scrim; game world remains visible in the background.
            ImageBox("Scrim", root, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, .28f), null);

            // ---------------- Left Panel (~34% width, ~80% height) ----------------
            var left = Panel("Menu", root, .02f, .165f, .356f, .965f, PanelColor);

            var lifeFrom = Label("Logo LIFE FROM", left, "LIFE FROM", .11f, .865f, .59f, .955f, 52, FontStyle.BoldAndItalic, TextAnchor.MiddleRight, White);
            lifeFrom.horizontalOverflow = HorizontalWrapMode.Overflow;
            var zero = Label("Logo ZERO", left, "ZERO", .61f, .865f, .88f, .955f, 54, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft, LogoRed);
            zero.horizontalOverflow = HorizontalWrapMode.Overflow;

            var crown = IconBox("Logo crown", left, MakeIcon(IconKind.Crown), .79f, .932f, .855f, .974f, LogoRed);
            crown.rectTransform.localEulerAngles = new Vector3(0f, 0f, -14f);
            ImageBox("Logo underline", left, .12f, .852f, .86f, .860f, LogoRed, pill);

            Label("Title", left, "Пауза", .12f, .775f, .86f, .842f, 38, FontStyle.Bold, TextAnchor.MiddleLeft, White);

            IconKind[] menuIcons =
            {
                IconKind.Play, IconKind.Save, IconKind.Load, IconKind.Settings,
                IconKind.Controls, IconKind.Home, IconKind.Power
            };

            for (int i = 0; i < MenuTitles.Length; i++)
            {
                float top = .745f - i * .096f;
                int n = i;
                var button = MakeMenuButton("Menu Button " + i, left, MakeIcon(menuIcons[i]), MenuTitles[i],
                    .075f, top - .080f, .925f, top, () =>
                    {
                        if (n == 0) controller.Close();
                        else if (n == 6) controller.Quit();
                        else controller.Invoke((PauseAction)(n - 1));
                    }, out var text, out var icon);

                if (i >= 1 && i <= 5)
                {
                    actions[i - 1] = button;
                    actionTexts[i - 1] = text;
                    actionIcons[i - 1] = icon;
                }
            }

            // ---------------- Right Panel (~59% width) ----------------
            var right = Panel("Dashboard", root, .372f, .215f, .962f, .865f, PanelColor);

            // 1. Top Row: Day, Time, Location + District
            var header = Panel("Header", right, .022f, .840f, .978f, .972f, SubColor);
            IconBox("Day Icon", header, MakeIcon(IconKind.Calendar), .024f, .20f, .064f, .80f, White);
            day = Label("Day Text", header, "Нет данных", .078f, .14f, .385f, .86f, 28, FontStyle.Normal, TextAnchor.MiddleLeft, White);

            IconBox("Time Icon", header, MakeIcon(IconKind.Clock), .405f, .20f, .445f, .80f, White);
            time = Label("Time Text", header, "Нет данных", .458f, .14f, .635f, .86f, 28, FontStyle.Normal, TextAnchor.MiddleLeft, White);

            var locBox = Panel("Location Box", header, .655f, .05f, .995f, .95f, new Color(.13f, .13f, .13f, .92f));
            IconBox("Location Icon", locBox, MakeIcon(IconKind.Home), .05f, .20f, .17f, .80f, White);
            location = Label("Location Text", locBox, "Нет данных", .21f, .50f, .97f, .92f, 25, FontStyle.Normal, TextAnchor.MiddleLeft, White);
            locationDetail = Label("Location Detail Text", locBox, "", .21f, .08f, .97f, .46f, 19, FontStyle.Normal, TextAnchor.MiddleLeft, Muted);

            // 2. Middle Row: Goals (left) & Status (right)
            var goalPanel = Panel("Goals", right, .022f, .425f, .515f, .815f, SubColor);
            Heading(goalPanel, MakeIcon(IconKind.Target), "Цели");
            for (int i = 0; i < 3; i++)
            {
                float y = .56f - .20f * i;
                goalChecks[i] = IconBox("Goal Check " + i, goalPanel, checkEmptySprite, .05f, y - .068f, .115f, y + .068f, Muted);
                goals[i] = Label("Goal Text " + i, goalPanel, "", .14f, y - .085f, .96f, y + .085f, 22, FontStyle.Normal, TextAnchor.MiddleLeft, White);
            }

            var status = Panel("Status", right, .532f, .425f, .978f, .815f, SubColor);
            Heading(status, MakeIcon(IconKind.Chart), "Статус");
            IconKind[] statusIcons = { IconKind.Heart, IconKind.ForkKnife, IconKind.Bolt, IconKind.Smile };
            Color[] iconColors =
            {
                new Color(.92f, .22f, .26f, 1f),
                new Color(.96f, .74f, .10f, 1f),
                new Color(.98f, .76f, .08f, 1f),
                new Color(.96f, .74f, .10f, 1f)
            };
            Color[] barColors =
            {
                new Color(.30f, .80f, .18f, 1f),
                new Color(.98f, .75f, .04f, 1f),
                new Color(.16f, .68f, .92f, 1f),
                new Color(.30f, .80f, .18f, 1f)
            };
            for (int i = 0; i < 4; i++)
            {
                float y = .595f - .162f * i;
                IconBox("Status Icon " + i, status, MakeIcon(statusIcons[i]), .045f, y - .062f, .112f, y + .062f, iconColors[i]);
                Label("Status Label " + i, status, StatusNames[i], .132f, y - .072f, .395f, y + .072f, 21, FontStyle.Normal, TextAnchor.MiddleLeft, White);
                ImageBox("Track " + i, status, .410f, y - .040f, .805f, y + .040f, new Color(.22f, .22f, .22f, .95f), pill);
                var bar = ImageBox("Fill " + i, status, .410f, y - .040f, .805f, y + .040f, barColors[i], pill);
                bar.type = Image.Type.Filled;
                bar.fillMethod = Image.FillMethod.Horizontal;
                bars[i] = bar;
                statusValues[i] = Label("Status Value " + i, status, "—", .820f, y - .072f, .965f, y + .072f, 21, FontStyle.Normal, TextAnchor.MiddleRight, White);
            }

            // 3. Bottom Row: Finance (left) & Quick links (right)
            var finance = Panel("Finance", right, .022f, .032f, .445f, .398f, SubColor);
            Heading(finance, MakeIcon(IconKind.Coins), "Финансы");
            for (int i = 0; i < 3; i++)
            {
                float y = .55f - .20f * i;
                Label("Finance Label " + i, finance, FinanceNames[i], .06f, y - .085f, .52f, y + .085f, 23, FontStyle.Normal, TextAnchor.MiddleLeft, White);
                financeValues[i] = Label("Finance Value " + i, finance, "Нет данных", .53f, y - .085f, .95f, y + .085f, 23, FontStyle.Normal, TextAnchor.MiddleRight, White);
            }

            var links = Panel("Quick links", right, .462f, .032f, .978f, .398f, SubColor);
            Heading(links, MakeIcon(IconKind.Grid), "Быстрые ссылки");
            IconKind[] quickIcons = { IconKind.Phone, IconKind.Bag, IconKind.Pin, IconKind.Tasks };
            for (int i = 0; i < 4; i++)
            {
                int n = i;
                float x = i % 2 == 0 ? .035f : .515f;
                float y = i < 2 ? .39f : .08f;
                actions[5 + i] = MakeQuickButton("Quick Button " + i, links, MakeIcon(quickIcons[i]), QuickTitles[i],
                    x, y, x + .450f, y + .265f,
                    () => controller.Invoke((PauseAction)(5 + n)),
                    out actionTexts[5 + i], out actionIcons[5 + i], out quickShortcuts[i]);
            }

            // ---------------- Bottom Screen Bar (outside main panels) ----------------
            Label("Version Text", root, "v0.2.0 (alpha)", .024f, .026f, .220f, .066f, 20, FontStyle.Normal, TextAnchor.MiddleLeft, Muted);

            var escBadge = Panel("Esc Badge", root, .775f, .026f, .818f, .068f, ButtonColor);
            Label("Esc Key Text", escBadge, "Esc", .08f, .08f, .92f, .92f, 19, FontStyle.Normal, TextAnchor.MiddleCenter, White);
            Label("Esc Hint Text", root, "Закрыть меню", .826f, .026f, .962f, .068f, 21, FontStyle.Normal, TextAnchor.MiddleLeft, White);
        }

        public void Render(PauseSnapshot snapshot, PauseIntegration integration)
        {
            var s = snapshot ?? new PauseSnapshot();
            day.text = OrMissing(s.Day);
            time.text = OrMissing(s.Time);
            location.text = OrMissing(s.Location);
            locationDetail.text = s.LocationDetail ?? "";

            bool anyGoals = s.Goals != null && s.Goals.Length > 0;
            for (int i = 0; i < goals.Length; i++)
            {
                var goal = anyGoals && i < s.Goals.Length ? s.Goals[i] : null;
                if (goal == null)
                {
                    goalChecks[i].enabled = false;
                    goals[i].text = !anyGoals && i == 0 ? "Нет данных" : "";
                    goals[i].color = Muted;
                }
                else
                {
                    goalChecks[i].enabled = true;
                    goalChecks[i].sprite = goal.Complete ? checkDoneSprite : checkEmptySprite;
                    goalChecks[i].color = goal.Complete ? IncomeGreen : Muted;
                    string title = OrMissing(goal.Title);
                    bool addProgress = !string.IsNullOrWhiteSpace(goal.Progress) &&
                                       title.IndexOf(goal.Progress, StringComparison.Ordinal) < 0;
                    goals[i].text = addProgress ? title + " (" + goal.Progress + ")" : title;
                    goals[i].color = White;
                }
            }

            float?[] values = { s.Health, s.ResolveSatiety(), s.Energy, s.ResolveMood() };
            for (int i = 0; i < 4; i++)
            {
                bool valid = values[i].HasValue && !float.IsNaN(values[i].Value) && !float.IsInfinity(values[i].Value);
                float value = valid ? Mathf.Clamp(values[i].Value, 0f, 100f) : 0f;
                bars[i].fillAmount = value / 100f;
                statusValues[i].text = valid ? value.ToString("0", CultureInfo.InvariantCulture) + "%" : "—";
            }

            financeValues[0].text = Currency(s.Money);
            financeValues[0].color = White;

            financeValues[1].text = Currency(s.Debt);
            financeValues[1].color = s.Debt.HasValue ? DebtRed : White;

            financeValues[2].text = SignedCurrency(s.DailyIncome);
            financeValues[2].color = !s.DailyIncome.HasValue || s.DailyIncome.Value == 0m
                ? White
                : s.DailyIncome.Value > 0m ? IncomeGreen : DebtRed;

            for (int i = 0; i < actions.Length; i++)
            {
                var action = (PauseAction)i;
                bool available = integration != null && integration.IsAvailable(action);
                actions[i].interactable = available;
                Color contentColor = available ? White : DisabledText;

                if (i < 5)
                {
                    actionTexts[i].text = MenuTitles[i + 1];
                    actionTexts[i].color = contentColor;
                    if (actionIcons[i] != null) actionIcons[i].color = contentColor;
                }
                else
                {
                    int q = i - 5;
                    string key = q == 0 ? "P" : integration != null ? integration.ShortcutLabel(action) : null;
                    actionTexts[i].text = QuickTitles[q];
                    actionTexts[i].color = contentColor;
                    if (actionIcons[i] != null) actionIcons[i].color = contentColor;
                    if (quickShortcuts[q] != null)
                    {
                        quickShortcuts[q].text = !string.IsNullOrWhiteSpace(key) ? "[" + key.Trim() + "]" : "";
                        quickShortcuts[q].color = available ? Muted : DisabledText;
                    }
                }
            }
        }

        private static string OrMissing(string value) => string.IsNullOrWhiteSpace(value) ? "Нет данных" : value;

        private static string FormatRubles(decimal value)
        {
            var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            nfi.NumberGroupSeparator = " ";
            nfi.NumberDecimalDigits = 0;
            return value.ToString("N0", nfi) + " ₽";
        }

        private static string Currency(decimal? value) =>
            value.HasValue ? FormatRubles(value.Value) : "Нет данных";

        private static string SignedCurrency(decimal? value)
        {
            if (!value.HasValue) return "Нет данных";
            if (value.Value > 0m) return "+" + FormatRubles(value.Value);
            if (value.Value < 0m) return "-" + FormatRubles(Math.Abs(value.Value));
            return "0 ₽";
        }

        private void Heading(RectTransform parent, Sprite iconSprite, string text)
        {
            IconBox("Heading Icon", parent, iconSprite, .045f, .76f, .112f, .93f, White);
            Label("Heading Text", parent, text, .132f, .74f, .95f, .95f, 28, FontStyle.Bold, TextAnchor.MiddleLeft, White);
            ImageBox("Divider", parent, .04f, .712f, .96f, .718f, DividerColor, null);
        }

        private Button MakeMenuButton(string name, RectTransform parent, Sprite iconSprite, string text,
            float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction click,
            out Text label, out Image icon)
        {
            var r = Panel(name, parent, x0, y0, x1, y1, ButtonColor);
            var button = ConfigureButton(r, click);
            icon = IconBox("Icon", r, iconSprite, .055f, .18f, .140f, .82f, White);
            label = Label("Text", r, text, .175f, .08f, .95f, .92f, 26, FontStyle.Normal, TextAnchor.MiddleLeft, White);
            return button;
        }

        private Button MakeQuickButton(string name, RectTransform parent, Sprite iconSprite, string text,
            float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction click,
            out Text label, out Image icon, out Text shortcut)
        {
            var r = Panel(name, parent, x0, y0, x1, y1, ButtonColor);
            var button = ConfigureButton(r, click);
            icon = IconBox("Icon", r, iconSprite, .055f, .18f, .180f, .82f, White);
            label = Label("Text", r, text, .215f, .08f, .72f, .92f, 23, FontStyle.Normal, TextAnchor.MiddleLeft, White);
            shortcut = Label("Shortcut", r, "", .73f, .08f, .94f, .92f, 21, FontStyle.Normal, TextAnchor.MiddleRight, Muted);
            return button;
        }

        private static Button ConfigureButton(RectTransform r, UnityEngine.Events.UnityAction click)
        {
            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = r.GetComponent<Image>();
            button.targetGraphic.raycastTarget = true;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(.84f, .84f, .84f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(.65f, .65f, .65f, .85f);
            button.colors = colors;
            button.onClick.AddListener(click);
            return button;
        }

        private RectTransform Panel(string name, Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var edge = ImageBox(name + " border", parent, x0, y0, x1, y1, BorderColor, rounded).rectTransform;
            var inside = ImageBox(name, edge, .003f, .005f, .997f, .995f, color, rounded).rectTransform;
            return inside;
        }

        private Text Label(string name, Transform parent, string value, float x0, float y0, float x1, float y1,
            int size, FontStyle style, TextAnchor anchor, Color color)
        {
            var r = Rect(name, parent, x0, y0, x1, y1);
            var t = r.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = value;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 10;
            t.resizeTextMaxSize = size;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        private Image IconBox(string name, Transform parent, Sprite sprite, float x0, float y0, float x1, float y1, Color color)
        {
            var r = Rect(name, parent, x0, y0, x1, y1);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        private static Image ImageBox(string name, Transform parent, float x0, float y0, float x1, float y1, Color color, Sprite sprite)
        {
            var r = Rect(name, parent, x0, y0, x1, y1);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        private static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = new Vector2(x0, y0);
            r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return r;
        }

        private Sprite MakeRounded(int width, int height, int radius)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "Pause rounded " + width + "x" + height;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Max(radius - x - .5f, 0f, x - (width - radius) + .5f);
                    float dy = Mathf.Max(radius - y - .5f, 0f, y - (height - radius) + .5f);
                    byte a = (byte)(Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy)) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            ownedAssets.Add(tex);
            ownedAssets.Add(sp);
            return sp;
        }

        private Sprite MakeIcon(IconKind kind)
        {
            const int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "Pause icon " + kind;
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                float y = (py + .5f) / size;
                for (int px = 0; px < size; px++)
                {
                    float x = (px + .5f) / size;
                    float d = EvalIconSdf(kind, x, y);
                    byte a = (byte)(Mathf.Clamp01(.5f - d * size) * 255f);
                    pixels[py * size + px] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
            ownedAssets.Add(tex);
            ownedAssets.Add(sp);
            return sp;
        }

        private static float EvalIconSdf(IconKind kind, float x, float y)
        {
            const float stroke = .038f;
            switch (kind)
            {
                case IconKind.Crown:
                    return Mathf.Min(
                        Seg(x, y, .16f, .24f, .84f, .24f, stroke),
                        Mathf.Min(
                            Mathf.Min(Seg(x, y, .16f, .24f, .14f, .74f, stroke), Seg(x, y, .14f, .74f, .38f, .46f, stroke)),
                            Mathf.Min(
                                Mathf.Min(Seg(x, y, .38f, .46f, .50f, .84f, stroke), Seg(x, y, .50f, .84f, .62f, .46f, stroke)),
                                Mathf.Min(Seg(x, y, .62f, .46f, .86f, .74f, stroke), Seg(x, y, .86f, .74f, .84f, .24f, stroke)))));

                case IconKind.Play:
                    return Triangle(x, y, .26f, .18f, .26f, .82f, .80f, .50f);

                case IconKind.Save:
                    return Mathf.Min(
                        BoxOutline(x, y, .50f, .50f, .32f, .32f, .05f, stroke),
                        Mathf.Min(
                            BoxOutline(x, y, .46f, .68f, .16f, .12f, .02f, stroke * .85f),
                            BoxOutline(x, y, .50f, .31f, .20f, .12f, .02f, stroke * .85f)));

                case IconKind.Load:
                    return Mathf.Min(
                        Mathf.Min(
                            Seg(x, y, .20f, .44f, .20f, .20f, stroke),
                            Mathf.Min(Seg(x, y, .20f, .20f, .80f, .20f, stroke), Seg(x, y, .80f, .20f, .80f, .44f, stroke))),
                        Mathf.Min(
                            Seg(x, y, .50f, .32f, .50f, .80f, stroke),
                            Mathf.Min(Seg(x, y, .32f, .62f, .50f, .80f, stroke), Seg(x, y, .68f, .62f, .50f, .80f, stroke))));

                case IconKind.Settings:
                {
                    float ring = Ring(x, y, .50f, .50f, .22f, stroke * 1.2f);
                    float teeth = 1f;
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * Mathf.PI * .25f;
                        float cx = .50f + Mathf.Cos(ang) * .28f;
                        float cy = .50f + Mathf.Sin(ang) * .28f;
                        teeth = Mathf.Min(teeth, Circle(x, y, cx, cy, .055f));
                    }
                    float outer = Mathf.Min(ring, teeth);
                    float hole = Circle(x, y, .50f, .50f, .12f);
                    return Mathf.Max(outer, -hole);
                }

                case IconKind.Controls:
                {
                    float d = BoxOutline(x, y, .50f, .50f, .36f, .24f, .05f, stroke);
                    for (int col = 0; col < 4; col++)
                    {
                        float kx = .29f + col * .14f;
                        d = Mathf.Min(d, BoxFill(x, y, kx, .58f, .032f, .032f));
                        if (col == 0 || col == 3) d = Mathf.Min(d, BoxFill(x, y, kx, .42f, .032f, .032f));
                    }
                    return Mathf.Min(d, Seg(x, y, .36f, .40f, .64f, .40f, stroke * .9f));
                }

                case IconKind.Home:
                    return Mathf.Min(
                        Mathf.Min(Seg(x, y, .16f, .52f, .50f, .82f, stroke), Seg(x, y, .50f, .82f, .84f, .52f, stroke)),
                        Mathf.Min(
                            Mathf.Min(Seg(x, y, .26f, .54f, .26f, .18f, stroke), Seg(x, y, .74f, .54f, .74f, .18f, stroke)),
                            Mathf.Min(Seg(x, y, .26f, .18f, .74f, .18f, stroke), BoxOutline(x, y, .50f, .31f, .08f, .13f, .01f, stroke * .85f))));

                case IconKind.Power:
                {
                    float ring = Ring(x, y, .50f, .46f, .28f, stroke);
                    if (y > .58f && Mathf.Abs(x - .50f) < .15f) ring = 1f;
                    return Mathf.Min(ring, Seg(x, y, .50f, .48f, .50f, .82f, stroke));
                }

                case IconKind.Calendar:
                    return Mathf.Min(
                        BoxOutline(x, y, .50f, .46f, .32f, .28f, .05f, stroke),
                        Mathf.Min(
                            Seg(x, y, .20f, .58f, .80f, .58f, stroke * .9f),
                            Mathf.Min(Seg(x, y, .34f, .68f, .34f, .82f, stroke), Seg(x, y, .66f, .68f, .66f, .82f, stroke))));

                case IconKind.Clock:
                    return Mathf.Min(
                        Ring(x, y, .50f, .50f, .33f, stroke),
                        Mathf.Min(Seg(x, y, .50f, .50f, .50f, .72f, stroke), Seg(x, y, .50f, .50f, .66f, .40f, stroke)));

                case IconKind.Target:
                    return Mathf.Min(
                        Mathf.Min(Ring(x, y, .48f, .48f, .30f, stroke), Ring(x, y, .48f, .48f, .15f, stroke * .9f)),
                        Mathf.Min(Circle(x, y, .48f, .48f, .045f), Seg(x, y, .48f, .48f, .80f, .80f, stroke)));

                case IconKind.CheckEmpty:
                    return BoxOutline(x, y, .50f, .50f, .34f, .34f, .07f, stroke);

                case IconKind.CheckDone:
                    return Mathf.Min(
                        BoxOutline(x, y, .50f, .50f, .34f, .34f, .07f, stroke),
                        Mathf.Min(Seg(x, y, .30f, .50f, .45f, .34f, stroke * 1.15f), Seg(x, y, .45f, .34f, .72f, .66f, stroke * 1.15f)));

                case IconKind.Chart:
                    return Mathf.Min(
                        BoxFill(x, y, .24f, .33f, .06f, .14f),
                        Mathf.Min(BoxFill(x, y, .50f, .44f, .06f, .25f), BoxFill(x, y, .76f, .53f, .06f, .34f)));

                case IconKind.Heart:
                {
                    float c1 = Circle(x, y, .36f, .60f, .18f);
                    float c2 = Circle(x, y, .64f, .60f, .18f);
                    float tri = Triangle(x, y, .19f, .53f, .81f, .53f, .50f, .18f);
                    return Mathf.Min(Mathf.Min(c1, c2), tri);
                }

                case IconKind.ForkKnife:
                {
                    float fork = Mathf.Min(
                        Seg(x, y, .34f, .18f, .34f, .54f, stroke),
                        Mathf.Min(
                            Seg(x, y, .25f, .54f, .43f, .54f, stroke * .9f),
                            Mathf.Min(
                                Seg(x, y, .25f, .54f, .25f, .80f, stroke * .75f),
                                Mathf.Min(Seg(x, y, .34f, .54f, .34f, .80f, stroke * .75f), Seg(x, y, .43f, .54f, .43f, .80f, stroke * .75f)))));
                    float knife = Mathf.Min(
                        Seg(x, y, .68f, .18f, .68f, .80f, stroke),
                        BoxFill(x, y, .64f, .64f, .05f, .15f));
                    return Mathf.Min(fork, knife);
                }

                case IconKind.Bolt:
                    return Mathf.Min(
                        Triangle(x, y, .56f, .84f, .24f, .46f, .52f, .46f),
                        Triangle(x, y, .44f, .16f, .48f, .54f, .76f, .54f));

                case IconKind.Smile:
                {
                    float face = Circle(x, y, .50f, .50f, .35f);
                    float eyes = Mathf.Min(Circle(x, y, .38f, .58f, .048f), Circle(x, y, .62f, .58f, .048f));
                    float smile = Ring(x, y, .50f, .48f, .18f, stroke);
                    if (y > .44f) smile = 1f;
                    return Mathf.Max(face, -Mathf.Min(eyes, smile));
                }

                case IconKind.Coins:
                    return Mathf.Min(
                        Ring(x, y, .42f, .58f, .22f, stroke),
                        Ring(x, y, .58f, .40f, .22f, stroke));

                case IconKind.Grid:
                    return Mathf.Min(
                        Mathf.Min(BoxOutline(x, y, .33f, .67f, .13f, .13f, .03f, stroke), BoxOutline(x, y, .67f, .67f, .13f, .13f, .03f, stroke)),
                        Mathf.Min(BoxOutline(x, y, .33f, .33f, .13f, .13f, .03f, stroke), BoxOutline(x, y, .67f, .33f, .13f, .13f, .03f, stroke)));

                case IconKind.Phone:
                    return Mathf.Min(
                        BoxOutline(x, y, .50f, .50f, .22f, .34f, .05f, stroke),
                        Mathf.Min(Seg(x, y, .43f, .76f, .57f, .76f, stroke * .75f), Circle(x, y, .50f, .24f, .035f)));

                case IconKind.Bag:
                {
                    float body = BoxOutline(x, y, .50f, .40f, .28f, .22f, .04f, stroke);
                    float handle = Ring(x, y, .50f, .62f, .12f, stroke);
                    if (y < .62f) handle = 1f;
                    return Mathf.Min(body, handle);
                }

                case IconKind.Pin:
                    return Mathf.Min(
                        Mathf.Min(Ring(x, y, .50f, .58f, .22f, stroke), Circle(x, y, .50f, .58f, .07f)),
                        Mathf.Min(Seg(x, y, .31f, .47f, .50f, .18f, stroke), Seg(x, y, .69f, .47f, .50f, .18f, stroke)));

                case IconKind.Tasks:
                    return Mathf.Min(
                        Mathf.Min(BoxOutline(x, y, .25f, .65f, .07f, .07f, .01f, stroke * .85f), Seg(x, y, .42f, .65f, .80f, .65f, stroke)),
                        Mathf.Min(BoxOutline(x, y, .25f, .35f, .07f, .07f, .01f, stroke * .85f), Seg(x, y, .42f, .35f, .80f, .35f, stroke)));

                default:
                    return 1f;
            }
        }

        private static float Circle(float x, float y, float cx, float cy, float r) =>
            Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        private static float Ring(float x, float y, float cx, float cy, float r, float halfWidth) =>
            Mathf.Abs(Circle(x, y, cx, cy, r)) - halfWidth;

        private static float BoxFill(float x, float y, float cx, float cy, float hx, float hy, float r = .01f)
        {
            float dx = Mathf.Abs(x - cx) - hx + r;
            float dy = Mathf.Abs(y - cy) - hy + r;
            return Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)) +
                   Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        }

        private static float BoxOutline(float x, float y, float cx, float cy, float hx, float hy, float r, float halfWidth) =>
            Mathf.Abs(BoxFill(x, y, cx, cy, hx, hy, r)) - halfWidth;

        private static float Seg(float px, float py, float ax, float ay, float bx, float by, float r)
        {
            float vx = bx - ax, vy = by - ay;
            float wx = px - ax, wy = py - ay;
            float t = Mathf.Clamp01((wx * vx + wy * vy) / (vx * vx + vy * vy + 1e-6f));
            float dx = wx - vx * t, dy = wy - vy * t;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        private static float Triangle(float px, float py, float x0, float y0, float x1, float y1, float x2, float y2)
        {
            float d0 = Seg(px, py, x0, y0, x1, y1, 0f);
            float d1 = Seg(px, py, x1, y1, x2, y2, 0f);
            float d2 = Seg(px, py, x2, y2, x0, y0, 0f);
            float minEdge = Mathf.Min(d0, Mathf.Min(d1, d2));
            float c0 = (x1 - x0) * (py - y0) - (y1 - y0) * (px - x0);
            float c1 = (x2 - x1) * (py - y1) - (y2 - y1) * (px - x1);
            float c2 = (x0 - x2) * (py - y2) - (y0 - y2) * (px - x2);
            bool inside = (c0 >= 0f && c1 >= 0f && c2 >= 0f) || (c0 <= 0f && c1 <= 0f && c2 <= 0f);
            return inside ? -minEdge : minEdge;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < ownedAssets.Count; i++)
            {
                if (ownedAssets[i] != null)
                    Destroy(ownedAssets[i]);
            }
            ownedAssets.Clear();
        }
    }
}
