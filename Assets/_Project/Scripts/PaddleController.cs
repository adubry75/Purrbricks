using UnityEngine;

public class PaddleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private BoxCollider2D _playfieldBounds;

    [Header("Movement")]
    [SerializeField] private float _yLocked = -4f;
    [SerializeField] private float _smoothTime = 0.04f;

    private float _velocityX;

    private void Reset()
    {
        _camera = Camera.main;
    }

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    private void Update()
    {
        if (_camera == null || _playfieldBounds == null) return;

        // Mouse position -> world position
        Vector3 mouseScreen = Input.mousePosition;
        Vector3 mouseWorld = _camera.ScreenToWorldPoint(mouseScreen);

        // Clamp X to playfield bounds, accounting for paddle width
        Bounds field = _playfieldBounds.bounds;
        float halfPaddleWidth = GetHalfWidthWorld();

        float minX = field.min.x + halfPaddleWidth;
        float maxX = field.max.x - halfPaddleWidth;

        float targetX = Mathf.Clamp(mouseWorld.x, minX, maxX);

        // Smooth damp for nice feel
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
