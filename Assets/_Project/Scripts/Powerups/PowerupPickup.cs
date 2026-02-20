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

    // Is this a harmful powerup? (index >= 8 in enum)
    private static bool IsBad(PowerupType t) => (int)t >= 8;

    // Color per powerup type — index matches enum value
    private static readonly Color[] TypeColors = new Color[]
    {
        // ── Good ──────────────────────────────────────────────────────
        new Color(0.30f, 0.60f, 1.00f),   // 0  WidePaddle   sky-blue
        new Color(1.00f, 0.40f, 0.00f),   // 1  MultiBall    orange
        new Color(0.60f, 0.00f, 1.00f),   // 2  StickyBall   purple
        new Color(1.00f, 0.85f, 0.00f),   // 3  SpeedBall    gold
        new Color(0.10f, 1.00f, 0.30f),   // 4  ExtraLife    green
        new Color(1.00f, 0.10f, 0.30f),   // 5  Laser        crimson
        new Color(1.00f, 0.45f, 0.00f),   // 6  Fireball     fire-orange
        new Color(0.90f, 0.20f, 0.90f),   // 7  BombBrick    magenta
        // ── Bad ───────────────────────────────────────────────────────
        new Color(0.55f, 0.05f, 0.05f),   // 8  ShrinkPaddle dark-red
        new Color(0.20f, 0.65f, 0.05f),   // 9  ZipBall      sickly-green
        new Color(0.40f, 0.00f, 0.50f),   // 10 FlipControls dark-purple
        new Color(0.10f, 0.35f, 0.10f),   // 11 CursedBall   murky-green
    };

    private static readonly string[] TypeIcons = new string[]
    {
        "↔", "⊛", "⊕", "⚡", "♥", "|", "F!", "B!",
        "↕", "!!", "↩", "☠",
    };

    private static readonly string[] TypeNames = new string[]
    {
        "Wide", "Multi", "Sticky", "Fast", "+Life", "Laser", "Fire", "Bomb",
        "SHRINK", "ZIP!", "FLIP!", "CURSE",
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
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        var col = GetComponent<CircleCollider2D>();
        col.radius    = 0.38f;
        col.isTrigger = true;
    }

    private void Update()
    {
        bool bad  = IsBad(_type);
        float speed = bad ? 6f : 3f;
        float amp   = bad ? 0.20f : 0.15f;
        _bobTimer += Time.deltaTime * speed;
        if (_sr != null)
        {
            float pulse = (1f - amp) + amp * Mathf.Sin(_bobTimer);
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
        int   idx   = Mathf.Clamp((int)_type, 0, TypeColors.Length - 1);
        Color color = TypeColors[idx];
        bool  bad   = IsBad(_type);

        // ── Outer glow ring ────────────────────────────────────────────────────
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(transform, false);
        var glowSr = glowGO.AddComponent<SpriteRenderer>();
        glowSr.sprite       = CreateCircleSprite(32, true);
        glowSr.color        = new Color(color.r, color.g, color.b, bad ? 0.55f : 0.35f);
        glowSr.sortingOrder = 8;
        glowGO.transform.localScale = Vector3.one * (bad ? 1.9f : 1.6f);

        // ── Filled orb ─────────────────────────────────────────────────────────
        var orbGO = new GameObject("Orb");
        orbGO.transform.SetParent(transform, false);
        _sr               = orbGO.AddComponent<SpriteRenderer>();
        _sr.sprite        = CreateOrbSprite(color, bad);
        _sr.sortingOrder  = 9;

        // ── Label ──────────────────────────────────────────────────────────────
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(transform, false);
        labelGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);

        string label = idx < TypeNames.Length ? TypeNames[idx] : "?";
        var tm = labelGO.AddComponent<TextMesh>();
        tm.text          = label;
        tm.fontSize      = 9;
        tm.fontStyle     = FontStyle.Bold;
        tm.color         = bad ? new Color(1f, 0.6f, 0.6f) : Color.white;
        tm.alignment     = TextAlignment.Center;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.characterSize = 0.1f;

        var mr = labelGO.GetComponent<MeshRenderer>();
        mr.sortingOrder = 10;
    }

    private void SpawnCollectEffect()
    {
        int   idx   = Mathf.Clamp((int)_type, 0, TypeColors.Length - 1);
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
        var tex    = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = resolution * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float dist  = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
            float alpha = softEdge
                ? Mathf.Clamp01(1f - dist / radius)
                : (dist < radius ? 1f : 0f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    private static Sprite CreateOrbSprite(Color baseColor, bool bad)
    {
        int res    = 32;
        var tex    = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = res * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
            if (dist >= radius) { tex.SetPixel(x, y, Color.clear); continue; }

            float t = dist / radius;

            // Good: bright center → color at edge. Bad: dark murky center.
            Color c = bad
                ? Color.Lerp(new Color(baseColor.r * 0.4f, baseColor.g * 0.4f, baseColor.b * 0.4f), baseColor, t * 0.8f)
                : Color.Lerp(Color.white, baseColor, t * 0.7f);

            // Specular highlight
            float hx      = (x + 0.5f - center * 0.65f) / radius;
            float hy      = (y + 0.5f - center * 1.35f) / radius;
            float specDist = Mathf.Sqrt(hx * hx + hy * hy);
            float spec    = Mathf.Clamp01(1f - specDist * 2.5f);
            c = Color.Lerp(c, Color.white, spec * (bad ? 0.25f : 0.55f));

            c.a = Mathf.Clamp01((radius - dist) / 2f);
            tex.SetPixel(x, y, c);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
