using UnityEngine;

/// <summary>
/// Added at runtime to bricks that have a "movement" field in their level JSON.
/// Oscillates the brick in horizontal, vertical, or circular patterns.
/// </summary>
public class BrickMover : MonoBehaviour
{
    private string _type = "horizontal";
    private float _amplitude = 1.5f;
    private float _period = 2.5f;
    private float _phaseOffset = 0f;

    private Vector3 _origin;
    private float _time;

    public void Init(BrickMovement data)
    {
        _type = data.type ?? "horizontal";
        _amplitude = data.amplitude;
        _period = data.period;
        _phaseOffset = data.phaseOffset;
    }

    private void Start()
    {
        _origin = transform.position;
        _time = _phaseOffset; // start at the specified phase
    }

    private void Update()
    {
        _time += Time.deltaTime;
        float t = _time * (2f * Mathf.PI / Mathf.Max(0.01f, _period));

        switch (_type)
        {
            case "vertical":
                transform.position = _origin + new Vector3(0f, Mathf.Sin(t) * _amplitude, 0f);
                break;
            case "circular":
                transform.position = _origin + new Vector3(
                    Mathf.Sin(t) * _amplitude,
                    Mathf.Cos(t) * _amplitude,
                    0f);
                break;
            default: // "horizontal"
                transform.position = _origin + new Vector3(Mathf.Sin(t) * _amplitude, 0f, 0f);
                break;
        }
    }
}
