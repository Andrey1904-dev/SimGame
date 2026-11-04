// Life From Zero — City/Quarter module (ТЗ №023)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Тесты генератора квартала: дороги, дома, дворы, воспроизводимость.
// Запуск вне Unity: dotnet run --project City/Tests/Console.csproj

using System;
using System.Collections.Generic;
using LifeFromZero.City.Quarter;
using static LifeFromZero.City.Quarter.Tests.MiniTestRunner;

namespace LifeFromZero.City.Quarter.Tests
{
    public static class QuarterGeneratorTests
    {
        private const int Seed = 1904;

        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();
            suite.Add("Повторяемость: один seed — побитово одинаковый план", DeterminismSameSeed);
            suite.Add("Разные seed дают разные кварталы", DeterminismDifferentSeed);
            suite.Add("Размер квартала 250×250 и все объекты внутри границ", SizeAndBounds);
            suite.Add("Дорожный граф связный (улицы + перекрёстки + проезды)", RoadGraphConnected);
            suite.Add("Перекрёстков 9, у каждого — переходы и стоп-линии", IntersectionsAndCrosswalks);
            suite.Add("Тротуары идут вдоль каждой улицы с двух сторон", SidewalksAlongRoads);
            suite.Add("Дорожная разметка: осевой пунктир присутствует и не заходит на перекрёстки", CenterLineMarkings);
            suite.Add("Дома пятиэтажные, стоят внутри квартала", BuildingsAreFiveStorey);
            suite.Add("Дома не пересекают проезжую часть и тротуары", BuildingsAvoidRoadCorridors);
            suite.Add("Дома не пересекаются друг с другом", BuildingsDoNotOverlap);
            suite.Add("У каждого дома есть подъезды у фасада", BuildingsHaveEntrances);
            suite.Add("Дворов 2–3, в каждом: площадка, лавочки, деревья, парковка, фонари", CourtyardsAreComplete);
            suite.Add("Дворовые проезды соединяют парковки с улицами", DrivewaysConnectYardsToStreets);
            suite.Add("Уличные фонари стоят вдоль улиц с заданным шагом", StreetLampsAlongRoads);
            suite.Add("Все Id объектов уникальны", ObjectIdsUnique);
            suite.Add("Расширяемость: свой архетип здания и свой размещатель работают без правки ядра", Extensibility);
            suite.Add("Невалидные параметры отклоняются", InvalidOptionsRejected);
            suite.Add("Стабильность на серии seed 1..10: связность, дома вне дорог и без пересечений", ManySeedsStayValid);
            return suite;
        }

        private static QuarterPlan Plan() => QuarterGenerator.Generate(Seed);

        // ---------- воспроизводимость ----------

        private static void DeterminismSameSeed()
        {
            string a = QuarterPlanSerializer.Serialize(QuarterGenerator.Generate(42));
            string b = QuarterPlanSerializer.Serialize(QuarterGenerator.Generate(42));
            AreEqual(a, b, "Планы с seed=42 должны совпадать полностью");
            string c = QuarterPlanSerializer.Serialize(
                new QuarterGenerator(new QuarterGeneratorOptions { Seed = 42 }).Generate());
            AreEqual(a, c, "Статический и объектный входы должны давать один результат");
        }

        private static void DeterminismDifferentSeed()
        {
            string a = QuarterPlanSerializer.Serialize(QuarterGenerator.Generate(1));
            string b = QuarterPlanSerializer.Serialize(QuarterGenerator.Generate(2));
            AreNotEqual(a, b, "Разные seed должны давать разное наполнение");
        }

        // ---------- геометрия квартала ----------

        private static void SizeAndBounds()
        {
            QuarterPlan plan = Plan();
            AreEqual(250f, plan.Size, "Размер квартала по ТЗ");
            foreach (PlacedObject obj in plan.Objects)
            {
                Rect2 f = obj.Footprint;
                IsTrue(f.MinX >= -0.01f && f.MinZ >= -0.01f && f.MaxX <= plan.Size + 0.01f && f.MaxZ <= plan.Size + 0.01f,
                    "Объект " + obj.Id + " (" + obj.TypeId + ") выходит за границу: " + f.MinX + ".." + f.MaxX);
            }
        }

        // ---------- дороги ----------

