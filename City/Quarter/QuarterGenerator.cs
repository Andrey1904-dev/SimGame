// Life From Zero — City/Quarter module (ТЗ №023)
// Namespace: LifeFromZero.City.Quarter
//
// Процедурный генератор российского спального квартала 250×250 м.
//
// Планировка (детерминирована геометрией, seed влияет на наполнение):
//   * сетка улиц 3×3 (линии на 20%/50%/80% размера) делит квартал на 16 квартальных блоков;
//   * улицы: проезжая часть + тротуары с двух сторон + разметка + переходы на перекрёстках;
//   * дома ставятся по периметру блоков, входами во двор; между домами — разрывы;
//   * 2–3 блока получают полноценный двор: газон, детская площадка, лавочки,
//     деревья, парковка и дворовый проезд к ближайшей улице;
//   * фонари: уличные вдоль улиц, дворовые — по углам дворов.
//
// Гарантии по построению (проверяются тестами):
//   * дома не выходят за границу квартала и не заходят на проезжую часть/тротуары:
//     они размещаются только внутри блоков, отстоящих от коридора улицы;
//   * дома не пересекаются друг с другом (глобальная проверка AABB с запасом);
//   * дорожный граф связный (улицы пересекаются, проезды геометрически
//     примыкают к улицам — см. RoadGraph).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LifeFromZero.City.Quarter
{
    /// <summary>Точка входа генератора. Один экземпляр = одни параметры; Generate() можно вызывать повторно.</summary>
    public sealed class QuarterGenerator
    {
        private readonly QuarterGeneratorOptions _options;

        public QuarterGenerator(QuarterGeneratorOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
        }

        public QuarterPlan Generate()
        {
            var builder = new Builder(_options);
            return builder.Build();
        }

        /// <summary>Удобный статический вход: квартал по умолчанию с заданным seed.</summary>
        public static QuarterPlan Generate(int seed) =>
            new QuarterGenerator(new QuarterGeneratorOptions { Seed = seed }).Generate();

        private sealed class Builder
        {
            private readonly QuarterGeneratorOptions _o;
            private readonly SeededRandom _rng;
            private readonly List<PlacedObject> _objects = new List<PlacedObject>();
            private readonly List<RoadSegment> _roads = new List<RoadSegment>();
            private readonly List<RoadNode> _nodes = new List<RoadNode>();
            private readonly List<BuildingInfo> _buildings = new List<BuildingInfo>();
            private readonly List<CourtyardInfo> _courtyards = new List<CourtyardInfo>();
            private readonly List<Rect2> _solidFootprints = new List<Rect2>(); // дома/площадки: сюда не ставим
            private int _objectSeq;
            private int _roadSeq;
            private int _nodeSeq;
            private int _buildingSeq;
            private int _courtyardSeq;
            private PickupPointInfo _pickup;
            private Rect2? _pickupBlock; // блок с ПВЗ не становится двором, чтобы проезд/парковка не спорили за место
            private GroceryShopInfo _shop;
            private Rect2? _shopBlock;
            private PlayerHomeInfo _playerHome;
            private BasementInfo _basement;
            private readonly List<DeliveryPointInfo> _deliveryPoints = new List<DeliveryPointInfo>();
            private readonly List<RealEstateProperty> _properties = new List<RealEstateProperty>();
            private readonly Dictionary<int, int> _placedCount = new Dictionary<int, int>(); // этажность -> сколько домов стоит (квоты ТЗ №025)
            private readonly HashSet<Rect2> _specialBlocks = new HashSet<Rect2>(); // блоки ЖК/частных домов: обычной застройки там нет
            private readonly List<BuildingInfo> _eliteBuildings = new List<BuildingInfo>();
            private readonly List<BuildingInfo> _privateBuildings = new List<BuildingInfo>();
            private readonly Dictionary<int, HashSet<string>> _usedVariants = new Dictionary<int, HashSet<string>>();
            // Сигнатуры уже стоящих домов: соседние дома не должны совпадать комбинацией.
            private readonly List<(Vec2 Center, string VariantId, string Color, int BalconyOffset)> _signatures =
                new List<(Vec2, string, string, int)>();

            private float[] _lines;       // координаты улиц по каждой оси
            private float CorridorHalf => _o.RoadWidth / 2f + _o.SidewalkWidth;

            internal Builder(QuarterGeneratorOptions options)
            {
                _o = options;
                _rng = new SeededRandom(options.Seed);
            }

            internal QuarterPlan Build()
            {
                float s = _o.Size;
                _lines = new[] { s * 0.20f, s * 0.50f, s * 0.80f };

                BuildRoadNetwork();
                if (_o.IncludePickupPoint)
                    PlacePickupPoint();
                PlaceGroceryShop();
                if (_o.PlayerHome != null && _o.PlayerHome.Enabled)
                    PlacePlayerHome();
                if (_o.IncludeEliteAndPrivate)
                {
                    PlaceEliteComplexes();
                    PlacePrivateHouses();
                }
                BuildBlocksAndCourtyards();
                BuildBasement();
                BuildDeliveryPoints();
                BuildRealEstateRegistry();
                BuildStreetFurniture();

                var context = new QuarterPlacementContext(_rng, s, _objects, o => _objects.Add(o), IsFree);
                foreach (IQuarterObjectPlacer placer in _o.CustomPlacers)
                    placer.Place(context);

                return new QuarterPlan(_o.Seed, s, _objects, _roads, _nodes, _buildings, _courtyards,
                                       _pickup, _playerHome, _shop, _basement, _deliveryPoints, _properties);
            }

            // ---------- инфраструктура ----------

            private string NextObjectId() => "obj-" + (++_objectSeq).ToString("D4", CultureInfo.InvariantCulture);

            private PlacedObject AddObject(string typeId, Vec3 pos, float rotY, Vec3 size,
                                           Dictionary<string, string> parameters = null)
            {
                var obj = new PlacedObject(NextObjectId(), typeId, pos, rotY, size, parameters);
                _objects.Add(obj);
                return obj;
            }

            private bool IsFree(Rect2 rect, float margin)
            {
                Rect2 probe = rect.Expanded(margin);
                foreach (Rect2 solid in _solidFootprints)
                    if (probe.Overlaps(solid))
                        return false;
                return true;
            }

            private static Dictionary<string, string> Params(params (string Key, string Value)[] pairs)
            {
                var dict = new Dictionary<string, string>();
                foreach ((string key, string value) in pairs)
                    dict[key] = value;
                return dict;
            }

            private static string Num(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

            // ---------- дороги ----------

            private string AddNode(Vec2 pos, bool intersection)
            {
                string id = "node-" + (++_nodeSeq).ToString("D3", CultureInfo.InvariantCulture);
                _nodes.Add(new RoadNode(id, pos, intersection));
                return id;
            }

            private void AddRoad(string aId, string bId, Vec2 a, Vec2 b, float width, RoadKind kind, string typeId)
            {
                string id = "road-" + (++_roadSeq).ToString("D3", CultureInfo.InvariantCulture);
                _roads.Add(new RoadSegment(id, aId, bId, a, b, width, kind));
                bool vertical = Math.Abs(a.X - b.X) < 0.01f;
                float length = a.DistanceTo(b);
                AddObject(typeId,
                    new Vec3((a.X + b.X) / 2f, 0f, (a.Z + b.Z) / 2f),
                    vertical ? 90f : 0f,
                    new Vec3(length, 0.1f, width),
                    Params(("segmentId", id), ("kind", kind.ToString())));
            }

            private void BuildRoadNetwork()
            {
                float s = _o.Size;
                // Узлы: перекрёстки 3×3 + выходы улиц на границу квартала.
                var intersectionIds = new string[3, 3];
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        intersectionIds[i, j] = AddNode(new Vec2(_lines[i], _lines[j]), true);

                // Сегменты вдоль каждой улицы между соседними узлами (включая границу).
                for (int i = 0; i < 3; i++)
                {
                    // Вертикальная улица x = lines[i].
                    var vStops = new List<(Vec2 Pos, string Id)>
                    {
                        (new Vec2(_lines[i], 0f), AddNode(new Vec2(_lines[i], 0f), false)),
                        (new Vec2(_lines[i], _lines[0]), intersectionIds[i, 0]),
                        (new Vec2(_lines[i], _lines[1]), intersectionIds[i, 1]),
                        (new Vec2(_lines[i], _lines[2]), intersectionIds[i, 2]),
                        (new Vec2(_lines[i], s), AddNode(new Vec2(_lines[i], s), false)),
                    };
                    // Горизонтальная улица z = lines[i].
                    var hStops = new List<(Vec2 Pos, string Id)>
                    {
                        (new Vec2(0f, _lines[i]), AddNode(new Vec2(0f, _lines[i]), false)),
                        (new Vec2(_lines[0], _lines[i]), intersectionIds[0, i]),
                        (new Vec2(_lines[1], _lines[i]), intersectionIds[1, i]),
                        (new Vec2(_lines[2], _lines[i]), intersectionIds[2, i]),
                        (new Vec2(s, _lines[i]), AddNode(new Vec2(s, _lines[i]), false)),
                    };
                    for (int k = 0; k + 1 < vStops.Count; k++)
                    {
                        AddRoad(vStops[k].Id, vStops[k + 1].Id, vStops[k].Pos, vStops[k + 1].Pos,
                                _o.RoadWidth, RoadKind.Street, QuarterObjectTypes.Road);
                        AddRoad(hStops[k].Id, hStops[k + 1].Id, hStops[k].Pos, hStops[k + 1].Pos,
                                _o.RoadWidth, RoadKind.Street, QuarterObjectTypes.Road);
                    }
                }

                // Плиты перекрёстков, тротуары, разметка, переходы.
                foreach (RoadNode node in _nodes)
                {
                    if (!node.IsIntersection) continue;
                    AddObject(QuarterObjectTypes.Intersection,
                        new Vec3(node.Position.X, 0f, node.Position.Z), 0f,
                        new Vec3(_o.RoadWidth, 0.11f, _o.RoadWidth));
                    AddCrosswalks(node.Position);
                }
                BuildSidewalksAndMarkings();
            }

            private void AddCrosswalks(Vec2 c)
            {
                float offset = _o.RoadWidth / 2f + 2.2f;
                float stopOffset = offset + 2.4f;
                // Переход через вертикальную улицу (северный и южный подходы) и через горизонтальную.
                AddObject(QuarterObjectTypes.Crosswalk, new Vec3(c.X, 0f, c.Z + offset), 0f,
                    new Vec3(_o.RoadWidth, 0.12f, 3f), Params(("stripes", "5")));
                AddObject(QuarterObjectTypes.Crosswalk, new Vec3(c.X, 0f, c.Z - offset), 0f,
                    new Vec3(_o.RoadWidth, 0.12f, 3f), Params(("stripes", "5")));
                AddObject(QuarterObjectTypes.Crosswalk, new Vec3(c.X + offset, 0f, c.Z), 90f,
                    new Vec3(_o.RoadWidth, 0.12f, 3f), Params(("stripes", "5")));
                AddObject(QuarterObjectTypes.Crosswalk, new Vec3(c.X - offset, 0f, c.Z), 90f,
                    new Vec3(_o.RoadWidth, 0.12f, 3f), Params(("stripes", "5")));
                // Стоп-линии перед переходами (по половине ширины — правая сторона движения).
                AddObject(QuarterObjectTypes.StopLine, new Vec3(c.X + _o.RoadWidth / 4f, 0f, c.Z - stopOffset), 0f,
                    new Vec3(_o.RoadWidth / 2f, 0.12f, 0.4f));
                AddObject(QuarterObjectTypes.StopLine, new Vec3(c.X - _o.RoadWidth / 4f, 0f, c.Z + stopOffset), 0f,
                    new Vec3(_o.RoadWidth / 2f, 0.12f, 0.4f));
                AddObject(QuarterObjectTypes.StopLine, new Vec3(c.X - stopOffset, 0f, c.Z + _o.RoadWidth / 4f), 90f,
                    new Vec3(_o.RoadWidth / 2f, 0.12f, 0.4f));
                AddObject(QuarterObjectTypes.StopLine, new Vec3(c.X + stopOffset, 0f, c.Z - _o.RoadWidth / 4f), 90f,
                    new Vec3(_o.RoadWidth / 2f, 0.12f, 0.4f));
            }

            private void BuildSidewalksAndMarkings()
            {
                float s = _o.Size;
                foreach (float line in _lines)
                {
                    // Тротуары: две полосы вдоль всей улицы, по обе стороны проезжей части.
                    float off = _o.RoadWidth / 2f + _o.SidewalkWidth / 2f;
                    AddObject(QuarterObjectTypes.Sidewalk, new Vec3(line + off, 0f, s / 2f), 90f,
                        new Vec3(s, 0.08f, _o.SidewalkWidth));
                    AddObject(QuarterObjectTypes.Sidewalk, new Vec3(line - off, 0f, s / 2f), 90f,
                        new Vec3(s, 0.08f, _o.SidewalkWidth));
                    AddObject(QuarterObjectTypes.Sidewalk, new Vec3(s / 2f, 0f, line + off), 0f,
                        new Vec3(s, 0.08f, _o.SidewalkWidth));
                    AddObject(QuarterObjectTypes.Sidewalk, new Vec3(s / 2f, 0f, line - off), 0f,
                        new Vec3(s, 0.08f, _o.SidewalkWidth));

                    // Осевой пунктир: шаг 6 м, у перекрёстков разметку прерываем.
                    for (float t = 3f; t < s - 1f; t += 6f)
                    {
                        if (!NearAnyLine(t))
                            AddObject(QuarterObjectTypes.CenterLine, new Vec3(line, 0f, t), 90f,
                                new Vec3(3f, 0.12f, 0.15f));
                        if (!NearAnyLine(t))
                            AddObject(QuarterObjectTypes.CenterLine, new Vec3(t, 0f, line), 0f,
                                new Vec3(3f, 0.12f, 0.15f));
                    }
                }

                bool NearAnyLine(float t)
                {
                    foreach (float l in _lines)
                        if (Math.Abs(t - l) < CorridorHalf + 3f)
                            return true;
                    return false;
                }
            }

            /// <summary>
            /// Случайная сторона блока, выходящая на улицу (для ПВЗ и магазина).
            /// FacesSouth=true: фасад смотрит на южную улицу блока; false — на северную.
            /// Исключённые блоки (уже занятые ПВЗ/магазином) пропускаются, поэтому
            /// ПВЗ и магазин гарантированно оказываются в разных блоках.
            /// </summary>
            private (Rect2 Block, bool FacesSouth) PickFrontageBlock(params Rect2?[] excluded)
            {
                var candidates = new List<(Rect2 Block, bool FacesSouth)>();
                foreach (Rect2 block in BlockRects())
                {
                    bool skip = false;
                    foreach (Rect2? ex in excluded)
                        if (ex.HasValue && block.Equals(ex.Value))
                            skip = true;
                    if (skip) continue;
                    if (block.MinZ > 1f) candidates.Add((block, true));
                    if (block.MaxZ < _o.Size - 1f) candidates.Add((block, false));
                }
                return _rng.Pick(candidates);
            }

            // ---------- пункт выдачи заказов (ПВЗ) ----------

            /// <summary>
            /// Один ПВЗ на квартал: небольшое одноэтажное здание с плоской крышей
            /// фасадом к улице. Участок резервируется ДО застройки: след здания и
            /// площадка попадают в _solidFootprints, поэтому дома, деревья и
            /// площадки дворов туда не встанут, а дома дополнительно проверяются
            /// тестами против следа ПВЗ.
            /// Схема участка от улицы: тротуар → площадка (4 м) → здание (глубина 8 м).
            /// </summary>
            private void PlacePickupPoint()
            {
                const string pickupId = "pvz-01";
                const float buildingLength = 12f, buildingDepth = 8f, buildingHeight = 3.2f, plazaDepth = 4f;

                // Сторона блока, выходящая на улицу; фасад смотрит на улицу.
                (Rect2 blk, bool facesSouth) = PickFrontageBlock();
                _pickupBlock = blk;

                float cx = blk.Center.X + _rng.NextRange(-6f, 6f);
                float frontZ, centerZ;
                if (facesSouth)
                {
                    frontZ = blk.MinZ + plazaDepth;         // фасад — граница площадки и здания
                    centerZ = frontZ + buildingDepth / 2f;
                }
                else
                {
                    frontZ = blk.MaxZ - plazaDepth;
                    centerZ = frontZ - buildingDepth / 2f;
                }
                float outward = facesSouth ? -1f : 1f;      // направление от здания к улице

                var buildingRect = Rect2.FromCenterSize(new Vec2(cx, centerZ), buildingLength, buildingDepth);
                var plazaRect = Rect2.FromCenterSize(
                    new Vec2(cx, frontZ + outward * plazaDepth / 2f), buildingLength + 2f, plazaDepth);
                _solidFootprints.Add(buildingRect);
                _solidFootprints.Add(plazaRect);

                var entrance = new Vec3(cx, 0f, frontZ + outward * 0.3f);
                var interaction = new Vec3(cx, 0f, frontZ + outward * 1.6f);

                var buildingParams = Params(
                    ("buildingId", pickupId), ("floors", "1"), ("roof", "flat"),
                    ("signText", PickupPointInfo.SignText),
                    ("entrance", Num(entrance.X) + "," + Num(entrance.Z)),
                    ("interactionPoint", Num(interaction.X) + "," + Num(interaction.Z)));
                AddObject(QuarterObjectTypes.PickupBuilding, new Vec3(cx, 0f, centerZ), 0f,
                    new Vec3(buildingLength, buildingHeight, buildingDepth), buildingParams);
                // Плоская крыша — тонкая плита с небольшим свесом.
                AddObject(QuarterObjectTypes.PickupRoof, new Vec3(cx, buildingHeight, centerZ), 0f,
                    new Vec3(buildingLength + 0.6f, 0.25f, buildingDepth + 0.6f),
                    Params(("buildingId", pickupId)));
                // Дверь по центру фасада, окна-витрины по бокам, вывеска над дверью.
                // Поворот фасадных элементов — наружу, к улице (адаптер вешает текст вывески по локальному +Z).
                float facadeRot = facesSouth ? 180f : 0f;
                AddObject(QuarterObjectTypes.PickupDoor, new Vec3(cx, 0f, frontZ + outward * 0.08f), facadeRot,
                    new Vec3(1.2f, 2.2f, 0.15f), Params(("buildingId", pickupId)));
                foreach (float offsetX in new[] { -4.4f, -2.6f, 2.6f, 4.4f })
                {
                    // Y окна — 0.9 (низ окна): адаптер поставит центр на Y + Size.Y/2.
                    AddObject(QuarterObjectTypes.PickupWindow,
                        new Vec3(cx + offsetX, 0.9f, frontZ + outward * 0.06f), facadeRot,
                        new Vec3(1.6f, 1.2f, 0.1f), Params(("buildingId", pickupId)));
                }
                AddObject(QuarterObjectTypes.PickupSign, new Vec3(cx, 2.2f, frontZ + outward * 0.15f), facadeRot,
                    new Vec3(6.5f, 0.8f, 0.18f),
                    Params(("buildingId", pickupId), ("text", PickupPointInfo.SignText)));
                // Площадка перед входом — место, где стоит игрок.
                AddObject(QuarterObjectTypes.PickupPlaza, new Vec3(plazaRect.Center.X, 0f, plazaRect.Center.Z), 0f,
                    new Vec3(plazaRect.Width, 0.07f, plazaRect.Height),
                    Params(("buildingId", pickupId)));
                // Отдельная точка взаимодействия: пока только координаты и назначение,
                // выдача заказов сюда не зашита (будущий стык с Delivery снаружи модуля).
                AddObject(QuarterObjectTypes.PickupPoint, interaction, 0f,
                    new Vec3(0.8f, 0.05f, 0.8f),
                    Params(("buildingId", pickupId), ("purpose", "delivery.pickup"),
                           ("interactionRadius", "1.5")));

                _pickup = new PickupPointInfo(pickupId, new Vec2(cx, centerZ), 0f,
                    new Vec3(buildingLength, buildingHeight, buildingDepth),
                    entrance, interaction, plazaRect, buildingRect);
            }

            // ---------- продуктовый магазин ----------

            /// <summary>
            /// Один продуктовый магазин: одноэтажный, витрины, вывеска «ПРОДУКТЫ»,
            /// площадка у тротуара, точка shop.grocery. Блок — другой, чем у ПВЗ.
            /// Экономики/покупок нет — только здание и точка взаимодействия.
            /// </summary>
            private void PlaceGroceryShop()
            {
                const string shopId = "shop-01";
                const float length = 14f, depth = 9f, height = 3.4f, plazaDepth = 4f;

                (Rect2 blk, bool facesSouth) = PickFrontageBlock(_pickupBlock);
                _shopBlock = blk;

                float cx = blk.Center.X + _rng.NextRange(-5f, 5f);
                float frontZ, centerZ;
                if (facesSouth) { frontZ = blk.MinZ + plazaDepth; centerZ = frontZ + depth / 2f; }
                else { frontZ = blk.MaxZ - plazaDepth; centerZ = frontZ - depth / 2f; }
                float outward = facesSouth ? -1f : 1f;
                float facadeRot = facesSouth ? 180f : 0f;

                var buildingRect = Rect2.FromCenterSize(new Vec2(cx, centerZ), length, depth);
                var plazaRect = Rect2.FromCenterSize(new Vec2(cx, frontZ + outward * plazaDepth / 2f), length + 2f, plazaDepth);
                _solidFootprints.Add(buildingRect);
                _solidFootprints.Add(plazaRect);

                var entrance = new Vec3(cx, 0f, frontZ + outward * 0.3f);
                var interaction = new Vec3(cx, 0f, frontZ + outward * 1.6f);

                AddObject(QuarterObjectTypes.ShopBuilding, new Vec3(cx, 0f, centerZ), 0f,
                    new Vec3(length, height, depth),
                    Params(("buildingId", shopId), ("floors", "1"), ("roof", "flat"),
                           ("signText", GroceryShopInfo.SignText),
                           ("entrance", Num(entrance.X) + "," + Num(entrance.Z)),
                           ("interactionPoint", Num(interaction.X) + "," + Num(interaction.Z))));
                AddObject(QuarterObjectTypes.ShopRoof, new Vec3(cx, height, centerZ), 0f,
                    new Vec3(length + 0.6f, 0.25f, depth + 0.6f), Params(("buildingId", shopId)));
                AddObject(QuarterObjectTypes.ShopDoor, new Vec3(cx, 0f, frontZ + outward * 0.08f), facadeRot,
                    new Vec3(1.4f, 2.3f, 0.15f), Params(("buildingId", shopId)));
                foreach (float offsetX in new[] { -5.2f, -3.2f, 3.2f, 5.2f })
                {
                    AddObject(QuarterObjectTypes.ShopWindow,
                        new Vec3(cx + offsetX, 0.8f, frontZ + outward * 0.06f), facadeRot,
                        new Vec3(1.8f, 1.4f, 0.1f), Params(("buildingId", shopId)));
                }
                AddObject(QuarterObjectTypes.ShopSign, new Vec3(cx, 2.3f, frontZ + outward * 0.15f), facadeRot,
                    new Vec3(5.5f, 0.8f, 0.18f),
                    Params(("buildingId", shopId), ("text", GroceryShopInfo.SignText)));
                AddObject(QuarterObjectTypes.ShopPlaza, new Vec3(plazaRect.Center.X, 0f, plazaRect.Center.Z), 0f,
                    new Vec3(plazaRect.Width, 0.07f, plazaRect.Height), Params(("buildingId", shopId)));
                AddObject(QuarterObjectTypes.ShopPoint, interaction, 0f,
                    new Vec3(0.8f, 0.05f, 0.8f),
                    Params(("buildingId", shopId), ("purpose", "shop.grocery"),
                           ("interactionRadius", "1.5")));

                _shop = new GroceryShopInfo(shopId, new Vec2(cx, centerZ), new Vec3(length, height, depth),
                    entrance, interaction, plazaRect, buildingRect);
            }

            // ---------- дом игрока ----------

            /// <summary>
            /// Дом игрока (player.home, кв. №67 — только данные): пятиэтажка,
            /// место резервируется до обычной застройки, поэтому второй дом
            /// поверх не встанет (обычные дома проверяют _solidFootprints).
            /// Позицию/поворот можно задать извне через Options.PlayerHome;
            /// внешняя позиция валидируется: внутри квартала, не на дороге,
            /// не поверх ПВЗ/магазина — иначе генерация падает с понятной ошибкой.
            /// </summary>
            private void PlacePlayerHome()
            {
                BuildingArchetype arch = null;
                foreach (BuildingArchetype a in _o.Archetypes)
                    if (a.Floors == 5) { arch = a; break; }
                arch = arch ?? _o.Archetypes[0];

                float length = arch.LengthVariants[arch.LengthVariants.Count / 2];
                var size = new Vec3(length, arch.Floors * arch.FloorHeight, arch.Depth);
                Vec2 center;
                float rot;
                Vec2 facadeNormal; // куда смотрит фасад с подъездами (во двор)

                if (_o.PlayerHome.Position.HasValue)
                {
                    center = _o.PlayerHome.Position.Value;
                    rot = _o.PlayerHome.RotationY;
                    if (Math.Abs(rot) > 0.01f && Math.Abs(Math.Abs(rot) - 90f) > 0.01f)
                        throw new ArgumentException("PlayerHome.RotationY: допустимы только 0 или 90 градусов.");
                    bool alongX = Math.Abs(rot) < 0.01f;
                    facadeNormal = alongX ? new Vec2(0f, 1f) : new Vec2(1f, 0f);
                    var probe = new PlacedObject("probe", QuarterObjectTypes.Building,
                        new Vec3(center.X, 0f, center.Z), rot, size, null);
                    Rect2 fp = probe.Footprint;
                    if (fp.MinX < 0f || fp.MinZ < 0f || fp.MaxX > _o.Size || fp.MaxZ > _o.Size)
                        throw new ArgumentException("PlayerHome.Position: дом выходит за границу квартала.");
                    foreach (RoadSegment road in _roads)
                        if (road.Kind == RoadKind.Street && fp.Overlaps(road.Footprint.Expanded(CorridorHalf - road.Width / 2f + 0.1f)))
                            throw new ArgumentException("PlayerHome.Position: дом попадает на дорогу/тротуар.");
                    if (!IsFree(fp, 1f))
                        throw new ArgumentException("PlayerHome.Position: место занято (ПВЗ/магазин/другой объект).");
                }
                else
                {
                    // Автовыбор: южный край случайного внутреннего блока, фасад — во двор (на север).
                    var blocks = new List<Rect2>();
                    foreach (Rect2 b in BlockRects())
                        if (b.MinX > 1f && b.MinZ > 1f && b.MaxX < _o.Size - 1f && b.MaxZ < _o.Size - 1f &&
                            !(_pickupBlock.HasValue && b.Equals(_pickupBlock.Value)) &&
                            !(_shopBlock.HasValue && b.Equals(_shopBlock.Value)))
                            blocks.Add(b);
                    Rect2 blk = _rng.Pick(blocks);
                    center = new Vec2(blk.Center.X, blk.MinZ + 1.5f + arch.Depth / 2f);
                    rot = 0f;
                    facadeNormal = new Vec2(0f, 1f);
                }

                var footprintProbe = new PlacedObject("probe", QuarterObjectTypes.Building,
                    new Vec3(center.X, 0f, center.Z), rot, size, null);
                Rect2 footprint = footprintProbe.Footprint;
                _solidFootprints.Add(footprint);
                _placedCount[arch.Floors] = _placedCount.TryGetValue(arch.Floors, out int pc) ? pc + 1 : 1;
                string buildingId = "bld-" + (++_buildingSeq).ToString("D3", CultureInfo.InvariantCulture);

                BuildingVariant variant = PickVariant(arch.Floors, center,
                    out string color, out int playerBalconyOffset, out float playerWear);
                string doorColor = _rng.Pick(DoorColors);

                int entranceCount = Math.Max(1, (int)Math.Round(length / arch.EntranceSpacing));
                var entrances = new List<Vec2>();
                float halfDepth = arch.Depth / 2f + 0.4f;
                for (int e = 0; e < entranceCount; e++)
                {
                    float along = length * (e + 1) / (entranceCount + 1) - length / 2f;
                    Vec2 alongVec = Math.Abs(rot) < 0.01f ? new Vec2(along, 0f) : new Vec2(0f, along);
                    Vec2 p = center + alongVec + facadeNormal * halfDepth;
                    entrances.Add(p);
                    AddObject(QuarterObjectTypes.Entrance, new Vec3(p.X, 0f, p.Z), rot,
                        new Vec3(3f, 3f, 1f),
                        Params(("buildingId", buildingId),
                               ("entranceNumber", (e + 1).ToString(CultureInfo.InvariantCulture)),
                               ("normalX", Num(facadeNormal.X)), ("normalZ", Num(facadeNormal.Z)),
                               ("entranceStyle", variant.EntranceStyle), ("doorColor", doorColor)));
                }

                _buildings.Add(new BuildingInfo(buildingId, arch.Id, center, rot, size, arch.Floors,
                    entrances, variant.FacadeStyle, variant.RoofType, color, isPlayerHome: true,
                    variant.Id, variant.WindowStyle, variant.BalconyType, variant.RoofVariant,
                    playerWear, playerBalconyOffset));
                var playerParams = Params(
                    ("buildingId", buildingId), ("archetype", arch.Id),
                    ("floors", arch.Floors.ToString(CultureInfo.InvariantCulture)),
                    ("entrances", entranceCount.ToString(CultureInfo.InvariantCulture)),
                    ("style", variant.FacadeStyle), ("roof", variant.RoofType),
                    ("facadeColor", color),
                    ("variant", variant.Id), ("roofVariant", variant.RoofVariant),
                    ("windowStyle", variant.WindowStyle), ("balconyType", variant.BalconyType),
                    ("balconyOffset", playerBalconyOffset.ToString(CultureInfo.InvariantCulture)),
                    ("wear", playerWear.ToString("0.##", CultureInfo.InvariantCulture)),
                    ("entranceStyle", variant.EntranceStyle), ("doorColor", doorColor),
                    ("specialId", PlayerHomeInfo.SpecialId),
                    ("apartment", PlayerHomeInfo.ApartmentNumber.ToString(CultureInfo.InvariantCulture)));
                foreach (KeyValuePair<string, string> kv in FacadeDetailParams(length, arch.Floors))
                    playerParams[kv.Key] = kv.Value;
                AddObject(QuarterObjectTypes.Building, new Vec3(center.X, 0f, center.Z), rot, size,
                    playerParams);

                Vec2 doorGround = entrances[0];
                var door = new Vec3(doorGround.X, 0f, doorGround.Z);
                var exit = new Vec3(doorGround.X + facadeNormal.X * 2f, 0f, doorGround.Z + facadeNormal.Z * 2f);
                AddObject(QuarterObjectTypes.PlayerHomeDoor, door, rot, new Vec3(1.2f, 2.2f, 0.2f),
                    Params(("buildingId", buildingId), ("specialId", PlayerHomeInfo.SpecialId)));
                AddObject(QuarterObjectTypes.PlayerHomeExit, exit, rot, new Vec3(0.8f, 0.05f, 0.8f),
                    Params(("buildingId", buildingId), ("specialId", PlayerHomeInfo.SpecialId),
                           ("purpose", "player.home.exit")));
                AddObject(QuarterObjectTypes.PlayerHomeMarker, new Vec3(center.X, 0f, center.Z), rot,
                    new Vec3(1f, 0.05f, 1f),
                    Params(("buildingId", buildingId), ("specialId", PlayerHomeInfo.SpecialId),
                           ("apartment", PlayerHomeInfo.ApartmentNumber.ToString(CultureInfo.InvariantCulture))));

                _playerHome = new PlayerHomeInfo(buildingId, center, rot, door, exit);
            }

            private static readonly string[] DoorColors = { "#5A3A22", "#2F4F6F", "#4A4A4A", "#6B2F2F", "#33523B" };

            /// <summary>
            /// Выбор архитектурного варианта дома с защитой от повторов:
            /// 1) вариант не должен совпадать с вариантом дома в радиусе 60 м;
            /// 2) пока не использованы все варианты этажности — берём неиспользованные;
            /// 3) если вариант всё же совпал с соседом, комбинацию разводим цветом
            ///    фасада и сдвигом рисунка балконов (BalconyOffset).
            /// </summary>
            private BuildingVariant PickVariant(int floors, Vec2 center,
                                                out string facadeColor, out int balconyOffset, out float wear)
            {
                IReadOnlyList<BuildingVariant> variants = BuildingVariantCatalog.ForFloors(floors);
                var neighborVariants = new HashSet<string>();
                foreach ((Vec2 c, string v, string col, int off) in _signatures)
                    if (c.DistanceTo(center) < 60f)
                        neighborVariants.Add(v);

                var candidates = new List<BuildingVariant>();
                foreach (BuildingVariant v in variants)
                    if (!neighborVariants.Contains(v.Id))
                        candidates.Add(v);
                if (candidates.Count == 0)
                    candidates.AddRange(variants);

                if (!_usedVariants.TryGetValue(floors, out HashSet<string> used))
                {
                    used = new HashSet<string>();
                    _usedVariants[floors] = used;
                }
                var unused = new List<BuildingVariant>();
                foreach (BuildingVariant v in candidates)
                    if (!used.Contains(v.Id))
                        unused.Add(v);
                BuildingVariant variant = _rng.Pick(unused.Count > 0 ? unused : candidates);
                used.Add(variant.Id);

                // Цвет: не повторяем пару (вариант, цвет) у соседей, если палитра позволяет.
                var neighborPairs = new HashSet<string>();
                foreach ((Vec2 c, string v, string col, int off) in _signatures)
                    if (c.DistanceTo(center) < 60f && v == variant.Id)
                        neighborPairs.Add(col);
                var colors = new List<string>();
                foreach (string col in variant.Palette)
                    if (!neighborPairs.Contains(col))
                        colors.Add(col);
                facadeColor = _rng.Pick(colors.Count > 0 ? colors : variant.Palette);

                balconyOffset = _rng.NextInt(0, 3);
                foreach ((Vec2 c, string v, string col, int off) in _signatures)
                    if (c.DistanceTo(center) < 60f && v == variant.Id && col == facadeColor && off == balconyOffset)
                        balconyOffset = (balconyOffset + 1) % 3;

                wear = variant.Wear + _rng.NextRange(-0.08f, 0.12f);
                if (wear < 0f) wear = 0f;
                if (wear > 1f) wear = 1f;

                _signatures.Add((center, variant.Id, facadeColor, balconyOffset));
                return variant;
            }

            /// <summary>
            /// Фасадные мелочи здания в параметрах объекта: кондиционеры и «заплатки»
            /// ремонта как явные позиции (этаж:окно:сторона), водосточные трубы —
            /// смещения от центра вдоль корпуса. Адаптер строит по ним геометрию.
            /// </summary>
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
                    { "windowsPerFloor", perFloor.ToString(CultureInfo.InvariantCulture) },
                    { "acUnits", string.Join(";", ac) },
                    { "patchWindows", string.Join(";", patches) },
                    { "drainpipes", Num(-length / 2f + 1.2f) + ";" + Num(length / 2f - 1.2f) },
                };
            }

            /// <summary>
            /// Выбор архетипа дома: сначала гарантируем присутствие каждой этажности
            /// (3 и 10 этажей из списка), дальше — взвешенно, пятиэтажки преобладают.
            /// </summary>
            private BuildingArchetype PickArchetype()
            {
                // Квоты ТЗ №025: тип, чья квота исчерпана, больше не выбирается;
                // все квоты исчерпаны — null (сторона/квартал достроены).
                bool QuotaLeft(BuildingArchetype a) =>
                    _o.ResidentialQuotas == null ||
                    !_o.ResidentialQuotas.TryGetValue(a.Floors, out int quota) ||
                    !_placedCount.TryGetValue(a.Floors, out int placed) || placed < quota;

                // Сначала гарантируем присутствие каждого типа из квот.
                foreach (int need in new[] { 3, 9, 10, 5 })
                {
                    if (_placedCount.ContainsKey(need)) continue;
                    foreach (BuildingArchetype a in _o.Archetypes)
                        if (a.Floors == need && QuotaLeft(a)) return a;
                }
                int Weight(BuildingArchetype a) => a.Floors == 5 ? 55 : a.Floors == 3 ? 15 : 15;
                int totalWeight = 0;
                foreach (BuildingArchetype a in _o.Archetypes)
                    if (QuotaLeft(a)) totalWeight += Weight(a);
                if (totalWeight == 0) return null;
                int roll = _rng.NextInt(0, totalWeight);
                foreach (BuildingArchetype a in _o.Archetypes)
                {
                    if (!QuotaLeft(a)) continue;
                    roll -= Weight(a);
                    if (roll < 0) return a;
                }
                return null;
            }

            // ---------- элитные ЖК и частные дома (ТЗ №025) ----------

            /// <summary>
            /// Отдельный блок под ЖК/частный дом: у границы квартала (сохраняем
            /// внутренние блоки дворам), не ПВЗ/магазин/дом игрока, с улицей
            /// с южной или северной стороны. Блок помечается специальным:
            /// обычной застройки в нём не будет.
            /// </summary>
            private Rect2 PickSpecialBlock()
            {
                var candidates = new List<Rect2>();
                foreach (Rect2 block in BlockRects())
                {
                    bool outer = block.MinX < 1f || block.MinZ < 1f ||
                                 block.MaxX > _o.Size - 1f || block.MaxZ > _o.Size - 1f;
                    if (!outer) continue;
                    if (_specialBlocks.Contains(block)) continue;
                    if (_pickupBlock.HasValue && block.Equals(_pickupBlock.Value)) continue;
                    if (_shopBlock.HasValue && block.Equals(_shopBlock.Value)) continue;
                    if (_playerHome != null && block.Contains(_playerHome.Center)) continue;
                    if (!(block.MinZ > 1f || block.MaxZ < _o.Size - 1f)) continue; // нужна улица рядом
                    candidates.Add(block);
                }
                Rect2 picked = _rng.Pick(candidates);
                _specialBlocks.Add(picked);
                return picked;
            }

            /// <summary>Общий путь особых зданий: след, вариант, вход(ы), объект здания с параметрами.</summary>
            private BuildingInfo AddSpecialBuilding(BuildingArchetype arch, Vec2 center, float rot,
                                                    int entranceCount, Vec2 facadeNormal, string specialKind)
            {
                var size = new Vec3(arch.LengthVariants[0], arch.Height, arch.Depth);
                var probe = new PlacedObject("probe", QuarterObjectTypes.Building,
                    new Vec3(center.X, 0f, center.Z), rot, size, null);
                _solidFootprints.Add(probe.Footprint);
                string buildingId = "bld-" + (++_buildingSeq).ToString("D3", CultureInfo.InvariantCulture);

                BuildingVariant variant = PickVariant(arch.Floors, center,
                    out string facadeColor, out int balconyOffset, out float wearLevel);
                string doorColor = _rng.Pick(DoorColors);

                var entrances = new List<Vec2>();
                Vec2 alongVec = Math.Abs(facadeNormal.Z) > 0.5f ? new Vec2(1f, 0f) : new Vec2(0f, 1f);
                for (int e = 0; e < entranceCount; e++)
                {
                    float along = entranceCount == 1 ? 0f :
                        (e - (entranceCount - 1) / 2f) * arch.EntranceSpacing;
                    Vec2 p = center + alongVec * along + facadeNormal * (arch.Depth / 2f + 0.4f);
                    entrances.Add(p);
                    AddObject(QuarterObjectTypes.Entrance, new Vec3(p.X, 0f, p.Z), rot,
                        new Vec3(3f, 3f, 1f),
                        Params(("buildingId", buildingId),
                               ("entranceNumber", (e + 1).ToString(CultureInfo.InvariantCulture)),
                               ("normalX", Num(facadeNormal.X)), ("normalZ", Num(facadeNormal.Z)),
                               ("entranceStyle", variant.EntranceStyle), ("doorColor", doorColor)));
                }

                var info = new BuildingInfo(buildingId, arch.Id, center, rot, size, arch.Floors,
                    entrances, variant.FacadeStyle, variant.RoofType, facadeColor, isPlayerHome: false,
                    variant.Id, variant.WindowStyle, variant.BalconyType, variant.RoofVariant,
                    wearLevel, balconyOffset);
                _buildings.Add(info);

                var parameters = Params(
                    ("buildingId", buildingId), ("archetype", arch.Id),
                    ("floors", arch.Floors.ToString(CultureInfo.InvariantCulture)),
                    ("entrances", entranceCount.ToString(CultureInfo.InvariantCulture)),
                    ("style", variant.FacadeStyle), ("roof", variant.RoofType),
                    ("facadeColor", facadeColor),
                    ("variant", variant.Id), ("roofVariant", variant.RoofVariant),
                    ("windowStyle", variant.WindowStyle), ("balconyType", variant.BalconyType),
                    ("balconyOffset", balconyOffset.ToString(CultureInfo.InvariantCulture)),
                    ("wear", wearLevel.ToString("0.##", CultureInfo.InvariantCulture)),
                    ("entranceStyle", variant.EntranceStyle), ("doorColor", doorColor),
                    ("specialKind", specialKind));
                foreach (KeyValuePair<string, string> kv in FacadeDetailParams(size.X, arch.Floors))
                    parameters[kv.Key] = kv.Value;
                AddObject(QuarterObjectTypes.Building, new Vec3(center.X, 0f, center.Z), rot, size, parameters);
                return info;
            }

            /// <summary>
            /// Два элитных ЖК на отдельных блоках: стеклянная высотка (32 эт.) и
            /// премиальный комплекс с лоджиями (34 эт.). У каждого своя территория:
            /// газон, парковка, декоративные фонари, лобби; у второго — зона отдыха.
            /// Интерьеров нет; квартира игрока в будущем привяжется записью реестра.
            /// </summary>
            private void PlaceEliteComplexes()
            {
                var archs = new[] { BuildingArchetype.EliteGlassTower(), BuildingArchetype.ElitePremiumTower() };
                for (int k = 0; k < archs.Length; k++)
                {
                    BuildingArchetype arch = archs[k];
                    Rect2 block = PickSpecialBlock();
                    Vec2 center = block.Center;
                    bool streetSouth = block.MinZ > 1f;
                    Vec2 normal = streetSouth ? new Vec2(0f, -1f) : new Vec2(0f, 1f);
                    BuildingInfo info = AddSpecialBuilding(arch, center, 0f,
                        entranceCount: k == 0 ? 1 : 2, normal,
                        k == 0 ? "elite.glass" : "elite.premium");
                    _eliteBuildings.Add(info);
                    string bid = info.Id;

                    // Территория комплекса.
                    Rect2 territory = block.Shrunk(3f);
                    AddObject(QuarterObjectTypes.Lawn, new Vec3(territory.Center.X, 0f, territory.Center.Z), 0f,
                        new Vec3(territory.Width, 0.05f, territory.Height), Params(("buildingId", bid)));
                    // Лобби и навес входной группы.
                    Vec2 lobbyCenter = center + normal * (arch.Depth / 2f + 1.7f);
                    AddObject(QuarterObjectTypes.EliteLobby, new Vec3(lobbyCenter.X, 0f, lobbyCenter.Z), 0f,
                        new Vec3(10f, 4.2f, 3.4f), Params(("buildingId", bid)));
                    AddObject(QuarterObjectTypes.EliteCanopy, new Vec3(lobbyCenter.X, 4.2f, lobbyCenter.Z), 0f,
                        new Vec3(12f, 0.25f, 4.6f), Params(("buildingId", bid)));
                    // Парковка: первый кандидат, целиком помещающийся в блок и не
                    // пересекающийся с башней и лобби. Сбоку от длинной башни в
                    // угловом блоке места может не быть — тогда парковка уходит
                    // на торцевую сторону (раньше в таких случаях машино-места
                    // уезжали за границу квартала, аудит ТЗ №026, seed 3/10/777).
                    float halfLen = arch.LengthVariants[0] / 2f;
                    var lobbyRect = Rect2.FromCenterSize(lobbyCenter, 12f, 4.6f);
                    var parkingCandidates = new[]
                    {
                        center + new Vec2(halfLen + 7f, 0f),
                        center - new Vec2(halfLen + 7f, 0f),
                        center + new Vec2(-halfLen + 5.5f, 0f) + normal * (arch.Depth / 2f + 6f),
                        center + new Vec2(halfLen - 5.5f, 0f) + normal * (arch.Depth / 2f + 6f),
                        center + new Vec2(-halfLen + 5.5f, 0f) - normal * (arch.Depth / 2f + 6f),
                        center + new Vec2(halfLen - 5.5f, 0f) - normal * (arch.Depth / 2f + 6f),
                    };
                    Vec2 parkingCenter = parkingCandidates[0];
                    bool parkingPlaced = false;
                    foreach (Vec2 candidate in parkingCandidates)
                    {
                        var lotRect = Rect2.FromCenterSize(candidate, 11f, 9f);
                        if (lotRect.MinX < block.MinX + 1f || lotRect.MaxX > block.MaxX - 1f ||
                            lotRect.MinZ < block.MinZ + 1f || lotRect.MaxZ > block.MaxZ - 1f) continue;
                        if (lotRect.Overlaps(info.Footprint.Expanded(1f)) || lotRect.Overlaps(lobbyRect)) continue;
                        parkingCenter = candidate;
                        parkingPlaced = true;
                        break;
                    }
                    if (parkingPlaced)
                    {
                        AddObject(QuarterObjectTypes.ParkingLot, new Vec3(parkingCenter.X, 0f, parkingCenter.Z), 0f,
                            new Vec3(11f, 0.09f, 9f), Params(("buildingId", bid)));
                        for (int sIdx = 0; sIdx < 4; sIdx++)
                        {
                            AddObject(QuarterObjectTypes.ParkingStall,
                                new Vec3(parkingCenter.X - 3.75f + sIdx * 2.5f, 0f, parkingCenter.Z), 0f,
                                new Vec3(2.5f, 0.12f, 5f), Params(("buildingId", bid)));
                        }
                    }
                    // Декоративное освещение территории.
                    foreach (Vec2 corner in new[]
                             {
                                 new Vec2(territory.MinX + 2f, territory.MinZ + 2f), new Vec2(territory.MaxX - 2f, territory.MinZ + 2f),
                                 new Vec2(territory.MinX + 2f, territory.MaxZ - 2f), new Vec2(territory.MaxX - 2f, territory.MaxZ - 2f),
                             })
                    {
                        AddObject(QuarterObjectTypes.YardLamp, new Vec3(corner.X, 0f, corner.Z), 0f,
                            new Vec3(0.25f, 5f, 0.25f), Params(("buildingId", bid), ("decorative", "true")));
                    }
                    // Озеленение; у второго ЖК — ещё и зона отдыха с лавочками и дорожкой.
                    foreach (Vec2 tp in new[]
                             {
                                 center + new Vec2(-arch.LengthVariants[0] / 2f - 3f, -arch.Depth / 2f + 2f),
                                 center + new Vec2(-arch.LengthVariants[0] / 2f - 3f, arch.Depth / 2f - 2f),
                             })
                    {
                        if (tp.X > block.MinX + 1f && tp.X < block.MaxX - 1f && tp.Z > block.MinZ + 1f && tp.Z < block.MaxZ - 1f)
                            AddTree(tp, null);
                    }
                    if (k == 1)
                    {
                        Vec2 restCenter = center - normal * (arch.Depth / 2f + 4f);
                        // Если парковка заняла заднюю сторону, зону отдыха сдвигаем
                        // вдоль фасада, чтобы лавочки не стояли на машино-местах.
                        if (parkingPlaced && (parkingCenter.Z - center.Z) * normal.Z < -1f)
                            restCenter = new Vec2(center.X + (parkingCenter.X < center.X ? 9f : -9f), restCenter.Z);
                        AddObject(QuarterObjectTypes.Footpath, new Vec3(restCenter.X, 0f, restCenter.Z), 0f,
                            new Vec3(14f, 0.06f, 1.8f), Params(("buildingId", bid)));
                        for (int bn = 0; bn < 3; bn++)
                        {
                            AddObject(QuarterObjectTypes.Bench,
                                new Vec3(restCenter.X - 5f + bn * 5f, 0f, restCenter.Z + 1.8f), 0f,
                                new Vec3(1.8f, 0.6f, 0.6f),
                                Params(("buildingId", bid), ("benchStyle", "new")));
                        }
                    }
                }
            }

            /// <summary>
            /// Два частных дома на отдельных участках у улицы: современный коттедж
            /// с гаражом и классический дом с садом. Участок огорожен, ворота и
            /// дорожка выходят к улице; след участка и дома — в solid-списке,
            /// а сам блок помечен специальным (обычной застройки там нет).
            /// </summary>
            private void PlacePrivateHouses()
            {
                var archs = new[] { BuildingArchetype.PrivateModernHouse(), BuildingArchetype.PrivateClassicHouse() };
                for (int k = 0; k < archs.Length; k++)
                {
                    BuildingArchetype arch = archs[k];
                    Rect2 block = PickSpecialBlock();
                    bool streetSouth = block.MinZ > 1f;
                    Vec2 normal = streetSouth ? new Vec2(0f, -1f) : new Vec2(0f, 1f); // к улице
                    var plot = Rect2.FromCenterSize(block.Center, 26f, 22f);
                    Vec2 houseCenter = block.Center - normal * 3f;
                    BuildingInfo info = AddSpecialBuilding(arch, houseCenter, 0f, 1, normal,
                        k == 0 ? "private.modern" : "private.classic");
                    _privateBuildings.Add(info);
                    string bid = info.Id;
                    string fenceStyle = k == 0 ? "metal" : "wood";

                    AddObject(QuarterObjectTypes.Lawn, new Vec3(plot.Center.X, 0f, plot.Center.Z), 0f,
                        new Vec3(plot.Width, 0.05f, plot.Height), Params(("buildingId", bid)));
                    // Забор по периметру; на уличной стороне — две секции и разрыв 3 м под ворота.
                    float gateZ = streetSouth ? plot.MinZ : plot.MaxZ;
                    float otherZ = streetSouth ? plot.MaxZ : plot.MinZ;
                    AddFenceRun(new Vec2(plot.MinX, otherZ), new Vec2(plot.MaxX, otherZ), bid, fenceStyle, block);
                    AddFenceRun(new Vec2(plot.MinX, plot.MinZ), new Vec2(plot.MinX, plot.MaxZ), bid, fenceStyle, block);
                    AddFenceRun(new Vec2(plot.MaxX, plot.MinZ), new Vec2(plot.MaxX, plot.MaxZ), bid, fenceStyle, block);
                    AddFenceRun(new Vec2(plot.MinX, gateZ), new Vec2(plot.Center.X - 1.5f, gateZ), bid, fenceStyle, block);
                    AddFenceRun(new Vec2(plot.Center.X + 1.5f, gateZ), new Vec2(plot.MaxX, gateZ), bid, fenceStyle, block);
                    Vec2 gateCenter = new Vec2(plot.Center.X, gateZ);
                    AddObject(QuarterObjectTypes.PrivateGate, new Vec3(gateCenter.X, 0f, gateCenter.Z), 0f,
                        new Vec3(3f, 1.1f, 0.12f),
                        Params(("buildingId", bid), ("fenceStyle", fenceStyle)));
                    // Дорожка от ворот к двери дома.
                    Vec2 doorGround = info.Entrances[0];
                    Vec2 pathCenter = (gateCenter + doorGround) * 0.5f;
                    bool pathVertical = Math.Abs(gateCenter.Z - doorGround.Z) > Math.Abs(gateCenter.X - doorGround.X);
                    float pathLength = gateCenter.DistanceTo(doorGround);
                    AddObject(QuarterObjectTypes.PrivatePath, new Vec3(pathCenter.X, 0f, pathCenter.Z),
                        pathVertical ? 90f : 0f, new Vec3(pathLength, 0.06f, 1.6f),
                        Params(("buildingId", bid)));

                    if (k == 0)
                    {
                        // Гараж современного коттеджа + подъезд к нему от ворот.
                        Vec2 garageCenter = houseCenter + new Vec2(arch.LengthVariants[0] / 2f + 3.6f, 0f);
                        _solidFootprints.Add(Rect2.FromCenterSize(garageCenter, 6.5f, 5.5f));
                        AddObject(QuarterObjectTypes.PrivateGarage, new Vec3(garageCenter.X, 0f, garageCenter.Z), 0f,
                            new Vec3(6.5f, 2.7f, 5.5f), Params(("buildingId", bid)));
                        Vec2 driveCenter = (gateCenter + garageCenter) * 0.5f;
                        AddObject(QuarterObjectTypes.PrivatePath, new Vec3(driveCenter.X, 0f, driveCenter.Z),
                            pathVertical ? 90f : 0f,
                            new Vec3(gateCenter.DistanceTo(garageCenter), 0.06f, 3f),
                            Params(("buildingId", bid), ("driveway", "true")));
                    }
                    else
                    {
                        // Сад классического дома: плодовые деревья и кусты за домом.
                        Vec2 gardenCenter = houseCenter - normal * (arch.Depth / 2f + 4f);
                        AddObject(QuarterObjectTypes.PrivateGarden, new Vec3(gardenCenter.X, 0f, gardenCenter.Z), 0f,
                            new Vec3(14f, 0.06f, 7f), Params(("buildingId", bid)));
                        for (int tr = 0; tr < 3; tr++)
                        {
                            var tp = gardenCenter + new Vec2(-5f + tr * 5f, _rng.NextRange(-1.5f, 1.5f));
                            AddTree(tp, null);
                            AddObject(QuarterObjectTypes.Bush,
                                new Vec3(tp.X + 1.4f, 0f, tp.Z + 1f), 0f,
                                new Vec3(1.1f, 0.9f, 1.1f), Params(("buildingId", bid)));
                        }
                    }
                }
            }

            /// <summary>Одна прямая секция забора участка между двумя точками (осевая).</summary>
            private void AddFenceRun(Vec2 a, Vec2 b, string buildingId, string fenceStyle, Rect2 block)
            {
                Vec2 mid = (a + b) * 0.5f;
                bool vertical = Math.Abs(a.X - b.X) < 0.01f;
                float length = a.DistanceTo(b);
                // Уличную сторону не перекрываем сплошняком в месте ворот:
                // секции по краям участка, центр оставит объект ворот.
                AddObject(QuarterObjectTypes.PrivateFence, new Vec3(mid.X, 0f, mid.Z), vertical ? 90f : 0f,
                    new Vec3(length, 0.9f, 0.08f),
                    Params(("buildingId", buildingId), ("fenceStyle", fenceStyle)));
            }

            // ---------- блоки, дома, дворы ----------

            private List<Rect2> BlockRects()
            {
                var edges = new List<float> { 0f };
                foreach (float l in _lines) { edges.Add(l - CorridorHalf); edges.Add(l + CorridorHalf); }
                edges.Add(_o.Size);
                var blocks = new List<Rect2>();
                for (int i = 0; i + 1 < edges.Count; i += 2)
                    for (int j = 0; j + 1 < edges.Count; j += 2)
                        blocks.Add(new Rect2(edges[i], edges[j], edges[i + 1], edges[j + 1]));
                return blocks;
            }

            private void BuildBlocksAndCourtyards()
            {
                List<Rect2> blocks = BlockRects();
                // Внутренние блоки (не у границы квартала) — кандидаты во дворы.
                var candidates = new List<Rect2>();
                foreach (Rect2 b in blocks)
                    if (b.MinX > 1f && b.MinZ > 1f && b.MaxX < _o.Size - 1f && b.MaxZ < _o.Size - 1f &&
                        !(_pickupBlock.HasValue && b.Equals(_pickupBlock.Value)) &&
                        !(_shopBlock.HasValue && b.Equals(_shopBlock.Value)) &&
                        !(_playerHome != null && b.Contains(_playerHome.Center)))
                        candidates.Add(b);
                // Детерминированно перемешиваем кандидатов seed-ом и берём нужное число дворов.
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = _rng.NextInt(0, i + 1);
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }
                int yardCount = Math.Min(_o.CourtyardCount, candidates.Count);
                var yards = new HashSet<Rect2>();
                for (int i = 0; i < yardCount; i++)
                    yards.Add(candidates[i]);

                foreach (Rect2 block in blocks)
                {
                    if (_specialBlocks.Contains(block)) continue; // ЖК/частные дома уже построены
                    BuildBlock(block, yards.Contains(block));
                }
            }

            private void BuildBlock(Rect2 block, bool isCourtyard)
            {
                Rect2 inner = block.Shrunk(1.5f);
                float driveX = inner.Center.X;
                Rect2? reservedDrive = null;
                if (isCourtyard)
                {
                    // Проезд идёт от южной улицы блока к парковке: полосу по южному ряду домов резервируем.
                    reservedDrive = new Rect2(driveX - _o.DrivewayWidth / 2f - 1.5f, block.MinZ,
                                              driveX + _o.DrivewayWidth / 2f + 1.5f, inner.MinZ + 16f);
                }

                PlaceEdgeBuildings(inner, Edge.South, reservedDrive);
                PlaceEdgeBuildings(inner, Edge.North, null);
                PlaceEdgeBuildings(inner, Edge.West, null);
                PlaceEdgeBuildings(inner, Edge.East, null);

                if (isCourtyard)
                    BuildCourtyard(block, inner, driveX);
                else
                    PlantBlockGreenery(inner);
            }

            private enum Edge { South, North, West, East }

            private void PlaceEdgeBuildings(Rect2 inner, Edge edge, Rect2? reserved)
            {
                // Дома вдоль одной стороны блока; фасад с подъездами смотрит внутрь блока (во двор).
                float cursor = (edge == Edge.South || edge == Edge.North) ? inner.MinX : inner.MinZ;
                float limit = (edge == Edge.South || edge == Edge.North) ? inner.MaxX : inner.MaxZ;
                // Для вертикальных сторон не заходим на углы, занятые горизонтальными домами.
                if (edge == Edge.West || edge == Edge.East) { cursor += 15f; limit -= 15f; }

                while (true)
                {
                    // Подбираем архетип, который влезает в остаток стороны: если у
                    // выбранного длины не помещаются, пробуем другие архетипы,
                    // а не заканчиваем сторону (иначе длинные пятиэтажки проигрывают).
                    BuildingArchetype arch = null;
                    float length = -1f;
                    for (int attempt = 0; attempt < _o.Archetypes.Count && length < 0f; attempt++)
                    {
                        BuildingArchetype candidate = PickArchetype();
                        if (candidate == null) break; // квоты исчерпаны
                        var variants = new List<float>(candidate.LengthVariants);
                        while (variants.Count > 0 && length < 0f)
                        {
                            int idx = _rng.NextInt(0, variants.Count);
                            if (cursor + variants[idx] <= limit) { length = variants[idx]; arch = candidate; }
                            else variants.RemoveAt(idx);
                        }
                    }
                    if (length < 0f) break;
                    float gap = _rng.NextRange(5f, 8f);
                    float start = cursor;
                    cursor += length + gap;
                    if (_rng.NextChance(0.08f)) continue; // случайный разрыв застройки

                    float depth = arch.Depth;
                    Vec2 center;
                    float rot;
                    switch (edge)
                    {
                        case Edge.South: center = new Vec2(start + length / 2f, inner.MinZ + depth / 2f); rot = 0f; break;
                        case Edge.North: center = new Vec2(start + length / 2f, inner.MaxZ - depth / 2f); rot = 0f; break;
                        case Edge.West: center = new Vec2(inner.MinX + depth / 2f, start + length / 2f); rot = 90f; break;
                        default: center = new Vec2(inner.MaxX - depth / 2f, start + length / 2f); rot = 90f; break;
                    }

                    var size = new Vec3(length, arch.Height, depth);
                    var probe = new PlacedObject("probe", QuarterObjectTypes.Building,
                        new Vec3(center.X, 0f, center.Z), rot, size, null);
                    Rect2 footprint = probe.Footprint;
                    if (reserved.HasValue && footprint.Overlaps(reserved.Value)) continue;
                    if (!IsFree(footprint, 2.5f)) continue;

                    _solidFootprints.Add(footprint);
                    _placedCount[arch.Floors] = _placedCount.TryGetValue(arch.Floors, out int oc) ? oc + 1 : 1;
                    string buildingId = "bld-" + (++_buildingSeq).ToString("D3", CultureInfo.InvariantCulture);

                    // Архитектурный вариант дома: без одинаковых комбинаций у соседей.
                    BuildingVariant variant = PickVariant(arch.Floors, center,
                        out string facadeColor, out int balconyOffset, out float wearLevel);
                    string doorColor = _rng.Pick(DoorColors);

                    // Подъезды: вдоль фасада, обращённого внутрь блока.
                    // normal — куда смотрит фасад (адаптеру: дверь, козырёк, номер подъезда).
                    Vec2 normal;
                    switch (edge)
                    {
                        case Edge.South: normal = new Vec2(0f, 1f); break;
                        case Edge.North: normal = new Vec2(0f, -1f); break;
                        case Edge.West: normal = new Vec2(1f, 0f); break;
                        default: normal = new Vec2(-1f, 0f); break;
                    }
                    int entranceCount = Math.Max(1, (int)Math.Round(length / arch.EntranceSpacing));
                    var entrances = new List<Vec2>();
                    for (int e = 0; e < entranceCount; e++)
                    {
                        float along = length * (e + 1) / (entranceCount + 1) - length / 2f;
                        Vec2 p;
                        switch (edge)
                        {
                            case Edge.South: p = new Vec2(center.X + along, center.Z + depth / 2f + 0.4f); break;
                            case Edge.North: p = new Vec2(center.X + along, center.Z - depth / 2f - 0.4f); break;
                            case Edge.West: p = new Vec2(center.X + depth / 2f + 0.4f, center.Z + along); break;
                            default: p = new Vec2(center.X - depth / 2f - 0.4f, center.Z + along); break;
                        }
                        entrances.Add(p);
                        AddObject(QuarterObjectTypes.Entrance, new Vec3(p.X, 0f, p.Z), rot,
                            new Vec3(3f, 3f, 1f),
                            Params(("buildingId", buildingId),
                                   ("entranceNumber", (e + 1).ToString(CultureInfo.InvariantCulture)),
                                   ("normalX", Num(normal.X)), ("normalZ", Num(normal.Z)),
                                   ("entranceStyle", variant.EntranceStyle), ("doorColor", doorColor)));
                    }

                    _buildings.Add(new BuildingInfo(buildingId, arch.Id, center, rot, size, arch.Floors,
                        entrances, variant.FacadeStyle, variant.RoofType, facadeColor, isPlayerHome: false,
                        variant.Id, variant.WindowStyle, variant.BalconyType, variant.RoofVariant,
                        wearLevel, balconyOffset));
                    var buildingParams = Params(
                        ("buildingId", buildingId), ("archetype", arch.Id),
                        ("floors", arch.Floors.ToString(CultureInfo.InvariantCulture)),
                        ("entrances", entranceCount.ToString(CultureInfo.InvariantCulture)),
                        ("style", variant.FacadeStyle), ("roof", variant.RoofType),
                        ("facadeColor", facadeColor),
                        ("variant", variant.Id), ("roofVariant", variant.RoofVariant),
                        ("windowStyle", variant.WindowStyle), ("balconyType", variant.BalconyType),
                        ("balconyOffset", balconyOffset.ToString(CultureInfo.InvariantCulture)),
                        ("wear", wearLevel.ToString("0.##", CultureInfo.InvariantCulture)),
                        ("entranceStyle", variant.EntranceStyle), ("doorColor", doorColor));
                    foreach (KeyValuePair<string, string> kv in FacadeDetailParams(length, arch.Floors))
                        buildingParams[kv.Key] = kv.Value;
                    AddObject(QuarterObjectTypes.Building, new Vec3(center.X, 0f, center.Z), rot, size,
                        buildingParams);
                }
            }

            private void BuildCourtyard(Rect2 block, Rect2 inner, float driveX)
            {
                // Двор — центр блока, очищенный от домов полосой минимум в глубину корпуса.
                Rect2 yard = inner.Shrunk(17f);
                string yardId = "yard-" + (++_courtyardSeq).ToString("D2", CultureInfo.InvariantCulture);
                AddObject(QuarterObjectTypes.Lawn, new Vec3(yard.Center.X, 0f, yard.Center.Z), 0f,
                    new Vec3(yard.Width, 0.05f, yard.Height), Params(("courtyardId", yardId)));

                // Парковка — южная полоса двора; проезд ведёт к южной улице блока.
                float parkingWidth = Math.Min(24f, yard.Width - 2f);
                var parkingRect = new Rect2(driveX - parkingWidth / 2f, yard.MinZ + 1f,
                                            driveX + parkingWidth / 2f, yard.MinZ + 11f);
                _solidFootprints.Add(parkingRect);
                AddObject(QuarterObjectTypes.ParkingLot,
                    new Vec3(parkingRect.Center.X, 0f, parkingRect.Center.Z), 0f,
                    new Vec3(parkingRect.Width, 0.09f, parkingRect.Height), Params(("courtyardId", yardId)));
                int stalls = 0;
                for (float x = parkingRect.MinX + 1.25f; x + 1.25f <= parkingRect.MaxX - 1f; x += 2.5f)
                {
                    AddObject(QuarterObjectTypes.ParkingStall, new Vec3(x, 0f, parkingRect.MinZ + 3.5f), 0f,
                        new Vec3(2.5f, 0.12f, 5f), Params(("courtyardId", yardId)));
                    stalls++;
                }

                float southStreetZ = block.MinZ - CorridorHalf; // осевая южной улицы блока
                string fromNode = AddNode(new Vec2(driveX, southStreetZ), false);
                string toNode = AddNode(new Vec2(driveX, parkingRect.Center.Z), false);
                AddRoad(fromNode, toNode, new Vec2(driveX, southStreetZ), new Vec2(driveX, parkingRect.Center.Z),
                        _o.DrivewayWidth, RoadKind.Driveway, QuarterObjectTypes.Driveway);

                // Детская площадка — северная часть двора.
                var playgroundRect = Rect2.FromCenterSize(
                    new Vec2(yard.Center.X, yard.MaxZ - 7f), 12f, 10f);
                _solidFootprints.Add(playgroundRect);
                AddObject(QuarterObjectTypes.Playground,
                    new Vec3(playgroundRect.Center.X, 0f, playgroundRect.Center.Z), 0f,
                    new Vec3(12f, 0.3f, 10f),
                    Params(("courtyardId", yardId), ("equipment", "sandbox,slide,swing"),
                           ("playgroundVariant", _courtyardSeq.ToString(CultureInfo.InvariantCulture))));

                // Вариант планировки двора (0..2): дворы различаются наполнением и
                // расположением оборудования, а не только случайными деревьями.
                int variant = _rng.NextInt(0, 3);

                // Оборудование детской площадки: качели и песочницы настоящими объектами.
                int swings = variant == 1 ? 1 : 2;
                for (int i = 0; i < swings; i++)
                {
                    var p = new Vec2(playgroundRect.MinX + 2.5f + i * 3.5f, playgroundRect.MaxZ - 2f);
                    AddObject(QuarterObjectTypes.Swing, new Vec3(p.X, 0f, p.Z), 0f,
                        new Vec3(2.4f, 2.2f, 0.8f), Params(("courtyardId", yardId), ("seats", "2")));
                }
                int sandboxes = variant == 2 ? 2 : 1;
                for (int i = 0; i < sandboxes; i++)
                {
                    var p = new Vec2(playgroundRect.MaxX - 2.5f - i * 3.5f, playgroundRect.MinZ + 2f);
                    AddObject(QuarterObjectTypes.Sandbox, new Vec3(p.X, 0f, p.Z), 0f,
                        new Vec3(2.2f, 0.4f, 2.2f), Params(("courtyardId", yardId)));
                }

                // Пешеходные дорожки: от проезда/парковки к площадке и поперёк двора.
                AddObject(QuarterObjectTypes.Footpath,
                    new Vec3(driveX, 0f, (parkingRect.MaxZ + playgroundRect.MinZ) / 2f), 90f,
                    new Vec3(playgroundRect.MinZ - parkingRect.MaxZ, 0.06f, 1.6f),
                    Params(("courtyardId", yardId)));
                if (variant != 1)
                {
                    AddObject(QuarterObjectTypes.Footpath,
                        new Vec3(yard.Center.X, 0f, playgroundRect.MinZ - 3.5f), 0f,
                        new Vec3(yard.Width - 4f, 0.06f, 1.6f), Params(("courtyardId", yardId)));
                }

                // Лавочки вокруг площадки + урны рядом с частью лавочек.
                int benches = _rng.NextInt(4, 7);
                int urns = 0;
                for (int i = 0; i < benches; i++)
                {
                    float t = benches == 1 ? 0f : i / (float)(benches - 1);
                    var p = new Vec2(playgroundRect.MinX - 2f + t * (playgroundRect.Width + 4f),
                                     playgroundRect.MinZ - 2f);
                    AddObject(QuarterObjectTypes.Bench, new Vec3(p.X, 0f, p.Z), 0f,
                        new Vec3(1.8f, 0.6f, 0.6f),
                        Params(("courtyardId", yardId),
                               ("benchStyle", _rng.NextChance(0.5f) ? "new" : "old")));
                    if (i % 2 == 0)
                    {
                        AddObject(QuarterObjectTypes.Urn, new Vec3(p.X + 1.3f, 0f, p.Z), 0f,
                            new Vec3(0.45f, 0.7f, 0.45f), Params(("courtyardId", yardId)));
                        urns++;
                    }
                }

                // Ограждения палисадника вдоль южной стороны двора (не сплошняком — с разрывами у дорожек).
                int fenceSegments = _rng.NextInt(2, 4);
                for (int i = 0; i < fenceSegments; i++)
                {
                    float fx = yard.MinX + 3f + i * ((yard.Width - 6f) / Math.Max(1, fenceSegments));
                    if (Math.Abs(fx - driveX) < 3f) continue; // разрыв у дорожки к проезду
                    AddObject(QuarterObjectTypes.YardFence, new Vec3(fx, 0f, yard.MinZ + 0.8f), 0f,
                        new Vec3(4.5f, 0.55f, 0.08f),
                        Params(("courtyardId", yardId), ("fenceStyle", _rng.NextChance(0.5f) ? "wood" : "metal")));
                }

                // Мусорные контейнеры у проезда, рядом с парковкой.
                int trashContainers = variant == 0 ? 3 : 2;
                for (int i = 0; i < trashContainers; i++)
                {
                    var p = new Vec2(parkingRect.MaxX + 2f, parkingRect.MinZ + 1.5f + i * 2.2f);
                    if (p.X > yard.MaxX - 1f) p = new Vec2(parkingRect.MinX - 2f, p.Z);
                    AddObject(QuarterObjectTypes.TrashContainer, new Vec3(p.X, 0f, p.Z), 0f,
                        new Vec3(1.9f, 1.2f, 1.1f), Params(("courtyardId", yardId)));
                }

                // Деревья двора: случайно, но не на площадке/парковке/домах.
                int treesWanted = _rng.NextInt(6, 11);
                int treesPlaced = 0, attempts = 0;
                while (treesPlaced < treesWanted && attempts++ < 300)
                {
                    var p = new Vec2(_rng.NextRange(yard.MinX + 1.5f, yard.MaxX - 1.5f),
                                     _rng.NextRange(yard.MinZ + 1.5f, yard.MaxZ - 1.5f));
                    if (!IsFree(Rect2.FromCenterSize(p, 1.2f, 1.2f), 1.2f)) continue;
                    AddTree(p, yardId);
                    treesPlaced++;
                }

                // Дворовые фонари по углам двора.
                foreach (Vec2 corner in new[]
                         {
                             new Vec2(yard.MinX + 1f, yard.MinZ + 1f), new Vec2(yard.MaxX - 1f, yard.MinZ + 1f),
                             new Vec2(yard.MinX + 1f, yard.MaxZ - 1f), new Vec2(yard.MaxX - 1f, yard.MaxZ - 1f),
                         })
                {
                    AddObject(QuarterObjectTypes.YardLamp, new Vec3(corner.X, 0f, corner.Z), 0f,
                        new Vec3(0.25f, 4.5f, 0.25f), Params(("courtyardId", yardId), ("height", "4.5")));
                }

                // Кусты вдоль дорожек и у площадки.
                int bushesWanted = _rng.NextInt(4, 9);
                int bushesPlaced = 0;
                attempts = 0;
                while (bushesPlaced < bushesWanted && attempts++ < 200)
                {
                    var p = new Vec2(_rng.NextRange(yard.MinX + 1f, yard.MaxX - 1f),
                                     _rng.NextRange(yard.MinZ + 1f, yard.MaxZ - 1f));
                    if (!IsFree(Rect2.FromCenterSize(p, 0.9f, 0.9f), 0.8f)) continue;
                    AddObject(QuarterObjectTypes.Bush, new Vec3(p.X, 0f, p.Z), _rng.NextRange(0f, 360f),
                        new Vec3(_rng.NextRange(0.9f, 1.5f), 0.9f, _rng.NextRange(0.9f, 1.5f)),
                        Params(("courtyardId", yardId)));
                    bushesPlaced++;
                }

                _courtyards.Add(new CourtyardInfo(yardId, yard, parkingRect.Center,
                    benches, treesPlaced, stalls, hasPlayground: true,
                    swings: swings, sandboxes: sandboxes, urns: urns,
                    trashContainers: trashContainers, bushes: bushesPlaced, hasFootpath: true));
            }

            private void PlantBlockGreenery(Rect2 inner)
            {
                // Обычный блок после сокращения застройки (ТЗ №025) — это зелёный
                // внутриквартальный сквер, а не пустота: газон, дорожка, лавочки, деревья.
                Rect2 center = inner.Shrunk(17f);
                if (center.Width <= 4f || center.Height <= 4f) return;
                AddObject(QuarterObjectTypes.Lawn, new Vec3(center.Center.X, 0f, center.Center.Z), 0f,
                    new Vec3(center.Width, 0.05f, center.Height));
                AddObject(QuarterObjectTypes.Footpath, new Vec3(center.Center.X, 0f, center.Center.Z), 0f,
                    new Vec3(center.Width, 0.06f, 1.6f));
                int benchCount = _rng.NextInt(1, 3);
                for (int i = 0; i < benchCount; i++)
                {
                    var bp = new Vec2(center.MinX + 2f + i * (center.Width - 4f) / Math.Max(1, benchCount),
                                      center.Center.Z + 1.8f);
                    AddObject(QuarterObjectTypes.Bench, new Vec3(bp.X, 0f, bp.Z), 0f,
                        new Vec3(1.8f, 0.6f, 0.6f),
                        Params(("benchStyle", _rng.NextChance(0.5f) ? "new" : "old")));
                }
                int trees = _rng.NextInt(3, 7);
                for (int i = 0; i < trees; i++)
                {
                    var p = new Vec2(_rng.NextRange(center.MinX + 1f, center.MaxX - 1f),
                                     _rng.NextRange(center.MinZ + 1f, center.MaxZ - 1f));
                    if (!IsFree(Rect2.FromCenterSize(p, 1.2f, 1.2f), 1.5f)) continue;
                    AddTree(p, null);
                }
            }

            private void AddTree(Vec2 p, string yardId)
            {
                float crown = _rng.NextRange(2.2f, 3.6f);
                var parameters = Params(("crownDiameter", Num(crown)), ("trunkHeight", Num(_rng.NextRange(1.8f, 2.6f))));
                if (yardId != null) parameters["courtyardId"] = yardId;
                AddObject(QuarterObjectTypes.Tree, new Vec3(p.X, 0f, p.Z), _rng.NextRange(0f, 360f),
                    new Vec3(crown, 4.5f, crown), parameters);
            }

            // ---------- точки доставки у подъездов ----------

            /// <summary>
            /// Точка delivery.dropoff перед каждым жилым подъездом: уникальный Id,
            /// Id здания, номер подъезда, радиус — в DeliveryPointInfo и в объекте-маркере.
            /// Точка вынесена от стены: подъезд уже в 0.4 м от фасада, точка — ещё в 1.0 м наружу.
            /// </summary>
            private void BuildDeliveryPoints()
            {
                int seq = 0;
                foreach (BuildingInfo b in _buildings)
                {
                    for (int i = 0; i < b.Entrances.Count; i++)
                    {
                        Vec2 entrance = b.Entrances[i];
                        Vec2 away = entrance - b.Center;
                        Vec2 normal = Math.Abs(away.X) > Math.Abs(away.Z)
                            ? new Vec2(Math.Sign(away.X), 0f)
                            : new Vec2(0f, Math.Sign(away.Z));
                        Vec2 p = entrance + normal * 1.0f;
                        string id = "drop-" + (++seq).ToString("D4", CultureInfo.InvariantCulture);
                        _deliveryPoints.Add(new DeliveryPointInfo(id, b.Id, i + 1, new Vec3(p.X, 0f, p.Z)));
                        AddObject(QuarterObjectTypes.DeliveryDropoff, new Vec3(p.X, 0f, p.Z), 0f,
                            new Vec3(0.7f, 0.05f, 0.7f),
                            Params(("pointId", id), ("buildingId", b.Id),
                                   ("entranceNumber", (i + 1).ToString(CultureInfo.InvariantCulture)),
                                   ("interactionRadius", Num(DeliveryPointInfo.DefaultInteractionRadius))));
                    }
                }
            }

            // ---------- реестр недвижимости (жильё игрока) ----------

            /// <summary>
            /// Собирает QuarterPlan.Properties из построенных зданий. Система не
            /// привязана к пятиэтажке: стартовая квартира — дом игрока (5 этажей),
            /// квартира в девятиэтажке — первое 9-этажное здание квартала,
            /// элитные ЖК и частные дома с ТЗ №025 — реальные здания с записями
            /// FuturePurchasable (плановые записи без зданий остаются фолбэком
            /// при отключённых опциях). Покупки, переездов и цен здесь нет.
            /// </summary>
            private void BuildRealEstateRegistry()
            {
                if (_playerHome != null)
                {
                    _properties.Add(new RealEstateProperty(
                        PlayerHomeInfo.SpecialId, HousingKind.StartApartment,
                        _playerHome.BuildingId, _playerHome.Center,
                        PlayerHomeInfo.ApartmentNumber, PropertyStatus.PlayerStartHome));
                }

                foreach (BuildingInfo b in _buildings)
                {
                    if (b.Floors != 9 || b.IsPlayerHome) continue;
                    _properties.Add(new RealEstateProperty(
                        "property.apartment9.01", HousingKind.ApartmentNineStorey,
                        b.Id, b.Center, apartmentNumber: 0, PropertyStatus.FuturePurchasable));
                    break;
                }

                // Элитные ЖК и частные дома с ТЗ №025 построены в квартале:
                // записи привязаны к реальным зданиям; без них — плановые записи.
                if (_eliteBuildings.Count > 0)
                {
                    for (int i = 0; i < _eliteBuildings.Count; i++)
                    {
                        _properties.Add(new RealEstateProperty(
                            "property.elite.0" + (i + 1).ToString(CultureInfo.InvariantCulture),
                            HousingKind.EliteHighRise,
                            _eliteBuildings[i].Id, _eliteBuildings[i].Center,
                            apartmentNumber: 0, PropertyStatus.FuturePurchasable));
                    }
                }
                else
                {
                    _properties.Add(new RealEstateProperty(
                        "property.elite.highrise.future", HousingKind.EliteHighRise,
                        buildingId: null, position: null, apartmentNumber: 0, PropertyStatus.PlannedNotBuilt));
                }
                if (_privateBuildings.Count > 0)
                {
                    for (int i = 0; i < _privateBuildings.Count; i++)
                    {
                        _properties.Add(new RealEstateProperty(
                            "property.private.0" + (i + 1).ToString(CultureInfo.InvariantCulture),
                            HousingKind.PrivateHouse,
                            _privateBuildings[i].Id, _privateBuildings[i].Center,
                            apartmentNumber: 0, PropertyStatus.FuturePurchasable));
                    }
                }
                else
                {
                    _properties.Add(new RealEstateProperty(
                        "property.private.house.future", HousingKind.PrivateHouse,
                        buildingId: null, position: null, apartmentNumber: 0, PropertyStatus.PlannedNotBuilt));
                }
            }

            // ---------- подвал под трёхэтажкой ----------

            /// <summary>Локальные координаты подвала/лестницы -> мировые (поворот дома 0/90/180/270).</summary>
            private static Vec2 LocalToWorld(BuildingInfo b, float lx, float lz)
            {
                float r = b.RotationY;
                if (Math.Abs(r) < 0.01f) return new Vec2(b.Center.X + lx, b.Center.Z + lz);
                if (Math.Abs(Math.Abs(r) - 90f) < 0.01f) return new Vec2(b.Center.X + lz, b.Center.Z - lx);
                if (Math.Abs(Math.Abs(r) - 180f) < 0.01f) return new Vec2(b.Center.X - lx, b.Center.Z - lz);
                return new Vec2(b.Center.X - lz, b.Center.Z + lx);
            }

            /// <summary>
            /// Подвал под первой трёхэтажкой квартала. Снаружи: площадка, бетонная
            /// лестница вниз с задней стороны дома, ржавая железная дверь
            /// (basement.door, начальное состояние Locked) и тусклый фонарь над ней.
            /// Внутри: общий бетонный пол/потолок, коридор, две комнаты и секретная
            /// комната за второй дверью в конце коридора. Грунта/перекрытий поперёк
            /// прохода генератор не создаёт: единственный потолок — плита над всем
            /// подвалом на высоте 2.45 м от пола подвала, лестничный проём — снаружи здания.
            /// </summary>
            private void BuildBasement()
            {
                // Выбираем трёхэтажку, у которой лестница с задней стороны помещается
                // в квартал и не ложится на проезжую часть (у краевых домов задней
                // стороной к границе квартала места может не хватить — их пропускаем).
                BuildingInfo home = null;
                Vec2 rear = new Vec2(0f, -1f);
                foreach (BuildingInfo b in _buildings)
                {
                    if (b.Floors != 3) continue;
                    Vec2 inN = b.Entrances[0] - b.Center;
                    Vec2 r = Math.Abs(inN.X) > Math.Abs(inN.Z)
                        ? new Vec2(-Math.Sign(inN.X), 0f)
                        : new Vec2(0f, -Math.Sign(inN.Z));
                    Vec2 top = b.Center + r * (b.Size.Z / 2f + 3.4f);
                    if (top.X < 1f || top.Z < 1f || top.X > _o.Size - 1f || top.Z > _o.Size - 1f) continue;
                    Vec2 mid = b.Center + r * (b.Size.Z / 2f + 1.7f);
                    var stairRect = Rect2.FromCenterSize(mid,
                        Math.Abs(r.Z) > 0.5f ? 2.4f : 3.6f, Math.Abs(r.Z) > 0.5f ? 3.6f : 2.4f);
                    bool onRoad = false;
                    foreach (RoadSegment road in _roads)
                        if (road.Kind == RoadKind.Street && stairRect.Overlaps(road.Footprint))
                            onRoad = true;
                    if (onRoad) continue;
                    home = b;
                    rear = r;
                    break;
                }
                if (home == null) return; // нет подходящей трёхэтажки — подвала нет

                float length = home.Size.X, depth = home.Size.Z;
                bool rearAlongZ = Math.Abs(rear.Z) > 0.5f;

                // --- внешний вход и лестница ---
                Vec2 stairTopGround = home.Center + rear * (depth / 2f + 3.4f);
                Vec2 stairMidGround = home.Center + rear * (depth / 2f + 1.7f);
                _solidFootprints.Add(Rect2.FromCenterSize(stairMidGround,
                    rearAlongZ ? 2.4f : 3.6f, rearAlongZ ? 3.6f : 2.4f));
                var entranceTop = new Vec3(stairTopGround.X, 0f, stairTopGround.Z);
                AddObject(QuarterObjectTypes.BasementPoint, entranceTop, home.RotationY,
                    new Vec3(2.4f, 0.07f, 1.2f),
                    Params(("buildingId", home.Id), ("pointId", "basement.entry.exterior")));
                const int steps = 7;
                for (int i = 0; i < steps; i++)
                {
                    Vec2 sp = home.Center + rear * (depth / 2f + 3.0f - i * 0.42f);
                    AddObject(QuarterObjectTypes.BasementStep, new Vec3(sp.X, -0.35f - i * 0.4f, sp.Z),
                        home.RotationY, new Vec3(1.8f, 0.18f, 0.5f),
                        Params(("buildingId", home.Id)));
                }
                Vec2 bottomGround = home.Center + rear * (depth / 2f + 0.2f);
                var stairBottom = new Vec3(bottomGround.X, -2.8f, bottomGround.Z);
                Vec2 doorGround = home.Center + rear * (depth / 2f - 0.15f);
                var doorPos = new Vec3(doorGround.X, -2.8f, doorGround.Z);
                AddObject(QuarterObjectTypes.BasementDoor, doorPos, home.RotationY,
                    new Vec3(1.2f, 2.2f, 0.18f),
                    Params(("buildingId", home.Id), ("pointId", "basement.door"),
                           ("state", BasementDoorState.Locked.ToString()),
                           ("material", "rusty-iron"), ("interactionRadius", "1.5")));
                // Тусклый фонарь над входом снаружи.
                Vec2 lampGround = home.Center + rear * (depth / 2f + 0.3f);
                AddObject(QuarterObjectTypes.BasementLamp, new Vec3(lampGround.X, 2.4f, lampGround.Z),
                    home.RotationY, new Vec3(0.3f, 0.25f, 0.3f),
                    Params(("buildingId", home.Id), ("exterior", "true"), ("dim", "true")));

                // --- интерьер: пол, потолок, стены (локальные координаты дома) ---
                void Obj(string type, float lx, float y, float lz, Vec3 size, Dictionary<string, string> p = null)
                {
                    Vec2 w = LocalToWorld(home, lx, lz);
                    AddObject(type, new Vec3(w.X, y, w.Z), home.RotationY, size,
                        p ?? Params(("buildingId", home.Id)));
                }

                float floorY = -3.0f, wallBaseY = -2.8f;
                Obj(QuarterObjectTypes.BasementFloor, 0f, floorY, 0f, new Vec3(length - 1f, 0.2f, depth - 1f));
                Obj(QuarterObjectTypes.BasementCeiling, 0f, -0.3f, 0f, new Vec3(length - 1f, 0.2f, depth - 1f));
                float hx = length / 2f - 0.6f, hz = depth / 2f - 0.6f;
                // Наружные стены подвала. В задней стене (со стороны лестницы) —
                // дверной проём шириной 1.4 м под железную дверь + перемычка над ним:
                // сплошной стены поперёк прохода быть не должно.
                Vec2 localZWorld = LocalToWorld(home, 0f, 1f) - home.Center;
                float rearSign = (rear.X * localZWorld.X + rear.Z * localZWorld.Z) >= 0f ? 1f : -1f;
                foreach (float side in new[] { -1f, 1f })
                {
                    if (Math.Abs(side - rearSign) > 0.01f)
                    {
                        Obj(QuarterObjectTypes.BasementWall, 0f, wallBaseY, side * hz, new Vec3(length - 1f, 2.6f, 0.3f));
                    }
                    else
                    {
                        float segLen = hx - 0.7f;
                        Obj(QuarterObjectTypes.BasementWall, -(0.7f + segLen / 2f), wallBaseY, side * hz, new Vec3(segLen, 2.6f, 0.3f));
                        Obj(QuarterObjectTypes.BasementWall, 0.7f + segLen / 2f, wallBaseY, side * hz, new Vec3(segLen, 2.6f, 0.3f));
                        Obj(QuarterObjectTypes.BasementWall, 0f, wallBaseY + 2.2f, side * hz, new Vec3(1.4f, 0.4f, 0.3f)); // перемычка
                    }
                }
                Obj(QuarterObjectTypes.BasementWall, -hx, wallBaseY, 0f, new Vec3(0.3f, 2.6f, depth - 1f));
                Obj(QuarterObjectTypes.BasementWall, hx, wallBaseY, 0f, new Vec3(0.3f, 2.6f, depth - 1f));
                // Перегородка коридор/комнаты (z=+1.1) с двумя дверными проёмами (разрывы сегментов).
                Obj(QuarterObjectTypes.BasementWall, -8.5f, wallBaseY, 1.1f, new Vec3(3f, 2.6f, 0.25f));
                Obj(QuarterObjectTypes.BasementWall, -1.5f, wallBaseY, 1.1f, new Vec3(5f, 2.6f, 0.25f));
                Obj(QuarterObjectTypes.BasementWall, 5f, wallBaseY, 1.1f, new Vec3(2f, 2.6f, 0.25f));
                // Стена секретной комнаты (x=+6): вторая дверь по центру коридора.
                Obj(QuarterObjectTypes.BasementWall, 6f, wallBaseY, -1.9f, new Vec3(0.25f, 2.6f, 1.6f));
                Obj(QuarterObjectTypes.BasementWall, 6f, wallBaseY, 1.9f, new Vec3(0.25f, 2.6f, 1.6f));
                Obj(QuarterObjectTypes.BasementSecondDoor, 6f, wallBaseY, 0f, new Vec3(0.18f, 2.1f, 1.1f),
                    Params(("buildingId", home.Id), ("state", BasementDoorState.Locked.ToString())));

                // --- наполнение: стеллажи и ящики (storage), стол и стулья (table), трубы, лампы ---
                foreach (float sx in new[] { 0.5f, 2.5f, 4.5f })
                    Obj(QuarterObjectTypes.BasementShelf, sx, wallBaseY, 4.6f, new Vec3(1.8f, 2f, 0.6f));
                foreach ((float cx2, float cz2) in new[] { (1f, 2f), (4f, 2.2f), (-8f, -0.5f), (8f, 1.8f) })
                    Obj(QuarterObjectTypes.BasementCrate, cx2, wallBaseY, cz2, new Vec3(0.7f, 0.7f, 0.7f));
                Obj(QuarterObjectTypes.BasementTable, -5.5f, wallBaseY, 3.2f, new Vec3(1.8f, 0.8f, 0.9f));
                Obj(QuarterObjectTypes.BasementChair, -6.3f, wallBaseY, 3.2f, new Vec3(0.45f, 0.7f, 0.45f));
                Obj(QuarterObjectTypes.BasementChair, -4.7f, wallBaseY, 3.2f, new Vec3(0.45f, 0.7f, 0.45f));
                Obj(QuarterObjectTypes.BasementPipe, -2f, -0.55f, -0.7f, new Vec3(16f, 0.18f, 0.18f));
                Obj(QuarterObjectTypes.BasementPipe, -2f, -0.8f, 0.7f, new Vec3(16f, 0.14f, 0.14f));
                foreach (float lx in new[] { -7f, 0f, 7f })
                    Obj(QuarterObjectTypes.BasementLamp, lx, -0.45f, 0f, new Vec3(0.35f, 0.12f, 0.35f),
                        Params(("buildingId", home.Id), ("dim", "true")));

                // --- именованные точки подвала ---
                Vec2 insideDoor = doorGround - rear * 1.2f; // сразу за железной дверью
                Vec2 tableW = LocalToWorld(home, -5.5f, 3.2f);
                Vec2 storageW = LocalToWorld(home, 2.5f, 3.2f);
                Vec2 secretW = LocalToWorld(home, 8.5f, 0f);
                var points = new Dictionary<string, Vec3>
                {
                    { "basement.entry", new Vec3(insideDoor.X, -2.8f, insideDoor.Z) },
                    { "basement.table", new Vec3(tableW.X, -2.8f, tableW.Z) },
                    { "basement.storage", new Vec3(storageW.X, -2.8f, storageW.Z) },
                    { "basement.secretroom", new Vec3(secretW.X, -2.8f, secretW.Z) },
                };
                foreach (KeyValuePair<string, Vec3> kv in points)
                {
                    AddObject(QuarterObjectTypes.BasementPoint, kv.Value, 0f,
                        new Vec3(0.7f, 0.05f, 0.7f),
                        Params(("buildingId", home.Id), ("pointId", kv.Key)));
                }

                _basement = new BasementInfo(home.Id, entranceTop, stairBottom, doorPos, points);
            }

            // ---------- уличное наполнение ----------

            private void BuildStreetFurniture()
            {
                float s = _o.Size;
                // Асфальтовые заплатки: умеренная обжитость, не на перекрёстках.
                foreach (float line in _lines)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float t = _rng.NextRange(8f, s - 8f);
                        bool nearCross = false;
                        foreach (float l in _lines)
                            if (Math.Abs(t - l) < CorridorHalf + 4f) nearCross = true;
                        if (nearCross) continue;
                        float lane = _rng.NextRange(-_o.RoadWidth / 2f + 1f, _o.RoadWidth / 2f - 1f);
                        bool vertical = _rng.NextChance(0.5f);
                        AddObject(QuarterObjectTypes.RoadPatch,
                            vertical ? new Vec3(line + lane, 0f, t) : new Vec3(t, 0f, line + lane),
                            vertical ? 90f : 0f,
                            new Vec3(_rng.NextRange(2f, 4.5f), 0.03f, _rng.NextRange(1.5f, 3f)));
                    }
                }
                // Уличные фонари: вдоль улиц с шагом, стороны чередуются, у перекрёстков — пропуск.
                foreach (float line in _lines)
                {
                    int stepIndex = 0;
                    for (float t = _o.StreetLampSpacing / 2f; t < s; t += _o.StreetLampSpacing, stepIndex++)
                    {
                        if (NearIntersection(t)) continue;
                        float side = (stepIndex % 2 == 0) ? 1f : -1f;
                        float off = side * (_o.RoadWidth / 2f + 0.6f);
                        AddLamp(new Vec2(line + off, t));
                        AddLamp(new Vec2(t, line + off));
                    }
                }
                // Уличные деревья на тротуарах/газонах у улиц.
                foreach (float line in _lines)
                {
                    for (float t = 9f; t < s; t += 18f)
                    {
                        if (NearIntersection(t) || !_rng.NextChance(0.5f)) continue;
                        float off = CorridorHalf + 1.2f;
                        TryStreetTree(new Vec2(line + off, t + _rng.NextRange(-2f, 2f)));
                        TryStreetTree(new Vec2(t + _rng.NextRange(-2f, 2f), line - off));
                    }
                }

                bool NearIntersection(float t)
                {
                    foreach (float l in _lines)
                        if (Math.Abs(t - l) < CorridorHalf + 4f)
                            return true;
                    return false;
                }
            }

            private void AddLamp(Vec2 p)
            {
                AddObject(QuarterObjectTypes.StreetLamp, new Vec3(p.X, 0f, p.Z), 0f,
                    new Vec3(0.3f, 7f, 0.3f), Params(("height", "7")));
            }

            private void TryStreetTree(Vec2 p)
            {
                if (p.X < 1f || p.Z < 1f || p.X > _o.Size - 1f || p.Z > _o.Size - 1f) return;
                if (!IsFree(Rect2.FromCenterSize(p, 1.2f, 1.2f), 1f)) return;
                AddTree(p, null);
            }
        }
    }

    /// <summary>
    /// Связность дорожного графа. Узлы связаны не только общими концами сегментов:
    /// узел, лежащий НА чужом сегменте (примыкание проезда к улице), тоже создаёт связь.
    /// </summary>
    public static class RoadGraph
    {
        /// <summary>Все ли узлы достижимы из первого (квартал не должен распадаться на острова).</summary>
        public static bool IsConnected(QuarterPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.Nodes.Count == 0) return false;
            Dictionary<string, List<string>> adjacency = BuildAdjacency(plan);
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(plan.Nodes[0].Id);
            visited.Add(plan.Nodes[0].Id);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (string next in adjacency[current])
                    if (visited.Add(next))
                        queue.Enqueue(next);
            }
            return visited.Count == plan.Nodes.Count;
        }

        public static Dictionary<string, List<string>> BuildAdjacency(QuarterPlan plan)
        {
            var adjacency = new Dictionary<string, List<string>>();
            foreach (RoadNode node in plan.Nodes)
                adjacency[node.Id] = new List<string>();

            void Link(string a, string b)
            {
                if (a == b) return;
                if (!adjacency[a].Contains(b)) adjacency[a].Add(b);
                if (!adjacency[b].Contains(a)) adjacency[b].Add(a);
            }

            foreach (RoadSegment seg in plan.Roads)
            {
                Link(seg.StartNodeId, seg.EndNodeId);
                // Примыкания: любой узел на этом сегменте связан с его концами.
                foreach (RoadNode node in plan.Nodes)
                {
                    if (node.Id == seg.StartNodeId || node.Id == seg.EndNodeId) continue;
                    if (PointOnSegment(node.Position, seg.Start, seg.End))
                    {
                        Link(node.Id, seg.StartNodeId);
                        Link(node.Id, seg.EndNodeId);
                    }
                }
            }
            return adjacency;
        }

        private static bool PointOnSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            const float eps = 0.01f;
            if (Math.Abs(a.X - b.X) < eps) // вертикальный
                return Math.Abs(p.X - a.X) < eps && p.Z >= Math.Min(a.Z, b.Z) - eps && p.Z <= Math.Max(a.Z, b.Z) + eps;
            if (Math.Abs(a.Z - b.Z) < eps) // горизонтальный
                return Math.Abs(p.Z - a.Z) < eps && p.X >= Math.Min(a.X, b.X) - eps && p.X <= Math.Max(a.X, b.X) + eps;
            return false; // генератор строит только осевые сегменты
        }
    }

    /// <summary>Каноническая текстовая форма плана: одинаковый seed = одинаковая строка.</summary>
    public static class QuarterPlanSerializer
    {
        public static string Serialize(QuarterPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var sb = new StringBuilder();
            sb.Append("seed=").Append(plan.Seed).Append(";size=").Append(F(plan.Size)).Append('\n');
            foreach (PlacedObject o in plan.Objects)
            {
                sb.Append(o.Id).Append('|').Append(o.TypeId)
                  .Append('|').Append(F(o.Position.X)).Append(',').Append(F(o.Position.Y)).Append(',').Append(F(o.Position.Z))
                  .Append('|').Append(F(o.RotationY))
                  .Append('|').Append(F(o.Size.X)).Append(',').Append(F(o.Size.Y)).Append(',').Append(F(o.Size.Z));
                var keys = new List<string>(o.Parameters.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (string key in keys)
                    sb.Append('|').Append(key).Append('=').Append(o.Parameters[key]);
                sb.Append('\n');
            }
            foreach (RoadSegment r in plan.Roads)
                sb.Append(r.Id).Append('|').Append(r.Kind)
                  .Append('|').Append(F(r.Start.X)).Append(',').Append(F(r.Start.Z))
                  .Append("->").Append(F(r.End.X)).Append(',').Append(F(r.End.Z))
                  .Append('|').Append(F(r.Width)).Append('\n');
            if (plan.PickupPoint != null)
            {
                PickupPointInfo p = plan.PickupPoint;
                sb.Append(p.Id).Append("|pickup")
                  .Append("|entrance=").Append(F(p.Entrance.X)).Append(',').Append(F(p.Entrance.Z))
                  .Append("|interaction=").Append(F(p.InteractionPoint.X)).Append(',').Append(F(p.InteractionPoint.Z))
                  .Append('\n');
            }
            if (plan.PlayerHome != null)
                sb.Append(plan.PlayerHome.Id).Append("|building=").Append(plan.PlayerHome.BuildingId)
                  .Append("|door=").Append(F(plan.PlayerHome.EntranceDoor.X)).Append(',').Append(F(plan.PlayerHome.EntranceDoor.Z))
                  .Append('\n');
            if (plan.Shop != null)
                sb.Append(plan.Shop.Id).Append("|shop")
                  .Append("|entrance=").Append(F(plan.Shop.Entrance.X)).Append(',').Append(F(plan.Shop.Entrance.Z))
                  .Append('\n');
            foreach (DeliveryPointInfo d in plan.DeliveryPoints)
                sb.Append(d.Id).Append("|dropoff|building=").Append(d.BuildingId)
                  .Append("|entrance=").Append(d.EntranceNumber)
                  .Append('|').Append(F(d.Position.X)).Append(',').Append(F(d.Position.Z)).Append('\n');
            foreach (RealEstateProperty property in plan.Properties)
                sb.Append(property.Id).Append("|property|kind=").Append(property.Kind)
                  .Append("|building=").Append(property.BuildingId ?? "-")
                  .Append("|status=").Append(property.Status).Append('\n');
            if (plan.Basement != null)
            {
                sb.Append("basement|building=").Append(plan.Basement.BuildingId)
                  .Append("|door=").Append(F(plan.Basement.DoorPosition.X)).Append(',').Append(F(plan.Basement.DoorPosition.Z));
                var keys = new List<string>(plan.Basement.Points.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (string key in keys)
                    sb.Append('|').Append(key).Append('=')
                      .Append(F(plan.Basement.Points[key].X)).Append(',').Append(F(plan.Basement.Points[key].Z));
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
