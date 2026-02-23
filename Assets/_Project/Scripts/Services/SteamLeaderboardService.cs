using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class SteamLeaderboardService : MonoBehaviour
{
    [SerializeField]
    private string leaderboardName = "Purrbricks_HighScores";

    public event Action<List<LeaderboardEntryModel>> OnTopScoresUpdated;
    public event Action<List<LeaderboardEntryModel>> OnAroundUserUpdated;
    public event Action<string> OnError;

    private SteamLeaderboard_t _leaderboardHandle;
    private bool _isInitializing;
    private bool _isInitialized;
    private readonly Queue<int> _pendingScoreSubmissions = new Queue<int>();
    private int? _pendingTopCount;
    private (int before, int after)? _pendingAroundRequest;
    private bool _pendingInitialization;

    private CallResult<LeaderboardFindResult_t> _findResult;
    private CallResult<LeaderboardScoreUploaded_t> _uploadResult;
    private CallResult<LeaderboardScoresDownloaded_t> _downloadTopResult;
    private CallResult<LeaderboardScoresDownloaded_t> _downloadAroundResult;

    private void Awake()
    {
        _findResult = CallResult<LeaderboardFindResult_t>.Create(OnLeaderboardFound);
        _uploadResult = CallResult<LeaderboardScoreUploaded_t>.Create(OnScoreUploaded);
        _downloadTopResult = CallResult<LeaderboardScoresDownloaded_t>.Create(OnTopScoresDownloaded);
        _downloadAroundResult = CallResult<LeaderboardScoresDownloaded_t>.Create(OnAroundScoresDownloaded);
    }

    private void Start()
    {
        _pendingInitialization = true;
        TryInitializeWhenReady();
    }

    private void Update()
    {
        if (_pendingInitialization)
        {
            TryInitializeWhenReady();
        }
    }

    private void TryInitializeWhenReady()
    {
        if (!_pendingInitialization)
        {
            return;
        }

        if (SteamworksBootstrap.Instance?.IsSteamAvailable == true)
        {
            _pendingInitialization = false;
            InitializeLeaderboard();
        }
        else
        {
            Debug.Log("SteamLeaderboardService: Waiting for SteamworksBootstrap to initialize.");
        }
    }

    public void InitializeLeaderboard()
    {
        _pendingInitialization = false;

        if (string.IsNullOrEmpty(leaderboardName))
        {
            InvokeError("SteamLeaderboardService: Leaderboard name must not be empty.");
            return;
        }

        if (!EnsureSteamReady("InitializeLeaderboard"))
        {
            return;
        }

        if (_isInitialized)
        {
            return;
        }

        if (_isInitializing)
        {
            Debug.Log("SteamLeaderboardService: Already initializing leaderboard.");
            return;
        }

        Debug.Log($"SteamLeaderboardService: Finding or creating leaderboard '{leaderboardName}'.");
        var steamCall = SteamUserStats.FindOrCreateLeaderboard(leaderboardName, ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending, ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);
        _findResult.Set(steamCall);
        _isInitializing = true;
    }

    public void SubmitScore(int score)
    {
        if (!EnsureSteamReady("SubmitScore"))
        {
            return;
        }

        if (!_isInitialized)
        {
            Debug.Log("SteamLeaderboardService: Leaderboard not initialized. Queuing score submission.");
            _pendingScoreSubmissions.Enqueue(score);
            InitializeLeaderboard();
            return;
        }

        UploadScore(score);
    }

    public void FetchTopScores(int count)
    {
        if (!EnsureSteamReady("FetchTopScores"))
        {
            return;
        }

        if (!_isInitialized)
        {
            Debug.Log("SteamLeaderboardService: Leaderboard not ready. Caching top score request.");
            _pendingTopCount = count;
            InitializeLeaderboard();
            return;
        }

        StartTopDownload(count);
    }

    public void FetchScoresAroundUser(int before, int after)
    {
        if (!EnsureSteamReady("FetchScoresAroundUser"))
        {
            return;
        }

        if (!_isInitialized)
        {
            Debug.Log("SteamLeaderboardService: Leaderboard not ready. Caching around-user request.");
            _pendingAroundRequest = (before, after);
            InitializeLeaderboard();
            return;
        }

        StartAroundDownload(before, after);
    }

    private bool EnsureSteamReady(string caller)
    {
        if (SteamworksBootstrap.Instance?.IsSteamAvailable == true)
        {
            return true;
        }

        InvokeError($"SteamLeaderboardService: {caller} aborted because Steam is not available.");
        return false;
    }

    private void UploadScore(int score)
    {
        if (_leaderboardHandle.m_SteamLeaderboard == 0)
        {
            InvokeError("SteamLeaderboardService: Cannot upload score. Leaderboard handle is invalid.");
            return;
        }

        var uploadCall = SteamUserStats.UploadLeaderboardScore(_leaderboardHandle, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest, score, null, 0);
        _uploadResult.Set(uploadCall);
        Debug.Log($"SteamLeaderboardService: Uploading score {score}.");
    }

    private void StartTopDownload(int count)
    {
        if (count <= 0)
        {
            InvokeError("SteamLeaderboardService: Top score count must be greater than zero.");
            return;
        }

        var topCall = SteamUserStats.DownloadLeaderboardEntries(_leaderboardHandle, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, 1, count);
        _downloadTopResult.Set(topCall);
        Debug.Log($"SteamLeaderboardService: Requesting top {count} leaderboard entries.");
    }

    private void StartAroundDownload(int before, int after)
    {
        before = Mathf.Max(0, before);
        after = Mathf.Max(0, after);

        if (before + after == 0)
        {
            before = 5;
            after = 5;
        }

        var aroundCall = SteamUserStats.DownloadLeaderboardEntries(_leaderboardHandle, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser, -before, after);
        _downloadAroundResult.Set(aroundCall);
        Debug.Log($"SteamLeaderboardService: Requesting leaderboard entries around user ({before} before, {after} after).");
    }

    private void OnLeaderboardFound(LeaderboardFindResult_t result, bool failure)
    {
        _isInitializing = false;

        if (failure || result.m_bLeaderboardFound == 0 || result.m_hSteamLeaderboard.m_SteamLeaderboard == 0)
        {
            InvokeError("SteamLeaderboardService: Failed to find or create leaderboard.");
            return;
        }

        _leaderboardHandle = result.m_hSteamLeaderboard;
        _isInitialized = true;
        Debug.Log($"SteamLeaderboardService: Leaderboard '{leaderboardName}' ready (handle {result.m_hSteamLeaderboard.m_SteamLeaderboard}).");

        FlushPendingSubmissions();

        if (_pendingTopCount.HasValue)
        {
            var count = _pendingTopCount.Value;
            _pendingTopCount = null;
            StartTopDownload(count);
        }

        if (_pendingAroundRequest.HasValue)
        {
            var request = _pendingAroundRequest.Value;
            _pendingAroundRequest = null;
            StartAroundDownload(request.before, request.after);
        }

        var sort = SteamUserStats.GetLeaderboardSortMethod(_leaderboardHandle);
        var display = SteamUserStats.GetLeaderboardDisplayType(_leaderboardHandle);
        Debug.Log($"Leaderboard sort={sort}, display={display}");

    }

    private void OnScoreUploaded(LeaderboardScoreUploaded_t result, bool failure)
    {
        if (failure || result.m_bSuccess == 0)
        {
            InvokeError("SteamLeaderboardService: Score upload failed.");
            return;
        }

        Debug.Log($"SteamLeaderboardService: Score upload succeeded ({result.m_nScore} - new rank {result.m_nGlobalRankNew}).");

        FetchTopScores(10);


    }

    private void OnTopScoresDownloaded(LeaderboardScoresDownloaded_t result, bool failure)
    {
        if (failure || result.m_cEntryCount == 0)
        {
            InvokeError("SteamLeaderboardService: Failed to download top scores.");
            return;
        }

        var entries = ParseDownloadedEntries(result);
        OnTopScoresUpdated?.Invoke(entries);
    }

    private void OnAroundScoresDownloaded(LeaderboardScoresDownloaded_t result, bool failure)
    {
        if (failure || result.m_cEntryCount == 0)
        {
            InvokeError("SteamLeaderboardService: Failed to download scores around the user.");
            return;
        }

        var entries = ParseDownloadedEntries(result);
        OnAroundUserUpdated?.Invoke(entries);
    }

    private List<LeaderboardEntryModel> ParseDownloadedEntries(LeaderboardScoresDownloaded_t result)
    {
        var entries = new List<LeaderboardEntryModel>();

        for (int i = 0; i < result.m_cEntryCount; i++)
        {
            if (SteamUserStats.GetDownloadedLeaderboardEntry(result.m_hSteamLeaderboardEntries, i, out var entry, null, 0))
            {
                var displayName = ResolveDisplayName(entry.m_steamIDUser);
                entries.Add(new LeaderboardEntryModel(entry.m_nGlobalRank, entry.m_nScore, entry.m_steamIDUser, displayName));
            }
        }

        return entries;
    }

    private static string ResolveDisplayName(CSteamID steamId)
    {
        if (steamId == CSteamID.Nil)
        {
            return "Unknown";
        }

        var friendlyName = SteamFriends.GetFriendPersonaName(steamId);
        if (string.IsNullOrEmpty(friendlyName))
        {
            return steamId.m_SteamID.ToString();
        }

        return friendlyName;
    }

    private void FlushPendingSubmissions()
    {
        while (_pendingScoreSubmissions.Count > 0)
        {
            UploadScore(_pendingScoreSubmissions.Dequeue());
        }
    }

    private void InvokeError(string message)
    {
        Debug.LogWarning(message);
        OnError?.Invoke(message);
    }
}

