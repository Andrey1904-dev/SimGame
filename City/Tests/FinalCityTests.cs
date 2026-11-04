// Life From Zero — City/Quarter module (ТЗ №025: финальная застройка)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Финальная застройка квартала: ровно 15 жилых зданий (3×2, 5×3 включая
// дом игрока, 9×3 включая будущую квартиру, 10×3, 2 элитных ЖК 30–35
// этажей, 2 частных дома), внешняя геометрия ЖК и частных участков,
// реестр недвижимости привязан к реальным зданиям.

using System.Collections.Generic;
using LifeFromZero.City.Quarter;
using static LifeFromZero.City.Quarter.Tests.MiniTestRunner;

namespace LifeFromZero.City.Quarter.Tests
{
    public static class FinalCityTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();
            suite.Add("Финал: ровно 15 жилых зданий с точным составом по этажности", ExactComposition);
            suite.Add("Финал: два разных элитных ЖК с территорией и парковкой", TwoDistinctElite);
            suite.Add("Финал: два разных частных дома с участками, заборами и воротами", TwoDistinctPrivate);
            suite.Add("Финал: объекты территории не внутри зданий, здания не пересекаются", SpecialAreasClean);
            suite.Add("Финал: стартовый дом, девятиэтажка, подвал и доставка сохранены", KeptAnchors);
            return suite;
        }

        private static QuarterPlan Plan() => QuarterGenerator.Generate(1904);

        private static void ExactComposition()
        {
            QuarterPlan plan = Plan();
            AreEqual(15, plan.Buildings.Count, "Всего жилых зданий ровно 15");
            var byFloors = new Dictionary<int, int>();
            foreach (BuildingInfo b in plan.Buildings)
            {
                byFloors.TryGetValue(b.Floors, out int n);
                byFloors[b.Floors] = n + 1;
            }
            AreEqual(2, byFloors[3], "3-этажных: 2");
            AreEqual(3, byFloors[5], "5-этажных: 3 (с домом игрока)");
            AreEqual(3, byFloors[9], "9-этажных: 3 (с будущей квартирой)");
            AreEqual(3, byFloors[10], "10-этажных: 3");
            AreEqual(2, byFloors[2], "Частных домов: 2");
            int elite = 0;
            foreach (BuildingInfo b in plan.Buildings)
                if (b.Floors >= 30 && b.Floors <= 35) elite++;
            AreEqual(2, elite, "Элитных ЖК: 2");
            // ПВЗ и магазин в 15 жилых не входят.
            IsTrue(plan.PickupPoint != null, "ПВЗ вне счёта жилых зданий");
            IsTrue(plan.Shop != null, "Магазин вне счёта жилых зданий");
        }

        private static void TwoDistinctElite()
        {
            QuarterPlan plan = Plan();
            var elite = new List<BuildingInfo>();
            foreach (BuildingInfo b in plan.Buildings)
                if (b.Floors >= 30 && b.Floors <= 35) elite.Add(b);
            AreEqual(2, elite.Count, "Два элитных ЖК");
            IsTrue(elite[0].VariantId != elite[1].VariantId || elite[0].Floors != elite[1].Floors,
                "ЖК различаются: " + elite[0].VariantId + "/" + elite[0].Floors +
                " vs " + elite[1].VariantId + "/" + elite[1].Floors);
            bool glass = elite[0].VariantId == "elite.glass" || elite[1].VariantId == "elite.glass";
            bool premium = elite[0].VariantId == "elite.premium" || elite[1].VariantId == "elite.premium";
            IsTrue(glass && premium, "Один ЖК стеклянный, другой премиальный комбинированный");

            AreEqual(2, plan.CountOfType(QuarterObjectTypes.EliteLobby), "Лобби снаружи у каждого ЖК");
            AreEqual(2, plan.CountOfType(QuarterObjectTypes.EliteCanopy), "Козырьки входных групп");
            IsTrue(plan.CountOfType(QuarterObjectTypes.ParkingStall) >= 10,
                "Парковочные места у ЖК: " + plan.CountOfType(QuarterObjectTypes.ParkingStall));

            int lawns = 0;
            foreach (PlacedObject o in plan.Objects)
                if (o.TypeId == QuarterObjectTypes.Lawn &&
                    (o.Param("buildingId") == elite[0].Id || o.Param("buildingId") == elite[1].Id)) lawns++;
            IsTrue(lawns >= 2, "Газоны территории ЖК: " + lawns);
        }

        private static void TwoDistinctPrivate()
        {
            QuarterPlan plan = Plan();
            var priv = new List<BuildingInfo>();
            foreach (BuildingInfo b in plan.Buildings)
                if (b.Floors == 2) priv.Add(b);
            AreEqual(2, priv.Count, "Два частных дома");
            IsTrue(priv[0].VariantId != priv[1].VariantId,
                "Частные дома различаются: " + priv[0].VariantId + " vs " + priv[1].VariantId);

            foreach (BuildingInfo b in priv)
            {
                int fences = 0; bool gate = false, lawn = false, path = false;
                foreach (PlacedObject o in plan.Objects)
                {
                    if (o.Param("buildingId") != b.Id) continue;
                    if (o.TypeId == QuarterObjectTypes.PrivateFence) fences++;
                    else if (o.TypeId == QuarterObjectTypes.PrivateGate) gate = true;
                    else if (o.TypeId == QuarterObjectTypes.Lawn) lawn = true;
                    else if (o.TypeId == QuarterObjectTypes.PrivatePath) path = true;
                }
                IsTrue(fences >= 4, b.Id + ": забор участка (" + fences + " секций)");
                IsTrue(gate, b.Id + ": ворота");
                IsTrue(lawn, b.Id + ": газон участка");
                IsTrue(path, b.Id + ": дорожка к двери");
                IsTrue(b.Entrances.Count >= 1, b.Id + ": вход");
            }
            IsTrue(plan.CountOfType(QuarterObjectTypes.PrivateGarage) >= 1, "У одного из домов гараж");
            IsTrue(plan.CountOfType(QuarterObjectTypes.PrivateGarden) >= 1, "У другого дома сад");
        }

        private static void SpecialAreasClean()
        {
            QuarterPlan plan = Plan();
            foreach (PlacedObject o in plan.Objects)
            {
                bool special = o.TypeId == QuarterObjectTypes.EliteLobby ||
                               o.TypeId == QuarterObjectTypes.PrivateGarage ||
                               o.TypeId == QuarterObjectTypes.PrivateGarden ||
                               o.TypeId == QuarterObjectTypes.PrivateGate;
                if (!special) continue;
                var p = new Vec2(o.Position.X, o.Position.Z);
                foreach (BuildingInfo b in plan.Buildings)
                    IsFalse(b.Footprint.Contains(p),
                        o.TypeId + " " + o.Id + " внутри здания " + b.Id);
            }
            for (int i = 0; i < plan.Buildings.Count; i++)
                for (int j = i + 1; j < plan.Buildings.Count; j++)
                    IsFalse(plan.Buildings[i].Footprint.Overlaps(plan.Buildings[j].Footprint),
                        "Пересекаются " + plan.Buildings[i].Id + " и " + plan.Buildings[j].Id);
        }

        private static void KeptAnchors()
        {
            QuarterPlan plan = Plan();
            IsTrue(plan.PlayerHome != null, "Стартовый дом есть");
            BuildingInfo home = null, nine = null, basementHost = null;
            foreach (BuildingInfo b in plan.Buildings)
            {
                if (b.Id == plan.PlayerHome.BuildingId) home = b;
                if (plan.Basement != null && b.Id == plan.Basement.BuildingId) basementHost = b;
            }
            IsTrue(home != null, "Здание стартового дома существует");
            AreEqual(5, home.Floors, "Стартовый дом — пятиэтажка");
            AreEqual(67, PlayerHomeInfo.ApartmentNumber, "Квартира №67");

            RealEstateProperty future = null, start = null;
            foreach (RealEstateProperty p in plan.Properties)
            {
                if (p.Kind == HousingKind.ApartmentNineStorey) future = p;
                if (p.Kind == HousingKind.StartApartment) start = p;
            }
            IsTrue(future != null, "Будущая квартира в реестре");
            foreach (BuildingInfo b in plan.Buildings)
                if (b.Id == future.BuildingId) nine = b;
            IsTrue(nine != null, "Здание будущей квартиры существует");
            AreEqual(9, nine.Floors, "Будущая квартира — в девятиэтажке");
            IsTrue(start != null, "Стартовая квартира в реестре");
            AreEqual(home.Id, start.BuildingId, "Старт привязан к дому игрока");

            IsTrue(plan.Basement != null, "Подвал сохранён");
            IsTrue(basementHost != null, "Подвал под существующим зданием");

            // Доставка: точки у входов всех жилых домов.
            var homes = new HashSet<string>();
            foreach (DeliveryPointInfo d in plan.DeliveryPoints) homes.Add(d.BuildingId);
            AreEqual(15, homes.Count, "У каждого из 15 жилых домов есть точки доставки");
        }
    }
}
