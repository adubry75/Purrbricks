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
    private static readonly Dictionary<HintKey, string> MouseKB = new Dictionary<HintKey, string>
    {
        [HintKey.LaunchBall]         = "Hold LEFT CLICK to aim · Release to launch",
        [HintKey.FuryStrikeTutorial] = "Press LEFT + RIGHT mouse buttons together\nto unleash FURY STRIKE",
        [HintKey.FuryStrikeBar]      = "FURY STRIKE [🖱 LMB + RMB]",
        [HintKey.Radial]             = "Hold MMB to choose · or click INVENTORY · Favorites: 1 / 2 / 3",
        [HintKey.LevelCode]          = "ENTER to warp  ·  ESC to cancel",
        [HintKey.PauseInstruction]   = "Press ESCAPE to pause at any time.",
    };

    private static readonly Dictionary<HintKey, string> GamepadHints = new Dictionary<HintKey, string>
    {
        [HintKey.LaunchBall]         = "Hold [A] to aim · Release to launch",
        [HintKey.FuryStrikeTutorial] = "Hold LT + RT together\nto unleash FURY STRIKE",
        [HintKey.FuryStrikeBar]      = "FURY STRIKE [LT + RT]",
        [HintKey.Radial]             = "Hold LB/RB to choose · release to use · Favorites: D-pad left / up / right",
        [HintKey.LevelCode]          = "ENTER to warp  ·  ESC to cancel",
        [HintKey.PauseInstruction]   = "Press [Y] to pause at any time.",
    };

    private static InputScheme _scheme = InputScheme.MouseKeyboard;

    public static void SetScheme(InputScheme scheme) => _scheme = scheme;

    public static string Get(HintKey key)
    {
        var dict = _scheme == InputScheme.Gamepad ? GamepadHints : MouseKB;
        return dict.TryGetValue(key, out var v) ? v : string.Empty;
    }
}
