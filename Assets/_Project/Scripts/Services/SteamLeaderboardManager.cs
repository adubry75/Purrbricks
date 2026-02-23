using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

/// <summary>
/// Singleton that manages multiple named Steam leaderboards.
/// Serializes board initialization, score uploads (KeepBest), and top-score fetches
/// so that multiple boards can be used without conflicting CallResult handles.
/// </summary>
public class SteamLeaderboardManager : MonoBehaviour
{
    public static SteamLeaderboardManager Instance { get; private set; }

    public event Action<string> OnError;

    // ── Per-board state ───────────────────────────────────────────────────────

    private class BoardEntry
    {
        public SteamLeaderboard_t Handle;
        public bool IsReady;
        public readonly Queue<int> PendingUploads = new Queue<int>();
        public readonly Queue<(int count, Action<List<LeaderboardEntryModel>> cb)> PendingFetches
            = new Queue<(int, Action<List<LeaderboardEntryModel>>)>();
    }

    private readonly Dictionary<string, BoardEntry> _boards = new Dictionary<string, BoardEntry>();

    // ── Initialization queue (one board at a time) ────────────────────────────

    private readonly HashSet<string> _initQueued = new HashSet<string>();
    private readonly Queue<string>   _initQueue  = new Queue<string>();
    private string _initializingNow;
    private CallResult<LeaderboardFindResult_t> _findResult;

    // ── Upload queue (serialized) ─────────────────────────────────────────────

    private readonly Queue<(SteamLeaderboard_t handle, int score)> _uploadQueue
        = new Queue<(SteamLeaderboard_t, int)>();
    private bool _uploading;
    private CallResult<LeaderboardScoreUploaded_t> _uploadResult;

    // ── Download queue (serialized) ───────────────────────────────────────────

    private readonly Queue<(SteamLeaderboard_t handle, int count, Action<List<LeaderboardEntryModel>> cb)> _downloadQueue
        = new Queue<(SteamLeaderboard_t, int, Action<List<LeaderboardEntryModel>>)>();
    private bool _downloading;
    private Action<List<LeaderboardEntryModel>> _downloadCallback;
    private CallResult<LeaderboardScoresDownloaded_t> _downloadResult;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _findResult     = CallResult<LeaderboardFindResult_t>.Create(OnLeaderboardFound);
        _uploadResult   = CallResult<LeaderboardScoreUploaded_t>.Create(OnScoreUploaded);
        _downloadResult = CallResult<LeaderboardScoresDownloaded_t>.Create(OnScoresDownloaded);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Submit a score to the named leaderboard.
    /// Steam uses KeepBest — a lower score will never overwrite a higher one.
    /// Safe to call before the board is initialized (queued automatically).
    /// </summary>
    public void SubmitScore(string boardName, int score)
    {
        if (!IsSteamReady()) return;

        var entry = GetOrCreate(boardName);
        if (entry.IsReady)
            EnqueueUpload(entry.Handle, score);
        else
        {
            entry.PendingUploads.Enqueue(score);
            EnsureInit(boardName);
        }
    }

