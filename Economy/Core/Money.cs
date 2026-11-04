// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Core
// Unity 6000 / C# 9 compatible. No Unity API, no third-party packages.

using System;

namespace LifeFromZero.Economy.Core
{
    /// <summary>
    /// Денежная сумма в целых неотрицательных рублях.
    /// Отдельный тип нужен, чтобы рубли нельзя было случайно перепутать с количеством товаров,
    /// процентами или сытостью, а также чтобы сосредоточить в одном месте проверку переполнения
    /// и отрицательных значений.
    /// </summary>
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        /// <summary>Ноль рублей.</summary>
        public static readonly Money Zero = new Money(0L);

        /// <summary>Сумма в целых рублях. Всегда &gt;= 0.</summary>
        public long Rubles { get; }

        private Money(long rubles)
        {
            Rubles = rubles;
        }

        /// <summary>
        /// Создаёт сумму из количества рублей.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Если рубли отрицательные.</exception>
        public static Money FromRubles(long rubles)
        {
            if (rubles < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rubles), rubles, "Денежная сумма не может быть отрицательной.");
            }

            return new Money(rubles);
        }

        /// <summary>
        /// Пытается создать сумму из количества рублей без исключения.
        /// </summary>
        public static bool TryFromRubles(long rubles, out Money money)
        {
            if (rubles < 0L)
            {
                money = Zero;
                return false;
            }

            money = new Money(rubles);
            return true;
        }

        /// <summary>
        /// Умножает цену на количество с проверкой переполнения <see cref="long"/>.
        /// Используется магазином для расчёта стоимости покупки.
        /// </summary>
        /// <param name="quantity">Количество единиц товара, &gt;= 0.</param>
        /// <param name="total">Итоговая сумма.</param>
        /// <returns>
        /// <c>true</c> — стоимость посчитана; <c>false</c> — количество отрицательное
        /// или произведение не помещается в <see cref="long"/>.
        /// </returns>
        public bool TryMultiply(int quantity, out Money total)
        {
            if (quantity < 0)
            {
                total = Zero;
                return false;
            }

            if (Rubles == 0L || quantity == 0)
            {
                total = Zero;
                return true;
            }

            // Rubles >= 0 и quantity >= 0, поэтому достаточно сравнить с long.MaxValue / quantity.
            // Деление вместо long.MaxValue / quantity исключает переполнение самой проверки.
            if (Rubles > long.MaxValue / quantity)
            {
                total = Zero;
                return false;
            }

            total = new Money(Rubles * quantity);
            return true;
        }

        /// <summary>Сложение сумм с проверкой переполнения.</summary>
        public bool TryAdd(Money other, out Money sum)
        {
            if (Rubles > long.MaxValue - other.Rubles)
            {
                sum = Zero;
                return false;
            }

            sum = new Money(Rubles + other.Rubles);
            return true;
        }

        public bool Equals(Money other) => Rubles == other.Rubles;

        public override bool Equals(object obj) => obj is Money other && Equals(other);

        public override int GetHashCode() => Rubles.GetHashCode();

        public int CompareTo(Money other) => Rubles.CompareTo(other.Rubles);

        public override string ToString() => Rubles.ToString(System.Globalization.CultureInfo.InvariantCulture) + " RUB";

        public static bool operator ==(Money left, Money right) => left.Equals(right);

        public static bool operator !=(Money left, Money right) => !left.Equals(right);

        public static bool operator <(Money left, Money right) => left.Rubles < right.Rubles;

        public static bool operator >(Money left, Money right) => left.Rubles > right.Rubles;

        public static bool operator <=(Money left, Money right) => left.Rubles <= right.Rubles;

        public static bool operator >=(Money left, Money right) => left.Rubles >= right.Rubles;
    }
}
