using System;
using System.Collections;
using System.Text;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Singleton that stores per-level ratings (1–5 stars, 0 = unrated).
/// Storage strategy:
///   1. Local PlayerPrefs for instant read-back (always works, even offline).
///   2. Steam leaderboard (ForceUpdate) so ratings can change up or down.
///   3. Optional MySQL REST API on your own server.
///      Set ApiBaseUrl in the Inspector (e.g. https://api.purrbricks.dubry.com).
///      Leave empty to skip remote logging.
/// </summary>
public class LevelRatingService : MonoBehaviour
{
    public static LevelRatingService Instance { get; private set; }

    [Tooltip("Base URL of your Purrbricks rating API endpoint.\n" +
             "Example: https://api.purrbricks.dubry.com\n" +
             "The service will POST to <ApiBaseUrl>/rate.php\n" +
             "Leave empty to disable remote logging.")]
    [SerializeField] private string _apiBaseUrl = "";

    // PlayerPrefs key prefixes
    private const string KeyRating    = "lvlrating_";    // int 0–5, 0 = unrated
    private const string KeyCreatedAt = "lvlratingct_";  // long (UTC ticks), first time rated

    // Steam board naming: "Purrbricks_rate_level_03"
    private const string BoardPrefix = "Purrbricks_rate_";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[LevelRatingService] Singleton ready.");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns the locally stored rating for <paramref name="levelId"/> (0 = unrated).</summary>
    public int GetRating(string levelId)
    {
        int r = PlayerPrefs.GetInt(KeyRating + levelId, 0);
        Debug.Log($"[LevelRatingService] GetRating({levelId}) = {r}");
        return r;
    }

    /// <summary>
    /// Saves a rating (1–5) or clears it (0) for <paramref name="levelId"/>.
    /// Persists locally, submits to Steam, and optionally posts to the MySQL API.
    /// </summary>
    public void SetRating(string levelId, int levelIndex, int rating)
    {
        rating = Mathf.Clamp(rating, 0, 5);

        // ── 1. Local PlayerPrefs ──────────────────────────────────────────────
        bool isFirstRating = PlayerPrefs.GetInt(KeyRating + levelId, 0) == 0 && rating > 0;
        if (isFirstRating)
            PlayerPrefs.SetString(KeyCreatedAt + levelId, DateTime.UtcNow.Ticks.ToString());

        PlayerPrefs.SetInt(KeyRating + levelId, rating);
        PlayerPrefs.Save();
        Debug.Log($"[LevelRatingService] SetRating({levelId}, {rating}) → PlayerPrefs saved.");

        // ── 2. Steam leaderboard ──────────────────────────────────────────────
        if (rating > 0)
        {
            string boardName = BoardPrefix + levelId;
            SteamLeaderboardManager.Instance?.SubmitScoreForce(boardName, rating);
        }

        // ── 3. Remote MySQL API ───────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_apiBaseUrl))
            StartCoroutine(PostRating(levelId, levelIndex, rating));
    }

    // ── MySQL REST API ────────────────────────────────────────────────────────

    private IEnumerator PostRating(string levelId, int levelIndex, int rating)
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

        // after the try/catch block
        if (string.IsNullOrEmpty(steamId))
        {
            Debug.Log("[LevelRatingService] No Steam ID — skipping remote rating POST.");
            yield break;
        }

        long createdAtTicks = long.TryParse(
            PlayerPrefs.GetString(KeyCreatedAt + levelId, "0"), out long t) ? t : 0L;

        string createdAt = new DateTime(createdAtTicks, DateTimeKind.Utc).ToString("o");
        string updatedAt = DateTime.UtcNow.ToString("o");

        string body = "{" +
            $"\"levelId\":\"{levelId}\"," +
            $"\"levelIndex\":{levelIndex}," +
            $"\"steamId\":\"{steamId}\"," +
            $"\"steamName\":\"{EscapeJson(steamName)}\"," +
            $"\"rating\":{rating}," +
            $"\"createdAt\":\"{createdAt}\"," +
            $"\"updatedAt\":\"{updatedAt}\"" +
            "}";

        string url = _apiBaseUrl.TrimEnd('/') + "/rate.php";
        Debug.Log($"[LevelRatingService] POSTing rating to: {url}");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning($"[LevelRatingService] API error: {req.error}  ({url})");
        else
            Debug.Log($"[LevelRatingService] API response: {req.downloadHandler.text}");
    }

    private static string EscapeJson(string s)
        => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
