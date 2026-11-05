// Life From Zero — City/Quarter
//
// Контракт «отдай текущий план района» (ТЗ №042).
//
// Живёт в модуле данных, а не в UI: и адаптер города (City.Quarter.UnityAdapter),
// и интерфейс (LifeFromZero.UI) ссылаются на этот модуль, поэтому интерфейс может
// читать план, не завися от сборочной единицы адаптера (иначе — цикл зависимостей).

namespace LifeFromZero.City.Quarter
{
    /// <summary>Источник последнего сгенерированного плана района.</summary>
    public interface IQuarterPlanSource
    {
        /// <summary>План последней генерации или null, если район ещё не построен.</summary>
        QuarterPlan Plan { get; }
    }
}