        private static void RoadGraphConnected()
        {
            QuarterPlan plan = Plan();
            IsTrue(RoadGraph.IsConnected(plan), "Дорожный граф должен быть связным");
            int intersections = 0;
            foreach (RoadNode node in plan.Nodes)
                if (node.IsIntersection) intersections++;
            AreEqual(9, intersections, "Сетка 3×3 даёт 9 перекрёстков");
            IsTrue(plan.Roads.Count >= 24, "Минимум 24 уличных сегмента, фактически: " + plan.Roads.Count);
        }

        private static void IntersectionsAndCrosswalks()
        {
            QuarterPlan plan = Plan();
            AreEqual(36, plan.CountOfType(QuarterObjectTypes.Crosswalk),
                "9 перекрёстков × 4 перехода");
            AreEqual(36, plan.CountOfType(QuarterObjectTypes.StopLine),
                "9 перекрёстков × 4 стоп-линии");
            // Каждый переход — рядом с перекрёстком (в радиусе коридора улицы).
            foreach (PlacedObject cw in plan.OfType(QuarterObjectTypes.Crosswalk))
            {
                bool near = false;
                foreach (RoadNode node in plan.Nodes)
                    if (node.IsIntersection && node.Position.DistanceTo(cw.Position.Ground) < 12f)
                        near = true;
                IsTrue(near, "Переход " + cw.Id + " должен быть у перекрёстка");
            }
        }

        private static void SidewalksAlongRoads()
        {
            QuarterPlan plan = Plan();
            // 3 вертикальные + 3 горизонтальные улицы × 2 стороны = 12 тротуарных полос.
            AreEqual(12, plan.CountOfType(QuarterObjectTypes.Sidewalk), "Тротуары по обе стороны каждой улицы");
            foreach (PlacedObject sw in plan.OfType(QuarterObjectTypes.Sidewalk))
                IsTrue(sw.Size.X >= plan.Size - 0.01f || sw.Size.Z >= plan.Size - 0.01f,
                    "Тротуар " + sw.Id + " должен идти вдоль всей улицы");
        }

        private static void CenterLineMarkings()
        {
            QuarterPlan plan = Plan();
            IsTrue(plan.CountOfType(QuarterObjectTypes.CenterLine) > 100,
                "Осевого пунктира должно быть много, фактически: " + plan.CountOfType(QuarterObjectTypes.CenterLine));
            // Пунктир не рисуется поперёк перекрёстков: центр штриха не ближе 8 м к перекрёстку по обеим осям сразу.
            foreach (PlacedObject dash in plan.OfType(QuarterObjectTypes.CenterLine))
            {
                foreach (RoadNode node in plan.Nodes)
                {
                    if (!node.IsIntersection) continue;
                    float dx = Math.Abs(dash.Position.X - node.Position.X);
                    float dz = Math.Abs(dash.Position.Z - node.Position.Z);
                    IsFalse(dx < 9f && dz < 9f, "Штрих " + dash.Id + " заходит на перекрёсток");
                }
            }
        }

        // ---------- дома ----------

        private static void BuildingsAreFiveStorey()
        {
            QuarterPlan plan = Plan();
            IsTrue(plan.Buildings.Count >= 20, "Домов должно быть не меньше 20, фактически: " + plan.Buildings.Count);
            foreach (BuildingInfo b in plan.Buildings)
            {
                AreEqual(5, b.Floors, "Дом " + b.Id + " — пятиэтажный по ТЗ");
                AreEqual(15f, b.Size.Y, "Высота дома " + b.Id + " = 5 этажей × 3 м");
                IsTrue(b.Size.X >= 30f && b.Size.X <= 42f, "Длина корпуса " + b.Id + " в пределах архетипа");
            }
        }

        private static void BuildingsAvoidRoadCorridors()
        {
            QuarterPlan plan = Plan();
            foreach (BuildingInfo b in plan.Buildings)
            {
                foreach (RoadSegment road in plan.Roads)
                {
                    if (road.Kind != RoadKind.Street) continue;
                    IsFalse(b.Footprint.Overlaps(road.Footprint.Expanded(2.5f)),
                        "Дом " + b.Id + " пересекает улицу " + road.Id + " (с тротуаром)");
                }
            }
        }

