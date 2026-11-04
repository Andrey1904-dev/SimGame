// Life From Zero — City/Quarter module (ТЗ №031: полная карта района 750×750)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Регрессионные тесты только для нового макета District750: счётчики зданий,
// обязательные объекты, границы, детерминизм. Квартал 250×250 и его тесты не
// меняются; тест «по умолчанию остаётся quarter250» фиксирует, что старый путь жив.
// Соответствие координат Python-зеркалу City/Tools/district_layout_check.py
// проверяется отдельно, этими тестами не заменяется.

using System;
using System.Collections.Generic;
using System.Linq;
using LifeFromZero.City.Quarter;
using static LifeFromZero.City.Quarter.Tests.MiniTestRunner;

namespace LifeFromZero.City.Quarter.Tests
{
    public static class DistrictMapTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();
            suite.Add("District750: макет помечен, размер 750 м", LayoutAndSize);
            suite.Add("District750: 10 многоквартирных домов (С4, Ю3, В3) и 4–6 частных домов", BuildingCounts);
            suite.Add("District750: стартовая квартира №67 в bld-n4 (номер квартиры, не дом)", StartApartment);
            suite.Add("District750: обязательные коммерческие и публичные объекты есть", MandatoryCommercial);
            suite.Add("District750: четыре АЗС с уникальными id", FourGasStations);
            suite.Add("District750: число точек и доставок совпадает с Python-зеркалом", MirrorCounts);
            suite.Add("District750: ровно один круговой перекрёсток и заводские объекты", RoundaboutAndIndustry);
            suite.Add("District750: все объекты и точки доставки внутри границ карты", WithinBounds);
            suite.Add("District750: идентификаторы точек доставки уникальны", UniqueDeliveryIds);
            suite.Add("District750: детерминирован по seed (два прогона совпадают)", Deterministic);
            suite.Add("По умолчанию генерируется quarter250 (старый путь не сломан)", DefaultStaysQuarter250);
            return suite;
        }

        private static QuarterPlan District() => DistrictMapGenerator.Generate(1904);

        private static void LayoutAndSize()
        {
            QuarterPlan plan = District();
            AreEqual(QuarterLayoutIds.District750, plan.LayoutId, "Идентификатор макета");
            AreEqual(750f, plan.Size, "Размер карты, м");
            AreEqual(1904, plan.Seed, "Seed сохраняется");
        }

        private static void BuildingCounts()
        {
            QuarterPlan plan = District();
            int mkd = plan.Buildings.Count(b => b.Id.StartsWith("bld-n") || b.Id.StartsWith("bld-s") || b.Id.StartsWith("bld-e"));
            int priv = plan.Buildings.Count(b => b.Id.StartsWith("bld-p"));
            AreEqual(10, mkd, "Многоквартирных домов ровно 10");
            AreEqual(4, plan.Buildings.Count(b => b.Id.StartsWith("bld-n")), "Север: 4 дома");
            AreEqual(3, plan.Buildings.Count(b => b.Id.StartsWith("bld-s")), "Юг: 3 дома");
            AreEqual(3, plan.Buildings.Count(b => b.Id.StartsWith("bld-e")), "Восток: 3 дома");
            IsTrue(priv >= 4 && priv <= 6, "Частных домов 4–6, сейчас " + priv);
        }

        private static void StartApartment()
        {
            QuarterPlan plan = District();
            IsTrue(plan.PlayerHome != null, "Стартовая квартира задана");
            AreEqual("bld-n4", plan.PlayerHome.BuildingId, "Стартовая квартира — в северном доме bld-n4");
            IsTrue(plan.Buildings.Any(b => b.Id == "bld-n4"), "Дом bld-n4 существует");
            // Номер берём из данных объекта маркера, а не из константы (иначе тест не может упасть).
            PlacedObject marker = plan.OfType(QuarterObjectTypes.PlayerHomeMarker).FirstOrDefault();
            IsTrue(marker != null, "Маркер квартиры есть в данных");
            AreEqual("67", marker.Param("apartment"), "Маркер несёт номер квартиры 67");
            AreEqual("bld-n4", marker.Param("buildingId"), "Маркер привязан к bld-n4");
        }

        private static void MandatoryCommercial()
        {
            QuarterPlan plan = District();
            string[] mandatory =
            {
                "com-mall", "shop-01", "com-cafe", "com-restaurant", "com-clothing", "com-hardware",
                "com-pawnshop", "com-business", "com-incubator", "com-bank", "pvz-01",
                "com-city-hall", "com-police", "com-hospital", "com-education",
            };
            var ids = new HashSet<string>(plan.DeliveryPoints.Select(p => p.Id).Concat(plan.Buildings.Select(b => b.Id))
                .Concat(plan.DistrictPoints.Select(p => p.Id)));
            foreach (string id in mandatory)
                IsTrue(ids.Contains(id) || plan.DistrictPoints.Any(p => p.Id == id), "Нет обязательного объекта " + id);
        }

        /// <summary>Фиксирует число точек карты и доставки как в Python-зеркале (24 и 46).</summary>
        private static void MirrorCounts()
        {
            QuarterPlan plan = District();
            AreEqual(24, plan.DistrictPoints.Count, "Точек карты (как в district_layout_check.py)");
            AreEqual(46, plan.DeliveryPoints.Count, "Точек доставки (как в district_layout_check.py)");
        }

        private static void FourGasStations()
        {
            QuarterPlan plan = District();
            string[] gas = { "gas-01", "gas-02", "gas-03", "gas-04" };
            foreach (string id in gas)
                AreEqual(1, plan.DistrictPoints.Count(p => p.Id == id), "Ровно одна АЗС " + id);
            AreEqual(4, plan.DistrictPoints.Count(p => p.Type == "gas.station"), "Всего четыре АЗС");
            AreEqual(4, plan.CountOfType(DistrictObjectTypes.GasCanopy), "Четыре навеса");
        }

        private static void RoundaboutAndIndustry()
        {
            QuarterPlan plan = District();
            AreEqual(1, plan.CountOfType(DistrictObjectTypes.Roundabout), "Один круговой перекрёсток");
            IsTrue(plan.CountOfType(DistrictObjectTypes.IndustryBuilding) >= 2, "Завод и производственный корпус");
            IsTrue(plan.CountOfType(DistrictObjectTypes.IndustryLoading) >= 1, "Площадка погрузки");
        }

        private static void WithinBounds()
        {
            QuarterPlan plan = District();
            const float eps = 0.01f;
            foreach (PlacedObject obj in plan.Objects)
            {
                IsTrue(!float.IsNaN(obj.Position.X) && !float.IsNaN(obj.Position.Z), "NaN у объекта " + obj.Id);
                IsTrue(obj.Position.X >= -eps && obj.Position.X <= plan.Size + eps &&
                       obj.Position.Z >= -eps && obj.Position.Z <= plan.Size + eps,
                    "Объект вне карты: " + obj.Id);
            }
            foreach (DeliveryPointInfo dp in plan.DeliveryPoints)
            {
                IsTrue(dp.Position.X >= -eps && dp.Position.X <= plan.Size + eps &&
                       dp.Position.Z >= -eps && dp.Position.Z <= plan.Size + eps,
                    "Точка доставки вне карты: " + dp.Id);
            }
        }

        private static void UniqueDeliveryIds()
        {
            QuarterPlan plan = District();
            int total = plan.DeliveryPoints.Count;
            AreEqual(total, plan.DeliveryPoints.Select(p => p.Id).Distinct().Count(), "Id точек доставки уникальны");
            IsTrue(total > 0, "Точки доставки есть");
        }

        private static void Deterministic()
        {
            QuarterPlan a = DistrictMapGenerator.Generate(1904);
            QuarterPlan b = DistrictMapGenerator.Generate(1904);
            AreEqual(a.Objects.Count, b.Objects.Count, "Число объектов совпадает");
            AreEqual(a.Roads.Count, b.Roads.Count, "Число дорог совпадает");
            AreEqual(a.DeliveryPoints.Count, b.DeliveryPoints.Count, "Число точек доставки совпадает");
            for (int i = 0; i < a.Objects.Count; i++)
            {
                AreEqual(a.Objects[i].Id, b.Objects[i].Id, "Id объекта #" + i);
                AreEqual(a.Objects[i].Position.X, b.Objects[i].Position.X, "X объекта " + a.Objects[i].Id);
                AreEqual(a.Objects[i].Position.Z, b.Objects[i].Position.Z, "Z объекта " + a.Objects[i].Id);
            }
        }

        private static void DefaultStaysQuarter250()
        {
            QuarterPlan plan = QuarterGenerator.Generate(1904);
            AreEqual(QuarterLayoutIds.Quarter250, plan.LayoutId, "Квартал 250 по умолчанию");
            AreEqual(250f, plan.Size, "Размер квартала 250");
        }
    }
}
