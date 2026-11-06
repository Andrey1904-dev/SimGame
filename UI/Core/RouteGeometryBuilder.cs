// Life From Zero — UI/Core (ТЗ №042 §7.2)
// Namespace: LifeFromZero.UI.Core
//
// Построение дорожного графа навигации из РЕАЛЬНОЙ сети District750.
//
// Источник — QuarterPlan (City.Quarter): массив узлов (RoadNode) и сегментов
// (RoadSegment) генератора. Ничего не выдумывается: если узел или сегмент
// отсутствует в данных, он не появляется и в графе.
//
// Повторяется поведение RoadGraph.BuildAdjacency из ядра города: узел, лежащий
// НА чужом сегменте (примыкание проезда к улице), тоже связывает концы сегмента.
// Без этого проезды к АЗС и складам оказались бы отрезанными от улиц.

using System;
using System.Collections.Generic;

namespace LifeFromZero.UI.Core
{
    /// <summary>Узел сети в терминах данных генератора (без зависимости от City.Quarter).</summary>
    public readonly struct NetworkNode
    {
        public NetworkNode(string id, MapPoint position, bool isIntersection)
        {
            Id = id;
            Position = position;
            IsIntersection = isIntersection;
        }

        public string Id { get; }

        public MapPoint Position { get; }

        public bool IsIntersection { get; }
    }

    /// <summary>Сегмент сети. Координаты концов берутся из узлов, а не дублируются.</summary>
    public readonly struct NetworkSegment
    {
        public NetworkSegment(string startNodeId, string endNodeId, RouteEdgeKind kind)
        {
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
            Kind = kind;
        }

        public string StartNodeId { get; }

        public string EndNodeId { get; }

        public RouteEdgeKind Kind { get; }
    }

    /// <summary>Строитель графа из данных генератора района.</summary>
    public static class RouteGeometryBuilder
    {
        /// <summary>Допуск «узел лежит на сегменте», метры (как в ядре города).</summary>
        public const float OnSegmentEpsilon = 0.01f;

        /// <summary>
        /// Строит граф. Возвращает пустой граф, если данных нет — вызывающая сторона
        /// обязана показать «сеть не загружена», а не рисовать фиктивный маршрут.
        /// </summary>
        public static RoadGraphData Build(IReadOnlyList<NetworkNode> nodes, IReadOnlyList<NetworkSegment> segments)
        {
            var graph = new RoadGraphData();
            if (nodes == null || nodes.Count == 0) return graph;

            var indexById = new Dictionary<string, int>(nodes.Count, StringComparer.Ordinal);
            var index = new int[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                NetworkNode node = nodes[i];
                index[i] = graph.AddNode(node.Position, node.IsIntersection);
                if (!string.IsNullOrEmpty(node.Id) && !indexById.ContainsKey(node.Id))
                    indexById[node.Id] = index[i];
            }

            if (segments == null) return graph;

            for (int s = 0; s < segments.Count; s++)
            {
                NetworkSegment segment = segments[s];
                int a, b;
                if (string.IsNullOrEmpty(segment.StartNodeId) ||
                    string.IsNullOrEmpty(segment.EndNodeId) ||
                    !indexById.TryGetValue(segment.StartNodeId, out a) ||
                    !indexById.TryGetValue(segment.EndNodeId, out b))
                {
                    // Сегмент ссылается на несуществующий узел: данные битые, сегмент пропускаем.
                    continue;
                }

                graph.AddEdge(a, b, segment.Kind);

                // Примыкания: узлы, лежащие на этом сегменте, связаны с его концами.
                MapPoint start = graph.NodePosition(a);
                MapPoint end = graph.NodePosition(b);
                for (int k = 0; k < nodes.Count; k++)
                {
                    int n = index[k];
                    if (n == a || n == b) continue;
                    if (!IsPointOnSegment(graph.NodePosition(n), start, end)) continue;
                    graph.AddEdge(n, a, segment.Kind);
                    graph.AddEdge(n, b, segment.Kind);
                }
            }

            return graph;
        }

        /// <summary>Осевой сегмент содержит точку (генератор строит только осевые сегменты).</summary>
        public static bool IsPointOnSegment(MapPoint p, MapPoint a, MapPoint b)
        {
            const float eps = OnSegmentEpsilon;
            if (Math.Abs(a.X - b.X) < eps)
            {
                return Math.Abs(p.X - a.X) < eps &&
                       p.Z >= Math.Min(a.Z, b.Z) - eps &&
                       p.Z <= Math.Max(a.Z, b.Z) + eps;
            }
            if (Math.Abs(a.Z - b.Z) < eps)
            {
                return Math.Abs(p.Z - a.Z) < eps &&
                       p.X >= Math.Min(a.X, b.X) - eps &&
                       p.X <= Math.Max(a.X, b.X) + eps;
            }
            return false;
        }
    }
}
