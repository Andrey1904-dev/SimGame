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
        public const string ShopBuilding = "shop.building";      // продуктовый магазин (одноэтажный)
        public const string ShopRoof = "shop.roof";
        public const string ShopDoor = "shop.door";
        public const string ShopWindow = "shop.window";
        public const string ShopSign = "shop.sign";              // вывеска «ПРОДУКТЫ» (параметр text)
        public const string ShopPlaza = "shop.plaza";
        public const string ShopPoint = "shop.grocery";          // точка взаимодействия магазина
        public const string PlayerHomeMarker = "player.home";    // маркер дома игрока (у здания)
        public const string PlayerHomeDoor = "player.home.door"; // дверь главного подъезда дома игрока
        public const string PlayerHomeExit = "player.home.exit"; // точка выхода во двор из дома игрока
        public const string DeliveryDropoff = "delivery.dropoff";// точка доставки у подъезда
        // Дворовое наполнение (улучшенные дворы)
        public const string Swing = "yard.swing";                // качели
        public const string Sandbox = "yard.sandbox";            // песочница
        public const string Urn = "yard.urn";                    // урна
        public const string TrashContainer = "yard.trash";       // мусорный контейнер
        public const string Bush = "yard.bush";                  // куст
        public const string Footpath = "yard.footpath";          // пешеходная дорожка во дворе
        // Подвал под трёхэтажкой
        public const string BasementFloor = "basement.floor";
        public const string BasementWall = "basement.wall";
        public const string BasementCeiling = "basement.ceiling";
        public const string BasementStep = "basement.step";      // ступень бетонной лестницы
        public const string BasementDoor = "basement.door";      // железная дверь (точка взаимодействия)
        public const string BasementSecondDoor = "basement.door.second"; // вторая, закрытая дверь
        public const string BasementShelf = "basement.shelf";    // металлический стеллаж
        public const string BasementCrate = "basement.crate";    // деревянный ящик
        public const string BasementTable = "basement.table.object"; // стол (точка — basement.table в BasementInfo.Points)
        public const string BasementChair = "basement.chair";
        public const string BasementPipe = "basement.pipe";
        public const string BasementLamp = "basement.lamp";
        public const string BasementPoint = "basement.point";    // маркер именованной точки подвала
    }

    /// <summary>Дом игрока: здание из Buildings + входная дверь и выход во двор.</summary>
    public sealed class PlayerHomeInfo
    {
        public const string SpecialId = "player.home";
        public const int ApartmentNumber = 67; // только номер в данных; квартиру/сцену не создаём

        public PlayerHomeInfo(string buildingId, Vec2 center, float rotationY,
                              Vec3 entranceDoor, Vec3 courtyardExit)
        {
            BuildingId = buildingId; Center = center; RotationY = rotationY;
            EntranceDoor = entranceDoor; CourtyardExit = courtyardExit;
        }

        public string Id => SpecialId;
        public string BuildingId { get; }
        public Vec2 Center { get; }
        public float RotationY { get; }
        /// <summary>Дверь главного подъезда (первый подъезд здания).</summary>
        public Vec3 EntranceDoor { get; }
        /// <summary>Точка выхода во двор: перед главным подъездом, в сторону двора.</summary>
        public Vec3 CourtyardExit { get; }
    }

    /// <summary>
    /// Точка доставки у подъезда жилого дома. Только данные и маркер:
    /// модуль Delivery не изменяется, выдача сюда не зашита.
    /// </summary>
    public sealed class DeliveryPointInfo
    {
        public const float DefaultInteractionRadius = 1.5f;

        public DeliveryPointInfo(string id, string buildingId, int entranceNumber, Vec3 position)
        {
            Id = id; BuildingId = buildingId; EntranceNumber = entranceNumber; Position = position;
        }

        public string Id { get; }
        public string BuildingId { get; }
        /// <summary>Номер подъезда, начиная с 1.</summary>
        public int EntranceNumber { get; }
        /// <summary>Перед входом, вне стен здания.</summary>
        public Vec3 Position { get; }
        public float InteractionRadius => DefaultInteractionRadius;
        public string TypeId => QuarterObjectTypes.DeliveryDropoff;
    }

    /// <summary>Продуктовый магазин: здание + вход + точка взаимодействия shop.grocery.</summary>
    public sealed class GroceryShopInfo
    {
        public const string SignText = "ПРОДУКТЫ";

        public GroceryShopInfo(string id, Vec2 center, Vec3 size, Vec3 entrance,
                               Vec3 interactionPoint, Rect2 plaza, Rect2 buildingFootprint)
        {
            Id = id; Center = center; Size = size; Entrance = entrance;
            InteractionPoint = interactionPoint; Plaza = plaza; BuildingFootprint = buildingFootprint;
        }

        public string Id { get; }
        public Vec2 Center { get; }
        public Vec3 Size { get; }
        public Vec3 Entrance { get; }
        public Vec3 InteractionPoint { get; }
        public Rect2 Plaza { get; }
        public Rect2 BuildingFootprint { get; }
    }

    /// <summary>Состояния железной двери подвала.</summary>
    public enum BasementDoorState
    {
        Closed,
        Open,
        Locked
    }

    /// <summary>
    /// Логика железной двери подвала без Unity: состояния и переходы.
    /// Unity-компонент BasementDoor использует этот же контроллер и только
    /// поворачивает дверь на петлях по состоянию.
    /// </summary>
    public sealed class BasementDoorController
    {
        public BasementDoorController(BasementDoorState initial = BasementDoorState.Locked)
        {
            State = initial;
        }

        public BasementDoorState State { get; private set; }
        public bool IsOpen => State == BasementDoorState.Open;

        /// <summary>Открыть: из Closed — да, из Locked/Open — нет.</summary>
        public bool Open()
        {
            if (State != BasementDoorState.Closed) return false;
            State = BasementDoorState.Open;
            return true;
        }

        public bool Close()
        {
            if (State != BasementDoorState.Open) return false;
            State = BasementDoorState.Closed;
            return true;
        }

        /// <summary>Взаимодействие игрока: Closed↔Open; Locked не открывается без Unlock.</summary>
        public bool Toggle() => IsOpen ? Close() : Open();

        public bool Unlock()
        {
            if (State != BasementDoorState.Locked) return false;
            State = BasementDoorState.Closed;
            return true;
        }

        public bool Lock()
        {
            if (State != BasementDoorState.Closed) return false;
            State = BasementDoorState.Locked;
            return true;
        }
    }

    /// <summary>
    /// Подвал под одной трёхэтажкой: вход снаружи, лестница, дверь и именованные
    /// точки внутри (entry/table/storage/secretroom). Квестов/NPC здесь нет.
    /// Координаты точек — мировые; Y отрицательный (подвал ниже земли, пол ≈ -2.8 м).
    /// </summary>
    public sealed class BasementInfo
    {
        public BasementInfo(string buildingId, Vec3 entrance, Vec3 stairBottom, Vec3 doorPosition,
                            IReadOnlyDictionary<string, Vec3> points)
        {
            BuildingId = buildingId; Entrance = entrance; StairBottom = stairBottom;
            DoorPosition = doorPosition; Points = points;
        }

        public string BuildingId { get; }
        /// <summary>Начало лестницы снаружи (у торца/задней стороны дома), уровень земли.</summary>
        public Vec3 Entrance { get; }
        /// <summary>Низ лестницы, перед железной дверью.</summary>
        public Vec3 StairBottom { get; }
        /// <summary>Железная дверь (точка взаимодействия basement.door).</summary>
        public Vec3 DoorPosition { get; }
        /// <summary>Начальное состояние двери.</summary>
        public BasementDoorState InitialDoorState => BasementDoorState.Locked;
        /// <summary>Точки: basement.entry / basement.table / basement.storage / basement.secretroom.</summary>
        public IReadOnlyDictionary<string, Vec3> Points { get; }
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
            : this(id, archetypeId, center, rotationY, size, floors, entrances, "panel", "flat", null, false)
        {
        }

        public BuildingInfo(string id, string archetypeId, Vec2 center, float rotationY, Vec3 size,
                            int floors, IReadOnlyList<Vec2> entrances,
                            string facadeStyle, string roofType, string facadeColor, bool isPlayerHome)
        {
            Id = id; ArchetypeId = archetypeId; Center = center; RotationY = rotationY;
            Size = size; Floors = floors; Entrances = entrances;
            FacadeStyle = facadeStyle; RoofType = roofType; FacadeColor = facadeColor;
            IsPlayerHome = isPlayerHome;
        }

        public string Id { get; }
        public string ArchetypeId { get; }
        public Vec2 Center { get; }
        public float RotationY { get; }
        public Vec3 Size { get; }
        public int Floors { get; }
        public IReadOnlyList<Vec2> Entrances { get; }
        /// <summary>"brick" | "panel" | "modern" — влияет на детали фасада в адаптере.</summary>
        public string FacadeStyle { get; }
        /// <summary>"flat" | "pitched".</summary>
        public string RoofType { get; }
        /// <summary>Цвет фасада, hex "#RRGGBB" (может быть null).</summary>
        public string FacadeColor { get; }
        /// <summary>Это дом игрока (player.home).</summary>
        public bool IsPlayerHome { get; }

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
            : this(id, area, parkingCenter, benches, trees, parkingStalls, hasPlayground, 0, 0, 0, 0, 0, false)
        {
        }

        public CourtyardInfo(string id, Rect2 area, Vec2 parkingCenter, int benches, int trees,
                             int parkingStalls, bool hasPlayground,
                             int swings, int sandboxes, int urns, int trashContainers, int bushes,
                             bool hasFootpath)
        {
            Id = id; Area = area; ParkingCenter = parkingCenter; Benches = benches;
            Trees = trees; ParkingStalls = parkingStalls; HasPlayground = hasPlayground;
            Swings = swings; Sandboxes = sandboxes; Urns = urns;
            TrashContainers = trashContainers; Bushes = bushes; HasFootpath = hasFootpath;
        }

        public string Id { get; }
        public Rect2 Area { get; }
        public Vec2 ParkingCenter { get; }
        public int Benches { get; }
        public int Trees { get; }
        public int ParkingStalls { get; }
        public bool HasPlayground { get; }
        public int Swings { get; }
        public int Sandboxes { get; }
        public int Urns { get; }
        public int TrashContainers { get; }
        public int Bushes { get; }
        public bool HasFootpath { get; }
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
            : this(seed, size, objects, roads, nodes, buildings, courtyards, pickupPoint,
                   null, null, null, new DeliveryPointInfo[0])
        {
        }

        public QuarterPlan(int seed, float size,
                           IReadOnlyList<PlacedObject> objects,
                           IReadOnlyList<RoadSegment> roads,
                           IReadOnlyList<RoadNode> nodes,
                           IReadOnlyList<BuildingInfo> buildings,
                           IReadOnlyList<CourtyardInfo> courtyards,
                           PickupPointInfo pickupPoint,
                           PlayerHomeInfo playerHome,
                           GroceryShopInfo shop,
                           BasementInfo basement,
                           IReadOnlyList<DeliveryPointInfo> deliveryPoints)
            : this(seed, size, objects, roads, nodes, buildings, courtyards, pickupPoint,
                   playerHome, shop, basement, deliveryPoints, new RealEstateProperty[0])
        {
        }

        public QuarterPlan(int seed, float size,
                           IReadOnlyList<PlacedObject> objects,
                           IReadOnlyList<RoadSegment> roads,
                           IReadOnlyList<RoadNode> nodes,
                           IReadOnlyList<BuildingInfo> buildings,
                           IReadOnlyList<CourtyardInfo> courtyards,
                           PickupPointInfo pickupPoint,
                           PlayerHomeInfo playerHome,
                           GroceryShopInfo shop,
                           BasementInfo basement,
                           IReadOnlyList<DeliveryPointInfo> deliveryPoints,
                           IReadOnlyList<RealEstateProperty> properties)
        {
            Seed = seed; Size = size; Objects = objects; Roads = roads; Nodes = nodes;
            Buildings = buildings; Courtyards = courtyards; PickupPoint = pickupPoint;
            PlayerHome = playerHome; Shop = shop; Basement = basement;
            DeliveryPoints = deliveryPoints ?? new DeliveryPointInfo[0];
            Properties = properties ?? new RealEstateProperty[0];
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
        /// <summary>Дом игрока player.home (null при PlayerHome.Enabled=false).</summary>
        public PlayerHomeInfo PlayerHome { get; }
        /// <summary>Единственный продуктовый магазин (shop.grocery).</summary>
        public GroceryShopInfo Shop { get; }
        /// <summary>Подвал под одной трёхэтажкой (null, если в квартале нет трёхэтажек).</summary>
        public BasementInfo Basement { get; }
        /// <summary>Точки доставки у каждого жилого подъезда (delivery.dropoff).</summary>
        public IReadOnlyList<DeliveryPointInfo> DeliveryPoints { get; }
        /// <summary>
        /// Реестр недвижимости: варианты жилья игрока в этом квартале
        /// (построенные и плановые). Типы — HousingCatalog; покупок здесь нет.
        /// </summary>
        public IReadOnlyList<RealEstateProperty> Properties { get; }

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
