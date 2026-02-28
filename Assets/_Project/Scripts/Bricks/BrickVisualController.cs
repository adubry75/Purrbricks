using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BrickVisualController : MonoBehaviour
{
    private SpriteRenderer _sr;
    private BrickSkin _skin;

    // Color set by the level loader (template or JSON override)
    private Color _originalTint = Color.white;
    // Color currently displayed (may be darkened by damage)
    private Color _displayTint = Color.white;

    // Shimmer config — adjusted per brick type by SetShimmer()
    private float _shimmerSpeed  = 1.4f;
    private float _shimmerAmount = 0.05f; // fraction of brightness to pulse

    // Powerup brick extras
    private bool _isPowerupBrick;
    private bool _isPowerupSprite;   // true when overlay uses art sprite instead of procedural ring
    private GameObject _powerupOverlay;
    private float _overlayPulseTimer;
    private Vector3 _overlayBaseScaleV = Vector3.one * 0.18f; // world-space base scale of the overlay

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (_sr == null) return;

        float pulse = 1f - _shimmerAmount
            + _shimmerAmount * (0.5f + 0.5f * Mathf.Sin(Time.time * _shimmerSpeed));

        Color c = _displayTint * pulse;
        c.a = 1f;
        _sr.color = c;

        // Powerup overlay: pulsing icon/ring within brick bounds
        if (_isPowerupBrick && _powerupOverlay != null)
        {
            _overlayPulseTimer += Time.deltaTime * 2.5f;
            float glowPulse = 0.5f + 0.5f * Mathf.Sin(_overlayPulseTimer);
            var osr = _powerupOverlay.GetComponent<SpriteRenderer>();
            if (osr != null)
            {
                Color oc = osr.color;
                // Art sprite: pulse between 0.75 and 1.0 alpha (clearly visible)
                // Procedural ring: subtle 0.25–0.45 alpha
                oc.a = _isPowerupSprite
                    ? 0.75f + 0.25f * glowPulse
                    : 0.25f + 0.20f * glowPulse;
                osr.color = oc;
            }
            _powerupOverlay.transform.localScale = _overlayBaseScaleV * (1.0f + 0.06f * glowPulse);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by LevelLoader after spawning the brick.</summary>
    public void SetSkin(BrickSkin skin, Color tint)
    {
        _skin = skin;
        _originalTint = tint;
        _displayTint  = tint;

        if (_sr == null) _sr = GetComponent<SpriteRenderer>();

        if (skin != null && skin.sprite != null)
        {
            _sr.sprite   = skin.sprite;
            _sr.drawMode = SpriteDrawMode.Simple;
            _sr.color    = tint;
        }
        else
        {
            // Procedural shiny gradient sprite — shared across all bricks
            _sr.sprite   = BrickSpriteGenerator.GetShared();
            _sr.drawMode = SpriteDrawMode.Sliced;
            _sr.color    = tint;
        }
    }

    /// <summary>
    /// Makes high-HP bricks pulse more dramatically so they look special at a glance.
    /// </summary>
    public void SetShimmer(float speed, float amount)
    {
        _shimmerSpeed  = speed;
        _shimmerAmount = amount;
    }

    /// <summary>Darkens the brick proportionally to damage taken.</summary>
    public void UpdateDamageState(int currentHp, int maxHp)
    {
        if (_sr == null) return;

        if (_skin != null && _skin.damageSpriteStages != null && _skin.damageSpriteStages.Length > 0)
        {
            float ratio     = 1f - (float)currentHp / Mathf.Max(1, maxHp);
            int   stageIdx  = Mathf.Clamp(
                Mathf.FloorToInt(ratio * _skin.damageSpriteStages.Length),
                0, _skin.damageSpriteStages.Length - 1);

            if (_skin.damageSpriteStages[stageIdx] != null)
                _sr.sprite = _skin.damageSpriteStages[stageIdx];
        }
        else
        {
            // Procedural: shift color toward a dark "cracked" look as HP drops
            float damageFraction = 1f - (float)currentHp / Mathf.Max(1, maxHp);
            // Target: very dark brownish-gray when destroyed
            Color damagedColor = new Color(0.20f, 0.08f, 0.04f);
            _displayTint = Color.Lerp(_originalTint, damagedColor, damageFraction * 0.80f);
        }
    }

    /// <summary>
    /// Marks this brick as containing a powerup.
    /// If an art sprite exists in PowerupIconRegistry it is shown scaled to fit the brick;
    /// otherwise falls back to the procedural glowing ring.
    /// </summary>
    public void SetPowerupBrick(string powerupId)
    {
        _isPowerupBrick = true;

        // Gentle shimmer to hint at special status
        _shimmerSpeed  = 3.0f;
        _shimmerAmount = 0.10f;

        // Resolve powerup type and accent color
        Color overlayColor = Color.white;
        PowerupType pt = default;
        bool parsed = System.Enum.TryParse(powerupId, true, out pt);
        if (parsed)
            overlayColor = PowerupPickup.GetTypeColor(pt);

        // Tint the brick itself toward the powerup color
        _displayTint  = Color.Lerp(_originalTint, overlayColor, 0.55f);
        _originalTint = _displayTint;

        // Brick dimensions from its sprite bounds (fallback to standard 1.35 × 0.45)
        float brickW = (_sr != null && _sr.sprite != null) ? _sr.bounds.size.x : 1.35f;
        float brickH = (_sr != null && _sr.sprite != null) ? _sr.bounds.size.y : 0.45f;

        // Create overlay child
        _powerupOverlay = new GameObject("PowerupGlow");
        _powerupOverlay.transform.SetParent(transform, false);
        _powerupOverlay.transform.localPosition = Vector3.zero;
        var osr = _powerupOverlay.AddComponent<SpriteRenderer>();
        osr.sortingOrder = _sr != null ? _sr.sortingOrder + 1 : 2;

        // Try art sprite first
        Sprite icon = parsed ? PowerupIconRegistry.Instance?.GetIcon(pt) : null;
        if (icon != null)
        {
            _isPowerupSprite = true;
            osr.sprite = icon;
            osr.color  = Color.white; // alpha pulsed in Update

            // Scale sprite to fill ~88% of the brick (non-uniform so it fills the exact shape)
            float nativeW = icon.rect.width  / icon.pixelsPerUnit;
            float nativeH = icon.rect.height / icon.pixelsPerUnit;
            float scaleX  = (brickW * 0.88f) / nativeW;
            float scaleY  = (brickH * 0.88f) / nativeH;
            _overlayBaseScaleV = new Vector3(scaleX, scaleY, 1f);
        }
        else
        {
            // Fallback: procedural ring sized to brick height
            _isPowerupSprite = false;
            osr.sprite = CreateRingSprite();
            osr.color  = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 0.35f);
            // Ring sprite native world size ≈ 2.778 units; scale to 90% of brick height
            float ringScale = (brickH * 0.90f) / 2.778f;
            _overlayBaseScaleV = Vector3.one * ringScale;
        }

        _powerupOverlay.transform.localScale = _overlayBaseScaleV;
    }

    private static Sprite _ringSprite;
    private static Sprite CreateRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;

        int res = 32;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = res * 0.5f;
        float outerR = center - 1f;
        float innerR = outerR - 4f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float alpha = 0f;
                if (dist < outerR && dist > innerR)
                    alpha = 1f - Mathf.Abs((dist - (outerR + innerR) * 0.5f)) / ((outerR - innerR) * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res * 0.36f);
        return _ringSprite;
    }

    /// <summary>Spawns break particle prefab if the skin provides one.</summary>
    public void PlayBreakEffect()
    {
        if (_skin != null && _skin.breakParticlePrefab != null)
            Instantiate(_skin.breakParticlePrefab, transform.position, Quaternion.identity);
    }
}
