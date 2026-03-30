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
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        // Resolution: default to closest to current screen resolution
        int defaultRes = FindClosestResolutionIndex(Screen.width, Screen.height);

        ResolutionIndex  = PlayerPrefs.GetInt(KEY_RES_W,    defaultRes);
        DisplayModeIndex = PlayerPrefs.GetInt(KEY_DISP,     0);
        MusicVolume      = PlayerPrefs.GetFloat(KEY_MUSIC,  0.5f);
        SfxVolume        = PlayerPrefs.GetFloat(KEY_SFX,    0.7f);

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
