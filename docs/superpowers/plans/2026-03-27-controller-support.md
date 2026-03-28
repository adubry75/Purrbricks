# Controller Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add full gamepad controller support to Purrbricks using Unity's New Input System, with passive auto-detection that silently switches all input hint text between mouse/keyboard and gamepad labels.

**Architecture:** A `PurrbricksInputActions.inputactions` asset declares all action bindings; a generated C# wrapper is owned by a new `InputManager` singleton. `InputHintService` is a static lookup for scheme-aware hint strings. Nine existing scripts are migrated from `Input.GetKey`/`Mouse.current` to action callbacks, with appropriate guards preserved.

**Tech Stack:** Unity 6 (6000.3.8f1), Unity Input System 1.18.0 (already installed, `com.unity.inputsystem`), C#

**Spec:** `docs/superpowers/specs/2026-03-27-controller-support-design.md`

---

## File Map

| File | Status | Responsibility |
|---|---|---|
| `Assets/_Project/Input/PurrbricksInputActions.inputactions` | **New** | All action bindings (Gameplay + UI maps) |
| `Assets/_Project/Scripts/InputManager.cs` | **New** | Singleton: owns actions, device auto-detection, Fury Strike composite, Gameplay map enable/disable |
| `Assets/_Project/Scripts/InputHintService.cs` | **New** | Static lookup of scheme-aware hint strings per `HintKey` |
| `Assets/_Project/Scripts/Editor/PurrbricksSetup.cs` | **Modify** | Auto-create InputManager GO |
| `Assets/_Project/Scripts/GameManager.cs` | **Modify** | Replace Fury Strike check, Escape→Pause, tutorial strings, wire Gameplay map on state change |
| `Assets/_Project/Scripts/PaddleController.cs` | **Modify** | Replace `Input.mousePosition` + laser `GetMouseButtonDown` with actions |
| `Assets/_Project/Scripts/BallController.cs` | **Modify** | Replace `Mouse.current` aim loop with action-based dual path |
| `Assets/_Project/Scripts/UI/InventoryRadialMenu.cs` | **Modify** | Replace MMB open/close, Escape/RMB cancel, `Input.mousePosition` hover |
| `Assets/_Project/Scripts/UI/HavocBar.cs` | **Modify** | Replace hardcoded hint string; subscribe `OnSchemeChanged` |
| `Assets/_Project/Scripts/UI/PauseMenuUI.cs` | **Modify** | Replace Escape-resume with CancelUI subscription (keep editor-test Escape on old IM) |
| `Assets/_Project/Scripts/UI/SettingsUI.cs` | **Modify** | Replace Escape-back with CancelUI subscription |
| `Assets/_Project/Scripts/UI/LevelCodeEntryUI.cs` | **Modify** | Replace Escape/Enter with CancelUI/ConfirmUI subscriptions |

---

## Task 1: Create `InputActions` asset

**Files:**
- Create: `Assets/_Project/Input/PurrbricksInputActions.inputactions`

- [ ] **Step 1: Create the Input directory and asset file**

```bash
mkdir -p Assets/_Project/Input
```

Create `Assets/_Project/Input/PurrbricksInputActions.inputactions` with these contents:

