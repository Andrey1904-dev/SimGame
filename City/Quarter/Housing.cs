// Life From Zero — City/Quarter module (ТЗ №024, уточнение: жильё игрока)
// Namespace: LifeFromZero.City.Quarter
//
// Расширяемая система типов жилья и реестра недвижимости игрока.
// ВАЖНО: это не система покупки. Здесь только типы, уникальные Id и
// привязка конкретных зданий квартала к вариантам жилья. Покупка,
// переезды, интерьеры элитного ЖК и частного дома — будущие задачи.
//
// Четыре варианта жилья LIFE FROM ZERO:
//   1) стартовая квартира №67 в пятиэтажке — существует в Unity, генератор
//      только вписывает её дом в город как player.home, сцену не трогает;
//   2) квартира в девятиэтажке — в будущем покупка/переезд; сейчас в городе
//      есть 9-этажные дома и один помечен как будущий объект недвижимости;
//   3) элитная квартира в ЖК 30+ этажей — поздний этап; в квартале 250×250
//      НЕ строится, есть только определение типа и плановая запись реестра;
//   4) собственный частный дом с участком — дорого; аналогично только тип
//      и плановая запись, здание в текущем квартале не строится.

using System;
using System.Collections.Generic;

namespace LifeFromZero.City.Quarter
{
    /// <summary>Вариант жилья игрока (не путать с этажностью обычных домов города).</summary>
    public enum HousingKind
    {
        /// <summary>Стартовая квартира №67 в пятиэтажке.</summary>
        StartApartment,
        /// <summary>Квартира в девятиэтажке (будущая покупка).</summary>
        ApartmentNineStorey,
        /// <summary>Элитная квартира в ЖК 30+ этажей (поздний этап, другой район).</summary>
        EliteHighRise,
        /// <summary>Собственный частный дом с участком (будущее).</summary>
        PrivateHouse
    }

    /// <summary>Статус объекта недвижимости в реестре квартала.</summary>
    public enum PropertyStatus
    {
        /// <summary>Стартовое жильё игрока (player.home).</summary>
        PlayerStartHome,
        /// <summary>Здание построено; в будущем квартиру можно будет купить.</summary>
        FuturePurchasable,
        /// <summary>Только плановая запись: здания в текущем квартале нет и не строится.</summary>
        PlannedNotBuilt
    }

    /// <summary>
    /// Определение типа жилья в каталоге. Новые варианты жилья добавляются
    /// записью в HousingCatalog — генератор и реестр переписывать не нужно.
    /// </summary>
    public sealed class HousingTypeDefinition
    {
        public HousingTypeDefinition(string id, HousingKind kind, string displayName,
                                     int typicalFloors, bool builtInCurrentQuarter)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Kind = kind;
            DisplayName = displayName ?? id;
            TypicalFloors = typicalFloors;
            BuiltInCurrentQuarter = builtInCurrentQuarter;
        }

        /// <summary>Стабильный строковый Id типа, например "housing.apartment.9".</summary>
        public string Id { get; }
        public HousingKind Kind { get; }
        public string DisplayName { get; }
        /// <summary>Характерная этажность (для элитного ЖК — 30, т.е. «30+»).</summary>
        public int TypicalFloors { get; }
        /// <summary>Строится ли такое здание в текущем квартале 250×250.</summary>
        public bool BuiltInCurrentQuarter { get; }
    }

    /// <summary>Каталог всех вариантов жилья игрока. Единая точка расширения.</summary>
    public static class HousingCatalog
    {
        public static readonly HousingTypeDefinition StartApartment =
            new HousingTypeDefinition("housing.start.apartment67", HousingKind.StartApartment,
                "Стартовая квартира №67 (пятиэтажка)", typicalFloors: 5, builtInCurrentQuarter: true);

        public static readonly HousingTypeDefinition ApartmentNineStorey =
            new HousingTypeDefinition("housing.apartment.9", HousingKind.ApartmentNineStorey,
                "Квартира в девятиэтажке", typicalFloors: 9, builtInCurrentQuarter: true);

        public static readonly HousingTypeDefinition EliteHighRise =
            new HousingTypeDefinition("housing.elite.highrise", HousingKind.EliteHighRise,
                "Элитная квартира в ЖК 30+ этажей", typicalFloors: 30, builtInCurrentQuarter: false);

        public static readonly HousingTypeDefinition PrivateHouse =
            new HousingTypeDefinition("housing.private.house", HousingKind.PrivateHouse,
                "Собственный частный дом с участком", typicalFloors: 2, builtInCurrentQuarter: false);

        public static IReadOnlyList<HousingTypeDefinition> All { get; } = new[]
        {
            StartApartment, ApartmentNineStorey, EliteHighRise, PrivateHouse
        };

        public static HousingTypeDefinition Find(HousingKind kind)
        {
            foreach (HousingTypeDefinition definition in All)
                if (definition.Kind == kind)
                    return definition;
            throw new ArgumentOutOfRangeException(nameof(kind), "Нет определения жилья для " + kind);
        }
    }

    /// <summary>
    /// Запись реестра недвижимости: уникальный Id, тип жилья, привязка к зданию
    /// (если здание построено) и статус. Покупки/переезда здесь нет.
    /// </summary>
    public sealed class RealEstateProperty
    {
        public RealEstateProperty(string id, HousingKind kind, string buildingId,
                                  Vec2? position, int apartmentNumber, PropertyStatus status)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Kind = kind;
            BuildingId = buildingId;
            Position = position;
            ApartmentNumber = apartmentNumber;
            Status = status;
        }

        /// <summary>Уникальный Id объекта недвижимости, например "player.home".</summary>
        public string Id { get; }
        public HousingKind Kind { get; }
        /// <summary>Тип жилья из каталога (строковый Id определения).</summary>
        public string HousingTypeId => HousingCatalog.Find(Kind).Id;
        /// <summary>Id здания в QuarterPlan.Buildings; null у плановых записей без здания.</summary>
        public string BuildingId { get; }
        /// <summary>Позиция здания; null у плановых записей.</summary>
        public Vec2? Position { get; }
        /// <summary>Номер квартиры, если известен (67 у стартовой); 0 — не задан.</summary>
        public int ApartmentNumber { get; }
        public PropertyStatus Status { get; }
        /// <summary>Построено ли здание в текущем квартале.</summary>
        public bool IsBuilt => BuildingId != null;
    }
}
