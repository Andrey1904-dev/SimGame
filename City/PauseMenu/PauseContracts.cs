using System;
using UnityEngine;

namespace LifeFromZero.City.Pause
{
    public enum PauseAction { Save, Load, Settings, Controls, MainMenu, Phone, Inventory, Map, Tasks }

    [Serializable] public sealed class PauseGoal
    {
        public string Title;
        public bool Complete;
        public string Progress;
    }

    // Null values mean unknown, not zero. Percent values are in the 0..100 range.
    // Sources storing Hunger or Stress are converted to Satiety (100 - Hunger) and Mood (100 - Stress).
    [Serializable] public sealed class PauseSnapshot
    {
        public string Day, Time, Location, LocationDetail;
        public PauseGoal[] Goals;
        public float? Health, Satiety, Hunger, Energy, Mood, Stress;
        public decimal? Money, Debt, DailyIncome;

        public float? ResolveSatiety()
        {
            if (IsFinite(Satiety)) return Mathf.Clamp(Satiety.Value, 0f, 100f);
            if (IsFinite(Hunger)) return 100f - Mathf.Clamp(Hunger.Value, 0f, 100f);
            return null;
        }

        public float? ResolveMood()
        {
            if (IsFinite(Mood)) return Mathf.Clamp(Mood.Value, 0f, 100f);
            if (IsFinite(Stress)) return 100f - Mathf.Clamp(Stress.Value, 0f, 100f);
            return null;
        }

        private static bool IsFinite(float? value) =>
            value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value);
    }

    // Implement in the existing application's integration layer. The event is optional;
    // RaiseChanged makes an open menu refresh once, without polling.
    public abstract class PauseDataSource : MonoBehaviour
    {
        public event Action Changed;
        protected void RaiseChanged() { Changed?.Invoke(); }
        public abstract PauseSnapshot Read();
    }

    // Integrate with the *existing* window/input/pause owner, rather than opening
    // alongside another modal. AcquireGameplayLock must preserve prior control state.
    // A null lock rejects opening. IsAvailable must be side-effect free.
    public abstract class PauseIntegration : MonoBehaviour
    {
        public abstract bool CanOpenPause();
        public abstract IDisposable AcquireGameplayLock();
        public abstract bool IsAvailable(PauseAction action);
        public abstract void Execute(PauseAction action);
        // Return the existing shortcut label for non-phone links, or null if none.
        public virtual string ShortcutLabel(PauseAction action) { return null; }
    }

    // Pure state transition, independent of scene/UI.
    public sealed class PauseState
    {
        public bool IsOpen { get; private set; }
        public bool Open() { if (IsOpen) return false; IsOpen = true; return true; }
        public bool Close() { if (!IsOpen) return false; IsOpen = false; return true; }
    }
}