```json
{
    "name": "PurrbricksInputActions",
    "maps": [
        {
            "name": "Gameplay",
            "id": "a9f3c1d2-4e5b-4f67-8a3c-1b2d3e4f5a6b",
            "actions": [
                {
                    "name": "MovePaddle",
                    "type": "Value",
                    "id": "b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d5e",
                    "expectedControlType": "Axis",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": true
                },
                {
                    "name": "LaunchBall",
                    "type": "Button",
                    "id": "c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                },
                {
                    "name": "FireLaser",
                    "type": "Button",
                    "id": "d3e4f5a6-b7c8-4d9e-0f1a-2b3c4d5e6f7a",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                },
                {
                    "name": "OpenRadialMenu",
                    "type": "Button",
                    "id": "e4f5a6b7-c8d9-4e0f-1a2b-3c4d5e6f7a8b",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                },
                {
                    "name": "RadialSelect",
                    "type": "Value",
                    "id": "f5a6b7c8-d9e0-4f1a-2b3c-4d5e6f7a8b9c",
                    "expectedControlType": "Vector2",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": true
                }
            ],
            "bindings": [
                { "name": "", "id": "aa000001-0000-0000-0000-000000000001", "path": "<Mouse>/position/x", "interactions": "", "processors": "", "groups": "", "action": "MovePaddle", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "aa000001-0000-0000-0000-000000000002", "path": "<Gamepad>/leftStick/x", "interactions": "", "processors": "", "groups": "", "action": "MovePaddle", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "aa000001-0000-0000-0000-000000000003", "path": "<Mouse>/leftButton", "interactions": "", "processors": "", "groups": "", "action": "LaunchBall", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "aa000001-0000-0000-0000-000000000004", "path": "<Gamepad>/buttonSouth", "interactions": "", "processors": "", "groups": "", "action": "LaunchBall", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "aa000001-0000-0000-0000-000000000005", "path": "<Mouse>/leftButton", "interactions": "", "processors": "", "groups": "", "action": "FireLaser", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "aa000001-0000-0000-0000-000000000006", "path": "<Gamepad>/buttonSouth", "interactions": "", "processors": "", "groups": "", "action": "FireLaser", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "aa000001-0000-0000-0000-000000000007", "path": "<Gamepad>/rightTrigger", "interactions": "", "processors": "", "groups": "", "action": "FireLaser", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "aa000001-0000-0000-0000-000000000008", "path": "<Mouse>/middleButton", "interactions": "", "processors": "", "groups": "", "action": "OpenRadialMenu", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "aa000001-0000-0000-0000-000000000009", "path": "<Gamepad>/leftShoulder", "interactions": "", "processors": "", "groups": "", "action": "OpenRadialMenu", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "aa000001-0000-0000-0000-00000000000a", "path": "<Gamepad>/rightShoulder", "interactions": "", "processors": "", "groups": "", "action": "OpenRadialMenu", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "aa000001-0000-0000-0000-00000000000b", "path": "<Mouse>/position", "interactions": "", "processors": "", "groups": "", "action": "RadialSelect", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "aa000001-0000-0000-0000-00000000000c", "path": "<Gamepad>/leftStick", "interactions": "", "processors": "", "groups": "", "action": "RadialSelect", "isComposite": false, "isPartOfComposite": false }
            ]
        },
        {
            "name": "UI",
            "id": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
            "actions": [
                {
                    "name": "Pause",
                    "type": "Button",
                    "id": "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                },
                {
                    "name": "CancelUI",
                    "type": "Button",
                    "id": "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                },
                {
                    "name": "ConfirmUI",
                    "type": "Button",
                    "id": "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                }
            ],
            "bindings": [
                { "name": "", "id": "bb000001-0000-0000-0000-000000000001", "path": "<Keyboard>/escape", "interactions": "", "processors": "", "groups": "", "action": "Pause", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "bb000001-0000-0000-0000-000000000002", "path": "<Gamepad>/startButton", "interactions": "", "processors": "", "groups": "", "action": "Pause", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "bb000001-0000-0000-0000-000000000003", "path": "<Keyboard>/escape", "interactions": "", "processors": "", "groups": "", "action": "CancelUI", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "bb000001-0000-0000-0000-000000000004", "path": "<Mouse>/rightButton", "interactions": "", "processors": "", "groups": "", "action": "CancelUI", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "bb000001-0000-0000-0000-000000000005", "path": "<Gamepad>/buttonEast", "interactions": "", "processors": "", "groups": "", "action": "CancelUI", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "bb000001-0000-0000-0000-000000000006", "path": "<Keyboard>/enter", "interactions": "", "processors": "", "groups": "", "action": "ConfirmUI", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "bb000001-0000-0000-0000-000000000007", "path": "<Keyboard>/numpadEnter", "interactions": "", "processors": "", "groups": "", "action": "ConfirmUI", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "bb000001-0000-0000-0000-000000000008", "path": "<Gamepad>/buttonSouth", "interactions": "", "processors": "", "groups": "", "action": "ConfirmUI", "isComposite": false, "isPartOfComposite": false }
            ]
        }
    ],
    "controlSchemes": []
}
```

**⚠️ Important:** The `MovePaddle` action has two bindings: mouse returns raw screen X pixels (e.g. 960.0) and gamepad returns -1..1. **Never call `MovePaddle.ReadValue<float>()` when `CurrentScheme == MouseKeyboard`** — the value is meaningless for game logic. Only the Gamepad branch in `PaddleController` and `BallController` reads this action value.

- [ ] **Step 2: Enable C# class generation in Unity**

In the Unity Editor:
1. Select `Assets/_Project/Input/PurrbricksInputActions.inputactions` in the Project window
2. In the Inspector, check **"Generate C# Class"**
3. Set **Class Name** to `PurrbricksInputActions`, leave **Namespace** empty
4. Click **Apply**

Unity generates `Assets/_Project/Input/PurrbricksInputActions.cs`. This file is auto-generated — do **not** edit it by hand.

- [ ] **Step 3: Verify the asset imported correctly**

In the Unity Editor, confirm:
- No errors in the Console about the .inputactions file
- `PurrbricksInputActions.cs` exists next to the asset
- The generated class compiles (no red errors)

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Input/
git commit -m "feat: add PurrbricksInputActions input asset with Gameplay and UI maps"
```

---

## Task 2: Create `InputManager.cs`

**Files:**
- Create: `Assets/_Project/Scripts/InputManager.cs`

- [ ] **Step 1: Create the file**

`Assets/_Project/Scripts/InputManager.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public enum InputScheme { MouseKeyboard, Gamepad }

