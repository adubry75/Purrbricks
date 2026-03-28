# Controller Support — Design Spec
**Date:** 2026-03-27
**Status:** Approved

---

## Overview

Add full gamepad controller support to Purrbricks using Unity's New Input System (NIS). Both mouse/keyboard and gamepad remain active simultaneously — neither is disabled. A passive auto-detection system watches the last-used device and silently switches all input hint verbiage without requiring any player action.

---

## Goals

- Full gameplay on gamepad (paddle, launch, Fury Strike, radial inventory menu, pause)
- Mouse/keyboard continues to work exactly as before
- All input hint text adapts automatically to the last-used device
- Clean, Unity-recommended input architecture replacing the current hybrid (old Input Manager + partial NIS)

## Non-Goals

- Input remapping UI
- Manual settings toggle for input scheme
- Gamepad text entry (level code dialog stays keyboard-only)
- Migrating debug/cheat hotkeys (numpad keys stay on old Input Manager)

---

## Architecture

Three layers:

### 1. `InputActions.inputactions` Asset

A single Unity Input Actions asset declaring all gameplay actions. Unity generates a typed C# wrapper class (`PurrbricksInputActions`) from this asset. Two action maps:

- **`Gameplay`** — enabled during Ready, Playing, and Paused states; disabled during MainMenu, Cleared, Victory, and GameOver states
- **`UI`** — always enabled (Pause, CancelUI, ConfirmUI)

### 2. `InputManager` Singleton

`MonoBehaviour` singleton, `DontDestroyOnLoad`, auto-created by `PurrbricksSetup`.

Responsibilities:
- Instantiates and owns `PurrbricksInputActions`
- Enables/disables action maps based on game state (subscribes to `GameManager` state change events)
- Implements passive auto-detection via `InputSystem.onEvent` — inspects source device on every raw input event
- Fires `static event Action<InputScheme> OnSchemeChanged` when active scheme changes
- Exposes `static InputScheme CurrentScheme` (`MouseKeyboard` or `Gamepad`)
- Implements `IsFuryStrikePressed()` — custom composite tracking LT+RT (or LMB+RMB) both-held logic, replacing `GameManager.IsFuryStrikeMouseComboPressed()`

### 3. `InputHintService` Static Class

Lookup table mapping `HintKey` enum values to scheme-specific strings. All hardcoded input hint strings in the game are replaced with `InputHintService.Get(HintKey.X)`. Components with persistent hints subscribe to `InputManager.OnSchemeChanged` and call `RefreshHints()`.

---

## Input Action Bindings

| Action | Mouse/KB Binding | Gamepad Binding | NIS Type |
|---|---|---|---|
| `MovePaddle` | Mouse X (screen position) | Left Stick X | Value (float) |
| `LaunchBall` | Left Mouse Button | South (A/Cross) | Button |
| `FireLaser` | Left Mouse Button | South (A/Cross) **or** Right Trigger | Button |
| `FuryStrike` | LMB + RMB (custom composite) | LT + RT (custom composite) | Custom (in InputManager) |
| `OpenRadialMenu` | Middle Mouse Button (hold) | Left Bumper **or** Right Bumper (hold) | Button |
| `RadialSelect` | Mouse Position (Vector2) | Left Stick X/Y (Vector2) | Value (Vector2) |
| `Pause` | Escape | Start (Menu) | Button |
| `CancelUI` | Escape / Right Mouse Button | East (B/Circle) | Button |
| `ConfirmUI` | Enter / Keypad Enter | South (A/Cross) | Button |

### Binding Notes

