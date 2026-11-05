using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LifeFromZero.City.Pause
{
    [DefaultExecutionOrder(-100)]
    public sealed class PauseController : MonoBehaviour
    {
        public static PauseController Instance { get; private set; }
        [Tooltip("Existing window/input integration; required for opening safely.")]
        [SerializeField] private PauseIntegration integration;
        [SerializeField] private PauseDataSource dataSource;
        private readonly PauseState state = new PauseState();
        private PauseView view;
        private IDisposable gameplayLock;
        private float previousTimeScale;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        private GameObject ownedEventSystem;
        private bool subscribed;
        public bool IsOpen => state.IsOpen;
        public GameObject ViewObject => view == null ? null : view.gameObject;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            view = PauseView.Create(transform, this);
            SceneManager.sceneLoaded += SceneLoaded;
            Subscribe();
        }
        private void Subscribe()
        {
            if (!subscribed && dataSource != null) { dataSource.Changed += OnDataChanged; subscribed = true; }
        }
        private void Unsubscribe()
        {
            if (subscribed && dataSource != null) dataSource.Changed -= OnDataChanged;
            subscribed = false;
        }
        private void OnDataChanged() { if (IsOpen) Refresh(); }
        // Rebind when a scene-local service changes. Close first so its lock can be released.
        public void Configure(PauseIntegration newIntegration, PauseDataSource newDataSource)
        {
            Close(); Unsubscribe(); integration = newIntegration; dataSource = newDataSource; Subscribe();
        }
        public void HandleEscape()
        {
            if (IsOpen) Close(); else Open();
        }
        public bool Open()
        {
            if (IsOpen || integration == null || !integration.CanOpenPause()) return false;
            IDisposable acquired = integration.AcquireGameplayLock();
            if (acquired == null) return false;
            gameplayLock = acquired;
            previousTimeScale = Time.timeScale;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            state.Open();
            try
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                EnsureEventSystem();
                Refresh();
                view.gameObject.SetActive(true);
                return true;
            }
            catch { Close(); throw; }
        }
        public void Close()
        {
            if (!state.Close()) return;
            if (view != null) view.gameObject.SetActive(false);
            if (ownedEventSystem != null) { ownedEventSystem.SetActive(false); Destroy(ownedEventSystem); ownedEventSystem = null; }
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            var token = gameplayLock; gameplayLock = null;
            token?.Dispose();
        }
        public void Refresh()
        {
            if (view == null) return;
            view.Render(dataSource != null ? dataSource.Read() : null, integration);
        }
        public void Invoke(PauseAction action)
        {
            if (!IsOpen || integration == null || !integration.IsAvailable(action)) return;
            // Return control to the application before switching screen or scene.
            var target = integration;
            Close();
            if (target != null) target.Execute(action);
        }
        // Call only for the physical P key. No other phone shortcuts are registered here.
        public void HandlePhoneKey() { Invoke(PauseAction.Phone); }
        public void Quit() { if (!IsOpen) return; Close(); Application.Quit(); }
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            ownedEventSystem = new GameObject("Pause EventSystem", typeof(EventSystem));
            ownedEventSystem.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            // Avoid a hard assembly dependency if the package is absent. Only executed on open.
            var module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null) ownedEventSystem.AddComponent(module);
            else Debug.LogError("Pause: Input System UI module not found; supply an EventSystem.");
#elif ENABLE_LEGACY_INPUT_MANAGER
            ownedEventSystem.AddComponent<StandaloneInputModule>();
#else
            Debug.LogError("Pause: no UI input module available; supply an EventSystem.");
#endif
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Never carry a scene-local lock, stale providers or an extra EventSystem into a scene.
            Close();
        }
        private void OnDisable() { if (Instance == this) Close(); }
        private void OnDestroy()
        {
            if (Instance != this) return;
            Close(); Unsubscribe(); SceneManager.sceneLoaded -= SceneLoaded; Instance = null;
        }
    }
}
