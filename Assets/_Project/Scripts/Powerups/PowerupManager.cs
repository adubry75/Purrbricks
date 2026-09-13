using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all active powerup timers and their effects.
/// Applying the same powerup before it expires stacks the duration.
/// </summary>
public class PowerupManager : MonoBehaviour
{
    [SerializeField] private GameObject _wallBottom;

    public static PowerupManager Instance { get; private set; }

    public const float POWERUP_DURATION = 10f;

    // Active timed powerup remaining durations
    private readonly Dictionary<PowerupType, float> _timers = new Dictionary<PowerupType, float>();
    private readonly List<PowerupType> _timerKeys = new List<PowerupType>(22);
    private readonly List<PowerupType> _expiredTimers = new List<PowerupType>(22);

    // Event so PowerupHUD can refresh
    public System.Action OnPowerupsChanged;

    // Fired when a 2% inventory drop triggers — PowerupHUD plays the fly-in VFX
    public System.Action<PowerupType> OnInventoryDrop;

    private PaddleController _paddle;
    private IReadOnlyCollection<BallController> _balls => BallController.ActiveBalls;

    // ShieldWall visual/physics object
    private GameObject _shieldWallGO;
    private bool _shieldWallWasActive;
    private ShieldWallFx _shieldWallFx;

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
        if (_timers.Count == 0 || (GameManager.Instance != null && GameManager.Instance.IsGameplaySuspended)) return;

        var toRemove = _expiredTimers; toRemove.Clear();
        var keys = _timerKeys; keys.Clear(); keys.AddRange(_timers.Keys);

