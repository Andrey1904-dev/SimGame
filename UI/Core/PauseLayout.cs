// Life From Zero — UI/Core (ТЗ №042 §12, §16)
// Namespace: LifeFromZero.UI.Core
//
// Геометрия и типографика меню паузы — ЧИСЛА, а не «на глаз».
//
// Источник: `UI/References/Pause Menu.PNG`, кадр 1672×941.
// Границы получены сканированием яркости кадра (перепады на кромках панелей и
// кнопок), габариты текста — по bbox ярких пикселей в каждой строке:
//
//   левая панель      x  33..595   y  36..788
//   правая панель     x 622..1608  y 127..733
//   логотип           x 140..470   y  80..140  (подчёркивание y 120..139)
//   заголовок «Пауза» x 111..205   y 172..202  (высота литер 31 px)
//   кнопки меню       x  75..553,  верх первой 241, высота 67, шаг 73.5 (7 строк)
//   иконка кнопки     x 117..142,  текст с x 185, высота литер 25 px
//   шапка (день/время) x 645..1225 y 141..206
//   шапка (адрес)     x 1220..1593 y 141..206
//   цели              x 648..1105  y 234..472
//   статус            x 1120..1590 y 234..472, полосы x 1315..1500,
//                     строки y 322/363/404/445, высота полосы 20 px
//   финансы           x 648..1063  y 490..711
//   быстрые ссылки    x 1070..1588 y 490..711,
//                     кнопки 1081..1310 / 1332..1561, ряды 556..619 / 629..692
//   проценты          x 1520..1566 (правое выравнивание до 1566)
//   подвал            «v0.2.0 (alpha)» y 882..899; подсказки y 859..891
//
// Кегли (px кадра): заголовок 36, кнопка 26, заголовок блока 27,
// строка списка/поля 21, мелкий текст 18, шапка 27/19, логотип 44.
//
// Модуль без UnityEngine — используется и рантаймом, и тестами, поэтому
// раскладка проверяется автоматически (см. Validate и UI/Tests).

using System;
using System.Collections.Generic;
using System.Globalization;

namespace LifeFromZero.UI.Core
{
    /// <summary>Прямоугольник в пикселях кадра, начало координат — левый верхний угол.</summary>
    public readonly struct LayoutRect : IEquatable<LayoutRect>
    {
        public LayoutRect(float left, float top, float width, float height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public float Left { get; }

        public float Top { get; }

        public float Width { get; }

        public float Height { get; }

        public float Right => Left + Width;

        public float Bottom => Top + Height;

        public float CenterX => Left + Width * 0.5f;

        public float CenterY => Top + Height * 0.5f;

        public bool Contains(LayoutRect other) =>
            other.Left >= Left - 0.001f && other.Top >= Top - 0.001f &&
            other.Right <= Right + 0.001f && other.Bottom <= Bottom + 0.001f;

        /// <summary>Пересечение по площади (касание кромками не считается).</summary>
        public bool Overlaps(LayoutRect other) =>
            Math.Min(Right, other.Right) - Math.Max(Left, other.Left) > 0.5f &&
            Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top) > 0.5f;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "[{0:0.#},{1:0.#} {2:0.#}×{3:0.#}]",
                Left, Top, Width, Height);

        public bool Equals(LayoutRect other) =>
            Left.Equals(other.Left) && Top.Equals(other.Top) &&
            Width.Equals(other.Width) && Height.Equals(other.Height);

        public override bool Equals(object obj) => obj is LayoutRect other && Equals(other);

