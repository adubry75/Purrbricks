using UnityEngine;

public class Brick : MonoBehaviour
{
    [SerializeField] private int _hitPoints = 1;

    public void Hit()
    {
        _hitPoints--;
        if (_hitPoints <= 0)
        {
            Debug.Log("Brick destroyed: " + name);
            LevelManager.Instance?.OnBrickDestroyed();
            Destroy(gameObject);
        }
    }
}
