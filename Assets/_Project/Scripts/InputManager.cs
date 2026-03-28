using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public enum InputScheme { MouseKeyboard, Gamepad }

/// <summary>
/// Singleton that owns PurrbricksInputActions, detects active device,
/// and exposes Fury Strike composite logic.
///
/// Auto-created at runtime — no scene setup required.
/// </summary>
public class InputManager : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("InputManager");
        go.AddComponent<InputManager>(); // Awake() fires here, setting Instance + DontDestroyOnLoad
    }

    public static InputManager Instance { get; private set; }

    /// <summary>The currently active input scheme (updated by device auto-detection).</summary>
    public static InputScheme CurrentScheme { get; private set; } = InputScheme.MouseKeyboard;

    /// <summary>Fires whenever the active scheme changes.</summary>
    public static event Action<InputScheme> OnSchemeChanged;

    /// <summary>The generated typed input actions wrapper.</summary>
    public static PurrbricksInputActions Actions { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Actions = new PurrbricksInputActions();
        Actions.UI.Enable();
        // Gameplay map is enabled/disabled by GameManager via EnableGameplay()

        InputSystem.onEvent += OnRawInputEvent;
    }

    private void OnDestroy()
    {
        if (Instance != this) return; // Duplicate being destroyed — don't touch shared static state
        InputSystem.onEvent -= OnRawInputEvent;
        Actions?.Dispose();
        Actions = null;
    }

    // ── Device auto-detection ─────────────────────────────────────────────────

    private void OnRawInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

        var newScheme = (device is Gamepad) ? InputScheme.Gamepad : InputScheme.MouseKeyboard;
        if (newScheme == CurrentScheme) return;

        CurrentScheme = newScheme;
        InputHintService.SetScheme(newScheme);
        OnSchemeChanged?.Invoke(newScheme);
    }

    // ── Gameplay map lifecycle ────────────────────────────────────────────────

    /// <summary>
    /// Enable or disable the Gameplay action map.
    /// Call from GameManager.SetState() — enable for Ready/Playing/Paused,
    /// disable for MainMenu/Cleared/Victory/GameOver.
    /// </summary>
    public static void EnableGameplay(bool enable)
    {
        if (Actions == null) return;
        if (enable) Actions.Gameplay.Enable();
        else        Actions.Gameplay.Disable();
    }

    // ── Fury Strike composite ─────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the Fury Strike combo is triggered this frame.
    /// Mouse: LMB + RMB both held, one pressed this frame.
    /// Gamepad: LT + RT both held (> 0.5), one just crossed the threshold.
    /// Replaces GameManager.IsFuryStrikeMouseComboPressed().
    /// </summary>
    public static bool IsFuryStrikePressed()
    {
        if (CurrentScheme == InputScheme.Gamepad)
        {
            var gp = Gamepad.current;
            if (gp == null) return false;

            bool ltHeld = gp.leftTrigger.ReadValue() > 0.5f;
            bool rtHeld = gp.rightTrigger.ReadValue() > 0.5f;
            if (!ltHeld || !rtHeld) return false;

            return gp.leftTrigger.wasPressedThisFrame || gp.rightTrigger.wasPressedThisFrame;
        }
        else
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;

            if (mouse.leftButton.isPressed && mouse.rightButton.isPressed)
                return mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;

            return false;
        }
    }
}