/// <summary>
/// Singleton that owns PurrbricksInputActions, detects active device,
/// and exposes Fury Strike composite logic.
///
/// Auto-created by PurrbricksSetup. DO NOT add manually to the scene.
/// </summary>
public class InputManager : MonoBehaviour
{
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
```

- [ ] **Step 2: Verify it compiles**

Open Unity. Confirm no compiler errors in the Console. `InputManager`, `InputScheme`, and `PurrbricksInputActions` should all resolve.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/InputManager.cs
git commit -m "feat: add InputManager singleton with device auto-detection and Fury Strike composite"
```

---

## Task 3: Create `InputHintService.cs`

**Files:**
- Create: `Assets/_Project/Scripts/InputHintService.cs`

- [ ] **Step 1: Create the file**

`Assets/_Project/Scripts/InputHintService.cs`:

```csharp
using System.Collections.Generic;

public enum HintKey
{
    LaunchBall,
    FuryStrikeTutorial,
    FuryStrikeBar,
    Radial,
    LevelCode,
    PauseInstruction
}

/// <summary>
/// Provides scheme-aware input hint strings.
/// Call SetScheme() when InputManager.OnSchemeChanged fires.
/// Call Get() to retrieve the current hint for a given key.
/// </summary>
public static class InputHintService
{
    private static readonly Dictionary<HintKey, string> MouseKB = new()
    {
        [HintKey.LaunchBall]         = "Hold LEFT CLICK to aim \u00B7 Release to launch",
        [HintKey.FuryStrikeTutorial] = "Press LEFT + RIGHT mouse buttons together\nto unleash FURY STRIKE",
        [HintKey.FuryStrikeBar]      = "FURY STRIKE [\U0001F5B1 LMB + RMB]",
        [HintKey.Radial]             = "HOLD MMB  \u2192  HOVER A POWER-UP  \u2192  RELEASE TO USE",
        [HintKey.LevelCode]          = "ENTER to warp  \u00B7  ESC to cancel",
        [HintKey.PauseInstruction]   = "Press ESCAPE to pause at any time.",
    };

    private static readonly Dictionary<HintKey, string> GamepadHints = new()
    {
        [HintKey.LaunchBall]         = "Hold [A] to aim \u00B7 Release to launch",
        [HintKey.FuryStrikeTutorial] = "Hold LT + RT together\nto unleash FURY STRIKE",
        [HintKey.FuryStrikeBar]      = "FURY STRIKE [LT + RT]",
        [HintKey.Radial]             = "HOLD LB/RB  \u2192  STICK  \u2192  RELEASE TO USE",
        [HintKey.LevelCode]          = "ENTER to warp  \u00B7  ESC to cancel",
        [HintKey.PauseInstruction]   = "Press START to pause at any time.",
    };

    private static InputScheme _scheme = InputScheme.MouseKeyboard;

    public static void SetScheme(InputScheme scheme) => _scheme = scheme;

    public static string Get(HintKey key)
    {
        var dict = _scheme == InputScheme.Gamepad ? GamepadHints : MouseKB;
        return dict.TryGetValue(key, out var v) ? v : string.Empty;
    }
}
```

- [ ] **Step 2: Verify it compiles**

Open Unity. Confirm no errors. `HintKey` and `InputHintService` should resolve.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/InputHintService.cs
git commit -m "feat: add InputHintService static lookup for scheme-aware hint strings"
```

---

## Task 4: Wire `InputManager` into `PurrbricksSetup`

**Files:**
- Modify: `Assets/_Project/Scripts/Editor/PurrbricksSetup.cs`

- [ ] **Step 1: Add InputManager creation**

In `PurrbricksSetup.cs`, find the block that creates `HavocBar` (around line 242). Add a new block **before** it (InputManager should be created early since other singletons may depend on it at Start):

```csharp
// ── InputManager ─────────────────────────────────────────────────────────
var inputMgrGO = EnsureGO("InputManager");
if (inputMgrGO.GetComponent<InputManager>() == null)
{
    inputMgrGO.AddComponent<InputManager>();
    Debug.Log("Added InputManager.");
}
```

Place this block near the top of the singleton creation section, before PowerupHUD or HavocBar.

- [ ] **Step 2: Verify in Unity**

Run **Purrbricks > Setup Scene**. Confirm a `InputManager` GameObject appears in the scene hierarchy and no errors are logged.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Editor/PurrbricksSetup.cs
git commit -m "feat: auto-create InputManager via PurrbricksSetup"
```

---

## Task 5: Migrate `GameManager.cs`

**Files:**
- Modify: `Assets/_Project/Scripts/GameManager.cs`

This is the largest migration. Make changes in this order to keep the file compiling at each step.

- [ ] **Step 1: Replace `IsFuryStrikeMouseComboPressed()`**

Find the method (around line 345):
```csharp
private static bool IsFuryStrikeMouseComboPressed()
{
    var mouse = Mouse.current;
    ...
}
```

Delete it entirely. Then find the call site (around line 267):
```csharp
IsFuryStrikeMouseComboPressed()
```
Replace with:
```csharp
InputManager.IsFuryStrikePressed()
```

- [ ] **Step 2: Replace Escape→Pause with `Pause` action**

Find in `Update()` (around line 301):
```csharp
if ((_state == GameState.Playing || _state == GameState.Ready) && Input.GetKeyDown(KeyCode.Escape)
    && (TutorialManager.Instance == null || !TutorialManager.Instance.IsShowing))
    SetState(GameState.Paused);
```

Delete this block. Instead, subscribe to the `Pause` action in `Start`/`OnDisable`:

**Use `Start()` not `OnEnable()`** — `Start` is guaranteed to run after all `Awake` calls, ensuring `InputManager.Actions` is initialized before the subscription attempt.

Add these methods to `GameManager.cs` (or add to existing `Start`/`OnDisable` if they exist):

```csharp
private void Start()
{
    // Subscribe here (not OnEnable) — InputManager.Awake must run first
    if (InputManager.Actions != null)
        InputManager.Actions.UI.Pause.performed += OnPausePerformed;
}

private void OnDisable()
{
    if (InputManager.Actions != null)
        InputManager.Actions.UI.Pause.performed -= OnPausePerformed;
}

private void OnPausePerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
{
    if ((_state == GameState.Playing || _state == GameState.Ready)
        && (TutorialManager.Instance == null || !TutorialManager.Instance.IsShowing))
        SetState(GameState.Paused);
}
```

- [ ] **Step 3: Enable/disable Gameplay map in `SetState()`**

Find `SetState()` in `GameManager.cs`. After the switch/if block that sets `_state`, add calls to `InputManager.EnableGameplay()`:

```csharp
// At the END of SetState(), after all other state setup:
bool gameplayActive = _state == GameState.Ready
                   || _state == GameState.Playing
                   || _state == GameState.Paused;
InputManager.EnableGameplay(gameplayActive);
```

- [ ] **Step 4: Replace tutorial body strings with `InputHintService`**

Find the LaunchBall tutorial call (around line 485). Replace the hardcoded body:
```csharp
// OLD:
"Hold LEFT CLICK to aim the ball.\nRelease to launch!\n\nPress ESCAPE to pause at any time."

// NEW:
InputHintService.Get(HintKey.LaunchBall) + "\n\n" + InputHintService.Get(HintKey.PauseInstruction)
```

Find the FuryStrike tutorial call (around line 259). Replace the hardcoded body:
```csharp
// OLD:
"Your Fury Charge is at maximum!\n\nPress LEFT + RIGHT mouse buttons together\nto unleash FURY STRIKE — a devastating\nbomb blast from every ball on screen!"

// NEW:
"Your Fury Charge is at maximum!\n\n"
+ InputHintService.Get(HintKey.FuryStrikeTutorial)
+ "\n— a devastating\nbomb blast from every ball on screen!"
```

- [ ] **Step 5: Verify in Unity Play mode**

- Enter Play mode. Confirm no Console errors.
- Start a game (click past main menu). Press Escape. Confirm game pauses.
- Confirm game can be resumed via the Resume button.
- Trigger the LaunchBall tutorial (first play). Confirm text reads correctly.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/GameManager.cs
git commit -m "feat: migrate GameManager to InputManager actions (Fury Strike, Pause, tutorial hints)"
```

---

## Task 6: Migrate `PaddleController.cs`

**Files:**
- Modify: `Assets/_Project/Scripts/PaddleController.cs`

- [ ] **Step 1: Add gamepad speed field**

Add to the `[Header("Movement")]` section near the top:
```csharp
[SerializeField] private float _gamepadPaddleSpeed = 12f;
```

- [ ] **Step 2: Replace mouse position tracking in `Update()`**

Find (around line 71-75):
```csharp
else
{
    float mouseX = _camera.ScreenToWorldPoint(Input.mousePosition).x;
    targetX = _isFlipped ? -mouseX : mouseX;
}
```

Replace with:
```csharp
else if (InputManager.CurrentScheme == InputScheme.Gamepad)
{
    float stickX = InputManager.Actions?.Gameplay.MovePaddle.ReadValue<float>() ?? 0f;
    if (_isFlipped) stickX = -stickX;
    targetX = transform.position.x + stickX * _gamepadPaddleSpeed * Time.deltaTime;
}
else
{
    // MouseKeyboard: read screen X from Mouse.current, convert to world space
    var mousePos = UnityEngine.InputSystem.Mouse.current?.position.ReadValue()
                   ?? (Vector2)UnityEngine.Input.mousePosition;
    float mouseX = _camera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, _camera.nearClipPlane)).x;
    targetX = _isFlipped ? -mouseX : mouseX;
}
```

Note: The old `Input.mousePosition` fallback is kept as a safety net. The primary path uses `Mouse.current`.

- [ ] **Step 3: Replace laser `GetMouseButtonDown` with action subscription**

Remove the line in `Update()` (around line 101):
```csharp
if (_laserCooldown <= 0f && Input.GetMouseButtonDown(0))
```

Replace with:
```csharp
if (_laserCooldown <= 0f && _laserFiredThisFrame)
```

Add a private field at the top of the class:
```csharp
private bool _laserFiredThisFrame;
```

Add `OnEnable`/`OnDisable` to subscribe to the `FireLaser` action:
```csharp
private void OnEnable()
{
    if (InputManager.Actions != null)
        InputManager.Actions.Gameplay.FireLaser.performed += OnFireLaserPerformed;
}

