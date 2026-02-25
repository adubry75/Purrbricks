using UnityEngine;

/// <summary>
/// A pass-through gate that tints a ball with a prism color when it passes through.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class PrismGate : MonoBehaviour
{
    [SerializeField] private PrismColor _color = PrismColor.Blue;
    // width = clear opening between posts (mist/trigger span)
    // height = mist/trigger height (posts are taller)
    [SerializeField] private Vector2 _size = new Vector2(4.0f, 0.35f);
    [SerializeField] private float _postThickness = 0.35f;

    private BoxCollider2D _trigger;
    private GameObject _fxRoot;
    private SpriteRenderer _postL;
    private SpriteRenderer _postR;
    private BoxCollider2D _postLCollider;
    private BoxCollider2D _postRCollider;
    private LineRenderer _mist;
    private Material _additiveMat;
    private float _t;

    public void Init(PrismColor color, Vector2 size, float postThickness)
    {
        _color = color;
        _size = size;
        _postThickness = postThickness;
        EnsureFx();
        ApplyGeometry();
        ApplyColors();
    }

    private void Awake()
    {
        _trigger = GetComponent<BoxCollider2D>();
        _trigger.isTrigger = true;
        EnsureFx();
        ApplyGeometry();
        ApplyColors();
    }

    private void OnDestroy()
    {
        if (_additiveMat != null)
            Destroy(_additiveMat);
    }

    private void EnsureFx()
    {
        if (_fxRoot == null)
        {
            _fxRoot = new GameObject("PrismGateFx");
            _fxRoot.transform.SetParent(transform, false);
        }

        if (_additiveMat == null)
        {
            _additiveMat = new Material(Shader.Find("Sprites/Default"));
            _additiveMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _additiveMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            _additiveMat.SetInt("_ZWrite", 0);
        }

        // Posts
        if (_postL == null) _postL = CreatePost("PostL");
        if (_postR == null) _postR = CreatePost("PostR");

        // Mist ribbon
        if (_mist == null)
        {
            var mistGO = new GameObject("Mist");
            mistGO.transform.SetParent(_fxRoot.transform, false);
            _mist = mistGO.AddComponent<LineRenderer>();
            _mist.useWorldSpace = false;
            _mist.positionCount = 28;
            _mist.numCapVertices = 6;
            _mist.material = _additiveMat;
            _mist.sortingOrder = 4;
        }
    }

    private SpriteRenderer CreatePost(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_fxRoot.transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = BrickSpriteGenerator.GetShared(); // simple sliced gradient
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sortingOrder = 3;

        // Solid post collider so the ball bounces off the posts.
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = false;

        if (name == "PostL")
            _postLCollider = col;
        else if (name == "PostR")
            _postRCollider = col;

        return sr;
    }

    private void ApplyGeometry()
    {
        float openingW = Mathf.Max(0.05f, _size.x);
        float openingH = Mathf.Max(0.05f, _size.y);
        float postW = Mathf.Max(0.05f, _postThickness);
        float postH = openingH * 1.35f;
        float postOffset = (openingW * 0.5f) + (postW * 0.5f);

        if (_trigger != null)
        {
            // Trigger is only the mist area (clear opening), not the posts.
            _trigger.size = new Vector2(openingW, openingH);
            _trigger.offset = Vector2.zero;
        }

        if (_postL != null)
        {
            _postL.transform.localPosition = new Vector3(-postOffset, 0f, 0f);
            _postL.size = new Vector2(postW, postH);
        }
        if (_postR != null)
        {
            _postR.transform.localPosition = new Vector3(postOffset, 0f, 0f);
            _postR.size = new Vector2(postW, postH);
        }

        if (_postLCollider != null)
            _postLCollider.size = new Vector2(postW, postH);
        if (_postRCollider != null)
            _postRCollider.size = new Vector2(postW, postH);

        if (_mist != null)
        {
            // Mist always spans exactly the clear opening between posts.
            _mist.startWidth = openingH * 0.85f;
            _mist.endWidth = openingH * 0.85f;
        }
    }

    private void ApplyColors()
    {
        var c = PrismColorUtil.ToUnityColor(_color);

        // Tint posts
        if (_fxRoot != null)
        {
            var srs = _fxRoot.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            foreach (var sr in srs)
            {
                if (sr == null) continue;
                sr.color = Color.Lerp(Color.black, c, 0.75f);
            }
        }

        if (_mist != null)
        {
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(c.r, c.g, c.b), 0f),
                    new GradientColorKey(Color.white, 0.5f),
                    new GradientColorKey(new Color(c.r, c.g, c.b), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.05f, 0f),
                    new GradientAlphaKey(0.55f, 0.5f),
                    new GradientAlphaKey(0.05f, 1f),
                }
            );
            _mist.colorGradient = grad;
        }
    }

    private void Update()
    {
        if (_mist == null) return;

        _t += Time.deltaTime * 1.5f;
        float w = Mathf.Max(0.05f, _size.x);
        int n = _mist.positionCount;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)(n - 1);
            float x = Mathf.Lerp(-w * 0.5f, w * 0.5f, u);

            float h = Mathf.Max(0.05f, _size.y);
            float y = Mathf.Sin((u * 6.0f + _t) * Mathf.PI * 2f) * (h * 0.18f)
                    + Mathf.Sin((u * 2.2f - _t * 0.7f) * Mathf.PI * 2f) * (h * 0.08f);

            _mist.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var ball = other.GetComponent<BallController>();
        if (ball == null) return;

        ball.SetPrismColor(_color);
    }
}
