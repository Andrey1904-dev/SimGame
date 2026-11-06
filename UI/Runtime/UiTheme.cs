// Life From Zero — UI/Runtime (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// Палитра, типографика и геометрия интерфейсов.
//
// ГЕОМЕТРИЯ МЕНЮ ПАУЗЫ ИЗМЕРЕНА по `Pause Menu.PNG` (1672×941) и переведена
// в нормализованные координаты (0..1). Пиксели указаны в комментариях, чтобы
// расхождения можно было проверить, а не «подбирать на глаз».
//
// Все размеры — доли экрана, поэтому меню корректно масштабируется на 1600×900,
// 1920×1080 и 2560×1440 (ТЗ §16).

using LifeFromZero.UI.Core;
using UnityEngine;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Палитра и метрики интерфейсов.</summary>
    public static class UiTheme
    {
        // ------------------------------------------------------------------ цвета меню паузы
        // Значения сняты с референса: панели почти чёрные с прозрачностью, кнопки светлее.
        public static readonly Color PausePanel = new Color(0.075f, 0.075f, 0.075f, 0.92f);
        public static readonly Color PauseSubPanel = new Color(0.115f, 0.115f, 0.115f, 0.86f);
        public static readonly Color PauseButton = new Color(0.145f, 0.145f, 0.145f, 0.96f);
        public static readonly Color PauseButtonHover = new Color(0.20f, 0.20f, 0.20f, 0.98f);
        public static readonly Color PauseButtonPressed = new Color(0.115f, 0.115f, 0.115f, 1f);
        public static readonly Color PauseBorder = new Color(0.42f, 0.42f, 0.42f, 0.50f);
        public static readonly Color Divider = new Color(1f, 1f, 1f, 0.08f);

        public static readonly Color TextPrimary = new Color(0.96f, 0.96f, 0.96f, 1f);
        public static readonly Color TextMuted = new Color(0.75f, 0.75f, 0.75f, 1f);
        public static readonly Color TextDisabled = new Color(0.58f, 0.58f, 0.58f, 0.72f);

        /// <summary>Акцент логотипа LIFE FROM ZERO.</summary>
        public static readonly Color AccentRed = new Color(0.90f, 0.07f, 0.11f, 1f);

        public static readonly Color Debt = new Color(0.94f, 0.20f, 0.22f, 1f);
        public static readonly Color Positive = new Color(0.34f, 0.85f, 0.22f, 1f);

        // Полосы статуса (как в референсе: зелёный/жёлтый/голубой/зелёный).
        public static readonly Color BarHealth = new Color(0.30f, 0.80f, 0.18f, 1f);
        public static readonly Color BarSatiety = new Color(0.98f, 0.75f, 0.04f, 1f);
        public static readonly Color BarEnergy = new Color(0.16f, 0.68f, 0.92f, 1f);
        public static readonly Color BarMood = new Color(0.30f, 0.80f, 0.18f, 1f);
        public static readonly Color BarTrack = new Color(0.22f, 0.22f, 0.22f, 0.95f);

        public static readonly Color IconHeart = new Color(0.92f, 0.22f, 0.26f, 1f);
        public static readonly Color IconFork = new Color(0.96f, 0.74f, 0.10f, 1f);
        public static readonly Color IconBolt = new Color(0.98f, 0.76f, 0.08f, 1f);
        public static readonly Color IconSmile = new Color(0.96f, 0.74f, 0.10f, 1f);

        // ------------------------------------------------------------------ телефон
        public static readonly Color PhoneBody = new Color(0.055f, 0.055f, 0.06f, 1f);
        public static readonly Color PhoneBezel = new Color(0.18f, 0.18f, 0.19f, 1f);
        public static readonly Color PhoneScreenTop = new Color(0.30f, 0.20f, 0.16f, 1f);
        public static readonly Color PhoneScreenBottom = new Color(0.10f, 0.07f, 0.07f, 1f);
        public static readonly Color PhoneCard = new Color(0.12f, 0.12f, 0.13f, 0.94f);
        public static readonly Color PhoneCardLight = new Color(0.17f, 0.17f, 0.18f, 0.96f);
        public static readonly Color PhoneTextMuted = new Color(0.72f, 0.72f, 0.74f, 1f);
        public static readonly Color PhoneAccent = new Color(0.98f, 0.78f, 0.16f, 1f);
        public static readonly Color BadgeRed = new Color(0.90f, 0.16f, 0.16f, 1f);
        public static readonly Color PhoneGreen = new Color(0.30f, 0.78f, 0.36f, 1f);
        public static readonly Color PhoneRedButton = new Color(0.86f, 0.20f, 0.18f, 1f);

        // Иконки приложений (как в референсе).
        public static readonly Color AppCalls = new Color(0.26f, 0.62f, 0.42f, 1f);
        public static readonly Color AppMessages = new Color(0.26f, 0.55f, 0.85f, 1f);
        public static readonly Color AppContacts = new Color(0.35f, 0.35f, 0.37f, 1f);
        public static readonly Color AppBank = new Color(0.72f, 0.18f, 0.18f, 1f);
        public static readonly Color AppOrders = new Color(0.82f, 0.62f, 0.16f, 1f);
        public static readonly Color AppTasks = new Color(0.28f, 0.52f, 0.78f, 1f);
        public static readonly Color AppMap = new Color(0.22f, 0.66f, 0.42f, 1f);
        public static readonly Color AppAds = new Color(0.80f, 0.45f, 0.18f, 1f);
        public static readonly Color AppEducation = new Color(0.25f, 0.45f, 0.72f, 1f);
        public static readonly Color AppGallery = new Color(0.24f, 0.52f, 0.68f, 1f);
        public static readonly Color AppSettings = new Color(0.42f, 0.42f, 0.45f, 1f);

        // ------------------------------------------------------------------ карта
        public static readonly Color MapBackground = new Color(0.09f, 0.11f, 0.13f, 1f);
        public static readonly Color MapPanel = new Color(0.10f, 0.12f, 0.14f, 0.95f);
        public static readonly Color MapPlayer = new Color(0.25f, 0.62f, 0.98f, 1f);
        public static readonly Color MapRoute = new Color(0.20f, 0.62f, 0.96f, 1f);
        public static readonly Color MapOrder = new Color(0.96f, 0.72f, 0.18f, 1f);
        public static readonly Color MapHome = new Color(0.94f, 0.30f, 0.32f, 1f);
        public static readonly Color MapPoi = new Color(0.72f, 0.78f, 0.86f, 1f);
        public static readonly Color MapManual = new Color(0.98f, 0.42f, 0.20f, 1f);

        // ------------------------------------------------------------------ HUD
        public static readonly Color HudPanel = new Color(0.05f, 0.06f, 0.07f, 0.62f);
        public static readonly Color HudBorder = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color HudNavPanel = new Color(0.06f, 0.10f, 0.16f, 0.72f);

        // ------------------------------------------------------------------ геометрия паузы
        // Все числа берутся из LifeFromZero.UI.Core.PauseLayout — единственного места,
        // где они измерены по `UI/References/Pause Menu.PNG` и проверены тестом.
        // Здесь только удобные обёртки, чтобы окна не считали дроби на месте.

        /// <summary>Пиксели кадра → единицы uGUI (1920×1080).</summary>
        public const float PauseScale = PauseLayout.Scale;

        /// <summary>Кегль из пикселей кадра (см. PauseLayout.FontSize).</summary>
        public static int Px(float framePixels) => PauseLayout.FontSize(framePixels);

        /// <summary>Прямоугольник кадра → якоря uGUI.</summary>
        public static AnchorRect Anchors(LayoutRect rect) => PauseLayout.ToAnchors(rect);

        /// <summary>Прямоугольник внутри родителя → якоря относительно родителя.</summary>
        public static AnchorRect Anchors(LayoutRect rect, LayoutRect parent) => PauseLayout.ToAnchors(rect, parent);

        /// <summary>Ширина/высота элемента кадра в единицах uGUI.</summary>
        public static float Units(float framePixels) => PauseLayout.Units(framePixels);

        // Кегли (px кадра).
        public const float FontMenuButton = PauseLayout.FontButton;
        public const float FontSection = PauseLayout.FontSection;
        public const float FontBody = PauseLayout.FontBody;
        public const float FontSmall = PauseLayout.FontSmall;
        public const float FontLogo = PauseLayout.FontLogo;
        public const float FontHeader = PauseLayout.FontHeader;
        public const float FontTitle = PauseLayout.FontTitle;

        public const float CornerRadiusSmall = 6f;
        public const float CornerRadiusPanel = 14f;
        public const float CornerRadiusButton = 10f;

        /// <summary>Единый шрифт UI. LegacyRuntime/Arial есть в Unity всегда.</summary>
        public static Font Font()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }
    }
}
