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
    private Vector2 _stickyHoldOffset; // offset from paddle center when caught

    // ── Powerup flags ─────────────────────────────────────────────────────────
    private bool _isSpeedBoost;
    private bool _isFireball;       // pierces bricks with < 5 HP
    private bool _isBomb;           // explodes 3×3 area on brick hit
    private bool _isCursed;         // ball direction drifts sinusoidally
    private bool _isZipBall;        // forced 2.5× speed (bad powerup)
    private float _curseTimer;

    // Fireball pierce — restore pre-bounce velocity in next FixedUpdate
    private bool _wantFireballPierce;
    private Vector2 _savedVelocity;

    private const float SPEED_MULTIPLIER = 2f;
    private const float ZIP_MULTIPLIER   = 2.5f;

    // Effective speed accounting for all active flags
    private float EffectiveSpeed
    {
        get
        {
            float s = _speed;
            if (_isSpeedBoost) s *= SPEED_MULTIPLIER;
            if (_isZipBall)    s *= ZIP_MULTIPLIER;
            return s;
        }
    }

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
            if (_paddle != null)
                transform.position = (Vector2)_paddle.position + _stickyHoldOffset;

            if (Input.GetKeyDown(KeyCode.Space))
                ReleaseStickyHold();
        }
    }

    private void FixedUpdate()
    {
        if (!_launched || _isStickyHeld) return;

        // Fireball pierce: restore pre-bounce direction from last frame
        if (_wantFireballPierce)
        {
            _wantFireballPierce = false;
            _rb.linearVelocity = _savedVelocity.normalized * EffectiveSpeed;
        }

        float currentSpeed = EffectiveSpeed;

        // Keep speed constant
        _rb.linearVelocity = _rb.linearVelocity.normalized * currentSpeed;

        // Cursed ball: oscillating directional drift
        if (_isCursed)
        {
            _curseTimer += Time.fixedDeltaTime * 2f;
            float rotDeg = Mathf.Sin(_curseTimer) * 4.5f;
            float rad = rotDeg * Mathf.Deg2Rad;
            Vector2 v = _rb.linearVelocity.normalized;
            v = new Vector2(
                v.x * Mathf.Cos(rad) - v.y * Mathf.Sin(rad),
                v.x * Mathf.Sin(rad) + v.y * Mathf.Cos(rad)
            );
            _rb.linearVelocity = v * currentSpeed;
        }

        Vector2 dir = _rb.linearVelocity.normalized;

        if (Mathf.Abs(dir.x) < _minHorizontal)
        {
            dir.x = Mathf.Sign(dir.x == 0 ? Random.Range(-1f, 1f) : dir.x) * _minHorizontal;
            dir = dir.normalized;
            _rb.linearVelocity = dir * currentSpeed;
        }

        if (Mathf.Abs(dir.y) < _minVertical)
        {
            dir.y = Mathf.Sign(dir.y == 0 ? 1f : dir.y) * _minVertical;
            dir = dir.normalized;
            _rb.linearVelocity = dir * currentSpeed;
        }

        // Save velocity for potential fireball pierce next frame
        _savedVelocity = _rb.linearVelocity;
    }

    public void Launch()
    {
        if (_launched) return;

        _launched = true;
        _isStickyHeld = false;
        _rb.simulated = true;
        _rb.linearVelocity = _launchDirection * EffectiveSpeed;
    }

    public bool IsLaunched() => _launched;

    // ── Powerup API ───────────────────────────────────────────────────────────

    public void SetSticky(bool on)
    {
        _isSticky = on;
        if (!on && _isStickyHeld)
            ReleaseStickyHold();
    }

    public void SetSpeedBoost(bool on)
    {
        _isSpeedBoost = on;
        if (_launched && !_isStickyHeld && _rb != null)
            _rb.linearVelocity = _rb.linearVelocity.normalized * EffectiveSpeed;
    }

    public void SetZipBall(bool on)
    {
        _isZipBall = on;
        if (_launched && !_isStickyHeld && _rb != null)
            _rb.linearVelocity = _rb.linearVelocity.normalized * EffectiveSpeed;
    }

    public void SetFireball(bool on) => _isFireball = on;

    public void SetBomb(bool on) => _isBomb = on;

    public void SetCursed(bool on)
    {
        _isCursed = on;
        if (!on) _curseTimer = 0f;
    }

    /// <summary>Spawns a clone of this ball rotated by angleOffset degrees.</summary>
    public void SpawnClone(float angleOffset)
    {
        if (!_launched) return;

        var clone = Instantiate(gameObject, transform.position, Quaternion.identity);
        var cloneBall = clone.GetComponent<BallController>();
        if (cloneBall == null) return;

        Vector2 currentDir = _rb.linearVelocity.normalized;
        float rad = angleOffset * Mathf.Deg2Rad;
        Vector2 newDir = new Vector2(
            currentDir.x * Mathf.Cos(rad) - currentDir.y * Mathf.Sin(rad),
            currentDir.x * Mathf.Sin(rad) + currentDir.y * Mathf.Cos(rad)
        ).normalized;

        cloneBall._launched     = true;
        cloneBall._isSpeedBoost = _isSpeedBoost;
        cloneBall._isSticky     = _isSticky;
        cloneBall._isFireball   = _isFireball;
        cloneBall._isBomb       = _isBomb;
        cloneBall._isCursed     = _isCursed;
        cloneBall._isZipBall    = _isZipBall;

        var cloneRb = cloneBall.GetComponent<Rigidbody2D>();
        if (cloneRb != null)
        {
            cloneRb.simulated = true;
            cloneRb.linearVelocity = newDir * cloneBall.EffectiveSpeed;
        }

        GameManager.Instance?.RegisterClone();
    }

    private void ReleaseStickyHold()
    {
        _isStickyHeld = false;
        _rb.simulated = true;

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
            _rb.linearVelocity = dir * EffectiveSpeed;
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
        {
            // Fireball: pierce through bricks with < 5 HP
            if (_isFireball && brick.CurrentHitPoints < 5 && !brick.IsIndestructible)
            {
                brick.Hit();
                _wantFireballPierce = true;
                return;
            }

            brick.Hit();

            // Bomb: detonate a 3×3 area around the hit brick
            if (_isBomb)
                TriggerBombAt(brick.transform.position, brick);
        }
    }

    private void TriggerBombAt(Vector2 center, Brick source)
    {
        CameraShake.Instance?.Shake(0.22f, 0.35f);
        BrickParticleGenerator.SpawnBurst(center, new Color(1f, 0.5f, 0f), 35, true);

        // 3×3 area = ~4.4 wide × 1.8 tall for standard bricks
        var overlaps = Physics2D.OverlapBoxAll(center, new Vector2(4.4f, 1.8f), 0f);
        foreach (var col in overlaps)
        {
            var b = col.GetComponent<Brick>();
            if (b != null && b != source)
                b.Hit();
        }
    }

    private void HandlePaddleBounce(Collision2D collision)
    {
        float paddleWidth   = collision.collider.bounds.size.x;
        float paddleCenterX = collision.collider.bounds.center.x;
        float hitX          = collision.GetContact(0).point.x;

        float t = Mathf.Clamp((hitX - paddleCenterX) / (paddleWidth * 0.5f), -1f, 1f);
        float angle = t * _maxBounceAngleDegrees;
        float rad   = angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)).normalized;

        _rb.linearVelocity = dir * EffectiveSpeed;
    }

    public void ResetToPaddle()
    {
        _launched           = false;
        _isStickyHeld       = false;
        _isSticky           = false;
        _isSpeedBoost       = false;
        _isFireball         = false;
        _isBomb             = false;
        _isCursed           = false;
        _isZipBall          = false;
        _wantFireballPierce = false;
        _curseTimer         = 0f;

        if (_rb != null)
        {
            _rb.linearVelocity  = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.simulated       = false;
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
