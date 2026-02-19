using UnityEngine;

/// <summary>
/// Screen shake effect for impact feedback.
/// Attach to Main Camera. Call Shake(intensity, duration) from anywhere.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3 _originalPos;
    private float _shakeIntensity;
    private float _shakeDuration;
    private float _shakeTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        _originalPos = transform.localPosition;
    }

    private void Update()
    {
        if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.deltaTime;

            // Decay intensity over time (ease-out)
            float currentIntensity = _shakeIntensity * (_shakeTimer / _shakeDuration);

            // Perlin-style smooth shake (feels better than pure random)
            float x = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * 2f;

            Vector3 offset = new Vector3(x, y, 0f) * currentIntensity;
            transform.localPosition = _originalPos + offset;

            if (_shakeTimer <= 0f)
            {
                transform.localPosition = _originalPos;
            }
        }
    }

    /// <summary>
    /// Triggers a camera shake.
    /// Intensity: 0.05 = subtle, 0.15 = medium, 0.3+ = strong
    /// </summary>
    public void Shake(float intensity, float duration = 0.2f)
    {
        // Allow stronger shakes to override weaker ones
        if (intensity > _shakeIntensity || _shakeTimer <= 0f)
        {
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTimer = duration;
        }
    }
}