private void OnDisable()
{
    if (InputManager.Actions != null)
        InputManager.Actions.Gameplay.FireLaser.performed -= OnFireLaserPerformed;
}

private void OnFireLaserPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
{
    _laserFiredThisFrame = true;
}
```

Add at the **end** of `Update()`, after all logic:
```csharp
_laserFiredThisFrame = false;
```

- [ ] **Step 4: Verify in Unity Play mode**

- Enter Play mode. Move mouse. Confirm paddle follows mouse.
- Connect a gamepad. Move left stick. Confirm paddle moves left/right.
- Activate Laser powerup (numpad 6). Confirm LMB fires laser. Confirm gamepad A or RT fires laser.
- Confirm FlipControls powerup still inverts correctly for both input modes.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/PaddleController.cs
git commit -m "feat: migrate PaddleController to InputManager (gamepad stick + action-based laser fire)"
```

---

## Task 7: Migrate `BallController.cs`

**Files:**
- Modify: `Assets/_Project/Scripts/BallController.cs`

This is the most complex migration. `HandleAimInput()` currently uses `Mouse.current` exclusively. We need dual paths: mouse uses delta accumulation (existing), gamepad uses stick X as direct angle.

- [ ] **Step 1: Refactor `HandleAimInput()`**

Find `HandleAimInput()` (around line 353). Replace the entire method with:

