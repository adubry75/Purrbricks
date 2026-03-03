using System;

public static class PurrbricksLeaderboards
{
    public const string OverallAllTime = "Purrbricks_HighScores";

    public static string LevelAllTime(int levelIndex) => $"Purrbricks_level_{levelIndex:D2}";

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

