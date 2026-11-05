using System;
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
        public override bool CanOpenPause()
        {
            if (playerInputBehaviours == null || playerInputBehaviours.Length == 0) return false;
            foreach (var item in playerInputBehaviours) if (item == null) return false;
            if (competingModalRoots != null)
                foreach (var modal in competingModalRoots) if (modal != null && modal.activeInHierarchy) return false;
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
                items = (Behaviour[])source.Clone(); enabled = new bool[items.Length];
                for (int i = 0; i < items.Length; i++) { enabled[i] = items[i].enabled; items[i].enabled = false; }
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
        public override bool IsAvailable(PauseAction action)
        {
            var b = Find(action);
            return b != null && b.enabled && b.callback != null && b.callback.GetPersistentEventCount() > 0;
        }
        public override void Execute(PauseAction action) { if (IsAvailable(action)) Find(action).callback.Invoke(); }
        public override string ShortcutLabel(PauseAction action) { return Find(action)?.existingShortcut; }
    }
}
