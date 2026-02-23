using System;
using Steamworks;
using UnityEngine;

/// <summary>
/// Bootstraps Steamworks. For local editor testing place steam_appid.txt with "480" next to the executable so the Spacewar AppId is used, and run Steam while testing.
/// Built versions must be launched from the Steam client and target your real AppId with leaderboards configured through Steamworks.
/// </summary>
public class SteamworksBootstrap : MonoBehaviour
{
    public static SteamworksBootstrap Instance { get; private set; }

    public bool IsSteamAvailable => _steamInitialized;

    private bool _steamInitialized;
    private bool _initAttempted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (_initAttempted)
        {
            return;
        }

        _initAttempted = true;
        try
        {
            Debug.Log("SteamworksBootstrap: Initializing SteamAPI...");
            _steamInitialized = SteamAPI.Init();
        }
        catch (DllNotFoundException e)
        {
            Debug.LogError("SteamworksBootstrap: Steamworks DLL not found: " + e.Message);
            _steamInitialized = false;
        }

        if (!_steamInitialized)
        {
            Debug.LogWarning("SteamworksBootstrap: SteamAPI.Init failed. Leaderboard features will remain disabled while Steam is not running or the AppId is missing.");
            enabled = false;
            return;
        }

        Debug.Log("SteamworksBootstrap: SteamAPI initialized successfully.");
    }

    private void Update()
    {
        if (_steamInitialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    private void OnApplicationQuit()
    {
        if (_steamInitialized)
        {
            Debug.Log("SteamworksBootstrap: Shutting down SteamAPI.");
            SteamAPI.Shutdown();
        }
    }
}

