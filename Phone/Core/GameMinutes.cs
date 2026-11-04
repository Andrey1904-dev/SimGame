// Life From Zero — Phone core (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Core
// Unity 6000 / C# 9 compatible. No Unity API, no third-party packages.

using System;

namespace LifeFromZero.Phone.Core
{
    /// <summary>
    /// Игровое время в минутах с начала игры.
    /// Отдельный тип, а не «голый» double: так время нельзя перепутать с деньгами
    /// или количеством, а проверка корректности сосредоточена в одном месте.
    /// </summary>
    /// <remarks>
    /// Телефон НЕ владеет временем: значение приходит из адаптера к GameClock
    /// (см. <see cref="IPhoneClock"/>). Если адаптер вернул NaN/бесконечность/отрицательное
    /// значение, телефон использует <see cref="Unknown"/> и не падает.
    /// </remarks>
    public readonly struct GameMinutes : IEquatable<GameMinutes>, IComparable<GameMinutes>
    {
        /// <summary>Время неизвестно (адаптер недоступен или вернул мусор).</summary>
        public static readonly GameMinutes Unknown = new GameMinutes(-1d, false);

        private readonly double _minutes;

        private GameMinutes(double minutes, bool isValid)
        {
            _minutes = minutes;
            IsValid = isValid;
        }

        /// <summary>Является ли значение корректным игровым временем.</summary>
        public bool IsValid { get; }

        /// <summary>Минуты с начала игры. Для <see cref="Unknown"/> — 0.</summary>
        public double Minutes => IsValid ? _minutes : 0d;

        /// <summary>
        /// Создаёт время из минут. Отрицательные, NaN и бесконечные значения дают <see cref="Unknown"/>.
        /// </summary>
        public static GameMinutes FromMinutes(double minutes)
        {
            if (double.IsNaN(minutes) || double.IsInfinity(minutes) || minutes < 0d)
            {
                return Unknown;
            }

            return new GameMinutes(minutes, true);
        }

        /// <summary>Пытается создать время без «тихого» превращения в <see cref="Unknown"/>.</summary>
        public static bool TryFromMinutes(double minutes, out GameMinutes value)
        {
            if (double.IsNaN(minutes) || double.IsInfinity(minutes) || minutes < 0d)
            {
                value = Unknown;
                return false;
            }

            value = new GameMinutes(minutes, true);
            return true;
        }

        public bool Equals(GameMinutes other)
        {
            if (IsValid != other.IsValid)
            {
                return false;
            }

            return !IsValid || Minutes.Equals(other.Minutes);
        }

        public override bool Equals(object obj) => obj is GameMinutes other && Equals(other);

        public override int GetHashCode() => IsValid ? Minutes.GetHashCode() : -1;

        public int CompareTo(GameMinutes other)
        {
            if (!IsValid && !other.IsValid)
            {
                return 0;
            }

            if (!IsValid)
            {
                return -1;
            }

            if (!other.IsValid)
            {
                return 1;
            }

            return Minutes.CompareTo(other.Minutes);
        }

        public override string ToString() => IsValid ? Minutes.ToString("0.###") + " min" : "<unknown time>";

        public static bool operator ==(GameMinutes left, GameMinutes right) => left.Equals(right);

        public static bool operator !=(GameMinutes left, GameMinutes right) => !left.Equals(right);
    }
}