    /// <summary>
    /// Fetch the top <paramref name="count"/> scores from the named leaderboard.
    /// <paramref name="callback"/> is invoked with the result list, or <c>null</c> on error.
    /// Safe to call before the board is initialized (queued automatically).
    /// </summary>
    public void FetchTopScores(string boardName, int count, Action<List<LeaderboardEntryModel>> callback)
    {
        if (!IsSteamReady())
        {
            callback?.Invoke(null);
            return;
        }

        var entry = GetOrCreate(boardName);
        if (entry.IsReady)
            EnqueueDownload(entry.Handle, count, callback);
        else
        {
            entry.PendingFetches.Enqueue((count, callback));
            EnsureInit(boardName);
        }
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private bool IsSteamReady() => SteamworksBootstrap.Instance?.IsSteamAvailable == true;

    private BoardEntry GetOrCreate(string name)
    {
        if (!_boards.TryGetValue(name, out var entry))
        {
            entry = new BoardEntry();
            _boards[name] = entry;
        }
        return entry;
    }

    private void EnsureInit(string name)
    {
        if (_initQueued.Contains(name)) return;
        _initQueued.Add(name);
        _initQueue.Enqueue(name);
        TryStartNextInit();
    }

    private void TryStartNextInit()
    {
        if (_initializingNow != null || _initQueue.Count == 0) return;
        _initializingNow = _initQueue.Dequeue();
        Debug.Log($"SteamLeaderboardManager: Finding/creating '{_initializingNow}'");
        var call = SteamUserStats.FindOrCreateLeaderboard(
            _initializingNow,
            ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
            ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);
        _findResult.Set(call);
    }

    private void OnLeaderboardFound(LeaderboardFindResult_t result, bool failure)
    {
        string name = _initializingNow;
        _initializingNow = null;
        _initQueued.Remove(name);

        if (failure || result.m_bLeaderboardFound == 0)
        {
            Debug.LogWarning($"SteamLeaderboardManager: Failed to find/create '{name}'");
            OnError?.Invoke($"Could not access leaderboard '{name}'.");
            TryStartNextInit();
            return;
        }

        var entry = GetOrCreate(name);
        entry.Handle  = result.m_hSteamLeaderboard;
        entry.IsReady = true;
        Debug.Log($"SteamLeaderboardManager: '{name}' ready");

        while (entry.PendingUploads.Count > 0)
            EnqueueUpload(entry.Handle, entry.PendingUploads.Dequeue());

        while (entry.PendingFetches.Count > 0)
        {
            var (cnt, cb) = entry.PendingFetches.Dequeue();
            EnqueueDownload(entry.Handle, cnt, cb);
        }

        TryStartNextInit();
    }

    // ── Uploads ───────────────────────────────────────────────────────────────

    private void EnqueueUpload(SteamLeaderboard_t handle, int score)
    {
        _uploadQueue.Enqueue((handle, score));
        TryStartNextUpload();
    }

    private void TryStartNextUpload()
    {
        if (_uploading || _uploadQueue.Count == 0) return;
        _uploading = true;
        var (handle, score) = _uploadQueue.Dequeue();
        var call = SteamUserStats.UploadLeaderboardScore(
            handle,
            ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
            score, null, 0);
        _uploadResult.Set(call);
        Debug.Log($"SteamLeaderboardManager: Uploading score {score}");
    }

    private void OnScoreUploaded(LeaderboardScoreUploaded_t result, bool failure)
    {
        _uploading = false;
        if (failure || result.m_bSuccess == 0)
            Debug.LogWarning("SteamLeaderboardManager: Score upload failed.");
        else
            Debug.Log($"SteamLeaderboardManager: Uploaded score {result.m_nScore} (rank {result.m_nGlobalRankNew})");
        TryStartNextUpload();
    }

    // ── Downloads ─────────────────────────────────────────────────────────────

    private void EnqueueDownload(SteamLeaderboard_t handle, int count, Action<List<LeaderboardEntryModel>> cb)
    {
        _downloadQueue.Enqueue((handle, count, cb));
        TryStartNextDownload();
    }

    private void TryStartNextDownload()
    {
        if (_downloading || _downloadQueue.Count == 0) return;
        _downloading = true;
        var (handle, count, cb) = _downloadQueue.Dequeue();
        _downloadCallback = cb;
        var call = SteamUserStats.DownloadLeaderboardEntries(
            handle,
            ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
            1, count);
        _downloadResult.Set(call);
        Debug.Log($"SteamLeaderboardManager: Downloading top {count} entries");
    }

    private void OnScoresDownloaded(LeaderboardScoresDownloaded_t result, bool failure)
    {
        _downloading = false;
        var cb = _downloadCallback;
        _downloadCallback = null;

        if (failure)
        {
            Debug.LogWarning("SteamLeaderboardManager: Download failed.");
            cb?.Invoke(null);
            TryStartNextDownload();
            return;
        }

        var entries = new List<LeaderboardEntryModel>(result.m_cEntryCount);
        for (int i = 0; i < result.m_cEntryCount; i++)
        {
            if (SteamUserStats.GetDownloadedLeaderboardEntry(
                result.m_hSteamLeaderboardEntries, i, out var e, null, 0))
            {
                string displayName = ResolveDisplayName(e.m_steamIDUser);
                entries.Add(new LeaderboardEntryModel(e.m_nGlobalRank, e.m_nScore, e.m_steamIDUser, displayName));
            }
        }

        cb?.Invoke(entries);
        TryStartNextDownload();
    }

    private static string ResolveDisplayName(CSteamID steamId)
    {
        if (steamId == CSteamID.Nil) return "Unknown";
        var name = SteamFriends.GetFriendPersonaName(steamId);
        return string.IsNullOrEmpty(name) ? steamId.m_SteamID.ToString() : name;
    }
}
