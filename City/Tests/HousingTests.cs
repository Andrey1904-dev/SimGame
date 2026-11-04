// Life From Zero — City/Quarter module (ТЗ №024, уточнение: жильё игрока)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Тесты системы жилья: каталог из 4 вариантов, реестр недвижимости,
// девятиэтажка как отдельный архетип, плановые записи без зданий.
// Покупка/переезды/интерьеры здесь не тестируются — их нет.

using System.Collections.Generic;
using LifeFromZero.City.Quarter;
using static LifeFromZero.City.Quarter.Tests.MiniTestRunner;

namespace LifeFromZero.City.Quarter.Tests
{
    public static class HousingTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();
            suite.Add("Жильё: каталог содержит 4 варианта с уникальными Id", CatalogHasFourTypes);
            suite.Add("Жильё: стартовая квартира — player.home в пятиэтажке, кв.67", StartApartmentRegistered);
            suite.Add("Жильё: квартира в девятиэтажке — будущий объект на реальном 9-этажном доме", NineStoreyPropertyRegistered);
            suite.Add("Жильё: элитные ЖК и частные дома построены и привязаны к зданиям (ТЗ №025)", FutureTypesBuilt);
            suite.Add("Жильё: Id объектов недвижимости уникальны, система не привязана к пятиэтажке", RegistryIntegrity);
            suite.Add("Жильё: реестр детерминирован по seed", RegistryDeterministic);
            return suite;
        }

        private static QuarterPlan Plan() => QuarterGenerator.Generate(1904);

        private static void CatalogHasFourTypes()
        {
            AreEqual(4, HousingCatalog.All.Count, "Четыре варианта жилья");
            var ids = new HashSet<string>();
            var kinds = new HashSet<HousingKind>();
            foreach (HousingTypeDefinition def in HousingCatalog.All)
            {
                IsTrue(ids.Add(def.Id), "Уникальный Id типа: " + def.Id);
                IsTrue(kinds.Add(def.Kind), "Уникальный kind: " + def.Kind);
                AreEqual(def, HousingCatalog.Find(def.Kind), "Find возвращает то же определение");
            }
            AreEqual(5, HousingCatalog.StartApartment.TypicalFloors, "Старт — пятиэтажка");
            AreEqual(9, HousingCatalog.ApartmentNineStorey.TypicalFloors, "Девятиэтажка");
            IsTrue(HousingCatalog.EliteHighRise.TypicalFloors >= 30, "Элитный ЖК — 30+ этажей");
            IsTrue(HousingCatalog.EliteHighRise.BuiltInCurrentQuarter, "Элитные ЖК строятся в квартале с ТЗ №025 (2 комплекса)");
            IsTrue(HousingCatalog.PrivateHouse.BuiltInCurrentQuarter, "Частные дома строятся в квартале с ТЗ №025 (2 участка)");
            IsTrue(HousingCatalog.StartApartment.BuiltInCurrentQuarter, "Стартовая квартира — в квартале");
            IsTrue(HousingCatalog.ApartmentNineStorey.BuiltInCurrentQuarter, "Девятиэтажки — в квартале");
        }

        private static void StartApartmentRegistered()
        {
            QuarterPlan plan = Plan();
            RealEstateProperty start = FindByKind(plan, HousingKind.StartApartment);
            IsTrue(start != null, "В реестре есть стартовая квартира");
            AreEqual("player.home", start.Id, "Id стартового жилья — player.home");
            AreEqual(PropertyStatus.PlayerStartHome, start.Status, "Статус стартового дома");
            AreEqual(67, start.ApartmentNumber, "Квартира №67");
            AreEqual("housing.start.apartment67", start.HousingTypeId, "Тип из каталога");
            IsTrue(start.IsBuilt, "Здание построено");
            AreEqual(plan.PlayerHome.BuildingId, start.BuildingId, "Привязка к дому игрока");

            BuildingInfo building = FindBuilding(plan, start.BuildingId);
            IsTrue(building.IsPlayerHome, "Здание помечено как дом игрока");
            AreEqual(5, building.Floors, "Стартовый дом — пятиэтажка");
        }

        private static void NineStoreyPropertyRegistered()
        {
            QuarterPlan plan = Plan();
            // Девятиэтажки есть в городе как обычные дома (отдельный архетип).
            int nineCount = 0;
            foreach (BuildingInfo b in plan.Buildings)
                if (b.Floors == 9)
                {
                    nineCount++;
                    AreEqual("building.panel9", b.ArchetypeId, "Девятиэтажка — архетип building.panel9");
                }
            IsTrue(nineCount >= 1, "В квартале есть девятиэтажки, фактически: " + nineCount);

            RealEstateProperty future = FindByKind(plan, HousingKind.ApartmentNineStorey);
            IsTrue(future != null, "В реестре есть будущая квартира в девятиэтажке");
            AreEqual(PropertyStatus.FuturePurchasable, future.Status, "Статус — будущая покупка (покупки пока нет)");
            IsTrue(future.IsBuilt, "Здание девятиэтажки построено");
            BuildingInfo building = FindBuilding(plan, future.BuildingId);
            AreEqual(9, building.Floors, "Объект привязан именно к 9-этажному дому");
            IsFalse(building.IsPlayerHome, "Это не стартовый дом игрока");
            IsTrue(future.Position.HasValue, "У объекта есть координаты");
            AreEqual(building.Center, future.Position.Value, "Координаты = центр здания");
        }

        private static void FutureTypesBuilt()
        {
            QuarterPlan plan = Plan();
            // ТЗ №025: два ЖК и два частных дома — реальные здания с записями реестра.
            int elite = 0, priv = 0;
            foreach (RealEstateProperty property in plan.Properties)
            {
                if (property.Kind == HousingKind.EliteHighRise)
                {
                    elite++;
                    AreEqual(PropertyStatus.FuturePurchasable, property.Status,
                        "EliteHighRise: будущая покупка (" + property.Id + ")");
                    BuildingInfo b = FindBuilding(plan, property.BuildingId);
                    IsTrue(b != null, property.Id + ": здание существует");
                    IsTrue(b.Floors >= 30 && b.Floors <= 35,
                        property.Id + ": ЖК 30–35 этажей, фактически " + b.Floors);
                    IsTrue(property.Position.HasValue, property.Id + ": позиция задана");
                }
                else if (property.Kind == HousingKind.PrivateHouse)
                {
                    priv++;
                    AreEqual(PropertyStatus.FuturePurchasable, property.Status,
                        "PrivateHouse: будущая покупка (" + property.Id + ")");
                    BuildingInfo b = FindBuilding(plan, property.BuildingId);
                    IsTrue(b != null, property.Id + ": здание существует");
                    AreEqual(2, b.Floors, property.Id + ": частный дом двухэтажный");
                    IsTrue(property.Position.HasValue, property.Id + ": позиция задана");
                }
                AreNotEqual(PropertyStatus.PlannedNotBuilt, property.Status,
                    property.Id + ": в квартале всё построено");
            }
            AreEqual(2, elite, "Два элитных ЖК в реестре");
            AreEqual(2, priv, "Два частных дома в реестре");
        }

        private static void RegistryIntegrity()
        {
            QuarterPlan plan = Plan();
            AreEqual(6, plan.Properties.Count, "В реестре 6 записей: старт, девятиэтажка, 2 ЖК, 2 частных дома");
            var ids = new HashSet<string>();
            var kinds = new Dictionary<HousingKind, int>();
            foreach (RealEstateProperty property in plan.Properties)
            {
                IsTrue(ids.Add(property.Id), "Уникальный Id недвижимости: " + property.Id);
                kinds.TryGetValue(property.Kind, out int kn);
                kinds[property.Kind] = kn + 1;
                AreEqual(HousingCatalog.Find(property.Kind).Id, property.HousingTypeId,
                    "HousingTypeId согласован с каталогом");
                if (property.IsBuilt)
                    IsTrue(FindBuilding(plan, property.BuildingId) != null,
                        "Построенный объект ссылается на существующее здание");
            }
            AreEqual(1, kinds[HousingKind.StartApartment], "Один стартовый дом");
            AreEqual(1, kinds[HousingKind.ApartmentNineStorey], "Одна квартира в девятиэтажке");
            AreEqual(2, kinds[HousingKind.EliteHighRise], "Два элитных ЖК");
            AreEqual(2, kinds[HousingKind.PrivateHouse], "Два частных дома");
            // Система не привязана к пятиэтажке: типы различаются этажностью каталога.
            IsTrue(HousingCatalog.Find(HousingKind.StartApartment).TypicalFloors !=
                   HousingCatalog.Find(HousingKind.ApartmentNineStorey).TypicalFloors,
                "Старт и девятиэтажка — разные типы, а не один «текущий дом»");
        }

        private static void RegistryDeterministic()
        {
            string a = QuarterPlanSerializer.Serialize(QuarterGenerator.Generate(1904));
            string b = QuarterPlanSerializer.Serialize(QuarterGenerator.Generate(1904));
            AreEqual(a, b, "Реестр недвижимости детерминирован по seed");
            IsTrue(a.Contains("player.home|property|kind=StartApartment"), "Сериализация: стартовая квартира");
            IsTrue(a.Contains("property.apartment9.01|property"), "Сериализация: будущая девятиэтажка");
            IsTrue(a.Contains("property.elite.01|property"), "Сериализация: первый ЖК");
            IsTrue(a.Contains("property.private.01|property"), "Сериализация: первый частный дом");
        }

        private static RealEstateProperty FindByKind(QuarterPlan plan, HousingKind kind)
        {
            foreach (RealEstateProperty property in plan.Properties)
                if (property.Kind == kind) return property;
            return null;
        }

        private static BuildingInfo FindBuilding(QuarterPlan plan, string buildingId)
        {
            foreach (BuildingInfo b in plan.Buildings)
                if (b.Id == buildingId) return b;
            return null;
        }
    }
}
