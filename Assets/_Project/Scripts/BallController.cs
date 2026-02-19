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
    [SerializeField] private float _minVertical = 0.25f; // prevents super-flat trajectories
    [SerializeField] private float _minHorizontal = 0.10f; // prevents endless near-vertical loops

    [Header("Paddle Aim Bounce")]
    [SerializeField] private float _maxBounceAngleDegrees = 75f;
    [SerializeField] private string _paddleObjectName = "Paddle";

    private bool _launched;

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
            {
                transform.position = (Vector2)_paddle.position + _paddleOffset;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                Launch();
            }
        }
    }

    private void FixedUpdate()
    {
        if (!_launched) return;

        // Keep speed constant
        _rb.linearVelocity = _rb.linearVelocity.normalized * _speed;

        // Nudge away from "boring" near-straight lines
        Vector2 v = _rb.linearVelocity.normalized;

        if (Mathf.Abs(v.x) < _minHorizontal)
        {
            v.x = Mathf.Sign(v.x == 0 ? Random.Range(-1f, 1f) : v.x) * _minHorizontal;
            v = v.normalized;
            _rb.linearVelocity = v * _speed;
        }

        if (Mathf.Abs(v.y) < _minVertical)
        {
            v.y = Mathf.Sign(v.y == 0 ? 1f : v.y) * _minVertical;
            v = v.normalized;
            _rb.linearVelocity = v * _speed;
        }
    }

    public void Launch()
    {
        if (_launched) return;

        _launched = true;

        // enable physics first in Unity 6
        _rb.simulated = true;

        _rb.linearVelocity = _launchDirection * _speed;
    }

    public bool IsLaunched()
    {
        return _launched;
    }




    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Paddle aim bounce
        if (collision.collider.gameObject.name == _paddleObjectName)
        {
            SfxPlayer.Instance?.PlayPaddleHit();
            HandlePaddleBounce(collision);
            return;
        }

        if (collision.collider.CompareTag("Wall"))
        {
            SfxPlayer.Instance?.PlayWallHit();
        }


        // Brick hits
        var brick = collision.collider.GetComponent<Brick>();
        if (brick != null)
        {
            brick.Hit();
        }
    }

    private void HandlePaddleBounce(Collision2D collision)
    {
        // Find paddle width in world units
        float paddleWidth = collision.collider.bounds.size.x;
        float paddleCenterX = collision.collider.bounds.center.x;

        // Contact point where ball hit the paddle
        float hitX = collision.GetContact(0).point.x;

        // Normalize hit position to -1..+1 across the paddle
        float t = (hitX - paddleCenterX) / (paddleWidth * 0.5f);
        t = Mathf.Clamp(t, -1f, 1f);

        // Convert to angle: left = +angle to left, right = +angle to right
        float angle = t * _maxBounceAngleDegrees;

        // Build direction from angle (0 degrees = straight up)
        float rad = angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)).normalized;

        _rb.linearVelocity = dir * _speed;
    }

    public void ResetToPaddle()
    {
        _launched = false;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.simulated = false; // physics off while "stuck" to paddle
        }

        // Ensure we have a paddle reference (prefer the serialized one)
        if (_paddle == null)
        {
            var paddleCtrl = FindFirstObjectByType<PaddleController>();
            if (paddleCtrl != null) _paddle = paddleCtrl.transform;
        }

        // Snap immediately (Update will keep it attached)
        if (_paddle != null)
            transform.position = (Vector2)_paddle.position + _paddleOffset;
    }


}
