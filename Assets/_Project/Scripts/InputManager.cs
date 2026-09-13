using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;

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

        if (!HasMeaningfulActivity(eventPtr, device)) return;
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
    private static bool HasMeaningfulActivity(InputEventPtr eventPtr, InputDevice device)
    {
        if (!(device is Mouse) && !(device is Keyboard) && !(device is Gamepad)) return false;
        if (device is Mouse mouse)
        {
            if (mouse.delta.ReadValueFromEvent(eventPtr, out Vector2 delta) && delta.sqrMagnitude >= 4f) return true;
            if (mouse.scroll.ReadValueFromEvent(eventPtr, out Vector2 scroll) && scroll.sqrMagnitude > 0.01f) return true;
        }
        if (device is Gamepad gamepad)
        {
            float threshold = Mathf.Max(0.2f, SettingsManager.Instance != null ? SettingsManager.Instance.GamepadDeadzone : 0.15f);
            if (StickMoved(gamepad.leftStick, eventPtr, threshold) || StickMoved(gamepad.rightStick, eventPtr, threshold)) return true;
        }
        foreach (var control in eventPtr.EnumerateChangedControls(device))
        {
            if (control.synthetic || !(control is ButtonControl button)) continue;
            if (button.ReadValueFromEvent(eventPtr, out float value) && value > 0.5f) return true;
        }
        return false;
    }

    private static bool StickMoved(StickControl stick, InputEventPtr eventPtr, float threshold)
    {
        if (!stick.ReadUnprocessedValueFromEvent(eventPtr, out Vector2 value)) return false;
        var previous = stick.ReadUnprocessedValue();
        return value.sqrMagnitude > threshold * threshold
            && (previous.sqrMagnitude <= threshold * threshold || (value - previous).sqrMagnitude > 0.0025f);
    }

    private static readonly List<RaycastResult> PointerHits = new List<RaycastResult>();

    /// <summary>Raycast now, including callbacks before EventSystem's frame update.</summary>
    public static bool IsPointerOverUI()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;
        PointerHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            { position = Mouse.current.position.ReadValue() }, PointerHits);
        return PointerHits.Count > 0;
    }

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
