using UnityEngine;

/// <summary>
/// Pinball-style bumper: indestructible, round, plays a "ding" and gives the ball
/// a temporary speed burst that decays smoothly over time.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BumperBrick : MonoBehaviour
{
    [SerializeField] private float _boostDurationSeconds = 5.0f;
    [SerializeField] private float _sfxCooldownSeconds = 0.05f;

    private float _nextSfxTime;

    private SpriteRenderer _sr;
    private CircleCollider2D _circle;

    // Cache a simple procedural sprite so bumpers look round even without imported art.
    private static Sprite s_cachedSprite;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _circle = GetComponent<CircleCollider2D>();
    }

    public void Configure(float worldDiameter)
    {
        // Disable the default brick box collider if present.
        var box = GetComponent<BoxCollider2D>();
        if (box != null) box.enabled = false;

        if (_circle == null) _circle = gameObject.AddComponent<CircleCollider2D>();
        _circle.enabled = true;

        if (_sr != null)
        {
            _sr.drawMode = SpriteDrawMode.Simple;
            _sr.sprite = GetOrCreateSprite();
            _sr.color = new Color(1.0f, 0.85f, 0.25f, 1.0f); // warm pinball gold

            // Scale the bumper uniformly to match the requested world diameter.
            float spriteDiamUnits = Mathf.Max(0.0001f, _sr.sprite.bounds.size.x);
            float s = worldDiameter / spriteDiamUnits;
            transform.localScale = new Vector3(s, s, 1f);

            // CircleCollider2D radius is in local units; match it to the sprite.
            _circle.radius = _sr.sprite.bounds.size.x * 0.5f;
        }
        else
        {
            // Fallback: set a reasonable local radius and keep scale at 1.
            _circle.radius = Mathf.Max(0.05f, worldDiameter * 0.5f);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var ball = collision.collider.GetComponent<BallController>();
        if (ball == null) return;

        // SFX
        if (Time.time >= _nextSfxTime)
        {
            _nextSfxTime = Time.time + _sfxCooldownSeconds;
            SfxPlayer.Instance?.PlayBumperDing();
        }

        // Gameplay effect
        ball.TriggerBumperBoost(_boostDurationSeconds);
    }

    private static Sprite GetOrCreateSprite()
    {
        if (s_cachedSprite != null) return s_cachedSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float r = (size - 2) * 0.5f;
        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c.x;
                float dy = y - c.y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d > r)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                // Soft edge + simple highlight
                float edge = Mathf.InverseLerp(r, r - 2.5f, d);
                float highlight = Mathf.Clamp01(((-dx - dy) / (r * 1.4f)) * 0.5f + 0.5f);

                Color baseCol = new Color(1.0f, 0.85f, 0.25f, 1.0f);
                Color hiCol = new Color(1.0f, 0.98f, 0.55f, 1.0f);
                Color col = Color.Lerp(baseCol, hiCol, highlight * 0.65f);
                col.a *= edge;
                tex.SetPixel(x, y, col);
            }
        }

        tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        // 64 px at 100 PPU => ~0.64 world-units diameter at scale 1.
        s_cachedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit: 100f);
        return s_cachedSprite;
    }
}

