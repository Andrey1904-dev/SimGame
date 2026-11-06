// Life From Zero — UI/Runtime (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// Единый владелец интерфейсов: состояния, ввод, курсор, блокировка игрового ввода,
// видимость HUD. НИ ОДИН контроллер окна не читает клавиши сам (ТЗ §2.1, §13).
//
// Зависимости минимальны и намеренно необязательны:
//   * состояние паузы и блокировка ввода — через уже существующий LifeFromZero.City.Pause
//     (PauseController/PauseIntegration). Своей второй системы паузы здесь НЕТ.
//   * если City.Pause в проекте отсутствует, работает встроенный режим: timeScale
//     сохраняется и восстанавливается этим же компонентом.
//
// Файл компилируется только при наличии UnityEngine — вне Unity тестируется UI/Core.

using System;
using System.Collections.Generic;
using LifeFromZero.UI.Core;
using UnityEngine;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Что должен сделать роутер по нажатию клавиши.</summary>
    public enum UiInputAction
    {
        None = 0,
        Phone = 1,
        Map = 2,
        Back = 3,
        Interact = 4,
        Inventory = 5,
        Tasks = 6,
        Confirm = 7,
    }

    /// <summary>
    /// Источник данных HUD/паузы. Реализуется адаптером Codex поверх существующих
    /// систем (GameClock, MoneyWallet, Debts, потребности). Незаполненные делегаты
    /// означают «нет данных», а не ноль.
    /// </summary>
    public abstract class UiSnapshotSource : MonoBehaviour
    {
        private Action _changed;

        public event Action Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        /// <summary>Сообщить, что данные изменились (без опроса каждый кадр).</summary>
        protected void RaiseChanged() => _changed?.Invoke();

        /// <summary>Публичный вызов для адаптеров.</summary>
        public void NotifyChanged() => RaiseChanged();

        public abstract void ReadInto(UiSnapshot snapshot);
    }

    /// <summary>
    /// Мост к игре: положение игрока, направление взгляда, корабль HUD, выполнение действий.
    /// </summary>
    public abstract class UiWorldAdapter : MonoBehaviour
    {
        /// <summary>Мировая позиция игрока (X/Z).</summary>
        public abstract bool TryGetPlayerPose(out Vector3 position, out float headingDegrees);

        /// <summary>Полностью скрыть/показать обычный игровой HUD (ТЗ §8.3, §11).</summary>
        public abstract void SetHudVisibility(HudVisibility visibility);

        /// <summary>Заблокировать/разблокировать игровой ввод персонажа и камеры.</summary>
        public abstract IDisposable LockGameplayInput(string reason);

        /// <summary>Выполнить действие меню (сохранение, загрузка, выход и т. п.).</summary>
        public virtual bool ExecuteMenuAction(string actionId) => false;

        /// <summary>Доступно ли действие (кнопка иначе disabled).</summary>
        public virtual bool IsMenuActionAvailable(string actionId) => false;

        /// <summary>Горячая клавиша действия для подписи в меню, если она отличается от стандартной.</summary>
        public virtual string MenuActionShortcut(string actionId) => null;
    }

    /// <summary>
    /// Центральный контроллер интерфейсов. Живёт один на игру (DontDestroyOnLoad),
    /// переживает переходы между Apartment67 и District750 и не создаёт дубликатов.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class UiRouter : MonoBehaviour
    {
        public static UiRouter Instance { get; private set; }

        [Header("Связи (назначает установщик)")]
        [SerializeField] private UiWorldAdapter world;
        [SerializeField] private UiSnapshotSource snapshotSource;
        [SerializeField] private PhoneScreen phone;
        [SerializeField] private MapScreen map;
        [SerializeField] private HudScreen hud;
        [SerializeField] private PauseMenuScreen pause;

        [Header("Ввод")]
        [Tooltip("Читать клавиши самому. Выключите, если P/M/Esc уже маршрутизируются вашим вводом — тогда зовите Request().")]
        [SerializeField] private bool readKeyboard = true;

        [Tooltip("Использовать Legacy Input Manager, если Input System недоступен.")]
        [SerializeField] private bool allowLegacyInput = true;

        [Header("Пауза")]
        [Tooltip("Остановить время, если в проекте нет City.Pause. Если City.Pause найден, он управляет паузой сам.")]
        [SerializeField] private bool fallbackTimeScalePause = true;

        [Header("Отладка")]
        [SerializeField] private bool verboseLogging;

        private readonly UiStateMachine _states = new UiStateMachine();
        private readonly List<IDisposable> _locks = new List<IDisposable>(2);
        private bool _modalActive;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLock;
        private float _previousTimeScale = 1f;
        private bool _timeScalePaused;
        private bool _subscribed;
        private bool _quitting;
        private bool _lastExternalPauseOpen;
        private UiSnapshotSource _subscribedSource;

        /// <summary>Машина состояний. Только чтение для остальных контроллеров.</summary>
        public UiStateMachine States => _states;

        public UiScreen Screen => _states.Screen;

        public bool IsModalOpen => _states.IsModalOpen;

        /// <summary>
        /// В проекте присутствует внешний владелец паузы (<c>City.Pause</c>).
        /// В этом режиме <c>UiRouter</c> продолжает обрабатывать <c>P</c> (телефон),
        /// <c>M</c> (карта) и закрытие своих окон по <c>Esc</c>, а <c>Esc</c> в обычной
        /// игре уступает <c>City.Pause</c>.
        /// </summary>
        public bool ExternalPauseOwner { get; set; }

        /// <summary>Проверка, открыто ли сейчас внешнее меню паузы (<c>PauseController.IsOpen</c>).</summary>
        public Func<bool> IsExternalPauseOpen { get; set; }

        /// <summary>Возврат во внешнее меню паузы после закрытия окна, открытого из него.</summary>
        public Action ReopenExternalPause { get; set; }

        /// <summary>Событие входа/выхода из модального окна UI (для <c>ModalGuard</c> против гонки Esc).</summary>
        public event Action<bool> ModalOpenChanged;

        /// <summary>
        /// Останавливать ли время самому. Выключается, когда в сцене уже есть
        /// City.Pause: иначе два владельца Time.timeScale будут мешать друг другу
        /// (второй «продолжит» игру, пока первый ещё в паузе).
        /// </summary>
        public bool FallbackTimeScalePause
        {
            get { return fallbackTimeScalePause; }
            set { fallbackTimeScalePause = value; }
        }

        /// <summary>Читать ли клавиши самому (P/M/Esc).</summary>
        public bool ReadsKeyboard
        {
            get { return readKeyboard; }
            set { readKeyboard = value; }
        }

        /// <summary>Единый сервис навигации (маршрут, метки).</summary>
        public NavigationService Navigation { get; private set; }

        public UiWorldAdapter World => world;

        public UiSnapshotSource SnapshotSource => snapshotSource;

        /// <summary>Текущий снимок данных (для окон).</summary>
        public UiSnapshot Snapshot { get; private set; } = new UiSnapshot();

        /// <summary>Последнее событие интерфейса (для HUD/телефона).</summary>
        public event Action<UiScreen> ScreenChanged;

        /// <summary>Курсор сейчас должен быть свободен.</summary>
        public bool RequiresCursor => _states.RequiresCursor;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Дубликат из другой сцены: не удаляем объект вслепую, но и не работаем.
                Debug.LogWarning("[UiRouter] Найден второй экземпляр — отключаю дубликат. " +
                                 "Оставлен объект «" + Instance.gameObject.name + "».");
                enabled = false;
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Navigation = new NavigationService();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Subscribe();
            RefreshSnapshot();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            Unsubscribe();
            ReleaseAllLocks();
            Instance = null;
        }

        private void OnApplicationQuit() => _quitting = true;

        // ------------------------------------------------------------------ конфигурация

        /// <summary>
        /// Перепривязка к сервисам новой сцены. Вызывается из адаптера после загрузки
        /// сцены (в Apartment67 и District750 разные источники данных и камера).
        /// </summary>
        public void Configure(
            UiWorldAdapter newWorld,
            UiSnapshotSource newSource,
            PhoneScreen newPhone = null,
            MapScreen newMap = null,
            HudScreen newHud = null,
            PauseMenuScreen newPause = null)
        {
            Unsubscribe();
            world = newWorld;
            snapshotSource = newSource;
            if (newPhone != null) phone = newPhone;
            if (newMap != null) map = newMap;
            if (newHud != null) hud = newHud;
            if (newPause != null) pause = newPause;

            // Переключение сцены не должно оставлять открытым модальное окно.
            if (_states.IsModalOpen) ApplyState(_states.Screen, true);
            _states.Reset();

            Subscribe();
            RefreshSnapshot();
            ApplyPresentation();
        }

        private void Subscribe()
        {
            if (snapshotSource == null) return;
            if (_subscribed && ReferenceEquals(_subscribedSource, snapshotSource)) return;
            Unsubscribe();
            snapshotSource.Changed += OnSnapshotChanged;
            _subscribedSource = snapshotSource;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_subscribedSource != null) _subscribedSource.Changed -= OnSnapshotChanged;
            _subscribedSource = null;
            _subscribed = false;
        }

        private void OnSnapshotChanged()
        {
            RefreshSnapshot();
            if (hud != null && hud.isActiveAndEnabled) hud.Render(Snapshot, Navigation);
            if (pause != null && pause.isActiveAndEnabled) pause.Render(Snapshot, Navigation, world);
        }

        /// <summary>Перечитать данные из игры. Дешёвая операция, вызывается по событию.</summary>
        public void RefreshSnapshot()
        {
            if (snapshotSource == null) return;
            var fresh = new UiSnapshot();
            try
            {
                snapshotSource.ReadInto(fresh);
            }
            catch (Exception exception)
            {
                Debug.LogError("[UiRouter] Источник данных UI выбросил исключение: " + exception.Message);
                return;
            }

            // Навигация всегда берётся из единого сервиса — окна не считают её сами.
            if (Navigation != null && Navigation.ActiveRoute != null)
            {
                fresh.Objective = Navigation.ActiveRoute.DestinationLabel;
                fresh.RemainingDistance = RouteNavigator.RemainingDistance(
                    Navigation.ActiveRoute, PlayerPosition());
            }

            Snapshot = fresh;
        }

        private MapPoint PlayerPosition()
        {
            Vector3 position;
            float heading;
            if (world != null && world.TryGetPlayerPose(out position, out heading))
                return new MapPoint(position.x, position.z);
            return new MapPoint(0f, 0f);
        }

        // ------------------------------------------------------------------ ввод

        private void Update()
        {
            if (_quitting) return;

            bool externalPauseOpen = ExternalPauseOwner && IsExternalPauseOpen != null && IsExternalPauseOpen();
            if (externalPauseOpen != _lastExternalPauseOpen)
            {
                _lastExternalPauseOpen = externalPauseOpen;
                ApplyPresentation();
            }

            if (!readKeyboard) return;

            // Пока открыта внешняя пауза (City.Pause), клавиши Esc и P обслуживает она сама.
            if (externalPauseOpen && !_states.IsModalOpen) return;

            UiInputAction action = ReadKeyboard();
            if (action == UiInputAction.None) return;

            // В обычной игре Esc принадлежит внешнему меню паузы (City.Pause).
            if (ExternalPauseOwner && _states.Screen == UiScreen.Gameplay && action == UiInputAction.Back)
                return;

            Request(action);
        }

        private UiInputAction ReadKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            UiInputAction inputSystemAction = InputSystemKeyboardReader.Read();
            if (inputSystemAction != UiInputAction.None) return inputSystemAction;
            if (InputSystemKeyboardReader.HasCurrentKeyboard) return UiInputAction.None;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (allowLegacyInput)
            {
                if (Input.GetKeyDown(KeyCode.P)) return UiInputAction.Phone;
                if (Input.GetKeyDown(KeyCode.M)) return UiInputAction.Map;
                if (Input.GetKeyDown(KeyCode.Escape)) return UiInputAction.Back;
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) return UiInputAction.Confirm;
                if (Input.GetKeyDown(KeyCode.I)) return UiInputAction.Inventory;
                if (Input.GetKeyDown(KeyCode.J)) return UiInputAction.Tasks;
            }