        public override int GetHashCode() =>
            Left.GetHashCode() ^ (Top.GetHashCode() << 2) ^ (Width.GetHashCode() << 4) ^ (Height.GetHashCode() << 6);
    }

    /// <summary>Нормализованные якоря uGUI: (xMin, yMin) — левый низ, (xMax, yMax) — правый верх.</summary>
    public readonly struct AnchorRect : IEquatable<AnchorRect>
    {
        public AnchorRect(float xMin, float yMin, float xMax, float yMax)
        {
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
        }

        public float XMin { get; }

        public float YMin { get; }

        public float XMax { get; }

        public float YMax { get; }

        public float Width => XMax - XMin;

        public float Height => YMax - YMin;

        public bool IsInsideScreen =>
            XMin >= -0.0001f && YMin >= -0.0001f && XMax <= 1.0001f && YMax <= 1.0001f;

        public bool Overlaps(AnchorRect other) =>
            Math.Min(XMax, other.XMax) - Math.Max(XMin, other.XMin) > 0.0001f &&
            Math.Min(YMax, other.YMax) - Math.Max(YMin, other.YMin) > 0.0001f;

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "({0:0.0000},{1:0.0000})-({2:0.0000},{3:0.0000})", XMin, YMin, XMax, YMax);

        public bool Equals(AnchorRect other) =>
            XMin.Equals(other.XMin) && YMin.Equals(other.YMin) &&
            XMax.Equals(other.XMax) && YMax.Equals(other.YMax);

        public override bool Equals(object obj) => obj is AnchorRect other && Equals(other);

        public override int GetHashCode() =>
            XMin.GetHashCode() ^ (YMin.GetHashCode() << 2) ^ (XMax.GetHashCode() << 4) ^ (YMax.GetHashCode() << 6);
    }

    /// <summary>Измеренная раскладка меню паузы, её типографика и проверки.</summary>
    public static class PauseLayout
    {
        // ---------------------------------------------------------------- кадр

        /// <summary>Размер кадра-референса.</summary>
        public const float FrameWidth = 1672f;

        public const float FrameHeight = 941f;

        /// <summary>Референсное разрешение uGUI (CanvasScaler).</summary>
        public const float CanvasWidth = 1920f;

        public const float CanvasHeight = 1080f;

        /// <summary>Пиксели кадра → единицы uGUI (1920×1080).</summary>
        public const float Scale = CanvasWidth / FrameWidth;

        /// <summary>Минимальный размер интерактивной цели при 1080p (рекомендация доступности).</summary>
        public const float MinimumTouchTarget = 44f;

        /// <summary>Минимальный кегль при 1080p, чтобы текст читался на телефоне.</summary>
        public const float MinimumReadableFont = 18f;

        // ---------------------------------------------------------------- панели

        public static readonly LayoutRect LeftPanel = new LayoutRect(33f, 36f, 562f, 752f);

        public static readonly LayoutRect RightPanel = new LayoutRect(622f, 127f, 986f, 606f);

        public static readonly LayoutRect Logo = new LayoutRect(140f, 80f, 330f, 60f);

        public static readonly LayoutRect Title = new LayoutRect(111f, 166f, 200f, 42f);

        // ---------------------------------------------------------------- шапка

        public static readonly LayoutRect HeaderDate = new LayoutRect(645f, 141f, 570f, 65f);

        public static readonly LayoutRect HeaderAddress = new LayoutRect(1220f, 141f, 373f, 65f);

        public static readonly LayoutRect HeaderDateIcon = new LayoutRect(660f, 158f, 26f, 30f);

        public static readonly LayoutRect HeaderClockIcon = new LayoutRect(950f, 158f, 26f, 30f);

        public static readonly LayoutRect HeaderHomeIcon = new LayoutRect(1240f, 160f, 30f, 28f);

        // ---------------------------------------------------------------- блоки

        public static readonly LayoutRect Goals = new LayoutRect(648f, 234f, 457f, 238f);

        public static readonly LayoutRect Status = new LayoutRect(1120f, 234f, 470f, 238f);

        public static readonly LayoutRect Finance = new LayoutRect(648f, 490f, 415f, 221f);

        public static readonly LayoutRect QuickLinks = new LayoutRect(1070f, 490f, 518f, 221f);

        /// <summary>Заголовок блока: иконка 24 px и текст с отступом 12 px.</summary>
        public const float SectionIconSize = 26f;

        public const float SectionIconOffset = 16f;

        public const float SectionTitleOffset = 50f;

        // ---------------------------------------------------------------- строки

        /// <summary>Верх строк целей (три строки через ~45 px).</summary>
        public static readonly float[] GoalRowTops = { 320f, 365f, 410f };

        public const float GoalCheckboxSize = 24f;

        public const float GoalTextOffset = 76f;

        /// <summary>Верх строк статуса внутри блока «Статус».</summary>
        public static readonly float[] StatusRowTops = { 320f, 361f, 402f, 443f };

        /// <summary>Полоса статуса: x 1315..1500 внутри блока.</summary>
        public static readonly LayoutRect BarTrack = new LayoutRect(1315f, 296f, 185f, 171f);

        public const float BarWidth = 185f;

        public const float BarHeight = 20f;

        public const float StatusIconX = 1146f;

        public const float StatusLabelX = 1200f;

        public const float StatusPercentRight = 1566f;

        /// <summary>Строки «Финансов»: подпись слева, значение справа.</summary>
        public static readonly float[] FinanceRowTops = { 578f, 622f, 666f };

        public const float FinanceLabelX = 678f;

        public const float FinanceValueRight = 1040f;

        /// <summary>Сетка быстрых ссылок 2×2.</summary>
        public static readonly LayoutRect[] QuickLinkButtons =
        {
            new LayoutRect(1081f, 556f, 229f, 63f),
            new LayoutRect(1332f, 556f, 229f, 63f),
            new LayoutRect(1081f, 629f, 229f, 63f),
            new LayoutRect(1332f, 629f, 229f, 63f),
        };

        // ---------------------------------------------------------------- кнопки меню

        public const float ButtonLeft = 75f;

        public const float ButtonWidth = 478f;

        public const float ButtonTop = 241f;

        public const float ButtonHeight = 67f;

        public const float ButtonPitch = 73.5f;

        public const float ButtonIconLeft = 117f;

        public const float ButtonIconSize = 26f;

        public const float ButtonTextLeft = 185f;

        public const float ButtonTextRight = 553f;

        /// <summary>Подписи кнопок в порядке референса.</summary>
        public static readonly string[] MenuCaptions =
        {
            "Продолжить игру",
            "Сохранить игру",
            "Загрузить игру",
            "Настройки",
            "Управление",
            "Выйти в главное меню",
            "Выйти из игры",
        };

        public static int ButtonCount => MenuCaptions.Length;

        /// <summary>Прямоугольник кнопки меню по индексу.</summary>
        public static LayoutRect Button(int index)
        {
            if (index < 0 || index >= ButtonCount) throw new ArgumentOutOfRangeException(nameof(index));
            return new LayoutRect(ButtonLeft, ButtonTop + ButtonPitch * index, ButtonWidth, ButtonHeight);
        }

        /// <summary>Прямоугольник полосы статуса по индексу.</summary>
        public static LayoutRect Bar(int index)
        {
            if (index < 0 || index >= StatusRowTops.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return new LayoutRect(BarTrack.Left, StatusRowTops[index], BarWidth, BarHeight);
        }

        // ---------------------------------------------------------------- типографика

        /// <summary>Кегли в пикселях кадра.</summary>
        public const float FontLogo = 44f;

        public const float FontTitle = 36f;

        public const float FontButton = 26f;

        public const float FontSection = 27f;

        public const float FontBody = 21f;

        public const float FontSmall = 18f;

        public const float FontHeader = 27f;

        public const float FontHeaderSub = 19f;

        public const float FontPercent = 21f;

        /// <summary>Средняя ширина литеры кириллицы в долях кегля (для проверки влезания текста).</summary>
        public const float AverageGlyphWidth = 0.54f;

        /// <summary>Пиксели кадра → единицы uGUI.</summary>
        public static float Units(float framePixels) => framePixels * Scale;

        /// <summary>Кегль кадра → кегль uGUI (целое, не меньше 8).</summary>
        public static int FontSize(float framePixels) =>
            Math.Max(8, (int)Math.Round(framePixels * Scale, MidpointRounding.AwayFromZero));

        /// <summary>Оценка ширины строки в пикселях кадра при заданном кегле.</summary>
        public static float EstimateTextWidth(string text, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return text.Length * fontSize * AverageGlyphWidth;
        }

        /// <summary>Влезает ли подпись в кнопку меню (с полями).</summary>
        public static bool ButtonCaptionFits(string caption) =>
            EstimateTextWidth(caption, FontButton) <= ButtonTextRight - ButtonTextLeft;

        // ---------------------------------------------------------------- преобразования

        /// <summary>Точка кадра → нормализованные якоря uGUI (y считается снизу).</summary>
        public static AnchorRect ToAnchors(LayoutRect rect) => new AnchorRect(
            rect.Left / FrameWidth,
            1f - rect.Bottom / FrameHeight,
            rect.Right / FrameWidth,
            1f - rect.Top / FrameHeight);

        /// <summary>Прямоугольник внутри родителя → нормализованные якоря родителя.</summary>
        public static AnchorRect ToAnchors(LayoutRect rect, LayoutRect parent)
        {
            if (parent.Width <= 0f || parent.Height <= 0f) return ToAnchors(rect);
            return new AnchorRect(
                (rect.Left - parent.Left) / parent.Width,
                1f - (rect.Bottom - parent.Top) / parent.Height,
                (rect.Right - parent.Left) / parent.Width,
                1f - (rect.Top - parent.Top) / parent.Height);
        }

        /// <summary>Прямоугольник, заданный отступом от родителя (для иконок и подписей).</summary>
        public static LayoutRect Inset(LayoutRect parent, float left, float top, float right, float bottom) =>
            new LayoutRect(parent.Left + left, parent.Top + top,
                parent.Width - left - right, parent.Height - top - bottom);

        // ---------------------------------------------------------------- проверки

        /// <summary>
        /// Полная проверка раскладки. Пустой список — всё в порядке.
        /// Тест падает, если панель вылезла за кадр, кнопки пересеклись
        /// или кегль стал нечитаемым, — то есть «поехавшую» вёрстку видно в CI.
        /// </summary>
        public static IReadOnlyList<string> Validate()
        {
            var issues = new List<string>();

            LayoutRect[] frameBound =
            {
                LeftPanel, RightPanel, Logo, Title, HeaderDate, HeaderAddress,
                Goals, Status, Finance, QuickLinks, BarTrack,
            };
            for (int i = 0; i < frameBound.Length; i++)
            {
                LayoutRect r = frameBound[i];
                if (r.Left < 0f || r.Top < 0f || r.Right > FrameWidth || r.Bottom > FrameHeight)
                    issues.Add("Элемент " + r + " выходит за кадр " + FrameWidth + "×" + FrameHeight);
                if (r.Width <= 0f || r.Height <= 0f)
                    issues.Add("Элемент " + r + " имеет неположительный размер");
            }

            if (LeftPanel.Overlaps(RightPanel)) issues.Add("Левая и правая панели пересекаются");
            if (LeftPanel.Right >= RightPanel.Left)
                issues.Add("Нет зазора между панелями: " + LeftPanel.Right + " ≥ " + RightPanel.Left);

            if (!LeftPanel.Contains(Logo)) issues.Add("Логотип вне левой панели");
            if (!LeftPanel.Contains(Title)) issues.Add("Заголовок «Пауза» вне левой панели");
            if (Logo.Overlaps(Title)) issues.Add("Логотип перекрывает заголовок");

            LayoutRect[] inRight = { HeaderDate, HeaderAddress, Goals, Status, Finance, QuickLinks };
            for (int i = 0; i < inRight.Length; i++)
            {
                if (!RightPanel.Contains(inRight[i]))
                    issues.Add("Блок " + inRight[i] + " вне правой панели");
            }

            if (HeaderDate.Overlaps(HeaderAddress)) issues.Add("Блоки шапки пересекаются");
            if (Goals.Overlaps(Status)) issues.Add("«Цели» и «Статус» пересекаются");
            if (Finance.Overlaps(QuickLinks)) issues.Add("«Финансы» и «Быстрые ссылки» пересекаются");
            if (Math.Max(Goals.Bottom, Status.Bottom) > Math.Min(Finance.Top, QuickLinks.Top))
                issues.Add("Верхние и нижние блоки пересекаются по вертикали");
            if (Status.Top < HeaderDate.Bottom - 1f)
                issues.Add("«Статус» наезжает на шапку");

            if (!Status.Contains(BarTrack)) issues.Add("Полосы статуса вне блока «Статус»");
            for (int i = 0; i < StatusRowTops.Length; i++)
            {
                LayoutRect bar = Bar(i);
                if (!Status.Contains(bar)) issues.Add("Полоса " + i + " вне блока: " + bar);
                if (i > 0 && Bar(i - 1).Bottom > bar.Top)
                    issues.Add("Полосы " + (i - 1) + " и " + i + " пересекаются");
                // Полоса не должна налезать на проценты справа.
                if (bar.Right > StatusPercentRight - 20f)
                    issues.Add("Полоса " + i + " налезает на проценты: правый край " + bar.Right);
                if (bar.Left < StatusLabelX + 90f)
                    issues.Add("Полоса " + i + " начинается слишком рано и налезает на подпись");
            }

            for (int i = 0; i < ButtonCount; i++)
            {
                LayoutRect button = Button(i);
                if (!LeftPanel.Contains(button)) issues.Add("Кнопка " + i + " вне левой панели: " + button);
                if (Units(button.Height) < MinimumTouchTarget)
                    issues.Add("Кнопка " + i + " ниже минимума " + MinimumTouchTarget + " px при 1080p");
                if (i > 0 && Button(i - 1).Bottom > button.Top)
                    issues.Add("Кнопки " + (i - 1) + " и " + i + " пересекаются");
                if (!ButtonCaptionFits(MenuCaptions[i]))
                    issues.Add("Подпись «" + MenuCaptions[i] + "» не влезает в кнопку " + i);
            }

            if (Button(0).Top < Title.Bottom)
                issues.Add("Первая кнопка наезжает на заголовок «Пауза»");
            if (Button(ButtonCount - 1).Bottom > LeftPanel.Bottom - 8f)
                issues.Add("Нижняя кнопка слишком близко к краю панели");

            // Кнопки быстрых ссылок не должны пересекаться и вылезать из блока.
            for (int i = 0; i < QuickLinkButtons.Length; i++)
            {
                LayoutRect link = QuickLinkButtons[i];
                if (!QuickLinks.Contains(link)) issues.Add("Быстрая ссылка " + i + " вне блока: " + link);
                if (Units(link.Height) < MinimumTouchTarget)
                    issues.Add("Быстрая ссылка " + i + " ниже минимума " + MinimumTouchTarget + " px");
                for (int j = i + 1; j < QuickLinkButtons.Length; j++)
                {
                    if (link.Overlaps(QuickLinkButtons[j]))
                        issues.Add("Быстрые ссылки " + i + " и " + j + " пересекаются");
                }
            }

            // Типографика: минимальный кегль и влезание подписей.
            float smallest = Math.Min(FontSmall, Math.Min(FontBody, FontPercent));
            if (Units(smallest) < MinimumReadableFont)
                issues.Add("Самый мелкий кегль " + Units(smallest).ToString("0.#", CultureInfo.InvariantCulture) + " px < " + MinimumReadableFont);

            return issues;
        }

        /// <summary>Краткая сводка раскладки — печатается в отчёте и в тестах.</summary>
        public static string Describe()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Кадр " + FrameWidth + "×" + FrameHeight + " → uGUI 1920×1080, масштаб " +
                               Scale.ToString("0.0000", CultureInfo.InvariantCulture));
            builder.AppendLine("Левая панель  " + LeftPanel);
            builder.AppendLine("Правая панель " + RightPanel);
            builder.AppendLine("Кнопки: " + ButtonCount + " шт., " + Button(0) + " шаг " + ButtonPitch +
                               ", высота при 1080p " + Units(ButtonHeight).ToString("0.#", CultureInfo.InvariantCulture) + " px");
            builder.AppendLine("Полосы статуса: " + Bar(0) + " шаг " + (Bar(1).Top - Bar(0).Top));
            builder.AppendLine("Кегли (px кадра): title " + FontTitle + ", button " + FontButton +
                               ", section " + FontSection + ", body " + FontBody + ", small " + FontSmall);
            return builder.ToString();
        }
    }
}