```csharp
private void HandleAimInput()
{
    bool isGamepad = InputManager.CurrentScheme == InputScheme.Gamepad;

    var launchAction = InputManager.Actions?.Gameplay.LaunchBall;
    if (launchAction == null)
    {
        if (_aimLineGO != null && _aimLineGO.activeSelf) _aimLineGO.SetActive(false);
        return;
    }

    // Begin aiming only on a fresh press (prevents menu-click carry-over)
    if (!_isAiming)
    {
        if (!launchAction.WasPerformedThisFrame())
        {
            if (_aimLineGO != null && _aimLineGO.activeSelf) _aimLineGO.SetActive(false);
            return;
        }

        if (_paddleCtrl == null && _paddle != null)
            _paddleCtrl = _paddle.GetComponent<PaddleController>();
        _paddleCtrl?.SetFrozen(true);

        _aimAngleDegrees = 0f;
        _aimDir = Vector2.up;
        _isAiming = true;
    }

    if (launchAction.IsPressed())
    {
        if (isGamepad)
        {
            // Gamepad: stick X maps directly to aim angle (-1..1 → -60°..60°)
            float stickX = InputManager.Actions?.Gameplay.MovePaddle.ReadValue<float>() ?? 0f;
            _aimAngleDegrees = stickX * 60f;
        }
        else
        {
            // Mouse: accumulate delta (existing behaviour)
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float deltaX = mouse.delta.ReadValue().x;
                float deltaDegrees = deltaX / Screen.width * 180f;
                _aimAngleDegrees = Mathf.Clamp(_aimAngleDegrees + deltaDegrees, -60f, 60f);
            }
        }

        float rad = _aimAngleDegrees * Mathf.Deg2Rad;
        _aimDir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

        if (_aimLineGO != null)
        {
            Vector2 origin = transform.position;
            _aimLineGO.SetActive(true);
            _aimLine.SetPosition(0, origin);
            _aimLine.SetPosition(1, origin + _aimDir * 2.8f);
        }
    }
    else if (launchAction.WasReleasedThisFrame())
    {
        if (_aimLineGO != null) _aimLineGO.SetActive(false);
        _launchDirection = _aimDir;
        _isAiming = false;
        Launch();

        // Only warp cursor on mouse — not needed on gamepad
        if (!isGamepad) WarpMouseToPaddleX();

        _paddleCtrl?.SetFrozen(false);
    }
    else
    {
        if (_aimLineGO != null && _aimLineGO.activeSelf) _aimLineGO.SetActive(false);
    }
}
```

- [ ] **Step 2: Replace `WasLeftMousePressedThisFrame()`**

Find (around line 425):
```csharp
private static bool WasLeftMousePressedThisFrame()
{
    return Mouse.current?.leftButton?.wasPressedThisFrame ?? false;
}
```

Replace with:
```csharp
private static bool WasLaunchPerformedThisFrame()
{
    return InputManager.Actions?.Gameplay.LaunchBall.WasPerformedThisFrame() ?? false;
}
```

