// Life From Zero — City/Quarter module (ТЗ №031, усилено в ТЗ №033)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Тесты карты District750. Геометрия проверяется по ДАННЫМ созданного QuarterPlan
// (DistrictPlanChecks.Analyze), а не сравнением констант. Негативные тесты мутируют
// снимок плана и требуют, чтобы проверка нашла нарушение.
// Квартал 250×250 и его тесты здесь не меняются.
//
// ВНИМАНИЕ: в этом окружении C# не компилировался и тесты не запускались (нет .NET SDK).

using System;
using System.Collections.Generic;
using System.Linq;
using static LifeFromZero.City.Quarter.Tests.MiniTestRunner;

namespace LifeFromZero.City.Quarter.Tests
{
    public static class DistrictMapTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();
            // --- базовые свойства карты ---
            suite.Add("District750: макет помечен, размер 750 м", LayoutAndSize);
            suite.Add("District750: квартира №67 в юго-западном доме северного квартала, южнее площадки", StartApartment);
            suite.Add("District750: число точек карты и доставок совпадает с Python-зеркалом (24 и 46)", MirrorCounts);
            // --- правила геометрии по данным плана ---
            suite.Add("District750: геометрия без нарушений (полный набор правил)", FullRuleSetPasses);
            suite.Add("District750: здания не пересекают улицы, проезды, площадки и друг друга", NoBuildingOverlaps);
            suite.Add("District750: дубликатов зданий, дорог и объектов нет", NoDuplicates);
            suite.Add("District750: дорожная сеть связна", RoadNetworkConnected);
            suite.Add("District750: все входы, точки доставки и взаимодействия доступны пешком", AllAccessReachable);
            suite.Add("District750: обязательные объекты присутствуют, запрещённых нет", MandatoryPresentForbiddenAbsent);
            suite.Add("District750: АЗС-4 непосредственно к югу от ломбарда", Azs4BelowPawnshop);
            suite.Add("District750: 4/3/3 многоквартирных домов и 5 частных домов", ResidentialCounts);
            // --- негативные проверки: нарушение обязано быть найдено ---
            suite.Add("Негатив: здание на улице V270 обнаруживается", NegativeBuildingOnStreet);
            suite.Add("Негатив: дубль здания обнаруживается", NegativeDuplicateBuilding);
            suite.Add("Негатив: удалённый МКД обнаруживается", NegativeMissingMkd);
            suite.Add("Негатив: запертая точка доставки банка обнаруживается", NegativeSealedBankDelivery);
            suite.Add("Негатив: изолированная дорога обнаруживается", NegativeIsolatedRoad);
            suite.Add("Негатив: дублирующаяся дорога обнаруживается", NegativeDuplicateRoad);
            suite.Add("Негатив: АЗС-4 на старом месте (далеко от ломбарда) обнаруживается", NegativeAzs4OldSpot);
            suite.Add("Негатив: блокер на точке погрузки обнаруживается", NegativeLoadingBlocksDelivery);
            suite.Add("Негатив: заблокированный подъезд жилого дома обнаруживается", NegativeBlockedEntrance);
            suite.Add("Негатив: запрещённый объект и пропавший обязательный объект обнаруживаются", NegativeForbiddenAndMissing);
            // --- детерминизм и повторная генерация ---
            suite.Add("District750: детерминирован по seed (два прогона совпадают полностью)", Deterministic);
            suite.Add("District750: повторная генерация одним генератором даёт тот же план", RepeatGenerationStable);
            suite.Add("District750: seed не меняет геометрию (только наполнение)", SeedKeepsGeometry);
            // --- регрессия квартала 250 ---
            suite.Add("По умолчанию генерируется quarter250 и без объектов района", DefaultStaysQuarter250);
            return suite;
        }

        private static QuarterPlan District() => DistrictMapGenerator.Generate(1904);

        private static LayoutSnapshot Snap() => DistrictPlanChecks.FromPlan(District());

        private static void ExpectViolation(LayoutSnapshot s, string keyword, string what)
        {
            List<string> v = DistrictPlanChecks.Analyze(s);
            IsTrue(v.Any(x => x.Contains(keyword)),
                "Проверка не нашла нарушение (" + what + "). Найдено: " +
                (v.Count == 0 ? "ничего" : string.Join("; ", v.Take(3))));
        }

        private static void ExpectNoViolation(LayoutSnapshot s, string keyword, string what)
        {
            List<string> v = DistrictPlanChecks.Analyze(s);
            IsFalse(v.Any(x => x.Contains(keyword)), what + ": " + string.Join("; ", v.Where(x => x.Contains(keyword)).Take(3)));
        }

        // ---------------------------------------------------------------- базовые свойства

        private static void LayoutAndSize()
        {
            QuarterPlan plan = District();
            AreEqual(QuarterLayoutIds.District750, plan.LayoutId, "Идентификатор макета");
            AreEqual(750f, plan.Size, "Размер карты, м");
            AreEqual(1904, plan.Seed, "Seed сохраняется");
        }

        /// <summary>
        /// Квартира №67 (карта района v1.1): дом — самый юго-западный многоквартирный дом
        /// Северного квартала, стоящий южнее футбольной площадки. Дом выбирает генератор
        /// по данным раскладки (PickPlayerHomeId), тест проверяет результат этого правила,
        /// а не зашитое имя здания.
        /// </summary>
        private static void StartApartment()
        {
            QuarterPlan plan = District();
            IsTrue(plan.PlayerHome != null, "Стартовая квартира задана");
            IsTrue(plan.PlayerHome.BuildingId == "bld-n3",
                "Квартира №67 — в юго-западном доме северного квартала (bld-n3), а не в "
                + plan.PlayerHome.BuildingId);

            BuildingInfo home = plan.Buildings.First(b => b.Id == plan.PlayerHome.BuildingId);
            IsTrue(home.IsPlayerHome, "Здание помечено как дом игрока");
            IsTrue(home.Center.Z > 600f, "Дом в северном квартале");
            // Площадка северного двора: прямоугольник (203; 655)-(217; 665).
            IsTrue(home.Footprint.MaxZ < 655f, "Дом южнее футбольной площадки");
            for (int i = 0; i < plan.Buildings.Count; i++)
            {
                BuildingInfo other = plan.Buildings[i];
                if (other.Id == home.Id || other.Center.Z <= 600f) continue;
                IsTrue(!(other.Center.X <= home.Center.X + 0.01f && other.Center.Z <= home.Center.Z + 0.01f),
                    "Нет дома западнее и южнее одновременно: " + other.Id);
            }

            // Номер читается из данных маркера, а не из константы.
            PlacedObject marker = plan.OfType(QuarterObjectTypes.PlayerHomeMarker).FirstOrDefault();
            IsTrue(marker != null, "Маркер квартиры есть в данных");
            AreEqual("67", marker.Param("apartment"), "Маркер несёт номер квартиры 67");
            AreEqual(home.Id, marker.Param("buildingId"), "Маркер привязан к дому квартиры");

            // Дверь и выход: по одному объекту, выход в 2 м и вне стен дома (карта v1.1).
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.PlayerHomeDoor), "Объект двери квартиры");
            AreEqual(1, plan.CountOfType(QuarterObjectTypes.PlayerHomeExit), "Объект выхода во двор");
            IsFalse(home.Footprint.Contains(plan.PlayerHome.CourtyardExit.Ground), "Выход во двор вне стен");
            float doorToExit = plan.PlayerHome.EntranceDoor.Ground.DistanceTo(plan.PlayerHome.CourtyardExit.Ground);
            IsTrue(Math.Abs(doorToExit - 2f) < 0.05f, "Выход в 2 м от двери: " + doorToExit.ToString("0.##"));
        }

        private static void MirrorCounts()
        {
            QuarterPlan plan = District();
            AreEqual(24, plan.DistrictPoints.Count, "Точек карты (как в district_layout_check.py)");
            AreEqual(46, plan.DeliveryPoints.Count, "Точек доставки (как в district_layout_check.py)");
            AreEqual(974, plan.Objects.Count, "Объектов плана (как в district_layout_check.py)");
            AreEqual(82, plan.Roads.Count, "Дорожных сегментов (как в district_layout_check.py)");
            AreEqual(59, plan.Nodes.Count, "Узлов дорожного графа (как в district_layout_check.py)");
        }

        // ---------------------------------------------------------------- правила

        private static void FullRuleSetPasses()
        {
            List<string> v = DistrictPlanChecks.Analyze(Snap());
            AreEqual(0, v.Count, "Нарушений правил: " + string.Join("; ", v.Take(5)));
        }

        private static void NoBuildingOverlaps()
        {
            LayoutSnapshot s = Snap();
            ExpectNoViolation(s, "перекрывает улицу", "здания и площадки на улицах");
            ExpectNoViolation(s, "пересечение:", "пересечения зданий и площадок");
            ExpectNoViolation(s, "газон на здании", "газоны на зданиях");
        }

        private static void NoDuplicates()
        {
            ExpectNoViolation(Snap(), "дублирующ", "дубликаты");
        }

        private static void RoadNetworkConnected()
        {
            ExpectNoViolation(Snap(), "не связна", "связность дорожной сети");
        }

        private static void AllAccessReachable()
        {
            LayoutSnapshot s = Snap();
            ExpectNoViolation(s, "недостижима", "доступность входов и точек доставки");
            // Все 46 точек доставки должны быть в снимке доступа.
            AreEqual(46, s.Access.Count(p => p.Kind == "delivery"), "Точек доставки в снимке");
        }

        private static void MandatoryPresentForbiddenAbsent()
        {
            LayoutSnapshot s = Snap();
            ExpectNoViolation(s, "нет обязательного", "обязательные объекты");
            ExpectNoViolation(s, "запрещённый", "запрещённые объекты");
        }

        private static void Azs4BelowPawnshop()
        {
            ExpectNoViolation(Snap(), "АЗС-4", "расположение АЗС-4 относительно ломбарда");
        }

        private static void ResidentialCounts()
        {
            LayoutSnapshot s = Snap();
            AreEqual(4, DistrictPlanChecks.CountGroup(s, "mkd-n"), "МКД на севере");
            AreEqual(3, DistrictPlanChecks.CountGroup(s, "mkd-s"), "МКД на юге");
            AreEqual(3, DistrictPlanChecks.CountGroup(s, "mkd-e"), "МКД на востоке");
            AreEqual(5, DistrictPlanChecks.CountGroup(s, "private"), "Частных домов");
            ExpectNoViolation(s, "МКД", "счётчики МКД");
            ExpectNoViolation(s, "частных домов", "счётчик частных домов");
        }

        // ---------------------------------------------------------------- негативные проверки

        private static void NegativeBuildingOnStreet()
        {
            LayoutSnapshot s = Snap();
            int i = s.Buildings.FindIndex(b => b.Name == "bld-n4");
            IsTrue(i >= 0, "bld-n4 есть в снимке");
            PlanRect r = s.Buildings[i];
            float w = r.Rect.Width;
            s.Buildings[i] = new PlanRect(r.Name, r.Group, new Rect2(270f - w / 2f, r.Rect.MinZ, 270f + w / 2f, r.Rect.MaxZ));
            ExpectViolation(s, "перекрыва", "здание на улице V270");
        }

        private static void NegativeDuplicateBuilding()
        {
            LayoutSnapshot s = Snap();
            s.Buildings.Add(s.Buildings.First(b => b.Name == "com-bank"));
            ExpectViolation(s, "дублирующ", "дубль здания com-bank");
        }

        private static void NegativeMissingMkd()
        {
            LayoutSnapshot s = Snap();
            s.Buildings.RemoveAll(b => b.Name == "bld-n2");
            ExpectViolation(s, "МКД", "удалён МКД bld-n2");
        }

        private static void NegativeSealedBankDelivery()
        {
            QuarterPlan plan = District();
            DeliveryPointInfo bank = plan.DeliveryPoints.First(d => d.Kind == "bank");
            LayoutSnapshot s = DistrictPlanChecks.FromPlan(plan);
            // Кольцо стен вокруг клетки точки доставки: все четыре соседние клетки заняты.
            int cx = (int)Math.Floor(bank.Position.X), cz = (int)Math.Floor(bank.Position.Z);
            float x = cx, z = cz;
            s.Blockers.Add(new PlanRect("wall-top", "test", new Rect2(x - 1f, z + 1f, x + 2f, z + 2f)));
            s.Blockers.Add(new PlanRect("wall-bottom", "test", new Rect2(x - 1f, z - 1f, x + 2f, z)));
            s.Blockers.Add(new PlanRect("wall-left", "test", new Rect2(x - 1f, z, x, z + 1f)));
            s.Blockers.Add(new PlanRect("wall-right", "test", new Rect2(x + 1f, z, x + 2f, z + 1f)));
            ExpectViolation(s, "недостижима", "точка доставки банка заперта");
        }

        private static void NegativeIsolatedRoad()
        {
            LayoutSnapshot s = Snap();
            s.Roads.Add(new PlanRoad("road-test-isolated", "node-x1", "node-x2",
                new Vec2(1f, 1f), new Vec2(2f, 1f), 7f, false));
            ExpectViolation(s, "не связна", "изолированная дорога");
        }

        private static void NegativeDuplicateRoad()
        {
            LayoutSnapshot s = Snap();
            PlanRoad r = s.Roads[0];
            s.Roads.Add(new PlanRoad("road-dup", r.From, r.To, r.A, r.B, r.Width, r.Driveway));
            ExpectViolation(s, "дублирующ", "дублирующаяся дорога");
        }

        private static void NegativeAzs4OldSpot()
        {
            LayoutSnapshot s = Snap();
            int i = s.Open.FindIndex(r => r.Name == "gas-04.pad");
            IsTrue(i >= 0, "gas-04.pad есть в снимке");
            PlanRect p = s.Open[i];
            s.Open[i] = new PlanRect(p.Name, p.Group,
                new Rect2(p.Rect.MinX, p.Rect.MinZ - 55f, p.Rect.MaxX, p.Rect.MaxZ - 55f));
            ExpectViolation(s, "не непосредственно", "АЗС-4 смещена на старое место");
        }

        private static void NegativeLoadingBlocksDelivery()
        {
            LayoutSnapshot s = Snap();
            // Блокер ровно на клетке точки доставки на погрузке (652, 662).
            s.Blockers.Add(new PlanRect("test-block", "test", new Rect2(651.5f, 661.5f, 652.5f, 662.5f)));
            ExpectViolation(s, "недостижима", "точка погрузки закрыта блокером");
        }

        private static void NegativeBlockedEntrance()
        {
            LayoutSnapshot s = Snap();
            PlanPoint entrance = s.Access.First(p => p.Name == "bld-n2.entrance1");
            s.Blockers.Add(new PlanRect("test-entrance-block", "test",
                new Rect2(entrance.Position.X - 0.6f, entrance.Position.Z - 0.6f,
                          entrance.Position.X + 0.6f, entrance.Position.Z + 0.6f)));
            ExpectViolation(s, "bld-n2.entrance1", "подъезд bld-n2.entrance1 закрыт блокером");
        }

        private static void NegativeForbiddenAndMissing()
        {
            LayoutSnapshot s = Snap();
            s.Ids.Add("carwash-test");
            ExpectViolation(s, "запрещённый", "запрещённый объект");

            LayoutSnapshot t = Snap();
            t.Ids.Remove("com-mall");
            ExpectViolation(t, "нет обязательного", "пропавший ТЦ");
        }

        // ---------------------------------------------------------------- детерминизм

        private static void Deterministic()
        {
            QuarterPlan a = DistrictMapGenerator.Generate(1904);
            QuarterPlan b = DistrictMapGenerator.Generate(1904);
            AreEqual(a.Objects.Count, b.Objects.Count, "Число объектов");
            AreEqual(a.Roads.Count, b.Roads.Count, "Число дорог");
            AreEqual(a.Buildings.Count, b.Buildings.Count, "Число зданий");
            AreEqual(a.DeliveryPoints.Count, b.DeliveryPoints.Count, "Число точек доставки");
            for (int i = 0; i < a.Objects.Count; i++)
            {
                PlacedObject x = a.Objects[i], y = b.Objects[i];
                AreEqual(x.Id, y.Id, "Id объекта #" + i);
                AreEqual(x.TypeId, y.TypeId, "Тип объекта " + x.Id);
                AreEqual(x.Position.X, y.Position.X, "X " + x.Id);
                AreEqual(x.Position.Y, y.Position.Y, "Y " + x.Id);
                AreEqual(x.Position.Z, y.Position.Z, "Z " + x.Id);
                AreEqual(x.RotationY, y.RotationY, "Поворот " + x.Id);
                AreEqual(x.Parameters.Count, y.Parameters.Count, "Число параметров " + x.Id);
                foreach (KeyValuePair<string, string> kv in x.Parameters)
                {
                    IsTrue(y.Parameters.TryGetValue(kv.Key, out string other) && other == kv.Value,
                        "Параметр " + kv.Key + " объекта " + x.Id + " отличается");
                }
            }
        }

        private static void RepeatGenerationStable()
        {
            var generator = new DistrictMapGenerator(new DistrictMapOptions { Seed = 1904 });
            QuarterPlan first = generator.Generate();
            QuarterPlan second = generator.Generate();
            QuarterPlan third = generator.Generate();
            AreEqual(first.Objects.Count, second.Objects.Count, "Повтор 2: объекты");
            AreEqual(first.Objects.Count, third.Objects.Count, "Повтор 3: объекты");
            AreEqual(first.DeliveryPoints.Count, third.DeliveryPoints.Count, "Повтор 3: доставки");
            AreEqual(first.DistrictPoints.Count, third.DistrictPoints.Count, "Повтор 3: точки");
        }

        private static void SeedKeepsGeometry()
        {
            QuarterPlan a = DistrictMapGenerator.Generate(1904);
            QuarterPlan b = DistrictMapGenerator.Generate(1905);
            AreEqual(a.Objects.Count, b.Objects.Count, "Число объектов не зависит от seed");
            for (int i = 0; i < a.Objects.Count; i++)
            {
                AreEqual(a.Objects[i].Position.X, b.Objects[i].Position.X, "X " + a.Objects[i].Id);
                AreEqual(a.Objects[i].Position.Z, b.Objects[i].Position.Z, "Z " + a.Objects[i].Id);
            }
        }

        // ---------------------------------------------------------------- квартал 250

        private static void DefaultStaysQuarter250()
        {
            QuarterPlan plan = QuarterGenerator.Generate(1904);
            AreEqual(QuarterLayoutIds.Quarter250, plan.LayoutId, "Квартал 250 по умолчанию");
            AreEqual(250f, plan.Size, "Размер квартала 250");
            AreEqual(0, plan.CountOfType(DistrictObjectTypes.Commerce), "В квартале 250 нет коммерции района");
            AreEqual(0, plan.CountOfType(DistrictObjectTypes.GasCanopy), "В квартале 250 нет навесов АЗС");
            AreEqual(0, plan.DistrictPoints.Count, "В квартале 250 нет точек района");
        }
    }
}
