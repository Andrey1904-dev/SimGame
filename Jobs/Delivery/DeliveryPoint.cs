// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Delivery
// Unity 6000 / C# 9 compatible. No Unity API, no third-party packages.

using System;

namespace LifeFromZero.Jobs.Delivery
{
    /// <summary>
    /// Точка на карте: место получения груза или место доставки.
    /// Модуль не содержит системы перемещения — сравнивает только идентификаторы,
    /// а «где сейчас игрок» спрашивает через <see cref="ILocationProvider"/>.
    /// </summary>
    public readonly struct DeliveryPoint : IEquatable<DeliveryPoint>
    {
        /// <summary>Строковый идентификатор точки, например "bakery" или "office_12".</summary>
        public string Id { get; }

        /// <summary>Отображаемое название точки. Не используется в логике.</summary>
        public string DisplayName { get; }

        public DeliveryPoint(string id, string displayName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Идентификатор точки не должен быть пустым.", nameof(id));
            }

            Id = id;
            DisplayName = displayName ?? id;
        }

        public bool Equals(DeliveryPoint other)
        {
            return string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is DeliveryPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id != null ? StringComparer.Ordinal.GetHashCode(Id) : 0;
        }

        public override string ToString()
        {
            return Id;
        }

        public static bool operator ==(DeliveryPoint left, DeliveryPoint right) => left.Equals(right);

        public static bool operator !=(DeliveryPoint left, DeliveryPoint right) => !left.Equals(right);
    }
}
