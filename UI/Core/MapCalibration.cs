// Life From Zero — UI/Core (ТЗ №042)
// Namespace: LifeFromZero.UI.Core
//
// Модель координат карты. Единственное место, где мировые координаты Unity (X/Z)
// превращаются в координаты картинки (U/V, 0..1) и обратно.
//
// Правила (ТЗ §5.3):
//   * мировые координаты и экранные пиксели не смешиваются;
//   * обратное преобразование обязательно и проверяется тестами;
//   * ориентация и масштаб — параметры привязки, а не «магия» в контроллере.
//
// Модуль не зависит от UnityEngine: он компилируется и прогоняется вне Unity.

using System;
using System.Globalization;

namespace LifeFromZero.UI.Core
{
    /// <summary>Точка на плоскости мира (метры, X — восток, Z — север).</summary>
    public readonly struct MapPoint : IEquatable<MapPoint>
    {
        public MapPoint(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float X { get; }

        public float Z { get; }

        public static MapPoint Lerp(MapPoint a, MapPoint b, float t) =>
            new MapPoint(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);

        public MapPoint Offset(float dx, float dz) => new MapPoint(X + dx, Z + dz);

        public static MapPoint operator +(MapPoint a, MapPoint b) => new MapPoint(a.X + b.X, a.Z + b.Z);

        public static MapPoint operator -(MapPoint a, MapPoint b) => new MapPoint(a.X - b.X, a.Z - b.Z);

        public static MapPoint operator *(MapPoint a, float k) => new MapPoint(a.X * k, a.Z * k);

        public float Length => (float)Math.Sqrt(X * X + Z * Z);

        public float DistanceTo(MapPoint other) => (this - other).Length;

        public bool Equals(MapPoint other) => X.Equals(other.X) && Z.Equals(other.Z);

        public override bool Equals(object obj) => obj is MapPoint other && Equals(other);

        public override int GetHashCode() => X.GetHashCode() * 397 ^ Z.GetHashCode();

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.##};{1:0.##})", X, Z);
    }

    /// <summary>Нормализованная точка изображения карты: U — вправо (0..1), V — вверх (0..1).</summary>
    public readonly struct MapUv : IEquatable<MapUv>
    {
        public MapUv(float u, float v)
        {
            U = u;
            V = v;
        }

        public float U { get; }

        public float V { get; }

        public bool IsFinite =>
            !float.IsNaN(U) && !float.IsInfinity(U) && !float.IsNaN(V) && !float.IsInfinity(V);

        public bool Equals(MapUv other) => U.Equals(other.U) && V.Equals(other.V);

        public override bool Equals(object obj) => obj is MapUv other && Equals(other);

        public override int GetHashCode() => U.GetHashCode() * 397 ^ V.GetHashCode();

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "u={0:0.####} v={1:0.####}", U, V);
    }

    /// <summary>
    /// Привязка картинки района к мировым координатам.
    /// </summary>
    /// <remarks>
    /// Базовое преобразование — подобие (масштаб + поворот + перенос):
    /// <code>
    /// u = u0 + ( X * cos(k) + Z * sin(k) * mirrorU ) / metersPerUnitU
    /// v = v0 + ( Z * cos(k) - X * sin(k) ) / metersPerUnitV
    /// </code>
    /// V растёт к северу (+Z), потому что на картинке север сверху, а в Unity
    /// ось V текстуры направлена вверх: мировая Z=0 (южная граница) попадает в V=0.
    /// Такой привязки достаточно, пока картинка ортогональна. Если рендер снят
    /// в перспективе, включается <see cref="Perspective"/> — тогда используется
    /// гомография 3×3 (см. <see cref="MapCalibration.SolvePerspective"/>).
    ///
    /// ВАЖНО: официальный `Map.PNG` — наклонный (перспективный) рендер, а раскладка
    /// District750 — ортогональная. Прямое преобразование «мир → картинка» нужно для
    /// наложения маркеров на картинку; обратное даёт только приблизительное чтение
    /// координат из клика по картинке. Обе операции обязаны существовать, обе
    /// проверяются тестами, а расхождение описано в отчёте (ТЗ §5.3).
    /// </remarks>
    public sealed class MapCalibration
    {
        /// <summary>Допуск на «нулевой» масштаб.</summary>
        public const float MinMetersPerUnit = 1f;

