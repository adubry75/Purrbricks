using UnityEngine;

public enum GameState
{
    Title,
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

    private int _lives;


    [SerializeField] private GameState _state = GameState.Title;

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
        SetState(GameState.Title);
    }

    private void Update()
    {
        // Temporary: press Space to start playing
        if (_state == GameState.Title && Input.GetKeyDown(KeyCode.Space))
        {
            SetState(GameState.Playing);
        }

        // Temporary: press Escape to toggle pause
        if (_state == GameState.Playing && Input.GetKeyDown(KeyCode.Escape))
        {
            SetState(GameState.Paused);
        }
        else if (_state == GameState.Paused && Input.GetKeyDown(KeyCode.Escape))
        {
            SetState(GameState.Playing);
        }
    }

    public void SetState(GameState newState)
    {
        _state = newState;

        switch (_state)
        {
            case GameState.Title:
                SetCursorMenuMode();
                Time.timeScale = 1f;
                break;

            case GameState.Playing:
                SetCursorPlayMode();
                Time.timeScale = 1f;
                break;

            case GameState.Paused:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                break;

            case GameState.GameOver:
                SetCursorMenuMode();
                Time.timeScale = 1f;
                break;

            case GameState.Win:
                SetCursorMenuMode();
                Time.timeScale = 0f;
                break;
        }

        Debug.Log("GameState = " + _state);
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

        _lives--;
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

        SetState(GameState.Title);
    }

    public void OnLevelCleared()
    {
        if (_state != GameState.Playing) return;

        Debug.Log("Level cleared!");
        SetState(GameState.Win);

        if (_ball != null)
            _ball.ResetToPaddle();
    }

}
