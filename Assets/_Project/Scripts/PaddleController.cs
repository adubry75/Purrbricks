using UnityEngine;

public class PaddleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private BoxCollider2D _playfieldBounds;

    [Header("Movement")]
    [SerializeField] private float _yLocked = -7f;
    [SerializeField] private float _smoothTime = 0.01f;

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
    private bool _isDemoMode;

    // Powerup state
    private bool _isWide;
    private bool _isLaser;
    private float _laserCooldown;

    private Vector3 _normalScale;
    private BoxCollider2D _col;

    private void Reset() { _camera = Camera.main; }

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

        float targetX;

        if (_isDemoMode)
        {
            var ball = FindFirstObjectByType<BallController>();
            targetX = ball != null ? ball.transform.position.x : 0f;
        }
        else
        {
            targetX = _camera.ScreenToWorldPoint(Input.mousePosition).x;
        }

        float halfWidth = GetHalfWidthWorld();
        float leftLimit  = _leftWall.bounds.max.x  + halfWidth + _wallPadding;
        float rightLimit = _rightWall.bounds.min.x - halfWidth - _wallPadding;

        targetX = Mathf.Clamp(targetX, leftLimit, rightLimit);

        float smoothTime = _isDemoMode ? _demoSmoothTime : _smoothTime;
        float newX = Mathf.SmoothDamp(transform.position.x, targetX, ref _velocityX, smoothTime);

        transform.position = new Vector3(newX, _yLocked, transform.position.z);

        // Laser: fire on left mouse button click (with cooldown to prevent spam)
        if (_isLaser && !_isDemoMode)
        {
            _laserCooldown -= Time.deltaTime;
            if (_laserCooldown <= 0f && Input.GetMouseButtonDown(0))
            {
                FireLasers();
                _laserCooldown = _laserFireRate;
            }
        }
    }

    // ── Powerup API ───────────────────────────────────────────────────────────

    public void SetWide(bool on)
    {
        _isWide = on;
        float xScale = on ? _normalScale.x * _widthMultiplier : _normalScale.x;
        transform.localScale = new Vector3(xScale, _normalScale.y, _normalScale.z);
    }

    public void SetLaser(bool on)
    {
        _isLaser = on;
        _laserCooldown = 0f; // fire immediately when activated
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
        var col = GetComponent<BoxCollider2D>();
        if (col != null) return col.bounds.extents.x;
        var r = GetComponent<Renderer>();
        if (r != null) return r.bounds.extents.x;
        return 0.5f;
    }
}