Find all call sites of `WasLeftMousePressedThisFrame()` in the file and replace with `WasLaunchPerformedThisFrame()`.

- [ ] **Step 3: Verify `WarpMouseToPaddleX()` is guarded**

Find `WarpMouseToPaddleX()` (around line 430). The call is now guarded in Step 1 (`if (!isGamepad)`). Confirm no other call sites exist for this method in the file. If there are others, add the same guard.

- [ ] **Step 4: Verify in Unity Play mode**

- Enter Play mode. Hold LMB and drag. Confirm aim line appears and angle tracks correctly. Release to launch.
- Connect a gamepad. Hold A. Confirm aim line appears. Move left stick left/right. Confirm angle changes. Release A to launch.
- Confirm StickyBall powerup still works (ball sticks to paddle, hold A to release on gamepad).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/BallController.cs
git commit -m "feat: migrate BallController aim system — mouse uses delta, gamepad uses stick angle"
```

---

## Task 8: Migrate `InventoryRadialMenu.cs`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/InventoryRadialMenu.cs`

- [ ] **Step 1: Add `using` for Input System**

At the top of the file, ensure:
```csharp
using UnityEngine.InputSystem;
```

- [ ] **Step 2: Replace `Update()` input handling**

Find `Update()` (around line 131). Replace the entire input-reading block:

```csharp
private void Update()
{
    if (PurrBucksManager.Instance == null) return;

    var gm = GameManager.Instance;
    if (gm == null) return;
    if (gm.State != GameState.Playing && gm.State != GameState.Ready) return;

    if (_isOpen)
        UpdateHover();
}
```

Then add `OnEnable`/`OnDisable` subscriptions:

```csharp
private void OnEnable()
{
    if (InputManager.Actions == null) return;
    InputManager.Actions.Gameplay.OpenRadialMenu.started   += OnOpenStarted;
    InputManager.Actions.Gameplay.OpenRadialMenu.canceled  += OnOpenCanceled;
    InputManager.Actions.UI.CancelUI.performed             += OnCancelUI;
}

private void OnDisable()
{
    if (InputManager.Actions == null) return;
    InputManager.Actions.Gameplay.OpenRadialMenu.started   -= OnOpenStarted;
    InputManager.Actions.Gameplay.OpenRadialMenu.canceled  -= OnOpenCanceled;
    InputManager.Actions.UI.CancelUI.performed             -= OnCancelUI;
}

private void OnOpenStarted(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
{
    var gm = GameManager.Instance;
    if (gm == null) return;
    if (gm.State != GameState.Playing && gm.State != GameState.Ready) return;
    if (!_isOpen) OpenRadial();
}

private void OnOpenCanceled(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
{
    if (_isOpen) CloseRadial(activate: true);
}

private void OnCancelUI(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
{
    if (_isOpen) CloseRadial(activate: false);
}
```

- [ ] **Step 3: Replace `UpdateHover()` with dual-path version**

Find `UpdateHover()` (around line 430). Replace it entirely:

```csharp
private void UpdateHover()
{
    if (_slotRTs.Count == 0) return;

    if (InputManager.CurrentScheme == InputScheme.Gamepad)
        UpdateHoverGamepad();
    else
        UpdateHoverMouse();
}

private void UpdateHoverMouse()
{
    Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    var rootRt = _radialRoot.GetComponent<RectTransform>();

    // Migrated from Input.mousePosition → Mouse.current.position
    Vector2 mousePos = Mouse.current?.position.ReadValue() ?? (Vector2)UnityEngine.Input.mousePosition;
    Vector2 mouseCanvas = (mousePos - screenCenter) / _canvas.scaleFactor - rootRt.anchoredPosition;

    int newHovered = -1;
    for (int i = 0; i < _slotRTs.Count; i++)
    {
        float radius = (_allSlots[i].isInner ? INNER_SLOT_SIZE : OUTER_SLOT_SIZE) * 0.5f;
        Vector2 delta = mouseCanvas - _slotRTs[i].anchoredPosition;
        if (delta.sqrMagnitude <= radius * radius)
        {
            newHovered = i;
            break;
        }
    }

    if (newHovered != _hoveredIndex)
    {
        _hoveredIndex = newHovered;
        RefreshHighlights();
    }
}

private void UpdateHoverGamepad()
{
    Vector2 stickDir = InputManager.Actions?.Gameplay.RadialSelect.ReadValue<Vector2>() ?? Vector2.zero;

    // Require minimum stick deflection (deadzone)
    if (stickDir.magnitude < 0.3f)
    {
        if (_hoveredIndex != -1) { _hoveredIndex = -1; RefreshHighlights(); }
        return;
    }

    // Find slot whose angular position is closest to stick direction
    float stickAngleDeg = Mathf.Atan2(stickDir.y, stickDir.x) * Mathf.Rad2Deg;
    int newHovered = -1;
    float bestDiff = float.MaxValue;

    for (int i = 0; i < _slotRTs.Count; i++)
    {
        Vector2 slotPos = _slotRTs[i].anchoredPosition;
        float slotAngleDeg = Mathf.Atan2(slotPos.y, slotPos.x) * Mathf.Rad2Deg;
        float diff = Mathf.Abs(Mathf.DeltaAngle(stickAngleDeg, slotAngleDeg));
        if (diff < bestDiff) { bestDiff = diff; newHovered = i; }
    }

    if (newHovered != _hoveredIndex)
    {
        _hoveredIndex = newHovered;
        RefreshHighlights();
    }
}
```

