using System.Collections;
using UnityEngine;

public enum GameState
{
    MainMenu,
    HighScores,
    Ready,
    Playing,
    Cleared,
    Paused,
    GameOver,
    Victory
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Round Settings")]
    [SerializeField] private int _startingLives = 3;

    [Header("Scoring")]
    [SerializeField] private int _score;
    [SerializeField] private int _combo;
    [SerializeField] private float _comboResetSeconds = 22.0f;

    [Header("Levels")]
    [SerializeField] private string[] _levelIds;
    [SerializeField] private float _levelClearDelay = 2.5f;

    [Header("Refs")]
    [SerializeField] private LevelLoader _levelLoader;
    [SerializeField] private BallController _ball;
    [SerializeField] private PaddleController _paddle;
    [SerializeField] private HudController _hud;

    [Header("UI Screens")]
    private MainMenuUI _mainMenuUI;
    private GameOverUI _gameOverUI;
    private VictoryUI _victoryUI;
    private HighScoresUI _highScoresUI;

    private int _currentLevelIndex = 0;
    private Coroutine _advanceRoutine;
    private bool _isAdvancingLevel;
    private float _comboTimer;
    private int _lives;
    private bool _isDemoMode;
    private bool _primaryBallOnHold; // true while primary fell but clones still active
    private int _activeClonesCount;  // explicit count — avoids deferred-Destroy false positives

    // ── Per-level score stats (reset each LoadLevel) ─────────────────────────
    private int _levelStartScore;
    private int _levelBestCombo;
    private int _levelComboBonus;   // accumulated bonus above base from combos

    /// <summary>Current game state — readable by other scripts (e.g. BallController).</summary>
    public GameState State => _state;

    // Pre-allocated buffer for Fury Strike overlap queries — avoids heap allocation
    private static readonly Collider2D[] s_furyBuffer = new Collider2D[128];

