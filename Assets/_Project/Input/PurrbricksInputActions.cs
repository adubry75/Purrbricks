// AUTO-GENERATED — do not edit by hand.
// This file mirrors what Unity's Input System generates from PurrbricksInputActions.inputactions.
// When opening this project in the Unity Editor, select the .inputactions asset,
// check "Generate C# Class", set Class Name to PurrbricksInputActions, and click Apply
// to regenerate this file from the asset.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public partial class @PurrbricksInputActions : IInputActionCollection2, IDisposable
{
    public InputActionAsset asset { get; }

    public @PurrbricksInputActions()
    {
        asset = InputActionAsset.FromJson(@"{
    ""name"": ""PurrbricksInputActions"",
    ""maps"": [
        {
            ""name"": ""Gameplay"",
            ""id"": ""a9f3c1d2-4e5b-4f67-8a3c-1b2d3e4f5a6b"",
            ""actions"": [
                { ""name"": ""MovePaddle"",     ""type"": ""Value"",  ""id"": ""b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d5e"", ""expectedControlType"": ""Axis"",    ""processors"": """", ""interactions"": """", ""initialStateCheck"": true  },
                { ""name"": ""LaunchBall"",      ""type"": ""Button"", ""id"": ""c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""FireLaser"",       ""type"": ""Button"", ""id"": ""d3e4f5a6-b7c8-4d9e-0f1a-2b3c4d5e6f7a"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""OpenRadialMenu"",  ""type"": ""Button"", ""id"": ""e4f5a6b7-c8d9-4e0f-1a2b-3c4d5e6f7a8b"", ""expectedControlType"": ""Button"",  ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""RadialSelect"",    ""type"": ""Value"",  ""id"": ""f5a6b7c8-d9e0-4f1a-2b3c-4d5e6f7a8b9c"", ""expectedControlType"": ""Vector2"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": true  }
            ],
            ""bindings"": [
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-000000000001"", ""path"": ""<Mouse>/position/x"",    ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""MovePaddle"",    ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-000000000002"", ""path"": ""<Gamepad>/leftStick/x"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""MovePaddle"",    ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-000000000003"", ""path"": ""<Mouse>/leftButton"",    ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""LaunchBall"",     ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-000000000004"", ""path"": ""<Gamepad>/buttonSouth"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""LaunchBall"",     ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-000000000005"", ""path"": ""<Mouse>/leftButton"",    ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""FireLaser"",      ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-000000000006"", ""path"": ""<Gamepad>/buttonSouth"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""FireLaser"",      ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-000000000007"", ""path"": ""<Gamepad>/rightTrigger"",""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""FireLaser"",      ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-000000000008"", ""path"": ""<Mouse>/middleButton"",  ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""OpenRadialMenu"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-000000000009"", ""path"": ""<Gamepad>/leftShoulder"",""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""OpenRadialMenu"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-00000000000a"", ""path"": ""<Gamepad>/rightShoulder"",""interactions"":"""", ""processors"": """", ""groups"": """", ""action"": ""OpenRadialMenu"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-00000000000b"", ""path"": ""<Mouse>/position"",      ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""RadialSelect"",   ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""aa000001-0000-0000-0000-00000000000c"", ""path"": ""<Gamepad>/leftStick"",   ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""RadialSelect"",   ""isComposite"": false, ""isPartOfComposite"": false }
            ]
        },
        {
            ""name"": ""UI"",
            ""id"": ""b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"",
            ""actions"": [
                { ""name"": ""Pause"",     ""type"": ""Button"", ""id"": ""c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""CancelUI"",  ""type"": ""Button"", ""id"": ""d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""ConfirmUI"", ""type"": ""Button"", ""id"": ""e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false }
            ],
            ""bindings"": [
                { ""name"": """", ""id"": ""bb000001-0000-0000-0000-000000000001"", ""path"": ""<Keyboard>/escape"",     ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Pause"",     ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""bb000001-0000-0000-0000-000000000002"", ""path"": ""<Gamepad>/startButton"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Pause"",     ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""bb000001-0000-0000-0000-000000000003"", ""path"": ""<Keyboard>/escape"",     ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""CancelUI"",  ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""bb000001-0000-0000-0000-000000000004"", ""path"": ""<Mouse>/rightButton"",   ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""CancelUI"",  ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""bb000001-0000-0000-0000-000000000005"", ""path"": ""<Gamepad>/buttonEast"",  ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""CancelUI"",  ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""bb000001-0000-0000-0000-000000000006"", ""path"": ""<Keyboard>/enter"",      ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""ConfirmUI"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""bb000001-0000-0000-0000-000000000007"", ""path"": ""<Keyboard>/numpadEnter"",""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""ConfirmUI"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""bb000001-0000-0000-0000-000000000008"", ""path"": ""<Gamepad>/buttonSouth"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""ConfirmUI"", ""isComposite"": false, ""isPartOfComposite"": false }
            ]
        }
    ],
    ""controlSchemes"": []
}");

        // Gameplay map
        var gameplayMap = asset.FindActionMap("Gameplay", throwIfNotFound: true);
        Gameplay = new GameplayActions(gameplayMap);

        // UI map
        var uiMap = asset.FindActionMap("UI", throwIfNotFound: true);
        UI = new UIActions(uiMap);
    }

    public void Dispose()
    {
        UnityEngine.Object.DestroyImmediate(asset);
    }

    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;
    public bool Contains(InputAction action) => asset.Contains(action);
    public IEnumerator<InputAction> GetEnumerator() => asset.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Enable()  => asset.Enable();
    public void Disable() => asset.Disable();
    public IEnumerable<InputBinding> bindings => asset.bindings;
    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false) => asset.FindAction(actionNameOrId, throwIfNotFound);
    public int FindBinding(InputBinding bindingMask, out InputAction action) => asset.FindBinding(bindingMask, out action);

    // ── Gameplay map ──────────────────────────────────────────────────────────

    public GameplayActions Gameplay { get; }

    public struct GameplayActions
    {
        private readonly InputActionMap _map;
        public GameplayActions(InputActionMap map) { _map = map; }

        public InputAction MovePaddle    => _map["MovePaddle"];
        public InputAction LaunchBall    => _map["LaunchBall"];
        public InputAction FireLaser     => _map["FireLaser"];
        public InputAction OpenRadialMenu=> _map["OpenRadialMenu"];
        public InputAction RadialSelect  => _map["RadialSelect"];

        public void Enable()  => _map.Enable();
        public void Disable() => _map.Disable();
        public InputActionMap Get() => _map;
        public static implicit operator InputActionMap(GameplayActions set) => set._map;
    }

    // ── UI map ────────────────────────────────────────────────────────────────

    public UIActions UI { get; }

    public struct UIActions
    {
        private readonly InputActionMap _map;
        public UIActions(InputActionMap map) { _map = map; }

        public InputAction Pause     => _map["Pause"];
        public InputAction CancelUI  => _map["CancelUI"];
        public InputAction ConfirmUI => _map["ConfirmUI"];

        public void Enable()  => _map.Enable();
        public void Disable() => _map.Disable();
        public InputActionMap Get() => _map;
        public static implicit operator InputActionMap(UIActions set) => set._map;
    }
}