        public MapCalibration(
            float metersPerUnitU = 750f,
            float metersPerUnitV = 750f,
            float rotationDegrees = 0f,
            float originU = 0f,
            float originV = 0f,
            bool mirroredU = false,
            double[] perspective = null,
            float worldOriginX = 0f,
            float worldOriginZ = 0f)
        {
            MetersPerUnitU = Math.Abs(metersPerUnitU) < MinMetersPerUnit ? MinMetersPerUnit : metersPerUnitU;
            MetersPerUnitV = Math.Abs(metersPerUnitV) < MinMetersPerUnit ? MinMetersPerUnit : metersPerUnitV;
            RotationDegrees = rotationDegrees;
            OriginU = originU;
            OriginV = originV;
            MirroredU = mirroredU;
            Perspective = perspective != null && perspective.Length == 9 ? (double[])perspective.Clone() : null;
            WorldOriginX = worldOriginX;
            WorldOriginZ = worldOriginZ;
        }

        /// <summary>Сколько метров мира содержит весь диапазон U картинки.</summary>
        public float MetersPerUnitU { get; }

        /// <summary>Сколько метров мира содержит весь диапазон V картинки.</summary>
        public float MetersPerUnitV { get; }

        /// <summary>Поворот раскладки относительно картинки, градусы (против часовой).</summary>
        public float RotationDegrees { get; }

        /// <summary>U (0..1) мировой точки отсчёта.</summary>
        public float OriginU { get; }

        /// <summary>V (0..1) мировой точки отсчёта.</summary>
        public float OriginV { get; }

        /// <summary>Зеркалирование по горизонтали (не должно использоваться на District750 — проверено тестом).</summary>
        public bool MirroredU { get; }

        /// <summary>Мировая X точки, попадающей в (OriginU, OriginV).</summary>
        public float WorldOriginX { get; }

        /// <summary>Мировая Z точки, попадающей в (OriginU, OriginV).</summary>
        public float WorldOriginZ { get; }

        /// <summary>Гомография 3×3 (row-major) для перспективной картинки или null.</summary>
        public double[] Perspective { get; }

        public bool IsPerspective => Perspective != null;

        /// <summary>Привязка по умолчанию для ортогональной картинки 750×750, снятой сверху.</summary>
        public static MapCalibration District750Ortho(float size = 750f) =>
            new MapCalibration(size, size, 0f, 0f, 0f, false, null, 0f, 0f);

        /// <summary>Привязка с учётом перспективы официального рендера (см. MapCalibrationDefaults).</summary>
        public static MapCalibration District750Reference() => MapCalibrationDefaults.District750Perspective();

        public MapUv WorldToMap(MapPoint world)
        {
            if (Perspective != null) return PerspectiveWorldToMap(world);

            double k = RotationDegrees * Math.PI / 180.0;
            double cos = Math.Cos(k), sin = Math.Sin(k);
            double dx = world.X - WorldOriginX;
            double dz = world.Z - WorldOriginZ;
            double rx = dx * cos + dz * sin;
            double rz = dz * cos - dx * sin;
            if (MirroredU) rx = -rx;
            return new MapUv(
                (float)(OriginU + rx / MetersPerUnitU),
                (float)(OriginV + rz / MetersPerUnitV));
        }

