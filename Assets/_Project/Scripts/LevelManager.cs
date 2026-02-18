using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    private int _bricksRemaining;

    private void Awake()
    {
        Instance = this;
        Debug.Log("LevelManager Awake");
    }

    public void BeginLevel(int brickCount)
    {
        _bricksRemaining = brickCount;
        Debug.Log($"BeginLevel -> bricks = {_bricksRemaining}");
    }

    public void OnBrickDestroyed()
    {
        _bricksRemaining--;
        Debug.Log($"OnBrickDestroyed -> {_bricksRemaining}");

        if (_bricksRemaining <= 0)
        {
            Debug.Log("Bricks hit zero. Calling OnLevelCleared().");
            GameManager.Instance?.OnLevelCleared();

        }
    }
}
