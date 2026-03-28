using UnityEngine;
using UnityEngine.InputSystem;

public class PaddleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private BoxCollider2D _playfieldBounds;

    [Header("Movement")]
    [SerializeField] private float _yLocked = -7f;
    [SerializeField] private float _smoothTime = 0.01f;
    [SerializeField] private float _gamepadPaddleSpeed = 12f;

    [Header("Demo Mode AI")]
    [SerializeField] private float _demoSmoothTime = 0.15f;

    [Header("Wide Paddle")]
    [SerializeField] private float _widthMultiplier = 1.5f;

    [Header("Laser")]
    [SerializeField] private GameObject _laserPrefab; // assigned at runtime if null
    [SerializeField] private float _laserFireRate = 0.35f;

    [SerializeField] private BoxCollider2D _leftWall;
    [SerializeField] private BoxCollider2D _rightWall;
    [SerializeField] private float _wallPadding = 0.02f;

    private float _velocityX;
    private bool _laserFiredThisFrame;
    private bool _isDemoMode;
    private bool _isFrozen;
    private float _frozenX;

    // Powerup state
    private bool _isWide;
    private bool _isLaser;
    private bool _isShrunk;
    private bool _isFlipped;
    private bool _isDrunk;
    private float _drunkTimer;
    private float _laserCooldown;

    private Vector3 _normalScale;
    private BoxCollider2D _col;

    private void Reset() { _camera = Camera.main; }

    private void OnEnable()
    {
        if (InputManager.Actions != null)
            InputManager.Actions.Gameplay.FireLaser.performed += OnFireLaserPerformed;
    }

    private void OnDisable()
    {
        if (InputManager.Actions != null)
            InputManager.Actions.Gameplay.FireLaser.performed -= OnFireLaserPerformed;
    }

    private void OnFireLaserPerformed(InputAction.CallbackContext ctx)
    {
        _laserFiredThisFrame = true;
    }

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
        _col = GetComponent<BoxCollider2D>();
        _normalScale = transform.localScale;
    }

    private void Update()
    {
        if (_camera == null) return;
        if (_leftWall == null || _rightWall == null) return;

        if (_isFrozen)
        {
            transform.position = new Vector3(_frozenX, _yLocked, transform.position.z);
            return;
        }

        float targetX;

        if (_isDemoMode)
        {
            var ball = FindFirstObjectByType<BallController>();
            targetX = ball != null ? ball.transform.position.x : 0f;
        }
        else if (InputManager.CurrentScheme == InputScheme.Gamepad)
        {
            float stickX = InputManager.Actions?.Gameplay.MovePaddle.ReadValue<float>() ?? 0f;
            if (_isFlipped) stickX = -stickX;
            targetX = transform.position.x + stickX * _gamepadPaddleSpeed * Time.deltaTime;
        }
        else
        {
            // MouseKeyboard: read screen X from Mouse.current, convert to world space
            var mousePos = Mouse.current?.position.ReadValue()
                           ?? (Vector2)UnityEngine.Input.mousePosition;
            float mouseX = _camera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, _camera.nearClipPlane)).x;
            targetX = _isFlipped ? -mouseX : mouseX;
        }

        // DrunkenPaddle: sinusoidal sway (±2.5 units at ~0.4 Hz cycle)
        if (_isDrunk)
        {
            _drunkTimer += Time.deltaTime;
            targetX += Mathf.Sin(_drunkTimer * 2.5f) * 2.5f;
        }

        float halfWidth = GetHalfWidthWorld();
        float leftLimit  = _leftWall.bounds.max.x  + halfWidth + _wallPadding;
        float rightLimit = _rightWall.bounds.min.x - halfWidth - _wallPadding;

        targetX = Mathf.Clamp(targetX, leftLimit, rightLimit);

        float smoothTime = _isDemoMode ? _demoSmoothTime : _smoothTime;
        float newX = Mathf.SmoothDamp(transform.position.x, targetX, ref _velocityX, smoothTime);

        transform.position = new Vector3(newX, _yLocked, transform.position.z);

        // Laser: only fire during active gameplay (prevents UI clicks like "Next Level"
        // from spawning lasers on the next level).
        bool canShoot = GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
        if (_isLaser && !_isDemoMode && canShoot)
        {
            _laserCooldown -= Time.deltaTime;
            if (_laserCooldown <= 0f && _laserFiredThisFrame)
            {
                FireLasers();
                _laserCooldown = _laserFireRate;
            }
        }
        _laserFiredThisFrame = false;
    }

    // ── Powerup API ───────────────────────────────────────────────────────────

    private void ApplyPaddleScale()
    {
        float xScale = _normalScale.x;
        if (_isWide)   xScale *= _widthMultiplier;
        if (_isShrunk) xScale *= 0.5f;
        transform.localScale = new Vector3(xScale, _normalScale.y, _normalScale.z);
    }

    public void SetWide(bool on)
    {
        _isWide = on;
        ApplyPaddleScale();
    }

    public void SetShrink(bool on)
    {
        _isShrunk = on;
        ApplyPaddleScale();
    }

    public void SetFlipped(bool on)
    {
        _isFlipped = on;
    }

    public void SetDrunk(bool on)
    {
        _isDrunk = on;
        if (!on) _drunkTimer = 0f;
    }

    public void SetLaser(bool on)
    {
        _isLaser = on;
        _laserCooldown = 0f;
    }

    public void SetFrozen(bool frozen)
    {
        _isFrozen = frozen;
        if (frozen)
        {
            _frozenX = transform.position.x;
        }
        
    }

    public void SetDemoMode(bool isDemoMode)
    {
        _isDemoMode = isDemoMode;
    }

    public void ResetPosition()
    {
        transform.position = new Vector3(0f, _yLocked, transform.position.z);
    }

    // ── Laser firing ──────────────────────────────────────────────────────────

    private void FireLasers()
    {
        if (_isWide)
        {
            // Double lasers - one from each side of the paddle
            float hw = GetHalfWidthWorld() * 0.6f;
            SpawnLaser(transform.position + Vector3.left  * hw);
            SpawnLaser(transform.position + Vector3.right * hw);
        }
        else
        {
            // Single laser from center
            SpawnLaser(transform.position);
        }

        SfxPlayer.Instance?.PlayLaser();
    }

    private void SpawnLaser(Vector3 position)
    {
        var go = new GameObject("Laser");
        // Spawn well above the paddle and ball to avoid instant self-collision
        go.transform.position = position + Vector3.up * 0.7f;
        go.AddComponent<LaserProjectile>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private float GetHalfWidthWorld()
    {
        if (_col != null) return _col.bounds.extents.x;
        var r = GetComponent<Renderer>();
        if (r != null) return r.bounds.extents.x;
        return 0.5f;
    }
}
