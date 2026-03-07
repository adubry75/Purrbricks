using System;
using System.Collections;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Singleton service that talks to the Bluehost PHP community-level API.
/// Caches fetched level JSON locally so repeated plays don't re-download.
/// </summary>
public class CommunityLevelService : MonoBehaviour
{
    public static CommunityLevelService Instance { get; private set; }

    [Tooltip("Base URL of the community API, e.g. https://api.purrbricks.dubry.com\n" +
             "The service calls <ApiBaseUrl>/community/list.php, get.php, etc.")]
    [SerializeField] private string _apiBaseUrl = "";

    // PlayerPrefs key prefixes
    private const string KEY_MY_RATING = "cl_rating_";     // int 0-5 per community level id
    private const string KEY_CLEARED   = "cl_cleared_";    // int 0/1
    private const string KEY_PUBLISHED  = "cl_published_"; // int communityId (keyed by local levelId)

    // Cache directory: persistentDataPath/community_cache/{id}.json
    private string CacheDir => Path.Combine(Application.persistentDataPath, "community_cache");

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(CacheDir);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void FetchLevels(string sort, int page, int limit, Action<CommunityLevelPage> cb)
        => StartCoroutine(FetchLevelsRoutine(sort, page, limit, cb));

    public void FetchLevel(int id, Action<LevelData, CommunityLevelMeta> cb)
        => StartCoroutine(FetchLevelRoutine(id, cb));

    public void PublishLevel(LevelData data, string localLevelId, string title, string desc, Action<int, string> cb)
        => StartCoroutine(PublishLevelRoutine(data, localLevelId, title, desc, cb));

    public void RateLevel(int id, int rating, Action<string> cb)
        => StartCoroutine(RateLevelRoutine(id, rating, cb));

    public void ReportLevel(int id, Action cb)
        => StartCoroutine(ReportLevelRoutine(id, cb));

    public void DeleteLevel(int id, Action<string> cb)
        => StartCoroutine(DeleteLevelRoutine(id, cb));

    /// <summary>Fire-and-forget play count increment. No callback.</summary>
    public void IncrementPlayCount(int id)
        => StartCoroutine(PlayedRoutine(id));

    /// <summary>Returns locally stored star rating (0 = unrated) for a community level.</summary>
    public int GetMyRating(int id)
        => PlayerPrefs.GetInt(KEY_MY_RATING + id, 0);

    /// <summary>Returns whether the player has cleared this community level before.</summary>
    public bool HasCleared(int id)
        => PlayerPrefs.GetInt(KEY_CLEARED + id, 0) == 1;

    /// <summary>Returns the community level ID that was assigned when a local level was published (0 = never published).</summary>
    public int GetPublishedId(string localLevelId)
        => PlayerPrefs.GetInt(KEY_PUBLISHED + localLevelId, 0);

    /// <summary>Marks a community level as cleared in PlayerPrefs.</summary>
    public void MarkCleared(int id)
    {
        PlayerPrefs.SetInt(KEY_CLEARED + id, 1);
        PlayerPrefs.Save();
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator FetchLevelsRoutine(string sort, int page, int limit, Action<CommunityLevelPage> cb)
    {
        string url = $"{ApiBase()}/list.php?sort={sort}&page={page}&limit={limit}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[CommunityLevelService] FetchLevels error: {req.error}");
            cb?.Invoke(null);
            yield break;
        }