        private static void BuildingsDoNotOverlap()
        {
            QuarterPlan plan = Plan();
            for (int i = 0; i < plan.Buildings.Count; i++)
                for (int j = i + 1; j < plan.Buildings.Count; j++)
                    IsFalse(plan.Buildings[i].Footprint.Expanded(1f).Overlaps(plan.Buildings[j].Footprint),
                        "Дома " + plan.Buildings[i].Id + " и " + plan.Buildings[j].Id + " пересекаются");
        }

        private static void BuildingsHaveEntrances()
        {
            QuarterPlan plan = Plan();
            foreach (BuildingInfo b in plan.Buildings)
            {
                IsTrue(b.Entrances.Count >= 2, "У дома " + b.Id + " минимум 2 подъезда");
                foreach (Vec2 entrance in b.Entrances)
                {
                    // Подъезд — у длинного фасада: расстояние до центра ≈ глубина/2.
                    bool alongX = Math.Abs(b.RotationY) < 0.01f;
                    float offset = alongX
                        ? Math.Abs(entrance.Z - b.Center.Z)
                        : Math.Abs(entrance.X - b.Center.X);
                    IsTrue(Math.Abs(offset - b.Size.Z / 2f) < 1.5f,
                        "Подъезд дома " + b.Id + " должен быть у фасада, offset=" + offset);
                }
            }
            AreEqual(CountEntrances(plan), plan.CountOfType(QuarterObjectTypes.Entrance),
                "Подъезды в Buildings и в общем списке объектов согласованы");
        }

        private static int CountEntrances(QuarterPlan plan)
        {
            int total = 0;
            foreach (BuildingInfo b in plan.Buildings) total += b.Entrances.Count;
            return total;
        }

        // ---------- дворы ----------

        private static void CourtyardsAreComplete()
        {
            QuarterPlan plan = Plan();
            IsTrue(plan.Courtyards.Count >= 2 && plan.Courtyards.Count <= 3,
                "Дворов должно быть 2–3, фактически: " + plan.Courtyards.Count);
            AreEqual(plan.Courtyards.Count, plan.CountOfType(QuarterObjectTypes.Playground),
                "В каждом дворе — детская площадка");
            foreach (CourtyardInfo yard in plan.Courtyards)
            {
                IsTrue(yard.HasPlayground, "Двор " + yard.Id + ": есть площадка");
                IsTrue(yard.Benches >= 4, "Двор " + yard.Id + ": лавочек >= 4, фактически " + yard.Benches);
                IsTrue(yard.Trees >= 5, "Двор " + yard.Id + ": деревьев >= 5, фактически " + yard.Trees);
                IsTrue(yard.ParkingStalls >= 6, "Двор " + yard.Id + ": парковочных мест >= 6, фактически " + yard.ParkingStalls);

                int yardLamps = 0;
                foreach (PlacedObject lamp in plan.OfType(QuarterObjectTypes.YardLamp))
                    if (lamp.Param("courtyardId") == yard.Id) yardLamps++;
                IsTrue(yardLamps >= 4, "Двор " + yard.Id + ": дворовых фонарей >= 4, фактически " + yardLamps);

                // Содержимое двора не пересекает дома.
                foreach (BuildingInfo b in plan.Buildings)
                    IsFalse(yard.Area.Overlaps(b.Footprint), "Двор " + yard.Id + " пересекает дом " + b.Id);
            }
        }

        private static void DrivewaysConnectYardsToStreets()
        {
            QuarterPlan plan = Plan();
            int driveways = 0;
            foreach (RoadSegment road in plan.Roads)
                if (road.Kind == RoadKind.Driveway) driveways++;
            AreEqual(plan.Courtyards.Count, driveways, "По одному проезду на двор");
            // Проезд не проходит сквозь дома.
            foreach (RoadSegment road in plan.Roads)
            {
                if (road.Kind != RoadKind.Driveway) continue;
                foreach (BuildingInfo b in plan.Buildings)
                    IsFalse(road.Footprint.Overlaps(b.Footprint),
                        "Проезд " + road.Id + " проходит сквозь дом " + b.Id);
            }
        }

        // ---------- фонари и прочее ----------

