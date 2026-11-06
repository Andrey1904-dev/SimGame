// Life From Zero — UI/Core (ТЗ №042 §7)
// Namespace: LifeFromZero.UI.Core
//
// A* по дорожному графу District750 и расчёт навигационных инструкций.
//
// Что гарантируется:
//   * путь строится только по существующим рёбрам дорожной сети (никаких прямых
//     линий через здания);
//   * расстояние считается по длине рёбер, а не по прямой;
//   * недоступная цель возвращает понятную причину, а не «пустой маршрут»;
//   * инструкции (ближайший поворот, расстояние до него) выводятся из геометрии
//     пути и текущего положения — без заранее прописанных фраз.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace LifeFromZero.UI.Core
{
    /// <summary>Почему маршрут не построен.</summary>
    public enum RouteFailure
    {
        None = 0,
        NoGraph = 1,
        StartNotConnected = 2,
        DestinationNotConnected = 3,
        NoPath = 4,
        InvalidPoint = 5,
    }

    /// <summary>Точка готового маршрута.</summary>
    public readonly struct RouteStep
    {
        public RouteStep(MapPoint position, RouteEdgeKind kind, bool isTurn, float cumulativeDistance)
        {
            Position = position;
            Kind = kind;
            IsTurn = isTurn;
            CumulativeDistance = cumulativeDistance;
        }

        public MapPoint Position { get; }

        /// <summary>Тип ребра, по которому пришли в эту точку.</summary>
        public RouteEdgeKind Kind { get; }

        /// <summary>Здесь меняется направление (поворот/примыкание).</summary>
        public bool IsTurn { get; }

        /// <summary>Накопленное расстояние от начала маршрута, метры.</summary>
        public float CumulativeDistance { get; }
    }

    /// <summary>Готовый маршрут. Карта и HUD обязаны читать ОДИН такой объект.</summary>
    public sealed class RoutePath
    {
        private readonly List<RouteStep> _steps;

        public RoutePath(
            string destinationId,
            string destinationLabel,
            MapPoint destination,
            RouteStep[] steps,
            float totalDistance,
            MapPoint snapStart,
            MapPoint snapEnd,
            bool endsOnApproach)
        {
            DestinationId = destinationId ?? string.Empty;
            DestinationLabel = destinationLabel ?? string.Empty;
            Destination = destination;
            _steps = new List<RouteStep>(steps ?? Array.Empty<RouteStep>());
            TotalDistance = totalDistance;
            SnapStart = snapStart;
            SnapEnd = snapEnd;
            EndsOnApproach = endsOnApproach;
        }

        public string DestinationId { get; }

        public string DestinationLabel { get; }

        /// <summary>Исходная (не «прилипшая») точка назначения.</summary>
        public MapPoint Destination { get; }

        /// <summary>Последовательность мировых точек маршрута.</summary>
        public IReadOnlyList<RouteStep> Steps => _steps;

        /// <summary>Длина маршрута по дорогам, метры.</summary>
        public float TotalDistance { get; }

        /// <summary>Ближайшая точка дороги к игроку, откуда начался маршрут.</summary>
        public MapPoint SnapStart { get; }

        /// <summary>Ближайшая точка дороги к цели, где маршрут заканчивается.</summary>
        public MapPoint SnapEnd { get; }

        /// <summary>Последний отрезок — подход к цели пешком от дороги.</summary>
        public bool EndsOnApproach { get; }

        public int Count => _steps.Count;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "Route({0}, {1:0} м, точек {2})",
                DestinationLabel, TotalDistance, _steps.Count);
    }

    /// <summary>Результат построения маршрута.</summary>
    public readonly struct RouteResult
    {
        private RouteResult(RoutePath path, RouteFailure failure, string message)
        {
            Path = path;
            Failure = failure;
            Message = message ?? string.Empty;
        }

        public RoutePath Path { get; }

        public RouteFailure Failure { get; }

        public string Message { get; }

        public bool Success => Path != null && Failure == RouteFailure.None;

        public static RouteResult Ok(RoutePath path) => new RouteResult(path, RouteFailure.None, string.Empty);

        public static RouteResult Fail(RouteFailure failure, string message) =>
            new RouteResult(null, failure, message);
    }

    /// <summary>Навигационная инструкция для HUD.</summary>
    public readonly struct NavigationCue
    {
        public NavigationCue(string label, float distanceToManeuver, float remainingDistance, float bearingDegrees)
        {
            Label = label ?? string.Empty;
            DistanceToManeuver = distanceToManeuver;
            RemainingDistance = remainingDistance;
            BearingDegrees = bearingDegrees;
        }

        /// <summary>«Направо», «Налево», «Прямо», «Вы прибыли».</summary>
        public string Label { get; }

        /// <summary>До ближайшего поворота, метры.</summary>
        public float DistanceToManeuver { get; }

        /// <summary>Осталось до цели, метры.</summary>
        public float RemainingDistance { get; }

        /// <summary>Направление на ближайшую точку маршрута, градусы (0 — север, по часовой).</summary>
        public float BearingDegrees { get; }

        public bool Arrived => RemainingDistance <= 0.01f;
    }

    /// <summary>
    /// Поиск пути A*. Экземпляр можно держать один: внутри только временные буферы,
    /// повторный поиск переиспользует их и не пересчитывает путь каждый кадр сам по себе.
    /// </summary>
    public sealed class RoutePlanner
    {
        /// <summary>Максимальное расстояние «прилипания» игрока к дороге, метры.</summary>
        public float StartSnapRadius { get; set; } = 45f;

        /// <summary>
        /// Допуск привязки к РЕБРУ дороги (а не к перекрёстку), метры.
        /// Подъезд/точка доставки стоят во дворе: до узла бывает 70–130 м,
        /// до ближайшей дороги — единицы метров. Без этого маршрут к заказу
        /// не строился бы у большинства адресов (видно по тесту района).
        /// </summary>
        public float EdgeSnapRadius { get; set; } = 45f;

        /// <summary>
        /// Максимальное расстояние «прилипания» цели к дороге, метры.
        ///
        /// Здесь допуск больше, чем у игрока: точка доставки — это подъезд внутри
        /// двора, а дороги идут по периметру квартала. По замеру District750 у 17 из
        /// 46 точек расстояние до ближайшей дороги 47–75 м, то есть при допуске 60 м
        /// маршрут к каждому третьему заказу не строился бы вовсе. Последний участок
        /// идёт как подход пешком (<see cref="RoutePath.EndsOnApproach"/>), поэтому
        /// 90 м — это «дойти через двор», а не «телепорт».
        /// </summary>
        public float DestinationSnapRadius { get; set; } = 90f;

        /// <summary>Насколько точка считается достигнутой, метры.</summary>
        public float ArriveRadius { get; set; } = 2.5f;

        /// <summary>Штраф за дворовый проезд при равной длине (улицы предпочтительнее).</summary>
        public float DrivewayCostMultiplier { get; set; } = 1.06f;

        public RouteResult Build(
            RoadGraphData graph,
            MapPoint start,
            MapPoint destination,
            string destinationId,
            string destinationLabel)
        {
            if (graph == null || graph.NodeCount == 0)
                return RouteResult.Fail(RouteFailure.NoGraph, "Дорожная сеть района не загружена.");

            if (!IsFinite(start) || !IsFinite(destination))
                return RouteResult.Fail(RouteFailure.InvalidPoint, "Координаты точки недоступны.");

            List<int> network = graph.LargestComponent();
            if (network.Count == 0)
                return RouteResult.Fail(RouteFailure.NoGraph, "Дорожная сеть пуста.");

            // Привязка к сети: сначала ищем ребро (любую точку дороги), затем узел.
            int startNode;
            int endNode;
            MapPoint startSnap;
            MapPoint endSnap;
            RouteFailure snapFailure;
            float startRadius = Math.Max(StartSnapRadius, EdgeSnapRadius);
            if (!SnapToNetwork(graph, start, startRadius, network, out startNode, out startSnap, out snapFailure))
                return RouteResult.Fail(snapFailure, "Нет дороги рядом с игроком — маршрут не построен.");

            if (!SnapToNetwork(graph, destination, DestinationSnapRadius, network, out endNode, out endSnap,
                    out snapFailure))
                return RouteResult.Fail(RouteFailure.DestinationNotConnected,
                    "Цель не связана с дорожной сетью.");

            List<int> nodes = AStar(graph, startNode, endNode);
            if (nodes == null || nodes.Count == 0)
                return RouteResult.Fail(RouteFailure.NoPath,
                    "Маршрут по дорогам не найден: цель недостижима от игрока.");

            // Собираем точки: проекция старта на дорогу, узлы пути, проекция цели.
            MapPoint snapStart = startSnap;
            MapPoint snapEnd = endSnap;
            var steps = new List<RouteStep>(nodes.Count + 2);
            float cumulative = 0f;
            MapPoint previous = snapStart;
            steps.Add(new RouteStep(snapStart, graph.EdgesFrom(nodes[0]).Count > 0
                ? graph.EdgesFrom(nodes[0])[0].Kind
                : RouteEdgeKind.Street, false, 0f));

            for (int i = 1; i < nodes.Count; i++)
            {
                MapPoint position = graph.NodePosition(nodes[i]);
                float segment = previous.DistanceTo(position);
                cumulative += segment;

                RouteEdgeKind kind = RouteEdgeKind.Street;
                IReadOnlyList<RouteEdge> edges = graph.EdgesFrom(nodes[i - 1]);
                for (int e = 0; e < edges.Count; e++)
                {
                    if (edges[e].To == nodes[i])
                    {
                        kind = edges[e].Kind;
                        break;
                    }
                }

                // Поворот, если направление изменилось заметно (не «прямо»).
                bool turn = false;
                if (steps.Count >= 2)
                {
                    MapPoint a = steps[steps.Count - 2].Position;
                    MapPoint b = previous;
                    MapPoint c = position;
                    float angle = TurnAngle(a, b, c);
                    turn = Math.Abs(angle) >= 25f;
                }

                steps.Add(new RouteStep(position, kind, turn, cumulative));
                previous = position;
            }

            bool endsOnApproach = snapEnd.DistanceTo(destination) > ArriveRadius;
            if (endsOnApproach)
            {
                cumulative += snapEnd.DistanceTo(destination);
                steps.Add(new RouteStep(destination, RouteEdgeKind.Approach, true, cumulative));
            }

            var path = new RoutePath(destinationId, destinationLabel, destination, steps.ToArray(),
                cumulative, snapStart, snapEnd, endsOnApproach);
            return RouteResult.Ok(path);
        }

        /// <summary>
        /// Привязать точку к дорожной сети: ближайшее ребро в допуске, иначе ближайший узел.
        /// Возвращает индекс входного узла и точку проекции на дорогу.
        /// </summary>
        private bool SnapToNetwork(RoadGraphData graph, MapPoint point, float radius,
            IReadOnlyList<int> network, out int node, out MapPoint snap, out RouteFailure failure)
        {
            failure = RouteFailure.None;
            node = -1;
            snap = point;

            MapPoint projection;
            float distance;
            int edgeIndex = graph.FindNearestEdge(point, radius, out projection, out distance, network);
            if (edgeIndex >= 0)
            {
                RouteEdge edge = graph.Edges[edgeIndex];
                int fromDistance = graph.NodePosition(edge.From).DistanceTo(projection) <=
                                   graph.NodePosition(edge.To).DistanceTo(projection)
                    ? edge.From
                    : edge.To;
                node = fromDistance;
                snap = projection;
                return true;
            }

            node = graph.FindNearestNode(point, radius, network);
            if (node < 0)
            {
                failure = RouteFailure.StartNotConnected;
                return false;
            }

            snap = graph.NodePosition(node);
            return true;
        }

        private static bool IsFinite(MapPoint p) =>
            !float.IsNaN(p.X) && !float.IsInfinity(p.X) && !float.IsNaN(p.Z) && !float.IsInfinity(p.Z);

        /// <summary>A* по дорожному графу. Возвращает последовательность узлов или null.</summary>
        internal static List<int> AStar(RoadGraphData graph, int start, int goal)
        {
            if (start == goal) return new List<int> { start };

            int count = graph.NodeCount;
            var gScore = new float[count];
            var fScore = new float[count];
            var cameFrom = new int[count];
            var closed = new bool[count];
            var inOpen = new bool[count];
            for (int i = 0; i < count; i++)
            {
                gScore[i] = float.PositiveInfinity;
                fScore[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
            }

            MapPoint goalPosition = graph.NodePosition(goal);
            gScore[start] = 0f;
            fScore[start] = graph.NodePosition(start).DistanceTo(goalPosition);
            var open = new MinHeap();
            open.Push(start, fScore[start]);
            inOpen[start] = true;

            while (open.Count > 0)
            {
                int current = open.Pop();
                if (current == goal) return Reconstruct(cameFrom, goal);
                if (closed[current]) continue;
                closed[current] = true;
                inOpen[current] = false;

                IReadOnlyList<RouteEdge> edges = graph.EdgesFrom(current);
                for (int i = 0; i < edges.Count; i++)
                {
                    RouteEdge edge = edges[i];
                    if (closed[edge.To]) continue;
                    float cost = edge.Length * (edge.Kind == RouteEdgeKind.Driveway ? 1.06f : 1f);
                    float tentative = gScore[current] + cost;
                    if (tentative >= gScore[edge.To]) continue;

                    cameFrom[edge.To] = current;
                    gScore[edge.To] = tentative;
                    fScore[edge.To] = tentative + graph.NodePosition(edge.To).DistanceTo(goalPosition);
                    if (!inOpen[edge.To])
                    {
                        inOpen[edge.To] = true;
                        open.Push(edge.To, fScore[edge.To]);
                    }
                    else
                    {
                        open.DecreaseKey(edge.To, fScore[edge.To]);
                    }
                }
            }
            return null;
        }

        private static List<int> Reconstruct(int[] cameFrom, int goal)
        {
            var path = new List<int>();
            int node = goal;
            while (node >= 0)
            {
                path.Add(node);
                node = cameFrom[node];
            }
            path.Reverse();
            return path;
        }

        /// <summary>Угол поворота в точке b: &gt;0 — направо, &lt;0 — налево, градусы.</summary>
        public static float TurnAngle(MapPoint a, MapPoint b, MapPoint c)
        {
            float inX = b.X - a.X, inZ = b.Z - a.Z;
            float outX = c.X - b.X, outZ = c.Z - b.Z;
            float inLen = (float)Math.Sqrt(inX * inX + inZ * inZ);
            float outLen = (float)Math.Sqrt(outX * outX + outZ * outZ);
            if (inLen < 1e-4f || outLen < 1e-4f) return 0f;
            inX /= inLen; inZ /= inLen;
            outX /= outLen; outZ /= outLen;
            float dot = Math.Max(-1f, Math.Min(1f, inX * outX + inZ * outZ));
            float angle = (float)(Math.Acos(dot) * 180.0 / Math.PI);
            // В системе (X — восток, Z — север) поворот НАПРАВО даёт отрицательное
            // векторное произведение: (0,1) -> (1,0) = -1.
            float cross = inX * outZ - inZ * outX;
            return cross <= 0f ? angle : -angle;
        }

        /// <summary>Минимальная двоичная куча по приоритету.</summary>
        private sealed class MinHeap
        {
            private readonly List<int> _items = new List<int>(64);
            private readonly List<float> _keys = new List<float>(64);
            private readonly Dictionary<int, int> _index = new Dictionary<int, int>(64);

            public int Count => _items.Count;

            public void Push(int item, float key)
            {
                _items.Add(item);
                _keys.Add(key);
                _index[item] = _items.Count - 1;
                SiftUp(_items.Count - 1);
            }

            public void DecreaseKey(int item, float key)
            {
                int i;
                if (!_index.TryGetValue(item, out i)) return;
                if (key < _keys[i])
                {
                    _keys[i] = key;
                    SiftUp(i);
                }
            }

            public int Pop()
            {
                int top = _items[0];
                int last = _items.Count - 1;
                Swap(0, last);
                _items.RemoveAt(last);
                _keys.RemoveAt(last);
                _index.Remove(top);
                if (_items.Count > 0) SiftDown(0);
                return top;
            }

            private void SiftUp(int i)
            {
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_keys[parent] <= _keys[i]) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            private void SiftDown(int i)
            {
                while (true)
                {
                    int left = i * 2 + 1, right = left + 1, smallest = i;
                    if (left < _items.Count && _keys[left] < _keys[smallest]) smallest = left;
                    if (right < _items.Count && _keys[right] < _keys[smallest]) smallest = right;
                    if (smallest == i) return;
                    Swap(i, smallest);
                    i = smallest;
                }
            }

            private void Swap(int a, int b)
            {
                int ti = _items[a]; _items[a] = _items[b]; _items[b] = ti;
                float tk = _keys[a]; _keys[a] = _keys[b]; _keys[b] = tk;
                _index[_items[a]] = a;
                _index[_items[b]] = b;
            }
        }
    }

    /// <summary>
    /// Навигационные указания и проверка отклонения от маршрута.
    /// Работает поверх ОДНОГО объекта <see cref="RoutePath"/> — того же, что рисует карта.
    /// </summary>
    public static class RouteNavigator
    {
        /// <summary>Порог, после которого маршрут перестраивается, метры.</summary>
        public const float OffRouteThreshold = 18f;

        /// <summary>Индекс ближайшей точки пути и расстояние до неё.</summary>
        public static int FindClosestStep(RoutePath path, MapPoint position, out float distance)
        {
            distance = float.PositiveInfinity;
            if (path == null || path.Count == 0) return -1;

            IReadOnlyList<RouteStep> steps = path.Steps;
            int best = 0;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < steps.Count; i++)
            {
                float d = steps[i].Position.DistanceTo(position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            distance = bestDistance;
            return best;
        }

        /// <summary>Игрок существенно отклонился от маршрута.</summary>
        public static bool ShouldRebuild(RoutePath path, MapPoint position, float threshold = OffRouteThreshold)
        {
            if (path == null) return false;
            float distance;
            FindClosestStep(path, position, out distance);
            return distance > threshold;
        }

        /// <summary>Маршрут пора завершать: игрок в радиусе прибытия.</summary>
        public static bool IsArrived(RoutePath path, MapPoint position, float radius = 3f)
        {
            if (path == null) return false;
            return path.Destination.DistanceTo(position) <= radius;
        }

        /// <summary>Оставшееся расстояние от ближайшей точки пути до конца маршрута.</summary>
        public static float RemainingDistance(RoutePath path, MapPoint position)
        {
            if (path == null || path.Count == 0) return 0f;
            float distance;
            int index = FindClosestStep(path, position, out distance);
            if (index < 0) return 0f;
            float fromStep = path.TotalDistance - path.Steps[index].CumulativeDistance;
            return Math.Max(0f, fromStep + distance);
        }

        /// <summary>Текущая инструкция: ближайший поворот, расстояние до него, остаток.</summary>
        public static NavigationCue Cue(RoutePath path, MapPoint position, float headingDegrees)
        {
            if (path == null || path.Count == 0)
                return new NavigationCue("Нет маршрута", 0f, 0f, headingDegrees);

            float distance;
            int index = FindClosestStep(path, position, out distance);
            float remaining = RemainingDistance(path, position);

            if (IsArrived(path, position, 3f))
                return new NavigationCue("Вы прибыли", 0f, 0f, headingDegrees);

            // Ищем следующий поворот после текущей точки.
            int turnIndex = -1;
            for (int i = index + 1; i < path.Count; i++)
            {
                if (path.Steps[i].IsTurn)
                {
                    turnIndex = i;
                    break;
                }
            }

            MapPoint lookAhead = turnIndex >= 0
                ? path.Steps[turnIndex].Position
                : path.Steps[path.Count - 1].Position;

            float toManeuver = distance + Math.Max(0f,
                (turnIndex >= 0 ? path.Steps[turnIndex].CumulativeDistance : path.TotalDistance) -
                path.Steps[index].CumulativeDistance);

            MapPoint from = position;
            MapPoint to = lookAhead.DistanceTo(position) < 1f && index + 1 < path.Count
                ? path.Steps[index + 1].Position
                : lookAhead;
            float bearing = Bearing(from, to);

            string label;
            if (turnIndex < 0)
            {
                label = remaining <= 12f ? "Цель рядом" : "Прямо к цели";
            }
            else
            {
                MapPoint a = index > 0 ? path.Steps[index - 1].Position : position;
                MapPoint b = path.Steps[turnIndex - 1 >= index ? turnIndex - 1 : index].Position;
                MapPoint c = path.Steps[turnIndex].Position;
                float angle = RoutePlanner.TurnAngle(a, b, c);
                label = TurnLabel(angle);
            }

            return new NavigationCue(label, toManeuver, remaining, bearing);
        }

        public static string TurnLabel(float angleDegrees)
        {
            float abs = Math.Abs(angleDegrees);
            if (abs < 22f) return "Прямо";
            if (abs > 150f) return "Разворот";
            if (abs > 100f) return angleDegrees > 0f ? "Резко направо" : "Резко налево";
            return angleDegrees > 0f ? "Направо" : "Налево";
        }

        /// <summary>Азимут от a к b: 0 — север (Z+), по часовой стрелке, градусы.</summary>
        public static float Bearing(MapPoint a, MapPoint b)
        {
            float dx = b.X - a.X;
            float dz = b.Z - a.Z;
            if (Math.Abs(dx) < 1e-5f && Math.Abs(dz) < 1e-5f) return 0f;
            double deg = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            if (deg < 0) deg += 360.0;
            return (float)deg;
        }

        /// <summary>Форматирование расстояния для HUD: «850 м» / «1,2 км».</summary>
        public static string FormatDistance(float meters)
        {
            if (float.IsNaN(meters) || float.IsInfinity(meters)) return "—";
            if (meters < 1000f) return ((int)Math.Round(meters, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + " м";
            double km = meters / 1000.0;
            return km.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " км";
        }
    }
}
