using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        var ball = other.GetComponent<BallController>();
        if (ball != null)
        {
            GameManager.Instance?.OnBallLost();
        }
    }
}
