// Life From Zero — City/Tests (ТЗ №033: проверки карты District750)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Проверки геометрии и доступности по ДАННЫМ созданного QuarterPlan.
// План переводится в LayoutSnapshot (прямоугольники, точки доступа, дорожные рёбра),
// проверки работают над снимком. Поэтому негативные тесты могут мутировать снимок
// (переместить здание на улицу, запереть точку доставки, разорвать дорогу) и
// убедиться, что проверка это ловит. Проверки повторяют правила Python-зеркала
// City/Tools/district_layout_check.py, но не являются его копией.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LifeFromZero.City.Quarter.Tests
{
    /// <summary>Именованный прямоугольник плана. Group — категория для подсчётов и правил.</summary>
    public sealed class PlanRect
    {
        public PlanRect(string name, string group, Rect2 rect)
        {
            Name = name; Group = group; Rect = rect;
        }
        public string Name { get; }
        public string Group { get; }
        public Rect2 Rect { get; }
    }

    /// <summary>Точка доступа: вход, точка доставки или точка взаимодействия.</summary>
    public sealed class PlanPoint
    {
        public PlanPoint(string name, string kind, Vec2 position)
        {
            Name = name; Kind = kind; Position = position;
        }
        public string Name { get; }
        public string Kind { get; }
        public Vec2 Position { get; }
    }

    /// <summary>Дорожное ребро: два узла графа и полоса движения.</summary>
    public sealed class PlanRoad
    {
        public PlanRoad(string id, string from, string to, Vec2 a, Vec2 b, float width, bool driveway)
        {
            Id = id; From = from; To = to; A = a; B = b; Width = width; Driveway = driveway;
        }
        public string Id { get; }
        public string From { get; }
        public string To { get; }
        public Vec2 A { get; }
        public Vec2 B { get; }
        public float Width { get; }
        public bool Driveway { get; }
    }

    /// <summary>
    /// Снимок плана для проверок. Группы:
    /// Buildings — здания и корпуса (не пересекают улицы, друг друга, открытые площадки);
    /// Open — проходимые площадки: парковки, пады АЗС, площадки, погрузка (не на улицах);
    /// Greens — газоны (не на зданиях);
    /// Streets — полосы улиц и проездов с тротуарами;
    /// Blockers — непроходимое для пешехода (для проверки доступности).
    /// </summary>
    public sealed class LayoutSnapshot
    {
        public List<PlanRect> Buildings { get; } = new List<PlanRect>();
        public List<PlanRect> Open { get; } = new List<PlanRect>();
        public List<PlanRect> Greens { get; } = new List<PlanRect>();
        public List<PlanRect> Streets { get; } = new List<PlanRect>();
        public List<PlanRect> Blockers { get; } = new List<PlanRect>();
        public List<PlanPoint> Access { get; } = new List<PlanPoint>();
        public List<PlanRoad> Roads { get; } = new List<PlanRoad>();
        public List<string> Ids { get; } = new List<string>();
        public List<string> Types { get; } = new List<string>();
        public List<string> ObjectKeys { get; } = new List<string>();
    }

    public static class DistrictPlanChecks
    {
        public const float MapSize = 750f;
        private const float Eps = 0.01f;
        private const float SidewalkWidth = 2.5f;
        private const int Cells = 750;

        /// <summary>Обязательные объекты ТЗ №031/№033 (коммерция, публичные здания, АЗС, промзона, ПВЗ).</summary>
        public static readonly string[] RequiredIds =
        {
            "com-mall", "shop-01", "com-cafe", "com-restaurant", "com-clothing", "com-hardware",
            "com-pawnshop", "com-business", "com-incubator", "com-bank", "pvz-01",
            "com-city-hall", "com-police", "com-hospital", "com-education",
            "com-carservice", "com-tuning", "com-warehouse", "ind-factory", "ind-warehouse",
            "gas-01", "gas-02", "gas-03", "gas-04", "bld-n4",
        };

        /// <summary>Запрещённые объекты ТЗ №031: мойка, фитнес, вода, железная дорога.</summary>
        public static readonly string[] ForbiddenWords =
        {
            "wash", "мойк", "gym", "fitness", "фитнес", "water", "river", "lake", "rail", "железн",
        };

        /// <summary>Переводит план в снимок для проверок.</summary>
        public static LayoutSnapshot FromPlan(QuarterPlan plan)
        {
            var s = new LayoutSnapshot();

            foreach (BuildingInfo b in plan.Buildings)
            {
                s.Ids.Add(b.Id);
                Rect2 r = Footprint(b.Center, b.RotationY, b.Size);
                string group = GroupOfBuilding(b);
                s.Buildings.Add(new PlanRect(b.Id, group, r));
                s.Blockers.Add(new PlanRect(b.Id, group, r));
                for (int i = 0; i < b.Entrances.Count; i++)
                    s.Access.Add(new PlanPoint(b.Id + ".entrance" + (i + 1), "entrance", b.Entrances[i]));
            }

            var gasCenters = new List<Vec2>();
            foreach (DistrictPointInfo p in plan.DistrictPoints)
            {
                s.Ids.Add(p.Id);
                s.Types.Add(p.Type);
                if (p.Type == "gas.station")
                {
                    gasCenters.Add(p.Center);
                    s.Open.Add(new PlanRect(p.Id + ".pad", "gas-pad", p.Footprint));
                }
                else
                {
                    s.Buildings.Add(new PlanRect(p.Id, "point:" + p.Type, p.Footprint));
                    s.Blockers.Add(new PlanRect(p.Id, "point:" + p.Type, p.Footprint));
                }
                for (int i = 0; i < p.Entrances.Count; i++)
                    s.Access.Add(new PlanPoint(p.Id + ".entrance" + (i + 1), "entrance",
                        new Vec2(p.Entrances[i].X, p.Entrances[i].Z)));
                if (p.InteractionPoint.HasValue)
                {
                    Vec3 ip = p.InteractionPoint.Value;
                    s.Access.Add(new PlanPoint(p.Id + ".interaction", "interaction", new Vec2(ip.X, ip.Z)));
                }
            }

            foreach (DeliveryPointInfo d in plan.DeliveryPoints)
                s.Access.Add(new PlanPoint(d.Id, "delivery", new Vec2(d.Position.X, d.Position.Z)));

            foreach (PlacedObject o in plan.Objects)
            {
                s.Ids.Add(o.Id);
                s.Types.Add(o.TypeId);
                s.ObjectKeys.Add(o.TypeId + "|" + Q(o.Position.X) + "|" + Q(o.Position.Y) + "|" + Q(o.Position.Z));
                var c = new Vec2(o.Position.X, o.Position.Z);
                switch (o.TypeId)
                {
                    case QuarterObjectTypes.ParkingLot:
                        // Парковка АЗС совпадает с падом — пад уже учтён, второй раз не добавляем.
                        if (!gasCenters.Any(g => Near(g, c)))
                            s.Open.Add(new PlanRect(o.Id, "lot", Footprint(c, o.RotationY, o.Size)));
                        break;
                    case QuarterObjectTypes.Playground:
                    case QuarterObjectTypes.PickupPlaza:
                    case DistrictObjectTypes.IndustryLoading:
                        s.Open.Add(new PlanRect(o.Id, o.TypeId, Footprint(c, o.RotationY, o.Size)));
                        break;
                    case QuarterObjectTypes.Lawn:
                        s.Greens.Add(new PlanRect(o.Id, "lawn", Footprint(c, o.RotationY, o.Size)));
                        break;
                    case DistrictObjectTypes.GasIsland:
                    case DistrictObjectTypes.GasPump:
                    case DistrictObjectTypes.GasKiosk:
                    case DistrictObjectTypes.GasPost:
                    case DistrictObjectTypes.IndustryChimney:
                        s.Blockers.Add(new PlanRect(o.Id, o.TypeId, Footprint(c, o.RotationY, o.Size)));
                        break;
                    // Кольцо (DistrictObjectTypes.Roundabout) занимает перекрёсток и не проверяется
                    // против улиц; остальные объекты — декор без влияния на планировку.
                }
            }

            foreach (RoadSegment r in plan.Roads)
            {
                bool driveway = r.Kind == RoadKind.Driveway;
                float half = r.Width / 2f + (driveway ? 0f : SidewalkWidth);
                s.Streets.Add(new PlanRect(r.Id, driveway ? "driveway" : "street", StreetRect(r.Start, r.End, half)));
                s.Roads.Add(new PlanRoad(r.Id, r.StartNodeId, r.EndNodeId, r.Start, r.End, r.Width, driveway));
            }

            return s;
        }

        /// <summary>Все нарушения снимка. Пустой список — план проходит проверки.</summary>
        public static List<string> Analyze(LayoutSnapshot s)
        {
            var v = new List<string>();
            CheckBounds(s, v);
            CheckStreetOverlaps(s, v);
            CheckPairwise(s, v);
            CheckGreens(s, v);
            CheckDuplicates(s, v);
            CheckRoads(s, v);
            CheckAccess(s, v);
            CheckRequiredIds(s, v);
            CheckForbidden(s, v);
            CheckCounts(s, v);
            CheckAzs4Pawnshop(s, v);
            return v;
        }

        /// <summary>Число зданий группы (mkd-n, mkd-s, mkd-e, private) по данным снимка.</summary>
        public static int CountGroup(LayoutSnapshot s, string group) => s.Buildings.Count(b => b.Group == group);

        // ---------------------------------------------------------------- правила

        private static void CheckBounds(LayoutSnapshot s, List<string> v)
        {
            foreach (PlanRect r in Everything(s))
                if (r.Rect.MinX < -Eps || r.Rect.MinZ < -Eps || r.Rect.MaxX > MapSize + Eps || r.Rect.MaxZ > MapSize + Eps)
                    v.Add("вне карты: " + r.Name);
            foreach (PlanPoint p in s.Access)
                if (p.Position.X < 0f || p.Position.Z < 0f || p.Position.X > MapSize || p.Position.Z > MapSize)
                    v.Add("точка вне карты: " + p.Name);
        }

        private static void CheckStreetOverlaps(LayoutSnapshot s, List<string> v)
        {
            foreach (PlanRect r in s.Buildings.Concat(s.Open))
                foreach (PlanRect st in s.Streets)
                    if (Overlaps(r.Rect, st.Rect))
                        v.Add("перекрывает улицу или проезд: " + r.Name + " / " + st.Name);
        }

        private static void CheckPairwise(LayoutSnapshot s, List<string> v)
        {
            List<PlanRect> solids = s.Buildings.Concat(s.Open).ToList();
            for (int i = 0; i < solids.Count; i++)
                for (int j = i + 1; j < solids.Count; j++)
                {
                    PlanRect a = solids[i], b = solids[j];
                    if (a.Name == b.Name) continue; // одинаковые имена — это дубль, он проверяется отдельно
                    if (Overlaps(a.Rect, b.Rect))
                        v.Add("пересечение: " + a.Name + " и " + b.Name);
                }
        }

        private static void CheckGreens(LayoutSnapshot s, List<string> v)
        {
            foreach (PlanRect g in s.Greens)
                foreach (PlanRect b in s.Buildings)
                    if (Overlaps(g.Rect, b.Rect))
                        v.Add("газон на здании: " + g.Name + " / " + b.Name);
        }

        private static void CheckDuplicates(LayoutSnapshot s, List<string> v)
        {
            var names = new HashSet<string>();
            foreach (PlanRect r in s.Buildings.Concat(s.Open).Concat(s.Greens))
                if (!names.Add(r.Name))
                    v.Add("дублирующийся объект: " + r.Name);

            var keys = new HashSet<string>();
            foreach (string k in s.ObjectKeys)
                if (!keys.Add(k))
                    v.Add("дублирующийся объект (тип и позиция): " + k);
        }

        private static void CheckRoads(LayoutSnapshot s, List<string> v)
        {
            var ids = new HashSet<string>();
            var pairs = new HashSet<string>();
            foreach (PlanRoad r in s.Roads)
            {
                if (!ids.Add(r.Id))
                    v.Add("дублирующийся идентификатор дороги: " + r.Id);
                string key = string.CompareOrdinal(r.From, r.To) <= 0 ? r.From + "|" + r.To : r.To + "|" + r.From;
                if (!pairs.Add(key))
                    v.Add("дублирующаяся дорога: " + r.Id);
            }

            // Связность дорожного графа: объединение узлов по рёбрам.
            var parent = new Dictionary<string, string>();
            foreach (PlanRoad r in s.Roads)
            {
                Ensure(parent, r.From);
                Ensure(parent, r.To);
                string ra = Find(parent, r.From), rb = Find(parent, r.To);
                if (ra != rb) parent[ra] = rb;
            }
            int components = parent.Keys.Select(k => Find(parent, k)).Distinct().Count();
            if (components > 1)
                v.Add("дорожная сеть не связна: компонентов " + components);
        }

        private static void CheckAccess(LayoutSnapshot s, List<string> v)
        {
            int n = Cells;
            var blocked = new bool[n * n];
            foreach (PlanRect b in s.Blockers)
                MarkBlocked(blocked, b.Rect, n);

            var seen = new bool[n * n];
            var queue = new Queue<int>();
            for (int x = 0; x < n; x++)
            {
                Enqueue(x * n + 0, blocked, seen, queue);
                Enqueue(x * n + (n - 1), blocked, seen, queue);
            }
            for (int z = 0; z < n; z++)
            {
                Enqueue(0 * n + z, blocked, seen, queue);
                Enqueue((n - 1) * n + z, blocked, seen, queue);
            }

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                int x = idx / n, z = idx % n;
                if (x > 0) Enqueue(idx - n, blocked, seen, queue);
                if (x < n - 1) Enqueue(idx + n, blocked, seen, queue);
                if (z > 0) Enqueue(idx - 1, blocked, seen, queue);
                if (z < n - 1) Enqueue(idx + 1, blocked, seen, queue);
            }

            foreach (PlanPoint p in s.Access)
            {
                int x = (int)Math.Floor(p.Position.X), z = (int)Math.Floor(p.Position.Z);
                if (x < 0 || z < 0 || x >= n || z >= n || !seen[x * n + z] || IsPointBlocked(s, p.Position))
                    v.Add("недостижима точка (" + p.Kind + "): " + p.Name);
            }
        }

        private static bool IsPointBlocked(LayoutSnapshot s, Vec2 p)
        {
            foreach (PlanRect b in s.Blockers)
                if (p.X > b.Rect.MinX + Eps && p.X < b.Rect.MaxX - Eps &&
                    p.Z > b.Rect.MinZ + Eps && p.Z < b.Rect.MaxZ - Eps)
                    return true;
            return false;
        }

        private static void CheckRequiredIds(LayoutSnapshot s, List<string> v)
        {
            var ids = new HashSet<string>(s.Ids);
            foreach (string id in RequiredIds)
                if (!ids.Contains(id))
                    v.Add("нет обязательного объекта: " + id);
        }

        private static void CheckForbidden(LayoutSnapshot s, List<string> v)
        {
            foreach (string t in s.Types.Concat(s.Ids))
            {
                string low = t.ToLowerInvariant();
                foreach (string w in ForbiddenWords)
                    if (low.Contains(w))
                    {
                        v.Add("запрещённый объект: " + t);
                        break;
                    }
            }
        }

        private static void CheckCounts(LayoutSnapshot s, List<string> v)
        {
            int north = CountGroup(s, "mkd-n"), south = CountGroup(s, "mkd-s"), east = CountGroup(s, "mkd-e");
            int priv = CountGroup(s, "private");
            if (north != 4) v.Add("МКД на севере: " + north + " (нужно 4)");
            if (south != 3) v.Add("МКД на юге: " + south + " (нужно 3)");
            if (east != 3) v.Add("МКД на востоке: " + east + " (нужно 3)");
            if (north + south + east != 10) v.Add("МКД всего: " + (north + south + east) + " (нужно 10)");
            if (priv != 5) v.Add("частных домов: " + priv + " (нужно 5)");
        }

        private static void CheckAzs4Pawnshop(LayoutSnapshot s, List<string> v)
        {
            PlanRect pad = s.Open.FirstOrDefault(r => r.Name == "gas-04.pad");
            PlanRect pawn = s.Buildings.FirstOrDefault(r => r.Name == "com-pawnshop");
            if (pad == null || pawn == null)
            {
                v.Add("нет АЗС-4 или ломбарда");
                return;
            }
            float gap = pawn.Rect.MinZ - pad.Rect.MaxZ; // пад к югу от ломбарда: z ломбарда больше
            if (gap < 0f || gap > 8f)
                v.Add("АЗС-4 не непосредственно к югу от ломбарда (зазор " +
                      gap.ToString("0.0", CultureInfo.InvariantCulture) + " м)");
            if (!(pad.Rect.MinX < pawn.Rect.MaxX && pawn.Rect.MinX < pad.Rect.MaxX))
                v.Add("АЗС-4 не перекрывает ломбард по X");
        }

        // ---------------------------------------------------------------- помощники

        private static IEnumerable<PlanRect> Everything(LayoutSnapshot s) =>
            s.Buildings.Concat(s.Open).Concat(s.Greens).Concat(s.Streets).Concat(s.Blockers);

        private static bool Overlaps(Rect2 a, Rect2 b) =>
            a.MinX < b.MaxX - Eps && b.MinX < a.MaxX - Eps && a.MinZ < b.MaxZ - Eps && b.MinZ < a.MaxZ - Eps;

        private static bool Near(Vec2 a, Vec2 b) => Math.Abs(a.X - b.X) < Eps && Math.Abs(a.Z - b.Z) < Eps;

        private static string Q(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string GroupOfBuilding(BuildingInfo b)
        {
            if (b.Id.StartsWith("bld-n", StringComparison.Ordinal)) return "mkd-n";
            if (b.Id.StartsWith("bld-s", StringComparison.Ordinal)) return "mkd-s";
            if (b.Id.StartsWith("bld-e", StringComparison.Ordinal)) return "mkd-e";
            if (b.ArchetypeId.StartsWith("building.private.", StringComparison.Ordinal)) return "private";
            return "other";
        }

        /// <summary>Прямоугольник следа по центру, размеру и повороту (0/90 градусов).</summary>
        private static Rect2 Footprint(Vec2 c, float rotY, Vec3 size)
        {
            float n = rotY % 180f;
            if (n < 0f) n += 180f;
            bool swap = Math.Abs(n - 90f) < 1f;
            float hx = (swap ? size.Z : size.X) / 2f;
            float hz = (swap ? size.X : size.Z) / 2f;
            return new Rect2(c.X - hx, c.Z - hz, c.X + hx, c.Z + hz);
        }

        /// <summary>Полоса улицы: только поперечное расширение, концы не удлиняются.</summary>
        private static Rect2 StreetRect(Vec2 a, Vec2 b, float half)
        {
            if (Math.Abs(a.X - b.X) < Eps)
            {
                float minZ = Math.Min(a.Z, b.Z), maxZ = Math.Max(a.Z, b.Z);
                return new Rect2(a.X - half, minZ, a.X + half, maxZ);
            }
            float minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X);
            return new Rect2(minX, a.Z - half, maxX, a.Z + half);
        }

        private static void MarkBlocked(bool[] blocked, Rect2 r, int n)
        {
            int x0 = Math.Max(0, (int)Math.Floor(r.MinX));
            int x1 = Math.Min(n - 1, (int)Math.Ceiling(r.MaxX) - 1);
            int z0 = Math.Max(0, (int)Math.Floor(r.MinZ));
            int z1 = Math.Min(n - 1, (int)Math.Ceiling(r.MaxZ) - 1);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    float cx = x + 0.5f, cz = z + 0.5f;
                    if (cx > r.MinX + Eps && cx < r.MaxX - Eps && cz > r.MinZ + Eps && cz < r.MaxZ - Eps)
                        blocked[x * n + z] = true;
                }
        }

        private static void Enqueue(int idx, bool[] blocked, bool[] seen, Queue<int> queue)
        {
            if (blocked[idx] || seen[idx]) return;
            seen[idx] = true;
            queue.Enqueue(idx);
        }

        private static void Ensure(Dictionary<string, string> parent, string x)
        {
            if (!parent.ContainsKey(x)) parent[x] = x;
        }

        private static string Find(Dictionary<string, string> parent, string x)
        {
            string root = x;
            while (parent[root] != root) root = parent[root];
            return root;
        }
    }
}
