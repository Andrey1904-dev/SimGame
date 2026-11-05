// Life From Zero — UI/Runtime (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// Одна uGUI-графика, которая рисует сетку, дороги и маршрут в общем буфере вершин.
//
// Почему так: 750-метровый район даёт сотни отрезков. Отдельный Image на каждый
// отрезок — это сотни GameObject'ов и Canvas-батчей. Здесь — один меш на слой,
// поэтому карта остаётся дешёвой на телефоне и в Unity 6.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Провайдер геометрии карты в мировых координатах.</summary>
    public interface IMapGeometrySource
    {
        /// <summary>Сетка (шаг в метрах) — линии по X и Z.</summary>
        void GetGrid(float stepMeters, float minX, float maxX, float minZ, float maxZ,
            Action<Vector2, Vector2> line);

        /// <summary>Дороги района.</summary>
        void GetRoads(Action<Vector2, Vector2, bool> segment);

        /// <summary>Точки интереса: позиция и радиус в метрах.</summary>
        void GetPoints(Action<Vector2, float> point);
    }

    /// <summary>Слой карты, рисуемый одним мешем.</summary>
    public sealed class MapCanvasGraphic : MaskableGraphic
    {
        /// <summary>Мировая точка → локальная точка RectTransform.</summary>
        public Func<Vector2, Vector2> Project;

        /// <summary>Геометрия. Меняется при смене района.</summary>
        public IMapGeometrySource Source;

        /// <summary>Шаг сетки в метрах; 0 — не рисовать.</summary>
        public float GridStepMeters = 100f;

        /// <summary>Границы мира, метры.</summary>
        public Rect WorldBounds = new Rect(0f, 0f, 750f, 750f);

        /// <summary>Толщина дороги, пиксели.</summary>
        public float RoadWidthPixels = 5f;

        /// <summary>Толщина тротуара/проезда, пиксели.</summary>
        public float DrivewayWidthPixels = 3f;

        /// <summary>Цвет сетки.</summary>
        public Color GridColor = new Color(1f, 1f, 1f, 0.07f);

        /// <summary>Цвет дорог.</summary>
        public Color RoadColor = new Color(0.62f, 0.66f, 0.72f, 0.95f);

        /// <summary>Цвет проездов.</summary>
        public Color DrivewayColor = new Color(0.42f, 0.45f, 0.50f, 0.90f);

        /// <summary>Цвет точек интереса.</summary>
        public Color PointColor = new Color(0.72f, 0.78f, 0.86f, 0.85f);

        /// <summary>Маршрут (мировые точки) и его толщина.</summary>
        public IList<Vector2> RoutePoints;

        public float RouteWidthPixels = 9f;

        public Color RouteColor = new Color(0.20f, 0.62f, 0.96f, 1f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Project == null) return;

            var rect = rectTransform.rect;
            float halfW = rect.width * 0.5f;
            float halfH = rect.height * 0.5f;

            if (Source != null && GridStepMeters > 0f)
            {
                float minX = WorldBounds.xMin;
                float maxX = WorldBounds.xMax;
                float minZ = WorldBounds.yMin;
                float maxZ = WorldBounds.yMax;

                Source.GetGrid(GridStepMeters, minX, maxX, minZ, maxZ, (a, b) =>
                    AddLine(vh, a, b, 1f, GridColor, halfW, halfH));
            }

            if (Source != null)
            {
                Source.GetRoads((a, b, driveway) =>
                    AddLine(vh, a, b,
                        driveway ? DrivewayWidthPixels : RoadWidthPixels,
                        driveway ? DrivewayColor : RoadColor, halfW, halfH));

                Source.GetPoints((point, radiusMeters) =>
                {
                    Vector2 center = Project(point);
                    float radiusPixels = Mathf.Max(2f, radiusMeters * PixelsPerMeter(rect));
                    AddDisc(vh, center, radiusPixels, PointColor, halfW, halfH);
                });
            }

            if (RoutePoints != null && RoutePoints.Count > 1)
            {
                for (int i = 0; i < RoutePoints.Count - 1; i++)
                {
                    AddLine(vh, RoutePoints[i], RoutePoints[i + 1], RouteWidthPixels, RouteColor, halfW, halfH);
                }
                for (int i = 0; i < RoutePoints.Count - 1; i++)
                {
                    AddDisc(vh, RoutePoints[i], RouteWidthPixels * 0.55f, RouteColor, halfW, halfH);
                }
            }
        }

        /// <summary>Сколько пикселей в одном метре при текущем масштабе.</summary>
        public Func<float> PixelsPerMeterProvider;

        private float PixelsPerMeter(Rect rect)
        {
            return PixelsPerMeterProvider != null ? PixelsPerMeterProvider() : 1f;
        }

        /// <summary>Перерисовать (после смены зума/маршрута).</summary>
        public void Redraw() => SetVerticesDirty();

        private void AddLine(VertexHelper vh, Vector2 worldA, Vector2 worldB, float widthPixels, Color color,
            float halfW, float halfH)
        {
            Vector2 a = ToLocal(Project(worldA), halfW, halfH);
            Vector2 b = ToLocal(Project(worldB), halfW, halfH);

            Vector2 direction = b - a;
            float length = direction.magnitude;
            if (length < 0.01f) return;

            Vector2 normal = new Vector2(-direction.y, direction.x) / length * (widthPixels * 0.5f);
            AddQuad(vh, a - normal, b - normal, b + normal, a + normal, color);
        }

        private Vector2 ToLocal(Vector2 projected, float halfW, float halfH) =>
            new Vector2((projected.x - 0.5f) * halfW * 2f, (projected.y - 0.5f) * halfH * 2f);

        private void AddDisc(VertexHelper vh, Vector2 worldPoint, float radiusPixels, Color color,
            float halfW, float halfH)
        {
            Vector2 center = ToLocal(Project(worldPoint), halfW, halfH);
            const int segments = 10;
            Vector2 previous = center + new Vector2(radiusPixels, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 current = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radiusPixels;
                AddTriangle(vh, center, previous, current, color);
                previous = current;
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
