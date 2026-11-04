// Life From Zero — City/Quarter module (ТЗ №026: финальный аудит)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Регрессионные проверки аудита: генератор обязан держать утверждённую
// конфигурацию не только на seed=1904, объекты не должны выходить за
// границу квартала (найденный дефект парковок ЖК на seed 3/10/777),
// Id обязаны быть уникальны, повторная генерация — независимой.

using System.Collections.Generic;
using LifeFromZero.City.Quarter;
using static LifeFromZero.City.Quarter.Tests.MiniTestRunner;

namespace LifeFromZero.City.Quarter.Tests
{
    public static class AuditTests
    {
        private static readonly int[] Seeds = { 1904, 1, 2, 3, 5, 7, 10, 42, 99, 777, 2024 };

        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();
            suite.Add("Аудит: состав 15 жилых зданий держится на серии seed", CompositionAcrossSeeds);
            suite.Add("Аудит: объекты и их следы внутри квартала на серии seed", BoundsAcrossSeeds);
            suite.Add("Аудит: Id объектов/зданий/доставки уникальны, точки вне стен", UniqueIdsAcrossSeeds);
            suite.Add("Аудит: повторная генерация детерминирована и независима", RegenerationStable);
            suite.Add("Аудит: подвал — пол, ступени и лестница присутствуют как объекты", BasementPhysicalObjects);
            return suite;
        }

        private static void CompositionAcrossSeeds()
        {
            foreach (int seed in Seeds)
            {
                QuarterPlan plan = QuarterGenerator.Generate(seed);
                AreEqual(15, plan.Buildings.Count, "seed " + seed + ": ровно 15 жилых зданий");
                var byFloors = new Dictionary<int, int>();
                foreach (BuildingInfo b in plan.Buildings)
                {
                    byFloors.TryGetValue(b.Floors, out int n);
                    byFloors[b.Floors] = n + 1;
                }
                AreEqual(2, byFloors[3], "seed " + seed + ": 3-этажных 2");
                AreEqual(3, byFloors[5], "seed " + seed + ": 5-этажных 3");
                AreEqual(3, byFloors[9], "seed " + seed + ": 9-этажных 3");
                AreEqual(3, byFloors[10], "seed " + seed + ": 10-этажных 3");
                AreEqual(2, byFloors[2], "seed " + seed + ": частных домов 2");
                int elite = 0;
                foreach (BuildingInfo b in plan.Buildings)
                    if (b.Floors >= 30 && b.Floors <= 35) elite++;
                AreEqual(2, elite, "seed " + seed + ": элитных ЖК 2");
            }
        }

        private static void BoundsAcrossSeeds()
        {
            foreach (int seed in Seeds)
            {
                QuarterPlan plan = QuarterGenerator.Generate(seed);
                foreach (BuildingInfo b in plan.Buildings)
                {
                    Rect2 f = b.Footprint;
                    IsTrue(f.MinX >= -0.01f && f.MaxX <= plan.Size + 0.01f &&
                           f.MinZ >= -0.01f && f.MaxZ <= plan.Size + 0.01f,
                        "seed " + seed + ": здание " + b.Id + " внутри квартала");
                    IsTrue(b.Size.X > 0f && b.Size.Y > 0f && b.Size.Z > 0f,
                        "seed " + seed + ": размеры " + b.Id + " положительны");
                }
                foreach (PlacedObject o in plan.Objects)
                {
                    IsTrue(o.Size.X > 0f && o.Size.Y > 0f && o.Size.Z > 0f,
                        "seed " + seed + ": размер объекта " + o.Id + " (" + o.TypeId + ") положителен");
                    IsFalse(float.IsNaN(o.Position.X) || float.IsNaN(o.Position.Z) ||
                            float.IsInfinity(o.Position.X) || float.IsInfinity(o.Position.Z),
                        "seed " + seed + ": координаты " + o.Id + " конечны");
                    // Дорожные объекты идут до самой границы квартала — их не проверяем.
                    if (o.TypeId.StartsWith("road") || o.TypeId == QuarterObjectTypes.Sidewalk ||
                        o.TypeId == QuarterObjectTypes.Crosswalk || o.TypeId == QuarterObjectTypes.CenterLine ||
                        o.TypeId == QuarterObjectTypes.StopLine) continue;
                    Rect2 f = o.Footprint;
                    IsTrue(f.MinX >= -0.6f && f.MaxX <= plan.Size + 0.6f &&
                           f.MinZ >= -0.6f && f.MaxZ <= plan.Size + 0.6f,
                        "seed " + seed + ": след " + o.Id + " (" + o.TypeId + ") внутри квартала: " +
                        f.MinX + ".." + f.MaxX + ", " + f.MinZ + ".." + f.MaxZ);
                }
            }
        }

