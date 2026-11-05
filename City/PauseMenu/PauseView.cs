using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace LifeFromZero.City.Pause
{
    // Constructed once, retained across openings and scenes. All dimensions are relative
    // to an aspect-fitted 1568x882 reference area, so no control leaves the screen.
    public sealed class PauseView : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(.07f, .07f, .07f, .94f);
        private static readonly Color SubColor = new Color(.12f, .12f, .12f, .88f);
        private static readonly Color BorderColor = new Color(.42f, .42f, .42f, .6f);
        private static readonly Color White = new Color(.96f, .96f, .96f, 1f);
        private Font font;
        private Sprite rounded;
        private Texture2D texture;
        private Text day, time, location, locationDetail;
        private readonly Text[] goals = new Text[3], goalMarks = new Text[3];
        private readonly Text[] statusValues = new Text[4], financeValues = new Text[3];
        private readonly Image[] bars = new Image[4];
        private readonly Button[] actions = new Button[9];
        private readonly Text[] actionTexts = new Text[9];
        private PauseController controller;

        public static PauseView Create(Transform parent, PauseController owner)
        {
            var go = new GameObject("Pause Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PauseView));
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<PauseView>(); v.controller = owner; v.Build();
            go.SetActive(false); return v;
        }
        private void Build()
        {
            var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1568, 882); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rounded = MakeRounded();
            var root = Rect("Safe frame", transform, 0, 0, 1, 1);
            var fitter = root.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 1568f / 882f;
            // A light transparent scrim; game remains visible, no full-frame blur.
            ImageBox("Scrim", root, 0, 0, 1, 1, new Color(0, 0, 0, .28f), false);
            var left = Panel("Menu", root, .02f, .16f, .355f, .96f, PanelColor);
            Label(left, "LIFE FROM", .13f, .86f, .57f, .96f, 45, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft, White);
            Label(left, "ZERO", .57f, .86f, .87f, .96f, 47, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft, new Color(.9f, .05f, .1f));
            ImageBox("Logo underline", left, .13f, .851f, .86f, .858f, new Color(.85f, .04f, .08f), false);
            Label(left, "Пауза", .13f, .78f, .8f, .85f, 32, FontStyle.Bold, TextAnchor.MiddleLeft, White);
            string[] menu = { "▶   Продолжить игру", "▣   Сохранить игру", "⇧   Загрузить игру", "⚙   Настройки", "⌨   Управление", "⌂   Выйти в главное меню", "⏻   Выйти из игры" };
            for (int i = 0; i < menu.Length; i++)
            {
                float top = .73f - i * .096f;
                int n = i;
                var button = MakeButton(left, menu[i], .075f, top - .082f, .93f, top, () => {
                    if (n == 0) controller.Close();
                    else if (n == 6) controller.Quit();
                    else controller.Invoke((PauseAction)(n - 1));
                }, out var text);
                if (i >= 1 && i <= 5) { actions[i - 1] = button; actionTexts[i - 1] = text; }
            }
            var right = Panel("Dashboard", root, .37f, .22f, .963f, .86f, PanelColor);
            var header = Panel("Header", right, .025f, .845f, .975f, .975f, SubColor);
            day = Label(header, "Нет данных", .025f, .16f, .39f, .85f, 25, FontStyle.Normal, TextAnchor.MiddleLeft, White);
            time = Label(header, "Нет данных", .4f, .16f, .59f, .85f, 25, FontStyle.Normal, TextAnchor.MiddleLeft, White);
            location = Label(header, "Нет данных", .64f, .46f, .98f, .9f, 23, FontStyle.Normal, TextAnchor.MiddleLeft, White);
            locationDetail = Label(header, "", .64f, .08f, .98f, .48f, 18, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(.75f,.75f,.75f));
            var goalPanel = Panel("Goals", right, .025f, .435f, .49f, .815f, SubColor);
            Heading(goalPanel, "◎   Цели");
            for (int i = 0; i < 3; i++)
            {
                float y = .66f - .19f * i;
                goalMarks[i] = Label(goalPanel, "□", .06f, y - .08f, .15f, y + .08f, 24, FontStyle.Normal, TextAnchor.MiddleLeft, White);
                goals[i] = Label(goalPanel, "", .17f, y - .08f, .95f, y + .08f, 20, FontStyle.Normal, TextAnchor.MiddleLeft, White);
            }
            var status = Panel("Status", right, .505f, .435f, .975f, .815f, SubColor);
            Heading(status, "▮   Статус");
            string[] names = { "♥  Здоровье", "♨  Сытость", "ϟ  Энергия", "●  Настроение" };
            Color[] colors = { new Color(.3f,.8f,.18f), new Color(1f,.76f,.02f), new Color(.15f,.68f,.9f), new Color(.3f,.8f,.18f) };
            for (int i = 0; i < 4; i++)
            {
                float y = .67f - .165f * i;
                Label(status, names[i], .05f, y - .07f, .40f, y + .07f, 18, FontStyle.Normal, TextAnchor.MiddleLeft, White);
                ImageBox("Track", status, .42f, y - .04f, .81f, y + .04f, new Color(.25f,.25f,.25f), true);
                var bar = ImageBox("Fill", status, .42f, y - .04f, .81f, y + .04f, colors[i], true);
                bar.type = Image.Type.Filled; bar.fillMethod = Image.FillMethod.Horizontal; bars[i] = bar;
                statusValues[i] = Label(status, "—", .82f, y - .07f, .97f, y + .07f, 18, FontStyle.Normal, TextAnchor.MiddleRight, White);
            }
            var finance = Panel("Finance", right, .025f, .035f, .425f, .405f, SubColor);
            Heading(finance, "◉   Финансы");
            string[] fin = { "Деньги:", "Долг:", "Доход за день:" };
            for (int i = 0; i < 3; i++)
            {
                float y = .63f - .20f * i;
                Label(finance, fin[i], .08f, y - .08f, .59f, y + .08f, 21, FontStyle.Normal, TextAnchor.MiddleLeft, White);
                financeValues[i] = Label(finance, "Нет данных", .53f, y - .08f, .94f, y + .08f, 21, FontStyle.Normal, TextAnchor.MiddleRight, White);
            }
            var links = Panel("Quick links", right, .44f, .035f, .975f, .405f, SubColor);
            Heading(links, "▦   Быстрые ссылки");
            string[] quick = { "▯   Телефон", "♧   Инвентарь", "◇   Карта", "☷   Задания" };
            for (int i = 0; i < 4; i++)
            {
                int n = i; float x = i % 2 == 0 ? .04f : .52f; float y = i < 2 ? .48f : .15f;
                actions[5+i] = MakeButton(links, quick[i], x, y, x + .44f, y + .25f,
                    () => controller.Invoke((PauseAction)(5+n)), out actionTexts[5+i]);
            }
            Label(root, "LIFE FROM ZERO", .04f, .03f, .25f, .07f, 16, FontStyle.Normal, TextAnchor.MiddleLeft, White);
            Label(root, "Esc   Закрыть меню", .76f, .035f, .97f, .07f, 17, FontStyle.Normal, TextAnchor.MiddleRight, White);
        }
        public void Render(PauseSnapshot snapshot, PauseIntegration integration)
        {
            var s = snapshot ?? new PauseSnapshot();
            day.text = "▣   " + OrMissing(s.Day); time.text = "◷   " + OrMissing(s.Time);
            location.text = "⌂   " + OrMissing(s.Location); locationDetail.text = s.LocationDetail ?? "";
            for (int i = 0; i < goals.Length; i++)
            {
                var goal = s.Goals != null && i < s.Goals.Length ? s.Goals[i] : null;
                goalMarks[i].text = goal == null ? "" : goal.Complete ? "☑" : "□";
                goalMarks[i].color = goal != null && goal.Complete ? new Color(.35f,.85f,.22f) : White;
                goals[i].text = goal == null ? (i == 0 ? "Нет данных" : "") : OrMissing(goal.Title) + (string.IsNullOrEmpty(goal.Progress) ? "" : "  (" + goal.Progress + ")");
            }
            float?[] values = { s.Health, s.Hunger, s.Energy, s.Mood };
            for (int i = 0; i < 4; i++)
            {
                bool valid = values[i].HasValue && !float.IsNaN(values[i].Value) && !float.IsInfinity(values[i].Value);
                float value = valid ? Mathf.Clamp(values[i].Value, 0, 100) : 0;
                bars[i].fillAmount = value / 100f; statusValues[i].text = valid ? value.ToString("0", CultureInfo.InvariantCulture) + "%" : "—";
            }
            financeValues[0].text = Currency(s.Money); financeValues[1].text = Currency(s.Debt); financeValues[2].text = Currency(s.DailyIncome);
            financeValues[1].color = s.Debt.HasValue ? new Color(.95f,.17f,.2f) : White;
            financeValues[2].color = s.DailyIncome.HasValue ? new Color(.36f,.86f,.22f) : White;
            string[] labels = { "▣   Сохранить игру", "⇧   Загрузить игру", "⚙   Настройки", "⌨   Управление", "⌂   Выйти в главное меню", "▯   Телефон", "♧   Инвентарь", "◇   Карта", "☷   Задания" };
            for (int i = 0; i < actions.Length; i++)
            {
                var action = (PauseAction)i;
                bool available = integration != null && integration.IsAvailable(action);
                actions[i].interactable = available;
                string key = i == 5 ? "P" : i >= 6 && integration != null ? integration.ShortcutLabel(action) : null;
                actionTexts[i].text = labels[i] + (!string.IsNullOrEmpty(key) ? "  [" + key + "]" : "") + (!available ? "  —" : "");
            }
        }
        private static string OrMissing(string value) => string.IsNullOrWhiteSpace(value) ? "Нет данных" : value;
        private static string Currency(decimal? value) => value.HasValue ? value.Value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU")) + " ₽" : "Нет данных";
        private void Heading(RectTransform parent, string text) => Label(parent, text, .05f, .78f, .96f, .96f, 25, FontStyle.Bold, TextAnchor.MiddleLeft, White);
        private Button MakeButton(RectTransform parent, string text, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction click, out Text label)
        {
            var r = Panel("Button", parent, x0, y0, x1, y1, new Color(.14f,.14f,.14f,.96f));
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = r.GetComponent<Image>(); button.targetGraphic.raycastTarget = true;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.16f,1.16f,1.16f); colors.pressedColor = new Color(.8f,.8f,.8f); colors.selectedColor = new Color(1.08f,1.08f,1.08f); colors.disabledColor = new Color(.65f,.65f,.65f); button.colors = colors;
            button.onClick.AddListener(click);
            label = Label(r, text, .07f, .06f, .95f, .94f, 24, FontStyle.Normal, TextAnchor.MiddleLeft, White);
            return button;
        }
        private RectTransform Panel(string name, Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var edge = ImageBox(name + " border", parent, x0, y0, x1, y1, BorderColor, true).rectTransform;
            // Inner content also serves as button target where applicable.
            var inside = ImageBox(name, edge, .002f, .004f, .998f, .996f, color, true).rectTransform;
            return inside;
        }
        private Text Label(Transform parent, string value, float x0, float y0, float x1, float y1, int size, FontStyle style, TextAnchor anchor, Color color)
        {
            var r = Rect("Text", parent, x0, y0, x1, y1); var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.text = value; t.fontSize = size; t.fontStyle = style; t.alignment = anchor; t.color = color; t.raycastTarget = false;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 9; t.resizeTextMaxSize = size; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }
        private Image ImageBox(string name, Transform parent, float x0, float y0, float x1, float y1, Color color, bool round)
        {
            var r = Rect(name, parent, x0, y0, x1, y1); var img = r.gameObject.AddComponent<Image>();
            img.color = color; img.raycastTarget = false;
            if (round) { img.sprite = rounded; img.type = Image.Type.Sliced; }
            return img;
        }
        private static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = new Vector2(x0,y0); r.anchorMax = new Vector2(x1,y1); r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; return r;
        }
        private Sprite MakeRounded()
        {
            const int size = 32, radius = 8;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false); texture.name = "Pause rounded UI";
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - .5f, 0, x - (size - radius) + .5f);
                float dy = Mathf.Max(radius - y - .5f, 0, y - (size - radius) + .5f);
                byte a = (byte)(Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx*dx + dy*dy)) * 255);
                pixels[y * size + x] = new Color32(255,255,255,a);
            }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f,.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius,radius,radius,radius));
        }
        private void OnDestroy() { if (rounded != null) Destroy(rounded); if (texture != null) Destroy(texture); }
    }
}
