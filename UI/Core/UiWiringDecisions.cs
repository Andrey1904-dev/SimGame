// Life From Zero — UI/Core (ТЗ №045)
// Namespace: LifeFromZero.UI.Core
//
// ЗАЧЕМ ЭТОТ ФАЙЛ (регрессия после реального Play Mode).
//
// 1. Телефон. В UiBootstrap было два источника данных телефона: UiSnapshotSource
//    и DeliveryServiceBridge. Готовый мост PhoneFacadeUiBridge
//    (UnityIntegrationKit/PhoneUiBridge.cs) не искался вообще, а поле инспектора
//    «phoneSystem» не использовалось нигде. Итог в Play Mode: телефон открывался
//    с пустым рабочим столом. Здесь зафиксирован ПОРЯДОК выбора источника —
//    одним местом, которое можно проверить тестом.
//
// 2. Район. «План района не подключён» печаталось как ошибка и в Apartment67,
//    где района нет вообще (это нормально), и в District750, где план появляется
//    позже генерации. Здесь три состояния, а не одно: «сцена без района»,
//    «ждём генерацию», «готово».
//
// Модуль без UnityEngine.

namespace LifeFromZero.UI.Core
{
    /// <summary>Откуда телефон возьмёт данные.</summary>
    public enum PhoneSourceChoice
    {
        /// <summary>Источника нет: рабочий стол покажет заглушки и честное сообщение.</summary>
        None = 0,

        /// <summary>Поле «phoneSystem» в инспекторе UiBootstrap (явное назначение интегратора).</summary>
        PhoneSystemField = 1,

        /// <summary>DeliveryServiceBridge: заказы + (если передан) внешний телефонный мост.</summary>
        DeliveryBridge = 2,

        /// <summary>Другой компонент сцены, реализующий IPhoneDataSource (например PhoneFacadeUiBridge).</summary>
        ScenePhoneBridge = 3,

        /// <summary>UiSnapshotSource, реализующий IPhoneDataSource (PauseUiSnapshotSource интегратора).</summary>
        SnapshotSource = 4,
    }

    /// <summary>Порядок выбора источника данных телефона.</summary>
    public static class PhoneSourceResolver
    {
        /// <summary>
        /// Приоритет:
        /// 1) явное поле инспектора — интегратор знает лучше;
        /// 2) DeliveryServiceBridge — он объединяет заказы и внешний телефонный мост
        ///    (<c>externalPhoneDataSource</c>), поэтому сообщения не теряются;
        /// 3) любой другой IPhoneDataSource сцены (PhoneFacadeUiBridge);
        /// 4) UiSnapshotSource, если реализует IPhoneDataSource.
        /// </summary>
        public static PhoneSourceChoice Choose(
            bool phoneSystemHasSource,
            bool deliveryBridgeAvailable,
            bool scenePhoneBridgeAvailable,
            bool snapshotIsPhoneSource)
        {
            if (phoneSystemHasSource) return PhoneSourceChoice.PhoneSystemField;
            if (deliveryBridgeAvailable) return PhoneSourceChoice.DeliveryBridge;
            if (scenePhoneBridgeAvailable) return PhoneSourceChoice.ScenePhoneBridge;
            if (snapshotIsPhoneSource) return PhoneSourceChoice.SnapshotSource;
            return PhoneSourceChoice.None;
        }

        /// <summary>Нужно ли передать мосту доставки внешний телефонный мост, чтобы не потерять сообщения.</summary>
        public static bool NeedsExternalBridge(PhoneSourceChoice choice) =>
            choice == PhoneSourceChoice.DeliveryBridge;

        /// <summary>Короткое описание для отчёта UiBootstrap.</summary>
        public static string Describe(PhoneSourceChoice choice)
        {
            switch (choice)
            {
                case PhoneSourceChoice.PhoneSystemField: return "поле phoneSystem в UiBootstrap";
                case PhoneSourceChoice.DeliveryBridge: return "DeliveryServiceBridge";
                case PhoneSourceChoice.ScenePhoneBridge: return "IPhoneDataSource сцены";
                case PhoneSourceChoice.SnapshotSource: return "UiSnapshotSource";
                default: return "нет";
            }
        }
    }

    /// <summary>Состояние плана района для карты.</summary>
    public enum DistrictPlanState
    {
        /// <summary>Ничего не проверяли.</summary>
        Unknown = 0,

        /// <summary>В сцене нет источника района (квартира №67). Это НОРМА, а не ошибка.</summary>
        NoDistrictInScene = 1,

        /// <summary>Источник есть, план ещё не сгенерирован — ждём PlanChanged.</summary>
        AwaitingGeneration = 2,

        /// <summary>План есть, карта может рисовать район.</summary>
        Ready = 3,
    }

    /// <summary>Оценка состояния района: «норма», «ждём», «готово».</summary>
    public static class DistrictPlanDiagnostics
    {
        public static DistrictPlanState Evaluate(bool sourceFound, bool planReady)
        {
            if (planReady) return DistrictPlanState.Ready;
            return sourceFound ? DistrictPlanState.AwaitingGeneration : DistrictPlanState.NoDistrictInScene;
        }

        /// <summary>Проблема или норма. Ждать генерацию — не ошибка конфигурации, но и не «готово».</summary>
        public static bool IsProblem(DistrictPlanState state) => state == DistrictPlanState.AwaitingGeneration;

        /// <summary>Текст для отчёта <c>UiBootstrap</c>.</summary>
        public static string Message(DistrictPlanState state, string sourceName)
        {
            string name = string.IsNullOrEmpty(sourceName) ? "источник района" : sourceName;
            switch (state)
            {
                case DistrictPlanState.Ready:
                    return "план района (" + name + ")";
                case DistrictPlanState.AwaitingGeneration:
                    return "план района ещё не сгенерирован (" + name +
                           "): карта покажет сетку и подхватит район по PlanChanged";
                case DistrictPlanState.NoDistrictInScene:
                    return "сцена без района: карта показывает только сетку (норма для квартиры)";
                default:
                    return "состояние района не проверялось";
            }
        }
    }
}
