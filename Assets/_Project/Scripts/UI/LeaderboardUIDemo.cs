using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LeaderboardUIDemo : MonoBehaviour
{
    [SerializeField]
    private SteamLeaderboardService leaderboardService;

    [SerializeField]
    private TMP_InputField scoreInput;

    [SerializeField]
    private Button submitButton;

    [SerializeField]
    private Button refreshTopButton;

    [SerializeField]
    private Button aroundMeButton;

    [SerializeField]
    private Button randomScoreButton;

    [SerializeField]
    private TextMeshProUGUI resultsText;

    [SerializeField]
    private GameObject steamUnavailablePanel;

    [SerializeField]
    private TextMeshProUGUI steamUnavailableText;

    private const int DefaultTopCount = 10;
    private const int AroundBefore = 4;
    private const int AroundAfter = 5;

    private void Awake()
    {
        RegisterButtonCallbacks();

        if (leaderboardService == null)
        {
            Debug.LogError("LeaderboardUIDemo: Leaderboard service reference is missing.");
            return;
        }

        leaderboardService.OnTopScoresUpdated += HandleTopScoresUpdated;
        leaderboardService.OnAroundUserUpdated += HandleAroundUserUpdated;
        leaderboardService.OnError += HandleServiceError;
    }

    private void OnEnable()
    {
        UpdateSteamAvailability();
    }

    private void OnDisable()
    {
        if (leaderboardService != null)
        {
            leaderboardService.OnTopScoresUpdated -= HandleTopScoresUpdated;
            leaderboardService.OnAroundUserUpdated -= HandleAroundUserUpdated;
            leaderboardService.OnError -= HandleServiceError;
        }
    }

    private void OnDestroy()
    {
        UnregisterButtonCallbacks();
    }

    private void Start()
    {
        UpdateSteamAvailability();

        if (SteamworksBootstrap.Instance?.IsSteamAvailable == true && leaderboardService != null)
        {
            SetResultText("Loading top scores...");
            leaderboardService.FetchTopScores(DefaultTopCount);
        }
        else
        {
            SetResultText("Steam not available. High scores are disabled until Steam is running.");
        }
    }

    public void OnSubmitScore()
    {
        Debug.Log($"Here1");
        if (!TryParseScore(out var score))
        {
            score = Random.Range(100, 1000);
            Debug.Log($"LeaderboardUIDemo: Using random score {score} because input was invalid.");
        }

        Debug.Log($"LeaderboardUIDemo: OnSubmitScore triggered (score {score}).");
        if (leaderboardService == null)
        {
            Debug.LogWarning("LeaderboardUIDemo: leaderboardService is null; cannot submit score.");
            return;
        }
        Debug.Log($"Here2");

        leaderboardService.SubmitScore(score);
        Debug.Log($"Here3");
    }

    public void OnSubmitRandomScore()
    {
        var score = Random.Range(100, 1000);
        Debug.Log($"LeaderboardUIDemo: OnSubmitRandomScore triggered (score {score}).");
        if (leaderboardService == null)
        {
            Debug.LogWarning("LeaderboardUIDemo: leaderboardService is null; cannot submit random score.");
            return;
        }

        leaderboardService.SubmitScore(score);
    }

    public void OnRefreshTop()
    {
        Debug.Log("LeaderboardUIDemo: OnRefreshTop triggered.");
        if (leaderboardService == null)
        {
            Debug.LogWarning("LeaderboardUIDemo: leaderboardService is null; cannot fetch top scores.");
            return;
        }

        leaderboardService.FetchTopScores(DefaultTopCount);
    }

    public void OnViewAroundMe()
    {
        Debug.Log("LeaderboardUIDemo: OnViewAroundMe triggered.");
        if (leaderboardService == null)
        {
            Debug.LogWarning("LeaderboardUIDemo: leaderboardService is null; cannot fetch around-user scores.");
            return;
        }

        leaderboardService.FetchScoresAroundUser(AroundBefore, AroundAfter);
    }

    private bool TryParseScore(out int score)
    {
        score = 0;
        if (scoreInput == null)
        {
            return false;
        }

        return int.TryParse(scoreInput.text, out score);
    }

    private void HandleTopScoresUpdated(List<LeaderboardEntryModel> entries)
    {
        SetResultText(FormatEntries(entries, "Top " + DefaultTopCount + " Users"));
    }

    private void HandleAroundUserUpdated(List<LeaderboardEntryModel> entries)
    {
        SetResultText(FormatEntries(entries, "Around Me"));
    }

    private void HandleServiceError(string message)
    {
        SetResultText(message);
    }

    private string FormatEntries(List<LeaderboardEntryModel> entries, string heading)
    {
        if (entries == null || entries.Count == 0)
        {
            return heading + "(no data)";
        }

        var builder = new StringBuilder();
        builder.AppendLine(heading + ":");

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            builder.AppendLine($"{entry.Rank}. {entry.DisplayName} - {entry.Score}");
        }

        return builder.ToString();
    }

    private void SetResultText(string text)
    {
        if (resultsText != null)
        {
            resultsText.text = text;
        }
    }

    private void UpdateSteamAvailability()
    {
        var steamAvailable = SteamworksBootstrap.Instance?.IsSteamAvailable == true;

        if (steamUnavailablePanel != null)
        {
            steamUnavailablePanel.SetActive(!steamAvailable);
        }

        if (steamUnavailableText != null)
        {
            steamUnavailableText.text = steamAvailable ? string.Empty : "Steam not available: run Steam or use steam_appid.txt with 480 for editor testing.";
        }

        SetButtonState(submitButton, steamAvailable);
        SetButtonState(refreshTopButton, steamAvailable);
        SetButtonState(aroundMeButton, steamAvailable);
        SetButtonState(randomScoreButton, steamAvailable);
    }

    private static void SetButtonState(Button button, bool enabled)
    {
        if (button != null)
        {
            button.interactable = enabled;
        }
    }

    private void RegisterButtonCallbacks()
    {
        AddButtonListener(submitButton, OnSubmitScore);
        AddButtonListener(randomScoreButton, OnSubmitRandomScore);
        AddButtonListener(refreshTopButton, OnRefreshTop);
        AddButtonListener(aroundMeButton, OnViewAroundMe);
    }

    private void UnregisterButtonCallbacks()
    {
        RemoveButtonListener(submitButton, OnSubmitScore);
        RemoveButtonListener(randomScoreButton, OnSubmitRandomScore);
        RemoveButtonListener(refreshTopButton, OnRefreshTop);
        RemoveButtonListener(aroundMeButton, OnViewAroundMe);
    }

    private static void AddButtonListener(Button button, UnityAction action)
    {
        if (button == null || action == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void RemoveButtonListener(Button button, UnityAction action)
    {
        if (button == null || action == null) return;
        button.onClick.RemoveListener(action);
    }
}

