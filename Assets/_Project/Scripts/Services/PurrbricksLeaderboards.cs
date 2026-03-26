using System;

public static class PurrbricksLeaderboards
{
    public const string OverallAllTime = "Purrbricks_HighScores";

    public static string LevelAllTime(int levelIndex) => $"Purrbricks_level_{levelIndex:D2}";

    /// <summary>All-time board for a community level. Max 30 chars: "Purrbricks_cl_99999_at" = 22 chars.</summary>
    public static string CommunityAllTime(int communityId) => $"Purrbricks_cl_{communityId}_at";

    /// <summary>
    /// Returns a date-scoped board name for Daily or Weekly scope.
    /// NOTE: Only call this for the OVERALL board (OverallAllTime) and community boards.
    /// Per-level Daily/Weekly boards now use MySQL via LevelScoreService — do NOT call
    /// Scoped() with LevelAllTime() board names.
    /// </summary>
    public static string Scoped(string allTimeBoardName, LeaderboardTimeScope scope)
    {
        if (string.IsNullOrEmpty(allTimeBoardName)) return allTimeBoardName;
        if (scope == LeaderboardTimeScope.AllTime) return allTimeBoardName;

        DateTime utcNow = DateTime.UtcNow;

        return scope switch
        {
            LeaderboardTimeScope.Daily  => $"{allTimeBoardName}_Daily_{utcNow:yyyyMMdd}",
            LeaderboardTimeScope.Weekly => $"{allTimeBoardName}_Weekly_{GetUtcWeekStart(utcNow):yyyyMMdd}",
            _ => allTimeBoardName,
        };
    }

    public static DateTime GetUtcWeekStart(DateTime utcNow)
    {
        utcNow = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
        int daysSinceSunday = (int)utcNow.DayOfWeek; // Sunday = 0
        return utcNow.Date.AddDays(-daysSinceSunday);
    }
}

