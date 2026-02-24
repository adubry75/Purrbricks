using Steamworks;
using UnityEngine;

/// <summary>
/// Manages all Steam achievements for Purrbricks.
/// All achievement IDs must match exactly what is registered in the Steamworks Partner Portal.
/// Safe to call when Steam is unavailable — all methods become no-ops.
/// </summary>
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    // ── Achievement IDs ────────────────────────────────────────────────────────
    // These MUST match the API Names set in the Steamworks Partner Portal.
    public static class ID
    {
        // Progression
        public const string FirstPaws      = "ACH_FIRST_PAWS";
        public const string Burglar        = "ACH_BURGLAR";
        public const string ClawMarks      = "ACH_CLAW_MARKS";
        public const string Halfway        = "ACH_HALFWAY";
        public const string Meow           = "ACH_MEOW";
        public const string Purrfect       = "ACH_PURRFECT";

        // Score
        public const string Score10K       = "ACH_SCORE_10K";
        public const string Score100K      = "ACH_SCORE_100K";
        public const string Score1M        = "ACH_SCORE_1M";
        public const string GemHunter      = "ACH_GEM_HUNTER";

        // Combos & Fury Strike
        public const string ComboTen       = "ACH_COMBO_10";
        public const string ComboTwentyFive= "ACH_COMBO_25";
        public const string HavocFirst     = "ACH_HAVOC_FIRST";
        public const string HavocTen       = "ACH_HAVOC_10";
        public const string MaximumHavoc   = "ACH_MAXIMUM_HAVOC";

        // Powerup milestones
        public const string BallPit        = "ACH_BALL_PIT";
        public const string DoubleTrouble  = "ACH_DOUBLE_TROUBLE";
        public const string NineLives      = "ACH_NINE_LIVES";
        public const string Cursed         = "ACH_CURSED";

        // Survival
        public const string LastLife       = "ACH_LAST_LIFE";
        public const string Unbroken       = "ACH_UNBROKEN";
        public const string LongHaul       = "ACH_LONG_HAUL";
        public const string Blindfolded    = "ACH_BLINDFOLDED";
        public const string TinyTerror     = "ACH_TINY_TERROR";
        public const string DrunkDriver    = "ACH_DRUNK_DRIVER";

        // Weird / Extreme
        public const string Catastrophic   = "ACH_CATASTROPHIC";
        public const string Level1GameOver = "ACH_LEVEL1_GAMEOVER";
        public const string Pacifist       = "ACH_PACIFIST";
        public const string SpeedRunner    = "ACH_SPEED_RUNNER";
        public const string Curiosity      = "ACH_CURIOSITY";
        public const string Catastrophe    = "ACH_CATASTROPHE";
    }

    // Steam stat name for cumulative Fury Strike count
    private const string StatFuryStrikes = "STAT_FURY_STRIKES";

    // PlayerPrefs key prefix for per-level death counts
    private const string PrefixLevelDeaths = "lb_deaths_";

    private const int TotalLevels = 80;

    // ── Session state ──────────────────────────────────────────────────────────

    private int   _sessionExtraLives;         // extra lives collected this run
    private int   _sessionBricksDestroyed;    // bricks destroyed this run (for Pacifist)
    private int   _levelStartLives;           // lives at start of current level (for Last Life)
    private bool  _diedThisLevel;             // lost a life on the current level attempt
    private int   _consecutiveNoDeathStreak;  // levels cleared without dying in a row
    private float _ballAliveSeconds;          // seconds the primary ball has been live this session
    private bool  _longHaulUnlocked;          // prevent repeat unlocks within a run
    private int   _furyStrikeBricksHit;       // bricks destroyed by the current Fury Strike sweep
    private bool  _inFuryStrike;             // currently inside a Fury Strike sequence

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public hooks (called by GameManager, PowerupManager, Brick, etc.) ──────

    /// <summary>Call when a new run begins (Play button pressed).</summary>
    public void OnGameStarted()
    {
        _sessionExtraLives      = 0;
        _sessionBricksDestroyed = 0;
        _longHaulUnlocked       = false;
        _ballAliveSeconds       = 0f;
        _consecutiveNoDeathStreak = 0;
    }

    /// <summary>Call at the start of each level load.</summary>
    public void OnLevelStarted(int levelIndex, int lives)
    {
        _levelStartLives  = lives;
        _diedThisLevel    = false;
        _ballAliveSeconds = 0f; // reset per-level ball timer
    }

    /// <summary>
    /// Call when the player clears a level (before loading the next one).
    /// <paramref name="levelTime"/> is seconds from level start to clear.
    /// <paramref name="livesRemaining"/> is lives after clearing.
    /// </summary>
    public void OnLevelCompleted(int levelIndex, float levelTime, int livesRemaining,
                                 bool invisiBallActive, bool tinyBallActive, bool drunkenPaddleActive)
    {
        // ── Progression ─────────────────────────────────────────────────────
        Unlock(ID.FirstPaws);
        
        // Human-facing "levels completed" = levelIndex + 1 (0-based index)
        int humanLevel = levelIndex + 1;
        if (humanLevel >= 10) Unlock(ID.Burglar);
        if (humanLevel >= 25) Unlock(ID.ClawMarks);
        if (humanLevel >= 30) Unlock(ID.Halfway);
        if (humanLevel >= 60) Unlock(ID.Meow);
        if (levelIndex == (TotalLevels - 1)) Unlock(ID.Purrfect); // final level

        // ── Speed Runner ─────────────────────────────────────────────────────
        if (levelTime <= 30f) Unlock(ID.SpeedRunner);

        // ── Last Life ────────────────────────────────────────────────────────
        // livesRemaining == 1 means on their last life going into the next level
        if (livesRemaining == 1 && _levelStartLives > 1) Unlock(ID.LastLife);

        // ── Unbroken (5 consecutive levels without dying) ─────────────────────
        if (!_diedThisLevel)
        {
            _consecutiveNoDeathStreak++;
            if (_consecutiveNoDeathStreak >= 5) Unlock(ID.Unbroken);
        }
        else
        {
            _consecutiveNoDeathStreak = 0;
        }

        // ── Bad-powerup completions ───────────────────────────────────────────
        if (invisiBallActive)    Unlock(ID.Blindfolded);
        if (tinyBallActive)      Unlock(ID.TinyTerror);
        if (drunkenPaddleActive) Unlock(ID.DrunkDriver);
    }

    /// <summary>Call when game over occurs (all lives lost).</summary>
    public void OnGameOver(int finalScore, int levelIndex)
    {
        // Catastrophic Failure: game over with score 0
        if (finalScore == 0) Unlock(ID.Catastrophic);

        // Pacifist: never broke a single brick
        if (_sessionBricksDestroyed == 0) Unlock(ID.Pacifist);

        // I Meant To Do That: game over on level 1
        if (levelIndex == 0) Unlock(ID.Level1GameOver);
    }

    /// <summary>Call after score changes. Pass current total score.</summary>
    public void OnScoreChanged(int totalScore)
    {
        if (totalScore >= 10_000)    Unlock(ID.Score10K);
        if (totalScore >= 100_000)   Unlock(ID.Score100K);
        if (totalScore >= 1_000_000) Unlock(ID.Score1M);
    }

    /// <summary>Call when combo increments. Pass new combo value and whether ScoreFrenzy is active.</summary>
    public void OnComboChanged(int combo, bool scoreFrenzyActive)
    {
        if (combo >= 10) Unlock(ID.ComboTen);
        if (combo >= 25) Unlock(ID.ComboTwentyFive);

        // Double Trouble: ScoreFrenzy active while at ×10+ combo
        if (scoreFrenzyActive && combo >= 10) Unlock(ID.DoubleTrouble);
    }

    /// <summary>Call at the start of a Fury Strike sequence with the current ball count.</summary>
    public void OnFuryStrikeStarted(int ballCount)
    {
        _furyStrikeBricksHit = 0;
        _inFuryStrike        = true;

        Unlock(ID.HavocFirst);

        if (ballCount >= 5) Unlock(ID.MaximumHavoc);

        // Increment persistent Steam stat
        IncrementFuryStrikeStat();
    }

    /// <summary>Call after all Fury Strike bricks have been destroyed.</summary>
    public void OnFuryStrikeFinished()
    {
        _inFuryStrike = false;
        if (_furyStrikeBricksHit >= 20) Unlock(ID.Catastrophe);
    }

    /// <summary>Call each time a brick is destroyed during the Fury Strike sweep.</summary>
    public void OnFuryStrikeBrickDestroyed()
    {
        if (!_inFuryStrike) return;
        _furyStrikeBricksHit++;
    }

    /// <summary>Call whenever an Extra Life powerup is collected.</summary>
    public void OnExtraLifePickup()
    {
        _sessionExtraLives++;
        if (_sessionExtraLives >= 9) Unlock(ID.NineLives);
    }

    /// <summary>Call when the count of active bad powerups changes.</summary>
    public void OnBadPowerupCountChanged(int count)
    {
        if (count >= 3) Unlock(ID.Cursed);
    }

    /// <summary>Call when the total active ball count changes.</summary>
    public void OnBallCountChanged(int count)
    {
        if (count >= 5) Unlock(ID.BallPit);
    }

    /// <summary>Call when a Gem brick is destroyed (template ID "gem").</summary>
    public void OnGemBrickDestroyed()
    {
        Unlock(ID.GemHunter);
    }

    /// <summary>
    /// Call each time any brick is destroyed during normal play.
    /// Used to track the Pacifist achievement counter.
    /// </summary>
    public void OnBrickDestroyed()
    {
        _sessionBricksDestroyed++;
    }

    /// <summary>
    /// Call each time the player loses a life on a specific level.
    /// Tracks the Curiosity Killed the Cat achievement (10 deaths on same level).
    /// </summary>
    public void OnLifeLostOnLevel(int levelIndex)
    {
        _diedThisLevel = true;

        string key   = PrefixLevelDeaths + levelIndex;
        int    count = PlayerPrefs.GetInt(key, 0) + 1;
        PlayerPrefs.SetInt(key, count);
        PlayerPrefs.Save();

        if (count >= 10) Unlock(ID.Curiosity);
    }

    /// <summary>
    /// Call each Update tick while the primary ball is live (not on paddle).
    /// Pass delta time in seconds.
    /// </summary>
    public void OnBallAliveUpdate(float dt)
    {
        if (_longHaulUnlocked) return;
        _ballAliveSeconds += dt;
        if (_ballAliveSeconds >= 300f) // 5 minutes
        {
            _longHaulUnlocked = true;
            Unlock(ID.LongHaul);
        }
    }

    // ── Steam helpers ──────────────────────────────────────────────────────────

    /// <summary>Unlock a Steam achievement by ID. Safe to call when Steam is unavailable.</summary>
    public void Unlock(string achievementId)
    {
        if (!IsSteamReady()) return;

        bool alreadySet;
        if (!SteamUserStats.GetAchievement(achievementId, out alreadySet)) return;
        if (alreadySet) return; // already earned — don't spam StoreStats

        if (SteamUserStats.SetAchievement(achievementId))
        {
            SteamUserStats.StoreStats();
            Debug.Log($"[Achievement] Unlocked: {achievementId}");
        }
        else
        {
            Debug.LogWarning($"[Achievement] SetAchievement failed for: {achievementId}");
        }
    }

    private void IncrementFuryStrikeStat()
    {
        if (!IsSteamReady()) return;

        int current;
        if (!SteamUserStats.GetStat(StatFuryStrikes, out current)) current = 0;
        current++;
        SteamUserStats.SetStat(StatFuryStrikes, current);
        SteamUserStats.StoreStats();

        if (current >= 10) Unlock(ID.HavocTen);
    }

    private static bool IsSteamReady() =>
        SteamworksBootstrap.Instance?.IsSteamAvailable == true;
}
