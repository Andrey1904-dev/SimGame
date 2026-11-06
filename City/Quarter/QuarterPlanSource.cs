// Life From Zero — City/Quarter
//
// Контракт «отдай текущий план района» (ТЗ №042).
//
// Живёт в модуле данных, а не в UI: и адаптер города (City.Quarter.UnityAdapter),
// и интерфейс (LifeFromZero.UI) ссылаются на этот модуль, поэтому интерфейс может
// читать план, не завися от сборочной единицы адаптера (иначе — цикл зависимостей).

using System;

namespace LifeFromZero.City.Quarter
{
    /// <summary>Источник последнего сгенерированного плана района.</summary>
    public interface IQuarterPlanSource
    {
        /// <summary>План последней генерации или null, если район ещё не построен.</summary>
        QuarterPlan Plan { get; }
    }

    /// <summary>
    /// Источник плана района с уведомлением о завершении сборки или очистке.
    /// Позволяет UI (карте, навигации и доставке) получить план независимо от того,
    /// вызвался ли <c>CityBootstrap.Start()</c> раньше или позже <c>UiBootstrap.Awake()</c>.
    /// </summary>
    public interface INotifyQuarterPlanSource : IQuarterPlanSource
    {
        /// <summary>Вызывается после построения района (ненулевой план) или его сноса (<c>null</c>).</summary>
        event Action<QuarterPlan> PlanChanged;
    }
}