    [SerializeField] private GameState _state = GameState.MainMenu;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        FindOrCreateUIScreens();
    }

    private void FindOrCreateUIScreens()
    {
        // Include inactive objects since UI screens hide themselves in Awake()
        _mainMenuUI = FindFirstObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        _gameOverUI = FindFirstObjectByType<GameOverUI>(FindObjectsInactive.Include);
        _victoryUI = FindFirstObjectByType<VictoryUI>(FindObjectsInactive.Include);
        _highScoresUI = FindFirstObjectByType<HighScoresUI>(FindObjectsInactive.Include);

        if (_mainMenuUI == null) Debug.LogError("MainMenuUI not found! Run Purrbricks > Setup Scene.");
        if (_gameOverUI == null) Debug.LogError("GameOverUI not found! Run Purrbricks > Setup Scene.");
        if (_victoryUI == null) Debug.LogError("VictoryUI not found! Run Purrbricks > Setup Scene.");
        if (_highScoresUI == null) Debug.LogError("HighScoresUI not found! Run Purrbricks > Setup Scene.");
    }

    private void Start()
    {
        _lives = _startingLives;
        ShowMainMenu();
    }

    private void Update()
    {
        // Numpad debug: activate powerups (1-6)
        if (_state == GameState.Playing)
        {
            if (Input.GetKeyDown(KeyCode.Keypad1)) PowerupManager.Instance?.Apply(PowerupType.WidePaddle);
            if (Input.GetKeyDown(KeyCode.Keypad2)) PowerupManager.Instance?.Apply(PowerupType.MultiBall);
            if (Input.GetKeyDown(KeyCode.Keypad3)) PowerupManager.Instance?.Apply(PowerupType.StickyBall);
            if (Input.GetKeyDown(KeyCode.Keypad4)) PowerupManager.Instance?.Apply(PowerupType.SpeedBall);
            if (Input.GetKeyDown(KeyCode.Keypad5)) PowerupManager.Instance?.Apply(PowerupType.ExtraLife);
            if (Input.GetKeyDown(KeyCode.Keypad6)) PowerupManager.Instance?.Apply(PowerupType.Laser);
            if (Input.GetKeyDown(KeyCode.Keypad7)) PowerupManager.Instance?.Apply(PowerupType.Fireball);
            if (Input.GetKeyDown(KeyCode.Keypad8)) PowerupManager.Instance?.Apply(PowerupType.BombBrick);
        }

        // Fury Strike: ENTER when charge bar is full
        if (_state == GameState.Playing &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            if (_ball != null && _ball.RampFraction >= 1f)
                TriggerFuryStrike();
        }

        // Debug hotkey: clear all but 1 brick
        if (Input.GetKeyDown(KeyCode.K) && _state == GameState.Playing)
            ClearAllButOneBrick();

        if (_state == GameState.Playing && Input.GetKeyDown(KeyCode.Backslash))
            DebugDropPrimaryBallIntoDeathZone();

        if (_state == GameState.Playing && Input.GetKeyDown(KeyCode.Escape))
            SetState(GameState.Paused);
        else if (_state == GameState.Paused && Input.GetKeyDown(KeyCode.Escape))
            SetState(GameState.Playing);

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

    public bool IsPrimaryBall(BallController ball) => ball == _ball;

    /// <summary>Exposes the primary ball so HavocBar can read RampFraction.</summary>
    public BallController GetPrimaryBall() => _ball;

    /// <summary>Called by BallController.SpawnClone after it creates a clone.</summary>
    public void RegisterClone() => _activeClonesCount++;

    public void StartGame()
    {
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

        _hud?.SetScore(_score);
        _hud?.SetLives(_lives);
        _hud?.SetCombo(_combo);

        LoadLevel(_currentLevelIndex);
        MusicPlayer.Instance?.PlayGameplay(0);
        SetState(GameState.Ready);

        _mainMenuUI?.Hide();
        _highScoresUI?.Hide();
    }

    public void ShowMainMenu()
    {
        _isDemoMode = true;
        _paddle?.SetDemoMode(true);
        _gameOverUI?.Hide();
        _victoryUI?.Hide();
        _highScoresUI?.Hide();
        _mainMenuUI?.Show();

        LoadDemoLevel();
        SetState(GameState.MainMenu);
    }

    public void ShowHighScores()
    {
        _isDemoMode = true;
        _paddle?.SetDemoMode(true);
        _mainMenuUI?.Hide();
        _highScoresUI?.Show();

        LoadDemoLevel();
        SetState(GameState.HighScores);
    }

    /// <summary>Called by GameOverUI after name is submitted — shows high scores without changing music.</summary>
    public void ShowHighScoresAfterGameOver()
    {
        _gameOverUI?.Hide();
        _highScoresUI?.Show();
        // Intentionally no music change — game over track keeps playing
    }

    public void RestartGame()
    {
        _score = 0;
        _combo = 0;
        _comboTimer = 0f;
        _lives = _startingLives;
        _currentLevelIndex = 0;

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
            // All levels complete - show final victory with high score entry
            SetState(GameState.GameOver);
            _gameOverUI?.ShowGameComplete(_score);
            return;
        }

        LoadLevel(next);
        MusicPlayer.Instance?.PlayGameplay(_currentLevelIndex);
        SetState(GameState.Ready);
    }

    // ── Level Loading ───────────────────────────────────────────────────────

    public void LoadLevel(int levelIndex)
    {
        _isAdvancingLevel = false;

        if (_levelIds == null || _levelIds.Length == 0)
        {
            Debug.LogError("No level IDs assigned in GameManager.");
            return;
        }

        _currentLevelIndex = Mathf.Clamp(levelIndex, 0, _levelIds.Length - 1);

        // Fallback if LevelLoader not wired
        if (_levelLoader == null)
            _levelLoader = FindFirstObjectByType<LevelLoader>();

        if (_levelLoader == null)
        {
            Debug.LogError("GameManager: No LevelLoader found!");
            return;
        }

        _levelLoader.LoadLevel(_levelIds[_currentLevelIndex]);

        // Reset per-level score stats
        _levelStartScore = _score;
        _levelBestCombo  = 0;
        _levelComboBonus = 0;

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
    }

    private void LoadDemoLevel()
    {
        if (_levelIds == null || _levelIds.Length == 0) return;

        _levelLoader?.LoadLevel(_levelIds[0]); // always load first level for demo
        _ball?.ResetToPaddle();
        _paddle?.ResetPosition();
    }

    // ── State Management ────────────────────────────────────────────────────

    public void SetState(GameState newState)
    {
        _state = newState;
        Time.timeScale = 1f;

        switch (_state)
        {
            case GameState.MainMenu:
                SetCursorMenuMode();
                _hud?.gameObject.SetActive(false);
                SfxPlayer.Instance?.MuteAll(true);
                MusicPlayer.Instance?.PlayMenu();
                break;

            case GameState.HighScores:
                SetCursorMenuMode();
                _hud?.gameObject.SetActive(false);
                SfxPlayer.Instance?.MuteAll(true);
                // Menu music continues — no change needed
                break;

            case GameState.Ready:
                SetCursorPlayMode();
                _hud?.gameObject.SetActive(true);
                _hud?.SetState("Ready");
                _hud?.ShowCenter("Hold Space to Aim\r\nRelease to Launch");
                SfxPlayer.Instance?.MuteAll(false);
                break;

            case GameState.Playing:
                SetCursorPlayMode();
                _hud?.SetState("Playing");
                _hud?.HideCenter();
                break;

            case GameState.Paused:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                _hud?.SetState("Paused");
                _hud?.ShowCenter("Paused (Esc to resume)");
                break;

            case GameState.GameOver:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                SfxPlayer.Instance?.PlayGameOver();
                _gameOverUI?.ShowGameOver(_score);
                MusicPlayer.Instance?.PlayGameOver();
                break;

            case GameState.Cleared:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                _hud?.SetState("Cleared");
                //_hud?.ShowCenter("Level Cleared!");
                _ball?.ResetToPaddle();
                break;

            case GameState.Victory:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                _victoryUI?.ShowVictory(_score - _levelStartScore, _levelComboBonus, _levelBestCombo);
                MusicPlayer.Instance?.PlayLevelFinish();
                break;
        }
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
        _lives--;
        _hud?.SetLives(_lives);

        if (_lives <= 0)
        {
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

        _score           += points;
        _levelComboBonus += comboBonus;
        _hud?.SetScore(_score);
        _comboTimer = _comboResetSeconds;

        return points;
    }

    public void IncrementCombo()
    {
        _combo++;
        if (_combo > _levelBestCombo) _levelBestCombo = _combo;
        _hud?.SetCombo(_combo);
        _comboTimer = _comboResetSeconds;

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
        PowerupNotification.Instance?.Show("FURY STRIKE!", new Color(1f, 0.85f, 0.05f), isSpecial: true);
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
