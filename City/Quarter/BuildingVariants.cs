// Life From Zero — City/Quarter module (ТЗ №024, дополнение: визуальное разнообразие)
// Namespace: LifeFromZero.City.Quarter
//
// Каталог архитектурных вариантов жилых домов. Минимум 3 узнаваемых
// варианта на каждый тип этажности; отличия — в геометрии (форма крыши,
// тип балконов/лоджий, форма окон, входная группа), а не только в цвете.
// Генератор выбирает вариант по seed и следит, чтобы соседние дома не
// получали одинаковую комбинацию «вариант + цвет + рисунок балконов».
// Адаптер строит детали по полям варианта из параметров здания.

using System;
using System.Collections.Generic;

namespace LifeFromZero.City.Quarter
{
    /// <summary>Один архитектурный вариант дома определённой этажности.</summary>
    public sealed class BuildingVariant
    {
        public BuildingVariant(string id, int floors, string facadeStyle, string roofVariant,
                               string windowStyle, string balconyType, string entranceStyle,
                               float wear, string[] palette)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Floors = floors;
            FacadeStyle = facadeStyle;   // brick | plaster | panel | modern
            RoofVariant = roofVariant;   // flat | flat-parapet | pitched | pitched-steep | mansard
            WindowStyle = windowStyle;   // standard | wide | tall
            BalconyType = balconyType;   // none | open | glazed | loggia
            EntranceStyle = entranceStyle; // flat-canopy | sloped-canopy | porch
            Wear = wear;                 // базовый износ 0..1 (генератор добавит jitter по seed)
            Palette = palette ?? throw new ArgumentNullException(nameof(palette));
        }

        public string Id { get; }
        public int Floors { get; }
        public string FacadeStyle { get; }
        public string RoofVariant { get; }
        /// <summary>Базовый тип крыши для совместимости: pitched-семейство -> "pitched", остальное -> "flat".</summary>
        public string RoofType => RoofVariant.StartsWith("pitched", StringComparison.Ordinal) ||
                                  RoofVariant == "mansard" ? "pitched" : "flat";
        public string WindowStyle { get; }
        public string BalconyType { get; }
        public string EntranceStyle { get; }
        /// <summary>Базовый износ фасада 0 (свежая краска) .. 1 (сильно обжитой).</summary>
        public float Wear { get; }
        /// <summary>Цвета фасада этого варианта, hex "#RRGGBB".</summary>
        public IReadOnlyList<string> Palette { get; }
    }

    /// <summary>Все архитектурные варианты. Новые варианты добавляются сюда без правок генератора.</summary>
    public static class BuildingVariantCatalog
    {
        public static IReadOnlyList<BuildingVariant> All { get; } = new[]
        {
            // --- Трёхэтажки: старый фонд ---
            new BuildingVariant("brick3.classic", 3, "brick", "pitched", "standard", "open", "flat-canopy", 0.55f,
                new[] { "#A55E41", "#9C6642", "#8F5138" }),
            new BuildingVariant("brick3.plaster", 3, "plaster", "pitched-steep", "wide", "open", "sloped-canopy", 0.4f,
                new[] { "#D9C9A8", "#CBB89B", "#E3D3B3" }),
            new BuildingVariant("brick3.mansard", 3, "brick", "mansard", "tall", "glazed", "porch", 0.3f,
                new[] { "#B98A5F", "#A9713F", "#C7B299" }),

            // --- Пятиэтажки ---
            new BuildingVariant("panel5.khrushchev", 5, "panel", "flat", "standard", "open", "flat-canopy", 0.6f,
                new[] { "#C8B8A2", "#B7C0C4", "#BFA98C" }),
            new BuildingVariant("panel5.brick", 5, "brick", "flat-parapet", "standard", "glazed", "porch", 0.35f,
                new[] { "#A55E41", "#B98A5F", "#9C6642" }),
            new BuildingVariant("panel5.repaired", 5, "panel", "flat-parapet", "wide", "loggia", "sloped-canopy", 0.15f,
                new[] { "#D6C9B0", "#C4D0C0", "#D8DDE2" }),

            // --- Девятиэтажки ---
            new BuildingVariant("panel9.series-a", 9, "panel", "flat", "standard", "loggia", "flat-canopy", 0.45f,
                new[] { "#B7C0C4", "#C8B8A2", "#AEB8BC" }),
            new BuildingVariant("panel9.brick", 9, "brick", "flat-parapet", "standard", "open", "porch", 0.3f,
                new[] { "#A9713F", "#9C6642", "#B98A5F" }),
            new BuildingVariant("panel9.modern", 9, "modern", "flat", "wide", "glazed", "sloped-canopy", 0.2f,
                new[] { "#9FB4C7", "#D8DDE2", "#8FA3B0" }),

            // --- Элитные ЖК (ТЗ №025) ---
            new BuildingVariant("elite.glass", 32, "glass", "flat", "wide", "none", "porch", 0.05f,
                new[] { "#7E97AC", "#93AEC4", "#6E8AA3" }),
            new BuildingVariant("elite.premium", 34, "modern", "flat-parapet", "standard", "loggia", "porch", 0.08f,
                new[] { "#C8B394", "#B7A582", "#D3C2A4" }),

            // --- Частные дома (ТЗ №025) ---
            new BuildingVariant("private.modern", 2, "modern", "flat", "wide", "none", "porch", 0.1f,
                new[] { "#D8DDE2", "#C9CFD4", "#E4E7EA" }),
            new BuildingVariant("private.classic", 2, "brick", "pitched", "standard", "none", "flat-canopy", 0.25f,
                new[] { "#B98A5F", "#A9713F", "#C7A06B" }),

            // --- Десятиэтажки ---
            new BuildingVariant("tower10.modern-panel", 10, "modern", "flat", "wide", "loggia", "flat-canopy", 0.25f,
                new[] { "#9FB4C7", "#B9C8D4", "#8FA3B0" }),
            new BuildingVariant("tower10.brick", 10, "brick", "flat-parapet", "standard", "glazed", "porch", 0.35f,
                new[] { "#A55E41", "#B98A5F", "#9C6642" }),
            new BuildingVariant("tower10.glass", 10, "modern", "flat", "tall", "glazed", "sloped-canopy", 0.1f,
                new[] { "#7E97AC", "#A9BCCB", "#8FA8BC" }),
        };

        /// <summary>Варианты для этажности; для нестандартной этажности — варианты ближайшего типа (по умолчанию пятиэтажки).</summary>
        public static IReadOnlyList<BuildingVariant> ForFloors(int floors)
        {
            var result = new List<BuildingVariant>();
            foreach (BuildingVariant variant in All)
                if (variant.Floors == floors)
                    result.Add(variant);
            if (result.Count > 0) return result;
            foreach (BuildingVariant variant in All)
                if (variant.Floors == 5)
                    result.Add(variant);
            return result;
        }
    }
}
