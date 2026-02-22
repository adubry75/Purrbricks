using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages persistent high scores:
///   - Global top-10 (all-time, across all levels)
///   - Per-level top-3 (keyed by level ID)
/// Saves to PlayerPrefs as JSON.
/// </summary>
public class HighScoreManager : MonoBehaviour
{
    public static HighScoreManager Instance { get; private set; }

    private const string SAVE_KEY        = "Purrbricks_HighScores";
    private const string LEVEL_SCORES_KEY = "Purrbricks_LevelScores";
    private const int    MAX_SCORES       = 10;
    private const int    MAX_LEVEL_SCORES = 3;

    private List<ScoreEntry>                    _scores      = new List<ScoreEntry>();
    private Dictionary<string, List<ScoreEntry>> _levelScores = new Dictionary<string, List<ScoreEntry>>();

    // ── Shared data types ────────────────────────────────────────────────────

    [Serializable]
    public class ScoreEntry
    {
        public string playerName;
        public int    score;

        public ScoreEntry(string name, int score)
        {
            this.playerName = name;
            this.score      = score;
        }
    }

    [Serializable]
    private class ScoreList
    {
        public List<ScoreEntry> scores;
    }

    // ── Per-level serialization helpers ─────────────────────────────────────

    [Serializable]
    private class LevelScoreData
    {
        public string          levelId;
        public List<ScoreEntry> scores;
    }

    [Serializable]
    private class LevelScoreList
    {
        public List<LevelScoreData> levels;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadScores();
        LoadLevelScores();
    }

    // ── Global high scores ───────────────────────────────────────────────────

    public bool IsHighScore(int score)
    {
        if (_scores.Count < MAX_SCORES) return true;
        return score > _scores[_scores.Count - 1].score;
    }

    public void AddScore(string playerName, int score)
    {
        _scores.Add(new ScoreEntry(playerName, score));
        _scores = _scores.OrderByDescending(s => s.score).Take(MAX_SCORES).ToList();
        SaveScores();
    }

    public List<ScoreEntry> GetTopScores()
    {
        return new List<ScoreEntry>(_scores);
    }

    public int GetLowestHighScore()
    {
        return _scores.Count >= MAX_SCORES ? _scores[MAX_SCORES - 1].score : 0;
    }

    private void SaveScores()
    {
        var wrapper = new ScoreList { scores = _scores };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadScores()
    {
        if (PlayerPrefs.HasKey(SAVE_KEY))
        {
            string json    = PlayerPrefs.GetString(SAVE_KEY);
            var    wrapper = JsonUtility.FromJson<ScoreList>(json);
            _scores = wrapper?.scores ?? new List<ScoreEntry>();
        }
        else
        {
            // Seed with dummy scores for testing
            _scores = new List<ScoreEntry>
            {
                new ScoreEntry("CLAUDE",  50000),
                new ScoreEntry("PURR",    40000),
                new ScoreEntry("BRICK",   30000),
                new ScoreEntry("MASTER",  20000),
                new ScoreEntry("PLAYER",  10000)
            };
            SaveScores();
        }
    }

    // ── Per-level top-3 scores ───────────────────────────────────────────────

    /// <summary>Returns true if score qualifies for the top-3 leaderboard for this level.</summary>
    public bool IsLevelHighScore(string levelId, int score)
    {
        if (string.IsNullOrEmpty(levelId)) return false;
        if (!_levelScores.TryGetValue(levelId, out var list)) return true; // no entries yet
        if (list.Count < MAX_LEVEL_SCORES) return true;
        return score > list[list.Count - 1].score;
    }

    /// <summary>Adds a score to the per-level top-3 and persists immediately.</summary>
    public void AddLevelScore(string levelId, string playerName, int score)
    {
        if (string.IsNullOrEmpty(levelId)) return;

        if (!_levelScores.TryGetValue(levelId, out var list))
        {
            list = new List<ScoreEntry>();
            _levelScores[levelId] = list;
        }

        list.Add(new ScoreEntry(playerName, score));
        _levelScores[levelId] = list
            .OrderByDescending(s => s.score)
            .Take(MAX_LEVEL_SCORES)
            .ToList();

        SaveLevelScores();
    }

    /// <summary>Returns a copy of the top-3 entries for a specific level (may be empty).</summary>
    public List<ScoreEntry> GetTopLevelScores(string levelId)
    {
        if (!string.IsNullOrEmpty(levelId) && _levelScores.TryGetValue(levelId, out var list))
            return new List<ScoreEntry>(list);
        return new List<ScoreEntry>();
    }

    private void SaveLevelScores()
    {
        var data = new LevelScoreList { levels = new List<LevelScoreData>() };
        foreach (var kv in _levelScores)
            data.levels.Add(new LevelScoreData { levelId = kv.Key, scores = kv.Value });

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(LEVEL_SCORES_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadLevelScores()
    {
        _levelScores = new Dictionary<string, List<ScoreEntry>>();
        if (!PlayerPrefs.HasKey(LEVEL_SCORES_KEY)) return;

        string json = PlayerPrefs.GetString(LEVEL_SCORES_KEY);
        var    data = JsonUtility.FromJson<LevelScoreList>(json);
        if (data?.levels == null) return;

        foreach (var ld in data.levels)
        {
            if (!string.IsNullOrEmpty(ld.levelId))
                _levelScores[ld.levelId] = ld.scores ?? new List<ScoreEntry>();
        }
    }
}
