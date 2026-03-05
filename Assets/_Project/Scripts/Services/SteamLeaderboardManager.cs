using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

/// <summary>
/// Singleton that manages multiple named Steam leaderboards.
/// Board initialization, score uploads, and score downloads are all fully parallel —
/// each async Steam API call gets its own CallResult so a stuck callback on one board
/// cannot block any other board.
/// </summary>
public class SteamLeaderboardManager : MonoBehaviour
{
    public static SteamLeaderboardManager Instance { get; private set; }

    public event Action<string> OnError;

    private const int FETCH_ALL_PAGE_SIZE = 100;

    // ── Per-board state ───────────────────────────────────────────────────────

    private class BoardEntry
    {
        public SteamLeaderboard_t Handle;
        public bool IsReady;
        public readonly Queue<(int score, ELeaderboardUploadScoreMethod method)> PendingUploads
            = new Queue<(int, ELeaderboardUploadScoreMethod)>();
        public readonly Queue<(ELeaderboardDataRequest reqType, int rangeStart, int rangeEnd, Action<List<LeaderboardEntryModel>> cb)> PendingFetches
            = new Queue<(ELeaderboardDataRequest, int, int, Action<List<LeaderboardEntryModel>>)>();
        public readonly Queue<Action<List<LeaderboardEntryModel>>> PendingFetchAll
            = new Queue<Action<List<LeaderboardEntryModel>>>();
    }

    private readonly Dictionary<string, BoardEntry> _boards = new Dictionary<string, BoardEntry>();

    // ── Parallel board initialization ─────────────────────────────────────────
    // Each FindOrCreateLeaderboard call gets its own CallResult so all boards
    // initialise concurrently rather than queuing behind each other.

    private readonly Dictionary<string, CallResult<LeaderboardFindResult_t>> _activeInits
        = new Dictionary<string, CallResult<LeaderboardFindResult_t>>();

    // ── Parallel uploads ──────────────────────────────────────────────────────
    // Each UploadLeaderboardScore call gets its own CallResult so a hung callback
    // on one board cannot block uploads to any other board.

    private readonly List<CallResult<LeaderboardScoreUploaded_t>> _activeUploads
        = new List<CallResult<LeaderboardScoreUploaded_t>>();

    // ── Parallel downloads ────────────────────────────────────────────────────
    // Each DownloadLeaderboardEntries call gets its own CallResult so a hung
    // download on one board cannot block downloads on any other board.

    private readonly List<CallResult<LeaderboardScoresDownloaded_t>> _activeDownloads
        = new List<CallResult<LeaderboardScoresDownloaded_t>>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Submit a score to the named leaderboard.
    /// Steam uses KeepBest — a lower score will never overwrite a higher one.
    /// Safe to call before the board is initialized (queued automatically).
    /// </summary>
    public void SubmitScore(string boardName, int score)
        => SubmitScoreInternal(boardName, score, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest);

    /// <summary>
    /// Submit a score using ForceUpdate — always overwrites the existing entry even if lower.
    /// Used for ratings where the value can go up or down.
    /// Safe to call before the board is initialized (queued automatically).
    /// </summary>
    public void SubmitScoreForce(string boardName, int score)
        => SubmitScoreInternal(boardName, score, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodForceUpdate);

    private void SubmitScoreInternal(string boardName, int score, ELeaderboardUploadScoreMethod method)
    {
        if (!IsSteamReady()) return;

        var entry = GetOrCreate(boardName);
        if (entry.IsReady)
            EnqueueUpload(entry.Handle, score, method);
        else
        {
            entry.PendingUploads.Enqueue((score, method));
            EnsureInit(boardName);
        }
    }

    /// <summary>Fetch top <paramref name="count"/> scores (ranks 1…count). Kept for compatibility.</summary>
    public void FetchTopScores(string boardName, int count, Action<List<LeaderboardEntryModel>> callback)
        => FetchRange(boardName, 1, count, callback);

    /// <summary>
    /// Fetch a specific rank range from the named leaderboard (1-based, global order).
    /// <paramref name="callback"/> is invoked with the result list, or <c>null</c> on error.
    /// Safe to call before the board is initialized.
    /// </summary>
    public void FetchRange(string boardName, int start, int end, Action<List<LeaderboardEntryModel>> callback)
    {
        if (!IsSteamReady()) { callback?.Invoke(null); return; }
        EnqueueOrPend(boardName, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, start, end, callback);
    }

