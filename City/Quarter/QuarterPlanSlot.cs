// Life From Zero — City/Quarter
//
// Жизненный цикл «последнего построенного плана» (ТЗ №042, регрессия ТЗ №043).
//
// Зачем отдельный класс: раньше план хранился полем внутри MonoBehaviour
// QuarterUnityAdapter, а порядок «присвоить план → очистить построенное» был
// перепутан, из-за чего Plan/HasPlan всегда оставались пустыми. MonoBehaviour
// нельзя прогонять вне Unity, поэтому ошибка не ловилась тестами. Здесь
// последовательность вынесена в чистый класс, который проверяется без редактора.
//
// Контракт:
//   * Clear() сбрасывает план: он описывает снесённый квартал;
//   * BeginBuild(plan) — единственная точка, где план появляется. Он СНАЧАЛА
//     очищает предыдущее состояние, ПОТОМ записывает новый план;
//   * Plan == null тогда и только тогда, когда квартал не построен.

using System;

namespace LifeFromZero.City.Quarter
{
    /// <summary>Хранилище плана последней успешной сборки квартала.</summary>
    public sealed class QuarterPlanSlot : INotifyQuarterPlanSource
    {
        /// <summary>План последней сборки или null, если квартал не построен/снесён.</summary>
        public QuarterPlan Plan { get; private set; }

        /// <summary>План уже сгенерирован и стоит в сцене.</summary>
        public bool HasPlan => Plan != null;

        /// <summary>
        /// Событие смены текущего плана: <c>null</c> при очистке, готовый <see cref="QuarterPlan"/>
        /// после завершения сборки (<see cref="NotifyBuilt"/>).
        /// </summary>
        public event Action<QuarterPlan> PlanChanged;

        /// <summary>
        /// Сбрасывает план. Вызывается при сносе построенного квартала: держать
        /// план снесённого района нельзя — карта нарисовала бы несуществующий город.
        /// </summary>
        public void Clear()
        {
            bool hadPlan = Plan != null;
            Plan = null;
            if (hadPlan) PlanChanged?.Invoke(null);
        }

        /// <summary>
        /// Начало сборки: очистка предыдущего состояния, затем фиксация нового плана.
        /// Порядок операций — часть контракта (см. комментарий в шапке файла).
        /// </summary>
        public QuarterPlan BeginBuild(QuarterPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            Plan = null;
            Plan = plan;
            return Plan;
        }

        /// <summary>
        /// Уведомляет подписчиков, что сборка объектов квартала по <see cref="Plan"/> завершена.
        /// </summary>
        public void NotifyBuilt()
        {
            if (Plan != null) PlanChanged?.Invoke(Plan);
        }
    }
}
