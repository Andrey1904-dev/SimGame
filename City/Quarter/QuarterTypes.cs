// Life From Zero — City/Quarter module (ТЗ №023)
// Namespace: LifeFromZero.City.Quarter
//
// Структурированные данные генератора. Это ЕДИНЫЙ контракт между
// автономным генератором и любым потребителем (Unity-адаптер, редактор,
// тесты): тип объекта, позиция, поворот, размер и параметры.

using System;
using System.Collections.Generic;

namespace LifeFromZero.City.Quarter
{
    /// <summary>
    /// Идентификаторы типов объектов. Строки, а не enum, — чтобы новые типы
    /// зданий/объектов добавлялись без изменения ядра генератора.
    /// </summary>
    public static class QuarterObjectTypes
    {
        public const string Road = "road";                       // проезжая часть улицы
        public const string Driveway = "driveway";               // дворовый проезд
        public const string Sidewalk = "sidewalk";               // тротуар
        public const string Intersection = "intersection";       // перекрёсток (плита)
        public const string Building = "building";               // жилой дом
        public const string Entrance = "entrance";               // подъезд
        public const string ParkingLot = "parking.lot";          // парковка (площадка)
        public const string ParkingStall = "parking.stall";      // парковочное место
        public const string Playground = "playground";           // детская площадка
        public const string Bench = "bench";                     // лавочка
        public const string Tree = "tree";                       // дерево
        public const string StreetLamp = "lamp.street";          // уличный фонарь
        public const string YardLamp = "lamp.yard";              // дворовый фонарь
        public const string Crosswalk = "marking.crosswalk";     // пешеходный переход
        public const string CenterLine = "marking.centerline";   // осевой пунктир разметки
        public const string StopLine = "marking.stopline";       // стоп-линия
        public const string Lawn = "lawn";                       // газон двора
        public const string PickupBuilding = "pickup.building";  // здание ПВЗ (одноэтажное)
        public const string PickupRoof = "pickup.roof";          // плоская крыша ПВЗ
        public const string PickupDoor = "pickup.door";          // входная дверь ПВЗ
        public const string PickupWindow = "pickup.window";      // окно-витрина ПВЗ
        public const string PickupSign = "pickup.sign";          // вывеска ПВЗ (параметр text)
        public const string PickupPlaza = "pickup.plaza";        // площадка перед входом ПВЗ
        public const string PickupPoint = "pickup.point";        // точка взаимодействия (будущее получение заказов)
    }

    /// <summary>
    /// Пункт выдачи заказов: здание + вход + точка будущего взаимодействия.
    /// Никакой выдачи/экономики здесь нет — только геометрия и координаты,
    /// к которым позже сможет привязаться Delivery (модуль Delivery не изменяется).
    /// </summary>
    public sealed class PickupPointInfo
    {
        /// <summary>Текст вывески над входом.</summary>
        public const string SignText = "ПУНКТ ВЫДАЧИ ЗАКАЗОВ";

        public PickupPointInfo(string id, Vec2 center, float rotationY, Vec3 size,
                               Vec3 entrance, Vec3 interactionPoint, Rect2 plaza, Rect2 buildingFootprint)
        {
            Id = id; Center = center; RotationY = rotationY; Size = size;
            Entrance = entrance; InteractionPoint = interactionPoint;
            Plaza = plaza; BuildingFootprint = buildingFootprint;
        }

        public string Id { get; }
        /// <summary>Центр здания ПВЗ на земле.</summary>
        public Vec2 Center { get; }
        public float RotationY { get; }
        /// <summary>Габариты здания (длина × высота × глубина), одноэтажное.</summary>
        public Vec3 Size { get; }
        /// <summary>Координаты входа (у двери, на площадке).</summary>
        public Vec3 Entrance { get; }
        /// <summary>
        /// Точка взаимодействия PickupPoint: куда встанет игрок, чтобы позже
        /// получать заказы. Лежит на площадке перед дверью.
        /// </summary>
        public Vec3 InteractionPoint { get; }
        /// <summary>Площадка перед входом (место для игрока), примыкает к тротуару.</summary>
        public Rect2 Plaza { get; }
        public Rect2 BuildingFootprint { get; }
    }

    /// <summary>
    /// Один размещённый объект квартала. Position — центр объекта на земле
    /// (Y обычно 0; объём задаётся Size, адаптер сам ставит центр по высоте).
    /// RotationY — поворот вокруг вертикальной оси, градусы, 0 = вдоль +X.
    /// </summary>
    public sealed class PlacedObject
    {
        public PlacedObject(string id, string typeId, Vec3 position, float rotationY, Vec3 size,
                            IReadOnlyDictionary<string, string> parameters)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            Position = position;
            RotationY = rotationY;
            Size = size;
            Parameters = parameters ?? new Dictionary<string, string>();
        }

        public string Id { get; }
        public string TypeId { get; }
        public Vec3 Position { get; }
        public float RotationY { get; }
        public Vec3 Size { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }

        /// <summary>След объекта на земле (без поворота; для повёрнутых на 90° адаптер меняет оси).</summary>
        public Rect2 Footprint
        {
            get
            {
                bool swapped = Math.Abs(Math.Abs(RotationY) - 90f) < 0.01f ||
                               Math.Abs(Math.Abs(RotationY) - 270f) < 0.01f;
                return Rect2.FromCenterSize(Position.Ground,
                    swapped ? Size.Z : Size.X, swapped ? Size.X : Size.Z);
            }
        }

