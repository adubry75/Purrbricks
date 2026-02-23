using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all active powerup timers and their effects.
/// Applying the same powerup before it expires stacks the duration.
/// </summary>
public class PowerupManager : MonoBehaviour
{
    public static PowerupManager Instance { get; private set; }

    public const float POWERUP_DURATION = 10f;

    // Active timed powerup remaining durations
    private readonly Dictionary<PowerupType, float> _timers = new Dictionary<PowerupType, float>();

    // Event so PowerupHUD can refresh
    public System.Action OnPowerupsChanged;

    private PaddleController _paddle;
    private BallController[] _balls => FindObjectsByType<BallController>(FindObjectsSortMode.None);

    // ShieldWall visual/physics object
    private GameObject _shieldWallGO;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _paddle = FindFirstObjectByType<PaddleController>();
    }

    private void Update()
    {
        if (_timers.Count == 0) return;

        var toRemove = new List<PowerupType>();
        var keys = new List<PowerupType>(_timers.Keys);

        foreach (var type in keys)
        {
            _timers[type] -= Time.deltaTime;
            if (_timers[type] <= 0f)
                toRemove.Add(type);
        }

        bool changed = false;
        foreach (var type in toRemove)
        {
            _timers.Remove(type);
            RemoveEffect(type);
            changed = true;
        }

        if (changed)
        {
            UpdateBadVignette();
            OnPowerupsChanged?.Invoke();
        }
    }

    /// <summary>Called when the player picks up a powerup.</summary>
    public void Apply(PowerupType type)
    {
        if (type == PowerupType.ExtraLife)
        {
            GameManager.Instance?.AddLife();
            PowerupNotification.Instance?.ShowPowerup(type);
            return;
        }

        if (type == PowerupType.MultiBall)
        {
            SpawnMultiBalls();
            PowerupNotification.Instance?.ShowPowerup(type);
            return;
        }

        bool wasActive = _timers.ContainsKey(type);

        if (wasActive)
            _timers[type] += POWERUP_DURATION;
        else
        {
            _timers[type] = POWERUP_DURATION;
            ApplyEffect(type);
        }

        // Fanfare notification (show even on stack refresh)
        PowerupNotification.Instance?.ShowPowerup(type);

        // Bad powerup vignette
        UpdateBadVignette();

        OnPowerupsChanged?.Invoke();
    }

    /// <summary>Returns remaining seconds for an active powerup, or 0 if inactive.</summary>
    public float GetRemaining(PowerupType type)
    {
        return _timers.TryGetValue(type, out float t) ? t : 0f;
    }

    public bool IsActive(PowerupType type) => _timers.ContainsKey(type);

    public Dictionary<PowerupType, float> GetAllTimers() => _timers;

    /// <summary>Clear everything (level reset / game over).</summary>
    public void ResetAll()
    {
        var types = new List<PowerupType>(_timers.Keys);
        foreach (var t in types)
            RemoveEffect(t);
        _timers.Clear();
        ScreenEffects.Instance?.SetBadVignette(false);
        OnPowerupsChanged?.Invoke();
    }

    private void UpdateBadVignette()
    {
        bool anyBad = false;
        foreach (var kvp in _timers)
        {
            if ((int)kvp.Key >= 11) { anyBad = true; break; }
        }
        ScreenEffects.Instance?.SetBadVignette(anyBad);
    }

    // ── Apply / Remove effects ────────────────────────────────────────────────

    private void ApplyEffect(PowerupType type)
    {
        if (_paddle == null)
            _paddle = FindFirstObjectByType<PaddleController>();

        switch (type)
        {
            case PowerupType.WidePaddle:    _paddle?.SetWide(true);                              break;
            case PowerupType.StickyBall:    foreach (var b in _balls) b.SetSticky(true);         break;
            case PowerupType.SpeedBall:     foreach (var b in _balls) b.SetSpeedBoost(true);     break;
            case PowerupType.Laser:         _paddle?.SetLaser(true);                             break;
            case PowerupType.Fireball:      foreach (var b in _balls) b.SetFireball(true);       break;
            case PowerupType.BombBrick:     foreach (var b in _balls) b.SetBomb(true);           break;
            case PowerupType.ShieldWall:    SpawnShieldWall();                                    break;
            case PowerupType.BigBall:       foreach (var b in _balls) b.SetBigBall(true);        break;
            case PowerupType.ScoreFrenzy:   GameManager.Instance?.SetScoreFrenzy(true);          break;
            case PowerupType.ShrinkPaddle:  _paddle?.SetShrink(true);                            break;
            case PowerupType.ZipBall:       foreach (var b in _balls) b.SetZipBall(true);        break;
            case PowerupType.FlipControls:  _paddle?.SetFlipped(true);                           break;
            case PowerupType.CursedBall:    foreach (var b in _balls) b.SetCursed(true);         break;
            case PowerupType.TinyBall:      foreach (var b in _balls) b.SetTinyBall(true);       break;
            case PowerupType.InvisiBall:    foreach (var b in _balls) b.SetInvisiBall(true);     break;
            case PowerupType.DrunkenPaddle: _paddle?.SetDrunk(true);                             break;
        }
    }

    private void RemoveEffect(PowerupType type)
    {
        if (_paddle == null)
            _paddle = FindFirstObjectByType<PaddleController>();

        switch (type)
        {
            case PowerupType.WidePaddle:    _paddle?.SetWide(false);                              break;
            case PowerupType.StickyBall:    foreach (var b in _balls) b.SetSticky(false);        break;
            case PowerupType.SpeedBall:     foreach (var b in _balls) b.SetSpeedBoost(false);    break;
            case PowerupType.Laser:         _paddle?.SetLaser(false);                            break;
            case PowerupType.Fireball:      foreach (var b in _balls) b.SetFireball(false);      break;
            case PowerupType.BombBrick:     foreach (var b in _balls) b.SetBomb(false);          break;
            case PowerupType.ShieldWall:    DestroyShieldWall();                                  break;
            case PowerupType.BigBall:       foreach (var b in _balls) b.SetBigBall(false);       break;
            case PowerupType.ScoreFrenzy:   GameManager.Instance?.SetScoreFrenzy(false);         break;
            case PowerupType.ShrinkPaddle:  _paddle?.SetShrink(false);                           break;
            case PowerupType.ZipBall:       foreach (var b in _balls) b.SetZipBall(false);       break;
            case PowerupType.FlipControls:  _paddle?.SetFlipped(false);                          break;
            case PowerupType.CursedBall:    foreach (var b in _balls) b.SetCursed(false);        break;
            case PowerupType.TinyBall:      foreach (var b in _balls) b.SetTinyBall(false);      break;
            case PowerupType.InvisiBall:    foreach (var b in _balls) b.SetInvisiBall(false);    break;
            case PowerupType.DrunkenPaddle: _paddle?.SetDrunk(false);                            break;
        }
    }

    // ── ShieldWall ────────────────────────────────────────────────────────────

    private void SpawnShieldWall()
    {
        if (_shieldWallGO != null) return;

        // Find death zone position for accurate placement
        float shieldY = -8.3f;
        var deathZone = FindFirstObjectByType<DeathZone>();
        if (deathZone != null)
            shieldY = deathZone.transform.position.y + 0.5f;

        _shieldWallGO = new GameObject("ShieldWall");

        // Create 1×1 white texture for the sprite
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        var sr = _shieldWallGO.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.0f, 0.9f, 1.0f, 0.55f); // glowing cyan
        sr.sortingOrder = 5;

        // BoxCollider so ball bounces off it
        var col = _shieldWallGO.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        // Scale to fill the playfield width
        _shieldWallGO.transform.localScale = new Vector3(14f, 0.25f, 1f);
        _shieldWallGO.transform.position = new Vector3(0f, shieldY, 0f);
    }

    private void DestroyShieldWall()
    {
        if (_shieldWallGO != null)
        {
            Destroy(_shieldWallGO);
            _shieldWallGO = null;
        }
    }

    // ── MultiBall ─────────────────────────────────────────────────────────────

    private void SpawnMultiBalls()
    {
        // Find any active ball and clone it twice at different angles
        var existingBall = FindFirstObjectByType<BallController>();
        if (existingBall == null || !existingBall.IsLaunched()) return;

        for (int i = 0; i < 2; i++)
        {
            float angleOffset = (i == 0) ? -35f : 35f;
            existingBall.SpawnClone(angleOffset);
        }
    }
}