    /// <summary>
    /// Fetch entries surrounding the current user (±<paramref name="range"/> ranks).
    /// Returns up to 2×range+1 entries with the user's entry centred.
    /// Falls back to an empty list (not null) when the user has no score on this board.
    /// </summary>
    public void FetchAroundMe(string boardName, int range, Action<List<LeaderboardEntryModel>> callback)
    {
        if (!IsSteamReady()) { callback?.Invoke(null); return; }
        EnqueueOrPend(boardName, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser, -range, range, callback);
    }

    /// <summary>
    /// Triggers board initialization (FindOrCreateLeaderboard) for <paramref name="boardName"/>
    /// without queuing any download. Call this to warm up a board before the user fetches it.
    /// Safe to call if the board is already ready — becomes a no-op.
    /// </summary>
    public void PrewarmBoard(string boardName)
    {
        if (!IsSteamReady()) return;
        var entry = GetOrCreate(boardName);
        if (!entry.IsReady) EnsureInit(boardName);
    }

    /// <summary>
    /// No-op: downloads are now fully parallel (no shared queue).
    /// A stale download's callback is discarded by the token check in HighScoresUI.
    /// Kept so existing call sites compile without changes.
    /// </summary>
    public void CancelPendingDownloads() { }

    /// <summary>
    /// Fetch all scores currently on the leaderboard.
    /// <paramref name="callback"/> is invoked with an empty list when the board has no entries, or <c>null</c> on error.
    /// </summary>
    public void FetchAll(string boardName, Action<List<LeaderboardEntryModel>> callback)
    {
        if (!IsSteamReady()) { callback?.Invoke(null); return; }

        var entry = GetOrCreate(boardName);
        if (entry.IsReady)
            EnqueueFetchAllNow(entry, callback);
        else
        {
            entry.PendingFetchAll.Enqueue(callback);
            EnsureInit(boardName);
        }
    }

