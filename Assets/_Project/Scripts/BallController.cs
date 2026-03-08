using UnityEngine;
using UnityEngine.InputSystem;

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

    private float _ignorePaddleBounceUntil;
    [SerializeField] private float _ignorePaddleBounceTime = 0.12f;

    private bool _launched;
    private bool _isSticky;
    private bool _isStickyHeld;     // caught by paddle, waiting for left click
    private Vector2 _stickyHoldOffset; // offset from paddle center when caught

    // ── Powerup flags ─────────────────────────────────────────────────────────
    private bool _isSpeedBoost;
    private bool _isFireball;       // pierces bricks with < 5 HP
    private bool _isBomb;           // explodes 3×3 area on brick hit
    private bool _isCursed;         // ball direction drifts sinusoidally
    private bool _isZipBall;        // forced 2.5× speed (bad powerup)
    private bool _isBigBall;        // ball scales to 2×
    private bool _isTinyBall;       // ball scales to 0.5×
    private bool _isInvisiBall;     // ball alpha 0.05, flashes every 3 s
    private bool _isGremlinBounces; // small random angle errors on paddle/wall bounces
    private float _curseTimer;
    private float _invisTimer;

    // Prism gate tint (gameplay color for locked bricks). This is separate from powerup visuals.
    private PrismColor _prismColor = PrismColor.None;
    public PrismColor PrismColor => _prismColor;

    // Fireball pierce — restore pre-bounce velocity in next FixedUpdate
    private bool _wantFireballPierce;
    private Vector2 _savedVelocity;

    // Pre-allocated buffer for bomb overlap queries — avoids heap allocation
    private static readonly Collider2D[] s_overlapBuffer = new Collider2D[64];

    private const float SPEED_MULTIPLIER = 2f;
    private const float ZIP_MULTIPLIER = 2.5f;

    // ── Pinball bumper speed burst ────────────────────────────────────────────
    // Multiplies the ball's current speed, then smoothly decays back to pre-hit speed.
    private const float BUMPER_MAX_MULTIPLIER = 2.0f;   // cap relative to normal EffectiveSpeed
    private const float BUMPER_DEFAULT_DURATION = 5.0f; // seconds to decay back
    private float _bumperMultiplier = 1.0f;       // current multiplier (>= 1)
    private float _bumperStartMultiplier = 1.0f;  // multiplier right after the latest bumper hit
    private float _bumperEndMultiplier = 1.0f;    // multiplier to decay back to (speed before the bumper hit)
    private float _bumperTimer = 0.0f;
    private float _bumperDuration = BUMPER_DEFAULT_DURATION;
    private bool _bumperActive;

    // ── Speed ramp (Fury Strike charge) ───────────────────────────────────────
    private const float RAMP_MAX = 2.0f;   // maximum speed multiplier
    public float RAMP_RATE = 0.015f; // multiplier gained per second while live
    private float _rampMultiplier = 1.0f;

    /// <summary>0 = no charge, 1 = Fury Strike ready.</summary>
    public float RampFraction => Mathf.Clamp01((_rampMultiplier - 1f) / (RAMP_MAX - 1f));

    public void ResetRamp() => _rampMultiplier = 1.0f;

    // ── Ball visuals ──────────────────────────────────────────────────────────
    private SpriteRenderer _ballSr;
    private TrailRenderer _ballTrail;

    // ── Aim system ────────────────────────────────────────────────────────────
    private GameObject _aimLineGO;
    private LineRenderer _aimLine;
    private bool _isAiming;
    private Vector2 _aimDir;
    private float _aimAngleDegrees;
    private PaddleController _paddleCtrl;

    // Cached gradient objects — rebuilt only when tint changes, not every frame
    private readonly Gradient _trailGradient = new Gradient();
    private readonly GradientColorKey[] _colorKeys = new GradientColorKey[2];
    private readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[2];
    private Color _lastTint = Color.clear;

    // Effective speed accounting for all active flags + ramp
    private float EffectiveSpeed
    {
        get
        {
            float s = _speed * _rampMultiplier;
            if (_isSpeedBoost) s *= SPEED_MULTIPLIER;
            if (_isZipBall) s *= ZIP_MULTIPLIER;
            return s;
        }
    }

    private float CurrentSpeed => EffectiveSpeed * _bumperMultiplier;

    /// <summary>
    /// Pinball bumper effect: doubles current ball speed (clamped to 2x normal EffectiveSpeed),
    /// then smoothly decays back to the speed it had immediately before this bumper hit.
    /// </summary>
    public void TriggerBumperBoost(float durationSeconds = BUMPER_DEFAULT_DURATION)
    {
        if (!_launched || _rb == null) return;

        float pre = _bumperMultiplier;
        float boosted = Mathf.Min(pre * 2.0f, BUMPER_MAX_MULTIPLIER);

        _bumperDuration = Mathf.Max(0.05f, durationSeconds);
        _bumperStartMultiplier = boosted;
        _bumperEndMultiplier = pre;
        _bumperTimer = 0.0f;
        _bumperActive = true;
        _bumperMultiplier = boosted;

        // Apply immediately so the hit feels snappy.
        if (_rb.linearVelocity.sqrMagnitude > 0.0001f)
            _rb.linearVelocity = _rb.linearVelocity.normalized * CurrentSpeed;
    }

    private void Reset()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Awake()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _launchDirection = _launchDirection.normalized;
        _aimDir = _launchDirection;
        _ballSr = GetComponent<SpriteRenderer>();
        _ballTrail = GetComponent<TrailRenderer>();

        SetupAimLine();
    }

    private void SetupAimLine()
    {
        _aimLineGO = new GameObject("AimLine");
        _aimLineGO.transform.SetParent(transform, false);

        _aimLine = _aimLineGO.AddComponent<LineRenderer>();
        _aimLine.positionCount = 2;
        _aimLine.startWidth = 0.06f;
        _aimLine.endWidth = 0.01f;
        _aimLine.useWorldSpace = true;
        _aimLine.sortingOrder = 10;

        var shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive
            _aimLine.material = mat;
        }

        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 1f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.7f, 0.1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f,   1f)
            }
        );
        _aimLine.colorGradient = grad;

        _aimLineGO.SetActive(false);
    }

    private void Update()
    {
        if (!_launched)
        {
            if (_paddle != null)
                transform.position = (Vector2)_paddle.position + _paddleOffset;

            // Aiming: only when GameManager is in Ready state (not demo mode etc.)
            // Block input while a tutorial popup is open so the dismiss click doesn't launch.
            var gm = GameManager.Instance;
            if ((gm == null || gm.State == GameState.Ready)
                && (TutorialManager.Instance == null || !TutorialManager.Instance.IsShowing))
                HandleAimInput();

            return;
        }

        // Sticky hold: waiting for Space to release
        if (_isStickyHeld)
        {
            if (_paddle != null)
                transform.position = (Vector2)_paddle.position + _stickyHoldOffset;

            if (WasLeftMousePressedThisFrame())
                ReleaseStickyHold();
        }

        // InvisiBall: tick flash timer for brief visibility pulse every 3 s
        if (_isInvisiBall)
            _invisTimer += Time.deltaTime;

        // Long Haul achievement: track how long the PRIMARY ball has been live
        var gm2 = GameManager.Instance;
        if (gm2 != null && gm2.IsPrimaryBall(this) && gm2.State == GameState.Playing)
            AchievementManager.Instance?.OnBallAliveUpdate(Time.deltaTime);

        UpdateBallColor();
    }

    private void UpdateBallColor()
    {
        // If a prism gate has affected the ball, keep that color locked until the ball is lost.
        // (Fury Strike ramp heat and other powerup tints shouldn't override it.)
        Color tint;
        if (_prismColor != PrismColor.None)
            tint = PrismColorUtil.ToUnityColor(_prismColor);
        else if (_isFireball)
            tint = new Color(1.0f, 0.45f, 0.0f);      // fire orange
        else if (_isBomb)
            tint = new Color(1.0f, 0.20f, 1.0f);      // magenta
        else if (_isSpeedBoost)
            tint = new Color(1.0f, 0.85f, 0.10f);     // gold
        else if (_isSticky)
            tint = new Color(0.70f, 0.20f, 1.0f);     // purple
        else if (_isCursed)
            tint = new Color(0.25f, 0.55f, 0.20f);    // murky green
        else if (_isZipBall)
            tint = new Color(0.35f, 0.90f, 0.10f);    // sickly green
        else
            tint = Color.white;

        // Blend toward ramp heat as charge builds (only when not prism-locked).
        float ramp = RampFraction;
        if (_prismColor == PrismColor.None && ramp > 0.6f && tint == Color.white)
        {
            float t = (ramp - 0.6f) / 0.4f;
            tint = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.1f), t);
        }

        // InvisiBall: mostly invisible but flashes for 0.25 s every 3 s
        if (_isInvisiBall)
        {
            float phase = _invisTimer % 3.0f;
            float alpha = (phase < 0.25f) ? 0.70f : 0.05f;
            tint = new Color(tint.r, tint.g, tint.b, alpha);
        }

        if (_ballSr != null)
            _ballSr.color = tint;

        // Only rebuild the trail gradient when the tint actually changed — avoids
        // allocating new Gradient/array objects every frame (major GC pressure).
        if (_ballTrail != null && tint != _lastTint)
        {
            _lastTint = tint;
            _colorKeys[0] = new GradientColorKey(tint, 0f);
            _colorKeys[1] = new GradientColorKey(new Color(tint.r * 0.4f, tint.g * 0.4f, tint.b * 0.4f), 1f);
            _alphaKeys[0] = new GradientAlphaKey(0.90f, 0f);
            _alphaKeys[1] = new GradientAlphaKey(0.00f, 1f);
            _trailGradient.SetKeys(_colorKeys, _alphaKeys);
            _ballTrail.colorGradient = _trailGradient;
        }
    }

    private void FixedUpdate()
    {
        if (!_launched || _isStickyHeld) return;

        // Speed ramp — charges while ball is live
        if (_rampMultiplier < RAMP_MAX)
            _rampMultiplier = Mathf.Min(RAMP_MAX, _rampMultiplier + RAMP_RATE * Time.fixedDeltaTime);

        // Fireball pierce: restore pre-bounce direction from last frame
        if (_wantFireballPierce)
        {
            _wantFireballPierce = false;
            _rb.linearVelocity = _savedVelocity.normalized * CurrentSpeed;
        }

        // Bumper boost: decay multiplier toward the pre-hit speed over time.
        if (_bumperActive)
        {
            _bumperTimer += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(_bumperTimer / _bumperDuration);
            float s = t * t * (3f - 2f * t); // SmoothStep
            _bumperMultiplier = Mathf.Lerp(_bumperStartMultiplier, _bumperEndMultiplier, s);

            if (t >= 1f)
            {
                _bumperMultiplier = _bumperEndMultiplier;
                _bumperActive = false;
            }
        }

        float currentSpeed = CurrentSpeed;

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

    private void HandleAimInput()
    {
        var mouse = Mouse.current;
        if (mouse == null)
        {
            if (_aimLineGO != null && _aimLineGO.activeSelf)
                _aimLineGO.SetActive(false);
            return;
        }

        var leftButton = mouse.leftButton;
        if (leftButton == null)
        {
            if (_aimLineGO != null && _aimLineGO.activeSelf)
                _aimLineGO.SetActive(false);
            return;
        }

        // Only begin aiming on a fresh press while we're in Ready.
        // This avoids a menu/UI click that is still being held during the scene/state
        // transition from "carrying" into aiming/launching.
        if (!_isAiming)
        {
            if (!leftButton.wasPressedThisFrame)
            {
                if (_aimLineGO != null && _aimLineGO.activeSelf)
                    _aimLineGO.SetActive(false);
                return;
            }

            // Freeze paddle so mouse can aim freely
            if (_paddleCtrl == null && _paddle != null)
                _paddleCtrl = _paddle.GetComponent<PaddleController>();
            _paddleCtrl?.SetFrozen(true);

            _aimAngleDegrees = 0f;
            _aimDir = Vector2.up;
            _isAiming = true;
        }

        if (leftButton.isPressed)
        {
            float deltaX = mouse.delta.ReadValue().x;
            float deltaDegrees = deltaX / Screen.width * 180f;
            _aimAngleDegrees = Mathf.Clamp(_aimAngleDegrees + deltaDegrees, -60f, 60f);
            float rad = _aimAngleDegrees * Mathf.Deg2Rad;
            _aimDir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

            if (_aimLineGO != null)
            {
                Vector2 origin = transform.position;
                _aimLineGO.SetActive(true);
                _aimLine.SetPosition(0, origin);
                _aimLine.SetPosition(1, origin + _aimDir * 2.8f);
            }
        }
        else if (leftButton.wasReleasedThisFrame)
        {
            if (_aimLineGO != null) _aimLineGO.SetActive(false);
            _launchDirection = _aimDir;
            _isAiming = false;
            Launch();
            WarpMouseToPaddleX();
            _paddleCtrl?.SetFrozen(false);
        }
        else
        {
            if (_aimLineGO != null && _aimLineGO.activeSelf)
                _aimLineGO.SetActive(false);
        }
    }

    private static bool WasLeftMousePressedThisFrame()
    {
        return Mouse.current?.leftButton?.wasPressedThisFrame ?? false;
    }

    private void WarpMouseToPaddleX()
    {
        if (_paddle == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // Paddle's x in screen coords
        float paddleScreenX = cam.WorldToScreenPoint(_paddle.position).x;

        // Keep current Y so we only "snap" horizontally
        float mouseY = mouse.position.ReadValue().y;

        mouse.WarpCursorPosition(new Vector2(paddleScreenX, mouseY));
    }


    public void Launch()
    {
        if (_launched) return;
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) return;

        _ignorePaddleBounceUntil = Time.time + _ignorePaddleBounceTime;
        _launched = true;
        _isStickyHeld = false;
        _rb.simulated = true;
        _rb.linearVelocity = _launchDirection * CurrentSpeed;
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
            _rb.linearVelocity = _rb.linearVelocity.normalized * CurrentSpeed;
    }

    public void SetZipBall(bool on)
    {
        _isZipBall = on;
        if (_launched && !_isStickyHeld && _rb != null)
            _rb.linearVelocity = _rb.linearVelocity.normalized * CurrentSpeed;
    }

    public void SetFireball(bool on) => _isFireball = on;

    public void SetBomb(bool on) => _isBomb = on;

    public void SetCursed(bool on)
    {
        _isCursed = on;
        if (!on) _curseTimer = 0f;
    }

    public void SetBigBall(bool on)
    {
        _isBigBall = on;
        if (on && _isTinyBall) _isTinyBall = false; // BigBall wins
        ApplyBallScale();
    }

    public void SetTinyBall(bool on)
    {
        _isTinyBall = on;
        if (on && _isBigBall) _isBigBall = false; // TinyBall wins
        ApplyBallScale();
    }

    public void SetInvisiBall(bool on)
    {
        _isInvisiBall = on;
        if (!on) _invisTimer = 0f;
    }

    public void SetGremlinBounces(bool on) => _isGremlinBounces = on;

    public void SetPrismColor(PrismColor color)
    {
        _prismColor = color;
        UpdateBallColor();
    }

    private void ApplyBallScale()
    {
        if (_isBigBall)
            transform.localScale = Vector3.one * 2f;
        else if (_isTinyBall)
            transform.localScale = Vector3.one * 0.5f;
        else
            transform.localScale = Vector3.one;
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

        cloneBall._launched = true;
        cloneBall._isSpeedBoost = _isSpeedBoost;
        cloneBall._isSticky = _isSticky;
        cloneBall._isFireball = _isFireball;
        cloneBall._isBomb = _isBomb;
        cloneBall._isCursed = _isCursed;
        cloneBall._isZipBall = _isZipBall;
        cloneBall._isBigBall = _isBigBall;
        cloneBall._isTinyBall = _isTinyBall;
        cloneBall._isInvisiBall = _isInvisiBall;
        cloneBall._isGremlinBounces = _isGremlinBounces;
        cloneBall._rampMultiplier = _rampMultiplier;
        cloneBall._bumperMultiplier = _bumperMultiplier;
        cloneBall._bumperStartMultiplier = _bumperStartMultiplier;
        cloneBall._bumperEndMultiplier = _bumperEndMultiplier;
        cloneBall._bumperTimer = _bumperTimer;
        cloneBall._bumperDuration = _bumperDuration;
        cloneBall._bumperActive = _bumperActive;
        cloneBall._prismColor = PrismColor.None;

        var cloneRb = cloneBall.GetComponent<Rigidbody2D>();
        if (cloneRb != null)
        {
            cloneRb.simulated = true;
            cloneRb.linearVelocity = newDir * cloneBall.CurrentSpeed;
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
            _rb.linearVelocity = dir * CurrentSpeed;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.gameObject.name == _paddleObjectName)
        {
            //Debug.Log($"PADDLE COLLISION at t={Time.time} vel={_rb.linearVelocity}");
            if (Time.time < _ignorePaddleBounceUntil)
                return;
        }


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
            if (!brick.CanBeHitByBall(this))
            {
                SfxPlayer.Instance?.PlayWallHit();
                return;
            }

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

        // GremlinBounces: introduce small random reflection errors on non-brick wall collisions.
        if (brick == null && _isGremlinBounces && _rb != null && collision.collider.CompareTag("Wall"))
        {
            _rb.linearVelocity = RotateDeg(_rb.linearVelocity, Random.Range(-7.0f, 7.0f));
        }
    }

    private void TriggerBombAt(Vector2 center, Brick source)
    {
        CameraShake.Instance?.Shake(0.22f, 0.35f);
        BrickParticleGenerator.SpawnBurst(center, new Color(1f, 0.5f, 0f), 35, true);

        // 3×3 area = ~4.4 wide × 1.8 tall for standard bricks
        int count = Physics2D.OverlapBox(center, new Vector2(4.4f, 1.8f), 0f, ContactFilter2D.noFilter, s_overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            var b = s_overlapBuffer[i].GetComponent<Brick>();
            if (b != null && b != source)
                b.Hit();
        }
    }

    private void HandlePaddleBounce(Collision2D collision)
    {
        float paddleWidth = collision.collider.bounds.size.x;
        float paddleCenterX = collision.collider.bounds.center.x;
        float hitX = collision.GetContact(0).point.x;

        float t = Mathf.Clamp((hitX - paddleCenterX) / (paddleWidth * 0.5f), -1f, 1f);
        float angle = t * _maxBounceAngleDegrees;
        float rad = angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)).normalized;

        Vector2 v = dir * CurrentSpeed;
        if (_isGremlinBounces)
            v = RotateDeg(v, Random.Range(-9.0f, 9.0f));
        _rb.linearVelocity = v;
    }

    private static Vector2 RotateDeg(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    public void ResetToPaddle()
    {
        _launched = false;
        _isStickyHeld = false;
        _isAiming = false;
        _aimDir = _launchDirection;
        _aimAngleDegrees = 0f;
        _paddleCtrl?.SetFrozen(false);
        if (_aimLineGO != null) _aimLineGO.SetActive(false);
        _isSticky = false;
        _isSpeedBoost = false;
        _isFireball = false;
        _isBomb = false;
        _isCursed = false;
        _isZipBall = false;
        _isBigBall = false;
        _isTinyBall = false;
        _isInvisiBall = false;
        _isGremlinBounces = false;
        _wantFireballPierce = false;
        _curseTimer = 0f;
        _invisTimer = 0f;
        _rampMultiplier = 1.0f;
        _prismColor = PrismColor.None;
        _bumperMultiplier = 1.0f;
        _bumperStartMultiplier = 1.0f;
        _bumperEndMultiplier = 1.0f;
        _bumperTimer = 0.0f;
        _bumperDuration = BUMPER_DEFAULT_DURATION;
        _bumperActive = false;

        transform.localScale = Vector3.one;
        if (_ballSr != null) _ballSr.color = Color.white;

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