        public string Param(string key, string fallback = null) =>
            Parameters.TryGetValue(key, out string value) ? value : fallback;
    }

    public enum RoadKind
    {
        Street,     // городская улица с тротуарами и разметкой
        Driveway    // дворовый проезд (без разметки, уже улицы)
    }

    /// <summary>Сегмент дороги между двумя узлами графа (Start/End — id узлов).</summary>
    public sealed class RoadSegment
    {
        public RoadSegment(string id, string startNodeId, string endNodeId, Vec2 start, Vec2 end,
                           float width, RoadKind kind)
        {
            Id = id; StartNodeId = startNodeId; EndNodeId = endNodeId;
            Start = start; End = end; Width = width; Kind = kind;
        }

        public string Id { get; }
        public string StartNodeId { get; }
        public string EndNodeId { get; }
        public Vec2 Start { get; }
        public Vec2 End { get; }
        public float Width { get; }
        public RoadKind Kind { get; }

        public float Length => Start.DistanceTo(End);

        /// <summary>Полоса дороги на земле (сегменты генератора осевые: горизонталь/вертикаль).</summary>
        public Rect2 Footprint =>
            new Rect2(Math.Min(Start.X, End.X) - (Start.X == End.X ? Width / 2f : 0f),
                      Math.Min(Start.Z, End.Z) - (Start.Z == End.Z ? Width / 2f : 0f),
                      Math.Max(Start.X, End.X) + (Start.X == End.X ? Width / 2f : 0f),
                      Math.Max(Start.Z, End.Z) + (Start.Z == End.Z ? Width / 2f : 0f));
    }

    /// <summary>Узел дорожного графа: перекрёсток или точка подключения проезда/границы.</summary>
    public sealed class RoadNode
    {
        public RoadNode(string id, Vec2 position, bool isIntersection)
        {
            Id = id; Position = position; IsIntersection = isIntersection;
        }

        public string Id { get; }
        public Vec2 Position { get; }
        public bool IsIntersection { get; }
    }

    /// <summary>Жилой дом: геометрия + подъезды. Подъезды также лежат в общем списке объектов.</summary>
    public sealed class BuildingInfo
    {
        public BuildingInfo(string id, string archetypeId, Vec2 center, float rotationY, Vec3 size,
                            int floors, IReadOnlyList<Vec2> entrances)
        {
            Id = id; ArchetypeId = archetypeId; Center = center; RotationY = rotationY;
            Size = size; Floors = floors; Entrances = entrances;
        }

        public string Id { get; }
        public string ArchetypeId { get; }
        public Vec2 Center { get; }
        public float RotationY { get; }
        public Vec3 Size { get; }
        public int Floors { get; }
        public IReadOnlyList<Vec2> Entrances { get; }

        public Rect2 Footprint
        {
            get
            {
                bool swapped = Math.Abs(Math.Abs(RotationY) - 90f) < 0.01f;
                return Rect2.FromCenterSize(Center, swapped ? Size.Z : Size.X, swapped ? Size.X : Size.Z);
            }
        }
    }

    /// <summary>Двор: территория + счётчики наполнения (для тестов и адаптера).</summary>
    public sealed class CourtyardInfo
    {
        public CourtyardInfo(string id, Rect2 area, Vec2 parkingCenter, int benches, int trees,
                             int parkingStalls, bool hasPlayground)
        {
            Id = id; Area = area; ParkingCenter = parkingCenter; Benches = benches;
            Trees = trees; ParkingStalls = parkingStalls; HasPlayground = hasPlayground;
        }

        public string Id { get; }
        public Rect2 Area { get; }
        public Vec2 ParkingCenter { get; }
        public int Benches { get; }
        public int Trees { get; }
        public int ParkingStalls { get; }
        public bool HasPlayground { get; }
    }

    /// <summary>Результат генерации квартала: всё, что нужно адаптеру и тестам.</summary>
    public sealed class QuarterPlan
    {
        public QuarterPlan(int seed, float size,
                           IReadOnlyList<PlacedObject> objects,
                           IReadOnlyList<RoadSegment> roads,
                           IReadOnlyList<RoadNode> nodes,
                           IReadOnlyList<BuildingInfo> buildings,
                           IReadOnlyList<CourtyardInfo> courtyards)
            : this(seed, size, objects, roads, nodes, buildings, courtyards, null)
        {
        }

        public QuarterPlan(int seed, float size,
                           IReadOnlyList<PlacedObject> objects,
                           IReadOnlyList<RoadSegment> roads,
                           IReadOnlyList<RoadNode> nodes,
                           IReadOnlyList<BuildingInfo> buildings,
                           IReadOnlyList<CourtyardInfo> courtyards,
                           PickupPointInfo pickupPoint)
        {
            Seed = seed; Size = size; Objects = objects; Roads = roads; Nodes = nodes;
            Buildings = buildings; Courtyards = courtyards; PickupPoint = pickupPoint;
        }

        public int Seed { get; }
        /// <summary>Размер квартала в метрах (квадрат Size × Size, координаты 0..Size).</summary>
        public float Size { get; }
        public IReadOnlyList<PlacedObject> Objects { get; }
        public IReadOnlyList<RoadSegment> Roads { get; }
        public IReadOnlyList<RoadNode> Nodes { get; }
        public IReadOnlyList<BuildingInfo> Buildings { get; }
        public IReadOnlyList<CourtyardInfo> Courtyards { get; }
        /// <summary>Единственный ПВЗ квартала (null, если генерация шла без него — у генератора всегда есть).</summary>
        public PickupPointInfo PickupPoint { get; }

        public IEnumerable<PlacedObject> OfType(string typeId)
        {
            foreach (PlacedObject obj in Objects)
                if (obj.TypeId == typeId)
                    yield return obj;
        }

        public int CountOfType(string typeId)
        {
            int count = 0;
            foreach (PlacedObject obj in Objects)
                if (obj.TypeId == typeId)
                    count++;
            return count;
        }
    }
}
