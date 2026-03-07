using System;
using System.Collections;
using System.Collections.Generic;
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
    private const string KEY_MY_RATING     = "cl_rating_";    // int 0-5 keyed by server id
    private const string KEY_CLEARED       = "cl_cleared_";   // int 0/1 keyed by server id
    private const string KEY_PUBLISHED_GUID = "cl_pguid_";    // int server id keyed by levelGuid

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

    /// <summary>Fetches all community levels published by the current Steam user.</summary>
    public void FetchMyLevels(Action<List<CommunityLevelMeta>> cb)
        => StartCoroutine(FetchMyLevelsRoutine(cb));

    public void FetchLevel(int id, Action<LevelData, CommunityLevelMeta> cb)
        => StartCoroutine(FetchLevelRoutine(id, cb));

    public void PublishLevel(LevelData data, string title, string desc, Action<PublishResult> cb)
        => StartCoroutine(PublishLevelRoutine(data, title, desc, cb));

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

    /// <summary>Returns the server row id that was assigned when this GUID was published (0 = never published).</summary>
    public int GetPublishedServerId(string levelGuid)
        => string.IsNullOrEmpty(levelGuid) ? 0 : PlayerPrefs.GetInt(KEY_PUBLISHED_GUID + levelGuid, 0);

    /// <summary>True if this level GUID has been successfully published to the community server.</summary>
    public bool IsPublished(string levelGuid) => GetPublishedServerId(levelGuid) > 0;

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

    private IEnumerator FetchMyLevelsRoutine(Action<List<CommunityLevelMeta>> cb)
    {
        string steamId = "";
        try { steamId = SteamUser.GetSteamID().m_SteamID.ToString(); } catch { }
        if (string.IsNullOrEmpty(steamId)) { cb?.Invoke(new List<CommunityLevelMeta>()); yield break; }

        string url = $"{ApiBase()}/my-levels.php?steamId={steamId}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[CommunityLevelService] FetchMyLevels error: {req.error}");
            cb?.Invoke(new List<CommunityLevelMeta>());
            yield break;
        }

        try
        {
            var result = JsonConvert.DeserializeObject<CommunityLevelPage>(req.downloadHandler.text);
            cb?.Invoke(result?.levels ?? new List<CommunityLevelMeta>());
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CommunityLevelService] FetchMyLevels parse error: {e.Message}");
            cb?.Invoke(new List<CommunityLevelMeta>());
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

    private IEnumerator PublishLevelRoutine(LevelData data, string title, string desc, Action<PublishResult> cb)
    {
        string steamId   = "";
        string steamName = "";
        try
        {
            steamId   = SteamUser.GetSteamID().m_SteamID.ToString();
            steamName = SteamFriends.GetPersonaName();
        }
        catch { /* Steam not available in editor without Steam running */ }

        string levelGuid = data?.levelGuid ?? "";
        if (string.IsNullOrEmpty(levelGuid))
        {
            // Safety net: generate a GUID if the level somehow has none
            levelGuid = Guid.NewGuid().ToString("N");
            if (data != null) data.levelGuid = levelGuid;
            Debug.LogWarning("[CommunityLevelService] Level had no GUID — generated one on publish.");
        }

        string jsonData   = JsonConvert.SerializeObject(data);
        int    brickCount = data?.bricks?.Count ?? 0;

        // Build request body as a flat JSON string — jsonData is embedded as raw JSON object
        string body = "{" +
            $"\"steamId\":\"{EscapeJson(steamId)}\"," +
            $"\"steamName\":\"{EscapeJson(steamName)}\"," +
            $"\"levelGuid\":\"{EscapeJson(levelGuid)}\"," +
            $"\"title\":\"{EscapeJson(title)}\"," +
            $"\"description\":\"{EscapeJson(desc)}\"," +
            $"\"jsonData\":{jsonData}," +
            $"\"brickCount\":{brickCount}" +
            "}";

        byte[] raw = Encoding.UTF8.GetBytes(body);
        string url = $"{ApiBase()}/publish.php";
        Debug.Log($"[CommunityLevelService] Publish → {url}  guid={levelGuid}");

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        string rawBody = req.downloadHandler?.text ?? "(no body)";

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[CommunityLevelService] Publish HTTP error {req.responseCode}: {req.error}\nBody: {rawBody}");
            cb?.Invoke(new PublishResult { error = $"HTTP {req.responseCode}: {req.error}" });
            yield break;
        }

        try
        {
            var resp = JsonConvert.DeserializeObject<PublishResponse>(rawBody);
            if (resp != null && resp.id > 0)
            {
                PlayerPrefs.SetInt(KEY_PUBLISHED_GUID + levelGuid, resp.id);
                PlayerPrefs.Save();
                Debug.Log($"[CommunityLevelService] Publish {resp.action}: server id={resp.id} guid={levelGuid}");
                cb?.Invoke(new PublishResult { serverId = resp.id, levelGuid = levelGuid, action = resp.action });
            }
            else
            {
                var err = JsonConvert.DeserializeObject<ErrorResponse>(rawBody);
                string msg = err?.error ?? $"Unexpected response: {rawBody}";
                Debug.LogWarning($"[CommunityLevelService] Publish error from server: {msg}");
                cb?.Invoke(new PublishResult { error = msg });
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CommunityLevelService] Publish parse error: {e.Message}\nRaw: {rawBody}");
            cb?.Invoke(new PublishResult { error = e.Message });
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
            Debug.LogWarning($"[LevelRatingService] API error: {req.error}  ({url})");
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
        public string levelGuid;
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
            levelGuid     = levelGuid,
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
    private class PublishResponse { public int id; public string levelGuid; public string action; }

    [Serializable]
    private class ErrorResponse  { public string error; }
}

// ── Publish result returned to callers ────────────────────────────────────────
public class PublishResult
{
    public int    serverId;
    public string levelGuid;
    public string action;    // "created" or "updated"
    public string error;
    public bool   Success => string.IsNullOrEmpty(error);
    public bool   WasCreated => action == "created";
    public bool   WasUpdated => action == "updated";
}
