using UnityEngine;

public class Brick : MonoBehaviour
{
    [SerializeField] private int _hitPoints = 1;
    [SerializeField] private int _points = 100;

    public void Hit()
    {
        _hitPoints--;
        if (_hitPoints <= 0)
        {
            SfxPlayer.Instance?.PlayBrickBreak();

            // Score + combo juice
            GameManager.Instance?.AddScore(_points);
            GameManager.Instance?.IncrementCombo();

            // Win tracking
            LevelManager.Instance?.OnBrickDestroyed();

            Destroy(gameObject);
        }
    }
}
