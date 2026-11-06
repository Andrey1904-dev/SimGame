// Life From Zero — UI/Core (ТЗ №042)
// Namespace: LifeFromZero.UI.Core
//
// Мост «план района → данные интерфейса» БЕЗ UnityEngine.
//
// Здесь живут две вещи, которые раньше пришлось бы писать в рантайме поверх
// Unity-типов, а значит нельзя было бы проверить тестом:
//   1. извлечение геометрии карты из QuarterPlan (дороги, точки интереса);
//   2. построение дорожного графа маршрутизации из узлов/сегментов плана.
//
// Сегменты плана осевые (генератор строит только горизонталь/вертикаль),
// поэтому примыкания восстанавливаются RouteGeometryBuilder'ом по координатам.
//
// Рантайм (UI/Runtime/QuarterMapGeometry.cs) только переводит MapPoint → Vector2.

using System;
using System.Collections.Generic;
using LifeFromZero.City.Quarter;

namespace LifeFromZero.UI.Core
{
    /// <summary>Один отрезок дороги для отрисовки карты.</summary>
    public readonly struct MapRoadSegment
    {
        public MapRoadSegment(MapPoint start, MapPoint end, bool isDriveway)
        {
            Start = start;
            End = end;
            IsDriveway = isDriveway;
        }

        public MapPoint Start { get; }

        public MapPoint End { get; }

        /// <summary>Дворовый проезд (рисуется тоньше).</summary>
        public bool IsDriveway { get; }
    }

    /// <summary>Точка интереса на карте.</summary>
    public readonly struct MapPoiMarker
    {
        public MapPoiMarker(MapPoint position, float radiusMeters, string label)
        {
            Position = position;
            RadiusMeters = radiusMeters;
            Label = label ?? string.Empty;
        }

        public MapPoint Position { get; }

        public float RadiusMeters { get; }

        public string Label { get; }
    }

    /// <summary>Готовые данные карты одного района.</summary>
    public sealed class QuarterPlanMapData
    {
        private static readonly MapRoadSegment[] NoRoads = Array.Empty<MapRoadSegment>();
        private static readonly MapPoiMarker[] NoPoints = Array.Empty<MapPoiMarker>();

        public QuarterPlanMapData(float size, IReadOnlyList<MapRoadSegment> roads, IReadOnlyList<MapPoiMarker> points,
            RoadGraphData graph)
        {
            Size = size;
            Roads = roads ?? NoRoads;
            Points = points ?? NoPoints;
            Graph = graph;
        }

        /// <summary>Сторона района в метрах.</summary>
        public float Size { get; }

        public IReadOnlyList<MapRoadSegment> Roads { get; }

        public IReadOnlyList<MapPoiMarker> Points { get; }

        /// <summary>Граф маршрутизации (может быть пустым — тогда маршрут недоступен).</summary>
        public RoadGraphData Graph { get; }

        public bool HasRoads => Roads.Count > 0;

        public bool HasGraph => Graph != null && Graph.NodeCount > 0;

        /// <summary>Пустые данные: карта покажет только сетку.</summary>
        public static QuarterPlanMapData Empty { get; } =
            new QuarterPlanMapData(0f, NoRoads, NoPoints, new RoadGraphData());
    }

    /// <summary>Преобразование QuarterPlan в данные интерфейса.</summary>
    public static class QuarterPlanAdapter
    {
        /// <summary>Радиус маркера района и точки доставки, метры.</summary>
        public const float DistrictPointRadius = 9f;

        public const float DeliveryPointRadius = 5f;

        /// <summary>Разобрать план в дороги/точки/граф.</summary>
        public static QuarterPlanMapData Build(QuarterPlan plan)
        {
            if (plan == null) return QuarterPlanMapData.Empty;

            IReadOnlyList<RoadSegment> planRoads = plan.Roads;
            var roads = new List<MapRoadSegment>(planRoads != null ? planRoads.Count : 0);
            var nodes = new List<NetworkNode>(plan.Nodes != null ? plan.Nodes.Count : 0);
            var segments = new List<NetworkSegment>(planRoads != null ? planRoads.Count : 0);

            if (plan.Nodes != null)
            {
                for (int i = 0; i < plan.Nodes.Count; i++)
                {
                    RoadNode node = plan.Nodes[i];
                    nodes.Add(new NetworkNode(node.Id, new MapPoint(node.Position.X, node.Position.Z),
                        node.IsIntersection));
                }
            }

            if (planRoads != null)
            {
                for (int i = 0; i < planRoads.Count; i++)
                {
                    RoadSegment road = planRoads[i];
                    bool driveway = road.Kind == RoadKind.Driveway;
                    roads.Add(new MapRoadSegment(
                        new MapPoint(road.Start.X, road.Start.Z),
                        new MapPoint(road.End.X, road.End.Z),
                        driveway));

                    // Узлы связываем по Id; если Id пуст — сегмент в граф не попадает.
                    segments.Add(new NetworkSegment(road.StartNodeId, road.EndNodeId,
                        driveway ? RouteEdgeKind.Driveway : RouteEdgeKind.Street));
                }
            }

            var points = new List<MapPoiMarker>();
            if (plan.DistrictPoints != null)
            {
                for (int i = 0; i < plan.DistrictPoints.Count; i++)
                {
                    DistrictPointInfo point = plan.DistrictPoints[i];
                    points.Add(new MapPoiMarker(new MapPoint(point.Center.X, point.Center.Z),
                        DistrictPointRadius, point.Name));
                }
            }

            if (plan.DeliveryPoints != null)
            {
                for (int i = 0; i < plan.DeliveryPoints.Count; i++)
                {
                    DeliveryPointInfo delivery = plan.DeliveryPoints[i];
                    points.Add(new MapPoiMarker(new MapPoint(delivery.Position.X, delivery.Position.Z),
                        DeliveryPointRadius, delivery.Id));
                }
            }

            RoadGraphData graph = RouteGeometryBuilder.Build(nodes, segments);
            return new QuarterPlanMapData(plan.Size, roads, points, graph);
        }

