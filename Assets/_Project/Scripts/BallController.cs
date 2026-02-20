using UnityEngine;

public class BallController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _paddle;
    [SerializeField] private Rigidbody2D _rb;

    [Header("Launch")]
    [SerializeField] private float _speed = 8.5f;
    [SerializeField] private Vector2 _launchDirection = new Vector2(0.35f, 0.94f);

    [Header("Follow Paddle")]
    [SerializeField] private Vector2 _paddleOffset = new Vector2(0f, 0.45f);

    [Header("Anti-Boring")]
    [SerializeField] private float _minVertical = 0.25f;
    [SerializeField] private float _minHorizontal = 0.10f;

    [Header("Paddle Aim Bounce")]
    [SerializeField] private float _maxBounceAngleDegrees = 75f;
    [SerializeField] private string _paddleObjectName = "Paddle";

    private bool _launched;
    private bool _isSticky;
    private bool _isStickyHeld;     // caught by paddle, waiting for space
    private bool _isSpeedBoost;
    private Vector2 _stickyHoldOffset; // offset from paddle center when caught

    private const float SPEED_MULTIPLIER = 2f;

    private void Reset()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Awake()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        _launchDirection = _launchDirection.normalized;
    }

    private void Update()
    {
        if (!_launched)
        {
            if (_paddle != null)
                transform.position = (Vector2)_paddle.position + _paddleOffset;

            if (Input.GetKeyDown(KeyCode.Space))
                Launch();

            return;
        }

        // Sticky hold: waiting for Space to release
        if (_isStickyHeld)
        {
            // Maintain exact catch position relative to paddle (not forced to center)
            if (_paddle != null)
                transform.position = (Vector2)_paddle.position + _stickyHoldOffset;

            if (Input.GetKeyDown(KeyCode.Space))
                ReleaseStickyHold();
        }
    }

    private void FixedUpdate()
    {
        if (!_launched || _isStickyHeld) return;

        float currentSpeed = _isSpeedBoost ? _speed * SPEED_MULTIPLIER : _speed;

        // Keep speed constant
        _rb.linearVelocity = _rb.linearVelocity.normalized * currentSpeed;

        Vector2 v = _rb.linearVelocity.normalized;

        if (Mathf.Abs(v.x) < _minHorizontal)
        {
            v.x = Mathf.Sign(v.x == 0 ? Random.Range(-1f, 1f) : v.x) * _minHorizontal;
            v = v.normalized;
            _rb.linearVelocity = v * currentSpeed;
        }

        if (Mathf.Abs(v.y) < _minVertical)
        {
            v.y = Mathf.Sign(v.y == 0 ? 1f : v.y) * _minVertical;
            v = v.normalized;
            _rb.linearVelocity = v * currentSpeed;
        }
    }

    public void Launch()
    {
        if (_launched) return;

        _launched = true;
        _isStickyHeld = false;
        _rb.simulated = true;
        _rb.linearVelocity = _launchDirection * (_isSpeedBoost ? _speed * SPEED_MULTIPLIER : _speed);
    }

    public bool IsLaunched() => _launched;

    // ── Powerup API ───────────────────────────────────────────────────────────

    public void SetSticky(bool on)
    {
        _isSticky = on;
        // If turning off while held, release
        if (!on && _isStickyHeld)
            ReleaseStickyHold();
    }

    public void SetSpeedBoost(bool on)
    {
        _isSpeedBoost = on;
        // Immediately adjust velocity if already launched
        if (_launched && !_isStickyHeld && _rb != null)
        {
            float newSpeed = on ? _speed * SPEED_MULTIPLIER : _speed;
            _rb.linearVelocity = _rb.linearVelocity.normalized * newSpeed;
        }
    }

    /// <summary>
    /// Spawns a clone of this ball rotated by angleOffset degrees.
    /// Called by PowerupManager for Multi-Ball.
    /// </summary>
    public void SpawnClone(float angleOffset)
    {
        if (!_launched) return;

        var clone = Instantiate(gameObject, transform.position, Quaternion.identity);
        var cloneBall = clone.GetComponent<BallController>();
        if (cloneBall == null) return;

        // Rotate current velocity by the offset angle
        Vector2 currentDir = _rb.linearVelocity.normalized;
        float rad = angleOffset * Mathf.Deg2Rad;
        Vector2 newDir = new Vector2(
            currentDir.x * Mathf.Cos(rad) - currentDir.y * Mathf.Sin(rad),
            currentDir.x * Mathf.Sin(rad) + currentDir.y * Mathf.Cos(rad)
        ).normalized;

        cloneBall._launched = true;
        cloneBall._isSpeedBoost = _isSpeedBoost;
        cloneBall._isSticky = _isSticky;

        var cloneRb = cloneBall.GetComponent<Rigidbody2D>();
        if (cloneRb != null)
        {
            cloneRb.simulated = true;
            float spd = _isSpeedBoost ? _speed * SPEED_MULTIPLIER : _speed;
            cloneRb.linearVelocity = newDir * spd;
        }

        // Tell GameManager a clone is now live so it can track count accurately
        GameManager.Instance?.RegisterClone();
    }

    private void ReleaseStickyHold()
    {
        _isStickyHeld = false;
        _rb.simulated = true;

        // Launch upward from current position
        if (_paddle != null)
        {
            float paddleCenterX = _paddle.position.x;
            float hitX = transform.position.x;
            var col = _paddle.GetComponent<Collider2D>();
            float paddleWidth = col != null ? col.bounds.size.x : 1f;

            float t = Mathf.Clamp((hitX - paddleCenterX) / (paddleWidth * 0.5f), -1f, 1f);
            float angle = t * _maxBounceAngleDegrees;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)).normalized;
            float spd = _isSpeedBoost ? _speed * SPEED_MULTIPLIER : _speed;
            _rb.linearVelocity = dir * spd;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Sticky catch
        if (_isSticky && collision.collider.gameObject.name == _paddleObjectName && !_isStickyHeld)
        {
            _isStickyHeld = true;
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
            // Record where on the paddle we landed (X offset from center, Y from paddleOffset)
            _stickyHoldOffset = new Vector2(
                transform.position.x - _paddle.position.x,
                _paddleOffset.y
            );
            SfxPlayer.Instance?.PlayPaddleHit();
            return;
        }

        // Normal paddle bounce
        if (collision.collider.gameObject.name == _paddleObjectName)
        {
            SfxPlayer.Instance?.PlayPaddleHit();
            HandlePaddleBounce(collision);
            return;
        }

        if (collision.collider.CompareTag("Wall"))
            SfxPlayer.Instance?.PlayWallHit();

        var brick = collision.collider.GetComponent<Brick>();
        if (brick != null)
            brick.Hit();
    }

    private void HandlePaddleBounce(Collision2D collision)
    {
        float paddleWidth = collision.collider.bounds.size.x;
        float paddleCenterX = collision.collider.bounds.center.x;
        float hitX = collision.GetContact(0).point.x;

        float t = (hitX - paddleCenterX) / (paddleWidth * 0.5f);
        t = Mathf.Clamp(t, -1f, 1f);

        float angle = t * _maxBounceAngleDegrees;
        float rad = angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)).normalized;

        float spd = _isSpeedBoost ? _speed * SPEED_MULTIPLIER : _speed;
        _rb.linearVelocity = dir * spd;
    }

    public void ResetToPaddle()
    {
        _launched = false;
        _isStickyHeld = false;
        _isSticky = false;
        _isSpeedBoost = false;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.simulated = false;
        }

        if (_paddle == null)
        {
            var paddleCtrl = FindFirstObjectByType<PaddleController>();
            if (paddleCtrl != null) _paddle = paddleCtrl.transform;
        }

        if (_paddle != null)
            transform.position = (Vector2)_paddle.position + _paddleOffset;
    }
}
