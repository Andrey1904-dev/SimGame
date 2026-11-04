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
                BuildBlocksAndCourtyards();
                BuildStreetFurniture();

                var context = new QuarterPlacementContext(_rng, s, _objects, o => _objects.Add(o), IsFree);
                foreach (IQuarterObjectPlacer placer in _o.CustomPlacers)
                    placer.Place(context);

                return new QuarterPlan(_o.Seed, s, _objects, _roads, _nodes, _buildings, _courtyards);
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
                    if (b.MinX > 1f && b.MinZ > 1f && b.MaxX < _o.Size - 1f && b.MaxZ < _o.Size - 1f)
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
                    BuildBlock(block, yards.Contains(block));
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
                    BuildingArchetype arch = _rng.Pick(_o.Archetypes);
                    // Берём случайную длину из тех, что ещё помещаются на стороне.
                    float length = -1f;
                    var variants = new List<float>(arch.LengthVariants);
                    while (variants.Count > 0 && length < 0f)
                    {
                        int idx = _rng.NextInt(0, variants.Count);
                        if (cursor + variants[idx] <= limit) length = variants[idx];
                        else variants.RemoveAt(idx);
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
                    string buildingId = "bld-" + (++_buildingSeq).ToString("D3", CultureInfo.InvariantCulture);

                    // Подъезды: вдоль фасада, обращённого внутрь блока.
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
                            new Vec3(3f, 3f, 1f), Params(("buildingId", buildingId)));
                    }

                    _buildings.Add(new BuildingInfo(buildingId, arch.Id, center, rot, size, arch.Floors, entrances));
                    AddObject(QuarterObjectTypes.Building, new Vec3(center.X, 0f, center.Z), rot, size,
                        Params(("buildingId", buildingId), ("archetype", arch.Id),
                               ("floors", arch.Floors.ToString(CultureInfo.InvariantCulture)),
                               ("entrances", entranceCount.ToString(CultureInfo.InvariantCulture))));
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
                    Params(("courtyardId", yardId), ("equipment", "sandbox,slide,swing")));

                // Лавочки вокруг площадки.
                int benches = _rng.NextInt(4, 7);
                for (int i = 0; i < benches; i++)
                {
                    float t = benches == 1 ? 0f : i / (float)(benches - 1);
                    var p = new Vec2(playgroundRect.MinX - 2f + t * (playgroundRect.Width + 4f),
                                     playgroundRect.MinZ - 2f);
                    AddObject(QuarterObjectTypes.Bench, new Vec3(p.X, 0f, p.Z), 0f,
                        new Vec3(1.8f, 0.6f, 0.6f), Params(("courtyardId", yardId)));
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

                _courtyards.Add(new CourtyardInfo(yardId, yard, parkingRect.Center,
                    benches, treesPlaced, stalls, hasPlayground: true));
            }

            private void PlantBlockGreenery(Rect2 inner)
            {
                // В обычных блоках центр не застраиваем: деревья с небольшим шансом,
                // чтобы квартал не выглядел пустым. Дворами такие блоки не считаются.
                Rect2 center = inner.Shrunk(17f);
                if (center.Width <= 4f || center.Height <= 4f) return;
                int trees = _rng.NextInt(2, 6);
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

            // ---------- уличное наполнение ----------

            private void BuildStreetFurniture()
            {
                float s = _o.Size;
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
            return sb.ToString();
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
