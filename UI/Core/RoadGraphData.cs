// Life From Zero — UI/Core (ТЗ №042 §7)
// Namespace: LifeFromZero.UI.Core
//
// Дорожный граф навигации. Строится из РЕАЛЬНОЙ дорожной сети District750
// (QuarterPlan.Roads + QuarterPlan.Nodes), а не из произвольной сетки.
//
// Модуль не зависит от UnityEngine и от City.Quarter: данные подаются массивами
// простых структур, чтобы граф можно было прогнать тестами вне Unity.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace LifeFromZero.UI.Core
{
    /// <summary>Тип проходимости ребра.</summary>
    public enum RouteEdgeKind
    {
        /// <summary>Городская улица (есть тротуары).</summary>
        Street = 0,

        /// <summary>Дворовый проезд (уже улицы, без разметки).</summary>
        Driveway = 1,

        /// <summary>Подход к зданию/точке от узла дороги (последние метры пешком).</summary>
        Approach = 2,
    }

    /// <summary>Узел графа: перекрёсток, примыкание проезда или точка подхода.</summary>
    public readonly struct RouteNode
    {
        public RouteNode(int id, MapPoint position, bool isIntersection)
        {
            Id = id;
            Position = position;
            IsIntersection = isIntersection;
        }

        public int Id { get; }

        public MapPoint Position { get; }

        public bool IsIntersection { get; }
    }

    /// <summary>Ребро графа: соединяет два узла, знает длину и тип проходимости.</summary>
    public readonly struct RouteEdge
    {
        public RouteEdge(int from, int to, float length, RouteEdgeKind kind)
        {
            From = from;
            To = to;
            Length = length;
            Kind = kind;
        }

        public int From { get; }

        public int To { get; }

        public float Length { get; }

        public RouteEdgeKind Kind { get; }
    }

    /// <summary>
    /// Дорожный граф района. Узлы индексируются целыми, поэтому поиск пути
    /// не трогает строковые ключи и словари на каждом шаге.
    /// </summary>
    public sealed class RoadGraphData
    {
        private readonly List<RouteNode> _nodes = new List<RouteNode>();
        private readonly List<List<RouteEdge>> _adjacency = new List<List<RouteEdge>>();
        private readonly List<RouteEdge> _edges = new List<RouteEdge>();

        public IReadOnlyList<RouteNode> Nodes => _nodes;

        public IReadOnlyList<RouteEdge> Edges => _edges;

        public int NodeCount => _nodes.Count;

        public int EdgeCount => _edges.Count;

        /// <summary>Добавляет узел и возвращает его индекс.</summary>
        public int AddNode(MapPoint position, bool isIntersection)
        {
            int id = _nodes.Count;
            _nodes.Add(new RouteNode(id, position, isIntersection));
            _adjacency.Add(new List<RouteEdge>(4));
            return id;
        }

        /// <summary>Добавляет двустороннее ребро. Повторное ребро игнорируется.</summary>
        public bool AddEdge(int from, int to, RouteEdgeKind kind)
        {
            if (from == to) return false;
            if (from < 0 || to < 0 || from >= _nodes.Count || to >= _nodes.Count) return false;
            List<RouteEdge> a = _adjacency[from];
            for (int i = 0; i < a.Count; i++)
                if (a[i].To == to) return false;

            float length = _nodes[from].Position.DistanceTo(_nodes[to].Position);
            var edge = new RouteEdge(from, to, length, kind);
            var reversed = new RouteEdge(to, from, length, kind);
            _adjacency[from].Add(edge);
            _adjacency[to].Add(reversed);
            _edges.Add(edge);
            return true;
        }

        public IReadOnlyList<RouteEdge> EdgesFrom(int node)
        {
            if (node < 0 || node >= _adjacency.Count) return Array.Empty<RouteEdge>();
            return _adjacency[node];
        }

        public MapPoint NodePosition(int node) => _nodes[node].Position;

        /// <summary>
        /// Ближайший узел графа к точке. Ищет только среди узлов, доступных
        /// от основной компоненты связности, чтобы не «припарковаться» на отрезанном куске.
        /// </summary>
        public int FindNearestNode(MapPoint point, float maxDistance, IReadOnlyList<int> allowedNodes = null)
        {
            int best = -1;
            float bestDistance = maxDistance;
            if (allowedNodes == null)
            {
                for (int i = 0; i < _nodes.Count; i++)
                {
                    float d = _nodes[i].Position.DistanceTo(point);
                    if (d <= bestDistance)
                    {
                        bestDistance = d;
                        best = i;
                    }
                }
                return best;
            }

            for (int k = 0; k < allowedNodes.Count; k++)
            {
                int i = allowedNodes[k];
                float d = _nodes[i].Position.DistanceTo(point);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Компонента связности, содержащая узел (обход в ширину по рёбрам).</summary>
        public List<int> ComponentOf(int node)
        {
            var result = new List<int>();
            if (node < 0 || node >= _nodes.Count) return result;
            var seen = new bool[_nodes.Count];
            var queue = new Queue<int>();
            queue.Enqueue(node);
            seen[node] = true;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                result.Add(current);
                List<RouteEdge> edges = _adjacency[current];
                for (int i = 0; i < edges.Count; i++)
                {
                    int next = edges[i].To;
                    if (!seen[next])
                    {
                        seen[next] = true;
                        queue.Enqueue(next);
                    }
                }
            }
            return result;
        }

        /// <summary>Наибольшая компонента связности — по ней и ходим.</summary>
        public List<int> LargestComponent()
        {
            var seen = new bool[_nodes.Count];
            List<int> best = new List<int>();
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (seen[i]) continue;
                List<int> component = ComponentOf(i);
                for (int k = 0; k < component.Count; k++) seen[component[k]] = true;
                if (component.Count > best.Count) best = component;
            }
            return best;
        }

        public bool IsConnected => _nodes.Count == 0 || LargestComponent().Count == _nodes.Count;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "RoadGraph(nodes={0}, edges={1})", _nodes.Count, _edges.Count);
    }
}
