using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// High Scores screen: shows top 10 local scores.
/// Future: tabs for Local / Global leaderboards.
/// </summary>
public class HighScoresUI : MonoBehaviour
{
    private Canvas _canvas;
    private GameObject _scoresPanel;

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Background panel - offset left to center on playfield
        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.75f);

        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.sizeDelta = Vector2.zero;
        // Shift left by 1/12th of screen width (half of 1/6th reserved area)
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        // Title
        CreateText(panel, "HIGH SCORES", new Vector2(0f, 400f), 90, new Color(1f, 0.6f, 0.2f));

        // Scores list panel
        _scoresPanel = new GameObject("ScoresPanel");
        _scoresPanel.transform.SetParent(panel.transform, false);

        var scoresPanelRt = _scoresPanel.AddComponent<RectTransform>();
        scoresPanelRt.anchorMin = new Vector2(0.5f, 0.5f);
        scoresPanelRt.anchorMax = new Vector2(0.5f, 0.5f);
        scoresPanelRt.sizeDelta = new Vector2(800f, 700f);
        scoresPanelRt.anchoredPosition = new Vector2(0f, 0f);

        // Back button
        CreateButton(panel, "Back", new Vector2(0f, -450f), () => GameManager.Instance?.ShowMainMenu());

        // Future: Tab buttons for Local / Global
        // CreateButton(panel, "Local", new Vector2(-150f, 350f), OnLocalTab);
        // CreateButton(panel, "Global", new Vector2(150f, 350f), OnGlobalTab);
    }

    private void CreateText(GameObject parent, string text, Vector2 pos, int fontSize, Color color)
    {
        var go = new GameObject(text);
        go.transform.SetParent(parent.transform, false);

        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1000f, fontSize + 30f);
        rt.anchoredPosition = pos;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(4f, -4f);
    }

    private void CreateButton(GameObject parent, string label, Vector2 pos, System.Action onClick)
    {
        var btnGO = new GameObject(label + "Button");
        btnGO.transform.SetParent(parent.transform, false);

        var btnImg = btnGO.AddComponent<Image>();
        var button = btnGO.AddComponent<Button>();
        button.targetGraphic = btnImg;

        var colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.6f, 1f);
        colors.highlightedColor = new Color(0.3f, 0.8f, 1f);
        colors.pressedColor = new Color(0.1f, 0.4f, 0.8f);
        button.colors = colors;

        button.onClick.AddListener(() => onClick?.Invoke());

        var btnRt = btnGO.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.sizeDelta = new Vector2(250f, 70f);
        btnRt.anchoredPosition = pos;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);

        var btnText = textGO.AddComponent<Text>();
        btnText.text = label;
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 36;
        btnText.fontStyle = FontStyle.Bold;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;

        var textRt = btnText.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
    }

    private void RefreshScores()
    {
        // Clear old entries
        foreach (Transform child in _scoresPanel.transform)
            Destroy(child.gameObject);

        // Get scores from manager
        var scores = HighScoreManager.Instance?.GetTopScores() ?? new List<HighScoreManager.ScoreEntry>();

        // Display top 10
        for (int i = 0; i < Mathf.Min(10, scores.Count); i++)
        {
            float yPos = 300f - i * 65f;
            CreateScoreEntry(i + 1, scores[i].playerName, scores[i].score, yPos);
        }
    }

    private void CreateScoreEntry(int rank, string playerName, int score, float yPos)
    {
        var entryGO = new GameObject($"Score{rank}");
        entryGO.transform.SetParent(_scoresPanel.transform, false);

        var entryRt = entryGO.AddComponent<RectTransform>();
        entryRt.anchorMin = new Vector2(0.5f, 0.5f);
        entryRt.anchorMax = new Vector2(0.5f, 0.5f);
        entryRt.sizeDelta = new Vector2(750f, 60f);
        entryRt.anchoredPosition = new Vector2(0f, yPos);

        // Rank
        CreateEntryText(entryGO, $"{rank}.", new Vector2(-320f, 0f), 40, rank <= 3 ? Color.yellow : Color.white);

        // Player name
        CreateEntryText(entryGO, playerName, new Vector2(-50f, 0f), 36, Color.white);

        // Score
        CreateEntryText(entryGO, score.ToString("N0"), new Vector2(250f, 0f), 40, new Color(0.4f, 1f, 0.6f));
    }

    private void CreateEntryText(GameObject parent, string text, Vector2 pos, int fontSize, Color color)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent.transform, false);

        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(300f, 60f);
        rt.anchoredPosition = pos;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
    }

    public void ShowScores()
    {
        gameObject.SetActive(true);
        RefreshScores();
    }

    public void Show() { gameObject.SetActive(true); RefreshScores(); }
    public void Hide() { gameObject.SetActive(false); }
}
