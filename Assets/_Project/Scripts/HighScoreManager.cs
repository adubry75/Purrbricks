using UnityEngine;

/// <summary>
/// Stores one personal-best score per level in PlayerPrefs.
/// No player names, no global leaderboard — those live on Steam now.
/// </summary>
public class HighScoreManager : MonoBehaviour
{
    public static HighScoreManager Instance { get; private set; }

    private const string PB_PREFIX = "pb_";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Returns the personal best score for the given level (0 if never played).</summary>
    public int GetPersonalBest(string levelId)
    {
        if (string.IsNullOrEmpty(levelId)) return 0;
        return PlayerPrefs.GetInt(PB_PREFIX + levelId, 0);
    }

    /// <summary>Saves <paramref name="score"/> as the new personal best only if it is higher.</summary>
    public void UpdatePersonalBest(string levelId, int score)
    {
        if (string.IsNullOrEmpty(levelId)) return;
        string key = PB_PREFIX + levelId;
        if (score > PlayerPrefs.GetInt(key, 0))
        {
            PlayerPrefs.SetInt(key, score);
            PlayerPrefs.Save();
        }
    }
}