#endif
            return UiInputAction.None;
        }

        /// <summary>
        /// Чтение <c>UnityEngine.InputSystem.Keyboard.current</c> без жёсткой сборочной ссылки
        /// <c>LifeFromZero.UI.asmdef</c> на <c>Unity.InputSystem</c> (иначе CS0234 в Unity 6
        /// при глобальном <c>ENABLE_INPUT_SYSTEM</c>).
        /// </summary>
        private static class InputSystemKeyboardReader
        {
            private static bool _initialized;
            private static System.Reflection.PropertyInfo _currentProp;
            private static Func<object, bool> _pPressed;
            private static Func<object, bool> _mPressed;
            private static Func<object, bool> _escPressed;
            private static Func<object, bool> _enterPressed;
            private static Func<object, bool> _numpadEnterPressed;
            private static Func<object, bool> _iPressed;
            private static Func<object, bool> _jPressed;

            public static bool HasCurrentKeyboard
            {
                get
                {
                    EnsureInitialized();
                    return _currentProp != null && _currentProp.GetValue(null, null) != null;
                }
            }

            public static UiInputAction Read()
            {
                EnsureInitialized();
                if (_currentProp == null) return UiInputAction.None;

                object keyboard = _currentProp.GetValue(null, null);
                if (keyboard == null) return UiInputAction.None;

                if (_pPressed != null && _pPressed(keyboard)) return UiInputAction.Phone;
                if (_mPressed != null && _mPressed(keyboard)) return UiInputAction.Map;
                if (_escPressed != null && _escPressed(keyboard)) return UiInputAction.Back;
                if ((_enterPressed != null && _enterPressed(keyboard)) ||
                    (_numpadEnterPressed != null && _numpadEnterPressed(keyboard)))
                    return UiInputAction.Confirm;
                if (_iPressed != null && _iPressed(keyboard)) return UiInputAction.Inventory;
                if (_jPressed != null && _jPressed(keyboard)) return UiInputAction.Tasks;
                return UiInputAction.None;
            }

            private static void EnsureInitialized()
            {
                if (_initialized) return;
                _initialized = true;

                Type keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem")
                                    ?? Type.GetType("UnityEngine.InputSystem.Keyboard");
                if (keyboardType == null) return;

                _currentProp = keyboardType.GetProperty("current",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (_currentProp == null) return;

                _pPressed = BindKeyWasPressed(keyboardType, "pKey");
                _mPressed = BindKeyWasPressed(keyboardType, "mKey");
                _escPressed = BindKeyWasPressed(keyboardType, "escapeKey");
                _enterPressed = BindKeyWasPressed(keyboardType, "enterKey");
                _numpadEnterPressed = BindKeyWasPressed(keyboardType, "numpadEnterKey");
                _iPressed = BindKeyWasPressed(keyboardType, "iKey");
                _jPressed = BindKeyWasPressed(keyboardType, "jKey");
            }

            private static Func<object, bool> BindKeyWasPressed(Type keyboardType, string keyMemberName)
            {
                const System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

                System.Reflection.PropertyInfo keyProp = keyboardType.GetProperty(keyMemberName, flags);
                System.Reflection.FieldInfo keyField = keyProp == null ? keyboardType.GetField(keyMemberName, flags) : null;
                Type controlType = keyProp != null ? keyProp.PropertyType : (keyField != null ? keyField.FieldType : null);
                if (controlType == null) return null;

                System.Reflection.PropertyInfo pressedProp = controlType.GetProperty("wasPressedThisFrame", flags);
                if (pressedProp == null) return null;

                try
                {
                    var kbParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "kb");
                    var typedKb = System.Linq.Expressions.Expression.Convert(kbParam, keyboardType);
                    var keyExpr = keyProp != null
                        ? System.Linq.Expressions.Expression.Property(typedKb, keyProp)
                        : System.Linq.Expressions.Expression.Field(typedKb, keyField);
                    var notNull = System.Linq.Expressions.Expression.NotEqual(
                        keyExpr, System.Linq.Expressions.Expression.Constant(null, controlType));
                    var pressedExpr = System.Linq.Expressions.Expression.Property(keyExpr, pressedProp);
                    var body = System.Linq.Expressions.Expression.Condition(
                        notNull, pressedExpr, System.Linq.Expressions.Expression.Constant(false));
                    return System.Linq.Expressions.Expression.Lambda<Func<object, bool>>(body, kbParam).Compile();
                }
                catch
                {
                    return kb =>
                    {
                        object key = keyProp != null ? keyProp.GetValue(kb, null) : keyField.GetValue(kb);
                        return key != null && (bool)pressedProp.GetValue(key, null);
                    };
                }
            }
        }

        /// <summary>
        /// Единственная точка входа для намерений UI. Проект может звать её из своего
        /// роутера ввода — тогда <see cref="readKeyboard"/> выключается.
        /// </summary>
        public void Request(UiInputAction action)
        {
            switch (action)
            {
                case UiInputAction.Phone:
                    RequestIntent(UiIntent.Phone);
                    break;
                case UiInputAction.Map:
                    RequestIntent(UiIntent.Map);
                    break;
                case UiInputAction.Back:
                    RequestIntent(UiIntent.Back);
                    break;
                case UiInputAction.Confirm:
                    if (phone != null && phone.isActiveAndEnabled) phone.Confirm();
                    else if (pause != null && pause.isActiveAndEnabled) pause.Confirm();
                    break;
                case UiInputAction.Inventory:
                    if (_states.Screen == UiScreen.Pause && pause != null) pause.TryQuickAction("inventory");
                    break;
                case UiInputAction.Tasks:
                    if (_states.Screen == UiScreen.Pause && pause != null) pause.TryQuickAction("tasks");
                    break;
            }
        }

        /// <summary>Применить намерение (используется кнопками меню паузы тоже).</summary>
        public UiTransition RequestIntent(UiIntent intent)
        {
            UiTransition transition = _states.Apply(intent);
            if (!transition.Accepted)
            {
                if (verboseLogging)
                    Debug.Log("[UiRouter] Переход отклонён: " + transition);
                return transition;
            }

            if (transition.Changed) ApplyState(transition.From, false);
            return transition;
        }

        /// <summary>Принудительно вернуть игру (кнопка «Продолжить», смена сцены).</summary>
        public void ReturnToGameplay()
        {
            _states.Reset();
            ApplyState(UiScreen.Gameplay, false);
        }

        /// <summary>
        /// Открыть телефон или карту из внешнего меню паузы (<c>City.Pause</c>) с возвратом
        /// обратно в паузу по <c>Esc</c>.
        /// </summary>
        public bool OpenFromExternalPause(UiIntent intent)
        {
            if (intent != UiIntent.OpenPhoneFromPause && intent != UiIntent.OpenMapFromPause)
                return false;

            UiScreen previous = _states.Screen;
            _states.Reset();
            _states.Apply(UiIntent.Back); // Gameplay -> Pause (контекст возврата в паузу)
            UiTransition transition = _states.Apply(intent);
            if (!transition.Accepted)
            {
                _states.Reset();
                return false;
            }

            ApplyState(previous, false);
            return true;
        }

        // ------------------------------------------------------------------ применение состояния

        /// <summary>
        /// Единственное место, где включаются/выключаются окна, курсор, HUD и ввод.
        /// Порядок важен: сначала освободить предыдущее состояние, потом включить новое.
        /// </summary>
        private void ApplyState(UiScreen previous, bool silent)
        {
            // Если паузой владеет City.Pause, переход в состояние UiScreen.Pause означает
            // «вернуться во внешнее меню паузы», а не показывать встроенный PauseMenuScreen.
            if (ExternalPauseOwner && _states.Screen == UiScreen.Pause)
            {
                _states.Reset();
                if (phone != null) phone.SetVisible(false);
                if (map != null) map.SetVisible(false);
                if (pause != null) pause.SetVisible(false);
                ExitModal();
                if (!silent && ReopenExternalPause != null) ReopenExternalPause.Invoke();
                ApplyPresentation();
                if (!silent) ScreenChanged?.Invoke(UiScreen.Gameplay);
                return;
            }

            UiScreen current = _states.Screen;

            // 1. Окна: включаем ровно одно.
            if (phone != null) phone.SetVisible(current == UiScreen.Phone || current == UiScreen.PhoneFromPause);
            if (map != null) map.SetVisible(current == UiScreen.MapFromGameplay || current == UiScreen.MapFromPause);
            if (pause != null) pause.SetVisible(!ExternalPauseOwner && current == UiScreen.Pause);

            // 2. HUD: политика из машины состояний (в паузе и на карте — полностью скрыт).
            HudVisibility hudPolicy = EffectiveHudVisibility();
            if (hud != null) hud.SetVisibility(hudPolicy);
            if (world != null) world.SetHudVisibility(hudPolicy);

            // 3. Ввод/курсор/время.
            if (_states.RequiresCursor) EnterModal();
            else ExitModal();

            // 4. Данные.
            RefreshSnapshot();
            if (hud != null && hudPolicy != HudVisibility.Hidden) hud.Render(Snapshot, Navigation);
            if (pause != null && !ExternalPauseOwner && current == UiScreen.Pause) pause.Render(Snapshot, Navigation, world);
            if (phone != null && phone.isActiveAndEnabled) phone.Refresh();
            if (map != null && map.isActiveAndEnabled) map.Refresh();

            ApplyPresentation();
            if (!silent) ScreenChanged?.Invoke(current);
            if (verboseLogging && previous != current)
                Debug.Log("[UiRouter] " + previous + " -> " + current + " (HUD: " + hudPolicy + ")");
        }

        private HudVisibility EffectiveHudVisibility()
        {
            if (ExternalPauseOwner && IsExternalPauseOpen != null && IsExternalPauseOpen())
                return HudVisibility.Hidden;
            return _states.Hud;
        }

        private void ApplyPresentation()
        {
            bool modal = _states.IsModalOpen;
            HudVisibility hudPolicy = EffectiveHudVisibility();
            if (hud != null) hud.SetVisibility(hudPolicy);
            if (world != null) world.SetHudVisibility(hudPolicy);
            if (modal) EnterModal();
            else ExitModal();
        }

        private void EnterModal()
        {
            bool openedNow = !_modalActive;
            if (openedNow)
            {
                _modalActive = true;
                _previousCursorVisible = Cursor.visible;
                _previousCursorLock = Cursor.lockState;

                // ВАЖНО: захват игрового ввода выполняется ДО поднятия ModalGuard,
                // чтобы при делегировании в StandalonePauseIntegration.AcquireGameplayLock()
                // guard ещё не считался конкурирующим окном.
                if (world != null)
                {
                    IDisposable token = world.LockGameplayInput("ui-modal");
                    if (token != null) _locks.Add(token);
                }

                ModalOpenChanged?.Invoke(true);
            }

            if ((fallbackTimeScalePause || ExternalPauseOwner) && _states.IsPausedContext && !_timeScalePaused)
            {
                _previousTimeScale = Time.timeScale;
                // Если City.Pause уже остановил время, повторная остановка безопасна.
                if (Time.timeScale > 0f)
                {
                    Time.timeScale = 0f;
                    _timeScalePaused = true;
                }
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ExitModal()
        {
            if (!_modalActive && _locks.Count == 0 && !_timeScalePaused) return;

            bool wasModal = _modalActive;
            _modalActive = false;

            ReleaseAllLocks();

            if (wasModal)
            {
                Cursor.lockState = _previousCursorLock;
                Cursor.visible = _previousCursorVisible;
            }

            if (_timeScalePaused)
            {
                Time.timeScale = _previousTimeScale;
                _timeScalePaused = false;
            }

            if (wasModal) ModalOpenChanged?.Invoke(false);
        }

        private void ReleaseAllLocks()
        {
            for (int i = _locks.Count - 1; i >= 0; i--)
            {
                try
                {
                    _locks[i].Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogError("[UiRouter] Ошибка при снятии блокировки ввода: " + exception.Message);
                }
            }
            _locks.Clear();
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // Пауза и модальные окна не переживают смену сцены: иначе игрок остаётся
            // без управления. Метки и ручной маршрут переносим явно.
            if (_states.IsModalOpen)
            {
                _states.Reset();
                ApplyState(UiScreen.Gameplay, true);
            }

            ReleaseAllLocks();
            _timeScalePaused = false;

            // В новой сцене другие адаптеры (Apartment67 ↔ District750). Ищем их сразу:
            // иначе после перехода карта и HUD работали бы с объектами старой сцены.
            RebindSceneServices();
        }

        /// <summary>
        /// Перепривязать источники данных и мир к текущей сцене. Уже привязанные и
        /// живые объекты остаются (их не пересоздаём), недостающие — ищутся заново.
        /// </summary>
        public void RebindSceneServices()
        {
            UiWorldAdapter freshWorld = world;
            if (freshWorld == null || freshWorld.gameObject == null)
                freshWorld = FindAnyObjectByType<UiWorldAdapter>();

            UiSnapshotSource freshSource = snapshotSource;
            if (freshSource == null || freshSource.gameObject == null)
                freshSource = FindAnyObjectByType<UiSnapshotSource>();

            if (freshWorld == world && freshSource == snapshotSource) return;
            Configure(freshWorld, freshSource);
        }

        // ------------------------------------------------------------------ удобные фасады

        /// <summary>Кнопка «Карта» в меню паузы и клавиша M на карте.</summary>
        public void ToggleMap()
        {
            RequestIntent(_states.Screen == UiScreen.MapFromPause || _states.Screen == UiScreen.MapFromGameplay
                ? UiIntent.Map
                : _states.Screen == UiScreen.Pause ? UiIntent.OpenMapFromPause : UiIntent.Map);
        }

        /// <summary>Кнопка «Телефон» в меню паузы и клавиша P.</summary>
        public void TogglePhone() => RequestIntent(UiIntent.Phone);

        /// <summary>Построить маршрут к активному заказу из приложения «Заказы».</summary>
        public NavigationOutcome NavigateToOrder(string orderId, MapPoint dropOff)
        {
            if (Navigation == null)
                return NavigationOutcome.Error("Навигация недоступна.", RouteFailure.NoGraph);

            Vector3 position;
            float heading;
            MapPoint from = world != null && world.TryGetPlayerPose(out position, out heading)
                ? new MapPoint(position.x, position.z)
                : new MapPoint(0f, 0f);

            Waypoint waypoint = Navigation.Waypoints.SetOrder(orderId, dropOff, "Заказ " + orderId);
            return Navigation.NavigateTo(waypoint, from);
        }
    }
}
