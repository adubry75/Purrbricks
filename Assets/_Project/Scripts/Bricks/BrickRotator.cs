using UnityEngine;

/// <summary>
/// Added at runtime to bricks that have a "rotation" field in their level JSON.
/// Rotates the brick (or a pivot/root object) around Z so angled hits deflect differently.
/// </summary>
public class BrickRotator : MonoBehaviour
{
    private float _speedDegPerSec = 180f;

    public void Init(BrickRotation data)
    {
        if (data == null) return;
        _speedDegPerSec = data.speed;

        if (Mathf.Abs(data.startAngle) > 0.001f)
            transform.rotation = Quaternion.Euler(0f, 0f, data.startAngle);
    }

    private void Update()
    {
        if (Mathf.Abs(_speedDegPerSec) < 0.001f) return;
        transform.Rotate(0f, 0f, _speedDegPerSec * Time.deltaTime, Space.Self);
    }
}

