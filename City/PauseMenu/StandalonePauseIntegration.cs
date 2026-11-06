using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LifeFromZero.City.Pause
{
    // Optional adapter only for scenes without an existing window/input manager.
    // Configure ALL player input behaviours and competing modal roots in Inspector.
    public sealed class StandalonePauseIntegration : PauseIntegration
    {
        [Serializable] public sealed class Binding
        {
            public PauseAction action;
            public bool enabled;
            public string existingShortcut;
            public UnityEvent callback;
        }
        [SerializeField] private Behaviour[] playerInputBehaviours;
        [SerializeField] private GameObject[] competingModalRoots;
        [SerializeField] private Binding[] bindings;

        private readonly List<GameObject> _runtimeCompetingRoots = new List<GameObject>(2);
        private readonly Dictionary<PauseAction, Action> _runtimeCallbacks = new Dictionary<PauseAction, Action>();
        private readonly Dictionary<PauseAction, string> _runtimeShortcuts = new Dictionary<PauseAction, string>();

        private static bool IsPauseComponent(Behaviour item) =>
            item is PauseController || item is PauseView || item is PauseIntegration ||
            item is PauseDataSource || item.GetType().Name == "PauseKeyboardInput";

        /// <summary>
        /// Регистрирует runtime-маркер открытого модального окна (телефон/карта UI),
        /// чтобы Esc закрывал активное окно, а не открывал паузу поверх или в том же кадре.
        /// </summary>
        public void RegisterCompetingModalRoot(GameObject modalRoot)
        {
            if (modalRoot == null) return;
            if (!_runtimeCompetingRoots.Contains(modalRoot)) _runtimeCompetingRoots.Add(modalRoot);
        }

        public void UnregisterCompetingModalRoot(GameObject modalRoot)
        {
            if (modalRoot == null) return;
            _runtimeCompetingRoots.Remove(modalRoot);
        }

        /// <summary>
        /// Подключает runtime-обработчик быстрой ссылки паузы (например, Телефон/Карта из LifeFromZero.UI),
        /// не перезаписывая сериализованные UnityEvent-привязки инспектора.
        /// </summary>
        public void SetRuntimeAction(PauseAction action, Action callback, string shortcutLabel = null)
        {
            if (callback == null)
            {
                _runtimeCallbacks.Remove(action);
                _runtimeShortcuts.Remove(action);
                return;
            }
            _runtimeCallbacks[action] = callback;
            if (!string.IsNullOrEmpty(shortcutLabel)) _runtimeShortcuts[action] = shortcutLabel;
            else _runtimeShortcuts.Remove(action);
        }

        public override bool CanOpenPause()
        {
            if (playerInputBehaviours == null || playerInputBehaviours.Length == 0) return false;
            foreach (var item in playerInputBehaviours) if (item == null || IsPauseComponent(item)) return false;
            if (competingModalRoots != null)
                foreach (var modal in competingModalRoots) if (modal != null && modal.activeInHierarchy) return false;
            for (int i = _runtimeCompetingRoots.Count - 1; i >= 0; i--)
            {
                var modal = _runtimeCompetingRoots[i];
                if (modal == null) { _runtimeCompetingRoots.RemoveAt(i); continue; }
                if (modal.activeInHierarchy) return false;
            }
            return true;
        }
        public override IDisposable AcquireGameplayLock()
        {
            if (!CanOpenPause()) return null;
            return new BehaviourLock(playerInputBehaviours);
        }
        private sealed class BehaviourLock : IDisposable
        {
            private Behaviour[] items;
            private bool[] enabled;
            public BehaviourLock(Behaviour[] source)
            {
                var unique = new List<Behaviour>(source.Length);
                for (int i = 0; i < source.Length; i++)
                {
                    var item = source[i];
                    if (item != null && !IsPauseComponent(item) && !unique.Contains(item))
                        unique.Add(item);
                }
                items = unique.ToArray();
                enabled = new bool[items.Length];
                for (int i = 0; i < items.Length; i++) enabled[i] = items[i].enabled;
                for (int i = 0; i < items.Length; i++) items[i].enabled = false;
            }
            public void Dispose()
            {
                if (items == null) return;
                for (int i = 0; i < items.Length; i++) if (items[i] != null) items[i].enabled = enabled[i];
                items = null;
            }
        }
        private Binding Find(PauseAction action)
        {
            if (bindings != null) foreach (var b in bindings) if (b != null && b.action == action) return b;
            return null;
        }
        private static bool HasPersistentBinding(Binding b) =>
            b != null && b.enabled && b.callback != null && b.callback.GetPersistentEventCount() > 0;
        public override bool IsAvailable(PauseAction action)
        {
            var b = Find(action);
            if (HasPersistentBinding(b)) return true;
            return _runtimeCallbacks.ContainsKey(action);
        }
        public override void Execute(PauseAction action)
        {
            var b = Find(action);
            if (HasPersistentBinding(b)) { b.callback.Invoke(); return; }
            Action runtime;
            if (_runtimeCallbacks.TryGetValue(action, out runtime) && runtime != null) runtime.Invoke();
        }
        public override string ShortcutLabel(PauseAction action)
        {
            var b = Find(action);
            if (b != null && !string.IsNullOrEmpty(b.existingShortcut)) return b.existingShortcut;
            string shortcut;
            return _runtimeShortcuts.TryGetValue(action, out shortcut) ? shortcut : null;
        }
    }
}
