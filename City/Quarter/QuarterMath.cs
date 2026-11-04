// Life From Zero — City/Quarter module (ТЗ №023)
// Namespace: LifeFromZero.City.Quarter
//
// Собственная математика модуля: генератор НЕ зависит от UnityEngine,
// поэтому Vector2/Vector3 из Unity здесь не используются.

using System;
using System.Globalization;

namespace LifeFromZero.City.Quarter
{
    /// <summary>Точка/вектор на плоскости квартала (метры, X — восток, Z — север).</summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Z;

        public Vec2(float x, float z) { X = x; Z = z; }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator *(Vec2 a, float k) => new Vec2(a.X * k, a.Z * k);

        public float Length() => (float)Math.Sqrt(X * X + Z * Z);

        public float DistanceTo(Vec2 other) => (this - other).Length();

        public bool Equals(Vec2 other) => X.Equals(other.X) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Z.GetHashCode();
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.##},{1:0.##})", X, Z);
    }

    /// <summary>Позиция/размер в метрах. Y — высота (0 = уровень земли).</summary>
    public struct Vec3 : IEquatable<Vec3>
    {
        public float X;
        public float Y;
        public float Z;

        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public Vec2 Ground => new Vec2(X, Z);

        public bool Equals(Vec3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is Vec3 other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.##},{1:0.##},{2:0.##})", X, Y, Z);
    }

    /// <summary>Осевой прямоугольник на плоскости (AABB), метры.</summary>
    public struct Rect2 : IEquatable<Rect2>
    {
        public float MinX;
        public float MinZ;
        public float MaxX;
        public float MaxZ;

        public Rect2(float minX, float minZ, float maxX, float maxZ)
        {
            MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ;
        }

        public static Rect2 FromCenterSize(Vec2 center, float sizeX, float sizeZ) =>
            new Rect2(center.X - sizeX / 2f, center.Z - sizeZ / 2f,
                      center.X + sizeX / 2f, center.Z + sizeZ / 2f);

        public float Width => MaxX - MinX;
        public float Height => MaxZ - MinZ;
        public Vec2 Center => new Vec2((MinX + MaxX) / 2f, (MinZ + MaxZ) / 2f);

        public Rect2 Expanded(float margin) =>
            new Rect2(MinX - margin, MinZ - margin, MaxX + margin, MaxZ + margin);

        public Rect2 Shrunk(float margin) => Expanded(-margin);

        public bool Contains(Vec2 p) => p.X >= MinX && p.X <= MaxX && p.Z >= MinZ && p.Z <= MaxZ;

        public bool Overlaps(Rect2 other) =>
            MinX < other.MaxX && MaxX > other.MinX && MinZ < other.MaxZ && MaxZ > other.MinZ;

        public bool Equals(Rect2 other) =>
            MinX.Equals(other.MinX) && MinZ.Equals(other.MinZ) &&
            MaxX.Equals(other.MaxX) && MaxZ.Equals(other.MaxZ);
        public override bool Equals(object obj) => obj is Rect2 other && Equals(other);
        public override int GetHashCode() =>
            ((MinX.GetHashCode() * 397 ^ MinZ.GetHashCode()) * 397 ^ MaxX.GetHashCode()) * 397 ^ MaxZ.GetHashCode();
    }

    /// <summary>
    /// Детерминированный генератор случайных чисел (SplitMix64).
    /// Не используем System.Random: его последовательность не является
    /// частью контракта между версиями .NET/Unity, а нам нужна
    /// побитово воспроизводимая генерация по seed на любой платформе.
    /// </summary>
    public sealed class SeededRandom
    {
        private ulong _state;

        public SeededRandom(int seed)
        {
            // Размешиваем seed, чтобы соседние seed давали непохожие последовательности.
            _state = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL + 0x2545F4914F6CDD1DUL);
            NextUInt64();
            NextUInt64();
        }

        public ulong NextUInt64()
        {
            ulong z = (_state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Целое в диапазоне [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Пустой диапазон.");
            ulong range = (ulong)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt64() % range);
        }

        /// <summary>Вещественное в диапазоне [0, 1).</summary>
        public float NextFloat()
        {
            // 24 бита — точно представимо во float.
            return (NextUInt64() >> 40) * (1f / 16777216f);
        }

        public float NextRange(float minInclusive, float maxInclusive) =>
            minInclusive + NextFloat() * (maxInclusive - minInclusive);

        public bool NextChance(float probability) => NextFloat() < probability;

        public T Pick<T>(System.Collections.Generic.IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
                throw new ArgumentException("Не из чего выбирать.", nameof(items));
            return items[NextInt(0, items.Count)];
        }
    }
}
