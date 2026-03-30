using Newtonsoft.Json.Linq;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameState
{
    MainMenu,
    HighScores,
    Ready,
    Playing,
    Cleared,
    Paused,
    GameOver,
    Victory,
    Credits
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Admin")]
    [Tooltip("When ON: numpad cheats apply powerups directly (ignoring inventory), AND native levels are editable in the Level Editor.")]
    [SerializeField] private bool _adminMode = false;

    /// <summary>Exposes admin mode to other systems (e.g. Level Editor read-only bypass).</summary>
    public bool AdminMode => _adminMode;

    /// <summary>True while the main-menu demo is running — achievements must not fire.</summary>
    public bool IsDemoMode => _isDemoMode;

    [Header("Round Settings")]
    [SerializeField] private int _startingLives = 3;

    [Header("Scoring")]
    [SerializeField] private int _score;
    [SerializeField] private int _combo;
    [SerializeField] private float _comboResetSeconds = 22.0f;

    // Populated at runtime by RefreshLevelIds() — not serialized.
    // Add a level JSON with "nativeLevel":true and it's automatically picked up.
    private string[] _levelIds;


    [Header("Refs")]
    [SerializeField] private LevelLoader _levelLoader;
    [SerializeField] private BallController _ball;
    [SerializeField] private PaddleController _paddle;
    [SerializeField] private HudController _hud;
    [SerializeField] private GameObject bottomWall;

    [Header("UI Screens")]
    private MainMenuUI       _mainMenuUI;
    private GameOverUI       _gameOverUI;
    private VictoryUI        _victoryUI;
    private CreditsUI        _creditsUI;
    private HighScoresUI     _highScoresUI;
    private PauseMenuUI      _pauseMenuUI;
    private SettingsUI       _settingsUI;
    private LevelCodeEntryUI _levelCodeEntryUI;
    private StoreUI          _storeUI;

    private const string PREF_GAME_COMPLETED = "game_completed";
    private int _currentLevelIndex = 0;
    private Coroutine _advanceRoutine;
    private bool _isAdvancingLevel;
    private float _comboTimer;
    private int _lives;
    private bool _isDemoMode;

    // ── Community level mode ─────────────────────────────────────────────────
    private bool               _isCommunityMode;
    private bool               _rampBoosted;
    private CommunityLevelMeta _currentCommunityMeta;
    private LevelData          _cachedCommunityData; // for Retry after GameOver
    private bool _primaryBallOnHold; // true while primary fell but clones still active
    private int _activeClonesCount;  // explicit count — avoids deferred-Destroy false positives
    private bool _scoreFrenzyActive;
    private float _cachedFuryCharge;
    private bool _hidGameplayForHighScores;
    private bool _cachedHudActive;
    private bool _cachedBallActive;
    private bool _cachedPaddleActive;
    private bool _cachedIsDemoMode;
    private GameState _cachedStateBeforeHighScores;
    private float _cachedTimeScaleBeforeHighScores;

    // ── Per-level score stats (reset each LoadLevel) ─────────────────────────
    private int   _levelStartScore;
    private int   _levelBestCombo;
    private int   _levelComboBonus;   // accumulated bonus above base from combos
    private float _levelStartTime;    // realtime seconds when the level was loaded

    /// <summary>Current game state — readable by other scripts (e.g. BallController).</summary>
    public GameState State => _state;

    /// <summary>Total number of levels discovered at runtime from Resources/Levels/.</summary>
    public int LevelCount => _levelIds?.Length ?? 0;

    /// <summary>
    /// Returns the stable levelGuid for the given level index, falling back to the
    /// filename slug if the JSON has no guid (old levels). Used by leaderboard UI.
    /// </summary>
    public string GetLevelGuid(int levelIndex)
    {
        if (_levelIds == null || levelIndex < 0 || levelIndex >= _levelIds.Length) return "";
        string slug = _levelIds[levelIndex];
        var asset = Resources.Load<TextAsset>("Levels/" + slug);
        if (asset == null) return slug;
        try
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<LevelData>(asset.text);
            return !string.IsNullOrEmpty(data?.levelGuid) ? data.levelGuid : slug;
        }
        catch { return slug; }
    }

    /// <summary>Lives the player had at the start of the current level (for perfect-clear detection).</summary>
    public int LivesAtLevelStart { get; private set; }

    // Pre-allocated buffer for Fury Strike overlap queries — avoids heap allocation
    private static readonly Collider2D[] s_furyBuffer = new Collider2D[128];

    [SerializeField] private GameState _state = GameState.MainMenu;

    // ── Level code entry state ───────────────────────────────────────────────
    private GameState _stateBeforeCodeEntry;
    private GameState _stateBeforePause;

    // ── Level Editor test-play session ───────────────────────────────────────
    private bool _isEditorTestMode;
    private GameState _cachedStateBeforeEditorTest;
    private bool _cachedIsDemoModeBeforeEditorTest;
    private LevelEditorUI _editorTestEditorUI;

    public bool IsEditorTestMode => _isEditorTestMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        RefreshLevelIds();
        FindOrCreateUIScreens();
    }

    /// <summary>
    /// Dynamically discovers native levels from Resources/Levels/ at startup.
    /// Any JSON file with "nativeLevel":true is included, sorted by "levelOrder".
    /// Set "active":false in a level JSON to exclude it without deleting the file.
    /// No editor Setup Scene step required — just drop JSON files and hit Play.
    /// </summary>
    private void RefreshLevelIds()
    {
        var allAssets = Resources.LoadAll<TextAsset>("Levels");
        var found = new List<(int order, string id)>(allAssets.Length);
        foreach (var asset in allAssets)
        {
            try
            {
                var j = JObject.Parse(asset.text);
                if (!(j["nativeLevel"]?.Value<bool>() ?? false)) continue;
                if (!(j["active"]?.Value<bool>() ?? true)) continue; // skip inactive levels
                int order = j["levelOrder"]?.Value<int>() ?? int.MaxValue;
                found.Add((order, asset.name));
            }
            catch { /* skip malformed JSON */ }
        }
        found.Sort((a, b) => a.order.CompareTo(b.order));
        _levelIds = new string[found.Count];
        for (int i = 0; i < found.Count; i++)
            _levelIds[i] = found[i].id;
        Debug.Log($"[GameManager] Discovered {_levelIds.Length} active native levels.");
    }

    private void FindOrCreateUIScreens()
    {
        // Include inactive objects since UI screens hide themselves in Awake()
        _mainMenuUI       = FindFirstObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        _gameOverUI       = FindFirstObjectByType<GameOverUI>(FindObjectsInactive.Include);
        _victoryUI        = FindFirstObjectByType<VictoryUI>(FindObjectsInactive.Include);
        _creditsUI        = FindFirstObjectByType<CreditsUI>(FindObjectsInactive.Include);
        _highScoresUI     = FindFirstObjectByType<HighScoresUI>(FindObjectsInactive.Include);
        _pauseMenuUI      = FindFirstObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
        _settingsUI       = FindFirstObjectByType<SettingsUI>(FindObjectsInactive.Include);
        _levelCodeEntryUI = FindFirstObjectByType<LevelCodeEntryUI>(FindObjectsInactive.Include);
        _storeUI          = FindFirstObjectByType<StoreUI>(FindObjectsInactive.Include);

        if (_mainMenuUI == null)   Debug.LogError("MainMenuUI not found! Run Purrbricks > Setup Scene.");
        if (_gameOverUI == null)   Debug.LogError("GameOverUI not found! Run Purrbricks > Setup Scene.");
        if (_victoryUI == null)    Debug.LogError("VictoryUI not found! Run Purrbricks > Setup Scene.");
        if (_highScoresUI == null) Debug.LogError("HighScoresUI not found! Run Purrbricks > Setup Scene.");
        // CommunityBrowserUI and CommunityLevelService are optional — no error if missing
        // LevelCodeEntryUI and StoreUI are optional — no error if missing

    }

    private void RefreshUIRefsIfMissing()
    {
        if (_mainMenuUI == null || _gameOverUI == null || _victoryUI == null || _highScoresUI == null)
            FindOrCreateUIScreens();

        if (_levelCodeEntryUI == null)
            _levelCodeEntryUI = FindFirstObjectByType<LevelCodeEntryUI>(FindObjectsInactive.Include);
        if (_pauseMenuUI == null)
            _pauseMenuUI = FindFirstObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
        if (_settingsUI == null)
            _settingsUI = FindFirstObjectByType<SettingsUI>(FindObjectsInactive.Include);
    }


    private void Start()
    {
        _lives = _startingLives;
        ShowMainMenu();
    }

    private void OnEnable()
    {
        // All Awake() calls complete before any OnEnable() in the same scene load,
        // so InputManager.Actions is guaranteed set when this fires.
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

    private void Update()
    {
        // Numpad cheats: activate powerups (admin = unlimited; non-admin = consumes inventory)
        if (_state == GameState.Playing && !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            if (Input.GetKeyDown(KeyCode.Keypad1))    CheatApply(PowerupType.WidePaddle);
            if (Input.GetKeyDown(KeyCode.Keypad2))    CheatApply(PowerupType.MultiBall);
            if (Input.GetKeyDown(KeyCode.Keypad3))    CheatApply(PowerupType.StickyBall);
            if (Input.GetKeyDown(KeyCode.Keypad4))    CheatApply(PowerupType.SpeedBall);
            if (Input.GetKeyDown(KeyCode.Keypad5))    CheatApply(PowerupType.ExtraLife);
            if (Input.GetKeyDown(KeyCode.Keypad6))    CheatApply(PowerupType.Laser);
            if (Input.GetKeyDown(KeyCode.Keypad7))    CheatApply(PowerupType.Fireball);
            if (Input.GetKeyDown(KeyCode.Keypad8))    CheatApply(PowerupType.BombBrick);
            if (Input.GetKeyDown(KeyCode.Keypad9))    CheatApply(PowerupType.ShieldWall);
            if (Input.GetKeyDown(KeyCode.KeypadPlus)) CheatApply(PowerupType.BigBall);
            if (Input.GetKeyDown(KeyCode.KeypadPeriod)) CheatApply(PowerupType.ScoreFrenzy);
            
            //Debug.Log($"{_ball.RampFraction} ramp");
        }

        if (_state == GameState.Playing && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            if (Input.GetKeyDown(KeyCode.Keypad1)) CheatApply(PowerupType.ShrinkPaddle);
            if (Input.GetKeyDown(KeyCode.Keypad2)) CheatApply(PowerupType.ZipBall);
            if (Input.GetKeyDown(KeyCode.Keypad3)) CheatApply(PowerupType.FlipControls);
            if (Input.GetKeyDown(KeyCode.Keypad4)) CheatApply(PowerupType.CursedBall);
            if (Input.GetKeyDown(KeyCode.Keypad5)) CheatApply(PowerupType.TinyBall);
            if (Input.GetKeyDown(KeyCode.Keypad6)) CheatApply(PowerupType.InvisiBall);
            if (Input.GetKeyDown(KeyCode.Keypad7)) CheatApply(PowerupType.DrunkenPaddle);
            if (Input.GetKeyDown(KeyCode.Keypad8)) CheatApply(PowerupType.DrunkVision);
            if (Input.GetKeyDown(KeyCode.Keypad9)) CheatApply(PowerupType.GremlinBounces);
        }

        // Fury tutorial: fire once the charge first reaches 100%
        if (_state == GameState.Playing && _ball != null && _ball.RampFraction >= 1f)
            TutorialManager.Instance?.TriggerIfNew(
                TutorialManager.ID.FuryStrike,
                "★ ★ ★",
                "FURY STRIKE READY!",
                "Your Fury Charge is at maximum!\n\n"
                + InputHintService.Get(HintKey.FuryStrikeTutorial)
                + "\n— a devastating\nbomb blast from every ball on screen!");

        // Fury Strike: both mouse buttons pressed together when charge is full
        if (_state == GameState.Playing && _ball != null && _ball.RampFraction >= 1f &&
            InputManager.IsFuryStrikePressed())
        {
            TriggerFuryStrike();
        }

        // Debug hotkey: clear all but 1 brick
        if (Input.GetKeyDown(KeyCode.K) && _state == GameState.Playing && _adminMode)
            ClearAllButOneBrick();

        // Speed up FURY for testing.
        if (Input.GetKeyDown(KeyCode.X) && _adminMode)
        {
            _rampBoosted = !_rampBoosted;
            if (_ball != null) _ball.RAMP_RATE = _rampBoosted ? 0.5f : 0.015f;
        }

        // Turn on bottom wall so no balls can be lost. for testing.
        if (Input.GetKeyDown(KeyCode.Z) && _adminMode)
        {
            if (bottomWall != null)
            {
                bottomWall.SetActive(!bottomWall.activeSelf);
            }
        }

        // Instant kill your ball. 
        if (_state == GameState.Playing && Input.GetKeyDown(KeyCode.Backslash) && _adminMode)
            DebugDropPrimaryBallIntoDeathZone();

        // Instant complete level
        if (_state == GameState.Playing && Input.GetKeyDown(KeyCode.RightBracket) && _adminMode)
            DebugClearLevelBricks();


        // Ctrl+Shift+F10: reset stars for the current level (dev QA — test star-upgrade flow)
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift)
            && Input.GetKeyDown(KeyCode.F10))
        {
            string levelId = (_levelIds != null && _currentLevelIndex >= 0 && _currentLevelIndex < _levelIds.Length)
                ? _levelIds[_currentLevelIndex] : null;
            if (!string.IsNullOrEmpty(levelId))
            {
                PlayerPrefs.DeleteKey("perf_stars_" + levelId);
                PlayerPrefs.Save();
                Debug.Log($"[Dev] Stars reset for level '{levelId}' (index {_currentLevelIndex}).");
            }
            else
            {
                Debug.LogWarning("[Dev] Ctrl+Shift+F10: no current level loaded — nothing reset.");
            }
        }

        // G key: open level code warp dialog (Ready, Playing, or Paused)
        // Intentionally kept on legacy Input Manager — text entry on gamepad is impractical,
        // so this feature is keyboard-only by design (spec: non-goal for gamepad).
        // Guard: skip if the dialog is already open so the player can type 'G' freely.
        // We pause time without entering the Paused state so the pause menu stays hidden.
        if ((_state == GameState.Ready || _state == GameState.Playing || _state == GameState.Paused)
            && Input.GetKeyDown(KeyCode.G)
            && _adminMode
            && (_levelCodeEntryUI == null || !_levelCodeEntryUI.IsVisible))
        {
            _stateBeforeCodeEntry = _state;
            Time.timeScale = 0f;
            RefreshUIRefsIfMissing();
            _levelCodeEntryUI?.Show();
        }

        // Combo timer (runs in Playing mode and demo mode)
        if (_comboTimer > 0f && (_state == GameState.Playing || _isDemoMode))
        {
            _comboTimer -= Time.unscaledDeltaTime;
            if (_comboTimer <= 0f)
                ResetCombo();
        }

        // Demo mode: auto-launch ball
        if ((_state == GameState.MainMenu || _state == GameState.HighScores) && _isDemoMode)
        {
            if (_ball != null && !_ball.IsLaunched())
            {
                // Auto-launch after 1 second
                _ball.Launch();
            }
        }

        // Transition to Playing the moment BallController launches (via aim system)
        if (_state == GameState.Ready && _ball != null && _ball.IsLaunched())
        {
            _hud?.SetStatus("");
            SetState(GameState.Playing);
        }
    }

    // ── Public API (called by UI buttons) ───────────────────────────────────

    public void AddLife()
    {
        _lives++;
        _hud?.SetLives(_lives);
    }

    public int GetLives() => _lives;

    /// <summary>Score earned on the current (or most-recently completed) level only.</summary>
    public int GetLevelScore() => Mathf.Max(0, _score - _levelStartScore);

    public bool IsPlayingOrReady() => _state == GameState.Playing || _state == GameState.Ready;

    public void ShowStore()
    {
        _pauseMenuUI?.Hide();
        if (_storeUI == null)
            _storeUI = FindFirstObjectByType<StoreUI>(FindObjectsInactive.Include);
        _storeUI?.Show();
    }

    public void HideStore()
    {
        _storeUI?.Hide();
        // Only restore time/audio if the game was NOT already paused before the store opened.
        // When coming from the Pause menu, the game stays paused and the pause menu re-appears.
        if (_state != GameState.Paused)
        {
            Time.timeScale = 1f;
        }
        if (_state == GameState.Paused)
            _pauseMenuUI?.Show();
    }

    /// <summary>
    /// Applies a powerup via numpad cheat.
    /// Admin mode: direct Apply (unlimited, ignores inventory).
    /// Non-admin mode: TryUseFromInventory (must own it, consumes one).
    /// </summary>
    private void CheatApply(PowerupType type)
    {
        if (_adminMode)
            PowerupManager.Instance?.Apply(type);
        else
            PurrBucksManager.Instance?.TryUseFromInventory(type);
    }

    public void SetScoreFrenzy(bool on)
    {
        _scoreFrenzyActive = on;
    }

    public bool IsPrimaryBall(BallController ball) => ball == _ball;

    /// <summary>Exposes the primary ball so HavocBar can read RampFraction.</summary>
    public BallController GetPrimaryBall() => _ball;

    /// <summary>Returns the highest Fury charge fraction among every launched ball (primary or clones).</summary>
    public float GetFuryChargeFraction()
    {
        float best = 0f;
        var balls = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        bool anyLaunched = false;
        foreach (var ball in balls)
        {
            if (ball == null || !ball.IsLaunched()) continue;
            anyLaunched = true;
            best = Mathf.Max(best, ball.RampFraction);
            if (Mathf.Approximately(best, 1f)) break;
        }

        if (anyLaunched)
        {
            _cachedFuryCharge = Mathf.Max(_cachedFuryCharge, best);
            return _cachedFuryCharge;
        }

        if (_cachedFuryCharge > 0f)
            _cachedFuryCharge = 0f;

        return 0f;
    }

    /// <summary>Called by BallController.SpawnClone after it creates a clone.</summary>
    public void RegisterClone() => _activeClonesCount++;

    public void StartGame()
    {
        _isEditorTestMode = false;
        _isCommunityMode  = false;
        RestoreGameplayAfterHighScores();
        _isDemoMode = false;
        _paddle?.SetDemoMode(false);

        // Clear powerups and particles from demo mode
        PowerupManager.Instance?.ResetAll();
        ClearAllParticles();

        _score = 0;
        _combo = 0;
        _comboTimer = 0f;
        _lives = _startingLives;
        _currentLevelIndex = 0;
        _scoreFrenzyActive = false;

        _hud?.SetScore(_score);
        _hud?.SetLives(_lives);
        _hud?.SetCombo(_combo);

        if (!_isEditorTestMode)
            AchievementManager.Instance?.OnGameStarted();

        LoadLevel(_currentLevelIndex);
        MusicPlayer.Instance?.PlayGameplay(0);
        SetState(GameState.Ready);

        _mainMenuUI?.Hide();
        _highScoresUI?.Hide();

        TutorialManager.Instance?.TriggerIfNew(
            TutorialManager.ID.LaunchBall,
            "● ● ●",
            "HOW TO PLAY",
            InputHintService.Get(HintKey.LaunchBall) + "\n\n" + InputHintService.Get(HintKey.PauseInstruction));
    }

    /// <summary>
    /// Called by CreditsUI once the screen has faded to full black.
    /// Tears down all game elements so only the parallax background remains
    /// when the overlay fades back to OVERLAY_ALPHA.
    /// </summary>
    public void HideAllForCredits()
    {
        // Stop demo / gameplay
        _isDemoMode = false;
        _paddle?.SetDemoMode(false);

        // Hide all UI panels
        _mainMenuUI?.Hide();
        _gameOverUI?.Hide();
        _victoryUI?.Hide();
        _pauseMenuUI?.Hide();
        _settingsUI?.Hide();
        _highScoresUI?.Hide();
        _hud?.SetVisible(false);
        PowerupHUD.Instance?.SetVisible(false);
        HavocBar.Instance?.gameObject.SetActive(false);
        PurrBucksManager.Instance?.SetVisible(false);
        TutorialManager.Instance?.gameObject.SetActive(false);

        // Clear all bricks and powerup pickups
        LevelLoader.Instance?.ClearLevel();
        PowerupManager.Instance?.ResetAll();
        ClearAllParticles();

        // Hide the ball, paddle, and playfield frame
        if (_ball != null)     _ball.gameObject.SetActive(false);
        if (_paddle != null)   _paddle.gameObject.SetActive(false);

        var frame = Object.FindFirstObjectByType<PlayfieldFrame>();
        if (frame != null) frame.gameObject.SetActive(false);

        var deathZone = Object.FindFirstObjectByType<DeathZone>();
        if (deathZone != null) deathZone.gameObject.SetActive(false);
    }

    public void ShowMainMenu()
    {
        _isEditorTestMode = false;
        RestoreGameplayAfterHighScores();

        // Restore anything that HideAllForCredits() may have disabled
        if (_ball   != null) _ball.gameObject.SetActive(true);
        if (_paddle != null) _paddle.gameObject.SetActive(true);
        var frame = Object.FindFirstObjectByType<PlayfieldFrame>(FindObjectsInactive.Include);
        if (frame != null) frame.gameObject.SetActive(true);
        var deathZone = Object.FindFirstObjectByType<DeathZone>(FindObjectsInactive.Include);
        if (deathZone != null) deathZone.gameObject.SetActive(true);
        if (HavocBar.Instance != null)       HavocBar.Instance.gameObject.SetActive(true);
        if (TutorialManager.Instance != null) TutorialManager.Instance.gameObject.SetActive(true);

        _isDemoMode = true;
        _paddle?.SetDemoMode(true);
        _gameOverUI?.Hide();
        _victoryUI?.Hide();
        _creditsUI?.Hide();
        _highScoresUI?.Hide();
        _pauseMenuUI?.Hide();
        _settingsUI?.Hide();
        _mainMenuUI?.Show();

        LoadDemoLevel();
        SetState(GameState.MainMenu);
    }

    public void ShowHighScores()
    {
        _isEditorTestMode = false;
        RestoreGameplayAfterHighScores();
        _isDemoMode = true;
        _paddle?.SetDemoMode(true);
        _mainMenuUI?.Hide();
        _highScoresUI?.Show();

        HideGameplayForHighScores();
        SetState(GameState.HighScores);
    }

    /// <summary>Opens the leaderboard screen on a specific level board (called from VictoryUI).</summary>
    public void ShowLevelLeaderboard(int levelIndex)
    {
        RestoreGameplayAfterHighScores();
        _victoryUI?.HideForLeaderboard();   // suspend without resetting community state
        _highScoresUI?.ShowForLevel(levelIndex, returnToVictory: true);

        HideGameplayForHighScores();
        SetState(GameState.HighScores);
    }

    /// <summary>Opens the leaderboard for a community level board (called from VictoryUI in community mode).</summary>
    public void ShowCommunityLevelLeaderboard(int communityId)
    {
        string boardName = PurrbricksLeaderboards.CommunityAllTime(communityId);
        RestoreGameplayAfterHighScores();
        _victoryUI?.HideForLeaderboard();
        _highScoresUI?.ShowForBoardName(boardName, $"Community #{communityId}", returnToVictory: true);
        HideGameplayForHighScores();
        SetState(GameState.HighScores);
    }

    public void ReturnToVictoryFromLevelBoard()
    {
        _highScoresUI?.Hide();
        RestoreGameplayAfterHighScores();
        _victoryUI?.Show();

        // Restore state/timeScale without re-running SetState(Victory) side effects
        // (Victory state triggers OnLevelCompleted, music, etc.).
        _state = _cachedStateBeforeHighScores;
        Time.timeScale = _cachedTimeScaleBeforeHighScores;
        SetCursorMenuMode();
    }

    /// <summary>Called by GameOverUI to show leaderboard — no music change.</summary>
    public void ShowHighScoresAfterGameOver()
    {
        RestoreGameplayAfterHighScores();
        _gameOverUI?.Hide();
        _highScoresUI?.Show();
        // Intentionally no music change — game over track keeps playing
        HideGameplayForHighScores();
        SetState(GameState.HighScores);
    }

    private void HideGameplayForHighScores()
    {
        if (_hidGameplayForHighScores) return;
        _hidGameplayForHighScores = true;

        _cachedStateBeforeHighScores = _state;
        _cachedTimeScaleBeforeHighScores = Time.timeScale;
        _cachedIsDemoMode = _isDemoMode;
        _cachedHudActive = _hud != null && _hud.IsVisible;
        _cachedBallActive = _ball != null && _ball.gameObject.activeSelf;
        _cachedPaddleActive = _paddle != null && _paddle.gameObject.activeSelf;

        _levelLoader?.ClearLevel();

        _hud?.SetVisible(false);
        if (_ball != null) _ball.gameObject.SetActive(false);
        if (_paddle != null) _paddle.gameObject.SetActive(false);
    }

    private void RestoreGameplayAfterHighScores()
    {
        if (!_hidGameplayForHighScores) return;
        _hidGameplayForHighScores = false;

        _isDemoMode = _cachedIsDemoMode;
        _paddle?.SetDemoMode(_isDemoMode);

        _hud?.SetVisible(_cachedHudActive);
        if (_ball != null) _ball.gameObject.SetActive(_cachedBallActive);
        if (_paddle != null) _paddle.gameObject.SetActive(_cachedPaddleActive);
    }

    /// <summary>Resumes gameplay from the pause menu.</summary>
    public void ResumeGame()
    {
        _pauseMenuUI?.Hide();
        _settingsUI?.Hide();
        SetState(_stateBeforePause == GameState.Ready ? GameState.Ready : GameState.Playing);
    }

    /// <summary>
    /// Called by LevelCodeEntryUI when the dialog is dismissed without warping (ESC).
    /// Restores time if we were the ones who paused it (i.e. the game was not already Paused).
    /// </summary>
    public void ResumeAfterCodeEntry()
    {
        if (_stateBeforeCodeEntry != GameState.Paused)
            Time.timeScale = 1f;
    }

    /// <summary>Starts a temporary play session from the in-game level editor.</summary>
    public void StartEditorTestLevel(LevelData editorLevelData, string levelId, LevelEditorUI editorUI)
    {
        if (editorLevelData == null)
        {
            Debug.LogError("GameManager: StartEditorTestLevel called with null level data.");
            return;
        }

        if (editorUI == null)
        {
            Debug.LogError("GameManager: StartEditorTestLevel called with null editor UI.");
            return;
        }

        // Cache and enter test mode
        _cachedStateBeforeEditorTest = _state;
        _cachedIsDemoModeBeforeEditorTest = _isDemoMode;
        _editorTestEditorUI = editorUI;
        _isEditorTestMode = true;

        // Stop any pending level-advance routine from previous gameplay
        if (_advanceRoutine != null) StopCoroutine(_advanceRoutine);
        _advanceRoutine = null;
        _isAdvancingLevel = false;

        // Hide editor/browser UI
        _editorTestEditorUI.Hide();
        var browser = Object.FindFirstObjectByType<LevelEditorBrowserUI>(FindObjectsInactive.Include);
        browser?.Hide();

        // Reset a clean test run (no progression, no store/purr bucks)
        RestoreGameplayAfterHighScores();
        _isDemoMode = false;
        _paddle?.SetDemoMode(false);

        PowerupManager.Instance?.ResetAll();
        ClearAllParticles();

        _score = 0;
        _combo = 0;
        _comboTimer = 0f;
        _lives = _startingLives;
        _scoreFrenzyActive = false;
        _cachedFuryCharge = 0f;

        _hud?.SetScore(_score);
        _hud?.SetLives(_lives);
        _hud?.SetCombo(_combo);

        _mainMenuUI?.Hide();
        _highScoresUI?.Hide();
        _gameOverUI?.Hide();
        _victoryUI?.Hide();
        _pauseMenuUI?.Hide();
        _settingsUI?.Hide();
        _storeUI?.Hide();

        if (_levelLoader == null)
            _levelLoader = FindFirstObjectByType<LevelLoader>();

        if (_levelLoader == null)
        {
            Debug.LogError("GameManager: No LevelLoader found for editor test play.");
            ReturnToEditorFromTest();
            return;
        }

        _levelLoader.LoadLevelData(editorLevelData);

        // Reset balls/paddle like normal gameplay
        _primaryBallOnHold = false;
        _activeClonesCount = 0;
        if (_ball != null) _ball.gameObject.SetActive(true);
        var allBalls = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        foreach (var b in allBalls)
            if (b != _ball) Destroy(b.gameObject);

        _ball?.ResetToPaddle();
        _paddle?.ResetPosition();

        // HUD: show "LEVEL TEST" instead of a numbered level
        string levelTitle = _levelLoader?.CurrentLevel?.displayName ?? "";
        _hud?.SetLevelInfo(0, string.IsNullOrEmpty(levelTitle) ? (levelId ?? "TEST") : levelTitle);
        _hud?.SetLevelCode("");

        AudioListener.pause = false;

        MusicPlayer.Instance?.PlayGameplay(0);
        SetState(GameState.Ready);
    }

    /// <summary>Returns from editor test-play back to the LevelEditorUI (no victory/gameover screens).</summary>
    public void ReturnToEditorFromTest()
    {
        if (!_isEditorTestMode) return;

        _isEditorTestMode = false;

        if (_advanceRoutine != null) StopCoroutine(_advanceRoutine);
        _advanceRoutine = null;
        _isAdvancingLevel = false;

        _pauseMenuUI?.Hide();
        _settingsUI?.Hide();
        _storeUI?.Hide();
        _gameOverUI?.Hide();
        _victoryUI?.Hide();

        Time.timeScale = 1f;
        AudioListener.pause = true;

        // Clean up gameplay objects created during test play
        _levelLoader?.ClearLevel();
        PowerupManager.Instance?.ResetAll();

        // Restore menu/demo mode background as it was before test (editor sits on top)
        _isDemoMode = _cachedIsDemoModeBeforeEditorTest;
        _paddle?.SetDemoMode(_isDemoMode);
        if (_isDemoMode)
            LoadDemoLevel();
        SetState(_cachedStateBeforeEditorTest);

        _editorTestEditorUI?.Show();
        _editorTestEditorUI = null;
    }

    /// <summary>Opens the pause menu without changing game state (called internally by Paused state).</summary>
    public void ShowPauseMenu()
    {
        _settingsUI?.Hide();
        _pauseMenuUI?.Show();
    }

    /// <summary>Opens the Settings screen. Pass fromPause=true when called from the pause menu.</summary>
    public void ShowSettings(bool fromPause)
    {
        if (fromPause)
            _pauseMenuUI?.Hide();
        else
            _mainMenuUI?.Hide();

        _settingsUI?.Show(fromPause);
    }

    /// <summary>Quits to desktop.</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Warps directly to a level by index (called from LevelCodeEntryUI).</summary>
    public void WarpToLevel(int levelIndex)
    {
        if (_levelIds == null || levelIndex < 0 || levelIndex >= _levelIds.Length) return;

        _isDemoMode = false;
        _paddle?.SetDemoMode(false);
        _levelCodeEntryUI?.Hide();
        _pauseMenuUI?.Hide();
        _gameOverUI?.Hide();
        _victoryUI?.Hide();

        // Reset per-level state but keep lives and cumulative score intact
        _combo      = 0;
        _comboTimer = 0f;
        _scoreFrenzyActive = false;
        _hud?.SetCombo(_combo);

        PowerupManager.Instance?.ResetAll();

        LoadLevel(levelIndex);
        MusicPlayer.Instance?.PlayGameplay(levelIndex);
        SetState(GameState.Ready);
    }

    public void RestartGame()
    {
        _score = 0;
        _combo = 0;
        _comboTimer = 0f;
        _lives = _startingLives;
        _currentLevelIndex = 0;
        _scoreFrenzyActive = false;

        _hud?.SetScore(_score);
        _hud?.SetLives(_lives);
        _hud?.SetCombo(_combo);

        _gameOverUI?.Hide();

        LoadLevel(_currentLevelIndex);
        MusicPlayer.Instance?.PlayGameplay(0);
        SetState(GameState.Ready);
    }

    public void LoadNextLevel()
    {
        _victoryUI?.Hide();
        int next = _currentLevelIndex + 1;

        if (next >= _levelIds.Length)
        {
            // All levels complete — show the end credits screen
            SetState(GameState.Credits);
            return;
        }

        LoadLevel(next);
        MusicPlayer.Instance?.PlayGameplay(_currentLevelIndex);
        SetState(GameState.Ready);
    }

    /// <summary>
    /// Replay the current level from scratch (keeps lives and cumulative score from earlier levels).
    /// Any score earned in this attempt is rolled back so the level starts fresh.
    /// </summary>
    public void ReplayCurrentLevel()
    {
        _victoryUI?.Hide();

        // Roll back score to what it was before this level started
        _score = _levelStartScore;
        _combo = 0;
        _comboTimer = 0f;
        _hud?.SetScore(_score);
        _hud?.SetCombo(_combo);

        LoadLevel(_currentLevelIndex);
        MusicPlayer.Instance?.PlayGameplay(_currentLevelIndex);
        SetState(GameState.Ready);
    }

    // ── Level Loading ───────────────────────────────────────────────────────

    public void LoadLevel(int levelIndex)
    {
        _isCommunityMode = false;
        _isAdvancingLevel = false;

        if (_levelIds == null || _levelIds.Length == 0)
        {
            Debug.LogError("No level IDs assigned in GameManager.");
            return;
        }

        _currentLevelIndex = Mathf.Clamp(levelIndex, 0, _levelIds.Length - 1);
        UnlockLevel(_currentLevelIndex);

        // Fallback if LevelLoader not wired
        if (_levelLoader == null)
            _levelLoader = FindFirstObjectByType<LevelLoader>();

        if (_levelLoader == null)
        {
            Debug.LogError("GameManager: No LevelLoader found!");
            return;
        }

        _levelLoader.LoadLevel(_levelIds[_currentLevelIndex]);

        RefreshUIRefsIfMissing();


        // Reset per-level score stats
        _levelStartScore  = _score;
        _levelBestCombo   = 0;
        _levelComboBonus  = 0;
        LivesAtLevelStart = _lives;

        _levelStartTime = Time.realtimeSinceStartup;
        if (!_isEditorTestMode)
            AchievementManager.Instance?.OnLevelStarted(_currentLevelIndex, _lives);

        // Reset powerups between levels
        PowerupManager.Instance?.ResetAll();

        // Destroy any extra balls from Multi-Ball and restore primary
        _primaryBallOnHold = false;
        _activeClonesCount = 0;
        if (_ball != null) _ball.gameObject.SetActive(true);
        var allBalls = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        foreach (var b in allBalls)
            if (b != _ball) Destroy(b.gameObject);

        _ball.ResetToPaddle();
        _paddle.ResetPosition();

        // Show persistent level info (number + title) in HUD
        string levelTitle = _levelLoader?.CurrentLevel?.displayName ?? "";
        _hud?.SetLevelInfo(_currentLevelIndex + 1, levelTitle);

        // Generate (or retrieve) the unique level code and show it under Lives
        string levelCode = LevelCodeManager.Instance?.GetOrCreateCode(_currentLevelIndex) ?? "";
        _hud?.SetLevelCode(levelCode);
    }

    private void LoadDemoLevel()
    {
        if (_levelIds == null || _levelIds.Length == 0) return;

        _levelLoader?.LoadLevel(_levelIds[0]); // always load first level for demo

        RefreshUIRefsIfMissing();

        _ball?.ResetToPaddle();
        _paddle?.ResetPosition();
    }

    // ── Level Unlock Tracking ───────────────────────────────────────────────

    public bool IsLevelUnlocked(int index)
    {
        if (index == 0) return true;
        return PlayerPrefs.GetInt($"lvl_unlocked_{index}", 0) == 1;
    }

    /// <summary>Returns the array index for a given level ID, or -1 if not found.</summary>
    public int GetLevelIndex(string levelId)
    {
        if (_levelIds == null) return -1;
        for (int i = 0; i < _levelIds.Length; i++)
            if (_levelIds[i] == levelId) return i;
        return -1;
    }

    private void UnlockLevel(int index)
    {
        if (PlayerPrefs.GetInt($"lvl_unlocked_{index}", 0) == 0)
        {
            PlayerPrefs.SetInt($"lvl_unlocked_{index}", 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Start a fresh game beginning at the specified level (used by Level Select from Main Menu).</summary>
    public void StartGameAtLevel(int levelIndex)
    {
        RestoreGameplayAfterHighScores();
        _isDemoMode = false;
        _paddle?.SetDemoMode(false);

        PowerupManager.Instance?.ResetAll();
        ClearAllParticles();

        _score = 0;
        _combo = 0;
        _comboTimer = 0f;
        _lives = _startingLives;
        _scoreFrenzyActive = false;

        _hud?.SetScore(_score);
        _hud?.SetLives(_lives);
        _hud?.SetCombo(_combo);

        if (!_isEditorTestMode)
            AchievementManager.Instance?.OnGameStarted();

        LoadLevel(levelIndex);
        MusicPlayer.Instance?.PlayGameplay(levelIndex);
        SetState(GameState.Ready);

        _mainMenuUI?.Hide();
        _highScoresUI?.Hide();
    }

    // ── Community Level ──────────────────────────────────────────────────────

    public bool IsCommunityMode => _isCommunityMode;
    public CommunityLevelMeta CurrentCommunityMeta => _currentCommunityMeta;

    /// <summary>Starts gameplay with a community-published level.</summary>
    public void StartCommunityLevel(CommunityLevelMeta meta, LevelData data)
    {
        if (meta == null || data == null)
        {
            Debug.LogError("[GameManager] StartCommunityLevel: meta or data is null.");
            return;
        }

        _isCommunityMode      = true;
        _currentCommunityMeta = meta;
        _cachedCommunityData  = data;
        _isEditorTestMode     = false;

        // Stop any in-progress level-advance routine from a previous level
        // (StartCommunityLevel bypasses LoadLevel which normally resets this).
        if (_advanceRoutine != null) { StopCoroutine(_advanceRoutine); _advanceRoutine = null; }
        _isAdvancingLevel = false;

        RestoreGameplayAfterHighScores();
        _isDemoMode = false;
        _paddle?.SetDemoMode(false);

        // Hide all menus
        _mainMenuUI?.Hide();
        _gameOverUI?.Hide();
        _victoryUI?.Hide();
        _pauseMenuUI?.Hide();
        _highScoresUI?.Hide();
        _settingsUI?.Hide();
        _storeUI?.Hide();

        PowerupManager.Instance?.ResetAll();
        ClearAllParticles();

        _score             = 0;
        _combo             = 0;
        _comboTimer        = 0f;
        _lives             = _startingLives;
        _scoreFrenzyActive = false;
        _cachedFuryCharge  = 0f;
        _levelStartScore   = 0;
        _levelBestCombo    = 0;
        _levelComboBonus   = 0;
        LivesAtLevelStart  = _lives;

        _hud?.SetScore(_score);
        _hud?.SetLives(_lives);
        _hud?.SetCombo(_combo);

        if (_levelLoader == null)
            _levelLoader = FindFirstObjectByType<LevelLoader>();

        if (_levelLoader == null)
        {
            Debug.LogError("[GameManager] StartCommunityLevel: No LevelLoader found.");
            return;
        }

        _levelLoader.LoadLevelData(data);

        _primaryBallOnHold = false;
        _activeClonesCount = 0;
        if (_ball != null) _ball.gameObject.SetActive(true);
        var allBalls = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        foreach (var b in allBalls)
            if (b != _ball) Destroy(b.gameObject);

        _ball?.ResetToPaddle();
        _paddle?.ResetPosition();

        string levelTitle = string.IsNullOrEmpty(meta.title) ? "Community Level" : meta.title;
        _hud?.SetLevelInfo(0, levelTitle);
        _hud?.SetLevelCode("");

        AudioListener.pause = false;
        _levelStartTime = Time.realtimeSinceStartup;

        // Notify play count (fire and forget)
        CommunityLevelService.Instance?.IncrementPlayCount(meta.id);

        MusicPlayer.Instance?.PlayGameplay(0);
        SetState(GameState.Ready);
    }

    /// <summary>Retries the last played community level (called from GameOverUI).</summary>
    public void RetryCommunityLevel()
    {
        if (_currentCommunityMeta != null && _cachedCommunityData != null)
            StartCommunityLevel(_currentCommunityMeta, _cachedCommunityData);
    }

    // ── State Management ────────────────────────────────────────────────────

    public void SetState(GameState newState)
    {
        if (newState == GameState.Paused)
            _stateBeforePause = _state; // capture BEFORE _state is overwritten
        _state = newState;
        Time.timeScale = 1f;

        _hud.SetState(_state.ToString());
        Debug.Log($"Setting GameState to '{newState.ToString()}'");


        switch (_state)
        {
            case GameState.MainMenu:
                SetCursorMenuMode();
                _hud?.SetVisible(false);
                PowerupHUD.Instance?.SetVisible(false);
                PurrBucksManager.Instance?.SetVisible(false);
                SfxPlayer.Instance?.MuteAll(true);
                MusicPlayer.Instance?.PlayMenu();
                break;

            case GameState.HighScores:
                SetCursorMenuMode();
                _hud?.SetVisible(false);
                PowerupHUD.Instance?.SetVisible(false);
                SfxPlayer.Instance?.MuteAll(true);
                // Menu music continues — no change needed
                break;

            case GameState.Ready:
                SetCursorPlayMode();
                _hud?.SetVisible(true);
                PowerupHUD.Instance?.SetVisible(true);
                PurrBucksManager.Instance?.SetVisible(!_isEditorTestMode);
                _hud?.SetState("Ready");
                SfxPlayer.Instance?.MuteAll(false);
                break;

            case GameState.Playing:
                SetCursorPlayMode();
                _hud?.SetState("Playing");
                _hud?.HideCenter();
                _pauseMenuUI?.Hide();
                _settingsUI?.Hide();
                if (_isEditorTestMode)
                    PurrBucksManager.Instance?.SetVisible(false);
                break;

            case GameState.Paused:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                _hud?.SetState("Paused");
                _hud?.HideCenter();
                ShowPauseMenu();
                break;

            case GameState.Credits:
                SetCursorMenuMode();
                Time.timeScale = 1f;
                _hud?.SetVisible(false);
                PowerupHUD.Instance?.SetVisible(false);
                _pauseMenuUI?.Hide();
                _victoryUI?.Hide();
                // Permanently unlock the Credits button on the main menu
                PlayerPrefs.SetInt(PREF_GAME_COMPLETED, 1);
                PlayerPrefs.Save();
                if (_creditsUI == null)
                    _creditsUI = FindFirstObjectByType<CreditsUI>(FindObjectsInactive.Include);
                // Submit final score to Steam global leaderboards before showing credits
                if (_score > 0)
                {
                    string allTimeBoard = PurrbricksLeaderboards.OverallAllTime;
                    string weeklyBoard  = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Weekly);
                    string dailyBoard   = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Daily);
                    SteamLeaderboardManager.Instance?.SubmitScore(allTimeBoard, _score);
                    SteamLeaderboardManager.Instance?.SubmitScore(weeklyBoard,  _score);
                    SteamLeaderboardManager.Instance?.SubmitScore(dailyBoard,   _score);
                }
                _creditsUI?.ShowCredits(_score);
                break;

            case GameState.GameOver:
                if (_isEditorTestMode)
                {
                    ReturnToEditorFromTest();
                    break;
                }
                SetCursorMenuMode();
                Time.timeScale = 0f;
                SfxPlayer.Instance?.PlayGameOver();
                if (_isCommunityMode)
                    _gameOverUI?.ShowCommunityGameOver(_score);
                else
                    _gameOverUI?.ShowGameOver(_score);
                MusicPlayer.Instance?.PlayGameOver();
                break;

            case GameState.Cleared:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                _hud?.SetState("Cleared");
                _pauseMenuUI?.Hide();
                ScreenEffects.Instance?.SetBadVignette(false);
                //_hud?.ShowCenter("Level Cleared!");
                _ball?.ResetToPaddle();
                break;

            case GameState.Victory:
            {
                if (_isEditorTestMode)
                {
                    ReturnToEditorFromTest();
                    break;
                }
                SetCursorMenuMode();
                Time.timeScale = 0f;
                _pauseMenuUI?.Hide();
                ScreenEffects.Instance?.SetBadVignette(false);
                if (_isCommunityMode && _currentCommunityMeta != null)
                {
                    _victoryUI?.ShowCommunityVictory(
                        _score - _levelStartScore, _levelComboBonus, _levelBestCombo,
                        _currentCommunityMeta);
                }
                else
                {
                    string levelId = (_levelIds != null && _currentLevelIndex < _levelIds.Length)
                        ? _levelIds[_currentLevelIndex] : "";
                    CurrentLevelPar = ComputeLevelPar(levelId);
                    _victoryUI?.ShowVictory(_score - _levelStartScore, _levelComboBonus, _levelBestCombo, levelId, _currentLevelIndex);
                    if (_levelIds != null)
                        AchievementManager.Instance?.CheckAllThreeStarred(_levelIds);
                }
                MusicPlayer.Instance?.PlayLevelFinish();

                float levelTime = Time.realtimeSinceStartup - _levelStartTime;
                bool invis   = PowerupManager.Instance?.IsActive(PowerupType.InvisiBall)    ?? false;
                bool tiny    = PowerupManager.Instance?.IsActive(PowerupType.TinyBall)      ?? false;
                bool drunk   = PowerupManager.Instance?.IsActive(PowerupType.DrunkenPaddle) ?? false;
                 if (!_isEditorTestMode)
                 {
                     AchievementManager.Instance?.OnLevelCompleted(
                         _currentLevelIndex, levelTime, _lives,
                         invisiBallActive: invis, tinyBallActive: tiny, drunkenPaddleActive: drunk);
                 }
                 break;
             }
        }

        bool gameplayActive = _state == GameState.Ready
                           || _state == GameState.Playing
                           || _state == GameState.Paused;
        InputManager.EnableGameplay(gameplayActive);
    }

    // ── Performance star support ──────────────────────────────────────────────

    /// <summary>Base-point sum of all destructible bricks on the current level. Set just before ShowVictory.</summary>
    public int CurrentLevelPar { get; private set; }

    /// <summary>Exposes the ordered level ID array for star-achievement checks.</summary>
    public string[] GetAllLevelIds() => _levelIds;

    private int ComputeLevelPar(string levelId)
    {
        if (string.IsNullOrEmpty(levelId)) return 0;
        var asset = Resources.Load<TextAsset>($"Levels/{levelId}");
        if (asset == null) return 0;
        try
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<LevelData>(asset.text);
            if (data?.bricks == null) return 0;
            int total = 0;
            foreach (var b in data.bricks)
            {
                var template = BrickTemplateRegistry.Instance?.Get(b.templateId);
                if (template == null || template.isIndestructible) continue;
                // Mirror LevelLoader's point resolution: JSON override first, template default as fallback.
                int pts = b.points ?? template.defaultPoints;
                total += pts;
            }
            return total;
        }
        catch { return 0; }
    }

    private void SetCursorPlayMode()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void SetCursorMenuMode()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // ── Game Events ─────────────────────────────────────────────────────────

    // Called when the PRIMARY ball hits the death zone
    public void OnPrimaryBallLost()
    {
        if (_isAdvancingLevel) return; // level already clearing, ignore
        if (_state != GameState.Playing && !_isDemoMode) return;

        if (_isDemoMode)
        {
            _ball?.ResetToPaddle();
            _ball?.Launch();
            return;
        }

        // Are any clone balls still in play? Use explicit counter to avoid
        // deferred-Destroy false positives from FindObjectsByType.
        if (_activeClonesCount > 0)
        {
            _primaryBallOnHold = true;
            if (_ball != null)
                _ball.gameObject.SetActive(false);
            return;
        }

        // No clones — standard life loss
        _primaryBallOnHold = false;
        LoseLife();
    }

    // Called when a CLONE ball hits the death zone (already destroyed by DeathZone)
    public void OnCloneBallLost()
    {
        if (_isAdvancingLevel) return; // level already clearing, ignore
        if (_state != GameState.Playing && !_isDemoMode) return;
        if (_isDemoMode) return;

        // Decrement first — Destroy() for this clone is deferred but we count it gone now
        if (_activeClonesCount > 0)
            _activeClonesCount--;

        // If primary is not on hold, nothing extra to do
        if (!_primaryBallOnHold) return;

        // Still clones alive — wait for them
        if (_activeClonesCount > 0) return;

        // Last clone just died — re-enable primary and lose a life
        _primaryBallOnHold = false;
        if (_ball != null)
            _ball.gameObject.SetActive(true);
        LoseLife();
    }

    // Keep old name as alias so nothing else breaks
    public void OnBallLost() => OnPrimaryBallLost();

    private void LoseLife()
    {
        SfxPlayer.Instance?.PlayLifeLost();
        ScreenEffects.Instance?.FlashRed();
        CameraShake.Instance?.Shake(0.30f, 0.55f);

        if (!_isEditorTestMode)
            AchievementManager.Instance?.OnLifeLostOnLevel(_currentLevelIndex);

        _lives--;
        _hud?.SetLives(_lives);

        PowerupManager.Instance?.ResetAll();

        if (_lives <= 0)
        {
            if (_isEditorTestMode)
            {
                ReturnToEditorFromTest();
                return;
            }

            if (!_isEditorTestMode)
                AchievementManager.Instance?.OnGameOver(_score, _currentLevelIndex);
            SetState(GameState.GameOver);
            return;
        }

        _ball?.ResetToPaddle();
        ResetCombo();
        SetState(GameState.Ready);
    }

    public void OnLevelCleared()
    {
        if (_isAdvancingLevel) return;
        if (_state == GameState.Cleared || _state == GameState.Victory || _state == GameState.GameOver) return;
        if (_isEditorTestMode)
        {
            ReturnToEditorFromTest();
            return;
        }
        if (_isDemoMode)
        {
            // Demo mode: silently restart level
            LoadDemoLevel();
            return;
        }

        _isAdvancingLevel = true;
        if (_advanceRoutine != null) StopCoroutine(_advanceRoutine);
        _advanceRoutine = StartCoroutine(AdvanceLevelRoutine());
    }

    /// <summary>Called by LevelManager when exactly 1 destructible brick remains.</summary>
    public void OnLastBrickRemaining(Vector3 brickPos)
    {
        if (_isDemoMode || _state != GameState.Playing) return;
        Time.timeScale = 0.35f;
        CameraShake.Instance?.ZoomIn(0.65f, 2.5f);
    }

    private IEnumerator AdvanceLevelRoutine()
    {
        // Hold slow-mo for a beat so the final-brick destruction plays dramatically,
        // then restore normal speed. All waits are realtime so they work during slow-mo.
        yield return new WaitForSecondsRealtime(0.55f);

        _ball?.ResetToPaddle();
        Time.timeScale = 1f;
        CameraShake.Instance?.ResetZoom();
        SfxPlayer.Instance?.PlayWin();
        yield return new WaitForSecondsRealtime(1.5f); // let particles/effects finish
        SetState(GameState.Victory);
    }

    // ── Scoring ─────────────────────────────────────────────────────────────

    public int AddScore(int basePoints)
    {
        int multiplier = 1 + _combo;
        int points     = basePoints * multiplier;
        int comboBonus = basePoints * _combo; // extra above base

        if (_scoreFrenzyActive)
        {
            points     *= 2;
            comboBonus *= 2;
        }

        _score           += points;
        _levelComboBonus += comboBonus;
        _hud?.SetScore(_score);
        _comboTimer = _comboResetSeconds;

        if (!_isEditorTestMode && !_isDemoMode)
            AchievementManager.Instance?.OnScoreChanged(_score);

        return points;
    }

    public void IncrementCombo()
    {
        _combo++;
        if (_combo > _levelBestCombo) _levelBestCombo = _combo;
        _hud?.SetCombo(_combo);
        _comboTimer = _comboResetSeconds;

        if (!_isEditorTestMode)
            AchievementManager.Instance?.OnComboChanged(_combo, _scoreFrenzyActive);

        // Show combo feedback at milestones
        if (_combo == 2 || _combo % 5 == 0)
            ComboFeedback.Show(_combo);
    }

    public void ResetCombo()
    {
        _combo = 0;
        _hud?.SetCombo(_combo);
        _comboTimer = 0f;
    }

    // ── Fury Strike ─────────────────────────────────────────────────────────

    private Coroutine _furyRoutine;

    private void TriggerFuryStrike()
    {
        if (_furyRoutine != null) return; // already running
        _furyRoutine = StartCoroutine(FuryStrikeSequence());
    }

    private IEnumerator FuryStrikeSequence()
    {
        var allBalls = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        if (allBalls.Length == 0) { _furyRoutine = null; yield break; }

        // Collect destructible bricks and sort top-to-bottom, left-to-right
        var allBricks = Object.FindObjectsByType<Brick>(FindObjectsSortMode.None);
        var targets   = new System.Collections.Generic.List<Brick>(allBricks.Length);
        foreach (var b in allBricks)
            if (!b.IsIndestructible) targets.Add(b);

        if (targets.Count == 0) { _furyRoutine = null; yield break; }

        if (!_isEditorTestMode)
            AchievementManager.Instance?.OnFuryStrikeStarted(allBalls.Length);

        targets.Sort((a, b2) =>
        {
            int rowCmp = b2.transform.position.y.CompareTo(a.transform.position.y);
            return rowCmp != 0 ? rowCmp : a.transform.position.x.CompareTo(b2.transform.position.x);
        });

        // Reset ramp on all balls before we begin
        foreach (var ball in allBalls) ball.ResetRamp();

        // ── Enter slow-motion ───────────────────────────────────────────────
        Time.timeScale = 0.08f;

        // Dramatic intro: flash, shake, notification, audio
        ScreenEffects.Instance?.FlashWhite(0.90f, 0.70f);
        CameraShake.Instance?.Shake(0.85f, 1.20f);
        PowerupNotification.Instance?.Show("PURRY FURY STRIKE!", new Color(1f, 0.85f, 0.05f), isSpecial: true);
        SfxPlayer.Instance?.PlayFuryStrike();

        // Gold burst at each ball position
        foreach (var ball in allBalls)
            if (ball != null)
                BrickParticleGenerator.SpawnBurst(ball.transform.position, new Color(1f, 0.85f, 0.05f), 60, true);

        // Create one sweeping laser beam per ball (additive gold beams)
        var beamGOs = new GameObject[allBalls.Length];
        var beams   = new LineRenderer[allBalls.Length];
        var beamMat = new Material(Shader.Find("Sprites/Default"));
        beamMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        beamMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive

        for (int i = 0; i < allBalls.Length; i++)
        {
            beamGOs[i] = new GameObject("FuryBeam");
            var lr = beamGOs[i].AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth    = 0.10f;
            lr.endWidth      = 0.03f;
            lr.material      = beamMat;
            lr.sortingOrder  = 20;

            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 1f, 0.5f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.15f, 1f)
                }
            );
            lr.colorGradient = grad;
            beams[i] = lr;
        }

        // Hold briefly for dramatic effect before carnage begins
        yield return new WaitForSecondsRealtime(0.32f);

        // ── Stagger-destroy bricks, beams sweep to each target ─────────────
        float delayBetween = Mathf.Clamp(0.85f / Mathf.Max(1, targets.Count), 0.012f, 0.050f);

        foreach (var brick in targets)
        {
            if (brick == null) continue;

            // Point each beam at this brick
            for (int i = 0; i < allBalls.Length; i++)
            {
                if (allBalls[i] != null && beams[i] != null)
                {
                    beams[i].SetPosition(0, allBalls[i].transform.position);
                    beams[i].SetPosition(1, brick.transform.position);
                }
            }

            brick.FuryKill();
            yield return new WaitForSecondsRealtime(delayBetween);
        }

        // Final big shake as the dust settles
        CameraShake.Instance?.Shake(0.45f, 0.55f);
        if (!_isEditorTestMode)
            AchievementManager.Instance?.OnFuryStrikeFinished();
        yield return new WaitForSecondsRealtime(0.22f);

        // Clean up laser beams
        foreach (var go in beamGOs)
            if (go != null) Object.Destroy(go);
        Object.Destroy(beamMat);

        // Restore time scale only if still actively playing
        if (_state == GameState.Playing)
            Time.timeScale = 1f;

        _furyRoutine = null;
    }

    // ── Debug Helpers ───────────────────────────────────────────────────────

    private void ClearAllButOneBrick()
    {
        var bricks = Object.FindObjectsByType<Brick>(FindObjectsSortMode.None);

        if (bricks.Length <= 1)
        {
            Debug.Log("ClearAllButOneBrick: Only 1 or 0 bricks remaining.");
            return;
        }

        Debug.Log($"Clearing {bricks.Length - 1} bricks, leaving 1 for testing...");

        for (int i = 1; i < bricks.Length; i++)
        {
            if (bricks[i] != null)
            {
                LevelManager.Instance?.OnBrickDestroyed();
                Destroy(bricks[i].gameObject);
            }
        }
    }

    private void DebugClearLevelBricks()
    {
        if (_isAdvancingLevel) return;

        var bricks = Object.FindObjectsByType<Brick>(FindObjectsSortMode.None);
        if (bricks.Length == 0)
        {
            Debug.Log("DebugClearLevelBricks: No bricks to clear.");
            return;
        }

        foreach (var brick in bricks)
        {
            if (brick == null) continue;
            LevelManager.Instance?.OnBrickDestroyed();
            Destroy(brick.gameObject);
        }

        Debug.Log("DebugClearLevelBricks: Cleared all bricks via ] hotkey.");
    }

    private void DebugDropPrimaryBallIntoDeathZone()
    {
        if (_ball == null) return;

        var deathZone = FindFirstObjectByType<DeathZone>();
        if (deathZone != null)
        {
            _ball.transform.position = deathZone.transform.position;
            var rb = _ball.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        OnPrimaryBallLost();
    }

    private void ClearAllParticles()
    {
        var particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        foreach (var ps in particles)
        {
            if (ps == null) continue;
            // Don't destroy the ball's own GameObject — BallTrail puts its PS on the ball root.
            if (ps.GetComponent<BallController>() != null) continue;
            Destroy(ps.gameObject);
        }
    }

}
