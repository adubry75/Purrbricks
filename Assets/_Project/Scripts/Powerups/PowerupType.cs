public enum PowerupType
{
    // ── Good powerups ───────────────────────────────
    WidePaddle    = 0,
    MultiBall     = 1,
    StickyBall    = 2,
    SpeedBall     = 3,
    ExtraLife     = 4,
    Laser         = 5,
    Fireball      = 6,   // pierces through bricks with < 5 HP
    BombBrick     = 7,   // explodes 3×3 area on hit
    ShieldWall    = 8,   // cyan force-field bar above death zone for 10 s
    BigBall       = 9,   // ball scales to 2× size for 10 s
    ScoreFrenzy   = 10,  // 2× score multiplier for 10 s

    // ── Bad powerups ────────────────────────────────
    ShrinkPaddle  = 11,  // paddle shrinks to half width
    ZipBall       = 12,  // ball moves at 2.5× speed
    FlipControls  = 13,  // paddle movement is mirrored
    CursedBall    = 14,  // ball drifts/curves unpredictably
    TinyBall      = 15,  // ball scales to 0.5× — terrifying
    InvisiBall    = 16,  // ball alpha drops to 0.05; flashes every 3 s
    DrunkenPaddle = 17,  // sinusoidal sway added to paddle target X

    // Special
    PermanentStickyBall = 18, // sticky ball for the rest of the level once collected

    // ── Extra bad (chaos) ─────────────────────────
    DrunkVision    = 19,  // screen wobble/tilt while active
    GremlinBounces = 20,  // small random angle errors on paddle/wall bounces
}
