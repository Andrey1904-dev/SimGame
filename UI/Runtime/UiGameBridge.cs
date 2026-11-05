// Life From Zero — UI/Runtime/UiGameBridge.cs (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// МОСТ К УЖЕ СУЩЕСТВУЮЩИМ СИСТЕМАМ. Здесь заканчивается «UI сам по себе» и
// начинается «UI в игре», но без единой выдуманной цифры.
//
// Правила, которые соблюдает этот файл:
//   1. Данные берутся из того, что уже есть в проекте (City.Pause.PauseDataSource,
//      адреса из District750Link). Если источника нет — поле остаётся null, и UI
//      честно пишет «Нет данных». Ничего не подставляется «для красоты».
//   2. Пауза не создаётся заново. В проекте уже есть City.Pause (PauseController +
//      PauseView). Если он найден — наш экран паузы не открывается, а действие
//      Resume/Save/... уходит в существующий PauseIntegration.Execute(...).
//      Пока это не сделано, в игре было бы ДВА меню паузы на Esc.
//   3. Позиция игрока ищется честно: явная ссылка → камера с тегом MainCamera →
//      любой включённый Camera. Если не нашлось — TryGetPlayerPose возвращает
//      false, и карта не рисует ложную точку в (0,0).
//   4. Заказы (IDeliveryUiBridge) НЕ реализованы: в репозитории нет DeliveryService
//      и PhoneFacade, поэтому нечего вызывать. Вместо заглушки — поле остаётся
//      пустым, а отчёт установщика перечисляет, что нужно подключить.