        public MapPoint MapToWorld(MapUv uv)
        {
            if (Perspective != null) return PerspectiveMapToWorld(uv);

            double k = RotationDegrees * Math.PI / 180.0;
            double cos = Math.Cos(k), sin = Math.Sin(k);
            double rx = (uv.U - OriginU) * MetersPerUnitU;
            double rz = (uv.V - OriginV) * MetersPerUnitV;
            if (MirroredU) rx = -rx;
            double dx = rx * cos - rz * sin;
            double dz = rz * cos + rx * sin;
            return new MapPoint((float)(WorldOriginX + dx), (float)(WorldOriginZ + dz));
        }

        private MapUv PerspectiveWorldToMap(MapPoint world)
        {
            double[] h = Perspective;
            double x = world.X, z = world.Z;
            double den = h[6] * x + h[7] * z + h[8];
            if (Math.Abs(den) < 1e-9) return new MapUv(float.NaN, float.NaN);
            return new MapUv(
                (float)((h[0] * x + h[1] * z + h[2]) / den),
                (float)((h[3] * x + h[4] * z + h[5]) / den));
        }

        private MapPoint PerspectiveMapToWorld(MapUv uv)
        {
            // Обратная гомография: 3x3 inverse, затем гомогенное деление.
            double[] h = Perspective;
            double det = h[0] * (h[4] * h[8] - h[5] * h[7])
                       - h[1] * (h[3] * h[8] - h[5] * h[6])
                       + h[2] * (h[3] * h[7] - h[4] * h[6]);
            if (Math.Abs(det) < 1e-12) return new MapPoint(float.NaN, float.NaN);
            double i0 = (h[4] * h[8] - h[5] * h[7]) / det;
            double i1 = (h[2] * h[7] - h[1] * h[8]) / det;
            double i2 = (h[1] * h[5] - h[2] * h[4]) / det;
            double i3 = (h[5] * h[6] - h[3] * h[8]) / det;
            double i4 = (h[0] * h[8] - h[2] * h[6]) / det;
            double i5 = (h[2] * h[3] - h[0] * h[5]) / det;
            double i6 = (h[3] * h[7] - h[4] * h[6]) / det;
            double i7 = (h[1] * h[6] - h[0] * h[7]) / det;
            double i8 = (h[0] * h[4] - h[1] * h[3]) / det;
            double u = uv.U, v = uv.V;
            double den = i6 * u + i7 * v + i8;
            if (Math.Abs(den) < 1e-12) return new MapPoint(float.NaN, float.NaN);
            return new MapPoint(
                (float)((i0 * u + i1 * v + i2) / den),
                (float)((i3 * u + i4 * v + i5) / den));
        }

        /// <summary>
        /// МНК-решение гомографии по парам «мир ↔ картинка» (минимум 4 пары, не все на одной прямой).
        /// Возвращает null, если система вырождена.
        /// </summary>
        public static double[] SolvePerspective(MapPoint[] world, MapUv[] map)
        {
            if (world == null || map == null || world.Length != map.Length || world.Length < 4) return null;

            int n = world.Length;
            // Неизвестные: h0..h7, h8 = 1. Строк 2n, столбцов 8.
            var a = new double[2 * n, 8];
            var b = new double[2 * n];
            for (int i = 0; i < n; i++)
            {
                double x = world[i].X, z = world[i].Z, u = map[i].U, v = map[i].V;
                a[2 * i, 0] = x; a[2 * i, 1] = z; a[2 * i, 2] = 1;
                a[2 * i, 6] = -u * x; a[2 * i, 7] = -u * z;
                b[2 * i] = u;

                a[2 * i + 1, 3] = x; a[2 * i + 1, 4] = z; a[2 * i + 1, 5] = 1;
                a[2 * i + 1, 6] = -v * x; a[2 * i + 1, 7] = -v * z;
                b[2 * i + 1] = v;
            }

            double[] sol = SolveLeastSquares(a, b);
            if (sol == null) return null;
            return new[] { sol[0], sol[1], sol[2], sol[3], sol[4], sol[5], sol[6], sol[7], 1.0 };
        }

