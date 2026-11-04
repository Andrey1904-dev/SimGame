// Life From Zero — City/Quarter module (ТЗ №024, дополнение: визуальное разнообразие)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Тесты архитектурного разнообразия: каталог вариантов, минимум 3 варианта
// на тип дома, отсутствие одинаковых комбинаций у соседей, корректность
// фасадных деталей в данных (окна/балконы/кондиционеры/водостоки),
// повторяемость. Сама картинка в Unity здесь не проверяется.

using System;
using System.Collections.Generic;
using LifeFromZero.City.Quarter;
using static LifeFromZero.City.Quarter.Tests.MiniTestRunner;

namespace LifeFromZero.City.Quarter.Tests
{
    public static class ArchitectureVarietyTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();
            suite.Add("Разнообразие: в каталоге минимум 3 варианта на каждый тип дома (3/5/9/10)", CatalogMinThreePerType);
            suite.Add("Разнообразие: в квартале используются минимум 3 варианта каждого представленного типа", UsedVariantsPerType);
            suite.Add("Разнообразие: соседние дома без одинаковой комбинации вариант+цвет+балконы", NeighborsDiffer);
            suite.Add("Разнообразие: варианты отличаются геометрией, не только цветом", VariantsDifferInGeometry);
            suite.Add("Детали фасада: окна, сдвиг балконов, кондиционеры и водостоки в допустимых пределах", FacadeDetailsValid);
            suite.Add("Детали фасада: входные группы и двери подъездов варьируются", EntrancesVary);
            suite.Add("Атмосфера: износ фасадов разный (не все свежие и не все руины)", WearVaries);
            suite.Add("Атмосфера: асфальтовые заплатки, ограждения, разные лавочки и площадки", AtmosphereObjects);
            suite.Add("Разнообразие детерминировано по seed", VarietyDeterministic);
            return suite;
        }

        private static QuarterPlan Plan() => QuarterGenerator.Generate(1904);

        private static void CatalogMinThreePerType()
        {
            foreach (int floors in new[] { 3, 5, 9, 10 })
            {
                IReadOnlyList<BuildingVariant> variants = BuildingVariantCatalog.ForFloors(floors);
                IsTrue(variants.Count >= 3, "Для " + floors + " этажей вариантов >= 3, фактически: " + variants.Count);
                var ids = new HashSet<string>();
                foreach (BuildingVariant v in variants)
                {
                    IsTrue(ids.Add(v.Id), "Уникальный вариант: " + v.Id);
                    IsTrue(v.Palette.Count >= 3, v.Id + ": палитра >= 3 цветов");
                }
            }
        }

        private static void UsedVariantsPerType()
        {
            QuarterPlan plan = Plan();
            var usedByFloors = new Dictionary<int, HashSet<string>>();
            var countByFloors = new Dictionary<int, int>();
            foreach (BuildingInfo b in plan.Buildings)
            {
                IsTrue(!string.IsNullOrEmpty(b.VariantId), "У дома " + b.Id + " задан вариант");
                if (!usedByFloors.TryGetValue(b.Floors, out HashSet<string> set))
                {
                    set = new HashSet<string>();
                    usedByFloors[b.Floors] = set;
                    countByFloors[b.Floors] = 0;
                }
                set.Add(b.VariantId);
                countByFloors[b.Floors]++;
            }
            foreach (KeyValuePair<int, HashSet<string>> kv in usedByFloors)
            {
                int expected = Math.Min(3, countByFloors[kv.Key]);
                IsTrue(kv.Value.Count >= expected,
                    "Для " + kv.Key + " этажей используется вариантов >= " + expected + ", фактически: " + kv.Value.Count);
            }
        }

        private static void NeighborsDiffer()
        {
            QuarterPlan plan = Plan();
            for (int i = 0; i < plan.Buildings.Count; i++)
            {
                for (int j = i + 1; j < plan.Buildings.Count; j++)
                {
                    BuildingInfo a = plan.Buildings[i], b = plan.Buildings[j];
                    if (a.Center.DistanceTo(b.Center) >= 55f) continue;
                    bool sameCombination = a.VariantId == b.VariantId &&
                                           a.FacadeColor == b.FacadeColor &&
                                           a.BalconyOffset == b.BalconyOffset;
                    IsFalse(sameCombination,
                        "Соседние дома " + a.Id + " и " + b.Id + " полностью одинаковы (" + a.VariantId + ")");
                }
            }
        }

        private static void VariantsDifferInGeometry()
        {
            foreach (int floors in new[] { 3, 5, 9, 10 })
            {
                var geometries = new HashSet<string>();
                foreach (BuildingVariant v in BuildingVariantCatalog.ForFloors(floors))
                    geometries.Add(v.RoofVariant + "/" + v.BalconyType + "/" + v.WindowStyle + "/" + v.EntranceStyle);
                IsTrue(geometries.Count >= 3,
                    "Для " + floors + " этажей минимум 3 разных геометрии (крыша/балконы/окна/вход), фактически: " + geometries.Count);
            }
            // Крыши разнообразны внутри типов.
            var roofs3 = new HashSet<string>();
            foreach (BuildingVariant v in BuildingVariantCatalog.ForFloors(3)) roofs3.Add(v.RoofVariant);
            IsTrue(roofs3.Count >= 3, "У трёхэтажек >= 3 форм крыш: " + string.Join(",", roofs3));
            // Есть и застеклённые балконы, и лоджии, и открытые.
            var balconies = new HashSet<string>();
            foreach (BuildingVariant v in BuildingVariantCatalog.All) balconies.Add(v.BalconyType);
            IsTrue(balconies.Contains("glazed") && balconies.Contains("loggia") && balconies.Contains("open"),
                "В каталоге есть open/glazed/loggia балконы");
        }

        private static void FacadeDetailsValid()
        {
            QuarterPlan plan = Plan();
            foreach (BuildingInfo b in plan.Buildings)
            {
                PlacedObject obj = BuildingObject(plan, b.Id);
                AreEqual(b.VariantId, obj.Param("variant"), "Вариант в объекте здания " + b.Id);
                IsTrue(b.BalconyOffset >= 0 && b.BalconyOffset <= 2, "Сдвиг балконов 0..2");
                IsTrue(b.WearLevel >= 0f && b.WearLevel <= 1f, "Износ 0..1");

                int perFloor = int.Parse(obj.Param("windowsPerFloor"));
                IsTrue(perFloor >= 2 && perFloor <= 20, "Окон в ряд разумно: " + perFloor);

                foreach (string entry in Split(obj.Param("acUnits")))
                {
                    string[] parts = entry.Split(':');
                    int floor = int.Parse(parts[0]);
                    int window = int.Parse(parts[1]);
                    IsTrue(floor >= 1 && floor < b.Floors, "Кондиционер на существующем этаже");
                    IsTrue(window >= 0 && window < perFloor, "Кондиционер под существующим окном");
                }
                foreach (string part in Split(obj.Param("drainpipes")))
                {
                    float offset = float.Parse(part, System.Globalization.CultureInfo.InvariantCulture);
                    IsTrue(Math.Abs(offset) < b.Size.X / 2f, "Водосток в пределах корпуса");
                }
            }
        }

        private static void EntrancesVary()
        {
            QuarterPlan plan = Plan();
            var styles = new HashSet<string>();
            var doorColors = new HashSet<string>();
            foreach (PlacedObject entrance in plan.OfType(QuarterObjectTypes.Entrance))
            {
                IsTrue(!string.IsNullOrEmpty(entrance.Param("entranceStyle")), "У подъезда задан стиль входной группы");
                styles.Add(entrance.Param("entranceStyle"));
                doorColors.Add(entrance.Param("doorColor"));
            }
            IsTrue(styles.Count >= 2, "В квартале >= 2 стилей входных групп: " + string.Join(",", styles));
            IsTrue(doorColors.Count >= 3, "Двери подъездов разных цветов >= 3: " + doorColors.Count);
        }

        private static void WearVaries()
        {
            QuarterPlan plan = Plan();
            float min = 1f, max = 0f;
            foreach (BuildingInfo b in plan.Buildings)
            {
                if (b.WearLevel < min) min = b.WearLevel;
                if (b.WearLevel > max) max = b.WearLevel;
            }
            IsTrue(max - min > 0.25f, "Разброс износа фасадов заметный: " + min + ".." + max);
            IsTrue(min < 0.35f, "Есть относительно свежие фасады");
            IsTrue(max > 0.45f, "Есть обжитые старые фасады (но район не руины: max <= 1)");
        }

        private static void AtmosphereObjects()
        {
            QuarterPlan plan = Plan();
            IsTrue(plan.CountOfType(QuarterObjectTypes.RoadPatch) >= 4,
                "Асфальтовых заплаток >= 4, фактически: " + plan.CountOfType(QuarterObjectTypes.RoadPatch));
            IsTrue(plan.CountOfType(QuarterObjectTypes.YardFence) >= 3,
                "Ограждений палисадников >= 3, фактически: " + plan.CountOfType(QuarterObjectTypes.YardFence));

            var benchStyles = new HashSet<string>();
            foreach (PlacedObject bench in plan.OfType(QuarterObjectTypes.Bench))
                benchStyles.Add(bench.Param("benchStyle"));
            IsTrue(benchStyles.Count >= 2, "Лавочки старые и новые: " + string.Join(",", benchStyles));

            var playgroundVariants = new HashSet<string>();
            foreach (PlacedObject pg in plan.OfType(QuarterObjectTypes.Playground))
                playgroundVariants.Add(pg.Param("playgroundVariant"));
            IsTrue(playgroundVariants.Count >= 2, "Детские площадки различаются по вариантам");

            // Заплатки не лежат на перекрёстках (центр заплатки — не в зоне перекрёстка).
            foreach (PlacedObject patch in plan.OfType(QuarterObjectTypes.RoadPatch))
            {
                foreach (RoadNode node in plan.Nodes)
                {
                    if (!node.IsIntersection) continue;
                    float dx = Math.Abs(patch.Position.X - node.Position.X);
                    float dz = Math.Abs(patch.Position.Z - node.Position.Z);
                    IsFalse(dx < 9f && dz < 9f, "Заплатка " + patch.Id + " на перекрёстке");
                }
            }
        }

        private static void VarietyDeterministic()
        {
            QuarterPlan a = QuarterGenerator.Generate(1904);
            QuarterPlan b = QuarterGenerator.Generate(1904);
            AreEqual(QuarterPlanSerializer.Serialize(a), QuarterPlanSerializer.Serialize(b),
                "Разнообразие воспроизводимо по seed");
            // У двух соседних по генерации планов с другим seed варианты не обязаны совпадать.
            QuarterPlan c = QuarterGenerator.Generate(7);
            bool anyDifferentVariant = false;
            int n = Math.Min(a.Buildings.Count, c.Buildings.Count);
            for (int i = 0; i < n; i++)
                if (a.Buildings[i].VariantId != c.Buildings[i].VariantId) anyDifferentVariant = true;
            IsTrue(anyDifferentVariant, "Другой seed даёт другое распределение вариантов");
        }

        private static string[] Split(string value)
        {
            if (string.IsNullOrEmpty(value)) return new string[0];
            return value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static PlacedObject BuildingObject(QuarterPlan plan, string buildingId)
        {
            foreach (PlacedObject obj in plan.OfType(QuarterObjectTypes.Building))
                if (obj.Param("buildingId") == buildingId) return obj;
            throw new InvalidOperationException("Нет объекта здания " + buildingId);
        }
    }
}
