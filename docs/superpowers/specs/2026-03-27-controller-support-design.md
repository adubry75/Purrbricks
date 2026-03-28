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

- **`Gameplay`** — enabled during Ready, Playing, and Paused states
- **`UI`** — always enabled (Pause, CancelUI, ConfirmUI)

### 2. `InputManager` Singleton

`MonoBehaviour` singleton, `DontDestroyOnLoad`, auto-created by `PurrbricksSetup`.

Responsibilities:
- Instantiates and owns `PurrbricksInputActions`
- Enables/disables action maps based on game state
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
- **`RadialSelect` on gamepad**: stick direction (Vector2) is converted to a synthetic screen-space offset from the paddle center position, feeding into `InventoryRadialMenu`'s existing angle calculation unchanged.
- **`OpenRadialMenu`**: either LB or RB triggers open; releasing either closes and activates the selection.
- **Level code dialog (G key)**: not triggered on gamepad. Text entry on controller is impractical; this is a dev/cheat feature.

---

## Per-Script Migration

### `PaddleController.cs`
- Remove: `Input.mousePosition` (paddle tracking), `Input.GetMouseButtonDown(0)` (laser fire)
- Add: Subscribe to `FireLaser` action performed callback
- Add: In `Update()`, read `MovePaddle` value — if `Gamepad`, accumulate stick X as velocity; if `MouseKeyboard`, use existing screen-to-world conversion

### `BallController.cs`
- Remove: All `Mouse.current.*` calls (leftButton, delta, WarpCursorPosition)
- Add: Subscribe to `LaunchBall` started (begin aim) and canceled (release to launch)
- Keep: `Mouse.WarpCursorPosition()` guarded by `if (InputManager.CurrentScheme == MouseKeyboard)`

### `GameManager.cs`
- Remove: `IsFuryStrikeMouseComboPressed()` method
- Replace: Call `InputManager.IsFuryStrikePressed()` instead
- Replace: `KeyCode.Escape` pause check → subscribe to `Pause` action
- Keep: Numpad debug hotkeys unchanged (old Input Manager, dev-only)

### `InventoryRadialMenu.cs`
- Remove: `Input.GetMouseButtonDown(2)`, `GetMouseButtonUp(2)`, `GetMouseButtonDown(1)`
- Add: Subscribe to `OpenRadialMenu` started/canceled for open/close
- Replace: Mouse position hover angle → read `RadialSelect` Vector2; if gamepad, convert stick direction to synthetic position; if mouse, use existing screen position logic

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

### Components That Refresh on Scheme Change

- `HavocBar` — subscribes `OnSchemeChanged`, calls `RefreshHints()` to update Fury Strike bar label
- `InventoryRadialMenu` — refreshes first-use hint text
- `GameManager` — uses `InputHintService.Get()` when calling `TutorialManager.TriggerIfNew()` for LaunchBall and FuryStrike tutorials

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
| `Assets/_Project/Scripts/UI/InventoryRadialMenu.cs` | Modified |
| `Assets/_Project/Scripts/UI/LevelCodeEntryUI.cs` | Modified |
| `Assets/_Project/Scripts/UI/SettingsUI.cs` | Modified |
| `Assets/_Project/Scripts/UI/HavocBar.cs` | Modified |
| `Assets/_Project/Scripts/PurrbricksSetup.cs` | Modified |
