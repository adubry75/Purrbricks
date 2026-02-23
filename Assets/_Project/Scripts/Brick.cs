using UnityEngine;

public class Brick : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private int _hitPoints = 1;
    [SerializeField] private int _maxHitPoints = 1;
    [SerializeField] private int _points = 100;
    [SerializeField] private bool _isIndestructible;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer _sr;

    private BrickVisualController _visual;
    private string _powerupId;
    private bool _isDead; // guard against multiple hits in the same frame

    private void Reset()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        _visual = GetComponent<BrickVisualController>();
    }

    // -------- Called by LevelLoader after spawn --------

    public void SetHitPoints(int hp)
    {
        _hitPoints = Mathf.Clamp(hp, 1, 99);
        _maxHitPoints = _hitPoints;
        _visual?.UpdateDamageState(_hitPoints, _maxHitPoints);
    }

    public void SetPoints(int points)
    {
        _points = Mathf.Max(0, points);
    }

    public void SetIndestructible(bool indestructible)
    {
        _isIndestructible = indestructible;
    }

    public void SetPowerupId(string id)
    {
        _powerupId = id;
        if (!string.IsNullOrEmpty(id))
            _visual?.SetPowerupBrick(id);
    }

    public int  CurrentHitPoints  => _hitPoints;
    public bool IsIndestructible  => _isIndestructible;

    public void SetTemplate(BrickTemplate template, BrickSkin skin, Color tint)
    {
        _visual?.SetSkin(skin, tint);

        // Fallback tint on SpriteRenderer when no visual controller
        if (_visual == null && _sr != null)
            _sr.color = tint;
    }

    // Legacy helpers kept for backward compatibility
    public void SetTint(Color tint)
    {
        if (_visual == null && _sr != null)
            _sr.color = tint;
    }

    // -------- Called by Fury Strike — one-shots this brick regardless of HP --------

    public void FuryKill()
    {
        if (_isIndestructible || _isDead) return;
        _hitPoints = 1;
        _isFuryKill = true;
        Hit();
    }

    private bool _isFuryKill;

    // -------- Called by BallController on collision --------

    public void Hit()
    {
        if (_isIndestructible) return;
        if (_isDead) return; // already destroyed this frame — ignore extra hits

        _hitPoints--;

        int totalPoints = GameManager.Instance?.AddScore(_points) ?? 0;
        GameManager.Instance?.IncrementCombo();

        // Spawn score popup showing points gained (use brick's current color)
        Color popupColor = _sr != null ? _sr.color : Color.white;
        ScorePopup.Spawn(transform.position, totalPoints, popupColor);

        if (_hitPoints > 0)
        {
            CameraShake.Instance?.Shake(0.06f, 0.12f);

            _visual?.UpdateDamageState(_hitPoints, _maxHitPoints);
            SfxPlayer.Instance?.PlayBrickHit();
            return;
        }

        // ── Brick destroyed ──────────────────────────────────────────────────
        _isDead = true; // prevent any further hits this frame

        // Achievement tracking
        AchievementManager.Instance?.OnBrickDestroyed();
        if (_isFuryKill)
            AchievementManager.Instance?.OnFuryStrikeBrickDestroyed();
        // Gem brick detection (10,000-point bricks use templateId "gem")
        if (_points >= 10000)
            AchievementManager.Instance?.OnGemBrickDestroyed();

        // If this is the last destructible brick, trigger slow-mo + zoom NOW —
        // right as the fatal hit lands, so the destruction plays in slow motion.
        // BricksRemaining still includes this brick (OnBrickDestroyed hasn't fired yet).
        if (LevelManager.Instance?.BricksRemaining == 1)
            GameManager.Instance?.OnLastBrickRemaining(transform.position);

        // Bigger shake for destruction of a tough brick.
        if (_maxHitPoints > 1)
        {
            CameraShake.Instance?.Shake(0.18f, 0.25f);
        }

        // Particle burst (more particles for tougher bricks)
        int particleCount = _maxHitPoints > 1 ? 35 : 22;
        bool isSpecial = _maxHitPoints > 2;
        BrickParticleGenerator.SpawnBurst(transform.position, popupColor, particleCount, isSpecial);

        _visual?.PlayBreakEffect();
        if (_maxHitPoints > 1)
            SfxPlayer.Instance?.PlayBrickBreakHeavy();
        else
            SfxPlayer.Instance?.PlayBrickBreak();

        if (!string.IsNullOrEmpty(_powerupId))
        {
            // Spawn the powerup orb at this brick's location
            var go = new GameObject("PowerupPickup");
            go.transform.position = transform.position;
            var pickup = go.AddComponent<PowerupPickup>();
            pickup.Init(_powerupId);
        }

        LevelManager.Instance?.OnBrickDestroyed();
        Destroy(gameObject);
    }
}
