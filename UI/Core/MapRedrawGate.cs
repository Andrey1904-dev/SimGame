// Life From Zero — UI/Core (ТЗ №045)
// Namespace: LifeFromZero.UI.Core
//
// ЗАЧЕМ ЭТОТ ФАЙЛ (регрессия после реального Play Mode).
//
// Экран карты перерисовывался на каждое обращение к MapScreen.Refresh(), а
// CanvasUpdateRegistry вызывал перестройку меша даже когда данные не менялись.
// Если слой карты оказывался без CanvasRenderer, uGUI выбрасывал
// MissingComponentException НА КАЖДОЙ перестройке — в консоли это «сотни раз».
//
// Правило: перерисовка только тогда, когда изменилось то, что видно на экране.
// Решение принимает чистый код (без UnityEngine) — его можно проверить тестами.
//
// Модуль без UnityEngine.

using System;

namespace LifeFromZero.UI.Core
{
    /// <summary>
    /// Всё, что влияет на картинку карты: масштаб, центр, размер района, шаг сетки,
    /// сам объект геометрии, маршрут и (округлённая) позиция игрока.
    /// </summary>
    public readonly struct MapFrameKey : IEquatable<MapFrameKey>
    {
        public MapFrameKey(
            bool visible,
            float zoom,
            float centerX,
            float centerY,
            float worldWidth,
            float worldHeight,
            float gridStep,
            int geometryId,
            int routeSignature,
            bool hasPlayer,
            float playerX,
            float playerY)
        {
            Visible = visible;
            Zoom = zoom;
            CenterX = centerX;
            CenterY = centerY;
            WorldWidth = worldWidth;
            WorldHeight = worldHeight;
            GridStep = gridStep;
            GeometryId = geometryId;
            RouteSignature = routeSignature;
            HasPlayer = hasPlayer;
            PlayerX = playerX;
            PlayerY = playerY;
        }

        public bool Visible { get; }
        public float Zoom { get; }
        public float CenterX { get; }
        public float CenterY { get; }
        public float WorldWidth { get; }
        public float WorldHeight { get; }
        public float GridStep { get; }
        /// <summary>Идентификатор объекта геометрии (сменился район — сменился id).</summary>
        public int GeometryId { get; }
        /// <summary>Подпись маршрута: см. <see cref="MapRedrawGate.RouteSignature"/> (0 — маршрута нет).</summary>
        public int RouteSignature { get; }
        public bool HasPlayer { get; }
        public float PlayerX { get; }
        public float PlayerY { get; }

        public bool Equals(MapFrameKey other)
        {
            return Visible == other.Visible
                   && Near(Zoom, other.Zoom)
                   && Near(CenterX, other.CenterX)
                   && Near(CenterY, other.CenterY)
                   && Near(WorldWidth, other.WorldWidth)
                   && Near(WorldHeight, other.WorldHeight)
                   && Near(GridStep, other.GridStep)
                   && GeometryId == other.GeometryId
                   && RouteSignature == other.RouteSignature
                   && HasPlayer == other.HasPlayer
                   && Near(PlayerX, other.PlayerX)
                   && Near(PlayerY, other.PlayerY);
        }

        public override bool Equals(object obj) => obj is MapFrameKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Visible ? 17 : 3;
                hash = hash * 31 + Zoom.GetHashCode();
                hash = hash * 31 + CenterX.GetHashCode();
                hash = hash * 31 + CenterY.GetHashCode();
                hash = hash * 31 + WorldWidth.GetHashCode();
                hash = hash * 31 + WorldHeight.GetHashCode();
                hash = hash * 31 + GridStep.GetHashCode();
                hash = hash * 31 + GeometryId;
                hash = hash * 31 + RouteSignature;
                hash = hash * 31 + PlayerX.GetHashCode();
                hash = hash * 31 + PlayerY.GetHashCode();
                return hash;
            }
        }

        public override string ToString() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "MapFrame(zoom={0:0.###}, center=({1:0.#};{2:0.#}), grid={3:0.#}, geom={4}, route={5}, player=({6:0.#};{7:0.#}))",
                Zoom, CenterX, CenterY, GridStep, GeometryId, RouteSignature, PlayerX, PlayerY);

        private static bool Near(float a, float b) => Math.Abs(a - b) < 0.0005f;
    }

    /// <summary>
    /// Решает, нужно ли перестраивать меш карты. Один экземпляр на <c>MapScreen</c>.
    /// </summary>
    public sealed class MapRedrawGate
    {
        private MapFrameKey _last;
        private bool _hasLast;

        /// <summary>Сколько раз карта действительно перерисовывалась.</summary>
        public int RedrawCount { get; private set; }

        /// <summary>Сколько обращений к Refresh() были пропущены как «ничего не изменилось».</summary>
        public int SkippedCount { get; private set; }

        /// <summary>true — данные изменились и карту нужно перерисовать.</summary>
        public bool ShouldRedraw(MapFrameKey key)
        {
            if (_hasLast && _last.Equals(key))
            {
                SkippedCount++;
                return false;
            }

            _last = key;
            _hasLast = true;
            RedrawCount++;
            return true;
        }

        /// <summary>Забыть последнее состояние (например, после смены района или сцены).</summary>
        public void Reset()
        {
            _hasLast = false;
            _last = default(MapFrameKey);
        }

        /// <summary>
        /// Подпись маршрута для <see cref="MapFrameKey.RouteSignature"/>: меняется, когда
        /// меняется сам маршрут (цель, число точек, длина) — и не раньше.
        /// </summary>
        public static int RouteSignature(RoutePath route)
        {
            if (route == null) return 0;

            unchecked
            {
                int hash = 19;
                hash = hash * 31 + (route.DestinationId ?? string.Empty).GetHashCode();
                hash = hash * 31 + route.Steps.Count;
                hash = hash * 31 + Quantize(route.TotalDistance);
                hash = hash * 31 + Quantize(route.Destination.X);
                hash = hash * 31 + Quantize(route.Destination.Z);
                if (route.Steps.Count > 0)
                {
                    MapPoint last = route.Steps[route.Steps.Count - 1].Position;
                    hash = hash * 31 + Quantize(last.X);
                    hash = hash * 31 + Quantize(last.Z);
                }
                return hash == 0 ? 1 : hash;
            }
        }

        /// <summary>Метры с точностью 0.1 — позиция игрока не «дрожит» от физики.</summary>
        public static int Quantize(float meters) => (int)Math.Round(meters * 10f);

        /// <summary>Округление позиции игрока до полуметра: метка не перерисовывается каждый кадр.</summary>
        public static int PlayerCell(float meters) => (int)Math.Floor(meters * 2f);
    }
}
