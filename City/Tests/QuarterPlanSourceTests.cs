// Life From Zero — City/Quarter module (ТЗ №043, аудит)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// РЕГРЕССИЯ P0 PR #14: план района обнулялся сразу после сборки.
//
// Дефект был в QuarterUnityAdapter.Build:
//     _plan = plan;      // план записан
//     ClearBuilt();      // ...и тут же обнулён (_plan = null)
// Следствие: Plan/HasPlan всегда false, карта UI получала null и рисовала
// пустую сетку без маршрутов.
//
// Тест воспроизводит ИСХОДНЫЙ порядок вызовов через тот же контракт
// (очистка обнуляет план, затем запись) и требует, чтобы после сборки план
// был жив. Проверяется чистая логика QuarterPlanSlot — она вынесена из
// MonoBehaviour именно для того, чтобы этот порядок можно было прогнать
// без Unity.

using System;

namespace LifeFromZero.City.Quarter.Tests
{
    public static class QuarterPlanSourceTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();

            suite.Add("План: после сборки Plan доступен (регрессия P0)", PlanSurvivesBuild);
            suite.Add("План: исходный порядок «план → очистка» обнуляет Plan", OldOrderLosesPlan);
            suite.Add("План: очистка сбрасывает план снесённого квартала", ClearDropsPlan);
            suite.Add("План: повторная сборка отдаёт последний план", RebuildKeepsLatestPlan);
            suite.Add("План: BeginBuild(null) бросает ArgumentNullException", BeginBuildRejectsNull);
            suite.Add("План: новый адаптер не отдаёт план до сборки", EmptySlotHasNoPlan);
            suite.Add("План: NotifyBuilt рассылает событие PlanChanged подписчикам", NotifyBuiltRaisesPlanChanged);
            suite.Add("План: Clear рассылает PlanChanged(null) при сбросе непустого слота", ClearRaisesPlanChangedWhenPopulated);

            return suite;
        }

        private static void PlanSurvivesBuild()
        {
            var slot = new QuarterPlanSlot();
            QuarterPlan plan = DistrictMapGenerator.Generate(12345);

            slot.BeginBuild(plan);

            MiniTestRunner.IsTrue(slot.HasPlan, "HasPlan после сборки");
            MiniTestRunner.IsTrue(ReferenceEquals(plan, slot.Plan), "Plan — тот же объект плана");
        }

        /// <summary>
        /// Воспроизведение старого кода: сначала запись плана, затем очистка.
        /// Тест фиксирует, что такой порядок план НЕ сохраняет — если кто-то
        /// вернёт присвоение до ClearBuilt(), PlanSurvivesBuild упадёт, а этот
        /// тест напомнит причину.
        /// </summary>
        private static void OldOrderLosesPlan()
        {
            var slot = new QuarterPlanSlot();
            QuarterPlan plan = DistrictMapGenerator.Generate(12345);

            // Так было в PR #14: записали план, потом вызвали ClearBuilt().
            slot.BeginBuild(plan);
            slot.BeginBuild(plan); // эквивалент повторного Build без внешней очистки
            slot.Clear();          // ClearBuilt() обнуляет

            MiniTestRunner.IsFalse(slot.HasPlan, "после очистки плана нет");
            MiniTestRunner.IsTrue(slot.Plan == null, "Plan == null после очистки");
        }

        private static void ClearDropsPlan()
        {
            var slot = new QuarterPlanSlot();
            slot.BeginBuild(DistrictMapGenerator.Generate(7));
            MiniTestRunner.IsTrue(slot.HasPlan, "план есть до очистки");

            slot.Clear();

            MiniTestRunner.IsFalse(slot.HasPlan, "план сброшен");
            MiniTestRunner.IsTrue(slot.Plan == null, "Plan == null");
        }

        private static void RebuildKeepsLatestPlan()
        {
            var slot = new QuarterPlanSlot();
            QuarterPlan first = DistrictMapGenerator.Generate(1);
            QuarterPlan second = DistrictMapGenerator.Generate(2);

            slot.BeginBuild(first);
            slot.BeginBuild(second);

            MiniTestRunner.IsTrue(ReferenceEquals(second, slot.Plan), "отдаётся последний план");
            MiniTestRunner.IsFalse(ReferenceEquals(first, slot.Plan), "старый план не отдаётся");
        }

        private static void BeginBuildRejectsNull()
        {
            var slot = new QuarterPlanSlot();
            bool thrown = false;

            try
            {
                slot.BeginBuild(null);
            }
            catch (ArgumentNullException)
            {
                thrown = true;
            }

            MiniTestRunner.IsTrue(thrown, "ArgumentNullException на null-плане");
            MiniTestRunner.IsFalse(slot.HasPlan, "после отказа плана нет");
        }

        private static void EmptySlotHasNoPlan()
        {
            var slot = new QuarterPlanSlot();

            MiniTestRunner.IsFalse(slot.HasPlan, "новый слот пуст");
            MiniTestRunner.IsTrue(slot.Plan == null, "Plan == null до сборки");
        }

        private static void NotifyBuiltRaisesPlanChanged()
        {
            var slot = new QuarterPlanSlot();
            INotifyQuarterPlanSource notifier = slot;
            int callCount = 0;
            QuarterPlan observed = null;
            notifier.PlanChanged += p =>
            {
                callCount++;
                observed = p;
            };

            QuarterPlan plan = DistrictMapGenerator.Generate(1904);
            slot.BeginBuild(plan);
            MiniTestRunner.AreEqual(0, callCount, "BeginBuild сам по себе не шлёт событие до завершения сборки геометрии");

            slot.NotifyBuilt();
            MiniTestRunner.AreEqual(1, callCount, "NotifyBuilt присылает ровно одно уведомление");
            MiniTestRunner.IsTrue(ReferenceEquals(plan, observed), "в PlanChanged передан собранный план");
        }

        private static void ClearRaisesPlanChangedWhenPopulated()
        {
            var slot = new QuarterPlanSlot();
            int callCount = 0;
            QuarterPlan observed = DistrictMapGenerator.Generate(1);
            slot.PlanChanged += p =>
            {
                callCount++;
                observed = p;
            };

            // Очистка пустого слота не должна спамить ложными событиями.
            slot.Clear();
            MiniTestRunner.AreEqual(0, callCount, "пустой Clear не шлёт PlanChanged");

            slot.BeginBuild(DistrictMapGenerator.Generate(2));
            slot.Clear();
            MiniTestRunner.AreEqual(1, callCount, "Clear после сборки шлёт PlanChanged");
            MiniTestRunner.IsTrue(observed == null, "при сносе района в PlanChanged приходит null");
        }
    }
}
