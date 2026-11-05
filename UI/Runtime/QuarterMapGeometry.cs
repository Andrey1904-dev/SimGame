// Life From Zero — UI/Runtime (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// Адаптер «данные карты (UI/Core) → IMapGeometrySource (uGUI)».
//
// Вся логика разбора QuarterPlan живёт в UI/Core/QuarterPlanAdapter.cs и покрыта
// тестами; здесь только перевод MapPoint → Vector2 и колбэки меша.
//
// Если плана нет, карта честно рисует только сетку района и пишет об этом в отчёт
// подключения (UiBootstrap.Report), а не подсовывает выдуманные дороги.

using System;
using System.Collections.Generic;
using LifeFromZero.UI.Core;
using UnityEngine;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Геометрия карты из заранее разобранного плана района.</summary>
    public sealed class QuarterMapGeometry : IMapGeometrySource
    {
        private readonly Vector2[] _roadStarts;
        private readonly Vector2[] _roadEnds;
        private readonly bool[] _roadIsDriveway;
        private readonly Vector2[] _points;
        private readonly float[] _pointRadii;

        public QuarterMapGeometry(QuarterPlanMapData data)
        {
            if (data == null)
            {
                _roadStarts = Array.Empty<Vector2>();
                _roadEnds = Array.Empty<Vector2>();
                _roadIsDriveway = Array.Empty<bool>();
                _points = Array.Empty<Vector2>();
                _pointRadii = Array.Empty<float>();
                return;
            }

            Size = data.Size;

            _roadStarts = new Vector2[data.Roads.Count];
            _roadEnds = new Vector2[data.Roads.Count];
            _roadIsDriveway = new bool[data.Roads.Count];
            for (int i = 0; i < data.Roads.Count; i++)
            {
                MapRoadSegment road = data.Roads[i];
                _roadStarts[i] = new Vector2(road.Start.X, road.Start.Z);
                _roadEnds[i] = new Vector2(road.End.X, road.End.Z);
                _roadIsDriveway[i] = road.IsDriveway;
            }

            _points = new Vector2[data.Points.Count];
            _pointRadii = new float[data.Points.Count];
            for (int i = 0; i < data.Points.Count; i++)
            {
                MapPoiMarker marker = data.Points[i];
                _points[i] = new Vector2(marker.Position.X, marker.Position.Z);
                _pointRadii[i] = marker.RadiusMeters;
            }

            Graph = data.Graph;
        }

        /// <summary>Сторона района в метрах (0 — данных нет).</summary>
        public float Size { get; }

        /// <summary>Граф маршрутизации из того же плана.</summary>
        public RoadGraphData Graph { get; }

        public bool HasData => _roadStarts.Length > 0 || _points.Length > 0;

        public int RoadCount => _roadStarts.Length;

        public int PointCount => _points.Length;

        public void GetGrid(float stepMeters, float minX, float maxX, float minZ, float maxZ,
            Action<Vector2, Vector2> line)
        {
            if (line == null || stepMeters <= 0.01f) return;

            float size = Size > 0f ? Size : 750f;
            for (float x = 0f; x <= size + 0.01f; x += stepMeters)
                line(new Vector2(x, 0f), new Vector2(x, size));
            for (float z = 0f; z <= size + 0.01f; z += stepMeters)
                line(new Vector2(0f, z), new Vector2(size, z));
        }

        public void GetRoads(Action<Vector2, Vector2, bool> segment)
        {
            if (segment == null) return;
            for (int i = 0; i < _roadStarts.Length; i++)
                segment(_roadStarts[i], _roadEnds[i], _roadIsDriveway[i]);
        }

        public void GetPoints(Action<Vector2, float> point)
        {
            if (point == null) return;
            for (int i = 0; i < _points.Length; i++) point(_points[i], _pointRadii[i]);
        }
    }
}
