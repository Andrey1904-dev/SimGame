// Life From Zero — City/Quarter module (ТЗ №023)
// Namespace: LifeFromZero.City.Quarter

using System;
using System.Collections.Generic;

namespace LifeFromZero.City.Quarter
{
    /// <summary>
    /// Архетип здания. Новые типы домов добавляются добавлением архетипа
    /// в Options.Archetypes — ядро генератора менять не нужно.
    /// </summary>
    public sealed class BuildingArchetype
    {
        public BuildingArchetype(string id, int floors, float depth, float floorHeight,
                                 float[] lengthVariants, float entranceSpacing)
            : this(id, floors, depth, floorHeight, lengthVariants, entranceSpacing, "flat", "panel")
        {
        }

        public BuildingArchetype(string id, int floors, float depth, float floorHeight,
                                 float[] lengthVariants, float entranceSpacing,
                                 string roofType, string facadeStyle)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            if (floors <= 0) throw new ArgumentOutOfRangeException(nameof(floors));
            if (lengthVariants == null || lengthVariants.Length == 0)
                throw new ArgumentException("Нужен хотя бы один вариант длины.", nameof(lengthVariants));
            Floors = floors;
            Depth = depth;
            FloorHeight = floorHeight;
            LengthVariants = lengthVariants;
            EntranceSpacing = entranceSpacing;
            RoofType = roofType ?? "flat";       // "flat" | "pitched"
            FacadeStyle = facadeStyle ?? "panel"; // "brick" | "panel" | "modern"
        }

        public string Id { get; }
        public int Floors { get; }
        /// <summary>Глубина корпуса, метры.</summary>
        public float Depth { get; }
        public float FloorHeight { get; }
        /// <summary>Допустимые длины корпуса, метры.</summary>
        public IReadOnlyList<float> LengthVariants { get; }
        /// <summary>Шаг подъездов вдоль фасада, метры.</summary>
        public float EntranceSpacing { get; }
        /// <summary>Тип крыши: "flat" (плоская) или "pitched" (скатная).</summary>
        public string RoofType { get; }
        /// <summary>Стиль фасада: "brick", "panel" или "modern" (влияет на цвет и детали адаптера).</summary>
        public string FacadeStyle { get; }

        public float Height => Floors * FloorHeight;

        /// <summary>Старая трёхэтажка: кирпич/штукатурка, скатная крыша.</summary>
        public static BuildingArchetype BrickThreeStorey() =>
            new BuildingArchetype("building.brick3", floors: 3, depth: 11f, floorHeight: 3.1f,
                                  lengthVariants: new[] { 24f, 30f }, entranceSpacing: 12f,
                                  roofType: "pitched", facadeStyle: "brick");

        /// <summary>Классическая пятиэтажка: советская панель, плоская крыша.</summary>
        public static BuildingArchetype PanelFiveStorey() =>
            new BuildingArchetype("building.panel5", floors: 5, depth: 12f, floorHeight: 3f,
                                  lengthVariants: new[] { 30f, 36f, 42f }, entranceSpacing: 15f,
                                  roofType: "flat", facadeStyle: "panel");