        foreach (var type in keys)
        {
            if (float.IsInfinity(_timers[type]))
                continue;

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
            NotifyBadPowerupCount();
            OnPowerupsChanged?.Invoke();
        }
    }

    private enum ApplicationSource { World, Inventory, Generated }

    /// <summary>Called when the player catches a falling world pickup.</summary>
    public void Apply(PowerupType type)
    {
        if (ApplyInternal(type, ApplicationSource.World))
            NineLivesService.Instance?.NotifyWorldPickup(type);
    }

    /// <summary>Generated/debug effects cannot earn pickup credit or inventory drops.</summary>
    public void ApplyGenerated(PowerupType type) => ApplyInternal(type, ApplicationSource.Generated);

    public void GrantMultiBall()
    {
        SpawnMultiBalls();
        NotifyBallCount();
    }

    /// <summary>Grant a timed effect without adding duration or treating it as a pickup.</summary>
    public void GrantAtLeast(PowerupType type, float seconds)
    {
        if (!System.Enum.IsDefined(typeof(PowerupType), type) || !IsTimed(type)
            || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f
            || (type == PowerupType.ShieldWall && _wallBottom == null)) return;
        bool wasActive = IsActive(type);
        RemoveOpposingSize(type);
        _timers[type] = Mathf.Max(GetRemaining(type), seconds);
        if (!wasActive) ApplyEffect(type);
        UpdateBadVignette();
        OnPowerupsChanged?.Invoke();
    }

    private static bool IsTimed(PowerupType type) => type != PowerupType.ExtraLife
        && type != PowerupType.MultiBall && type != PowerupType.PermanentStickyBall;

    private static bool IsBeneficialTimed(PowerupType type)
    {
        switch (type)
        {
            case PowerupType.WidePaddle: case PowerupType.StickyBall:
            case PowerupType.SpeedBall: case PowerupType.Laser:
            case PowerupType.Fireball: case PowerupType.BombBrick:
            case PowerupType.ShieldWall: case PowerupType.BigBall:
            case PowerupType.ScoreFrenzy: return true;
            default: return false;
        }
    }

    private void RemoveOpposingSize(PowerupType type)
    {
        if (type != PowerupType.BigBall && type != PowerupType.TinyBall) return;
        var opposing = type == PowerupType.BigBall ? PowerupType.TinyBall : PowerupType.BigBall;
        if (_timers.Remove(opposing)) RemoveEffect(opposing);
    }

    public bool CanApplyFromInventory(PowerupType type, out string reason)
    {
        reason = null;
        var gm = GameManager.Instance;
        if (!System.Enum.IsDefined(typeof(PowerupType), type)) reason = "Unknown powerup";
        else if (gm == null || !gm.IsPlayingOrReady() || gm.IsDemoMode || gm.IsEditorTestMode)
            reason = "Available during a game";
        else if (TutorialManager.Instance != null && TutorialManager.Instance.IsShowing)
            reason = "Close the tutorial first";
        else if (type == PowerupType.MultiBall && FindLaunchedBall() == null)
            reason = "Launch a ball first";
        else if (type == PowerupType.ShieldWall && _wallBottom == null)
            reason = "Shield unavailable on this board";
        return reason == null;
    }

    public bool TryApplyFromInventory(PowerupType type)
    {
        if (!CanApplyFromInventory(type, out _) || GameManager.Instance.IsInventoryUseBlocked) return false;
        return ApplyInternal(type, ApplicationSource.Inventory);
    }

    private bool ApplyInternal(PowerupType type, ApplicationSource source)
    {
        if (!System.Enum.IsDefined(typeof(PowerupType), type)
            || (type == PowerupType.ShieldWall && _wallBottom == null)) return false;
        if (type == PowerupType.ExtraLife)
        {
            GameManager.Instance?.AddLife();
            PowerupNotification.Instance?.ShowPowerup(type);
            AchievementManager.Instance?.OnExtraLifePickup();
            return true;
        }

        if (type == PowerupType.MultiBall)
        {
            SpawnMultiBalls();
            PowerupNotification.Instance?.ShowPowerup(type);
            // Ball count check is deferred — SpawnClone calls GameManager.RegisterClone;
            // we check after spawn via a one-frame delayed count.
            // Instead we notify after spawning so the count is up to date.
            NotifyBallCount();
            TutorialManager.Instance?.TriggerIfNew(
                TutorialManager.ID.MultiBallFury,
                "● ● ●",
                "MULTI-BALL ACTIVE!",
                "Multiple balls are in play!\n\nWhen you unleash FURY STRIKE,\nEVERY ball explodes simultaneously —\nmassive combo bonus and destruction!");
            return true;
        }

        if (type == PowerupType.PermanentStickyBall)
        {
            if (!_timers.ContainsKey(type))
            {
                _timers[type] = float.PositiveInfinity;
                ApplyEffect(type);
            }

            PowerupNotification.Instance?.ShowPowerup(type);
            UpdateBadVignette();
            NotifyBadPowerupCount();
            OnPowerupsChanged?.Invoke();
            return true;
        }

        bool wasActive = _timers.ContainsKey(type);
        RemoveOpposingSize(type);
        float duration = POWERUP_DURATION;
        var upgrades = NineLivesService.Instance;
        if (source != ApplicationSource.Generated && upgrades != null)
        {
            bool beneficial = IsBeneficialTimed(type);
            if (beneficial && upgrades.Has("B1")) duration *= 1.25f;
            if (IsBadPowerup(type) && upgrades.Has("C3")) duration *= 0.8f;
            if (source == ApplicationSource.World && beneficial && wasActive && upgrades.Has("B3"))
                duration += 3f;
        }

        if (wasActive)
            _timers[type] += duration;
        else
        {
            _timers[type] = duration;
            ApplyEffect(type);
        }

        // Fanfare notification (show even on stack refresh)
        PowerupNotification.Instance?.ShowPowerup(type);

        // Bad powerup vignette
        UpdateBadVignette();
        NotifyBadPowerupCount();

        OnPowerupsChanged?.Invoke();

        // 2% inventory drop roll (not on ExtraLife/MultiBall — those early-return above)
        if (source == ApplicationSource.World && PurrBucksManager.Instance != null && PurrBucksManager.Instance.RollInventoryDrop(type))
            OnInventoryDrop?.Invoke(type);
        return true;
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
        _timers.Clear();
        foreach (var t in types)
            RemoveEffect(t);
        ScreenEffects.Instance?.SetBadVignette(false);
        OnPowerupsChanged?.Invoke();
    }

    private void UpdateBadVignette()
    {
        bool anyBad = false;
        foreach (var kvp in _timers)
        {
            if (IsBadPowerup(kvp.Key)) { anyBad = true; break; }
        }
        ScreenEffects.Instance?.SetBadVignette(anyBad);
    }

    private void NotifyBadPowerupCount()
    {
        int count = 0;
        foreach (var kvp in _timers)
            if (IsBadPowerup(kvp.Key)) count++;
        AchievementManager.Instance?.OnBadPowerupCountChanged(count);
    }

    private static bool IsBadPowerup(PowerupType type)
        => PowerupRules.IsBad(type);

    private void NotifyBallCount()
    {
        // Count all active BallControllers in the scene
        int count = BallController.ActiveBalls.Count;
        AchievementManager.Instance?.OnBallCountChanged(count);
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
            case PowerupType.PermanentStickyBall: foreach (var b in _balls) b.SetSticky(true);   break;
            case PowerupType.DrunkVision:
                CameraShake.Instance?.SetDrunk(true);
                ScreenEffects.Instance?.SetDrunkVision(true);
                break;
            case PowerupType.GremlinBounces: foreach (var b in _balls) b.SetGremlinBounces(true); break;
            case PowerupType.FlipScreen:     CameraShake.Instance?.SetFlipScreen(true);            break;
        }
    }

    private void RemoveEffect(PowerupType type)
    {
        if (_paddle == null)
            _paddle = FindFirstObjectByType<PaddleController>();

        switch (type)
        {
            case PowerupType.WidePaddle:    _paddle?.SetWide(false);                              break;
            case PowerupType.StickyBall:    foreach (var b in _balls) b.SetSticky(IsActive(PowerupType.StickyBall) || IsActive(PowerupType.PermanentStickyBall));        break;
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
            case PowerupType.PermanentStickyBall: foreach (var b in _balls) b.SetSticky(IsActive(PowerupType.StickyBall) || IsActive(PowerupType.PermanentStickyBall));  break;
            case PowerupType.DrunkVision:
                CameraShake.Instance?.SetDrunk(false);
                ScreenEffects.Instance?.SetDrunkVision(false);
                break;
            case PowerupType.GremlinBounces: foreach (var b in _balls) b.SetGremlinBounces(false); break;
            case PowerupType.FlipScreen:     CameraShake.Instance?.SetFlipScreen(false);            break;
        }
    }

    // ── ShieldWall ────────────────────────────────────────────────────────────

    private void SpawnShieldWall()
    {
        if (_shieldWallGO != null) return;

        // Use the existing scene wall (requested) so collisions match the level setup.
        

        _shieldWallGO = _wallBottom;
        _shieldWallWasActive = _wallBottom.activeSelf;
        _wallBottom.SetActive(true);

        _shieldWallFx = _wallBottom.GetComponent<ShieldWallFx>();
        if (_shieldWallFx == null)
            _shieldWallFx = _wallBottom.AddComponent<ShieldWallFx>();
        _shieldWallFx.enabled = true;
    }

    private void DestroyShieldWall()
    {
        if (_shieldWallGO == null) return;

        if (_shieldWallFx != null)
            _shieldWallFx.enabled = false;

        // Restore whatever active state the wall had before the powerup.
        _shieldWallGO.SetActive(_shieldWallWasActive);

        _shieldWallGO = null;
        _shieldWallFx = null;
    }

    // ── MultiBall ─────────────────────────────────────────────────────────────

    private BallController FindLaunchedBall()
    {
        foreach (var ball in _balls)
            if (ball != null && ball.IsLaunched()) return ball;
        return null;
    }

    private void SpawnMultiBalls()
    {
        // Find any active ball and clone it twice at different angles
        var existingBall = FindLaunchedBall();
        if (existingBall == null || !existingBall.IsLaunched()) return;

        for (int i = 0; i < 2; i++)
        {
            float angleOffset = (i == 0) ? -35f : 35f;
            existingBall.SpawnClone(angleOffset);
        }
    }
}