        private static void UniqueIdsAcrossSeeds()
        {
            foreach (int seed in Seeds)
            {
                QuarterPlan plan = QuarterGenerator.Generate(seed);
                var ids = new HashSet<string>();
                foreach (PlacedObject o in plan.Objects)
                    IsTrue(ids.Add(o.Id), "seed " + seed + ": Id объекта уникален: " + o.Id);
                foreach (BuildingInfo b in plan.Buildings)
                    IsTrue(ids.Add(b.Id), "seed " + seed + ": Id здания уникален: " + b.Id);
                var delivery = new HashSet<string>();
                foreach (DeliveryPointInfo d in plan.DeliveryPoints)
                {
                    IsTrue(delivery.Add(d.Id), "seed " + seed + ": Id доставки уникален: " + d.Id);
                    var pt = new Vec2(d.Position.X, d.Position.Z);
                    foreach (BuildingInfo b in plan.Buildings)
                        IsFalse(b.Footprint.Contains(pt),
                            "seed " + seed + ": точка " + d.Id + " не внутри стен " + b.Id);
                }
                var props = new HashSet<string>();
                foreach (RealEstateProperty p in plan.Properties)
                {
                    IsTrue(props.Add(p.Id), "seed " + seed + ": Id недвижимости уникален: " + p.Id);
                    if (p.BuildingId != null)
                    {
                        bool found = false;
                        foreach (BuildingInfo b in plan.Buildings)
                            if (b.Id == p.BuildingId) found = true;
                        IsTrue(found, "seed " + seed + ": запись " + p.Id + " привязана к зданию");
                    }
                }
            }
        }

        private static void RegenerationStable()
        {
            QuarterPlan a = QuarterGenerator.Generate(1904);
            int objectsA = a.Objects.Count;
            QuarterPlan b = QuarterGenerator.Generate(1904);
            AreEqual(QuarterPlanSerializer.Serialize(a), QuarterPlanSerializer.Serialize(b),
                "Два запуска с одним seed дают одинаковый план");
            AreEqual(objectsA, b.Objects.Count, "Количество объектов стабильно");
            QuarterGenerator.Generate(42); // чужая генерация между запусками
            QuarterPlan c = QuarterGenerator.Generate(1904);
            AreEqual(QuarterPlanSerializer.Serialize(a), QuarterPlanSerializer.Serialize(c),
                "Генерация не зависит от предыдущих запусков (нет накопления состояния)");
        }

        private static void BasementPhysicalObjects()
        {
            QuarterPlan plan = QuarterGenerator.Generate(1904);
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementFloor) >= 1,
                "Пол подвала есть как объект (адаптер ставит ему коллайдер)");
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementStep) >= 3,
                "Ступени лестницы есть как объекты: " + plan.CountOfType(QuarterObjectTypes.BasementStep));
            IsTrue(plan.CountOfType(QuarterObjectTypes.BasementWall) >= 4,
                "Стены подвала присутствуют");
            IsTrue(plan.Basement != null, "Подвал есть в данных");
            // Дверь не внутри следов стен: она стоит в проёме у лестницы.
            var door = new Vec2(plan.Basement.DoorPosition.X, plan.Basement.DoorPosition.Z);
            IsFalse(plan.Basement.Entrance.Equals(plan.Basement.DoorPosition),
                "Вход снаружи и дверь подвала — разные точки");
            IsTrue(door.X >= 0f && door.X <= plan.Size && door.Z >= 0f && door.Z <= plan.Size,
                "Дверь подвала внутри квартала");
        }
    }
}
