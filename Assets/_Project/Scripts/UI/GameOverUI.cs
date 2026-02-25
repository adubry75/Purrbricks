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
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

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
            new Vector2(-160f, -60f), new Vector2(280f, 70f),
            OnLeaderboard, UIStyle.AccentBlue);

        UIStyle.CreateButton(panel.transform, "Main Menu",
            new Vector2(160f, -60f), new Vector2(280f, 70f),
            OnMainMenu, UIStyle.AccentGold);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowGameOver(int finalScore)
    {
        gameObject.SetActive(true);

        if (_titleText != null)
        {
            _titleText.text  = "GAME OVER";
            _titleText.color = UIStyle.AccentRed;
        }
        if (_scoreText != null) _scoreText.text = $"Final Score: {finalScore:N0}";

        // Auto-submit to Steam global board (KeepBest — no name needed)
        SteamLeaderboardManager.Instance?.SubmitScore("Purrbricks_HighScores", finalScore);
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

        SteamLeaderboardManager.Instance?.SubmitScore("Purrbricks_HighScores", finalScore);
    }

    private void OnLeaderboard() => GameManager.Instance?.ShowHighScoresAfterGameOver();
    private void OnMainMenu()    => GameManager.Instance?.ShowMainMenu();

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
}
