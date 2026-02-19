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

    [SerializeField] private BoxCollider2D _leftWall;
    [SerializeField] private BoxCollider2D _rightWall;
    [SerializeField] private float _wallPadding = 0.02f; // tiny safety margin
    private float _halfWidth;

    private float _velocityX;
    private bool _isDemoMode;

    private void Reset()
    {
        _camera = Camera.main;
    }

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;

        _halfWidth = GetComponent<BoxCollider2D>().bounds.extents.x;
    }

    private void Update()
    {
        if (_camera == null) return;
        if (_leftWall == null || _rightWall == null) return;

        float targetX;

        if (_isDemoMode)
        {
            // Demo AI: track the ball's X position
            var ball = FindFirstObjectByType<BallController>();
            if (ball != null)
                targetX = ball.transform.position.x;
            else
                targetX = 0f; // center if no ball
        }
        else
        {
            // Player control: follow mouse
            targetX = _camera.ScreenToWorldPoint(Input.mousePosition).x;
        }

        // Compute limits based on wall colliders and paddle width
        float halfWidth = GetHalfWidthWorld();

        float leftLimit = _leftWall.bounds.max.x + halfWidth + _wallPadding;
        float rightLimit = _rightWall.bounds.min.x - halfWidth - _wallPadding;

        targetX = Mathf.Clamp(targetX, leftLimit, rightLimit);

        // Smooth toward clamped target (slower in demo mode for more natural AI feel)
        float smoothTime = _isDemoMode ? _demoSmoothTime : _smoothTime;
        float newX = Mathf.SmoothDamp(transform.position.x, targetX, ref _velocityX, smoothTime);

        transform.position = new Vector3(newX, _yLocked, transform.position.z);
    }

    public void ResetPosition()
    {
        transform.position = new Vector3(0f, transform.position.y, transform.position.z);
    }

    public void SetDemoMode(bool isDemoMode)
    {
        _isDemoMode = isDemoMode;
    }


    private float GetHalfWidthWorld()
    {
        // Prefer collider if present
        var col = GetComponent<BoxCollider2D>();
        if (col != null) return col.bounds.extents.x;

        // Fallback: use renderer
        var r = GetComponent<Renderer>();
        if (r != null) return r.bounds.extents.x;

        return 0.5f;
    }
}