        /// <summary>Современная десятиэтажка.</summary>
        public static BuildingArchetype ModernTenStorey() =>
            new BuildingArchetype("building.tower10", floors: 10, depth: 13f, floorHeight: 2.9f,
                                  lengthVariants: new[] { 24f, 30f }, entranceSpacing: 12f,
                                  roofType: "flat", facadeStyle: "modern");
    }

    /// <summary>
    /// Дом игрока (кв. №67 — только номер в данных; саму квартиру/сцену
    /// Apartment67 генератор не создаёт и не трогает).
    /// Position/RotationY можно задать извне; если Position не задан —
    /// генератор сам выбирает место на краю блока и резервирует его до застройки.
    /// </summary>
    public sealed class PlayerHomeOptions
    {
        public bool Enabled { get; set; } = true;
        /// <summary>Центр дома в координатах квартала (метры). null = выбрать автоматически.</summary>
        public Vec2? Position { get; set; }
        /// <summary>Поворот дома: 0 или 90 градусов.</summary>
        public float RotationY { get; set; } = 0f;
    }

    /// <summary>
    /// Контекст для пользовательских размещателей (расширение без правки ядра):
    /// можно добавлять свои объекты, проверяя, что место свободно.
    /// </summary>
    public interface IQuarterObjectPlacer
    {
        void Place(QuarterPlacementContext context);
    }

    /// <summary>API, доступное пользовательским размещателям. См. QuarterGenerator.</summary>
    public sealed class QuarterPlacementContext
    {
        private readonly Action<PlacedObject> _add;
        private readonly Func<Rect2, float, bool> _isFree;
        private readonly List<PlacedObject> _objects;

        internal QuarterPlacementContext(SeededRandom random, float size,
                                         List<PlacedObject> objects,
                                         Action<PlacedObject> add, Func<Rect2, float, bool> isFree)
        {
            Random = random;
            Size = size;
            _objects = objects;
            _add = add;
            _isFree = isFree;
        }

        public SeededRandom Random { get; }
        public float Size { get; }
        public IReadOnlyList<PlacedObject> Objects => _objects;

        /// <summary>Свободен ли прямоугольник (с запасом) от уже размещённых домов/площадок.</summary>
        public bool IsFree(Rect2 rect, float margin = 1f) => _isFree(rect, margin);

        public void Add(PlacedObject obj) => _add(obj ?? throw new ArgumentNullException(nameof(obj)));
    }

    /// <summary>Параметры генерации. Значения по умолчанию — квартал 250×250 по ТЗ.</summary>
    public sealed class QuarterGeneratorOptions
    {
        /// <summary>Фиксированный seed: один seed = один и тот же квартал, побитово.</summary>
        public int Seed { get; set; } = 1904;

        /// <summary>Размер квартала, метры. По ТЗ — 250.</summary>
        public float Size { get; set; } = 250f;

        /// <summary>Ширина проезжей части улиц, метры (две полосы).</summary>
        public float RoadWidth { get; set; } = 7f;

        /// <summary>Ширина тротуара с каждой стороны улицы, метры.</summary>
        public float SidewalkWidth { get; set; } = 2.5f;

        /// <summary>Ширина дворового проезда, метры.</summary>
        public float DrivewayWidth { get; set; } = 3.5f;

        /// <summary>Шаг уличных фонарей вдоль улиц, метры.</summary>
        public float StreetLampSpacing { get; set; } = 25f;

        /// <summary>Сколько дворов обустраивать (по ТЗ — 2..3). Генератор кладёт min(3, value), но не меньше 2 при value>=2.</summary>
        public int CourtyardCount { get; set; } = 3;

        /// <summary>Ставить ли один пункт выдачи заказов (ПВЗ) у улицы. По умолчанию — да.</summary>
        public bool IncludePickupPoint { get; set; } = true;

        /// <summary>Дом игрока с зарезервированным местом (см. PlayerHomeOptions).</summary>
        public PlayerHomeOptions PlayerHome { get; set; } = new PlayerHomeOptions();

        /// <summary>
        /// Архетипы домов: трёхэтажки, пятиэтажки (преобладают) и десятиэтажки.
        /// Генератор гарантирует присутствие каждого типа этажности из списка.
        /// </summary>
        public List<BuildingArchetype> Archetypes { get; set; } = new List<BuildingArchetype>
        {
            BuildingArchetype.PanelFiveStorey(),
            BuildingArchetype.BrickThreeStorey(),
            BuildingArchetype.ModernTenStorey(),
        };

        /// <summary>Пользовательские размещатели: вызываются после базовой генерации.</summary>
        public List<IQuarterObjectPlacer> CustomPlacers { get; set; } = new List<IQuarterObjectPlacer>();

        internal void Validate()
        {
            if (Size < 120f) throw new ArgumentOutOfRangeException(nameof(Size), "Квартал меньше 120 м не помещает сетку улиц.");
            if (RoadWidth <= 0f || SidewalkWidth < 0f || DrivewayWidth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(RoadWidth), "Ширины дорог должны быть положительными.");
            if (Archetypes == null || Archetypes.Count == 0)
                throw new ArgumentException("Нужен хотя бы один архетип здания.", nameof(Archetypes));
            if (CourtyardCount < 0 || CourtyardCount > 3)
                throw new ArgumentOutOfRangeException(nameof(CourtyardCount), "По ТЗ дворов 2..3 (допустимо 0..3).");
        }
    }
}
