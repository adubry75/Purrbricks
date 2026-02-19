using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages persistent high scores (top 10).
/// Saves to PlayerPrefs as JSON.
/// </summary>
public class HighScoreManager : MonoBehaviour
{
    public static HighScoreManager Instance { get; private set; }

    private const string SAVE_KEY = "Purrbricks_HighScores";
    private const int MAX_SCORES = 10;

    private List<ScoreEntry> _scores = new List<ScoreEntry>();

    [Serializable]
    public class ScoreEntry
    {
        public string playerName;
        public int score;

        public ScoreEntry(string name, int score)
        {
            this.playerName = name;
            this.score = score;
        }
    }

    [Serializable]
    private class ScoreList
    {
        public List<ScoreEntry> scores;
    }

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
    }

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
            string json = PlayerPrefs.GetString(SAVE_KEY);
            var wrapper = JsonUtility.FromJson<ScoreList>(json);
            _scores = wrapper?.scores ?? new List<ScoreEntry>();
        }
        else
        {
            // Seed with dummy scores for testing
            _scores = new List<ScoreEntry>
            {
                new ScoreEntry("CLAUDE", 50000),
                new ScoreEntry("PURR", 40000),
                new ScoreEntry("BRICK", 30000),
                new ScoreEntry("MASTER", 20000),
                new ScoreEntry("PLAYER", 10000)
            };
            SaveScores();
        }
    }

    public int GetLowestHighScore()
    {
        return _scores.Count >= MAX_SCORES ? _scores[MAX_SCORES - 1].score : 0;
    }
}
