using UnityEngine;

public abstract class PowerupBase : MonoBehaviour
{
    public float duration;

    public abstract void Apply(GameManager gm, PaddleController paddle, BallController[] balls);
    public abstract void Remove();
}