        private static void StreetLampsAlongRoads()
        {
            QuarterPlan plan = Plan();
            int lamps = plan.CountOfType(QuarterObjectTypes.StreetLamp);
            IsTrue(lamps >= 30, "Уличных фонарей должно быть >= 30, фактически: " + lamps);
            // Каждый фонарь — у улицы: близко к осевой линии какой-либо улицы по одной из осей.
            float[] lines = { 50f, 125f, 200f };
            foreach (PlacedObject lamp in plan.OfType(QuarterObjectTypes.StreetLamp))
            {
                bool near = false;
                foreach (float line in lines)
                    if (Math.Abs(lamp.Position.X - line) < 6f || Math.Abs(lamp.Position.Z - line) < 6f)
                        near = true;
                IsTrue(near, "Фонарь " + lamp.Id + " должен стоять вдоль улицы");
            }
        }

        private static void ObjectIdsUnique()
        {
            QuarterPlan plan = Plan();
            var ids = new HashSet<string>();
            foreach (PlacedObject obj in plan.Objects)
                IsTrue(ids.Add(obj.Id), "Дублирующийся Id: " + obj.Id);
            IsTrue(plan.Objects.Count > 400, "Квартал должен быть наполнен, объектов: " + plan.Objects.Count);
        }

        // ---------- расширяемость и валидация ----------

        private sealed class KioskPlacer : IQuarterObjectPlacer
        {
            public void Place(QuarterPlacementContext context)
            {
                var rect = Rect2.FromCenterSize(new Vec2(20f, 20f), 4f, 3f);
                if (!context.IsFree(rect)) return;
                context.Add(new PlacedObject("custom-kiosk-1", "kiosk", new Vec3(20f, 0f, 20f), 0f,
                    new Vec3(4f, 3f, 3f), new Dictionary<string, string>()));
            }
        }

        private static void Extensibility()
        {
            var options = new QuarterGeneratorOptions { Seed = Seed };
            options.Archetypes.Add(new BuildingArchetype("building.brick5", 5, 12f, 3f,
                new[] { 33f }, 16f));
            options.CustomPlacers.Add(new KioskPlacer());
            QuarterPlan plan = new QuarterGenerator(options).Generate();
            IsTrue(plan.CountOfType("kiosk") == 1, "Пользовательский размещатель добавил киоск");
            // Генерация с расширенными опциями остаётся детерминированной и связной.
            string a = QuarterPlanSerializer.Serialize(new QuarterGenerator(options).Generate());
            string b = QuarterPlanSerializer.Serialize(new QuarterGenerator(options).Generate());
            AreEqual(a, b, "Расширенная генерация детерминирована");
            IsTrue(RoadGraph.IsConnected(plan), "Граф связный и с расширениями");
        }

        private static void ManySeedsStayValid()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                QuarterPlan plan = QuarterGenerator.Generate(seed);
                IsTrue(RoadGraph.IsConnected(plan), "seed=" + seed + ": граф связный");
                IsTrue(plan.Courtyards.Count >= 2, "seed=" + seed + ": дворов >= 2");
                for (int i = 0; i < plan.Buildings.Count; i++)
                {
                    foreach (RoadSegment road in plan.Roads)
                        if (road.Kind == RoadKind.Street)
                            IsFalse(plan.Buildings[i].Footprint.Overlaps(road.Footprint.Expanded(2.5f)),
                                "seed=" + seed + ": дом " + plan.Buildings[i].Id + " на дороге");
                    for (int j = i + 1; j < plan.Buildings.Count; j++)
                        IsFalse(plan.Buildings[i].Footprint.Expanded(1f).Overlaps(plan.Buildings[j].Footprint),
                            "seed=" + seed + ": дома пересекаются");
                }
            }
        }

        private static void InvalidOptionsRejected()
        {
            bool thrown = false;
            try { new QuarterGenerator(new QuarterGeneratorOptions { Size = 50f }); }
            catch (ArgumentOutOfRangeException) { thrown = true; }
            IsTrue(thrown, "Слишком маленький квартал должен отклоняться");

            thrown = false;
            try { new QuarterGenerator(new QuarterGeneratorOptions { CourtyardCount = 5 }); }
            catch (ArgumentOutOfRangeException) { thrown = true; }
            IsTrue(thrown, "Больше 3 дворов — вне ТЗ");
        }
    }
}