- **`MovePaddle` on gamepad**: stick X is read as a *delta velocity* (holding stick moves paddle continuously at configurable speed), not as an absolute world coordinate. This is standard breakout feel.
- **`FuryStrike`**: NIS has no native "both buttons held" composite. `InputManager` tracks LT and RT (or LMB and RMB) individually and fires the strike when both are pressed simultaneously, matching existing behavior.
- **`RadialSelect` on gamepad**: stick direction (Vector2) is converted to a synthetic screen-space offset from the paddle center position, feeding into `InventoryRadialMenu`'s existing angle calculation unchanged. While the radial is open, `Time.timeScale = 0f` so `MovePaddle` also reads the stick but has no effect — no suppression needed.
- **`OpenRadialMenu`**: either LB or RB triggers open; releasing either closes and activates the selection. `CancelUI` (B button or RMB) cancels without activating.
- **Level code dialog (G key)**: not triggered on gamepad. Text entry on controller is impractical; this is a dev/cheat feature.

---

## Per-Script Migration

### `PaddleController.cs`
- Remove: `Input.mousePosition` (paddle tracking), `Input.GetMouseButtonDown(0)` (laser fire)
- Add: Subscribe to `FireLaser` action performed callback
- Add: In `Update()`, read `MovePaddle` value — if `Gamepad`, accumulate stick X as velocity (`_gamepadPaddleSpeed * stickX * Time.deltaTime`); if `MouseKeyboard`, use existing screen-to-world conversion

### `BallController.cs`
- Remove: All `Mouse.current.*` calls (`leftButton`, `delta`, `WarpCursorPosition`)
- Add: Subscribe to `LaunchBall` started (begin aim) and canceled (release to launch)
- **Aim angle on mouse**: existing per-frame delta accumulation (`mouse.delta.ReadValue().x`) is preserved, guarded by `if (InputManager.CurrentScheme == MouseKeyboard)`
- **Aim angle on gamepad**: per-frame stick X value from `MovePaddle` action is read directly as the aim deflection — positive right, negative left. The existing `_aimAngleDegrees` field is driven by stick X multiplied by a max-angle constant instead of accumulated delta. No separate aim action needed; the paddle stick doubles as the aim direction while the ball is on the paddle.
- Keep: `Mouse.WarpCursorPosition()` guarded by `if (InputManager.CurrentScheme == MouseKeyboard)`

### `GameManager.cs`
- Remove: `IsFuryStrikeMouseComboPressed()` method
- Replace: Call `InputManager.IsFuryStrikePressed()` instead
- Replace: `KeyCode.Escape` pause check → subscribe to `Pause` action
- Keep: Numpad debug hotkeys unchanged (old Input Manager, dev-only)

### `PauseMenuUI.cs`
- Remove: `Input.GetKeyDown(KeyCode.Escape)` resume-game check in `Update()` — replace with `CancelUI` action subscription to call `GameManager.Instance.ResumeGame()`
- Keep: The second `KeyCode.Escape` check in `Update()` that handles editor test-mode exit — this is dev-only and intentionally left on old Input Manager

### `InventoryRadialMenu.cs`
- Remove: `Input.GetMouseButtonDown(2)`, `GetMouseButtonUp(2)`, `Input.GetMouseButtonDown(1)`, `Input.GetKeyDown(KeyCode.Escape)`
- Add: Subscribe to `OpenRadialMenu` started → open; `OpenRadialMenu` canceled → close+activate
- Add: Subscribe to `CancelUI` → close without activating (covers both RMB and B button/Escape)
- Replace hover angle in `UpdateHover()`: migrate `Input.mousePosition` → `Mouse.current.position.ReadValue()` for the mouse path; for gamepad, convert `RadialSelect` stick Vector2 to synthetic screen-space position offset from paddle center and feed into the existing angle calculation

### `LevelCodeEntryUI.cs`
- Remove: `KeyCode.Escape`, `KeyCode.Return`, `KeyCode.KeypadEnter` checks
- Add: Subscribe to `CancelUI` and `ConfirmUI` actions

### `SettingsUI.cs`
- Remove: `KeyCode.Escape` close check
- Add: Subscribe to `CancelUI` action

---

## Input Hint System

### `InputHintService` — Hint Keys and Strings

