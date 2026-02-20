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
        }

        // Debug hotkey: clear all but 1 brick
        if (Input.GetKeyDown(KeyCode.K) && _state == GameState.Playing)
            ClearAllButOneBrick();

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

        if (_state == GameState.Ready && Input.GetKeyDown(KeyCode.Space))
        {
            _ball.Launch();
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

        // Show level number in status
        _hud?.SetStatus($"Level {_currentLevelIndex + 1} - Ready");
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
        Debug.Log("GameState = " + _state);

        Time.timeScale = 1f;

        switch (_state)
        {
            case GameState.MainMenu:
                SetCursorMenuMode();
                _hud?.gameObject.SetActive(false);
                SfxPlayer.Instance?.MuteAll(true);
                break;

            case GameState.HighScores:
                SetCursorMenuMode();
                _hud?.gameObject.SetActive(false);
                SfxPlayer.Instance?.MuteAll(true);
                break;

            case GameState.Ready:
                SetCursorPlayMode();
                _hud?.gameObject.SetActive(true);
                _hud?.SetState("Ready");
                _hud?.ShowCenter("Press Space to Launch");
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
                break;

            case GameState.Cleared:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                _hud?.SetState("Cleared");
                _hud?.ShowCenter("Level Cleared!");
                _ball?.ResetToPaddle();
                break;

            case GameState.Victory:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                _victoryUI?.ShowVictory(_score);
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

    private IEnumerator AdvanceLevelRoutine()
    {
        // Skip "Cleared" state, go straight to Victory
        SfxPlayer.Instance?.PlayWin();
        Time.timeScale = 1f;
        SetState(GameState.Victory);
        yield break;
    }

    // ── Scoring ─────────────────────────────────────────────────────────────

    public int AddScore(int basePoints)
    {
        int multiplier = 1 + _combo;
        int points = basePoints * multiplier;

        _score += points;
        _hud?.SetScore(_score);
        _comboTimer = _comboResetSeconds;

        return points;
    }

    public void IncrementCombo()
    {
        _combo++;
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

    private void ClearAllParticles()
    {
        var particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        foreach (var ps in particles)
        {
            if (ps != null)
                Destroy(ps.gameObject);
        }
    }
}
