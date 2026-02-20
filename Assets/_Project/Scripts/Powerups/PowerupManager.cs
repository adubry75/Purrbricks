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
            OnPowerupsChanged?.Invoke();
    }

    /// <summary>Called when the player picks up a powerup.</summary>
    public void Apply(PowerupType type)
    {
        if (type == PowerupType.ExtraLife)
        {
            // Instant, no timer
            GameManager.Instance?.AddLife();
            return;
        }

        if (type == PowerupType.MultiBall)
        {
            // Instant effect, no timer
            SpawnMultiBalls();
            return;
        }

        bool wasActive = _timers.ContainsKey(type);

        // Stack: add to existing time or start fresh
        if (wasActive)
            _timers[type] += POWERUP_DURATION;
        else
        {
            _timers[type] = POWERUP_DURATION;
            ApplyEffect(type);
        }

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
        OnPowerupsChanged?.Invoke();
    }

    // ── Apply / Remove effects ────────────────────────────────────────────────

    private void ApplyEffect(PowerupType type)
    {
        if (_paddle == null)
            _paddle = FindFirstObjectByType<PaddleController>();

        switch (type)
        {
            case PowerupType.WidePaddle:    _paddle?.SetWide(true);                             break;
            case PowerupType.StickyBall:    foreach (var b in _balls) b.SetSticky(true);        break;
            case PowerupType.SpeedBall:     foreach (var b in _balls) b.SetSpeedBoost(true);    break;
            case PowerupType.Laser:         _paddle?.SetLaser(true);                            break;
            case PowerupType.Fireball:      foreach (var b in _balls) b.SetFireball(true);      break;
            case PowerupType.BombBrick:     foreach (var b in _balls) b.SetBomb(true);          break;
            case PowerupType.ShrinkPaddle:  _paddle?.SetShrink(true);                          break;
            case PowerupType.ZipBall:       foreach (var b in _balls) b.SetZipBall(true);      break;
            case PowerupType.FlipControls:  _paddle?.SetFlipped(true);                         break;
            case PowerupType.CursedBall:    foreach (var b in _balls) b.SetCursed(true);       break;
        }
    }

    private void RemoveEffect(PowerupType type)
    {
        if (_paddle == null)
            _paddle = FindFirstObjectByType<PaddleController>();

        switch (type)
        {
            case PowerupType.WidePaddle:    _paddle?.SetWide(false);                            break;
            case PowerupType.StickyBall:    foreach (var b in _balls) b.SetSticky(false);       break;
            case PowerupType.SpeedBall:     foreach (var b in _balls) b.SetSpeedBoost(false);   break;
            case PowerupType.Laser:         _paddle?.SetLaser(false);                           break;
            case PowerupType.Fireball:      foreach (var b in _balls) b.SetFireball(false);     break;
            case PowerupType.BombBrick:     foreach (var b in _balls) b.SetBomb(false);         break;
            case PowerupType.ShrinkPaddle:  _paddle?.SetShrink(false);                         break;
            case PowerupType.ZipBall:       foreach (var b in _balls) b.SetZipBall(false);     break;
            case PowerupType.FlipControls:  _paddle?.SetFlipped(false);                        break;
            case PowerupType.CursedBall:    foreach (var b in _balls) b.SetCursed(false);      break;
        }
    }

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
