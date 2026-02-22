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

    // ── Zoom (last-brick drama) ──────────────────────────────────────────────
    private Camera _cam;
    private float _originalOrthoSize;
    private bool  _isZooming;
    private float _zoomTargetSize;
    private float _zoomSpeed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        _originalPos = transform.localPosition;
        _cam = GetComponent<Camera>();
        if (_cam != null) _originalOrthoSize = _cam.orthographicSize;
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
                transform.localPosition = _originalPos;
        }

        // Smooth zoom toward target ortho size
        if (_isZooming && _cam != null)
        {
            _cam.orthographicSize = Mathf.Lerp(
                _cam.orthographicSize, _zoomTargetSize,
                Time.unscaledDeltaTime * _zoomSpeed);
        }
    }

    /// <summary>Smoothly zoom in to <paramref name="multiplier"/> × original size (e.g. 0.65 = zoom in 35%).</summary>
    public void ZoomIn(float multiplier = 0.65f, float speed = 2.5f)
    {
        if (_cam == null) return;
        _zoomTargetSize = _originalOrthoSize * multiplier;
        _zoomSpeed      = speed;
        _isZooming      = true;
    }

    /// <summary>Instantly restore original ortho size and stop zooming.</summary>
    public void ResetZoom()
    {
        _isZooming = false;
        if (_cam != null) _cam.orthographicSize = _originalOrthoSize;
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
