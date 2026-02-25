using UnityEngine;

/// <summary>
/// Visual polish for the existing bottom wall when ShieldWall powerup is active.
/// Keeps gameplay collision owned by the scene wall; this only adds glow/waves.
/// </summary>
[DisallowMultipleComponent]
public class ShieldWallFx : MonoBehaviour
{
    private const int Segments = 24;

    [SerializeField] private Color _baseColor = new Color(0.05f, 0.90f, 1.00f, 1f);
    [SerializeField] private float _outerWidth = 0.18f;
    [SerializeField] private float _innerWidth = 0.07f;
    [SerializeField] private float _waveAmp = 0.06f;
    [SerializeField] private float _waveFreq = 2.2f;
    [SerializeField] private float _scrollSpeed = 1.6f;

    private Collider2D _col;
    private SpriteRenderer _sr;
    private Color _originalSrColor;
    private bool _cachedOriginal;

    private GameObject _fxRoot;
    private LineRenderer _outer;
    private LineRenderer _inner;
    private Material _additiveMat;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        _sr = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (_sr != null && !_cachedOriginal)
        {
            _originalSrColor = _sr.color;
            _cachedOriginal = true;
        }

        EnsureFxObjects();

        if (_sr != null)
        {
            // Make the underlying bar feel like an energized shield, not a plain line.
            _sr.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0.35f);
        }

        if (_fxRoot != null)
            _fxRoot.SetActive(true);
    }

    private void OnDisable()
    {
        if (_fxRoot != null)
            _fxRoot.SetActive(false);

        if (_sr != null && _cachedOriginal)
            _sr.color = _originalSrColor;
    }

    private void OnDestroy()
    {
        if (_additiveMat != null)
            Destroy(_additiveMat);
    }

    private void EnsureFxObjects()
    {
        if (_fxRoot == null)
        {
            _fxRoot = new GameObject("ShieldWallFx");
            _fxRoot.transform.SetParent(transform, false);
            _fxRoot.transform.localPosition = Vector3.zero;
        }

        if (_additiveMat == null)
        {
            _additiveMat = new Material(Shader.Find("Sprites/Default"));
            _additiveMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _additiveMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            _additiveMat.SetInt("_ZWrite", 0);
        }

        if (_outer == null)
            _outer = CreateLine("Outer", _outerWidth, alpha0: 0.45f, alpha1: 0.05f, sortingOrder: 6);
        if (_inner == null)
            _inner = CreateLine("Inner", _innerWidth, alpha0: 0.90f, alpha1: 0.15f, sortingOrder: 7);
    }

    private LineRenderer CreateLine(string name, float width, float alpha0, float alpha1, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_fxRoot.transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = Segments;
        lr.useWorldSpace = true;
        lr.material = _additiveMat;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 6;
        lr.sortingOrder = sortingOrder;

        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(_baseColor.r, _baseColor.g, _baseColor.b), 0f),
                new GradientColorKey(new Color(1f, 1f, 1f), 0.5f),
                new GradientColorKey(new Color(_baseColor.r, _baseColor.g, _baseColor.b), 1f),
            },
            new[]
            {
                new GradientAlphaKey(alpha0, 0f),
                new GradientAlphaKey(alpha1, 1f),
            }
        );
        lr.colorGradient = grad;

        return lr;
    }

    private void Update()
    {
        if (_outer == null || _inner == null) return;

        // Determine shield span from collider bounds when possible.
        float leftX;
        float rightX;
        float y;

        if (_col != null)
        {
            var b = _col.bounds;
            leftX = b.min.x;
            rightX = b.max.x;
            y = b.center.y;
        }
        else
        {
            leftX = transform.position.x - 7f;
            rightX = transform.position.x + 7f;
            y = transform.position.y;
        }

        float t = Time.unscaledTime;
        float span = Mathf.Max(0.01f, rightX - leftX);

        for (int i = 0; i < Segments; i++)
        {
            float u = i / (float)(Segments - 1);
            float x = Mathf.Lerp(leftX, rightX, u);

            // Two layered waves gives a "magical" shimmer instead of a flat line.
            float phase = (u * _waveFreq * Mathf.PI * 2f) + (t * _scrollSpeed);
            float wobble =
                Mathf.Sin(phase) * _waveAmp +
                Mathf.Sin(phase * 2.7f + 1.3f) * (_waveAmp * 0.35f);

            var p = new Vector3(x, y + wobble, 0f);
            _outer.SetPosition(i, p);
            _inner.SetPosition(i, p);
        }
    }
}

