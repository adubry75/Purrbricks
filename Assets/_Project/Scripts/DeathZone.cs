using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        var ball = other.GetComponent<BallController>();
        if (ball == null) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.IsPrimaryBall(ball))
        {
            // Primary ball lost — GameManager decides whether to lose a life
            // or keep playing if clones are still active
            gm.OnPrimaryBallLost();
        }
        else
        {
            // Clone ball — just remove it; inform manager so it can
            // check if the primary ball also needs a reset
            Destroy(ball.gameObject);
            gm.OnCloneBallLost();
        }
    }
}