        /// <summary>
        /// Найти мировую точку по Id точки доставки или объекта района.
        /// Поддерживает точные <c>drop-*</c> Id, а также алиасы ПВЗ/зданий/коммерции
        /// (<c>pvz-01</c> → <c>drop-pvz-01</c>, <c>bld-n3</c> → <c>drop-bld-n3-1</c>, <c>player.home</c>).
        /// </summary>
        public static bool TryGetDeliveryPosition(QuarterPlan plan, string deliveryPointId, out MapPoint position)
        {
            position = new MapPoint(0f, 0f);
            if (plan == null || string.IsNullOrEmpty(deliveryPointId)) return false;

            if (plan.DeliveryPoints != null)
            {
                for (int i = 0; i < plan.DeliveryPoints.Count; i++)
                {
                    DeliveryPointInfo point = plan.DeliveryPoints[i];
                    if (!string.Equals(point.Id, deliveryPointId, StringComparison.Ordinal)) continue;
                    position = new MapPoint(point.Position.X, point.Position.Z);
                    return true;
                }

                string dropExact = "drop-" + deliveryPointId;
                string dropFirstEntrance = dropExact + "-1";
                for (int i = 0; i < plan.DeliveryPoints.Count; i++)
                {
                    DeliveryPointInfo point = plan.DeliveryPoints[i];
                    if (string.Equals(point.Id, dropExact, StringComparison.Ordinal) ||
                        string.Equals(point.Id, dropFirstEntrance, StringComparison.Ordinal) ||
                        string.Equals(point.BuildingId, deliveryPointId, StringComparison.Ordinal))
                    {
                        position = new MapPoint(point.Position.X, point.Position.Z);
                        return true;
                    }
                }
            }

            if (plan.PickupPoint != null &&
                string.Equals(plan.PickupPoint.Id, deliveryPointId, StringComparison.Ordinal))
            {
                position = new MapPoint(plan.PickupPoint.InteractionPoint.X, plan.PickupPoint.InteractionPoint.Z);
                return true;
            }

            if (plan.PlayerHome != null)
            {
                if (string.Equals(PlayerHomeInfo.SpecialId, deliveryPointId, StringComparison.Ordinal) ||
                    string.Equals("player.home.door", deliveryPointId, StringComparison.Ordinal) ||
                    string.Equals(plan.PlayerHome.BuildingId, deliveryPointId, StringComparison.Ordinal))
                {
                    position = new MapPoint(plan.PlayerHome.EntranceDoor.X, plan.PlayerHome.EntranceDoor.Z);
                    return true;
                }
                if (string.Equals("player.home.exit", deliveryPointId, StringComparison.Ordinal))
                {
                    position = new MapPoint(plan.PlayerHome.CourtyardExit.X, plan.PlayerHome.CourtyardExit.Z);
                    return true;
                }
            }

            if (plan.DistrictPoints != null)
            {
                for (int i = 0; i < plan.DistrictPoints.Count; i++)
                {
                    DistrictPointInfo dp = plan.DistrictPoints[i];
                    if (!string.Equals(dp.Id, deliveryPointId, StringComparison.Ordinal)) continue;
                    if (dp.InteractionPoint.HasValue)
                        position = new MapPoint(dp.InteractionPoint.Value.X, dp.InteractionPoint.Value.Z);
                    else
                        position = new MapPoint(dp.Center.X, dp.Center.Z);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Определяет Id ближайшей точки доставки/выдачи в радиусе взаимодействия от мировых
        /// координат игрока <paramref name="playerPosition"/>. Если игрок не у точки — пустая строка.
        /// </summary>
        public static string ResolveLocationId(QuarterPlan plan, MapPoint playerPosition, float minRadiusMeters = 3.0f)
        {
            if (plan == null ||
                float.IsNaN(playerPosition.X) || float.IsInfinity(playerPosition.X) ||
                float.IsNaN(playerPosition.Z) || float.IsInfinity(playerPosition.Z))
            {
                return string.Empty;
            }
            if (minRadiusMeters < 0.1f || float.IsNaN(minRadiusMeters) || float.IsInfinity(minRadiusMeters))
                minRadiusMeters = DeliveryPointInfo.DefaultInteractionRadius;

            string bestId = string.Empty;
            float bestDistSq = float.MaxValue;

            if (plan.DeliveryPoints != null)
            {
                for (int i = 0; i < plan.DeliveryPoints.Count; i++)
                {
                    DeliveryPointInfo point = plan.DeliveryPoints[i];
                    float radius = Math.Max(point.InteractionRadius, minRadiusMeters);
                    float dx = playerPosition.X - point.Position.X;
                    float dz = playerPosition.Z - point.Position.Z;
                    float distSq = dx * dx + dz * dz;
                    if (distSq <= radius * radius && distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestId = point.Id;
                    }
                }
            }

            if (!string.IsNullOrEmpty(bestId)) return bestId;

            if (plan.PickupPoint != null)
            {
                float radius = Math.Max(DeliveryPointInfo.DefaultInteractionRadius, minRadiusMeters);
                float dx = playerPosition.X - plan.PickupPoint.InteractionPoint.X;
                float dz = playerPosition.Z - plan.PickupPoint.InteractionPoint.Z;
                if (dx * dx + dz * dz <= radius * radius)
                    return plan.PickupPoint.Id;
            }

            return string.Empty;
        }
    }
}
