using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Handles MySQL-backed daily and weekly per-level leaderboards.
/// All-time leaderboards remain on Steam — this service is for Daily/Weekly only.
/// </summary>
public class LevelScoreService : MonoBehaviour
{
    public static LevelScoreService Instance { get; private set; }

    [SerializeField] private string _apiBaseUrl = "https://dubry.com/purrbricks-api/scores";

    private const int TIMEOUT_SECONDS = 8;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public types ───────────────────────────────────────────────────────────

    public struct SubmitResult
    {
        /// <summary>1, 2, or 3 if player is top-3 today; 0 otherwise.</summary>
        public int DailyRank;
        /// <summary>1, 2, or 3 if player is top-3 this week; 0 otherwise.</summary>
        public int WeeklyRank;
    }

    public struct ScoreEntry
    {
        public int    Rank;
        public ulong  SteamId;
        public string SteamName;
        public int    Score;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Submit a score. Callback receives daily and weekly rank (0 = not top-3 or error).
    /// Times out after 8 seconds and returns rank 0 on failure — game continues normally.
    /// </summary>
    public void SubmitScore(string levelId, string levelName, ulong steamId, string steamName, int score,
                            Action<SubmitResult> callback)
    {
        StartCoroutine(SubmitScoreRoutine(levelId, levelName, steamId, steamName, score, callback));
    }

    /// <summary>
    /// Fetch leaderboard entries for HighScoresUI. Only Daily or Weekly scope is valid —
    /// AllTime must use Steam. playerRank in callback is 0 if the player has no score this period.
    /// </summary>
    public void FetchScores(string levelId, LeaderboardTimeScope scope, int limit,
                            ulong steamId, Action<ScoreEntry[], int> callback)
    {
        StartCoroutine(FetchScoresRoutine(levelId, scope, limit, steamId, callback));
    }

    // ── Coroutines ─────────────────────────────────────────────────────────────

    private IEnumerator SubmitScoreRoutine(string levelId, string levelName, ulong steamId, string steamName,
                                           int score, Action<SubmitResult> callback)
    {
        var bodyObj = new SubmitRequestBody
        {
            steamId   = steamId.ToString(),
            steamName = steamName,
            levelId   = levelId,
            levelName = levelName,
            score     = score
        };
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(bodyObj));

        using (var req = new UnityWebRequest(_apiBaseUrl + "/submit.php", "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = TIMEOUT_SECONDS;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LevelScoreService] SubmitScore failed: {req.error}");
                callback?.Invoke(new SubmitResult());
                yield break;
            }

            var response = JsonUtility.FromJson<SubmitResponse>(req.downloadHandler.text);
            callback?.Invoke(new SubmitResult
            {
                DailyRank  = response?.dailyRank  ?? 0,
                WeeklyRank = response?.weeklyRank ?? 0
            });
        }
    }

    private IEnumerator FetchScoresRoutine(string levelId, LeaderboardTimeScope scope,
                                           int limit, ulong steamId,
                                           Action<ScoreEntry[], int> callback)
    {
        string scopeStr = scope == LeaderboardTimeScope.Daily   ? "daily"
                        : scope == LeaderboardTimeScope.Weekly  ? "weekly"
                        : "alltime";
        string url = $"{_apiBaseUrl}/list.php" +
                     $"?levelId={UnityWebRequest.EscapeURL(levelId)}" +
                     $"&scope={scopeStr}&limit={limit}&steamId={steamId}";

        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = TIMEOUT_SECONDS;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LevelScoreService] FetchScores failed: {req.error}");
                callback?.Invoke(null, 0); // null = server error (distinct from empty = no scores)
                yield break;
            }

            var response = JsonUtility.FromJson<ListResponse>(req.downloadHandler.text);
            if (response?.scores == null)
            {
                callback?.Invoke(Array.Empty<ScoreEntry>(), 0);
                yield break;
            }

            var entries = new ScoreEntry[response.scores.Length];
            for (int i = 0; i < response.scores.Length; i++)
            {
                var s = response.scores[i];
                entries[i] = new ScoreEntry
                {
                    Rank      = s.rank,
                    SteamId   = ulong.TryParse(s.steamId, out ulong sid) ? sid : 0UL,
                    SteamName = s.steamName ?? "",
                    Score     = s.score
                };
            }

            callback?.Invoke(entries, response.playerRank);
        }
    }

    // ── JSON DTOs ──────────────────────────────────────────────────────────────

    [Serializable] private class SubmitRequestBody
    {
        public string steamId;
        public string steamName;
        public string levelId;
        public string levelName;
        public int    score;
    }

    [Serializable] private class SubmitResponse
    {
        public bool success;
        public int  dailyRank;
        public int  weeklyRank;
    }

    [Serializable] private class ScoreEntryDto
    {
        public int    rank;
        public string steamId;
        public string steamName;
        public int    score;
    }

    [Serializable] private class ListResponse
    {
        public ScoreEntryDto[] scores;
        public int             playerRank; // 0 = no score this period
    }
}
