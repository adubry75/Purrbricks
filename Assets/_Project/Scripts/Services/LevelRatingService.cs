using System;
using System.Collections;
using System.Text;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Singleton that stores per-level ratings (1–5 stars, 0 = unrated).
/// Storage strategy:
///   1. Local PlayerPrefs for instant read-back.
///   2. Steam leaderboard (ForceUpdate) so ratings can change up or down.
///   3. Optional Firebase Realtime Database REST endpoint for rich analytics.
///      Set FirebaseBaseUrl in the Inspector (or leave empty to skip).
/// </summary>
public class LevelRatingService : MonoBehaviour
{
    public static LevelRatingService Instance { get; private set; }

    [Tooltip("Firebase Realtime Database base URL, e.g. https://yourproject-default-rtdb.firebaseio.com\n" +
             "Leave empty to disable remote logging.")]
    [SerializeField] private string _firebaseBaseUrl = "";

    // PlayerPrefs key prefixes
    private const string KeyRating      = "lvlrating_";      // int, 0 = unrated
    private const string KeyCreatedAt   = "lvlratingct_";    // long (UTC ticks)

    // Steam board naming: "Purrbricks_rate_level_03"
    private const string BoardPrefix = "Purrbricks_rate_";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns the locally stored rating for <paramref name="levelId"/> (0 = unrated).</summary>
    public int GetRating(string levelId)
        => PlayerPrefs.GetInt(KeyRating + levelId, 0);

    /// <summary>
    /// Saves a rating (1–5) or clears it (0) for <paramref name="levelId"/>.
    /// Persists locally, submits to Steam, and optionally logs to Firebase.
    /// </summary>
    public void SetRating(string levelId, int levelIndex, int rating)
    {
        rating = Mathf.Clamp(rating, 0, 5);

        bool isFirstRating = PlayerPrefs.GetInt(KeyRating + levelId, 0) == 0 && rating > 0;
        if (isFirstRating)
            PlayerPrefs.SetString(KeyCreatedAt + levelId, DateTime.UtcNow.Ticks.ToString());

        PlayerPrefs.SetInt(KeyRating + levelId, rating);
        PlayerPrefs.Save();

        // Steam — only submit non-zero ratings (can't remove an entry, but 0 never happened)
        if (rating > 0)
        {
            string boardName = BoardPrefix + levelId;
            SteamLeaderboardManager.Instance?.SubmitScoreForce(boardName, rating);
        }

        // Firebase — fire and forget (won't block gameplay)
        if (!string.IsNullOrEmpty(_firebaseBaseUrl))
            StartCoroutine(PostToFirebase(levelId, levelIndex, rating));
    }

    // ── Firebase REST ─────────────────────────────────────────────────────────

    private IEnumerator PostToFirebase(string levelId, int levelIndex, int rating)
    {
        string steamId   = "";
        string steamName = "";

        try
        {
            var csid = SteamUser.GetSteamID();
            steamId   = csid.m_SteamID.ToString();
            steamName = SteamFriends.GetPersonaName();
        }
        catch { /* Steam not available */ }

        long createdAtTicks = long.TryParse(
            PlayerPrefs.GetString(KeyCreatedAt + levelId, "0"), out long t) ? t : 0L;

        string createdAt  = new DateTime(createdAtTicks, DateTimeKind.Utc).ToString("o");
        string updatedAt  = DateTime.UtcNow.ToString("o");

        // Build a minimal JSON payload
        // PATCH merges fields into an existing record (preserves createdAt on subsequent updates)
        string json = rating == 0
            ? $"{{\"steamId\":\"{steamId}\",\"steamName\":\"{EscapeJson(steamName)}\",\"levelId\":\"{levelId}\",\"levelIndex\":{levelIndex},\"rating\":0,\"createdAt\":\"{createdAt}\",\"updatedAt\":\"{updatedAt}\"}}"
            : $"{{\"steamId\":\"{steamId}\",\"steamName\":\"{EscapeJson(steamName)}\",\"levelId\":\"{levelId}\",\"levelIndex\":{levelIndex},\"rating\":{rating},\"createdAt\":\"{createdAt}\",\"updatedAt\":\"{updatedAt}\"}}";

        // Path: /ratings/<levelId>/<steamId>.json
        string url = $"{_firebaseBaseUrl.TrimEnd('/')}/ratings/{levelId}/{steamId}.json";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        using var req = new UnityWebRequest(url, "PATCH");
        req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning($"LevelRatingService: Firebase write failed — {req.error}");
    }

    private static string EscapeJson(string s)
        => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