        /// <summary>Нормальные уравнения + Гаусс с частичным выбором ведущего элемента.</summary>
        internal static double[] SolveLeastSquares(double[,] a, double[] b)
        {
            int rows = a.GetLength(0), cols = a.GetLength(1);
            var ata = new double[cols, cols];
            var atb = new double[cols];
            for (int i = 0; i < cols; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double sum = 0d;
                    for (int r = 0; r < rows; r++) sum += a[r, i] * a[r, j];
                    ata[i, j] = sum;
                }
                double sb = 0d;
                for (int r = 0; r < rows; r++) sb += a[r, i] * b[r];
                atb[i] = sb;
            }

            for (int col = 0; col < cols; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < cols; r++)
                    if (Math.Abs(ata[r, col]) > Math.Abs(ata[pivot, col])) pivot = r;
                if (Math.Abs(ata[pivot, col]) < 1e-12) return null;

                if (pivot != col)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        double t = ata[col, c]; ata[col, c] = ata[pivot, c]; ata[pivot, c] = t;
                    }
                    double tb = atb[col]; atb[col] = atb[pivot]; atb[pivot] = tb;
                }

                double diag = ata[col, col];
                for (int c = col; c < cols; c++) ata[col, c] /= diag;
                atb[col] /= diag;

                for (int r = 0; r < cols; r++)
                {
                    if (r == col) continue;
                    double factor = ata[r, col];
                    if (Math.Abs(factor) < 1e-18) continue;
                    for (int c = col; c < cols; c++) ata[r, c] -= factor * ata[col, c];
                    atb[r] -= factor * atb[col];
                }
            }

            return atb;
        }

        /// <summary>
        /// Подбор подобия (без перспективы) по парам «мир ↔ картинка», МНК.
        /// Нужен, чтобы ЧЕСТНО измерить, насколько ортогональная модель описывает
        /// официальный рендер карты, и не подгонять маркеры вручную.
        /// </summary>
        public static MapCalibration FitSimilarity(MapPoint[] world, MapUv[] map, out float rmsError)
        {
            rmsError = float.PositiveInfinity;
            if (world == null || map == null || world.Length != map.Length || world.Length < 3) return null;

            // u = a*x + b*z + c ; v = d*x + e*z + f  (линейная модель, затем читаем масштаб/поворот)
            int n = world.Length;
            var a = new double[2 * n, 6];
            var b = new double[2 * n];
            for (int i = 0; i < n; i++)
            {
                a[2 * i, 0] = world[i].X; a[2 * i, 1] = world[i].Z; a[2 * i, 2] = 1;
                b[2 * i] = map[i].U;
                a[2 * i + 1, 3] = world[i].X; a[2 * i + 1, 4] = world[i].Z; a[2 * i + 1, 5] = 1;
                b[2 * i + 1] = map[i].V;
            }
            double[] sol = SolveLeastSquares(a, b);
            if (sol == null) return null;

            double scaleU = Math.Sqrt(sol[0] * sol[0] + sol[1] * sol[1]);
            double scaleV = Math.Sqrt(sol[3] * sol[3] + sol[4] * sol[4]);
            double rotation = Math.Atan2(sol[1], sol[0]) * 180.0 / Math.PI;
            var cal = new MapCalibration(
                (float)(1.0 / Math.Max(scaleU, 1e-9)),
                (float)(1.0 / Math.Max(scaleV, 1e-9)),
                (float)-rotation,
                (float)sol[2],
                (float)sol[5],
                false);
            // Повторно считаем ошибку уже через саму привязку (а не по коэффициентам).
            double sum = 0d;
            for (int i = 0; i < n; i++)
            {
                MapUv p = cal.WorldToMap(world[i]);
                double du = p.U - map[i].U, dv = p.V - map[i].V;
                sum += du * du + dv * dv;
            }
            rmsError = (float)Math.Sqrt(sum / n);
            return cal;
        }
    }
}
