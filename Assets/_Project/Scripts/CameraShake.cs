using UnityEngine;

/// <summary>
/// Screen shake effect for impact feedback.
/// Attach to Main Camera. Call Shake(intensity, duration) from anywhere.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3 _originalPos;
    private Quaternion _originalRot;
    private float _shakeIntensity;
    private float _shakeDuration;
    private float _shakeTimer;
    private Vector3 _shakeOffset;

    // ── Zoom (last-brick drama) ──────────────────────────────────────────────
    private Camera _cam;
    private float _originalOrthoSize;
    private bool  _isZooming;
    private float _zoomTargetSize;
    private float _zoomSpeed;

    // ── Flip screen (powerup) ───────────────────────────────────────────────
    private bool _isFlipped;

    // ── Drunk wobble (powerup) ───────────────────────────────────────────────
    private bool  _drunk;
    private float _drunkStrength = 1f;
    private float _drunkTimer;
    private Vector3 _drunkOffset;
    private float _drunkRotZ;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        _originalPos = transform.localPosition;
        _originalRot = transform.localRotation;
        _cam = GetComponent<Camera>();
        if (_cam != null) _originalOrthoSize = _cam.orthographicSize;
    }

    private void Update()
    {
        _shakeOffset = Vector3.zero;
        _drunkOffset = Vector3.zero;
        _drunkRotZ   = 0f;

        if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.deltaTime;

            // Decay intensity over time (ease-out)
            float currentIntensity = _shakeIntensity * (_shakeTimer / _shakeDuration);

            // Perlin-style smooth shake (feels better than pure random)
            float x = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * 2f;

            _shakeOffset = new Vector3(x, y, 0f) * currentIntensity;
        }

        if (_drunk)
        {
            _drunkTimer += Time.unscaledDeltaTime;
            float s = _drunkStrength;

            // Slow sway + faster micro-wobble.
            float swayX = Mathf.Sin(_drunkTimer * 0.85f) * 0.25f * s;
            float swayY = Mathf.Sin(_drunkTimer * 0.70f + 1.2f) * 0.12f * s;
            float wobX  = Mathf.Sin(_drunkTimer * 2.70f) * 0.06f * s;
            float wobY  = Mathf.Sin(_drunkTimer * 2.10f + 0.4f) * 0.05f * s;
            _drunkOffset = new Vector3(swayX + wobX, swayY + wobY, 0f);

            // Gentle tilt.
            _drunkRotZ = Mathf.Sin(_drunkTimer * 0.95f) * 1.65f * s
                       + Mathf.Sin(_drunkTimer * 2.15f) * 0.45f * s;
        }

        transform.localPosition = _originalPos + _shakeOffset + _drunkOffset;
        float flipRot = _isFlipped ? 180f : 0f;
        transform.localRotation = _originalRot * Quaternion.Euler(0f, 0f, _drunkRotZ + flipRot);

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

    /// <summary>Bad powerup: flips the camera 180° — everything is upside down.</summary>
    public void SetFlipScreen(bool on)
    {
        _isFlipped = on;
    }

    /// <summary>Bad powerup: persistent camera wobble/tilt using unscaled time.</summary>
    public void SetDrunk(bool on, float strength = 1f)
    {
        _drunk = on;
        _drunkStrength = Mathf.Clamp(strength, 0.25f, 2.0f);
        if (!on)
        {
            _drunkTimer = 0f;
            transform.localRotation = _originalRot;
        }
    }
}