- [ ] **Step 4: Update first-use hint text and subscribe to scheme changes**

Find where the first-use hint text is set (around line 547 based on the memory notes). The hint string `"HOLD MMB  →  HOVER A POWER-UP  →  RELEASE TO USE"` needs to be replaced and refreshed on scheme change.

Find the field that stores the hint text component (look for the hint label near line 547). Add:

```csharp
// Add as a field near other private fields:
private UnityEngine.UI.Text _hintLabel; // set when hint GO is created
```

When building the hint label UI (find the code that sets the hint text), replace the hardcoded string:
```csharp
// OLD: _hintLabel.text = "HOLD MMB  →  HOVER A POWER-UP  →  RELEASE TO USE";
// NEW:
_hintLabel.text = InputHintService.Get(HintKey.Radial);
```

Add to `OnEnable`:
```csharp
InputManager.OnSchemeChanged += RefreshHints;
```

Add to `OnDisable`:
```csharp
InputManager.OnSchemeChanged -= RefreshHints;
```

Add the method:
```csharp
private void RefreshHints(InputScheme _)
{
    if (_hintLabel != null)
        _hintLabel.text = InputHintService.Get(HintKey.Radial);
}
```

- [ ] **Step 5: Verify in Play mode**

- Enter Play mode. Acquire inventory items (numpad cheats or buy from store). Press MMB. Confirm radial opens. Hover slots with mouse, release MMB to activate. Press MMB, press RMB to cancel.
- With gamepad: press LB or RB to open. Move left stick to select slot. Release LB/RB to activate. Press LB/RB, press B to cancel.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/InventoryRadialMenu.cs
git commit -m "feat: migrate InventoryRadialMenu to actions — dual-path hover (mouse proximity / gamepad angle)"
```

---

## Task 9: Migrate `HavocBar.cs`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/HavocBar.cs`

- [ ] **Step 1: Replace hardcoded hint string in `BuildUI()`**

Find (around line 97):
```csharp
_readyLabel.text = "FURY STRIKE [\U0001F5B1 LMB + RMB]";
```

Replace with:
```csharp
_readyLabel.text = InputHintService.Get(HintKey.FuryStrikeBar);
```

- [ ] **Step 2: Subscribe to `OnSchemeChanged`**

Add `OnEnable` and `OnDisable`:

```csharp
private void OnEnable()
{
    InputManager.OnSchemeChanged += RefreshHints;
}

private void OnDisable()
{
    InputManager.OnSchemeChanged -= RefreshHints;
}

private void RefreshHints(InputScheme _)
{
    if (_readyLabel != null)
        _readyLabel.text = InputHintService.Get(HintKey.FuryStrikeBar);
}
```

- [ ] **Step 3: Verify in Play mode**

- Enter Play mode. Let the Fury bar fill. Confirm label reads "FURY STRIKE [🖱 LMB + RMB]".
- Connect a gamepad and move the stick. Confirm label instantly switches to "FURY STRIKE [LT + RT]".
- Switch back to mouse. Confirm label switches back.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/UI/HavocBar.cs
git commit -m "feat: migrate HavocBar — dynamic hint text switches with input scheme"
```

---

## Task 10: Migrate `PauseMenuUI.cs`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/PauseMenuUI.cs`

The current `Update()` has two Escape branches: one for editor-test-mode (keep as-is) and one for resume-game (migrate).

- [ ] **Step 1: Replace the resume-game Escape branch**

Find `Update()` (around line 123):
```csharp
private void Update()
{
    if (!gameObject.activeSelf) return;
    if (Input.GetKeyDown(KeyCode.Escape))
    {
        if (GameManager.Instance != null && GameManager.Instance.IsEditorTestMode)
            GameManager.Instance.ReturnToEditorFromTest();
        else
            GameManager.Instance?.ResumeGame();
    }
}
```

Replace with:
```csharp
private void Update()
{
    // Editor test-mode only: keep on old Input Manager (dev path, not gameplay)
    if (!gameObject.activeSelf) return;
    if (GameManager.Instance != null && GameManager.Instance.IsEditorTestMode
        && Input.GetKeyDown(KeyCode.Escape))
    {
        GameManager.Instance.ReturnToEditorFromTest();
    }
}

private void OnEnable()
{
    if (InputManager.Actions != null)
        InputManager.Actions.UI.CancelUI.performed += OnCancelUIPerformed;
}

private void OnDisable()
{
    if (InputManager.Actions != null)
        InputManager.Actions.UI.CancelUI.performed -= OnCancelUIPerformed;
}

private void OnCancelUIPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
{
    if (!gameObject.activeSelf) return;
    if (GameManager.Instance != null && GameManager.Instance.IsEditorTestMode) return;
    GameManager.Instance?.ResumeGame();
}
```

