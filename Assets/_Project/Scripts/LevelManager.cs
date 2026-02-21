using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    private int _bricksRemaining;

    private void Awake()
    {
        Instance = this;
    }

    public void BeginLevel(int brickCount)
    {
        _bricksRemaining = brickCount;
    }

    public void OnBrickDestroyed()
    {
        _bricksRemaining--;

        if (_bricksRemaining <= 0)
            GameManager.Instance?.OnLevelCleared();
    }
}
