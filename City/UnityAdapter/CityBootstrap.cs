// Life From Zero — City/Quarter точка запуска для Unity (ТЗ №028)
// Namespace: LifeFromZero.City.Quarter.UnityAdapter
//
// CityBootstrap — ЕДИНСТВЕННЫЙ компонент, который нужно добавить на пустой
// GameObject в тестовой сцене CityTest. Он генерирует план квартала ядром
// (QuarterGenerator, seed=1904 по умолчанию) и строит его существующим
// QuarterUnityAdapter — функционал адаптера не дублируется.
//
// Использование (без программирования):
//   1. Пустой GameObject в сцене -> Add Component -> CityBootstrap.
//   2. Play. Город построен; сводка — в Console и в поле Last Report.
// Параметры настраиваются в Inspector; автоматическую генерацию можно
// отключить (Generate On Start = off) и запускать кнопкой компонента
// из кода: bootstrap.Generate() / bootstrap.Clear().
//
// В Unity в рамках этого ТЗ НЕ запускалось (Unity-проекта в репозитории
// нет): проверка компиляции — против стаба UnityEngine, см. City/README.md.

using System;
using System.Diagnostics;
using UnityEngine;

namespace LifeFromZero.City.Quarter.UnityAdapter
{
    /// <summary>Сводка одной генерации города. Заполняется в Generate(), не каждый кадр.</summary>
    [Serializable]
    public sealed class CityGenerationReport
    {
        public int Seed;
        /// <summary>Жилых зданий в плане (у утверждённой конфигурации seed=1904 — 15).</summary>
        public int ResidentialBuildings;
        /// <summary>Всего размещённых объектов в плане (здания, дороги, дворы, детали).</summary>
        public int TotalObjects;
        public int DeliveryPoints;
        /// <summary>Сколько генераций завершилось ошибкой за время жизни компонента.</summary>
        public int ErrorCount;
        /// <summary>Время генерации плана + построения, миллисекунды (Stopwatch).</summary>
        public float GenerationTimeMs;
        /// <summary>Сколько раз вызывался Generate() за время жизни компонента.</summary>
        public int GenerationCount;
        /// <summary>True, если последний Build() был повторным (старый квартал уничтожен).</summary>
        public bool LastBuildWasRepeat;
        /// <summary>Имя корневого GameObject построенного квартала (пусто после Clear()).</summary>
        public string RootName = "";
        /// <summary>Текст последней ошибки генерации (пусто, если ошибок не было).</summary>
        public string LastError = "";
    }

    /// <summary>
    /// Точка запуска города: генерация + построение + диагностика одной кнопкой.
    /// Адаптер QuarterUnityAdapter добавляется автоматически на тот же объект,
    /// его собственный автозапуск отключается (генерацией управляет Bootstrap).
    /// </summary>
    public sealed class CityBootstrap : MonoBehaviour
    {
        [Header("Генерация города")]
        [SerializeField] private int seed = 1904;
        /// <summary>Макет: quarter250 (по умолчанию, 250×250) или district750 (ТЗ №031, 750×750).</summary>
        [SerializeField] private string layoutId = QuarterLayoutIds.Quarter250;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool addColliders = true;

        [Header("Диагностика (заполняется при генерации)")]
        [SerializeField] private CityGenerationReport lastReport = new CityGenerationReport();

        private QuarterUnityAdapter _adapter;
        private int _errorCount;

        /// <summary>Последняя сводка генерации (та же, что видна в Inspector).</summary>
        public CityGenerationReport LastReport => lastReport;
        /// <summary>Seed из Inspector (меняется до Play или между генерациями из кода).</summary>
        public int Seed { get => seed; set => seed = value; }
        /// <summary>Строить ли город автоматически при старте сцены.</summary>
        public bool GenerateOnStart { get => generateOnStart; set => generateOnStart = value; }
        /// <summary>Выбранный макет (QuarterLayoutIds.Quarter250 или District750).</summary>
        public string LayoutId { get => layoutId; set => layoutId = value; }

        private void Start()
        {
            if (generateOnStart)
                Generate();
        }

        /// <summary>
        /// Генерирует план по текущему seed и строит квартал. Повторный вызов
        /// безопасен: предыдущий квартал уничтожается адаптером, дублирования
        /// объектов и наложения городов не происходит. При ошибке конфигурации
        /// (например, ядро отклонило квоты) пишет понятную ошибку в Console,
        /// наращивает ErrorCount и не оставляет полупостроенного города.
        /// </summary>
        public CityGenerationReport Generate()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                QuarterPlan plan = layoutId == QuarterLayoutIds.District750
                    ? DistrictMapGenerator.Generate(seed)
                    : QuarterGenerator.Generate(seed);
                QuarterUnityAdapter adapter = Adapter();
                GameObject root = adapter.Build(plan, transform);
                stopwatch.Stop();

                bool repeat = lastReport.GenerationCount > 0;
                lastReport = new CityGenerationReport
                {
                    Seed = plan.Seed,
                    ResidentialBuildings = plan.Buildings.Count,
                    TotalObjects = plan.Objects.Count,
                    DeliveryPoints = plan.DeliveryPoints.Count,
                    ErrorCount = _errorCount,
                    GenerationTimeMs = (float)stopwatch.Elapsed.TotalMilliseconds,
                    GenerationCount = lastReport.GenerationCount + 1,
                    LastBuildWasRepeat = repeat,
                    RootName = root != null ? root.name : "",
                    LastError = "",
                };
                UnityEngine.Debug.Log(
                    "[CityBootstrap] Город построен: макет=" + plan.LayoutId + ", seed=" + plan.Seed +
                    ", жилых зданий=" + plan.Buildings.Count +
                    ", объектов=" + plan.Objects.Count +
                    ", точек доставки=" + plan.DeliveryPoints.Count +
                    ", время=" + lastReport.GenerationTimeMs.ToString("0") + " мс" +
                    (repeat ? " (повторный Build: предыдущий квартал уничтожен)" : "") + ".");
                return lastReport;
            }
            catch (Exception e)
            {
                stopwatch.Stop();
                _errorCount++;
                lastReport.ErrorCount = _errorCount;
                lastReport.LastError = e.Message;
                UnityEngine.Debug.LogError(
                    "[CityBootstrap] Генерация города не удалась (seed=" + seed + "): " + e.Message +
                    " Проверьте параметры CityBootstrap и сообщения ядра; полупостроенного квартала не оставлено.");
                return lastReport;
            }
        }

        /// <summary>Уничтожает построенный квартал и освобождает его меши (материалы кэша — за адаптером).</summary>
        public void Clear()
        {
            if (_adapter != null)
                _adapter.ClearBuilt();
            lastReport.RootName = "";
        }

        private QuarterUnityAdapter Adapter()
        {
            if (_adapter == null)
            {
                _adapter = GetComponent<QuarterUnityAdapter>();
                if (_adapter == null)
                    _adapter = gameObject.AddComponent<QuarterUnityAdapter>();
                // Автозапуск адаптера выключаем: генерацией управляет Bootstrap,
                // иначе в Start построились бы два квартала.
                _adapter.BuildOnStart = false;
                _adapter.AddColliders = addColliders;
                _adapter.Seed = seed;
            }
            return _adapter;
        }
    }
}
