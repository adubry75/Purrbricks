using UnityEngine;

public enum GameState
{
    Ready,
    Playing,
    Paused,
    GameOver,
    Win
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Round Settings")]
    [SerializeField] private int _startingLives = 3;

    [Header("References")]
    [SerializeField] private BallController _ball;

    [SerializeField] private HudController _hud;

    [Header("Scoring")]
    [SerializeField] private int _score;
    [SerializeField] private int _combo;
    [SerializeField] private float _comboResetSeconds = 22.0f;

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

        SetState(GameState.Ready);
    }

    private void Update()
    {
        if (_state == GameState.Ready && Input.GetKeyDown(KeyCode.Space))
        {
            SetState(GameState.Playing);
            _ball?.Launch();
        }

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
        if (_state != GameState.Playing) return;

        if (_ball != null)
            _ball.ResetToPaddle();
        
        SfxPlayer.Instance?.PlayWin();

        Debug.Log("Level cleared!");
        SetState(GameState.Win);

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