        try
        {
            var result = JsonConvert.DeserializeObject<CommunityLevelPage>(req.downloadHandler.text);
            cb?.Invoke(result);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CommunityLevelService] FetchLevels parse error: {e.Message}");
            cb?.Invoke(null);
        }
    }

    private IEnumerator FetchLevelRoutine(int id, Action<LevelData, CommunityLevelMeta> cb)
    {
        // Check local cache first
        string cachePath = Path.Combine(CacheDir, $"{id}.json");
        if (File.Exists(cachePath))
        {
            try
            {
                string cached = File.ReadAllText(cachePath);
                var cachedResponse = JsonConvert.DeserializeObject<GetLevelResponse>(cached);
                if (cachedResponse != null)
                {
                    var levelData = JsonConvert.DeserializeObject<LevelData>(cachedResponse.jsonData);
                    cb?.Invoke(levelData, cachedResponse.ToMeta());
                    yield break;
                }
            }
            catch { /* fall through to network fetch */ }
        }

        string url = $"{ApiBase()}/get.php?id={id}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[CommunityLevelService] FetchLevel error: {req.error}");
            cb?.Invoke(null, null);
            yield break;
        }

        try
        {
            string text     = req.downloadHandler.text;
            var    response = JsonConvert.DeserializeObject<GetLevelResponse>(text);
            if (response == null) { cb?.Invoke(null, null); yield break; }

            // Write to cache
            File.WriteAllText(cachePath, text);

            var levelData = JsonConvert.DeserializeObject<LevelData>(response.jsonData);
            cb?.Invoke(levelData, response.ToMeta());
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CommunityLevelService] FetchLevel parse error: {e.Message}");
            cb?.Invoke(null, null);
        }
    }

    private IEnumerator PublishLevelRoutine(LevelData data, string localLevelId, string title, string desc, Action<int, string> cb)
    {
        string steamId   = "";
        string steamName = "";
        try
        {
            steamId   = SteamUser.GetSteamID().m_SteamID.ToString();
            steamName = SteamFriends.GetPersonaName();
        }
        catch { /* Steam not available */ }

        string jsonData = JsonConvert.SerializeObject(data);
        int brickCount = data?.bricks?.Count ?? 0;

        string body = "{" +
            $"\"steamId\":\"{EscapeJson(steamId)}\"," +
            $"\"steamName\":\"{EscapeJson(steamName)}\"," +
            $"\"title\":\"{EscapeJson(title)}\"," +
            $"\"description\":\"{EscapeJson(desc)}\"," +
            $"\"jsonData\":{jsonData}," +
            $"\"brickCount\":{brickCount}" +
            "}";

        byte[] raw = Encoding.UTF8.GetBytes(body);
        string url  = $"{ApiBase()}/publish.php";
        Debug.Log($"[Level Editor]Publish URL '{url}'");
        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            cb?.Invoke(0, req.error);
            yield break;
        }

        try
        {
            var resp = JsonConvert.DeserializeObject<PublishResponse>(req.downloadHandler.text);
            if (resp != null && resp.id > 0)
            {
                PlayerPrefs.SetInt(KEY_PUBLISHED + localLevelId, resp.id);
                PlayerPrefs.Save();
                cb?.Invoke(resp.id, null);
            }
            else
            {
                var err = JsonConvert.DeserializeObject<ErrorResponse>(req.downloadHandler.text);
                cb?.Invoke(0, err?.error ?? "Unknown error");
            }
        }
        catch (Exception e)
        {
            cb?.Invoke(0, e.Message);
        }
    }

    private IEnumerator RateLevelRoutine(int id, int rating, Action<string> cb)
    {
        string steamId = "";
        try { steamId = SteamUser.GetSteamID().m_SteamID.ToString(); } catch { }

        string body = $"{{\"levelId\":{id},\"steamId\":\"{steamId}\",\"rating\":{rating}}}";
        byte[] raw  = Encoding.UTF8.GetBytes(body);
        string url  = $"{ApiBase()}/rate.php";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            cb?.Invoke(req.error);
            yield break;
        }

        // Save locally
        PlayerPrefs.SetInt(KEY_MY_RATING + id, rating);
        PlayerPrefs.Save();
        cb?.Invoke(null);
    }

    private IEnumerator ReportLevelRoutine(int id, Action cb)
    {
        string steamId = "";
        try { steamId = SteamUser.GetSteamID().m_SteamID.ToString(); } catch { }

        string body = $"{{\"levelId\":{id},\"steamId\":\"{steamId}\"}}";
        byte[] raw  = Encoding.UTF8.GetBytes(body);
        string url  = $"{ApiBase()}/report.php";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
        cb?.Invoke();
    }

    private IEnumerator DeleteLevelRoutine(int id, Action<string> cb)
    {
        string steamId = "";
        try { steamId = SteamUser.GetSteamID().m_SteamID.ToString(); } catch { }

        string body = $"{{\"levelId\":{id},\"steamId\":\"{steamId}\"}}";
        byte[] raw  = Encoding.UTF8.GetBytes(body);
        string url  = $"{ApiBase()}/delete.php";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) { cb?.Invoke(req.error); yield break; }

        // Also delete from local cache
        string cachePath = Path.Combine(CacheDir, $"{id}.json");
        if (File.Exists(cachePath)) File.Delete(cachePath);

        cb?.Invoke(null);
    }

    private IEnumerator PlayedRoutine(int id)
    {
        string body = $"{{\"levelId\":{id}}}";
        byte[] raw  = Encoding.UTF8.GetBytes(body);
        string url  = $"{ApiBase()}/played.php";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
        // fire and forget — ignore result
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string ApiBase() => _apiBaseUrl.TrimEnd('/') + "/community";

    private static string EscapeJson(string s)
        => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ── Internal DTOs ─────────────────────────────────────────────────────────

    [Serializable]
    private class GetLevelResponse
    {
        public int    id;
        public string steamId;
        public string steamName;
        public string title;
        public string description;
        public int    brickCount;
        public int    playCount;
        public float  averageRating;
        public int    ratingCount;
        public string publishedAt;
        public string jsonData;

        public CommunityLevelMeta ToMeta() => new CommunityLevelMeta
        {
            id            = id,
            steamId       = steamId,
            steamName     = steamName,
            title         = title,
            description   = description,
            brickCount    = brickCount,
            playCount     = playCount,
            averageRating = averageRating,
            ratingCount   = ratingCount,
            publishedAt   = publishedAt,
        };
    }

    [Serializable]
    private class PublishResponse { public int id; }

    [Serializable]
    private class ErrorResponse  { public string error; }
}