| `HintKey` | MouseKeyboard | Gamepad |
|---|---|---|
| `LaunchBall` | `"Hold LEFT CLICK to aim · Release to launch"` | `"Hold [A] to aim · Release to launch"` |
| `FuryStrikeTutorial` | `"Press LEFT + RIGHT mouse buttons together\nto unleash FURY STRIKE"` | `"Hold LT + RT together\nto unleash FURY STRIKE"` |
| `FuryStrikeBar` | `"FURY STRIKE [🖱 LMB + RMB]"` | `"FURY STRIKE [LT + RT]"` |
| `Radial` | `"HOLD MMB  →  HOVER A POWER-UP  →  RELEASE TO USE"` | `"HOLD LB/RB  →  STICK  →  RELEASE TO USE"` |
| `LevelCode` | `"ENTER to warp  ·  ESC to cancel"` | `"ENTER to warp  ·  ESC to cancel"` |
| `PauseInstruction` | `"Press ESCAPE to pause at any time."` | `"Press START to pause at any time."` |

`HintKey.LaunchBall` and `HintKey.PauseInstruction` are concatenated in `GameManager` when building the LaunchBall tutorial body:
```
body = InputHintService.Get(HintKey.LaunchBall) + "\n\n" + InputHintService.Get(HintKey.PauseInstruction)
```

`HintKey.FuryStrikeTutorial` covers only the device-specific instruction line. `GameManager` builds the full FuryStrike tutorial body as:
```
body = "Your Fury Charge is at maximum!\n\n"
     + InputHintService.Get(HintKey.FuryStrikeTutorial)
     + "\n— a devastating\nbomb blast from every ball on screen!"
```

### Components That Refresh on Scheme Change

- `HavocBar` — subscribes `OnSchemeChanged`, calls `RefreshHints()` which sets `_readyLabel.text = InputHintService.Get(HintKey.FuryStrikeBar)`
- `InventoryRadialMenu` — refreshes first-use hint text on scheme change
- `GameManager` — uses `InputHintService.Get()` when calling `TutorialManager.TriggerIfNew()` for LaunchBall and FuryStrike tutorials. Note: tutorials fire once per installation (PlayerPrefs guard), so they will only reflect the scheme active at first play. This is acceptable — re-showing tutorials on scheme switch is out of scope.

---

## `PurrbricksSetup` Changes

Add `InputManager` to the auto-created singleton list alongside existing singletons. No manual scene wiring required.

---

## Paddle Speed (Gamepad)

A `[SerializeField] float _gamepadPaddleSpeed = 12f` field on `PaddleController` controls how fast the paddle moves per unit of stick deflection per second. This is tunable in the Inspector without code changes.

---

## Out of Scope

- On-screen button prompts / glyph icons (e.g. Xbox button sprites) — hint text only
- Gamepad vibration / haptics
- Multiple simultaneous gamepad support (single player only)
- Input remapping
- Re-triggering tutorials when input scheme changes

---

## Files Changed

| File | Change Type |
|---|---|
| `Assets/_Project/Scripts/InputManager.cs` | New |
| `Assets/_Project/Scripts/InputHintService.cs` | New |
| `Assets/_Project/Input/PurrbricksInputActions.inputactions` | New |
| `Assets/_Project/Scripts/PaddleController.cs` | Modified |
| `Assets/_Project/Scripts/BallController.cs` | Modified |
| `Assets/_Project/Scripts/GameManager.cs` | Modified |
| `Assets/_Project/Scripts/UI/PauseMenuUI.cs` | Modified |
| `Assets/_Project/Scripts/UI/InventoryRadialMenu.cs` | Modified |
| `Assets/_Project/Scripts/UI/LevelCodeEntryUI.cs` | Modified |
| `Assets/_Project/Scripts/UI/SettingsUI.cs` | Modified |
| `Assets/_Project/Scripts/UI/HavocBar.cs` | Modified |
| `Assets/_Project/Scripts/PurrbricksSetup.cs` | Modified |
