public enum PowerupType
{
    // ── Good powerups ───────────────────────────────
    WidePaddle   = 0,
    MultiBall    = 1,
    StickyBall   = 2,
    SpeedBall    = 3,
    ExtraLife    = 4,
    Laser        = 5,
    Fireball     = 6,   // pierces through bricks with < 5 HP
    BombBrick    = 7,   // explodes 3×3 area on hit

    // ── Bad powerups ────────────────────────────────
    ShrinkPaddle = 8,   // paddle shrinks to half width
    ZipBall      = 9,   // ball moves at 2.5× speed
    FlipControls = 10,  // paddle movement is mirrored
    CursedBall   = 11,  // ball drifts/curves unpredictably
}
