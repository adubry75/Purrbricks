using UnityEngine;

/// <summary>
/// A glowing orb that falls from a destroyed brick.
/// Collected by touching the paddle; applies the corresponding powerup.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class PowerupPickup : MonoBehaviour
{
    private PowerupType _type;
    private SpriteRenderer _sr;
    private float _bobTimer;

    // Color scheme per powerup
    private static readonly Color[] TypeColors = new Color[]
    {
        new Color(0.3f, 0.6f, 1.0f),   // WidePaddle  — blue
        new Color(1.0f, 0.4f, 0.0f),   // MultiBall   — orange
        new Color(0.6f, 0.0f, 1.0f),   // StickyBall  — purple
        new Color(1.0f, 0.85f, 0.0f),  // SpeedBall   — gold
        new Color(0.1f, 1.0f, 0.3f),   // ExtraLife   — green
        new Color(1.0f, 0.1f, 0.3f),   // Laser       — red
    };

    // Short icon drawn in the center (unicode shapes that look like icons)
    private static readonly string[] TypeIcons = new string[]
    {
        "↔",  // WidePaddle
        "⊛",  // MultiBall
        "⊕",  // StickyBall
        "⚡",  // SpeedBall
        "♥",  // ExtraLife
        "|",  // Laser (vertical bolt)
    };

    private static readonly string[] TypeNames = new string[]
    {
        "Wide", "Multi", "Sticky", "Fast", "+Life", "Laser"
    };

    public void Init(PowerupType type)
    {
        _type = type;
        BuildVisuals();
    }

    // Legacy string-based init for backward compat
    public void Init(string powerupId)
    {
        if (System.Enum.TryParse(powerupId, true, out PowerupType t))
            Init(t);
        else
        {
            Debug.LogWarning($"PowerupPickup: unknown id '{powerupId}'");
            Destroy(gameObject);
        }
    }

    private void Awake()
    {
        var rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0.55f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var col = GetComponent<CircleCollider2D>();
        col.radius = 0.38f;
        col.isTrigger = true;
    }

    private void Update()
    {
        // Gentle bob animation while falling
        _bobTimer += Time.deltaTime * 3f;
        if (_sr != null)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(_bobTimer);
            _sr.transform.localScale = Vector3.one * pulse;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PaddleController>() != null)
        {
            PowerupManager.Instance?.Apply(_type);
            SpawnCollectEffect();
            Destroy(gameObject);
            return;
        }

        if (other.GetComponent<DeathZone>() != null)
            Destroy(gameObject);
    }

    private void BuildVisuals()
    {
        int idx = (int)_type;
        Color color = TypeColors[idx];
        string icon = TypeIcons[idx];
        string label = TypeNames[idx];

        // ── Outer glow ring ────────────────────────────────────────────────────
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(transform, false);
        var glowSr = glowGO.AddComponent<SpriteRenderer>();
        glowSr.sprite = CreateCircleSprite(32, true);
        glowSr.color  = new Color(color.r, color.g, color.b, 0.35f);
        glowSr.sortingOrder = 8;
        glowGO.transform.localScale = Vector3.one * 1.6f;

        // ── Filled orb ─────────────────────────────────────────────────────────
        var orbGO = new GameObject("Orb");
        orbGO.transform.SetParent(transform, false);
        _sr = orbGO.AddComponent<SpriteRenderer>();
        _sr.sprite = CreateOrbSprite(color);
        _sr.sortingOrder = 9;

        // ── Icon label (TextMesh) ──────────────────────────────────────────────
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(transform, false);
        labelGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);

        var tm = labelGO.AddComponent<TextMesh>();
        tm.text = label;
        tm.fontSize = 9;
        tm.fontStyle = FontStyle.Bold;
        tm.color = Color.white;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.characterSize = 0.1f;

        var mr = labelGO.GetComponent<MeshRenderer>();
        mr.sortingOrder = 10;
    }

    private void SpawnCollectEffect()
    {
        int idx = (int)_type;
        Color color = TypeColors[idx];
        BrickParticleGenerator.SpawnBurst(transform.position, color, 18, true);
        SfxPlayer.Instance?.PlayPowerupPickup();
    }

    public static Color GetTypeColor(PowerupType type)
    {
        int idx = Mathf.Clamp((int)type, 0, TypeColors.Length - 1);
        return TypeColors[idx];
    }

    // ── Sprite generators ─────────────────────────────────────────────────────

    private static Sprite CreateCircleSprite(int resolution, bool softEdge)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = resolution * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = softEdge
                    ? Mathf.Clamp01(1f - dist / radius)
                    : (dist < radius ? 1f : 0f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    private static Sprite CreateOrbSprite(Color baseColor)
    {
        int res = 32;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = res * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                if (dist >= radius) { tex.SetPixel(x, y, Color.clear); continue; }

                float t = dist / radius; // 0=center, 1=edge

                // Base gradient: bright center, darker edge
                Color c = Color.Lerp(Color.white, baseColor, t * 0.7f);

                // Specular highlight in upper-left
                float hx = (x + 0.5f - center * 0.65f) / radius;
                float hy = (y + 0.5f - center * 1.35f) / radius;
                float specDist = Mathf.Sqrt(hx * hx + hy * hy);
                float spec = Mathf.Clamp01(1f - specDist * 2.5f);
                c = Color.Lerp(c, Color.white, spec * 0.55f);

                // Edge fade
                float alpha = Mathf.Clamp01((radius - dist) / 2f);
                c.a = alpha;
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
