using System;
using System.Collections.Generic;

/// <summary>Credits only original destructible HP, independently of revive and overkill.</summary>
public sealed class NineLivesAttemptCredit
{
    readonly Dictionary<object, int> remaining = new Dictionary<object, int>();
    long total, damaged;
    int awarded;
    public void Begin(IDictionary<object, int> bricks)
    {
        remaining.Clear(); total = damaged = 0; awarded = 0;
        foreach (var pair in bricks)
        {
            if (pair.Key == null || pair.Value <= 0) continue;
            remaining[pair.Key] = pair.Value; total += pair.Value;
        }
    }
    public int Damage(object brick, int amount)
    {
        if (brick == null || amount <= 0 || !remaining.TryGetValue(brick, out int available)) return 0;
        int taken = Math.Min(amount, available);
        remaining[brick] = available - taken; damaged += taken;
        int earned = total > 0 ? (int)Math.Min(20, damaged * 20 / total) : 0;
        int delta = earned - awarded; awarded = earned; return delta;
    }
}
