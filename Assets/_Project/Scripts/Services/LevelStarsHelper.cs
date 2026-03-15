using System.Linq;
using UnityEngine;

/// <summary>
/// Static helpers for the 1-3 performance-star rating system.
/// Stars are calculated against a level's "par" score (sum of base points for all destructible bricks).
///   1 star  = completed
///   2 stars = score >= 1.5x par
///   3 stars = score >= 3x par
/// Best star count per level is persisted to PlayerPrefs and never downgraded.
/// </summary>
public static class LevelStarsHelper
{
    private const string PREF_PREFIX = "perf_stars_";

    /// <summary>Returns the player's best star count (0–3) for the given levelId.</summary>
    public static int GetBestStars(string levelId)
        => PlayerPrefs.GetInt(PREF_PREFIX + levelId, 0);

    /// <summary>Saves star count only if it improves on the stored best.</summary>
    public static void SaveBestStars(string levelId, int stars)
    {
        if (string.IsNullOrEmpty(levelId)) return;
        int current = GetBestStars(levelId);
        if (stars > current)
        {
            PlayerPrefs.SetInt(PREF_PREFIX + levelId, stars);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Converts a raw score + par into a 1–3 star rating.</summary>
    public static int CalculateStars(int score, int par)
    {
        if (par <= 0 || score <= 0) return 1;
        float ratio = score / (float)par;
        if (ratio >= 3f)   return 3;
        if (ratio >= 1.5f) return 2;
        return 1;
    }

    /// <summary>Returns true if every level in the supplied ID list has a saved 3-star rating.</summary>
    public static bool AllLevelsThreeStarred(string[] levelIds)
    {
        if (levelIds == null || levelIds.Length == 0) return false;
        return levelIds.All(id => GetBestStars(id) >= 3);
    }
}
