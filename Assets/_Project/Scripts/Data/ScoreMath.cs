/// <summary>
/// Pure scoring math — no Unity or singleton dependencies, fully testable in isolation.
/// </summary>
public static class ScoreMath
{
    /// <summary>
    /// Computes final points and combo bonus from a base brick value.
    /// </summary>
    /// <param name="basePoints">Raw point value of the brick.</param>
    /// <param name="combo">Current combo counter (0 = no active combo).</param>
    /// <param name="scoreFrenzy">Whether the Score Frenzy powerup is active.</param>
    /// <returns>points: amount added to total score. comboBonus: amount added to level combo bonus tracker.</returns>
    public static (int points, int comboBonus) Calculate(int basePoints, int combo, bool scoreFrenzy)
    {
        int points     = basePoints * (1 + combo);
        int comboBonus = basePoints * combo;

        if (scoreFrenzy)
        {
            points     *= 2;
            comboBonus *= 2;
        }

        return (points, comboBonus);
    }
}
