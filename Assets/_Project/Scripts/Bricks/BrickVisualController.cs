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

    /// <summary>Spawns break particle prefab if the skin provides one.</summary>
    public void PlayBreakEffect()
    {
        if (_skin != null && _skin.breakParticlePrefab != null)
            Instantiate(_skin.breakParticlePrefab, transform.position, Quaternion.identity);
    }
}
