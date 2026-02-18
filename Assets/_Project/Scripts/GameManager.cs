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
    [SerializeField] private LevelDefinition[] _levels;
    [SerializeField] private float _levelClearDelay = 3.0f;

    [Header("Refs")]
    [SerializeField] private BrickGridSpawner _spawner;
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
                // optional: update HUD later if you show combo
            }
        }

        if (_state == GameState.Win && Input.GetKeyDown(KeyCode.R))
        {
            int next = _currentLevelIndex + 1;
            if (next >= _levels.Length) next = 0;
            LoadLevel(next);
        }

        if (_state == GameState.Ready && Input.GetKeyDown(KeyCode.Space))
        {
            _ball.Launch();
            if (_hud != null) _hud.SetStatus("");
            SetState(GameState.Playing);
        }



        if ((_state == GameState.GameOver || _state == GameState.Win) && Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
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

        // Respawn bricks
        var spawner = FindFirstObjectByType<BrickGridSpawner>();
        if (spawner != null)
            spawner.Spawn();

        LoadLevel(_currentLevelIndex);

        SetState(GameState.Ready);
    }

    public void LoadLevel(int levelIndex)
    {
        _isAdvancingLevel = false;

        if (_levels == null || _levels.Length == 0)
        {
            Debug.LogError("No levels assigned in GameManager.");
            return;
        }

        _currentLevelIndex = Mathf.Clamp(levelIndex, 0, _levels.Length - 1);

        // Spawn bricks for this level
        _spawner.SetLevel(_levels[_currentLevelIndex]);
        _spawner.Spawn();

        // Reset ball + paddle to ready position
        _ball.ResetToPaddle();
        _paddle.ResetPosition();

        // Update HUD
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

        // Defaults
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
                // Freeze gameplay during the transition
                Time.timeScale = 0f;

                _hud?.SetState("Cleared");
                _hud?.ShowCenter("Level Cleared!");

                // Make sure ball is not moving during the pause
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
        Cursor.lockState = CursorLockMode.Confined; // best for multi-monitor
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

        // Reset ball to paddle and go back to "Title" style waiting-to-launch state
        if (_ball != null)
        {
            _ball.ResetToPaddle();
        }

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

        // Freeze + show cleared message
        SetState(GameState.Cleared);

        // Optional audio cue
        SfxPlayer.Instance?.PlayWin(); // If you don't have PlayWin(), comment this line out.

        // Wait while frozen (unscaled time)
        yield return new WaitForSecondsRealtime(_levelClearDelay);

        int next = _currentLevelIndex + 1;

        // If no more levels -> win screen
        if (next >= _levels.Length)
        {
            Time.timeScale = 1f;
            SetState(GameState.Win);
            Debug.Log("AdvanceLevelRoutine END -> WIN");
            yield break;
        }

        // Unfreeze before changing the world
        Time.timeScale = 1f;

        // Load next level (spawns bricks, resets ball/paddle, sets Ready)
        LoadLevel(next);

        // Short "Level X" splash before player launches
        Time.timeScale = 0f;
        _hud?.ShowCenter($"LEVEL {next + 1}");

        yield return new WaitForSecondsRealtime(0.75f);

        Time.timeScale = 1f;

        // Back to Ready (shows "Press Space to Launch")
        SetState(GameState.Ready);

        Debug.Log("AdvanceLevelRoutine END -> NEXT LEVEL READY");
    }



    public void AddScore(int basePoints)
    {
        // combo: 0 means x1, 1 means x2, etc (cap if you want later)
        int multiplier = 1 + _combo;
        int points = basePoints * multiplier;

        _score += points;
        _hud?.SetScore(_score);

        // refresh combo timer
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
