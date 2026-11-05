// Keep outside the Pause asmdef: the predefined assembly references the project's
// installed Input System automatically, and the conditional also supports legacy-only projects.
using LifeFromZero.City.Pause;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class PauseKeyboardInput : MonoBehaviour
{
    [Tooltip("Enable only when no existing input/window router owns Escape and P.")]
    [SerializeField] private bool useStandaloneKeyboard;
    private void Update()
    {
        if (!useStandaloneKeyboard || PauseController.Instance == null) return;
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame) PauseController.Instance.HandleEscape();
        else if (keyboard.pKey.wasPressedThisFrame) PauseController.Instance.HandlePhoneKey();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) PauseController.Instance.HandleEscape();
        else if (Input.GetKeyDown(KeyCode.P)) PauseController.Instance.HandlePhoneKey();
#endif
    }
}
