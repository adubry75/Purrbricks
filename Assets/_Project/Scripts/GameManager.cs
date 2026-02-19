using System.Collections;
using UnityEngine;

public enum GameState
{
    Title,
    Ready,
    Playing,
    Cleared,
    Paused,
    GameOver,
    Win
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
    [SerializeField] private float _levelClearDelay = 3.0f;

    [Header("Refs")]
    [SerializeField] private LevelLoader _levelLoader;
    [SerializeField] private BallController _ball;
    [SerializeField] private PaddleController _paddle;
    [SerializeField] private HudController _hud;

    private int _currentLevelIndex = 0;
    private Coroutine _advanceRoutine;
    private bool _isAdvancingLevel;

    private float _comboTimer;
    private int _lives;

    [SerializeField] private GameState _state = GameState.Ready;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _lives = _startingLives;
        _hud?.SetLives(_lives);
        _hud?.SetScore(_score);

        LoadLevel(_currentLevelIndex);

        SetState(GameState.Ready);
    }

    private void Update()
    {
        if (_state == GameState.Playing && Input.GetKeyDown(KeyCode.Escape))
        {
            SetState(GameState.Paused);
        }
        else if (_state == GameState.Paused && Input.GetKeyDown(KeyCode.Escape))
        {
            SetState(GameState.Playing);
        }

        if (_state == GameState.Playing && _comboTimer > 0f)
        {
            _comboTimer -= Time.unscaledDeltaTime;
            if (_comboTimer <= 0f)
            {
                ResetCombo();
            }
        }

        if ((_state == GameState.GameOver || _state == GameState.Win) && Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
        }

        if (_state == GameState.Ready && Input.GetKeyDown(KeyCode.Space))
        {
            _ball.Launch();
            if (_hud != null) _hud.SetStatus("");
            SetState(GameState.Playing);
        }
    }

    private void RestartLevel()
    {
        Time.timeScale = 1f;

        _score = 0;
        _combo = 0;
        _comboTimer = 0f;
        _hud?.SetScore(_score);

        _lives = _startingLives;
        _hud?.SetLives(_lives);

        _ball?.ResetToPaddle();

        LoadLevel(_currentLevelIndex);

        SetState(GameState.Ready);
    }

    public void LoadLevel(int levelIndex)
    {
        _isAdvancingLevel = false;

        if (_levelIds == null || _levelIds.Length == 0)
        {
            Debug.LogError("No level IDs assigned in GameManager.");
            return;
        }

        _currentLevelIndex = Mathf.Clamp(levelIndex, 0, _levelIds.Length - 1);

        // Fallback: if the inspector slot wasn't filled, find it in the scene
        if (_levelLoader == null)
            _levelLoader = FindFirstObjectByType<LevelLoader>();

        if (_levelLoader == null)
        {
            Debug.LogError("GameManager: No LevelLoader found in the scene!");
            return;
        }

        _levelLoader.LoadLevel(_levelIds[_currentLevelIndex]);

        _ball.ResetToPaddle();
        _paddle.ResetPosition();

        if (_hud != null)
        {
            _hud.SetLevel(_currentLevelIndex + 1);
            _hud.SetStatus("Ready");
        }

        SetState(GameState.Ready);
    }

    public void SetState(GameState newState)
    {
        _state = newState;
        Debug.Log("GameState = " + _state);

        Time.timeScale = 1f;

        switch (_state)
        {
            case GameState.Ready:
                SetCursorPlayMode();
                _hud?.SetState("Ready");
                _hud?.ShowCenter("Press Space to Launch");
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
                _hud?.SetState("Game Over");
                _hud?.ShowCenter("Game Over (R to restart)");
                break;

            case GameState.Cleared:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                _hud?.SetState("Cleared");
                _hud?.ShowCenter("Level Cleared!");
                _ball?.ResetToPaddle();
                break;

            case GameState.Win:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                _hud?.SetState("Win");
                _hud?.ShowCenter("You Win! (R for next)");
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

    public void OnBallLost()
    {
        if (_state != GameState.Playing) return;

        SfxPlayer.Instance?.PlayLifeLost();
        _lives--;
        _hud.SetLives(_lives);
        Debug.Log("Ball lost. Lives = " + _lives);

        if (_lives <= 0)
        {
            SetState(GameState.GameOver);
            return;
        }

        if (_ball != null)
            _ball.ResetToPaddle();

        ResetCombo();
        SetState(GameState.Ready);
    }

    public void OnLevelCleared()
    {
        if (_isAdvancingLevel) return;
        if (_state == GameState.Cleared || _state == GameState.Win || _state == GameState.GameOver) return;

        _isAdvancingLevel = true;

        Debug.Log("Level cleared!");
        if (_advanceRoutine != null) StopCoroutine(_advanceRoutine);
        _advanceRoutine = StartCoroutine(AdvanceLevelRoutine());
    }

    private IEnumerator AdvanceLevelRoutine()
    {
        Debug.Log($"AdvanceLevelRoutine START, delay={_levelClearDelay}");

        SetState(GameState.Cleared);
        SfxPlayer.Instance?.PlayWin();

        yield return new WaitForSecondsRealtime(_levelClearDelay);

        int next = _currentLevelIndex + 1;

        if (next >= _levelIds.Length)
        {
            Time.timeScale = 1f;
            SetState(GameState.Win);
            Debug.Log("AdvanceLevelRoutine END -> WIN");
            yield break;
        }

        Time.timeScale = 1f;

        LoadLevel(next);

        Time.timeScale = 0f;
        _hud?.ShowCenter($"LEVEL {next + 1}");

        yield return new WaitForSecondsRealtime(0.75f);

        Time.timeScale = 1f;
        SetState(GameState.Ready);

        Debug.Log("AdvanceLevelRoutine END -> NEXT LEVEL READY");
    }

    public void AddScore(int basePoints)
    {
        int multiplier = 1 + _combo;
        int points = basePoints * multiplier;

        _score += points;
        _hud?.SetScore(_score);

        _comboTimer = _comboResetSeconds;
    }

    public void IncrementCombo()
    {
        _combo++;
        _hud?.SetCombo(_combo);
        _comboTimer = _comboResetSeconds;
    }

    public void ResetCombo()
    {
        _combo = 0;
        _hud?.SetCombo(_combo);
        _comboTimer = 0f;
    }
}
