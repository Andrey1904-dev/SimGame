// Life From Zero — UI/Runtime (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// Мелкие фабрики uGUI-элементов, чтобы контроллеры описывали ВЁРСТКУ, а не
// повторяли создание RectTransform/Image/Text. Все окна строятся из настоящих
// Unity-компонентов (ТЗ §1: не вставлять скриншоты интерфейсов).

using LifeFromZero.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Хелперы построения uGUI.</summary>
    public static class UiBuilder
    {
        /// <summary>Создать корневой Canvas окна с корректным масштабированием.</summary>
        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        /// <summary>Прямоугольник на нормализованных якорях.</summary>
        public static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Прямоугольник по нормализованным якорям (структура из UI/Core.PauseLayout).</summary>
        public static RectTransform At(string name, Transform parent, AnchorRect anchors)
        {
            return Rect(name, parent, anchors.XMin, anchors.YMin, anchors.XMax, anchors.YMax);
        }

        /// <summary>Прямоугольник по пикселям кадра-референса внутри родителя.</summary>
        public static RectTransform At(string name, Transform parent, LayoutRect rect, LayoutRect parentRect)
        {
            return At(name, parent, PauseLayout.ToAnchors(rect, parentRect));
        }

        /// <summary>Прямоугольник по пикселям относительно референса 1920×1080.</summary>
        public static RectTransform PixelRect(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>Растянутый прямоугольник с отступами в пикселях референса.</summary>
        public static RectTransform Inset(RectTransform parent, float left, float top, float right, float bottom)
        {
            var go = new GameObject("Inset", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>
        /// Слой собственной uGUI-графики (сетка карты, маршрут): RectTransform +
        /// ГАРАНТИРОВАННЫЙ CanvasRenderer + компонент <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// ТЗ №045. В Play Mode Unity выдавал
        /// «MissingComponentException: There is no 'CanvasRenderer' attached to the
        /// "Grid"/"Route" game object» сотни раз. Причина — Graphic без CanvasRenderer:
        /// uGUI перестраивает такой слой на каждом обновлении канваса и падает каждый раз.
        /// Поэтому здесь рендерер проверяется ЯВНО до добавления графики, а не
        /// полагаемся только на [RequireComponent] базового класса: если Unity по какой-то
        /// причине его не добавил, слой отключается с ОДНИМ понятным сообщением, а не
        /// заливает консоль исключениями.
        /// </remarks>
        public static T GraphicLayer<T>(string name, Transform parent,
            float x0, float y0, float x1, float y1, out bool canvasRendererAdded) where T : Graphic
        {
            RectTransform rect = Rect(name, parent, x0, y0, x1, y1);
            return AttachGraphic<T>(rect, out canvasRendererAdded);
        }

        /// <summary>Добавить uGUI-графику на готовый RectTransform с проверкой CanvasRenderer.</summary>
        public static T AttachGraphic<T>(RectTransform rect, out bool canvasRendererAdded) where T : Graphic
        {
            canvasRendererAdded = false;
            if (rect == null) return null;

            GameObject go = rect.gameObject;
            if (go.GetComponent<CanvasRenderer>() == null)
            {
                try
                {
                    go.AddComponent<CanvasRenderer>();
                    canvasRendererAdded = go.GetComponent<CanvasRenderer>() != null;
                }
                catch (System.Exception exception)
                {
                    Debug.LogError("[UI] Не удалось добавить CanvasRenderer объекту «" + rect.name +
                                   "»: " + exception.Message);
                }
            }

            T graphic = go.AddComponent<T>();

            if (go.GetComponent<CanvasRenderer>() == null)
            {
                Debug.LogError("[UI] Объект «" + rect.name + "» получил " + typeof(T).Name +
                               " без CanvasRenderer — uGUI не сможет его нарисовать, слой ОТКЛЮЧЁН. " +
                               "Проверьте объект в сцене (Tools → Life From Zero → UI → " +
                               "Verify Scenes & Adapters) и пересоздайте компонент.");
                graphic.enabled = false;
            }

            graphic.raycastTarget = false;
            return graphic;
        }

        /// <summary>Подложка-изображение (по умолчанию без рейкаста).</summary>
        public static Image Image(string name, Transform parent, float x0, float y0, float x1, float y1,
            Color color, Sprite sprite = null, bool raycast = false)
        {
            RectTransform rect = Rect(name, parent, x0, y0, x1, y1);
            return Fill(rect, color, sprite, raycast);
        }

        /// <summary>Подложка по пикселям кадра-референса внутри родителя.</summary>
        public static Image ImageAt(string name, Transform parent, LayoutRect rect, LayoutRect parentRect,
            Color color, Sprite sprite = null, bool raycast = false)
        {
            return Fill(At(name, parent, rect, parentRect), color, sprite, raycast);
        }

        /// <summary>Текст по пикселям кадра-референса внутри родителя.</summary>
        public static Text LabelAt(string name, Transform parent, LayoutRect rect, LayoutRect parentRect,
            string value, float fontPixels, Color color, TextAnchor anchor = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal)
        {
            return TextOn(At(name, parent, rect, parentRect), value, PauseLayout.FontSize(fontPixels),
                color, anchor, style);
        }

        /// <summary>Добавить Image в существующий RectTransform.</summary>
        public static Image Fill(RectTransform rect, Color color, Sprite sprite = null, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = HasBorder(sprite) ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            }
            return image;
        }

        /// <summary>У спрайта есть 9-slice границы (тогда Image работает как Sliced).</summary>
        private static bool HasBorder(Sprite sprite)
        {
            if (sprite == null) return false;
            Vector4 border = sprite.border;
            return border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f;
        }

        /// <summary>Текст с единым шрифтом.</summary>
        public static Text Label(string name, Transform parent, string value, float x0, float y0, float x1, float y1,
            int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = Rect(name, parent, x0, y0, x1, y1);
            return TextOn(rect, value, fontSize, color, anchor, style);
        }

        /// <summary>Добавить Text в существующий RectTransform.</summary>
        public static Text TextOn(RectTransform rect, string value, int fontSize, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = UiTheme.Font();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            return text;
        }

        /// <summary>Иконка.</summary>
        public static Image Icon(string name, Transform parent, UiSpriteCache cache, UiIconKind kind,
            float x0, float y0, float x1, float y1, Color color)
        {
            Image image = Image(name, parent, x0, y0, x1, y1, color, cache != null ? cache.Icon(kind) : null);
            image.preserveAspect = true;
            return image;
        }

        /// <summary>Кнопка с иконкой и подписью (стиль меню паузы).</summary>
        public static Button Button(string name, RectTransform parent, UiSpriteCache cache, UiIconKind icon,
            string caption, float x0, float y0, float x1, float y1, out Text label, out Image iconImage,
            UnityEngine.Events.UnityAction onClick, float radius = 8, float fontSize = 22f)
        {
            RectTransform rect = Rect(name, parent, x0, y0, x1, y1);
            Image background = Fill(rect, UiTheme.PauseButton,
                cache != null ? cache.Rounded(Mathf.RoundToInt(radius)) : null, true);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.28f, 1.28f, 1.28f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.62f, 0.62f, 0.62f, 0.85f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);

            iconImage = Icon("Icon", rect, cache, icon, 0.05f, 0.18f, 0.135f, 0.82f, UiTheme.TextPrimary);
            label = Label("Label", rect, caption, 0.17f, 0.06f, 0.95f, 0.94f,
                Mathf.RoundToInt(fontSize), UiTheme.TextPrimary, TextAnchor.MiddleLeft);
            return button;
        }

        /// <summary>Простая кнопка-плашка с текстом по центру.</summary>
        public static Button PillButton(string name, RectTransform parent, UiSpriteCache cache, string caption,
            float x0, float y0, float x1, float y1, Color background, Color textColor,
            UnityEngine.Events.UnityAction onClick, float radius = 10, float fontSize = 20f)
        {
            RectTransform rect = Rect(name, parent, x0, y0, x1, y1);
            Image image = Fill(rect, background, cache != null ? cache.Rounded(Mathf.RoundToInt(radius)) : null, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            Label("Label", rect, caption, 0.04f, 0.05f, 0.96f, 0.95f,
                Mathf.RoundToInt(fontSize), textColor, TextAnchor.MiddleCenter);
            return button;
        }

        /// <summary>Панель с тонкой обводкой (как в референсе меню паузы).</summary>
        public static RectTransform Panel(string name, RectTransform parent, UiSpriteCache cache,
            float x0, float y0, float x1, float y1, Color color, float radius = 18, bool border = true)
        {
            RectTransform outer = Rect(name, parent, x0, y0, x1, y1);
            if (border)
            {
                Fill(outer, UiTheme.PauseBorder, cache != null ? cache.Rounded(Mathf.RoundToInt(radius) + 1) : null);
                RectTransform inner = Rect(name + " fill", outer, 0f, 0f, 1f, 1f);
                inner.offsetMin = new Vector2(1.5f, 1.5f);
                inner.offsetMax = new Vector2(-1.5f, -1.5f);
                Fill(inner, color, cache != null ? cache.Rounded(Mathf.RoundToInt(radius)) : null);
                return inner;
            }
            Fill(outer, color, cache != null ? cache.Rounded(Mathf.RoundToInt(radius)) : null);
            return outer;
        }

        /// <summary>Полоса прогресса: фон + заполнение (Filled Image) с «шапкой».</summary>
        public static Image ProgressBar(string name, RectTransform parent, UiSpriteCache cache,
            float x0, float y0, float x1, float y1, Color fillColor, out Image track)
        {
            track = Image(name + " track", parent, x0, y0, x1, y1, UiTheme.BarTrack,
                cache != null ? cache.Rounded(10) : null);
            Image fill = Image(name + " fill", parent, x0, y0, x1, y1, fillColor, cache != null ? cache.Rounded(10) : null);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            return fill;
        }

        /// <summary>Горизонтальная линия-разделитель.</summary>
        public static Image Divider(string name, RectTransform parent, float x0, float y, float x1)
        {
            return Image(name, parent, x0, y, x1, y + 0.0016f, UiTheme.Divider);
        }

        /// <summary>Красный круглый бейдж с числом.</summary>
        public static Text Badge(string name, RectTransform parent, UiSpriteCache cache, string value,
            float x0, float y0, float x1, float y1)
        {
            Image background = Image(name, parent, x0, y0, x1, y1, UiTheme.BadgeRed,
                cache != null ? cache.Rounded(16) : null);
            background.raycastTarget = false;
            return Label(name + " text", background.rectTransform, value, 0.05f, 0.05f, 0.95f, 0.95f,
                16, UiTheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        /// <summary>Гарантирует единственный EventSystem в сцене.</summary>
        public static void EnsureEventSystem(Transform parent)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            var go = new GameObject("UI EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            go.transform.SetParent(parent, false);
#if ENABLE_INPUT_SYSTEM
            System.Type module = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null)
            {
                go.AddComponent(module);
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#else
            Debug.LogWarning("[UI] Не найден модуль ввода UI: добавьте EventSystem вручную.");
#endif
        }

        /// <summary>Полностью удалить объект вместе с компонентами.</summary>
        public static void Destroy(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
