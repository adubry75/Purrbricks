using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// High Scores screen: shows top 10 local scores.
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
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.78f);

        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin       = Vector2.zero;
        panelRt.anchorMax       = Vector2.one;
        panelRt.sizeDelta       = Vector2.zero;
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panel.transform, false);

        var titleTxt = titleGO.AddComponent<Text>();
        titleTxt.text      = "HIGH SCORES";
        titleTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize  = 90;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color     = UIStyle.AccentGold;

        var titleRt = titleTxt.GetComponent<RectTransform>();
        titleRt.anchorMin       = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax       = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta       = new Vector2(1000f, 110f);
        titleRt.anchoredPosition = new Vector2(0f, 405f);

        var titleOl = titleGO.AddComponent<Outline>();
        titleOl.effectColor    = Color.black;
        titleOl.effectDistance = new Vector2(4f, -4f);

        // Scores list
        _scoresPanel = new GameObject("ScoresPanel");
        _scoresPanel.transform.SetParent(panel.transform, false);

        var spRt = _scoresPanel.AddComponent<RectTransform>();
        spRt.anchorMin       = new Vector2(0.5f, 0.5f);
        spRt.anchorMax       = new Vector2(0.5f, 0.5f);
        spRt.sizeDelta       = new Vector2(800f, 700f);
        spRt.anchoredPosition = Vector2.zero;

        // Column headers
        CreateHeaderText(panel, "#",      new Vector2(-310f, 310f));
        CreateHeaderText(panel, "NAME",   new Vector2( -50f, 310f));
        CreateHeaderText(panel, "SCORE",  new Vector2( 250f, 310f));

        // Divider line
        var lineGO  = new GameObject("Divider");
        lineGO.transform.SetParent(panel.transform, false);
        var lineImg = lineGO.AddComponent<Image>();
        lineImg.color = new Color(0.35f, 0.70f, 1f, 0.45f);
        var lineRt  = lineGO.GetComponent<RectTransform>();
        lineRt.anchorMin       = new Vector2(0.5f, 0.5f);
        lineRt.anchorMax       = new Vector2(0.5f, 0.5f);
        lineRt.sizeDelta       = new Vector2(760f, 2f);
        lineRt.anchoredPosition = new Vector2(0f, 290f);

        UIStyle.CreateButton(panel.transform, "Main Menu", new Vector2(0f, -450f), new Vector2(280f, 75f),
            () => GameManager.Instance?.ShowMainMenu(), UIStyle.AccentBlue);
    }

    private void CreateHeaderText(GameObject parent, string text, Vector2 pos)
    {
        var go = new GameObject("Header_" + text);
        go.transform.SetParent(parent.transform, false);

        var txt = go.AddComponent<Text>();
        txt.text      = text;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 28;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = new Color(0.55f, 0.75f, 1f, 0.70f);

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.sizeDelta       = new Vector2(300f, 36f);
        rt.anchoredPosition = pos;
    }

    private void RefreshScores()
    {
        foreach (Transform child in _scoresPanel.transform)
            Destroy(child.gameObject);

        var scores = HighScoreManager.Instance?.GetTopScores() ?? new List<HighScoreManager.ScoreEntry>();

        for (int i = 0; i < Mathf.Min(10, scores.Count); i++)
        {
            float yPos = 250f - i * 56f;
            CreateScoreEntry(i + 1, scores[i].playerName, scores[i].score, yPos);
        }

        if (scores.Count == 0)
        {
            var emptyGO  = new GameObject("Empty");
            emptyGO.transform.SetParent(_scoresPanel.transform, false);
            var emptyTxt = emptyGO.AddComponent<Text>();
            emptyTxt.text      = "No scores yet — be the first!";
            emptyTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            emptyTxt.fontSize  = 32;
            emptyTxt.alignment = TextAnchor.MiddleCenter;
            emptyTxt.color     = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            var emptyRt = emptyTxt.GetComponent<RectTransform>();
            emptyRt.anchorMin       = new Vector2(0.5f, 0.5f);
            emptyRt.anchorMax       = new Vector2(0.5f, 0.5f);
            emptyRt.sizeDelta       = new Vector2(700f, 50f);
            emptyRt.anchoredPosition = new Vector2(0f, 150f);
        }
    }

    private void CreateScoreEntry(int rank, string playerName, int score, float yPos)
    {
        // Row background (alternating subtle tint)
        var rowGO  = new GameObject($"Row{rank}");
        rowGO.transform.SetParent(_scoresPanel.transform, false);

        var rowImg = rowGO.AddComponent<Image>();
        rowImg.color = rank % 2 == 0
            ? new Color(1f, 1f, 1f, 0.03f)
            : new Color(0f, 0f, 0f, 0f);

        var rowRt = rowGO.GetComponent<RectTransform>();
        rowRt.anchorMin       = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax       = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta       = new Vector2(760f, 52f);
        rowRt.anchoredPosition = new Vector2(0f, yPos);

        // Medal color for top 3
        Color rankColor = rank switch
        {
            1 => new Color(1.00f, 0.84f, 0.10f), // gold
            2 => new Color(0.75f, 0.75f, 0.80f), // silver
            3 => new Color(0.80f, 0.50f, 0.30f), // bronze
            _ => new Color(0.65f, 0.65f, 0.70f)
        };

        CreateEntryText(rowGO, $"{rank}", new Vector2(-310f, 0f), 38, rankColor);
        CreateEntryText(rowGO, playerName, new Vector2(-50f, 0f), 34, Color.white);
        CreateEntryText(rowGO, score.ToString("N0"), new Vector2(250f, 0f), 38, UIStyle.AccentGreen);
    }

    private void CreateEntryText(GameObject parent, string text, Vector2 pos, int fontSize, Color color)
    {
        var go = new GameObject("ET");
        go.transform.SetParent(parent.transform, false);

        var txt = go.AddComponent<Text>();
        txt.text      = text;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = color;

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.sizeDelta       = new Vector2(300f, 54f);
        rt.anchoredPosition = pos;

        var ol = go.AddComponent<Outline>();
        ol.effectColor    = Color.black;
        ol.effectDistance = new Vector2(2f, -2f);
    }

    public void Show()  { gameObject.SetActive(true); RefreshScores(); }
    public void Hide()  { gameObject.SetActive(false); }
    public void ShowScores() { Show(); }
}
