using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Game Over screen: shows final score and provides Leaderboard / Main Menu buttons.
/// Score is submitted to Steam automatically — no name entry needed.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    private Canvas _canvas;
    private Text   _titleText;
    private Text   _scoreText;
    private Button _mainMenuBtn;

    [Header("Button Sprites")]
    [SerializeField] private Sprite _leaderboardSprite;
    [SerializeField] private Sprite _mainMenuSprite;

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.88f);

        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin        = Vector2.zero;
        panelRt.anchorMax        = Vector2.one;
        panelRt.sizeDelta        = Vector2.zero;
        panelRt.anchoredPosition = new Vector2(-200f, 0f);

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panel.transform, false);
        _titleText           = titleGO.AddComponent<Text>();
        _titleText.text      = "GAME OVER";
        _titleText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _titleText.fontSize  = 100;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color     = UIStyle.AccentRed;
        var titleRt = _titleText.GetComponent<RectTransform>();
        titleRt.anchorMin        = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax        = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta        = new Vector2(800f, 120f);
        titleRt.anchoredPosition = new Vector2(0f, 220f);
        var titleOl = titleGO.AddComponent<Outline>();
        titleOl.effectColor    = Color.black;
        titleOl.effectDistance = new Vector2(4f, -4f);

        // Score
        var scoreGO = new GameObject("ScoreText");
        scoreGO.transform.SetParent(panel.transform, false);
        _scoreText           = scoreGO.AddComponent<Text>();
        _scoreText.text      = "Final Score: 0";
        _scoreText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _scoreText.fontSize  = 60;
        _scoreText.fontStyle = FontStyle.Bold;
        _scoreText.alignment = TextAnchor.MiddleCenter;
        _scoreText.color     = Color.white;
        var scoreRt = _scoreText.GetComponent<RectTransform>();
        scoreRt.anchorMin        = new Vector2(0.5f, 0.5f);
        scoreRt.anchorMax        = new Vector2(0.5f, 0.5f);
        scoreRt.sizeDelta        = new Vector2(800f, 80f);
        scoreRt.anchoredPosition = new Vector2(0f, 90f);
        var scoreOl = scoreGO.AddComponent<Outline>();
        scoreOl.effectColor    = Color.black;
        scoreOl.effectDistance = new Vector2(3f, -3f);

        // Buttons (side by side)
        UIStyle.CreateButton(panel.transform, "Leaderboard",
            new Vector2(-200f, -60f), new Vector2(280f, 70f),
            OnLeaderboard, UIStyle.AccentBlue);

        _mainMenuBtn = UIStyle.CreateButton(panel.transform, "Main Menu",
            new Vector2(200f, -60f), new Vector2(280f, 70f),
            OnMainMenu, UIStyle.AccentGold);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowGameOver(int finalScore)
    {
        gameObject.SetActive(true);
        UINavController.SetDefault(_mainMenuBtn?.gameObject);

        if (_titleText != null)
        {
            _titleText.text  = "GAME OVER";
            _titleText.color = UIStyle.AccentRed;
        }
        if (_scoreText != null) _scoreText.text = $"Final Score: {finalScore:N0}";

        // Auto-submit to Steam global boards (KeepBest)
        if (finalScore > 0)
        {
            string allTimeBoard = PurrbricksLeaderboards.OverallAllTime;
            string weeklyBoard  = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Weekly);
            string dailyBoard   = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Daily);

            SteamLeaderboardManager.Instance?.SubmitScore(allTimeBoard, finalScore);
            SteamLeaderboardManager.Instance?.SubmitScore(weeklyBoard,  finalScore);
            SteamLeaderboardManager.Instance?.SubmitScore(dailyBoard,   finalScore);
        }
    }

    public void ShowGameComplete(int finalScore)
    {
        gameObject.SetActive(true);

        if (_titleText != null)
        {
            _titleText.text  = "ALL LEVELS CLEARED!";
            _titleText.color = UIStyle.AccentGreen;
        }
        if (_scoreText != null) _scoreText.text = $"Final Score: {finalScore:N0}";

        if (finalScore > 0)
        {
            string allTimeBoard = PurrbricksLeaderboards.OverallAllTime;
            string weeklyBoard  = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Weekly);
            string dailyBoard   = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Daily);

            SteamLeaderboardManager.Instance?.SubmitScore(allTimeBoard, finalScore);
            SteamLeaderboardManager.Instance?.SubmitScore(weeklyBoard,  finalScore);
            SteamLeaderboardManager.Instance?.SubmitScore(dailyBoard,   finalScore);
        }
    }

    /// <summary>
    /// Community mode game over — shows Retry and Browse Levels instead of Leaderboard / Main Menu.
    /// </summary>
    public void ShowCommunityGameOver(int finalScore)
    {
        gameObject.SetActive(true);

        if (_titleText != null)
        {
            _titleText.text  = "GAME OVER";
            _titleText.color = UIStyle.AccentRed;
        }
        if (_scoreText != null) _scoreText.text = $"Score: {finalScore:N0}";

        // Temporarily replace button text to show community actions.
        // The real label GOs are children of the panel — find and relabel them.
        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            var txt = btn.GetComponentInChildren<Text>();
            if (txt == null) continue;
            if (txt.text == "Leaderboard")
            {
                txt.text = "Retry";
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnRetryClicked);
            }
            else if (txt.text == "Main Menu")
            {
                txt.text = "Browse Levels";
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnBrowseLevels);
            }
        }
    }

    private void OnLeaderboard() => GameManager.Instance?.ShowHighScoresAfterGameOver();
    private void OnMainMenu()    => GameManager.Instance?.ShowMainMenu();

    private void OnRetryClicked()
    {
        // Restore button labels for next regular GameOver
        RestoreNormalButtons();
        Hide();
        GameManager.Instance?.RetryCommunityLevel();
    }

    private void OnBrowseLevels()
    {
        RestoreNormalButtons();
        Hide();
        var browser = Object.FindFirstObjectByType<CommunityBrowserUI>(FindObjectsInactive.Include);
        if (browser != null) browser.Show();
        else GameManager.Instance?.ShowMainMenu();
    }

    private void RestoreNormalButtons()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            var txt = btn.GetComponentInChildren<Text>();
            if (txt == null) continue;
            if (txt.text == "Retry")
            {
                txt.text = "Leaderboard";
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnLeaderboard);
            }
            else if (txt.text == "Browse Levels")
            {
                txt.text = "Main Menu";
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnMainMenu);
            }
        }
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
}