using System;
using System.Collections.Generic;
using UnityEngine;
using LifeFromZero.City.Pause;
using LifeFromZero.City.Quarter;
using LifeFromZero.UI.Core;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>
    /// Готовый мост «позиция игрока + HUD/пауза» для сцен, где уже есть камера
    /// и (необязательно) City.Pause. Ставится установщиком; всё настраивается.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class UiGameBridge : UiWorldAdapter
    {
        [Header("Позиция игрока (пусто — ищем камеру)")]
        [Tooltip("Трансформ игрока. Пусто — берётся камера: тег MainCamera, затем любая включённая.")]
        [SerializeField] private Transform player;

        [Tooltip("Если включено и игрок не найден, TryGetPlayerPose вернёт false (карта не рисует точку в 0,0).")]
        [SerializeField] private bool requirePlayer = true;

        [Header("Существующие игровые HUD-корни")]
        [Tooltip("Объекты игрового HUD, которые скрываются/приглушаются при открытом UI.")]
        [SerializeField] private GameObject[] gameplayHudRoots;

        [Header("Блокировка ввода")]
        [Tooltip("Компоненты ввода персонажа/камеры, которые отключаются на время модального окна.")]
        [SerializeField] private Behaviour[] playerInputBehaviours;

        [Header("Существующая пауза (City.Pause)")]
        [Tooltip("Пусто — ищется PauseIntegration в сцене. Найденный используется вместо собственного меню.")]
        [SerializeField] private PauseIntegration existingPause;

        /// <summary>Найденная существующая пауза (может быть null).</summary>
        public PauseIntegration ExistingPause => existingPause;

        /// <summary>Есть ли в сцене чужая пауза, которую нельзя дублировать.</summary>
        public bool HasExistingPause => existingPause != null;

        public Transform Player => ResolvePlayer();

        private void Awake() => ResolveReferences();

        /// <summary>Найти камеру/паузу, если они не назначены в инспекторе.</summary>
        public void ResolveReferences()
        {
            if (existingPause == null) existingPause = FindAnyObjectByType<PauseIntegration>();
            if (player == null)
            {
                Camera camera = Camera.main;
                if (camera == null)
                {
                    Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                    for (int i = 0; i < cameras.Length; i++)
                    {
                        if (cameras[i] != null && cameras[i].isActiveAndEnabled) { camera = cameras[i]; break; }
                    }
                }
                if (camera != null) player = camera.transform;
            }
        }

        private Transform ResolvePlayer()
        {
            if (player != null) return player;
            ResolveReferences();
            return player;
        }

        public override bool TryGetPlayerPose(out Vector3 position, out float headingDegrees)
        {
            Transform target = ResolvePlayer();
            if (target == null)
            {
                position = Vector3.zero;
                headingDegrees = 0f;
                return !requirePlayer;
            }

            position = target.position;
            headingDegrees = target.eulerAngles.y;
            return true;
        }

        public override void SetHudVisibility(HudVisibility visibility)
        {
            if (gameplayHudRoots == null) return;
            bool visible = visibility != HudVisibility.Hidden;
            for (int i = 0; i < gameplayHudRoots.Length; i++)
            {
                GameObject root = gameplayHudRoots[i];
                if (root == null) continue;
                if (root.activeSelf != visible) root.SetActive(visible);
            }

            // Приглушение — через CanvasGroup, если он есть: цвет/альфу чужого HUD
            // не трогаем, чтобы не перекрашивать чужие элементы.
            for (int i = 0; i < gameplayHudRoots.Length; i++)
            {
                GameObject root = gameplayHudRoots[i];
                if (root == null) continue;
                var group = root.GetComponent<CanvasGroup>();
                if (group == null) continue;
                float alpha = visibility == HudVisibility.Muted ? 0.35f : 1f;
                group.alpha = alpha;
            }
        }

        public override IDisposable LockGameplayInput(string reason)
        {
            if (playerInputBehaviours == null || playerInputBehaviours.Length == 0)
            {
                // Нечего блокировать — управление останется за игроком. Это хуже, чем
                // блокировка, но лучше, чем остановить время и оставить ввод активным.
                Debug.LogWarning("[UiGameBridge] Не назначены Player Input Behaviours: " +
                                 "на время окна персонаж продолжит двигаться (" + reason + ").");
                return null;
            }

            var locked = new List<Behaviour>(playerInputBehaviours.Length);
            for (int i = 0; i < playerInputBehaviours.Length; i++)
            {
                Behaviour behaviour = playerInputBehaviours[i];
                if (behaviour == null || !behaviour.enabled) continue;
                behaviour.enabled = false;
                locked.Add(behaviour);
            }

            return new InputLock(locked);
        }

        public override bool IsMenuActionAvailable(string actionId) => Availability(actionId);

        public override bool ExecuteMenuAction(string actionId)
        {
            // 1. Существующая пауза проекта имеет приоритет: она знает свои окна.
            PauseAction pauseAction;
            if (existingPause != null && TryMap(actionId, out pauseAction))
            {
                if (!existingPause.IsAvailable(pauseAction)) return false;
                existingPause.Execute(pauseAction);
                return true;
            }

            // 2. Действия, которые UI делает сам (навигация по своим экранам).
            UiRouter router = UiRouter.Instance;
            if (router == null) return false;

            switch (actionId)
            {
                case PauseActions.Phone:
                    router.Request(UiInputAction.Phone);
                    return true;
                case PauseActions.Map:
                    router.Request(UiInputAction.Map);
                    return true;
                case PauseActions.Tasks:
                case PauseActions.Inventory:
                    // Задачи и инвентарь живут внутри меню паузы: открываем его и
                    // передаём действие существующей паузе (City.Pause), если она есть.
                    if (existingPause == null) return false;
                    PauseAction quick;
                    if (!TryMap(actionId, out quick) || !existingPause.IsAvailable(quick)) return false;
                    existingPause.Execute(quick);
                    return true;
                case PauseActions.Resume:
                    router.ReturnToGameplay();
                    return true;
            }

            return false;
        }

        public override string MenuActionShortcut(string actionId)
        {
            PauseAction pauseAction;
            if (existingPause == null || !TryMap(actionId, out pauseAction)) return null;
            return existingPause.ShortcutLabel(pauseAction);
        }

        private bool Availability(string actionId)
        {
            PauseAction pauseAction;
            if (existingPause != null && TryMap(actionId, out pauseAction)) return existingPause.IsAvailable(pauseAction);

            UiRouter router = UiRouter.Instance;
            if (router == null) return false;

            switch (actionId)
            {
                case PauseActions.Phone:
                case PauseActions.Map:
                case PauseActions.Tasks:
                case PauseActions.Inventory:
                case PauseActions.Resume:
                    return true;
                default:
                    return false; // save/load/settings/controls/main-menu/quit — нечем выполнять
            }
        }

        internal static bool TryMap(string actionId, out PauseAction action)
        {
            switch (actionId)
            {
                case PauseActions.Save: action = PauseAction.Save; return true;
                case PauseActions.Load: action = PauseAction.Load; return true;
                case PauseActions.Settings: action = PauseAction.Settings; return true;
                case PauseActions.Controls: action = PauseAction.Controls; return true;
                case PauseActions.MainMenu: action = PauseAction.MainMenu; return true;
                case PauseActions.Phone: action = PauseAction.Phone; return true;
                case PauseActions.Inventory: action = PauseAction.Inventory; return true;
                case PauseActions.Map: action = PauseAction.Map; return true;
                case PauseActions.Tasks: action = PauseAction.Tasks; return true;
                default: action = PauseAction.Save; return false;
            }
        }

        private sealed class InputLock : IDisposable
        {
            private List<Behaviour> _locked;
            private bool _disposed;

            public InputLock(List<Behaviour> locked) => _locked = locked;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_locked == null) return;
                for (int i = 0; i < _locked.Count; i++)
                {
                    Behaviour behaviour = _locked[i];
                    if (behaviour != null) behaviour.enabled = true;
                }
                _locked = null;
            }
        }
    }

    /// <summary>
    /// Источник данных HUD/паузы поверх СУЩЕСТВУЮЩЕГО <see cref="PauseDataSource"/>
    /// проекта. Ничего не выдумывает: чего нет в PauseSnapshot — остаётся «Нет данных».
    /// Дополнение к нему — навигация и подсказка взаимодействия (их выставляет UiRouter).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class PauseUiSnapshotSource : UiSnapshotSource
    {
        [Tooltip("Существующий источник данных игры. Пусто — ищется PauseDataSource в сцене.")]
        [SerializeField] private PauseDataSource dataSource;

        private bool _hooked;

        /// <summary>Есть ли реальный источник данных.</summary>
        public bool HasSource => dataSource != null;

        private void Awake()
        {
            Resolve();
            Hook();
        }

        private void OnDestroy() => Unhook();

        /// <summary>Подхватить источник из сцены и подписаться на его изменения.</summary>
        public void Resolve()
        {
            if (dataSource == null) dataSource = FindAnyObjectByType<PauseDataSource>();
            Hook();
        }

        /// <summary>Принудительно перечитать данные (после смены сцены).</summary>
        public void RefreshSource()
        {
            Resolve();
            NotifyChanged();
        }

        private void Hook()
        {
            if (_hooked || dataSource == null) return;
            dataSource.Changed += OnSourceChanged;
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked || dataSource == null) return;
            dataSource.Changed -= OnSourceChanged;
            _hooked = false;
        }

        private void OnSourceChanged() => NotifyChanged();

        public override void ReadInto(UiSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (dataSource == null)
            {
                Resolve();
                if (dataSource == null) return; // «Нет данных» остаётся честным
            }

            PauseSnapshot source;
            try
            {
                source = dataSource.Read();
            }
            catch (Exception exception)
            {
                Debug.LogError("[PauseUiSnapshotSource] Read() выбросил исключение: " + exception.Message);
                return;
            }
            if (source == null) return;

            snapshot.Clock.DayLabel = source.Day;
            snapshot.Clock.TimeLabel = source.Time;
            snapshot.Clock.Location = source.Location;
            snapshot.Clock.LocationDetail = source.LocationDetail;

            snapshot.Status.Health = PercentValue.From(source.Health);
            snapshot.Status.Satiety = PercentValue.From(source.ResolveSatiety());
            snapshot.Status.Energy = PercentValue.From(source.Energy);
            snapshot.Status.Mood = PercentValue.From(source.ResolveMood());

            snapshot.Finance.Money = ToLong(source.Money);
            snapshot.Finance.Debt = ToLong(source.Debt);
            snapshot.Finance.DailyIncome = ToLong(source.DailyIncome);

            if (source.Goals != null && source.Goals.Length > 0)
            {
                int count = Mathf.Min(source.Goals.Length, UiSnapshot.MaxGoals);
                var goals = new GoalView[count];
                for (int i = 0; i < count; i++)
                {
                    PauseGoal goal = source.Goals[i];
                    if (goal == null) { goals[i] = new GoalView("", false, null); continue; }
                    goals[i] = new GoalView(goal.Title, goal.Complete, goal.Progress);
                }
                snapshot.Goals = goals;
            }
        }

        /// <summary>Деньги в UI — целые рубли. Округление к ближайшему, без «магии».</summary>
        private static long? ToLong(decimal? value)
        {
            if (!value.HasValue) return null;
            return (long)Math.Round(value.Value, MidpointRounding.AwayFromZero);
        }
    }
}
