// Life From Zero — City/Quarter module (ТЗ №023, дополнение: ПВЗ)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Тесты пункта выдачи заказов: единственность, внешний вид по данным,
// вход и точка взаимодействия, доступ с тротуара, отсутствие пересечений.
// Никакой выдачи заказов/экономики здесь не тестируется — её пока нет.

using System;
using LifeFromZero.City.Quarter;
using static LifeFromZero.City.Quarter.Tests.MiniTestRunner;

namespace LifeFromZero.City.Quarter.Tests
{
    public static class PickupPointTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();
            suite.Add("ПВЗ: ровно один на квартал, есть типизированные данные", ExactlyOne);
            suite.Add("ПВЗ: одноэтажный, плоская крыша, дверь, окна, вывеска с текстом", Appearance);
            suite.Add("ПВЗ: координаты входа и точки взаимодействия согласованы с объектами", EntranceAndInteraction);
            suite.Add("ПВЗ: у дороги, площадка перед входом примыкает к тротуару", NearRoadWithPlaza);
            suite.Add("ПВЗ: не пересекается с дорогами, домами и объектами дворов", NoOverlaps);
            suite.Add("ПВЗ: детерминирован по seed и корректен на серии seed 1..10", DeterministicAndStable);
            suite.Add("ПВЗ: можно отключить опцией, остальные генерации это не ломает", CanBeDisabled);
            return suite;
        }

        private static QuarterPlan Plan() => QuarterGenerator.Generate(1904);

        private static void ExactlyOne()
        {
            QuarterPlan plan = Plan();
            IsTrue(plan.PickupPoint != null, "В плане должен быть PickupPointInfo");
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.PickupBuilding), "Одно здание ПВЗ");
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.PickupPoint), "Одна точка взаимодействия");
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.PickupPlaza), "Одна площадка перед входом");
            AreEqual("pvz-01", plan.PickupPoint.Id, "Стабильный Id ПВЗ");
        }

        private static void Appearance()
        {
            QuarterPlan plan = Plan();
            PickupPointInfo pvz = plan.PickupPoint;
            AreEqual(3.2f, pvz.Size.Y, "Высота одноэтажного ПВЗ");
            IsTrue(pvz.Size.X >= 10f && pvz.Size.Z >= 6f && pvz.Size.Z < pvz.Size.X,
                "ПВЗ небольшой и вытянутый вдоль улицы: " + pvz.Size);

            PlacedObject building = Single(plan, QuarterObjectTypes.PickupBuilding);
            AreEqual("1", building.Param("floors"), "Один этаж в параметрах здания");
            AreEqual("flat", building.Param("roof"), "Плоская крыша в параметрах здания");
            AreEqual(PickupPointInfo.SignText, building.Param("signText"), "Текст вывески в параметрах здания");

            AreEqual(1, plan.CountOfType(QuarterObjectTypes.PickupRoof), "Есть плоская крыша");
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.PickupDoor), "Есть входная дверь");
            IsTrue(plan.CountOfType(QuarterObjectTypes.PickupWindow) >= 3,
                "Окон-витрин >= 3, фактически: " + plan.CountOfType(QuarterObjectTypes.PickupWindow));
            PlacedObject sign = Single(plan, QuarterObjectTypes.PickupSign);
            AreEqual("ПУНКТ ВЫДАЧИ ЗАКАЗОВ", sign.Param("text"), "Вывеска с точным текстом");

            // Дверь и вывеска — на том же фасаде, что и вход, вывеска над дверью.
            PlacedObject door = Single(plan, QuarterObjectTypes.PickupDoor);
            IsTrue(Math.Abs(door.Position.X - pvz.Entrance.X) < 0.1f, "Дверь по X совпадает со входом");
            IsTrue(door.Position.Z > building.Position.Z == pvz.Entrance.Z > building.Position.Z ||
                   door.Position.Z < building.Position.Z == pvz.Entrance.Z < building.Position.Z,
                "Дверь и вход — с одной (уличной) стороны здания");
        }

        private static void EntranceAndInteraction()
        {
            QuarterPlan plan = Plan();
            PickupPointInfo pvz = plan.PickupPoint;

            // Координаты из типизированных данных совпадают с объектами на плане.
            PlacedObject point = Single(plan, QuarterObjectTypes.PickupPoint);
            AreEqual(pvz.InteractionPoint, point.Position, "Объект pickup.point стоит в InteractionPoint");
            AreEqual("delivery.pickup", point.Param("purpose"), "Назначение точки — будущее получение заказов");
            IsTrue(point.Param("buildingId") == pvz.Id, "Точка привязана к ПВЗ");

            PlacedObject building = Single(plan, QuarterObjectTypes.PickupBuilding);
            string[] entranceParam = building.Param("entrance").Split(',');
            IsTrue(Math.Abs(float.Parse(entranceParam[0], System.Globalization.CultureInfo.InvariantCulture) - pvz.Entrance.X) < 0.01f &&
                   Math.Abs(float.Parse(entranceParam[1], System.Globalization.CultureInfo.InvariantCulture) - pvz.Entrance.Z) < 0.01f,
                "Координаты входа продублированы в параметрах здания");

            // Точка взаимодействия — перед дверью, на площадке, в шаге от входа.
            IsTrue(pvz.Plaza.Contains(pvz.InteractionPoint.Ground), "Точка взаимодействия на площадке");
            IsTrue(pvz.Plaza.Contains(pvz.Entrance.Ground), "Вход на площадке (у её края к зданию)");
            float dist = pvz.Entrance.Ground.DistanceTo(pvz.InteractionPoint.Ground);
            IsTrue(dist >= 0.8f && dist <= 2.5f, "Точка взаимодействия в 0.8–2.5 м от входа, фактически: " + dist);
            IsFalse(pvz.BuildingFootprint.Contains(pvz.InteractionPoint.Ground),
                "Точка взаимодействия не внутри здания");
        }

        private static void NearRoadWithPlaza()
        {
            QuarterPlan plan = Plan();
            PickupPointInfo pvz = plan.PickupPoint;

            // Площадка выходит к коридору улицы (проезжая часть + тротуар):
            // расстояние от края площадки до ближайшей проезжей части <= ширины тротуара + допуск.
            float best = float.MaxValue;
            foreach (RoadSegment road in plan.Roads)
            {
                if (road.Kind != RoadKind.Street) continue;
                Rect2 f = road.Footprint;
                // Расстояние между прямоугольниками площадки и дороги.
                float dx = Math.Max(0f, Math.Max(f.MinX - pvz.Plaza.MaxX, pvz.Plaza.MinX - f.MaxX));
                float dz = Math.Max(0f, Math.Max(f.MinZ - pvz.Plaza.MaxZ, pvz.Plaza.MinZ - f.MaxZ));
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dz * dz));
            }
            IsTrue(best <= 2.6f, "Площадка ПВЗ примыкает к тротуару у дороги, зазор: " + best);

            // И вход недалеко от улицы: тротуар (2.5) + площадка (4) + запас.
            float entranceToRoad = float.MaxValue;
            foreach (RoadSegment road in plan.Roads)
            {
                if (road.Kind != RoadKind.Street) continue;
                Rect2 f = road.Footprint;
                float dx = Math.Max(0f, Math.Max(f.MinX - pvz.Entrance.X, pvz.Entrance.X - f.MaxX));
                float dz = Math.Max(0f, Math.Max(f.MinZ - pvz.Entrance.Z, pvz.Entrance.Z - f.MaxZ));
                entranceToRoad = Math.Min(entranceToRoad, (float)Math.Sqrt(dx * dx + dz * dz));
            }
            IsTrue(entranceToRoad <= 7.5f, "Вход ПВЗ в пределах тротуар+площадка от проезжей части: " + entranceToRoad);
        }

        private static void NoOverlaps()
        {
            QuarterPlan plan = Plan();
            PickupPointInfo pvz = plan.PickupPoint;

            foreach (RoadSegment road in plan.Roads)
                IsFalse(pvz.BuildingFootprint.Overlaps(road.Footprint.Expanded(2.5f)),
                    "ПВЗ пересекает дорогу/тротуар " + road.Id);
            foreach (BuildingInfo b in plan.Buildings)
            {
                IsFalse(pvz.BuildingFootprint.Expanded(1f).Overlaps(b.Footprint),
                    "ПВЗ пересекает дом " + b.Id);
                IsFalse(pvz.Plaza.Overlaps(b.Footprint), "Площадка ПВЗ пересекает дом " + b.Id);
            }
            // Чужие объекты (не детали самого ПВЗ) не стоят на здании и площадке.
            foreach (PlacedObject obj in plan.Objects)
            {
                if (obj.TypeId.StartsWith("pickup.", StringComparison.Ordinal)) continue;
                if (obj.TypeId == QuarterObjectTypes.Sidewalk || obj.TypeId == QuarterObjectTypes.Road ||
                    obj.TypeId == QuarterObjectTypes.CenterLine) continue; // плоскостные слои улиц
                IsFalse(pvz.BuildingFootprint.Overlaps(obj.Footprint),
                    "Объект " + obj.Id + " (" + obj.TypeId + ") пересекает здание ПВЗ");
            }
            // Точка взаимодействия не занята другим объектом-«мебелью».
            foreach (PlacedObject obj in plan.Objects)
            {
                if (obj.TypeId.StartsWith("pickup.", StringComparison.Ordinal)) continue;
                IsFalse(Rect2.FromCenterSize(pvz.InteractionPoint.Ground, 0.8f, 0.8f).Overlaps(obj.Footprint) &&
                        (obj.TypeId == QuarterObjectTypes.Bench || obj.TypeId == QuarterObjectTypes.Tree ||
                         obj.TypeId == QuarterObjectTypes.StreetLamp || obj.TypeId == QuarterObjectTypes.YardLamp),
                    "Точка взаимодействия занята объектом " + obj.Id);
            }
        }

        private static void DeterministicAndStable()
        {
            string a = QuarterPlanSerializer.Serialize(QuarterGenerator.Generate(7));
            string b = QuarterPlanSerializer.Serialize(QuarterGenerator.Generate(7));
            AreEqual(a, b, "План с ПВЗ детерминирован по seed");
            IsTrue(a.Contains("pvz-01|pickup"), "Сериализация содержит вход и точку ПВЗ");

            for (int seed = 1; seed <= 10; seed++)
            {
                QuarterPlan plan = QuarterGenerator.Generate(seed);
                IsTrue(plan.PickupPoint != null, "seed=" + seed + ": ПВЗ есть");
                AreEqual(1, plan.CountOfType(QuarterObjectTypes.PickupBuilding), "seed=" + seed + ": один ПВЗ");
                foreach (RoadSegment road in plan.Roads)
                    if (road.Kind == RoadKind.Street)
                        IsFalse(plan.PickupPoint.BuildingFootprint.Overlaps(road.Footprint.Expanded(2.5f)),
                            "seed=" + seed + ": ПВЗ на дороге");
                foreach (BuildingInfo building in plan.Buildings)
                    IsFalse(plan.PickupPoint.BuildingFootprint.Expanded(1f).Overlaps(building.Footprint),
                        "seed=" + seed + ": ПВЗ пересекает дом " + building.Id);
            }
        }

        private static void CanBeDisabled()
        {
            var options = new QuarterGeneratorOptions { Seed = 1904, IncludePickupPoint = false };
            QuarterPlan plan = new QuarterGenerator(options).Generate();
            IsTrue(plan.PickupPoint == null, "Опция выключает ПВЗ");
            AreEqual(0, plan.CountOfType(QuarterObjectTypes.PickupBuilding), "Объектов ПВЗ нет");
            IsTrue(RoadGraph.IsConnected(plan), "Граф по-прежнему связный");
            IsTrue(plan.Buildings.Count >= 20, "Застройка по-прежнему на месте");
        }

        private static PlacedObject Single(QuarterPlan plan, string typeId)
        {
            PlacedObject found = null;
            foreach (PlacedObject obj in plan.OfType(typeId))
            {
                IsTrue(found == null, "Объект типа " + typeId + " должен быть один");
                found = obj;
            }
            IsTrue(found != null, "Нет объекта типа " + typeId);
            return found;
        }
    }
}
