using NUnit.Framework;

public class ScoreMathTests
{
    // ── No combo ─────────────────────────────────────────────────────────────

    [Test]
    public void NoCombo_NoFrenzy_ReturnsBasePoints()
    {
        var (points, comboBonus) = ScoreMath.Calculate(100, 0, false);
        Assert.AreEqual(100, points);
        Assert.AreEqual(0, comboBonus);
    }

    [Test]
    public void NoCombo_Frenzy_DoublesPoints_NoBonusChange()
    {
        var (points, comboBonus) = ScoreMath.Calculate(100, 0, true);
        Assert.AreEqual(200, points);
        Assert.AreEqual(0, comboBonus);
    }

    // ── Combo only ───────────────────────────────────────────────────────────

    [Test]
    public void Combo1_NoFrenzy_DoublesPoints()
    {
        var (points, comboBonus) = ScoreMath.Calculate(100, 1, false);
        Assert.AreEqual(200, points);
        Assert.AreEqual(100, comboBonus);
    }

    [Test]
    public void Combo2_NoFrenzy_TriplesPoints()
    {
        var (points, comboBonus) = ScoreMath.Calculate(100, 2, false);
        Assert.AreEqual(300, points);
        Assert.AreEqual(200, comboBonus);
    }

    [Test]
    public void Combo10_NoFrenzy_ScalesCorrectly()
    {
        var (points, comboBonus) = ScoreMath.Calculate(100, 10, false);
        Assert.AreEqual(1100, points);   // 100 * 11
        Assert.AreEqual(1000, comboBonus);
    }

    // ── Combo + frenzy ───────────────────────────────────────────────────────

    [Test]
    public void Combo2_Frenzy_SixTimesBase()
    {
        var (points, comboBonus) = ScoreMath.Calculate(100, 2, true);
        Assert.AreEqual(600, points);    // 100 * 3 * 2
        Assert.AreEqual(400, comboBonus); // 100 * 2 * 2
    }

    [Test]
    public void Combo1_Frenzy_FourTimesBase()
    {
        var (points, comboBonus) = ScoreMath.Calculate(100, 1, true);
        Assert.AreEqual(400, points);    // 100 * 2 * 2
        Assert.AreEqual(200, comboBonus); // 100 * 1 * 2
    }

    // ── Invariant: comboBonus == points - basePoints (no frenzy) ─────────────

    [Test]
    public void ComboBonusEqualsPointsMinusBase_WhenNoFrenzy()
    {
        int basePoints = 500;
        var (points, comboBonus) = ScoreMath.Calculate(basePoints, 4, false);
        Assert.AreEqual(points - basePoints, comboBonus);
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Test]
    public void ZeroBasePoints_AlwaysZero()
    {
        var (points, comboBonus) = ScoreMath.Calculate(0, 5, true);
        Assert.AreEqual(0, points);
        Assert.AreEqual(0, comboBonus);
    }

    [Test]
    public void GemBrick_NoCombo_NoFrenzy_TenThousandPoints()
    {
        // gem template = 10000 pts — sanity check on a real game value
        var (points, comboBonus) = ScoreMath.Calculate(10000, 0, false);
        Assert.AreEqual(10000, points);
        Assert.AreEqual(0, comboBonus);
    }

    [Test]
    public void GemBrick_Combo5_Frenzy_ScalesCorrectly()
    {
        // combo=5 → multiplier=6, frenzy doubles → 10000 * 6 * 2 = 120000
        var (points, comboBonus) = ScoreMath.Calculate(10000, 5, true);
        Assert.AreEqual(120000, points);
        Assert.AreEqual(100000, comboBonus); // 10000 * 5 * 2
    }
}
