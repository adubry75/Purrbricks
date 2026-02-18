using UnityEngine;

public class PaddleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private BoxCollider2D _playfieldBounds;

    [Header("Movement")]
    [SerializeField] private float _yLocked = -7f;
    [SerializeField] private float _smoothTime = 0.01f;

    [SerializeField] private BoxCollider2D _leftWall;
    [SerializeField] private BoxCollider2D _rightWall;
    [SerializeField] private float _wallPadding = 0.02f; // tiny safety margin
    private float _halfWidth;

    private float _velocityX;

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

        // Mouse -> world X
        float mouseX = _camera.ScreenToWorldPoint(Input.mousePosition).x;

        // Compute limits based on wall colliders and paddle width
        float halfWidth = GetHalfWidthWorld();

        float leftLimit = _leftWall.bounds.max.x + halfWidth + _wallPadding;
        float rightLimit = _rightWall.bounds.min.x - halfWidth - _wallPadding;

        float targetX = Mathf.Clamp(mouseX, leftLimit, rightLimit);

        // Smooth toward clamped target
        float newX = Mathf.SmoothDamp(transform.position.x, targetX, ref _velocityX, _smoothTime);

        transform.position = new Vector3(newX, _yLocked, transform.position.z);
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
