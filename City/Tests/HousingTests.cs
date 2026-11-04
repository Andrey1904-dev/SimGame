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
            suite.Add("Жильё: элитный ЖК и частный дом — только плановые записи, зданий нет", FutureTypesNotBuilt);
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
            IsFalse(HousingCatalog.EliteHighRise.BuiltInCurrentQuarter, "Элитный ЖК не строится в квартале 250×250");
            IsFalse(HousingCatalog.PrivateHouse.BuiltInCurrentQuarter, "Частный дом не строится в квартале 250×250");
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

        private static void FutureTypesNotBuilt()
        {
            QuarterPlan plan = Plan();
            foreach (HousingKind kind in new[] { HousingKind.EliteHighRise, HousingKind.PrivateHouse })
            {
                RealEstateProperty property = FindByKind(plan, kind);
                IsTrue(property != null, "Плановая запись есть: " + kind);
                AreEqual(PropertyStatus.PlannedNotBuilt, property.Status, kind + ": только план");
                IsTrue(property.BuildingId == null, kind + ": здания нет");
                IsFalse(property.IsBuilt, kind + ": не построено");
                IsTrue(property.Position == null, kind + ": позиции в квартале нет");
            }
            foreach (BuildingInfo b in plan.Buildings)
            {
                IsTrue(b.Floors < 30, "В квартале нет 30-этажного ЖК (дом " + b.Id + ")");
                IsTrue(b.Floors >= 3, "Частных домов среди зданий квартала нет (дом " + b.Id + ")");
            }
        }

        private static void RegistryIntegrity()
        {
            QuarterPlan plan = Plan();
            AreEqual(4, plan.Properties.Count, "В реестре 4 записи: старт, девятиэтажка, ЖК, частный дом");
            var ids = new HashSet<string>();
            var kinds = new HashSet<HousingKind>();
            foreach (RealEstateProperty property in plan.Properties)
            {
                IsTrue(ids.Add(property.Id), "Уникальный Id недвижимости: " + property.Id);
                IsTrue(kinds.Add(property.Kind), "Один объект на тип жилья: " + property.Kind);
                AreEqual(HousingCatalog.Find(property.Kind).Id, property.HousingTypeId,
                    "HousingTypeId согласован с каталогом");
                if (property.IsBuilt)
                    IsTrue(FindBuilding(plan, property.BuildingId) != null,
                        "Построенный объект ссылается на существующее здание");
            }
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
            IsTrue(a.Contains("property.elite.highrise.future|property"), "Сериализация: плановый ЖК");
            IsTrue(a.Contains("property.private.house.future|property"), "Сериализация: плановый частный дом");
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
