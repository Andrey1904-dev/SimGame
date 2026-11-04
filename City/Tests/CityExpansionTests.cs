// Life From Zero — City/Quarter module (ТЗ №024)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Тесты развития квартала: дом игрока, этажность, точки доставки, магазин,
// улучшенные дворы, подвал с железной дверью. Только данные генератора —
// Play Mode, дверь в Unity и подвал в сцене здесь НЕ проверяются.

using System;
using System.Collections.Generic;
using LifeFromZero.City.Quarter;
using static LifeFromZero.City.Quarter.Tests.MiniTestRunner;

namespace LifeFromZero.City.Quarter.Tests
{
    public static class CityExpansionTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();
            suite.Add("Дом игрока: один, player.home, кв.67 в данных, дверь и выход во двор", PlayerHomeBasics);
            suite.Add("Дом игрока: место задаётся извне (позиция+поворот), дорога отклоняется", PlayerHomeExternalPlacement);
            suite.Add("Дом игрока: поверх него нет второго здания", PlayerHomeNotDuplicated);
            suite.Add("Магазин: один, «ПРОДУКТЫ», вход/площадка/точка shop.grocery у тротуара", ShopBasics);
            suite.Add("Магазин не пересекается с дорогами, домами и ПВЗ", ShopNoOverlaps);
            suite.Add("Точки доставки: у каждого подъезда, с Id/зданием/номером/радиусом", DeliveryPointsComplete);
            suite.Add("Точки доставки: перед входами, вне стен зданий и дорог", DeliveryPointsPlacement);
            suite.Add("Дворы: качели, песочницы, урны, контейнеры, кусты, дорожки, фонари", CourtyardsRich);
            suite.Add("Дворы различаются по наполнению", CourtyardsDiffer);
            suite.Add("Подвал: один, под трёхэтажным домом, вход/лестница/дверь снаружи", BasementBasics);
            suite.Add("Подвал: структура помещений, точки entry/table/storage/secretroom", BasementStructure);
            suite.Add("Железная дверь: состояния Closed/Open/Locked и переходы", BasementDoorStates);
            suite.Add("Повторяемость по seed со всеми новыми элементами", DeterminismWithExpansions);
            return suite;
        }

        private static QuarterPlan Plan() => QuarterGenerator.Generate(1904);

        // ---------- дом игрока ----------

        private static void PlayerHomeBasics()
        {
            QuarterPlan plan = Plan();
            IsTrue(plan.PlayerHome != null, "Должен быть дом игрока");
            AreEqual("player.home", plan.PlayerHome.Id, "Уникальный ID player.home");

            BuildingInfo home = null;
            int homes = 0;
            foreach (BuildingInfo b in plan.Buildings)
                if (b.IsPlayerHome) { homes++; home = b; }
            AreEqual(1, homes, "Ровно одно здание дома игрока");
            AreEqual(home.Id, plan.PlayerHome.BuildingId, "PlayerHome ссылается на своё здание");
            AreEqual(5, home.Floors, "Дом игрока — пятиэтажка");
            AreEqual(home.Center, plan.PlayerHome.Center, "Центр в данных совпадает со зданием");

            PlacedObject buildingObj = FindBuildingObject(plan, home.Id);
            AreEqual("player.home", buildingObj.Param("specialId"), "specialId в объекте здания");
            AreEqual("67", buildingObj.Param("apartment"), "Номер квартиры 67 — только данные, не сцена");

            // Дверь = первый подъезд здания; выход во двор — перед ней, наружу от фасада.
            AreEqual(home.Entrances[0].X, plan.PlayerHome.EntranceDoor.X, "Дверь у первого подъезда (X)");
            AreEqual(home.Entrances[0].Z, plan.PlayerHome.EntranceDoor.Z, "Дверь у первого подъезда (Z)");
            float doorToExit = plan.PlayerHome.EntranceDoor.Ground.DistanceTo(plan.PlayerHome.CourtyardExit.Ground);
            IsTrue(doorToExit >= 1.5f && doorToExit <= 2.5f, "Выход во двор в ~2 м от двери: " + doorToExit);
            IsFalse(home.Footprint.Contains(plan.PlayerHome.CourtyardExit.Ground), "Выход во двор вне стен");
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.PlayerHomeDoor), "Объект двери дома игрока");
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.PlayerHomeExit), "Объект выхода во двор");
        }

        private static void PlayerHomeExternalPlacement()
        {
            QuarterPlan baseline = Plan();
            // Берём место реального повёрнутого дома из базовой генерации — оно заведомо свободно.
            BuildingInfo donor = null;
            foreach (BuildingInfo b in baseline.Buildings)
                if (!b.IsPlayerHome && Math.Abs(Math.Abs(b.RotationY) - 90f) < 0.01f) { donor = b; break; }
            IsTrue(donor != null, "В квартале есть повёрнутые дома");

            var options = new QuarterGeneratorOptions { Seed = 1904 };
            options.PlayerHome.Position = donor.Center;
            options.PlayerHome.RotationY = 90f;
            QuarterPlan plan = new QuarterGenerator(options).Generate();
            AreEqual(donor.Center, plan.PlayerHome.Center, "Дом игрока встал на заданную позицию");
            AreEqual(90f, plan.PlayerHome.RotationY, "Поворот задан извне");

            // Позиция на дороге отклоняется, а не молча сдвигается.
            var bad = new QuarterGeneratorOptions { Seed = 1904 };
            bad.PlayerHome.Position = new Vec2(125f, 125f); // перекрёсток
            bool thrown = false;
            try { new QuarterGenerator(bad).Generate(); }
            catch (ArgumentException) { thrown = true; }
            IsTrue(thrown, "Дом игрока на дороге должен отклоняться");
        }

        private static void PlayerHomeNotDuplicated()
        {
            QuarterPlan plan = Plan();
            BuildingInfo home = null;
            foreach (BuildingInfo b in plan.Buildings)
                if (b.IsPlayerHome) home = b;
            foreach (BuildingInfo b in plan.Buildings)
            {
                if (b.Id == home.Id) continue;
                IsFalse(b.Footprint.Overlaps(home.Footprint),
                    "Поверх дома игрока стоит " + b.Id);
            }
            int markers = 0;
            foreach (PlacedObject obj in plan.Objects)
                if (obj.Param("specialId") == "player.home" && obj.TypeId == QuarterObjectTypes.Building)
                    markers++;
            AreEqual(1, markers, "Одно здание с specialId=player.home");
        }

        // ---------- магазин ----------

        private static void ShopBasics()
        {
            QuarterPlan plan = Plan();
            IsTrue(plan.Shop != null, "Должен быть магазин");
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.ShopBuilding), "Одно здание магазина");
            PlacedObject shop = SingleOf(plan, QuarterObjectTypes.ShopBuilding);
            AreEqual("1", shop.Param("floors"), "Магазин одноэтажный");
            AreEqual("ПРОДУКТЫ", SingleOf(plan, QuarterObjectTypes.ShopSign).Param("text"), "Вывеска «ПРОДУКТЫ»");
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.ShopDoor), "Дверь магазина");
            IsTrue(plan.CountOfType(QuarterObjectTypes.ShopWindow) >= 3, "Витрины >= 3");
            PlacedObject point = SingleOf(plan, QuarterObjectTypes.ShopPoint);
            AreEqual(plan.Shop.InteractionPoint, point.Position, "Точка shop.grocery в данных и объекте совпадает");
            IsTrue(plan.Shop.Plaza.Contains(plan.Shop.InteractionPoint.Ground), "Точка на площадке магазина");
            IsTrue(plan.Shop.Plaza.Contains(plan.Shop.Entrance.Ground), "Вход у площадки");

            float best = DistanceRectToRoads(plan, plan.Shop.Plaza);
            IsTrue(best <= 2.6f, "Площадка магазина примыкает к тротуару, зазор: " + best);
        }

        private static void ShopNoOverlaps()
        {
            QuarterPlan plan = Plan();
            foreach (RoadSegment road in plan.Roads)
                IsFalse(plan.Shop.BuildingFootprint.Overlaps(road.Footprint.Expanded(2.5f)),
                    "Магазин на дороге " + road.Id);
            foreach (BuildingInfo b in plan.Buildings)
                IsFalse(plan.Shop.BuildingFootprint.Expanded(1f).Overlaps(b.Footprint),
                    "Магазин пересекает дом " + b.Id);
            IsFalse(plan.Shop.BuildingFootprint.Overlaps(plan.PickupPoint.BuildingFootprint),
                "Магазин пересекает ПВЗ");
            IsFalse(plan.PickupPoint.BuildingFootprint.Overlaps(plan.Shop.BuildingFootprint),
                "ПВЗ пересекает магазин");
        }

        // ---------- точки доставки ----------

        private static void DeliveryPointsComplete()
        {
            QuarterPlan plan = Plan();
            int entrances = 0;
            foreach (BuildingInfo b in plan.Buildings) entrances += b.Entrances.Count;
            AreEqual(entrances, plan.DeliveryPoints.Count, "По точке на каждый подъезд");
            AreEqual(entrances, plan.CountOfType(QuarterObjectTypes.DeliveryDropoff), "Маркеров столько же");

            var ids = new HashSet<string>();
            var byBuilding = new Dictionary<string, BuildingInfo>();
            foreach (BuildingInfo b in plan.Buildings) byBuilding[b.Id] = b;
            foreach (DeliveryPointInfo d in plan.DeliveryPoints)
            {
                IsTrue(ids.Add(d.Id), "Уникальный Id точки: " + d.Id);
                IsTrue(byBuilding.ContainsKey(d.BuildingId), "Точка ссылается на существующее здание");
                BuildingInfo b = byBuilding[d.BuildingId];
                IsTrue(d.EntranceNumber >= 1 && d.EntranceNumber <= b.Entrances.Count,
                    "Номер подъезда в пределах здания");
                AreEqual(1.5f, d.InteractionRadius, "Радиус взаимодействия 1.5");
                AreEqual("delivery.dropoff", d.TypeId, "Тип точки delivery.dropoff");
                // Координаты точки = подъезд + ~1 м наружу от фасада.
                Vec2 entrance = b.Entrances[d.EntranceNumber - 1];
                float dist = entrance.DistanceTo(d.Position.Ground);
                IsTrue(dist >= 0.8f && dist <= 1.3f, "Точка в ~1 м перед входом: " + dist);
            }
        }

        private static void DeliveryPointsPlacement()
        {
            QuarterPlan plan = Plan();
            foreach (DeliveryPointInfo d in plan.DeliveryPoints)
            {
                foreach (BuildingInfo b in plan.Buildings)
                    IsFalse(b.Footprint.Contains(d.Position.Ground),
                        "Точка " + d.Id + " внутри стен здания " + b.Id);
                foreach (RoadSegment road in plan.Roads)
                    if (road.Kind == RoadKind.Street)
                        IsFalse(road.Footprint.Expanded(2.5f).Contains(d.Position.Ground),
                            "Точка " + d.Id + " на проезжей части/тротуаре");
                IsFalse(plan.PickupPoint.BuildingFootprint.Contains(d.Position.Ground),
                    "Точка " + d.Id + " внутри ПВЗ");
                IsFalse(plan.Shop.BuildingFootprint.Contains(d.Position.Ground),
                    "Точка " + d.Id + " внутри магазина");
            }
        }

        // ---------- дворы ----------

        private static void CourtyardsRich()
        {
            QuarterPlan plan = Plan();
            foreach (CourtyardInfo yard in plan.Courtyards)
            {
                IsTrue(yard.Swings >= 1, yard.Id + ": качели >= 1");
                IsTrue(yard.Sandboxes >= 1, yard.Id + ": песочницы >= 1");
                IsTrue(yard.Urns >= 2, yard.Id + ": урны >= 2");
                IsTrue(yard.TrashContainers >= 2, yard.Id + ": контейнеры >= 2");
                IsTrue(yard.Bushes >= 3, yard.Id + ": кусты >= 3");
                IsTrue(yard.HasFootpath, yard.Id + ": есть дорожки");
                IsTrue(yard.Benches >= 4 && yard.Trees >= 5 && yard.ParkingStalls >= 6,
                    yard.Id + ": прежнее наполнение сохранено");
            }
            AreEqual(plan.Courtyards.Count * 4, CountLampsInYards(plan), "По 4 дворовых фонаря на двор");
            IsTrue(plan.CountOfType(QuarterObjectTypes.Swing) >= 3, "Качелей в квартале >= 3");
            IsTrue(plan.CountOfType(QuarterObjectTypes.TrashContainer) >= 6, "Контейнеров >= 6");
            IsTrue(plan.CountOfType(QuarterObjectTypes.Bush) >= 9, "Кустов >= 9");
            IsTrue(plan.CountOfType(QuarterObjectTypes.Footpath) >= 3, "Дорожек >= 3");
        }

        private static int CountLampsInYards(QuarterPlan plan)
        {
            int count = 0;
            foreach (PlacedObject obj in plan.OfType(QuarterObjectTypes.YardLamp))
                if (obj.Param("courtyardId") != null) count++;
            return count;
        }

        private static void CourtyardsDiffer()
        {
            QuarterPlan plan = Plan();
            IsTrue(plan.Courtyards.Count >= 2, "Нужно минимум 2 двора для сравнения");
            var signatures = new HashSet<string>();
            foreach (CourtyardInfo y in plan.Courtyards)
                signatures.Add(y.Benches + "/" + y.Trees + "/" + y.Bushes + "/" + y.Swings + "/" +
                               y.Sandboxes + "/" + y.TrashContainers + "/" + y.ParkingStalls);
            IsTrue(signatures.Count >= 2, "Дворы должны различаться наполнением, разных сигнатур: " + signatures.Count);
        }

        // ---------- подвал ----------

        private static void BasementBasics()
        {
            QuarterPlan plan = Plan();
            IsTrue(plan.Basement != null, "Должен быть подвал");
            BuildingInfo home = FindBuilding(plan, plan.Basement.BuildingId);
            AreEqual(3, home.Floors, "Подвал — под трёхэтажным домом");

            int basementBuildings = 0;
            foreach (PlacedObject obj in plan.Objects)
                if (obj.TypeId == QuarterObjectTypes.BasementDoor) basementBuildings++;
            AreEqual(1, basementBuildings, "Одна железная дверь подвала = один подвал");

            AreEqual(0f, plan.Basement.Entrance.Y, "Вход в подвал — с уровня земли");
            AreEqual(-2.8f, plan.Basement.StairBottom.Y, "Низ лестницы на глубине подвала");
            IsFalse(home.Footprint.Contains(plan.Basement.Entrance.Ground), "Вход снаружи здания");
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementStep) >= 5, "Бетонных ступеней >= 5");
            PlacedObject door = SingleOf(plan, QuarterObjectTypes.BasementDoor);
            AreEqual("basement.door", door.Param("pointId"), "Точка взаимодействия basement.door");
            AreEqual("Locked", door.Param("state"), "Начальное состояние двери — Locked");
            AreEqual(plan.Basement.DoorPosition, door.Position, "Дверь в данных и объекте совпадает");
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementLamp) >= 3, "Лампы: над входом и внутри");
        }

        private static void BasementStructure()
        {
            QuarterPlan plan = Plan();
            BasementInfo basement = plan.Basement;
            BuildingInfo home = FindBuilding(plan, basement.BuildingId);

            AreEqual(4, basement.Points.Count, "Четыре именованные точки");
            foreach (string key in new[] { "basement.entry", "basement.table", "basement.storage", "basement.secretroom" })
            {
                IsTrue(basement.Points.ContainsKey(key), "Есть точка " + key);
                Vec3 p = basement.Points[key];
                AreEqual(-2.8f, p.Y, key + " на полу подвала");
                IsTrue(home.Footprint.Expanded(0.5f).Contains(p.Ground), key + " внутри контура дома");
            }
            // Точки различны; entry — ближайшая к двери.
            var seen = new HashSet<string>();
            foreach (KeyValuePair<string, Vec3> kv in basement.Points)
                IsTrue(seen.Add(kv.Value.X + "," + kv.Value.Z), "Точки не совпадают: " + kv.Key);
            float entryDist = basement.Points["basement.entry"].Ground.DistanceTo(basement.DoorPosition.Ground);
            IsTrue(entryDist < 2.5f, "basement.entry сразу за дверью: " + entryDist);
            foreach (KeyValuePair<string, Vec3> kv in basement.Points)
            {
                if (kv.Key == "basement.entry") continue;
                float d = kv.Value.Ground.DistanceTo(basement.DoorPosition.Ground);
                IsTrue(d > entryDist, kv.Key + " дальше от двери, чем entry");
            }

            // Геометрия интерьера присутствует объектами.
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementFloor) >= 1, "Пол подвала");
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementCeiling) >= 1, "Потолок подвала");
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementWall) >= 6, "Стены и перегородки");
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.BasementSecondDoor), "Вторая закрытая дверь");
            PlacedObject secondDoor = SingleOf(plan, QuarterObjectTypes.BasementSecondDoor);
            AreEqual("Locked", secondDoor.Param("state"), "Вторая дверь закрыта");
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementShelf) >= 2, "Стеллажи в storage");
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementCrate) >= 2, "Ящики");
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.BasementTable), "Стол");
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementChair) >= 2, "Стулья");
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementPipe) >= 1, "Трубы");

            // Секретная комната — за второй дверью: дальше от входа, чем storage/table по X-коридору.
            float secretDist = basement.Points["basement.secretroom"].Ground.DistanceTo(basement.Points["basement.entry"].Ground);
            IsTrue(secretDist > 8f, "Секретная комната в глубине подвала: " + secretDist);
        }

        private static void BasementDoorStates()
        {
            var door = new BasementDoorController();
            AreEqual(BasementDoorState.Locked, door.State, "По умолчанию Locked");
            IsFalse(door.Toggle(), "Locked не открывается взаимодействием");
            IsFalse(door.Open(), "Locked не открывается напрямую");
            IsTrue(door.Unlock(), "Unlock: Locked -> Closed");
            AreEqual(BasementDoorState.Closed, door.State, "После Unlock — Closed");
            IsTrue(door.Toggle(), "Closed -> Open по взаимодействию");
            IsTrue(door.IsOpen, "Дверь открыта");
            IsFalse(door.Lock(), "Открытую дверь нельзя запереть");
            IsTrue(door.Toggle(), "Open -> Closed");
            IsTrue(door.Lock(), "Closed -> Locked");
            IsTrue(door.Unlock() && door.Open(), "Полный цикл: Unlock+Open");
            IsTrue(door.Close(), "Close после Open");

            var closed = new BasementDoorController(BasementDoorState.Closed);
            IsTrue(closed.Open(), "Явное начальное состояние Closed открывается");
        }

        private static void DeterminismWithExpansions()
        {
            string a = QuarterPlanSerializer.Serialize(QuarterGenerator.Generate(1904));
            string b = QuarterPlanSerializer.Serialize(QuarterGenerator.Generate(1904));
            AreEqual(a, b, "Полный план (дома, магазин, ПВЗ, подвал, точки) детерминирован");
            IsTrue(a.Contains("player.home"), "Сериализация содержит дом игрока");
            IsTrue(a.Contains("basement|"), "Сериализация содержит подвал");
            IsTrue(a.Contains("dropoff"), "Сериализация содержит точки доставки");
        }

        // ---------- helpers ----------

        private static PlacedObject FindBuildingObject(QuarterPlan plan, string buildingId)
        {
            foreach (PlacedObject obj in plan.OfType(QuarterObjectTypes.Building))
                if (obj.Param("buildingId") == buildingId) return obj;
            throw new InvalidOperationException("Нет объекта здания " + buildingId);
        }

        private static BuildingInfo FindBuilding(QuarterPlan plan, string buildingId)
        {
            foreach (BuildingInfo b in plan.Buildings)
                if (b.Id == buildingId) return b;
            throw new InvalidOperationException("Нет здания " + buildingId);
        }

        private static PlacedObject SingleOf(QuarterPlan plan, string typeId)
        {
            PlacedObject found = null;
            foreach (PlacedObject obj in plan.OfType(typeId))
            {
                IsTrue(found == null, "Объект " + typeId + " должен быть один");
                found = obj;
            }
            IsTrue(found != null, "Нет объекта " + typeId);
            return found;
        }

        private static float DistanceRectToRoads(QuarterPlan plan, Rect2 rect)
        {
            float best = float.MaxValue;
            foreach (RoadSegment road in plan.Roads)
            {
                if (road.Kind != RoadKind.Street) continue;
                Rect2 f = road.Footprint;
                float dx = Math.Max(0f, Math.Max(f.MinX - rect.MaxX, rect.MinX - f.MaxX));
                float dz = Math.Max(0f, Math.Max(f.MinZ - rect.MaxZ, rect.MinZ - f.MaxZ));
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }
    }
}
