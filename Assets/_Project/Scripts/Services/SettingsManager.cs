using UnityEngine;

/// <summary>
/// Persists and applies display + audio settings.
/// Settings are saved to PlayerPrefs and applied on startup and whenever Apply is called.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    // ── Preset resolutions ────────────────────────────────────────────────────

    public static readonly (int w, int h, string label)[] Resolutions =
    {
        (3840, 2160, "4K"),
        (2560, 1440, "1440p"),
        (1920, 1080, "1080p"),
        (1280,  720, "720p"),
        (1024,  768, "XGA"),
        ( 854,  480, "480p"),
    };

    public static readonly (FullScreenMode mode, string label)[] DisplayModes =
    {
        (FullScreenMode.ExclusiveFullScreen, "Fullscreen"),
        (FullScreenMode.FullScreenWindow,    "Borderless"),
        (FullScreenMode.Windowed,            "Windowed"),
    };

    // ── PlayerPrefs keys ──────────────────────────────────────────────────────

    private const string KEY_MOUSE_RELATIVE = "Set_MouseRelative";
    private const string KEY_MOUSE_SENSITIVITY = "Set_MouseSensitivity";
    private const string KEY_GAMEPAD_SENSITIVITY = "Set_GamepadSensitivity";
    private const string KEY_GAMEPAD_DEADZONE = "Set_GamepadDeadzone";
    private const string KEY_REDUCED_EFFECTS = "Set_ReducedEffects";
    private const string KEY_RES_W    = "Set_ResW";
    private const string KEY_RES_H    = "Set_ResH";
    private const string KEY_DISP     = "Set_Display";   // index into DisplayModes
    private const string KEY_MUSIC    = "Set_MusicVol";
    private const string KEY_SFX      = "Set_SfxVol";

    // ── Current values ────────────────────────────────────────────────────────

    public int  ResolutionIndex  { get; private set; } = 2;   // default 1080p
    public int  DisplayModeIndex { get; private set; } = 1;   // default Borderless (avoids alt-tab GPU issues with ExclusiveFullScreen)
    public float MusicVolume     { get; private set; } = 0.5f;
    public float SfxVolume       { get; private set; } = 0.7f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public bool MouseRelative { get; private set; }
    public float MouseSensitivity { get; private set; } = 1f;
    public float GamepadSensitivity { get; private set; } = 1f;
    public float GamepadDeadzone { get; private set; } = 0.15f;
    public bool ReducedEffects { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    private void Start()
    {
        // Apply after all Awakes complete so MusicPlayer/SfxPlayer singletons are ready.
        ApplySettings();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetResolutionIndex(int index)
    {
        ResolutionIndex = Mathf.Clamp(index, 0, Resolutions.Length - 1);
    }

    public void SetDisplayModeIndex(int index)
    {
        DisplayModeIndex = Mathf.Clamp(index, 0, DisplayModes.Length - 1);
    }

    public void SetMusicVolume(float v)  { MusicVolume = Mathf.Clamp01(v); }
    public void SetSfxVolume(float v)    { SfxVolume   = Mathf.Clamp01(v); }

    public void SetMouseRelative(bool value) => MouseRelative = value;
    public void SetMouseSensitivity(float value) => MouseSensitivity = FiniteClamp(value, 0.25f, 3f, 1f);
    public void SetGamepadSensitivity(float value) => GamepadSensitivity = FiniteClamp(value, 0.25f, 2f, 1f);
    public void SetGamepadDeadzone(float value) => GamepadDeadzone = FiniteClamp(value, 0.05f, 0.4f, 0.15f);
    public void SetReducedEffects(bool value) => ReducedEffects = value;

    private static float FiniteClamp(float value, float min, float max, float fallback)
        => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

    /// <summary>Apply pending changes to the screen and audio systems, then save.</summary>
    public void ApplySettings()
    {
        var (w, h, _) = Resolutions[ResolutionIndex];
        Screen.SetResolution(w, h, DisplayModes[DisplayModeIndex].mode);

        MusicPlayer.Instance?.SetVolume(MusicVolume);
        SfxPlayer.Instance?.SetVolume(SfxVolume);

        SaveSettings();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void SaveSettings()
    {
        PlayerPrefs.SetInt(KEY_RES_W, ResolutionIndex);
        PlayerPrefs.SetInt(KEY_DISP,  DisplayModeIndex);
        PlayerPrefs.SetFloat(KEY_MUSIC, MusicVolume);
        PlayerPrefs.SetFloat(KEY_SFX,   SfxVolume);
        PlayerPrefs.SetInt(KEY_MOUSE_RELATIVE, MouseRelative ? 1 : 0);
        PlayerPrefs.SetFloat(KEY_MOUSE_SENSITIVITY, MouseSensitivity);
        PlayerPrefs.SetFloat(KEY_GAMEPAD_SENSITIVITY, GamepadSensitivity);
        PlayerPrefs.SetFloat(KEY_GAMEPAD_DEADZONE, GamepadDeadzone);
        PlayerPrefs.SetInt(KEY_REDUCED_EFFECTS, ReducedEffects ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        // Resolution: default to closest to current screen resolution
        int defaultRes = FindClosestResolutionIndex(Screen.width, Screen.height);

        ResolutionIndex  = PlayerPrefs.GetInt(KEY_RES_W,    defaultRes);
        DisplayModeIndex = PlayerPrefs.GetInt(KEY_DISP,     1);
        MusicVolume      = PlayerPrefs.GetFloat(KEY_MUSIC,  0.5f);
        SfxVolume        = PlayerPrefs.GetFloat(KEY_SFX,    0.7f);

        SetMouseRelative(PlayerPrefs.GetInt(KEY_MOUSE_RELATIVE, 0) != 0);
        SetMouseSensitivity(PlayerPrefs.GetFloat(KEY_MOUSE_SENSITIVITY, 1f));
        SetGamepadSensitivity(PlayerPrefs.GetFloat(KEY_GAMEPAD_SENSITIVITY, 1f));
        SetGamepadDeadzone(PlayerPrefs.GetFloat(KEY_GAMEPAD_DEADZONE, 0.15f));
        SetReducedEffects(PlayerPrefs.GetInt(KEY_REDUCED_EFFECTS, 0) != 0);
        MusicVolume = FiniteClamp(MusicVolume, 0f, 1f, 0.5f);
        SfxVolume = FiniteClamp(SfxVolume, 0f, 1f, 0.7f);

        // Clamp in case the preset lists changed
        ResolutionIndex  = Mathf.Clamp(ResolutionIndex,  0, Resolutions.Length   - 1);
        DisplayModeIndex = Mathf.Clamp(DisplayModeIndex, 0, DisplayModes.Length   - 1);
    }

    private static int FindClosestResolutionIndex(int screenW, int screenH)
    {
        int best = 2; // fallback to 1080p
        long bestDist = long.MaxValue;
        for (int i = 0; i < Resolutions.Length; i++)
        {
            long dx = Resolutions[i].w - screenW;
            long dy = Resolutions[i].h - screenH;
            long dist = dx * dx + dy * dy;
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        return best;
    }
}
