// Life From Zero — City/Quarter/DistrictMap (ТЗ №031)
// Namespace: LifeFromZero.City.Quarter
//
// Типы данных карты игрового района 750×750 м (раскладка «District750»).
// Тот же контракт QuarterPlan: объекты, дороги, узлы, здания, точки доставки.
// Здесь — только идентификаторы и точки взаимодействия. Экономики, заказов,
// интерьеров и механик нет.

using System;
using System.Collections.Generic;

namespace LifeFromZero.City.Quarter
{

    /// <summary>Идентификаторы раскладки в QuarterPlan.LayoutId.</summary>
    public static class QuarterLayoutIds
    {
        public const string Quarter250 = "quarter250";
        public const string District750 = "district750";
    }

    /// <summary>Типы объектов, которых нет в QuarterObjectTypes (только карта района).</summary>
    public static class DistrictObjectTypes
    {
        public const string Commerce = "district.commerce";          // коммерческое здание (параметр facadeColor, name)
        public const string Sign = "district.sign";                  // вывеска фасада (параметр text)
        public const string Door = "district.door";                  // дверь коммерческого входа
        public const string Roundabout = "district.roundabout";      // круговой перекрёсток (диск)
        public const string GasCanopy = "gas.canopy";                // навес АЗС
        public const string GasPost = "gas.post";                    // стойка навеса
        public const string GasIsland = "gas.island";                // островок с колонками
        public const string GasPump = "gas.pump";                    // топливная колонка (только геометрия)
        public const string GasKiosk = "gas.kiosk";                  // кассовый киоск АЗС
        public const string IndustryBuilding = "industry.building";  // корпус завода/производства (facadeColor, name)
        public const string IndustryChimney = "industry.chimney";    // дымовая труба
        public const string IndustryLoading = "industry.loading";    // площадка погрузки
    }

    /// <summary>
    /// Точка карты района: коммерческое здание, АЗС или производство.
    /// Id уникален в карте; Entrances — входы (для АЗС — въезды);
    /// InteractionPoint — точка взаимодействия/доставки (null, если её нет).
    /// </summary>
    public sealed class DistrictPointInfo
    {
        public DistrictPointInfo(string id, string type, string name, Vec2 center, Rect2 footprint,
                                 IReadOnlyList<Vec3> entrances, Vec3? interactionPoint)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Name = name ?? string.Empty;
            Center = center;
            Footprint = footprint;
            Entrances = entrances ?? new Vec3[0];
            InteractionPoint = interactionPoint;
        }

        public string Id { get; }
        /// <summary>Тип: mall, grocery, cafe, restaurant, clothing, hardware, pawnshop, pickup, bank, city.hall,
        /// police, hospital, education, business.center, incubator, car.service, tuning, warehouse,
        /// factory, industry.warehouse, gas.station.</summary>
        public string Type { get; }
        public string Name { get; }
        public Vec2 Center { get; }
        public Rect2 Footprint { get; }
        public IReadOnlyList<Vec3> Entrances { get; }
        public Vec3? InteractionPoint { get; }
    }
}