- [ ] **Step 2: Verify in Play mode**

- Start a game. Press Escape. Confirm pause menu shows. Press Escape again (or gamepad B). Confirm game resumes.
- Confirm the Resume button still works.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/PauseMenuUI.cs
git commit -m "feat: migrate PauseMenuUI Escape-to-resume to CancelUI action"
```

---

## Task 11: Migrate `SettingsUI.cs`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/SettingsUI.cs`

- [ ] **Step 1: Replace Escape→back**

Find `Update()` (around line 223):
```csharp
private void Update()
{
    if (!gameObject.activeSelf) return;
    if (Input.GetKeyDown(KeyCode.Escape)) OnBack();
}
```

Replace with:
```csharp
private void Update() { } // Input handled via action subscription

private void OnEnable()
{
    if (InputManager.Actions != null)
        InputManager.Actions.UI.CancelUI.performed += OnCancelUIPerformed;
}

private void OnDisable()
{
    if (InputManager.Actions != null)
        InputManager.Actions.UI.CancelUI.performed -= OnCancelUIPerformed;
}

private void OnCancelUIPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
{
    if (!gameObject.activeSelf) return;
    OnBack();
}
```

**Note:** If `SettingsUI` already has `OnEnable`/`OnDisable` methods, add to them rather than creating new ones.

- [ ] **Step 2: Verify in Play mode**

- Open Settings (from pause menu or main menu). Press Escape. Confirm settings close. Press gamepad B. Confirm same.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/SettingsUI.cs
git commit -m "feat: migrate SettingsUI Escape-to-back to CancelUI action"
```

---

## Task 12: Migrate `LevelCodeEntryUI.cs`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/LevelCodeEntryUI.cs`

- [ ] **Step 1: Replace Escape/Enter in `Update()`**

Find `Update()` (around line 145):
```csharp
private void Update()
{
    if (!_visible) return;

    if (Input.GetKeyDown(KeyCode.Escape))
    {
        Hide();
        GameManager.Instance?.ResumeAfterCodeEntry();
        return;
    }

    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        TrySubmit();
}
```

Replace with:
```csharp
private void Update() { } // Input handled via action subscriptions

private void OnEnable()
{
    if (InputManager.Actions != null)
    {
        InputManager.Actions.UI.CancelUI.performed  += OnCancelUIPerformed;
        InputManager.Actions.UI.ConfirmUI.performed += OnConfirmUIPerformed;
    }
}

private void OnDisable()
{
    if (InputManager.Actions != null)
    {
        InputManager.Actions.UI.CancelUI.performed  -= OnCancelUIPerformed;
        InputManager.Actions.UI.ConfirmUI.performed -= OnConfirmUIPerformed;
    }
}

private void OnCancelUIPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
{
    if (!_visible) return;
    Hide();
    GameManager.Instance?.ResumeAfterCodeEntry();
}

private void OnConfirmUIPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
{
    if (!_visible) return;
    TrySubmit();
}
```

- [ ] **Step 2: Update the hint label text**

Find where the hint text "ENTER to warp  ·  ESC to cancel" is set (around line 103). Replace the hardcoded string with:
```csharp
InputHintService.Get(HintKey.LevelCode)
```

(The LevelCode hint is identical for both schemes, so this is a no-op functionally but keeps it consistent.)

- [ ] **Step 3: Verify in Play mode**

- Enter a game. Press G to open level code dialog. Type a valid code and press Enter (confirm warp). Open again, press Escape (confirm cancel). Gamepad: confirm B cancels, A confirms.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/UI/LevelCodeEntryUI.cs
git commit -m "feat: migrate LevelCodeEntryUI Escape/Enter to CancelUI/ConfirmUI actions"
```

---

## Final Verification

- [ ] Connect a gamepad. Start a fresh game from the main menu.
- [ ] Confirm paddle responds to left stick.
- [ ] Hold A to aim. Move stick to change angle. Release A to launch.
- [ ] Confirm Fury bar fills and label shows "FURY STRIKE [LT + RT]".
- [ ] Hold LT + RT when Fury is full. Confirm Fury Strike triggers.
- [ ] Press LB or RB to open radial menu (requires inventory items). Move stick to select. Release to activate.
- [ ] Press LB or RB, then B to cancel without activating.
- [ ] Press Start to pause. Press B or Start to resume.
- [ ] Open Settings, press B to close.
- [ ] Confirm switching to mouse mid-session switches all hint labels back to mouse/keyboard text.
- [ ] Commit any final fixes.

```bash
git add -A
git commit -m "feat: controller support — full gamepad input via Unity New Input System"
```