    private void EnqueueOrPend(string boardName, ELeaderboardDataRequest reqType,
        int rangeStart, int rangeEnd, Action<List<LeaderboardEntryModel>> callback)
    {
        var entry = GetOrCreate(boardName);
        if (entry.IsReady)
            EnqueueDownload(entry.Handle, reqType, rangeStart, rangeEnd, callback);
        else
        {
            entry.PendingFetches.Enqueue((reqType, rangeStart, rangeEnd, callback));
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
        if (_activeInits.ContainsKey(name)) return; // already in progress
        // Each board gets its own CallResult so multiple boards init concurrently.
        var cr = CallResult<LeaderboardFindResult_t>.Create(
            (result, failure) => OnLeaderboardFound(name, result, failure));
        _activeInits[name] = cr;
        Debug.Log($"SteamLeaderboardManager: Finding/creating '{name}'");
        var call = SteamUserStats.FindOrCreateLeaderboard(
            name,
            ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
            ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);
        cr.Set(call);
    }

    private void OnLeaderboardFound(string name, LeaderboardFindResult_t result, bool failure)
    {
        _activeInits.Remove(name);

        if (failure || result.m_bLeaderboardFound == 0)
        {
            Debug.LogWarning($"SteamLeaderboardManager: Failed to find/create '{name}'");
            OnError?.Invoke($"Could not access leaderboard '{name}'.");
            // Fail any pending fetches for this board so callers don't hang.
            if (_boards.TryGetValue(name, out var failEntry))
            {
                while (failEntry.PendingFetches.Count > 0)
                {
                    var (_, _, _, cb) = failEntry.PendingFetches.Dequeue();
                    cb?.Invoke(null);
                }
                while (failEntry.PendingUploads.Count > 0) failEntry.PendingUploads.Dequeue();
                while (failEntry.PendingFetchAll.Count > 0)
                {
                    var cb = failEntry.PendingFetchAll.Dequeue();
                    cb?.Invoke(null);
                }
            }
            return;
        }

        var entry = GetOrCreate(name);
        entry.Handle  = result.m_hSteamLeaderboard;
        entry.IsReady = true;
        Debug.Log($"SteamLeaderboardManager: '{name}' ready");

        while (entry.PendingUploads.Count > 0)
        {
            var (score, method) = entry.PendingUploads.Dequeue();
            EnqueueUpload(entry.Handle, score, method);
        }

        while (entry.PendingFetches.Count > 0)
        {
            var (reqType, rangeStart, rangeEnd, cb) = entry.PendingFetches.Dequeue();
            EnqueueDownload(entry.Handle, reqType, rangeStart, rangeEnd, cb);
        }

        while (entry.PendingFetchAll.Count > 0)
        {
            var cb = entry.PendingFetchAll.Dequeue();
            EnqueueFetchAllNow(entry, cb);
        }
    }

    private void EnqueueFetchAllNow(BoardEntry entry, Action<List<LeaderboardEntryModel>> callback)
    {
        // Avoid relying on GetLeaderboardEntryCount (can be unreliable on some boards until after first download).
        // Page until a page returns fewer than FETCH_ALL_PAGE_SIZE entries.
        FetchAllPaged(entry.Handle, 1, new List<LeaderboardEntryModel>(), callback);
    }

    private void FetchAllPaged(SteamLeaderboard_t handle, int startRank, List<LeaderboardEntryModel> acc, Action<List<LeaderboardEntryModel>> callback)
    {
        int endRank = startRank + FETCH_ALL_PAGE_SIZE - 1;
        EnqueueDownload(handle, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, startRank, endRank, page =>
        {
            if (page == null) { callback?.Invoke(null); return; }
            if (page.Count == 0) { callback?.Invoke(acc); return; }

            acc.AddRange(page);

            if (page.Count < FETCH_ALL_PAGE_SIZE)
                callback?.Invoke(acc);
            else
                FetchAllPaged(handle, startRank + FETCH_ALL_PAGE_SIZE, acc, callback);
        });
    }

    // ── Uploads ───────────────────────────────────────────────────────────────

    private void EnqueueUpload(SteamLeaderboard_t handle, int score, ELeaderboardUploadScoreMethod method)
    {
        // Each upload gets its own CallResult — a hung callback on one board cannot
        // block uploads to any other board (same pattern as parallel downloads).
        CallResult<LeaderboardScoreUploaded_t> cr = null;
        cr = CallResult<LeaderboardScoreUploaded_t>.Create((result, failure) =>
        {
            _activeUploads.Remove(cr);
            if (failure || result.m_bSuccess == 0)
                Debug.LogWarning($"SteamLeaderboardManager: Score upload failed (failure={failure}).");
            else
                Debug.Log($"SteamLeaderboardManager: Uploaded score {result.m_nScore} (rank {result.m_nGlobalRankNew})");
        });

        _activeUploads.Add(cr); // keep alive until callback fires
        var call = SteamUserStats.UploadLeaderboardScore(handle, method, score, null, 0);
        cr.Set(call);
        Debug.Log($"SteamLeaderboardManager: Uploading score {score} ({method})");
    }

    // ── Downloads ─────────────────────────────────────────────────────────────

    private void EnqueueDownload(SteamLeaderboard_t handle, ELeaderboardDataRequest reqType,
        int rangeStart, int rangeEnd, Action<List<LeaderboardEntryModel>> cb)
    {
        // Declare cr before the lambda so the closure can capture it.
        CallResult<LeaderboardScoresDownloaded_t> cr = null;
        cr = CallResult<LeaderboardScoresDownloaded_t>.Create((result, failure) =>
        {
            _activeDownloads.Remove(cr);

            if (Debug.isDebugBuild)
                Debug.Log($"SteamLeaderboardManager: Download complete failure={failure} entries={result.m_cEntryCount}");

            if (failure)
            {
                Debug.LogWarning("SteamLeaderboardManager: Download failed.");
                cb?.Invoke(null);
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
        });

        _activeDownloads.Add(cr); // keep alive until callback fires
        var call = SteamUserStats.DownloadLeaderboardEntries(handle, reqType, rangeStart, rangeEnd);
        cr.Set(call);
        Debug.Log($"SteamLeaderboardManager: Downloading {reqType} [{rangeStart},{rangeEnd}]");
    }

    private static string ResolveDisplayName(CSteamID steamId)
    {
        if (steamId == CSteamID.Nil) return "Unknown";
        var name = SteamFriends.GetFriendPersonaName(steamId);
        return string.IsNullOrEmpty(name) ? steamId.m_SteamID.ToString() : name;
    }
}
