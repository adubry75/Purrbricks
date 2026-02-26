public static class PowerupRules
{
    public static bool IsBad(PowerupType type)
    {
        switch (type)
        {
            case PowerupType.ShrinkPaddle:
            case PowerupType.ZipBall:
            case PowerupType.FlipControls:
            case PowerupType.CursedBall:
            case PowerupType.TinyBall:
            case PowerupType.InvisiBall:
            case PowerupType.DrunkenPaddle:
            case PowerupType.DrunkVision:
            case PowerupType.GremlinBounces:
                return true;
            default:
                return false;
        }
    }
}

