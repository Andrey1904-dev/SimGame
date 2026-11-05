// Life From Zero — City/Quarter/DistrictMap (ТЗ №031)
// Namespace: LifeFromZero.City.Quarter
//
// Генератор карты игрового района 750×750 м по визуальному референсу «Карта района v1.1».
// Это НЕ новый генератор с нуля: он выдаёт тот же контракт QuarterPlan, использует
// BuildingArchetype, BuildingVariantCatalog, SeededRandom, RoadGraph и QuarterPlanSerializer
// и строится тем же QuarterUnityAdapter.
//
// Раскладка — ДАННЫЕ, а не случайная упаковка: оси улиц, блоки и вызовы объектов
// перечислены ниже в порядке Build(). Зеркало этих координат с проверками
// (пересечения, входы, связность, счётчики) — City/Tools/district_layout_check.py.
// Если меняете координаты здесь — меняйте и там.
//
// seed влияет только на наполнение: варианты фасадов, цвета, балконы, износ,
// кондиционеры, повороты деревьев. Геометрия от seed не зависит.
//
// В Unity НЕ запускалось (Unity-проекта в репозитории нет). Компиляция и тесты C#
// в этом окружении не выполнялись — см. CODEX_MAP_INTEGRATION.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LifeFromZero.City.Quarter
{
    /// <summary>Параметры карты района. Размер фиксирован: 750 м (ТЗ №031, без расширения).</summary>
    public sealed class DistrictMapOptions
    {
        /// <summary>Seed для наполнения (seed=1904 — эталон).</summary>
        public int Seed { get; set; } = 1904;
    }

    /// <summary>Точка входа генератора карты района.</summary>
    public sealed class DistrictMapGenerator
    {
        /// <summary>Сторона квадрата карты, метры.</summary>
        public const float MapSize = 750f;

        private readonly DistrictMapOptions _options;

        public DistrictMapGenerator(DistrictMapOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public QuarterPlan Generate()
        {
            var builder = new Builder(_options.Seed);
            return builder.Build();
        }

        /// <summary>Удобный вход: карта района с заданным seed.</summary>
        public static QuarterPlan Generate(int seed) =>
            new DistrictMapGenerator(new DistrictMapOptions { Seed = seed }).Generate();

        private sealed class Builder
        {
            // ---------- общие размеры (метры) ----------
            private const float RoadWidth = 7f;
            private const float SidewalkWidth = 2.5f;
            private const float DriveWidth = 6f;
            private const float Corridor = RoadWidth / 2f + SidewalkWidth; // 6 м: ось улицы -> внешний край тротуара

            // ---------- оси улиц: (координата линии, от, до) ----------
            private static readonly (float Line, float From, float To)[] VLines =
            {
                (20f, 20f, 738f), (150f, 20f, 738f), (270f, 20f, 738f),
                (420f, 20f, 738f), (505f, 20f, 600f), (730f, 20f, 738f),
            };

            private static readonly (float Line, float From, float To)[] HLines =
            {
                (20f, 20f, 730f), (200f, 20f, 730f), (300f, 20f, 730f),
                (470f, 20f, 730f), (600f, 20f, 730f), (738f, 20f, 730f),
            };

            /// <summary>Центр кругового перекрёстка (пересечение V270 × H300).</summary>
            private static readonly Vec2 RoundaboutCenter = new Vec2(270f, 300f);

            /// <summary>Проезды: (x0, z0, x1, z1) — въезды АЗС, въезды на парковки и склады, грузовые подъезды.</summary>
            private static readonly (float X0, float Z0, float X1, float Z1)[] Driveways =
            {
                (492f, 58f, 505f, 58f), (492f, 74f, 505f, 74f),      // АЗС-1 -> V505
                (115f, 672f, 150f, 672f), (115f, 688f, 150f, 688f),  // АЗС-2 -> V150
                (718f, 522f, 730f, 522f), (718f, 538f, 730f, 538f),  // АЗС-3 -> V730
                (115f, 387f, 150f, 387f), (115f, 403f, 150f, 403f),  // АЗС-4 -> V150 (южнее ломбарда, ТЗ №033)
                (345f, 470f, 345f, 492f),                            // парковка ТЦ «Горизонт» с H470
                (85f, 470f, 85f, 492f),                              // склад с H470
                (470f, 600f, 470f, 669f),                            // грузовой подъезд к заводу с H600
                (640f, 600f, 640f, 658f),                            // грузовой подъезд к производству с H600
            };

            // ---------- направления фасадов ----------
            private static readonly Vec2 North = new Vec2(0f, 1f);
            private static readonly Vec2 South = new Vec2(0f, -1f);
            private static readonly Vec2 East = new Vec2(1f, 0f);
            private static readonly Vec2 West = new Vec2(-1f, 0f);

            private static readonly string[] DoorColors = { "#5A3A22", "#2F4F6F", "#4A4A4A", "#6B2F2F", "#33523B" };

            // ---------- состояние сборки ----------
            private readonly int _seed;
            private readonly SeededRandom _rng;
            private readonly List<PlacedObject> _objects = new List<PlacedObject>();
            private readonly List<RoadSegment> _roads = new List<RoadSegment>();
            private readonly List<RoadNode> _nodes = new List<RoadNode>();
            private readonly Dictionary<(long, long), string> _nodeIds = new Dictionary<(long, long), string>();
            private readonly HashSet<(long, long)> _crossings = new HashSet<(long, long)>();
            private readonly List<BuildingInfo> _buildings = new List<BuildingInfo>();
            private readonly List<DeliveryPointInfo> _deliveries = new List<DeliveryPointInfo>();
            private readonly List<DistrictPointInfo> _points = new List<DistrictPointInfo>();
            private readonly Dictionary<int, int> _floorCounter = new Dictionary<int, int>();
            private PickupPointInfo _pickup;
            private GroceryShopInfo _shop;
            private PlayerHomeInfo _playerHome;
            private int _objectSeq;
            private int _roadSeq;
            private int _nodeSeq;
            private int _buildingSeq;

            internal Builder(int seed)
            {
                _seed = seed;
                _rng = new SeededRandom(seed);
            }

            internal QuarterPlan Build()
            {
                BuildStreets();
                EmitStreetObjects();

                BuildNorthQuarter();
                BuildSouthQuarter();
                BuildEastQuarter();
                BuildPrivateSector();
                BuildCentre();
                BuildBusinessZone();
                BuildWestBlock();
                BuildPickupPoint();
                BuildGasStations();
                BuildAutoRow();
                BuildIndustrialZone();
                BuildForest();
                BuildSquare();

                return new QuarterPlan(_seed, MapSize, _objects, _roads, _nodes, _buildings,
                                       new CourtyardInfo[0], _pickup, _playerHome, _shop, null,
                                       _deliveries, new RealEstateProperty[0],
                                       QuarterLayoutIds.District750, _points);
            }

            // ======================= общие помощники =======================

            private string NextObjectId() => "obj-" + (++_objectSeq).ToString("D4", CultureInfo.InvariantCulture);

            private PlacedObject AddObject(string typeId, Vec3 pos, float rotY, Vec3 size,
                                           Dictionary<string, string> parameters = null)
            {
                var obj = new PlacedObject(NextObjectId(), typeId, pos, rotY, size, parameters);
                _objects.Add(obj);
                return obj;
            }

            /// <summary>Короткая запись объекта: тип, центр на земле (y — база), поворот, размер, параметры.</summary>
            private void Put(string typeId, float x, float y, float z, float rot, float sx, float sy, float sz,
                             params (string Key, string Value)[] pairs)
            {
                AddObject(typeId, new Vec3(x, y, z), rot, new Vec3(sx, sy, sz), Params(pairs));
            }

            private static Dictionary<string, string> Params(params (string Key, string Value)[] pairs)
            {
                var dict = new Dictionary<string, string>();
                foreach ((string key, string value) in pairs)
                    dict[key] = value;
                return dict;
            }

            private static string Num(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

            private static string Inv(int v) => v.ToString(CultureInfo.InvariantCulture);

            private static (long, long) NodeKey(float x, float z) =>
                ((long)Math.Round(x * 100.0), (long)Math.Round(z * 100.0));

            private string NodeAt(float x, float z)
            {
                (long, long) key = NodeKey(x, z);
                if (_nodeIds.TryGetValue(key, out string existing))
                    return existing;
                string id = "node-" + (++_nodeSeq).ToString("D3", CultureInfo.InvariantCulture);
                _nodeIds[key] = id;
                _nodes.Add(new RoadNode(id, new Vec2(x, z), _crossings.Contains(key)));
                return id;
            }

            /// <summary>Прямоугольник по центру и размеру с учётом поворота 0/90.</summary>
            private static Rect2 RectC(float cx, float cz, float sx, float sz, float rot = 0f)
            {
                if (Math.Abs(rot) > 45f && Math.Abs(rot) < 135f) { float t = sx; sx = sz; sz = t; }
                return new Rect2(cx - sx / 2f, cz - sz / 2f, cx + sx / 2f, cz + sz / 2f);
            }

            private static float FaceRot(Vec2 normal)
            {
                if (normal.X > 0.5f) return 90f;
                if (normal.X < -0.5f) return 270f;
                if (normal.Z > 0.5f) return 0f;
                return 180f;
            }

            // ======================= улицы =======================

            /// <summary>
            /// Сетка улиц: пересечения осей, остановки на улицах (включая примыкания проездов),
            /// узлы графа и сегменты. Сегмент — между соседними остановками.
            /// </summary>
            private void BuildStreets()
            {
                var stopsV = new List<float>[VLines.Length];
                var stopsH = new List<float>[HLines.Length];
                for (int i = 0; i < VLines.Length; i++)
                    stopsV[i] = new List<float> { VLines[i].From, VLines[i].To };
                for (int j = 0; j < HLines.Length; j++)
                    stopsH[j] = new List<float> { HLines[j].From, HLines[j].To };

                for (int i = 0; i < VLines.Length; i++)
                {
                    for (int j = 0; j < HLines.Length; j++)
                    {
                        float x = VLines[i].Line;
                        float z = HLines[j].Line;
                        if (VLines[i].From <= z && z <= VLines[i].To && HLines[j].From <= x && x <= HLines[j].To)
                        {
                            _crossings.Add(NodeKey(x, z));
                            stopsV[i].Add(z);
                            stopsH[j].Add(x);
                        }
                    }
                }

                foreach ((float x0, float z0, float x1, float z1) in Driveways)
                {
                    AddDriveStop(x0, z0, stopsV, stopsH);
                    AddDriveStop(x1, z1, stopsV, stopsH);
                }

                for (int i = 0; i < VLines.Length; i++)
                {
                    List<float> ps = stopsV[i].Distinct().OrderBy(v => v).ToList();
                    float x = VLines[i].Line;
                    for (int k = 0; k + 1 < ps.Count; k++)
                    {
                        string a = NodeAt(x, ps[k]);
                        string b = NodeAt(x, ps[k + 1]);
                        _roads.Add(new RoadSegment(NextRoadId(), a, b,
                            new Vec2(x, ps[k]), new Vec2(x, ps[k + 1]), RoadWidth, RoadKind.Street));
                    }
                }
                for (int j = 0; j < HLines.Length; j++)
                {
                    List<float> ps = stopsH[j].Distinct().OrderBy(v => v).ToList();
                    float z = HLines[j].Line;
                    for (int k = 0; k + 1 < ps.Count; k++)
                    {
                        string a = NodeAt(ps[k], z);
                        string b = NodeAt(ps[k + 1], z);
                        _roads.Add(new RoadSegment(NextRoadId(), a, b,
                            new Vec2(ps[k], z), new Vec2(ps[k + 1], z), RoadWidth, RoadKind.Street));
                    }
                }
                foreach ((float x0, float z0, float x1, float z1) in Driveways)
                {
                    string a = NodeAt(x0, z0);
                    string b = NodeAt(x1, z1);
                    _roads.Add(new RoadSegment(NextRoadId(), a, b,
                        new Vec2(x0, z0), new Vec2(x1, z1), DriveWidth, RoadKind.Driveway));
                }
            }

            private void AddDriveStop(float px, float pz, List<float>[] stopsV, List<float>[] stopsH)
            {
                for (int i = 0; i < VLines.Length; i++)
                    if (VLines[i].Line == px && VLines[i].From <= pz && pz <= VLines[i].To)
                        stopsV[i].Add(pz);
                for (int j = 0; j < HLines.Length; j++)
                    if (HLines[j].Line == pz && HLines[j].From <= px && px <= HLines[j].To)
                        stopsH[j].Add(px);
            }

            private string NextRoadId() => "road-" + (++_roadSeq).ToString("D3", CultureInfo.InvariantCulture);

            /// <summary>Проезжая часть, тротуары, перекрёстки, переходы, фонари.</summary>
            private void EmitStreetObjects()
            {
                foreach (RoadSegment r in _roads)
                {
                    bool vertical = Math.Abs(r.Start.X - r.End.X) < 0.01f;
                    if (r.Kind == RoadKind.Driveway)
                    {
                        if (vertical)
                            Put(QuarterObjectTypes.Driveway, r.Start.X, 0f, (r.Start.Z + r.End.Z) / 2f, 90f,
                                Math.Abs(r.End.Z - r.Start.Z), 0.1f, DriveWidth);
                        else
                            Put(QuarterObjectTypes.Driveway, (r.Start.X + r.End.X) / 2f, 0f, r.Start.Z, 0f,
                                Math.Abs(r.End.X - r.Start.X), 0.1f, DriveWidth);
                        continue;
                    }
                    if (vertical)
                    {
                        float z0 = Math.Min(r.Start.Z, r.End.Z), z1 = Math.Max(r.Start.Z, r.End.Z);
                        float len = z1 - z0, cz = (z0 + z1) / 2f;
                        Put(QuarterObjectTypes.Road, r.Start.X, 0f, cz, 90f, len, 0.1f, RoadWidth);
                        for (int side = -1; side <= 1; side += 2)
                            Put(QuarterObjectTypes.Sidewalk, r.Start.X + side * (RoadWidth / 2f + SidewalkWidth / 2f),
                                0f, cz, 90f, len, 0.08f, SidewalkWidth);
                    }
                    else
                    {
                        float x0 = Math.Min(r.Start.X, r.End.X), x1 = Math.Max(r.Start.X, r.End.X);
                        float len = x1 - x0, cx = (x0 + x1) / 2f;
                        Put(QuarterObjectTypes.Road, cx, 0f, r.Start.Z, 0f, len, 0.1f, RoadWidth);
                        for (int side = -1; side <= 1; side += 2)
                            Put(QuarterObjectTypes.Sidewalk, cx, 0f, r.Start.Z + side * (RoadWidth / 2f + SidewalkWidth / 2f),
                                0f, len, 0.08f, SidewalkWidth);
                    }
                }

                foreach (RoadNode node in _nodes)
                {
                    if (!node.IsIntersection) continue;
                    Put(QuarterObjectTypes.Intersection, node.Position.X, 0f, node.Position.Z, 0f,
                        RoadWidth, 0.11f, RoadWidth);
                    if (node.Position.X == RoundaboutCenter.X && node.Position.Z == RoundaboutCenter.Z)
                        continue; // в круговом перекрёстке переходов нет: кольцо объезжают
                    float off = RoadWidth / 2f + 2.2f;
                    Put(QuarterObjectTypes.Crosswalk, node.Position.X, 0f, node.Position.Z + off, 0f, RoadWidth, 0.12f, 3f);
                    Put(QuarterObjectTypes.Crosswalk, node.Position.X, 0f, node.Position.Z - off, 0f, RoadWidth, 0.12f, 3f);
                    Put(QuarterObjectTypes.Crosswalk, node.Position.X + off, 0f, node.Position.Z, 90f, RoadWidth, 0.12f, 3f);
                    Put(QuarterObjectTypes.Crosswalk, node.Position.X - off, 0f, node.Position.Z, 90f, RoadWidth, 0.12f, 3f);
                }

                // Фонари: каждые 40 м, чередуя стороны улицы.
                foreach (RoadSegment r in _roads)
                {
                    if (r.Kind != RoadKind.Street) continue;
                    bool vertical = Math.Abs(r.Start.X - r.End.X) < 0.01f;
                    float a = vertical ? Math.Min(r.Start.Z, r.End.Z) : Math.Min(r.Start.X, r.End.X);
                    float b = vertical ? Math.Max(r.Start.Z, r.End.Z) : Math.Max(r.Start.X, r.End.X);
                    int side = -1;
                    for (float s = a + 20f; s < b - 10f; s += 40f)
                    {
                        float offset = side * (RoadWidth / 2f + SidewalkWidth / 2f);
                        if (vertical)
                            Put(QuarterObjectTypes.StreetLamp, r.Start.X + offset, 0f, s, 0f, 0.3f, 6f, 0.3f);
                        else
                            Put(QuarterObjectTypes.StreetLamp, s, 0f, r.Start.Z + offset, 0f, 0.3f, 6f, 0.3f);
                        side = -side;
                    }
                }

                // Один небольшой круговой перекрёсток на пересечении V270 × H300.
                Put(DistrictObjectTypes.Roundabout, RoundaboutCenter.X, 0f, RoundaboutCenter.Z, 0f, 16f, 0.12f, 16f);
                Put(QuarterObjectTypes.Lawn, RoundaboutCenter.X, 0f, RoundaboutCenter.Z, 0f, 7f, 0.2f, 7f);
            }

            // ======================= помощники для территорий =======================

            /// <summary>Парковка: площадка и парковочные места (одна линия, не больше stalls).</summary>
            private void AddLot(Rect2 r, int stalls)
            {
                float w = r.Width, h = r.Height;
                Put(QuarterObjectTypes.ParkingLot, r.Center.X, 0f, r.Center.Z, 0f, w, 0.07f, h);
                int n = Math.Min(stalls, (int)Math.Floor(w / 3f));
                for (int i = 0; i < n; i++)
                {
                    float x = r.MinX + (i + 0.5f) * (w / n);
                    Put(QuarterObjectTypes.ParkingStall, x, 0f, r.Center.Z, 0f, 2.4f, 0.06f, 4.8f);
                }
            }

            /// <summary>Декоративный газон (без физики; не перекрывает здания).</summary>
            private void AddLawn(Rect2 r)
            {
                Put(QuarterObjectTypes.Lawn, r.Center.X, 0f, r.Center.Z, 0f, r.Width, 0.06f, r.Height);
            }

            private void AddTree(float x, float z, float crown)
            {
                Put(QuarterObjectTypes.Tree, x, 0f, z, 0f, crown, 4.5f, crown,
                    ("crownDiameter", Num(crown)), ("trunkHeight", "2.2"));
            }

            private void AddBench(float x, float z, float rot)
            {
                Put(QuarterObjectTypes.Bench, x, 0f, z, rot, 2f, 0.6f, 0.6f);
            }

            private void AddPlayground(float cx, float cz, float sx, float sz)
            {
                AddPlayground(cx, cz, sx, sz, new (string, string)[0]);
            }

            /// <summary>Площадка с параметрами (например, футбольная: sport=football).</summary>
            private void AddPlayground(float cx, float cz, float sx, float sz,
                                       params (string Key, string Value)[] pairs)
            {
                Put(QuarterObjectTypes.Playground, cx, 0f, cz, 0f, sx, 1.2f, sz, pairs);
            }

            // ======================= жилые дома =======================

            /// <summary>
            /// Жилой дом по архетипу (панель, кирпич, современная башня, частный дом).
            /// Вход по фасаду normal: подъезды распределены по длине, у каждого — точка доставки.
            /// </summary>
            private void AddResidential(string bid, BuildingArchetype arch, float cx, float cz, float rot,
                                        float length, Vec2 normal, bool isHome)
            {
                float depth = arch.Depth;
                float height = arch.Floors * arch.FloorHeight;
                var center = new Vec2(cx, cz);
                BuildingVariant variant = PickVariant(arch.Floors, out string color, out int balconyOffset, out float wear);
                string doorColor = _rng.Pick(DoorColors);

                int n = Math.Max(1, (int)Math.Round((double)(length / arch.EntranceSpacing)));
                var entrances = new List<Vec2>();
                for (int e = 0; e < n; e++)
                {
                    float along = length * (e + 1) / (n + 1) - length / 2f;
                    Vec2 alongVec = rot == 0f ? new Vec2(along, 0f) : new Vec2(0f, along);
                    Vec2 p = center + alongVec + normal * (depth / 2f + 0.4f);
                    entrances.Add(p);
                    Put(QuarterObjectTypes.Entrance, p.X, 0f, p.Z, rot, 3f, 3f, 1f,
                        ("buildingId", bid), ("entranceNumber", Inv(e + 1)),
                        ("normalX", Num(normal.X)), ("normalZ", Num(normal.Z)),
                        ("entranceStyle", variant.EntranceStyle), ("doorColor", doorColor));
                    Vec2 d = p + normal * 1f;
                    string did = "drop-" + bid + "-" + Inv(e + 1);
                    _deliveries.Add(new DeliveryPointInfo(did, bid, e + 1, new Vec3(d.X, 0f, d.Z),
                        arch.Id.StartsWith("building.private.", StringComparison.Ordinal) ? "private" : "residential"));
                    Put(QuarterObjectTypes.DeliveryDropoff, d.X, 0f, d.Z, 0f, 0.7f, 0.05f, 0.7f,
                        ("pointId", did), ("buildingId", bid), ("entranceNumber", Inv(e + 1)),
                        ("interactionRadius", Num(DeliveryPointInfo.DefaultInteractionRadius)));
                }

                var parameters = Params(
                    ("buildingId", bid), ("archetype", arch.Id),
                    ("floors", Inv(arch.Floors)), ("entrances", Inv(n)),
                    ("style", variant.FacadeStyle), ("roof", variant.RoofType),
                    ("facadeColor", color),
                    ("variant", variant.Id), ("roofVariant", variant.RoofVariant),
                    ("windowStyle", variant.WindowStyle), ("balconyType", variant.BalconyType),
                    ("balconyOffset", Inv(balconyOffset)),
                    ("wear", wear.ToString("0.##", CultureInfo.InvariantCulture)),
                    ("entranceStyle", variant.EntranceStyle), ("doorColor", doorColor));
                foreach (KeyValuePair<string, string> kv in FacadeDetailParams(length, arch.Floors))
                    parameters[kv.Key] = kv.Value;
                if (isHome)
                {
                    parameters["specialId"] = PlayerHomeInfo.SpecialId;
                    parameters["apartment"] = Inv(PlayerHomeInfo.ApartmentNumber);
                }
                AddObject(QuarterObjectTypes.Building, new Vec3(cx, 0f, cz), rot, new Vec3(length, height, depth), parameters);

                _buildings.Add(new BuildingInfo(bid, arch.Id, center, rot, new Vec3(length, height, depth),
                    arch.Floors, entrances, variant.FacadeStyle, variant.RoofType, color, isHome,
                    variant.Id, variant.WindowStyle, variant.BalconyType, variant.RoofVariant,
                    wear, balconyOffset));

                if (isHome)
                {
                    // Квартира №67 (карта района v1.1): дом выбран юго-западным в северном квартале,
                    // южнее футбольной площадки. Дверь — первый подъезд на фасаде normal, выход —
                    // в 2 м наружу, во двор. Это постоянная привязка: координаты берутся из центра
                    // дома и его глубины, вручную не дублируются.
                    Vec2 door = entrances[0];
                    Vec2 exit = door + normal * 2f;
                    _playerHome = new PlayerHomeInfo(bid, center, rot,
                        new Vec3(door.X, 0f, door.Z), new Vec3(exit.X, 0f, exit.Z));
                    Put(QuarterObjectTypes.PlayerHomeDoor, door.X, 0f, door.Z, rot, 1.2f, 2.2f, 0.2f,
                        ("buildingId", bid), ("specialId", PlayerHomeInfo.SpecialId),
                        ("purpose", "player.home.door"),
                        ("apartment", Inv(PlayerHomeInfo.ApartmentNumber)));
                    Put(QuarterObjectTypes.PlayerHomeExit, exit.X, 0f, exit.Z, rot, 0.8f, 0.05f, 0.8f,
                        ("buildingId", bid), ("specialId", PlayerHomeInfo.SpecialId),
                        ("purpose", "player.home.exit"),
                        ("apartment", Inv(PlayerHomeInfo.ApartmentNumber)));
                    Put(QuarterObjectTypes.PlayerHomeMarker, cx, 0f, cz, rot, 1f, 0.05f, 1f,
                        ("buildingId", bid), ("specialId", PlayerHomeInfo.SpecialId),
                        ("apartment", Inv(PlayerHomeInfo.ApartmentNumber)));
                }
            }

            /// <summary>
            /// Вариант фасада: по кругу внутри этажности (3 варианта на тип), цвет и износ — из seed.
            /// </summary>
            private BuildingVariant PickVariant(int floors, out string facadeColor, out int balconyOffset, out float wear)
            {
                IReadOnlyList<BuildingVariant> variants = BuildingVariantCatalog.ForFloors(floors);
                int counter = _floorCounter.TryGetValue(floors, out int c) ? c : 0;
                _floorCounter[floors] = counter + 1;
                BuildingVariant variant = variants[counter % variants.Count];
                facadeColor = _rng.Pick(variant.Palette);
                balconyOffset = _rng.NextInt(0, 3);
                wear = variant.Wear + _rng.NextRange(-0.08f, 0.12f);
                if (wear < 0f) wear = 0f;
                if (wear > 1f) wear = 1f;
                return variant;
            }

            /// <summary>Фасадные мелочи (как в QuarterGenerator): кондиционеры, заплатки, водостоки.</summary>
            private Dictionary<string, string> FacadeDetailParams(float length, int floors)
            {
                int perFloor = Math.Max(2, (int)Math.Floor(length / 3f));
                var ac = new List<string>();
                int acCount = _rng.NextInt(0, floors >= 9 ? 5 : 3);
                for (int i = 0; i < acCount; i++)
                    ac.Add(_rng.NextInt(1, floors) + ":" + _rng.NextInt(0, perFloor) + ":" + (_rng.NextChance(0.5f) ? "1" : "-1"));
                var patches = new List<string>();
                int patchCount = _rng.NextInt(0, 3);
                for (int i = 0; i < patchCount; i++)
                    patches.Add(_rng.NextInt(0, floors) + ":" + _rng.NextInt(0, perFloor) + ":" + (_rng.NextChance(0.5f) ? "1" : "-1"));
                return new Dictionary<string, string>
                {
                    { "windowsPerFloor", Inv(perFloor) },
                    { "acUnits", string.Join(";", ac) },
                    { "patchWindows", string.Join(";", patches) },
                    { "drainpipes", Num(-length / 2f + 1.2f) + ";" + Num(length / 2f - 1.2f) },
                };
            }

            // ======================= коммерция =======================

            /// <summary>
            /// Коммерческое здание: корпус, вывеска над фасадом, дверь, точка доставки
            /// (1.2 м наружу от входа). Возвращает запись POI.
            /// </summary>
            private DistrictPointInfo AddCommerce(string cid, string ctype, string name, float cx, float cz,
                                                  float length, float depth, float height, Vec2 normal,
                                                  string color, int entranceCount)
            {
                float half = normal.X != 0f ? length / 2f : depth / 2f;
                float rot = FaceRot(normal);
                var entrances = new List<Vec3>();
                Vec3 firstInteraction = new Vec3(0f, 0f, 0f);
                for (int k = 0; k < entranceCount; k++)
                {
                    float ex, ez;
                    if (entranceCount == 1)
                    {
                        ex = cx + normal.X * (half + 0.3f);
                        ez = cz + normal.Z * (half + 0.3f);
                    }
                    else
                    {
                        float off = k == 0 ? -length / 4f : length / 4f;
                        ex = cx + off;
                        ez = cz + normal.Z * (depth / 2f + 0.3f);
                    }
                    float ix = ex + normal.X * 1.2f, iz = ez + normal.Z * 1.2f;
                    entrances.Add(new Vec3(ex, 0f, ez));
                    if (k == 0) firstInteraction = new Vec3(ix, 0f, iz);
                    Put(DistrictObjectTypes.Door, ex, 0f, ez, rot, 1.4f, 2.3f, 0.15f);
                    string did = "drop-" + cid + "-" + Inv(k + 1);
                    _deliveries.Add(new DeliveryPointInfo(did, cid, k + 1, new Vec3(ix, 0f, iz), ctype));
                    Put(QuarterObjectTypes.DeliveryDropoff, ix, 0f, iz, 0f, 0.7f, 0.05f, 0.7f,
                        ("pointId", did), ("buildingId", cid), ("entranceNumber", Inv(k + 1)),
                        ("interactionRadius", Num(DeliveryPointInfo.DefaultInteractionRadius)));
                }

                Rect2 footprint = RectC(cx, cz, length, depth);
                // Фасадные детали (окна, панели) — через BuildBuildingDetails; без балконов и интерьера.
                int floorsCount = Math.Max(1, (int)Math.Floor(height / 3.2f));
                int windowsPerFloor = 2 * Math.Max(1, (int)Math.Round(length / 6f)); // чётное число: по центру фасада дверь
                Put(DistrictObjectTypes.Commerce, cx, 0f, cz, 0f, length, height, depth,
                    ("id", cid), ("type", ctype), ("name", name), ("facadeColor", color),
                    ("style", ctype == "warehouse" ? "panel" : "brick"),
                    ("floors", Inv(floorsCount)), ("windowsPerFloor", Inv(windowsPerFloor)),
                    ("windowStyle", FacadeWindowStyle(cid)), ("roofVariant", "flat"), ("balconyType", "none"),
                    ("balconyOffset", "0"), ("wear", "0.2"), ("acUnits", ""), ("patchWindows", ""));
                float signWidth = normal.X == 0f ? Math.Min(length * 0.6f, 8f) : Math.Min(depth * 0.6f, 8f);
                // Вывеска выше двери (дверь до 2.3 м): центр 2.75, низ 2.35, верх 3.15 (все фасады ≥ 3.4 м).
                Put(DistrictObjectTypes.Sign, cx + normal.X * (half + 0.15f), 2.75f, cz + normal.Z * (half + 0.15f),
                    rot, signWidth, 0.8f, 0.18f, ("text", name));

                var point = new DistrictPointInfo(cid, ctype, name, new Vec2(cx, cz), footprint,
                    entrances, firstInteraction);
                _points.Add(point);
                return point;
            }

            /// <summary>Окна коммерции: разные типы у магазина, банка, больницы и бизнес-центра.</summary>
            private static string FacadeWindowStyle(string cid)
            {
                switch (cid)
                {
                    case "shop-01":
                    case "com-bank": return "wide";
                    case "com-hospital":
                    case "com-business": return "tall";
                    default: return "standard";
                }
            }

            /// <summary>Парковка, навес, острова и колонки, киоск. Въезд и выезд — проезды к улице.</summary>
            private void AddGasStation(string gid, float cx, float cz)
            {
                Rect2 pad = RectC(cx, cz, 60f, 34f);
                Put(QuarterObjectTypes.ParkingLot, cx, 0f, cz, 0f, 60f, 0.07f, 34f);
                Put(DistrictObjectTypes.GasCanopy, cx, 4.5f, cz, 0f, 26f, 0.5f, 14f);
                foreach (float dx in new[] { -12f, 12f })
                    foreach (float dz in new[] { -6f, 6f })
                        Put(DistrictObjectTypes.GasPost, cx + dx, 0f, cz + dz, 0f, 0.3f, 4.5f, 0.3f);
                foreach (float ix in new[] { cx - 6f, cx + 6f })
                {
                    Put(DistrictObjectTypes.GasIsland, ix, 0f, cz, 0f, 1.6f, 0.2f, 9f);
                    foreach (float pz in new[] { cz - 3.2f, cz + 3.2f })
                        Put(DistrictObjectTypes.GasPump, ix, 0f, pz, 0f, 1f, 1.4f, 0.6f);
                }
                Put(DistrictObjectTypes.GasKiosk, cx - 22f, 0f, cz - 10f, 0f, 6f, 3f, 4f);
                _points.Add(new DistrictPointInfo(gid, "gas.station", "АЗС", new Vec2(cx, cz), pad,
                    new[] { new Vec3(cx + 30f, 0f, cz - 8f), new Vec3(cx + 30f, 0f, cz + 8f) }, null));
                // Въезд и выезд — проезды из таблицы Driveways (их строит BuildStreets).
            }

            // ======================= кварталы =======================

            // ---------- северный двор: футбольная площадка (карта района v1.1) ----------
            /// <summary>Центр футбольной площадки северного двора (карта района v1.1).</summary>
            private static readonly Vec2 NorthPitchCenter = new Vec2(210f, 660f);

            /// <summary>Размер футбольной площадки северного двора: по X (14 м) и по Z (10 м).</summary>
            private const float NorthPitchWidth = 14f;
            private const float NorthPitchDepth = 10f;

            /// <summary>Жилой дом северного квартала: id, архетип, центр, поворот, длина, нормаль фасада.</summary>
            private readonly struct NorthBuilding
            {
                public NorthBuilding(string id, BuildingArchetype arch, float x, float z,
                                     float rot, float length, Vec2 normal)
                {
                    Id = id; Arch = arch; X = x; Z = z; Rot = rot; Length = length; Normal = normal;
                }

                public string Id { get; }
                public BuildingArchetype Arch { get; }
                public float X { get; }
                public float Z { get; }
                public float Rot { get; }
                public float Length { get; }
                public Vec2 Normal { get; }
            }

            /// <summary>
            /// Северный квартал (карта района v1.1): 4 многоквартирных дома, двор с футбольной
            /// площадкой. Квартира №67 привязана к дому, ВЫБРАННОМУ ПО ДАННЫМ раскладки:
            /// самый юго-западный дом квартала, стоящий южнее футбольной площадки
            /// (см. <see cref="PickPlayerHomeId"/>). Координаты дома и двора не дублируются:
            /// дверь, выход и маркер player.home выводит AddResidential из этих же данных.
            /// </summary>
            private void BuildNorthQuarter()
            {
                // Северный квартал: 4 дома, двор с футбольной площадкой и лавочками.
                AddLawn(new Rect2(160f, 640f, 260f, 680f));
                AddPlayground(NorthPitchCenter.X, NorthPitchCenter.Z, NorthPitchWidth, NorthPitchDepth,
                    ("sport", "football"), ("kind", "pitch"), ("size", "14x10"));
                AddBench(180f, 650f, 0f);
                AddBench(240f, 670f, 0f);
                AddTree(170f, 646f, 3.2f);
                AddTree(250f, 646f, 3.2f);
                AddTree(172f, 674f, 3.2f);
                AddTree(248f, 676f, 3.2f);

                var buildings = new[]
                {
                    new NorthBuilding("bld-n1", BuildingArchetype.PanelFiveStorey(), 210f, 690f, 0f, 30f, South),
                    new NorthBuilding("bld-n3", BuildingArchetype.PanelFiveStorey(), 210f, 630f, 0f, 30f, North),
                    new NorthBuilding("bld-n2", BuildingArchetype.BrickThreeStorey(), 345f, 690f, 0f, 24f, South),
                    new NorthBuilding("bld-n4", BuildingArchetype.PanelFiveStorey(), 345f, 630f, 0f, 36f, North),
                };
                Rect2 pitch = Rect2.FromCenterSize(NorthPitchCenter, NorthPitchWidth, NorthPitchDepth);
                string homeId = PickPlayerHomeId(buildings, pitch);
                foreach (NorthBuilding b in buildings)
                    AddResidential(b.Id, b.Arch, b.X, b.Z, b.Rot, b.Length, b.Normal, b.Id == homeId);

                AddLot(new Rect2(286f, 644f, 340f, 662f), 6);
                AddLot(new Rect2(352f, 644f, 404f, 662f), 6);
                AddLawn(new Rect2(280f, 664f, 410f, 680f));
                AddTree(296f, 674f, 3f);
                AddTree(392f, 674f, 3f);
            }

            /// <summary>
            /// Дом квартиры №67 по данным раскладки: самый юго-западный многоквартирный дом
            /// северного квартала, стоящий южнее футбольной площадки.
            /// Правило: кандидаты — дома, центр которых южнее нижней границы площадки
            /// (Z меньше pitch.MinZ); среди них берётся самый западный (минимальный X).
            /// Ноль кандидатов или несколько домов на одинаковом X — ошибка раскладки,
            /// а не повод угадывать: генерация останавливается с понятным сообщением.
            /// </summary>
            private static string PickPlayerHomeId(IReadOnlyList<NorthBuilding> buildings, Rect2 pitch)
            {
                var south = new List<NorthBuilding>();
                foreach (NorthBuilding b in buildings)
                    if (b.Z < pitch.MinZ)
                        south.Add(b);
                if (south.Count == 0)
                    throw new InvalidOperationException(
                        "Квартира №67: ни один дом северного квартала не стоит южнее футбольной площадки " +
                        "(Z < " + Num(pitch.MinZ) + "). Раскладка изменена — укажите дом player.home явно.");

                float west = south[0].X;
                foreach (NorthBuilding b in south)
                    if (b.X < west)
                        west = b.X;

                var candidates = new List<string>();
                foreach (NorthBuilding b in south)
                    if (Math.Abs(b.X - west) < 0.01f)
                        candidates.Add(b.Id);
                if (candidates.Count != 1)
                    throw new InvalidOperationException(
                        "Квартира №67: юго-западный дом северного квартала определён неоднозначно (" +
                        string.Join(", ", candidates.ToArray()) + "). Нужен ручной выбор в DistrictMapGenerator.");

                return candidates[0];
            }

            private void BuildSouthQuarter()
            {
                // Южный квартал: 3 панельных дома, двор, парковка.
                AddLawn(new Rect2(160f, 70f, 262f, 140f));
                AddPlayground(200f, 100f, 40f, 16f);
                AddBench(230f, 120f, 0f);
                AddTree(176f, 124f, 3.2f);
                AddTree(246f, 122f, 3.2f);
                AddTree(178f, 80f, 3.2f);
                AddLot(new Rect2(228f, 72f, 258f, 100f), 6);
                AddResidential("bld-s1", BuildingArchetype.PanelFiveStorey(), 210f, 150f, 0f, 30f, South, false);
                AddResidential("bld-s2", BuildingArchetype.PanelFiveStorey(), 210f, 60f, 0f, 30f, North, false);
                AddResidential("bld-s3", BuildingArchetype.PanelFiveStorey(), 340f, 105f, 90f, 30f, West, false);
                AddLot(new Rect2(280f, 95f, 325f, 115f), 6);
            }

            private void BuildEastQuarter()
            {
                // Восточный квартал: 3 современные башни 10 этажей, двор и парковки.
                AddLot(new Rect2(520f, 215f, 715f, 250f), 14);
                AddLot(new Rect2(530f, 320f, 700f, 355f), 12);
                AddLawn(new Rect2(550f, 362f, 680f, 376f));
                AddTree(560f, 368f, 3f);
                AddTree(615f, 368f, 3f);
                AddTree(670f, 368f, 3f);
                AddResidential("bld-e1", BuildingArchetype.ModernTenStorey(), 540f, 400f, 90f, 30f, East, false);
                AddResidential("bld-e2", BuildingArchetype.ModernTenStorey(), 620f, 400f, 90f, 30f, West, false);
                AddResidential("bld-e3", BuildingArchetype.ModernTenStorey(), 690f, 400f, 90f, 30f, West, false);
            }

            private void BuildPrivateSector()
            {
                // Частный сектор: 5 небольших домов в юго-восточной части.
                AddResidential("bld-p1", BuildingArchetype.PrivateModernHouse(), 545f, 150f, 0f, 11f, North, false);
                AddResidential("bld-p2", BuildingArchetype.PrivateClassicHouse(), 605f, 150f, 0f, 10f, North, false);
                AddResidential("bld-p3", BuildingArchetype.PrivateModernHouse(), 665f, 150f, 0f, 11f, North, false);
                AddResidential("bld-p4", BuildingArchetype.PrivateClassicHouse(), 575f, 80f, 0f, 10f, South, false);
                AddResidential("bld-p5", BuildingArchetype.PrivateModernHouse(), 640f, 80f, 0f, 11f, South, false);
            }

            private void BuildCentre()
            {
                // ТЦ «Горизонт»: два входа, парковка с въездом с H470.
                AddCommerce("com-mall", "mall", "ТЦ «ГОРИЗОНТ»", 345f, 535f, 120f, 50f, 9f, South, "#E8E4DC", 2);
                AddLot(new Rect2(285f, 480f, 339f, 504f), 10);
                AddLot(new Rect2(351f, 480f, 405f, 504f), 10);
                AddCommerce("com-bank", "bank", "БАНК", 390f, 380f, 30f, 22f, 7f, East, "#2F5F8A", 1);
                AddCommerce("com-city-hall", "city.hall", "МЭРИЯ", 330f, 430f, 44f, 24f, 7.5f, North, "#D9CFA8", 1);
                AddLawn(new Rect2(300f, 350f, 362f, 400f));
                AddTree(312f, 360f, 3f);
                AddTree(350f, 395f, 3f);
                AddTree(316f, 392f, 3f);
                AddBench(338f, 362f, 0f);
                AddBench(322f, 404f, 0f);
            }

            private void BuildBusinessZone()
            {
                // Бизнес-центр, полиция, больница — вдоль V505; курсы и инкубатор.
                AddCommerce("com-business", "business.center", "БИЗНЕС-ЦЕНТР", 470f, 428f, 36f, 22f, 16f, East, "#5B6F82", 1);
                AddCommerce("com-police", "police", "ПОЛИЦИЯ", 470f, 378f, 30f, 18f, 6f, East, "#34495E", 1);
                AddCommerce("com-hospital", "hospital", "БОЛЬНИЦА", 470f, 333f, 36f, 22f, 8f, East, "#E9EDF0", 1);
                AddLot(new Rect2(430f, 215f, 495f, 290f), 8);
                AddCommerce("com-incubator", "incubator", "БИЗНЕС-ИНКУБАТОР", 345f, 250f, 40f, 26f, 9f, South, "#7A8C6E", 1);
                AddCommerce("com-education", "education", "КУРСЫ", 462f, 150f, 40f, 22f, 6f, East, "#C9A66B", 1);
            }

            private void BuildWestBlock()
            {
                // Продукты (супермаркет), кафе, ресторан, одежда — колонка у V150.
                DistrictPointInfo shop = AddCommerce("shop-01", "grocery", "ПРОДУКТЫ", 210f, 250f, 44f, 26f, 3.4f, West, "#C8D8C0", 1);
                Vec3 shopEntrance = shop.Entrances[0];
                _shop = new GroceryShopInfo("shop-01", new Vec2(210f, 250f), new Vec3(44f, 3.4f, 26f),
                    shopEntrance, shop.InteractionPoint.Value,
                    new Rect2(shopEntrance.X - 2f, shop.Center.Z - 6f, shopEntrance.X, shop.Center.Z + 6f),
                    shop.Footprint);
                AddCommerce("com-cafe", "cafe", "КАФЕ", 190f, 425f, 26f, 16f, 4f, West, "#D8A878", 1);
                AddCommerce("com-restaurant", "restaurant", "РЕСТОРАН", 190f, 390f, 26f, 16f, 4.6f, West, "#8E3B3B", 1);
                AddCommerce("com-clothing", "clothing", "ОДЕЖДА", 190f, 355f, 26f, 16f, 4f, West, "#A9B7C6", 1);
                // Строймаркет: большой корпус с въездной парковкой.
                AddCommerce("com-hardware", "hardware", "СТРОЙМАРКЕТ", 85f, 250f, 70f, 44f, 8f, East, "#D1A44A", 1);
                AddLot(new Rect2(40f, 276f, 130f, 290f), 10);
                // Ломбард — над АЗС-4.
                AddCommerce("com-pawnshop", "pawnshop", "ЛОМБАРД", 85f, 425f, 30f, 18f, 5f, East, "#4F5D75", 1);
                // Склад / логистика (запад, у H470).
                AddCommerce("com-warehouse", "warehouse", "СКЛАД", 85f, 535f, 70f, 36f, 8f, South, "#8C8C86", 1);
                AddLot(new Rect2(52f, 480f, 82f, 510f), 0);
                AddLot(new Rect2(88f, 480f, 118f, 510f), 0);
            }

            private void BuildPickupPoint()
            {
                // ПВЗ — единственный пункт выдачи (та же форма, что PickupPointInfo в квартале), фасад на юг.
                const string pvzId = "pvz-01";
                const float frontZ = 312f, centerZ = 316f; // ТЗ №033: ПВЗ перенесён в Западный район, к H300
                const float cx = 85f;
                Rect2 footprint = new Rect2(cx - 6f, frontZ, cx + 6f, frontZ + 8f);
                Rect2 plaza = new Rect2(cx - 7f, frontZ - 4f, cx + 7f, frontZ);
                var entrance = new Vec3(cx, 0f, frontZ - 0.3f);
                var interaction = new Vec3(cx, 0f, frontZ - 1.6f);

                Put(QuarterObjectTypes.PickupBuilding, cx, 0f, centerZ, 0f, 12f, 3.2f, 8f,
                    ("buildingId", pvzId), ("floors", "1"), ("roof", "flat"),
                    ("signText", PickupPointInfo.SignText),
                    ("entrance", Num(entrance.X) + "," + Num(entrance.Z)),
                    ("interactionPoint", Num(interaction.X) + "," + Num(interaction.Z)));
                Put(QuarterObjectTypes.PickupRoof, cx, 3.2f, centerZ, 0f, 12.6f, 0.25f, 8.6f, ("buildingId", pvzId));
                Put(QuarterObjectTypes.PickupDoor, cx, 0f, frontZ - 0.08f, 180f, 1.2f, 2.2f, 0.15f, ("buildingId", pvzId));
                foreach (float ox in new[] { -4.4f, -2.6f, 2.6f, 4.4f })
                    Put(QuarterObjectTypes.PickupWindow, cx + ox, 0.9f, frontZ - 0.06f, 180f, 1.6f, 1.2f, 0.1f, ("buildingId", pvzId));
                Put(QuarterObjectTypes.PickupSign, cx, 2.2f, frontZ - 0.15f, 180f, 6.5f, 0.8f, 0.18f,
                    ("buildingId", pvzId), ("text", PickupPointInfo.SignText));
                Put(QuarterObjectTypes.PickupPlaza, plaza.Center.X, 0f, plaza.Center.Z, 0f, plaza.Width, 0.07f, plaza.Height,
                    ("buildingId", pvzId));
                Put(QuarterObjectTypes.PickupPoint, interaction.X, 0f, interaction.Z, 0f, 0.8f, 0.05f, 0.8f,
                    ("buildingId", pvzId), ("purpose", "delivery.pickup"), ("interactionRadius", "1.5"));

                _pickup = new PickupPointInfo(pvzId, new Vec2(cx, centerZ), 0f, new Vec3(12f, 3.2f, 8f),
                    entrance, interaction, plaza, footprint);
                _points.Add(new DistrictPointInfo(pvzId, "pickup", PickupPointInfo.SignText, new Vec2(cx, centerZ), footprint,
                    new[] { entrance }, interaction));
                _deliveries.Add(new DeliveryPointInfo("drop-" + pvzId, pvzId, 1, new Vec3(interaction.X, 0f, interaction.Z), "pickup"));
                Put(QuarterObjectTypes.DeliveryDropoff, interaction.X, 0f, interaction.Z, 0f, 0.7f, 0.05f, 0.7f,
                    ("pointId", "drop-" + pvzId), ("buildingId", pvzId), ("entranceNumber", "1"),
                    ("interactionRadius", Num(DeliveryPointInfo.DefaultInteractionRadius)));
            }

            private void BuildGasStations()
            {
                AddGasStation("gas-04", 85f, 395f);   // АЗС-4: непосредственно к югу от ломбарда (85, 425)
                AddGasStation("gas-02", 85f, 680f);   // АЗС-2: северо-запад
                AddGasStation("gas-01", 462f, 66f);   // АЗС-1: юг
                AddGasStation("gas-03", 688f, 530f);  // АЗС-3: восток
            }

            private void BuildAutoRow()
            {
                // Автосервис и тюнинг — выход на H470; парковка вдоль.
                AddCommerce("com-carservice", "car.service", "АВТОСЕРВИС", 535f, 535f, 36f, 24f, 6f, South, "#6E7B8B", 1);
                AddCommerce("com-tuning", "tuning", "ТЮНИНГ", 600f, 535f, 36f, 24f, 6f, South, "#2B2D42", 1);
                AddLot(new Rect2(511f, 480f, 724f, 505f), 16);
            }

            private void BuildIndustrialZone()
            {
                // Промзона (северо-восток): завод, корпус производства/склада, погрузка, грузовые подъезды.
                Put(DistrictObjectTypes.IndustryBuilding, 500f, 0f, 690f, 0f, 100f, 12f, 40f,
                    ("facadeColor", "#6B6F73"), ("name", "ЗАВОД"), ("style", "panel"), ("floors", "3"),
                    ("windowsPerFloor", "34"), ("windowStyle", "wide"), ("roofVariant", "flat"),
                    ("balconyType", "none"), ("balconyOffset", "0"), ("wear", "0.3"), ("acUnits", ""), ("patchWindows", ""));
                Put(DistrictObjectTypes.Door, 470f, 0f, 670f, 0f, 6f, 4.5f, 0.3f, ("kind", "gate")); // ворота завода, не solid
                Put(DistrictObjectTypes.IndustryChimney, 532f, 0f, 712f, 0f, 2.4f, 30f, 2.4f);
                Put(DistrictObjectTypes.IndustryChimney, 538f, 0f, 712f, 0f, 2.4f, 30f, 2.4f);
                _points.Add(new DistrictPointInfo("ind-factory", "factory", "ЗАВОД", new Vec2(500f, 690f),
                    RectC(500f, 690f, 100f, 40f),
                    new[] { new Vec3(470f, 0f, 669f) }, new Vec3(478f, 0f, 666f)));
                _deliveries.Add(new DeliveryPointInfo("drop-ind-factory-gate", "ind-factory", 1,
                    new Vec3(478f, 0f, 666f), "logistics"));
                Put(QuarterObjectTypes.DeliveryDropoff, 478f, 0f, 666f, 0f, 0.7f, 0.05f, 0.7f,
                    ("pointId", "drop-ind-factory-gate"), ("buildingId", "ind-factory"), ("entranceNumber", "1"),
                    ("interactionRadius", Num(DeliveryPointInfo.DefaultInteractionRadius)));

                Put(DistrictObjectTypes.IndustryBuilding, 640f, 0f, 690f, 0f, 80f, 8f, 30f,
                    ("facadeColor", "#7D8B6A"), ("name", "ПРОИЗВОДСТВО"), ("style", "panel"), ("floors", "2"),
                    ("windowsPerFloor", "26"), ("windowStyle", "wide"), ("roofVariant", "flat"),
                    ("balconyType", "none"), ("balconyOffset", "0"), ("wear", "0.3"), ("acUnits", ""), ("patchWindows", ""));
                Put(DistrictObjectTypes.Door, 640f, 0f, 675f, 0f, 6f, 4.5f, 0.3f, ("kind", "gate")); // ворота производства, не solid
                // Площадка погрузки: ровная земля высотой 0.06 м (раньше 0.9 м — перекрывала точку доставки).
                Put(DistrictObjectTypes.IndustryLoading, 640f, 0f, 662f, 0f, 70f, 0.06f, 8f);
                _points.Add(new DistrictPointInfo("ind-warehouse", "industry.warehouse", "ПРОИЗВОДСТВО/СКЛАД",
                    new Vec2(640f, 690f), RectC(640f, 690f, 80f, 30f),
                    new[] { new Vec3(640f, 0f, 674f) }, new Vec3(652f, 0f, 662f)));
                _deliveries.Add(new DeliveryPointInfo("drop-ind-loading-dock", "ind-warehouse", 1,
                    new Vec3(652f, 0f, 662f), "logistics"));
                Put(QuarterObjectTypes.DeliveryDropoff, 652f, 0f, 662f, 0f, 0.7f, 0.05f, 0.7f,
                    ("pointId", "drop-ind-loading-dock"), ("buildingId", "ind-warehouse"), ("entranceNumber", "1"),
                    ("interactionRadius", Num(DeliveryPointInfo.DefaultInteractionRadius)));
            }

            private void BuildForest()
            {
                // Лесопарк (юго-запад): газон, дорожка, лавочки, деревья по сетке.
                AddLawn(new Rect2(26f, 26f, 144f, 194f));
                int placed = 0;
                for (int x = 40; x < 140 && placed < 40; x += 12)
                {
                    for (int z = 40; z < 194 && placed < 40; z += 14)
                    {
                        placed++;
                        if ((x + z) % 3 == 0) continue;
                        AddTree(x + ((z * 7) % 5) - 2, z, 3.4f);
                    }
                }
                AddBench(60f, 120f, 0f);
                AddBench(100f, 60f, 0f);
                Put(QuarterObjectTypes.Footpath, 85f, 0f, 110f, 0f, 118f, 0.07f, 2f);
            }

            private void BuildSquare()
            {
                // Сквер в центре-западе: газон, деревья, лавочка.
                AddLawn(new Rect2(156f, 476f, 264f, 594f));
                AddTree(180f, 500f, 3f);
                AddTree(240f, 500f, 3f);
                AddTree(210f, 560f, 3f);
                AddBench(200f, 520f, 0f);
            }
        }
    }
}
