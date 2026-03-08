/// <summary>
/// Central configuration for the Purr Bucks economy.
/// All tunable values live here so nothing is hardcoded elsewhere.
/// </summary>
public static class PurrBucksConfig
{
    // ── Earning ───────────────────────────────────────────────────────────────
    /// <summary>Global Steam rank 1 on the level leaderboard.</summary>
    public const int REWARD_FIRST_PLACE   = 50;
    /// <summary>Global Steam rank 2 on the level leaderboard.</summary>
    public const int REWARD_SECOND_PLACE  = 35;
    /// <summary>Global Steam rank 3 on the level leaderboard.</summary>
    public const int REWARD_THIRD_PLACE   = 20;
    /// <summary>Awarded to everyone for completing a level (floor).</summary>
    public const int REWARD_PARTICIPATION = 10;
    /// <summary>Bonus for clearing without losing a life.</summary>
    public const int REWARD_PERFECT_CLEAR = 15;
    /// <summary>One-time discovery bonus for clearing a level for the first time.</summary>
    public const int REWARD_FIRST_TIME    = 10;

    // ── Inventory Drop ────────────────────────────────────────────────────────
    /// <summary>Probability that catching a powerup also drops a copy into inventory.</summary>
    public const float INVENTORY_DROP_CHANCE       = 0.02f;
    /// <summary>Whether bad powerups are eligible for the inventory drop.</summary>
    public const bool  INVENTORY_DROP_BAD_POWERUPS = true;

    // ── Store Prices — Good Powerups ─────────────────────────────────────────
    public const int PRICE_EXTRA_LIFE        = 500;
    public const int PRICE_FIREBALL          = 500;
    public const int PRICE_LASER             = 400;
    public const int PRICE_SHIELD_WALL       = 400;
    public const int PRICE_WIDE_PADDLE       = 100;
    public const int PRICE_MULTI_BALL        = 100;
    public const int PRICE_STICKY_BALL       = 100;
    public const int PRICE_SPEED_BALL        = 100;
    public const int PRICE_BOMB_BRICK        = 100;
    public const int PRICE_BIG_BALL          = 100;
    public const int PRICE_SCORE_FRENZY      = 100;
    public const int PRICE_PERMANENT_STICKY  = 100;

    // ── Store Prices — Bad (Cursed) Powerups ─────────────────────────────────
    /// <summary>All nine bad powerups share the same discounted price.</summary>
    public const int PRICE_CURSED            = 50;
}
