using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// High Scores screen: LOCAL tab shows top-10 PlayerPrefs scores;
/// GLOBAL tab fetches and displays the Steam leaderboard top-10.
/// Defaults to LOCAL on every Show().
/// </summary>
public class HighScoresUI : MonoBehaviour
{
    private Canvas     _canvas;
    private GameObject _localPanel;
    private GameObject _globalPanel;
    private Text       _globalStatusText;
    private Image      _localTabBg;
    private Image      _globalTabBg;
    private Text       _localTabText;
    private Text       _globalTabText;
    private bool       _showingGlobal;

    [SerializeField] private Sprite _mainMenuSprite;

    private const string GLOBAL_BOARD = "Purrbricks_HighScores";

    private static readonly Color TabActiveColor   = new Color(0.10f, 0.38f, 0.85f, 0.95f);
    private static readonly Color TabInactiveColor = new Color(0.07f, 0.10f, 0.20f, 0.80f);
    private static readonly Color TabActiveText    = Color.white;
    private static readonly Color TabInactiveText  = new Color(0.50f, 0.62f, 0.72f, 0.85f);

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    // ── UI Construction ───────────────────────────────────────────────────────

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
        panelRt.anchorMin        = Vector2.zero;
        panelRt.anchorMax        = Vector2.one;
        panelRt.sizeDelta        = Vector2.zero;
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        // ── Title ─────────────────────────────────────────────────────────────
        var titleGO  = new GameObject("Title");
        titleGO.transform.SetParent(panel.transform, false);
        var titleTxt = titleGO.AddComponent<Text>();
        titleTxt.text      = "HIGH SCORES";
        titleTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize  = 90;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color     = UIStyle.AccentGold;

        var titleRt = titleTxt.GetComponent<RectTransform>();
        titleRt.anchorMin        = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax        = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta        = new Vector2(1000f, 110f);
        titleRt.anchoredPosition = new Vector2(0f, 405f);

        var titleOl = titleGO.AddComponent<Outline>();
        titleOl.effectColor    = Color.black;
        titleOl.effectDistance = new Vector2(4f, -4f);

        // ── Tab buttons ───────────────────────────────────────────────────────
        BuildTabButton(panel, "LOCAL",  new Vector2(-130f, 318f),
            out _localTabBg,  out _localTabText,  SwitchToLocal);
        BuildTabButton(panel, "GLOBAL", new Vector2( 130f, 318f),
            out _globalTabBg, out _globalTabText, SwitchToGlobal);

        // ── Column headers (shared, always visible) ───────────────────────────
        CreateHeaderText(panel, "#",     new Vector2(-310f, 248f));
        CreateHeaderText(panel, "NAME",  new Vector2( -50f, 248f));
        CreateHeaderText(panel, "SCORE", new Vector2( 250f, 248f));

        // ── Divider ───────────────────────────────────────────────────────────
        var lineGO  = new GameObject("Divider");
        lineGO.transform.SetParent(panel.transform, false);
        var lineImg = lineGO.AddComponent<Image>();
        lineImg.color = new Color(0.35f, 0.70f, 1f, 0.45f);
        var lineRt  = lineGO.GetComponent<RectTransform>();
        lineRt.anchorMin        = new Vector2(0.5f, 0.5f);
        lineRt.anchorMax        = new Vector2(0.5f, 0.5f);
        lineRt.sizeDelta        = new Vector2(760f, 2f);
        lineRt.anchoredPosition = new Vector2(0f, 228f);

        // ── Local content panel ───────────────────────────────────────────────
        _localPanel = new GameObject("LocalPanel");
        _localPanel.transform.SetParent(panel.transform, false);
        var lpRt = _localPanel.AddComponent<RectTransform>();
        lpRt.anchorMin        = new Vector2(0.5f, 0.5f);
        lpRt.anchorMax        = new Vector2(0.5f, 0.5f);
        lpRt.sizeDelta        = new Vector2(800f, 700f);
        lpRt.anchoredPosition = Vector2.zero;

        // ── Global content panel ──────────────────────────────────────────────
        _globalPanel = new GameObject("GlobalPanel");
        _globalPanel.transform.SetParent(panel.transform, false);
        var gpRt = _globalPanel.AddComponent<RectTransform>();
        gpRt.anchorMin        = new Vector2(0.5f, 0.5f);
        gpRt.anchorMax        = new Vector2(0.5f, 0.5f);
        gpRt.sizeDelta        = new Vector2(800f, 700f);
        gpRt.anchoredPosition = Vector2.zero;

        // Status text (loading / error / no scores) inside the global panel
        var statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(_globalPanel.transform, false);
        _globalStatusText           = statusGO.AddComponent<Text>();
        _globalStatusText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _globalStatusText.fontSize  = 30;
        _globalStatusText.alignment = TextAnchor.MiddleCenter;
        _globalStatusText.color     = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        var statusRt = statusGO.GetComponent<RectTransform>();
        statusRt.anchorMin        = new Vector2(0.5f, 0.5f);
        statusRt.anchorMax        = new Vector2(0.5f, 0.5f);
        statusRt.sizeDelta        = new Vector2(700f, 80f);
        statusRt.anchoredPosition = new Vector2(0f, 100f);

        // ── Main Menu button ──────────────────────────────────────────────────
        if (_mainMenuSprite != null)
            CreateImageButton(panel.transform, _mainMenuSprite,
                new Vector2(0f, -392f), () => GameManager.Instance?.ShowMainMenu());
        else
            UIStyle.CreateButton(panel.transform, "Main Menu",
                new Vector2(0f, -392f), new Vector2(280f, 75f),
                () => GameManager.Instance?.ShowMainMenu(), UIStyle.AccentBlue);
    }

    private void BuildTabButton(GameObject parent, string label, Vector2 pos,
        out Image bgOut, out Text txtOut, UnityAction onClick)
    {
        var go = new GameObject("Tab_" + label);
        go.transform.SetParent(parent.transform, false);

        var bg    = go.AddComponent<Image>();
        bg.color  = TabInactiveColor;
        bgOut     = bg;

        var outline = go.AddComponent<Outline>();
        outline.effectColor    = new Color(0.35f, 0.60f, 1f, 0.35f);
        outline.effectDistance = new Vector2(1f, -1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(onClick);
        var cols = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
        cols.pressedColor     = new Color(0.85f, 0.85f, 0.85f);
        btn.colors = cols;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(220f, 56f);
        rt.anchoredPosition = pos;

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var txt           = txtGO.AddComponent<Text>();
        txt.text          = label;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = 28;
        txt.fontStyle     = FontStyle.Bold;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.color         = TabInactiveText;
        txtOut            = txt;

        var txtRt         = txt.GetComponent<RectTransform>();
        txtRt.anchorMin   = Vector2.zero;
        txtRt.anchorMax   = Vector2.one;
        txtRt.sizeDelta   = Vector2.zero;
        txtRt.anchoredPosition = Vector2.zero;
    }

    // ── Tab switching ─────────────────────────────────────────────────────────

    private void SetTabVisuals(bool localActive)
    {
        if (_localTabBg   != null) _localTabBg.color   = localActive ? TabActiveColor   : TabInactiveColor;
        if (_globalTabBg  != null) _globalTabBg.color  = localActive ? TabInactiveColor : TabActiveColor;
        if (_localTabText != null) _localTabText.color  = localActive ? TabActiveText    : TabInactiveText;
        if (_globalTabText!= null) _globalTabText.color = localActive ? TabInactiveText  : TabActiveText;

        _localPanel?.SetActive(localActive);
        _globalPanel?.SetActive(!localActive);
    }

    private void SwitchToLocal()
    {
        _showingGlobal = false;
        SetTabVisuals(true);
        RefreshLocalScores();
    }

    private void SwitchToGlobal()
    {
        _showingGlobal = true;
        SetTabVisuals(false);
        FetchGlobalScores();
    }

    // ── Local scores ──────────────────────────────────────────────────────────

    private void RefreshLocalScores()
    {
        foreach (Transform child in _localPanel.transform)
            Destroy(child.gameObject);

        var scores = HighScoreManager.Instance?.GetTopScores() ?? new List<HighScoreManager.ScoreEntry>();

        for (int i = 0; i < Mathf.Min(10, scores.Count); i++)
        {
            float yPos = 188f - i * 52f;
            CreateScoreRow(_localPanel, i + 1, scores[i].playerName, scores[i].score, yPos);
        }

        if (scores.Count == 0)
            CreateEmptyLabel(_localPanel, "No scores yet — be the first!");
    }

    // ── Global scores ─────────────────────────────────────────────────────────

    private void FetchGlobalScores()
    {
        // Clear old rows, keep status text, show loading message
        foreach (Transform child in _globalPanel.transform)
            if (child.gameObject != _globalStatusText.gameObject)
                Destroy(child.gameObject);

        if (SteamLeaderboardManager.Instance == null)
        {
            _globalStatusText.text = "Leaderboard service not available.\nRun Purrbricks > Setup Scene.";
            _globalStatusText.gameObject.SetActive(true);
            return;
        }

        _globalStatusText.text = "Loading global scores...";
        _globalStatusText.gameObject.SetActive(true);

        SteamLeaderboardManager.Instance.FetchTopScores(GLOBAL_BOARD, 10, OnGlobalScoresFetched);
    }

    private void OnGlobalScoresFetched(List<LeaderboardEntryModel> entries)
    {
        // Discard if the tab was switched away before the callback arrived
        if (!_showingGlobal) return;

        foreach (Transform child in _globalPanel.transform)
            if (child.gameObject != _globalStatusText.gameObject)
                Destroy(child.gameObject);

        if (entries == null)
        {
            _globalStatusText.text = "Steam is not available.\nRun Steam to access global scores.";
            _globalStatusText.gameObject.SetActive(true);
            return;
        }

        if (entries.Count == 0)
        {
            _globalStatusText.text = "No global scores yet.\nBe the first to set a record!";
            _globalStatusText.gameObject.SetActive(true);
            return;
        }

        _globalStatusText.gameObject.SetActive(false);

        for (int i = 0; i < entries.Count; i++)
        {
            float yPos = 188f - i * 52f;
            CreateScoreRow(_globalPanel, entries[i].Rank, entries[i].DisplayName, entries[i].Score, yPos);
        }
    }

    // ── Shared row builder ────────────────────────────────────────────────────

    private void CreateScoreRow(GameObject parent, int rank, string playerName, int score, float yPos)
    {
        var rowGO  = new GameObject($"Row{rank}");
        rowGO.transform.SetParent(parent.transform, false);

        var rowImg = rowGO.AddComponent<Image>();
        rowImg.color = rank % 2 == 0
            ? new Color(1f, 1f, 1f, 0.03f)
            : new Color(0f, 0f, 0f, 0f);

        var rowRt = rowGO.GetComponent<RectTransform>();
        rowRt.anchorMin        = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax        = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta        = new Vector2(760f, 48f);
        rowRt.anchoredPosition = new Vector2(0f, yPos);

        Color rankColor = rank switch
        {
            1 => new Color(1.00f, 0.84f, 0.10f),
            2 => new Color(0.75f, 0.75f, 0.80f),
            3 => new Color(0.80f, 0.50f, 0.30f),
            _ => new Color(0.65f, 0.65f, 0.70f)
        };

        CreateEntryText(rowGO, $"{rank}",              new Vector2(-310f, 0f), 34, rankColor);
        CreateEntryText(rowGO, playerName,             new Vector2( -50f, 0f), 30, Color.white);
        CreateEntryText(rowGO, score.ToString("N0"),   new Vector2( 250f, 0f), 34, UIStyle.AccentGreen);
    }

    private void CreateEmptyLabel(GameObject parent, string message)
    {
        var go  = new GameObject("Empty");
        go.transform.SetParent(parent.transform, false);
        var txt = go.AddComponent<Text>();
        txt.text      = message;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 30;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        var rt        = txt.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(700f, 50f);
        rt.anchoredPosition = new Vector2(0f, 100f);
    }

    // ── Text helpers ──────────────────────────────────────────────────────────

    private void CreateHeaderText(GameObject parent, string text, Vector2 pos)
    {
        var go  = new GameObject("Header_" + text);
        go.transform.SetParent(parent.transform, false);
        var txt = go.AddComponent<Text>();
        txt.text      = text;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 28;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = new Color(0.55f, 0.75f, 1f, 0.70f);
        var rt        = txt.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(300f, 36f);
        rt.anchoredPosition = pos;
    }

    private void CreateEntryText(GameObject parent, string text, Vector2 pos, int fontSize, Color color)
    {
        var go  = new GameObject("ET");
        go.transform.SetParent(parent.transform, false);
        var txt = go.AddComponent<Text>();
        txt.text      = text;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = color;
        var rt        = txt.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(300f, fontSize + 18f);
        rt.anchoredPosition = pos;
        var ol              = go.AddComponent<Outline>();
        ol.effectColor      = Color.black;
        ol.effectDistance   = new Vector2(2f, -2f);
    }

    private void CreateImageButton(Transform parent, Sprite sprite, Vector2 anchoredPos, UnityAction onClick)
    {
        if (sprite == null) return;

        var go  = new GameObject("ImageButton");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite         = sprite;
        img.type           = Image.Type.Simple;
        img.preserveAspect = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var colors              = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor     = new Color(0.80f, 0.80f, 0.80f);
        btn.colors              = colors;

        float aspect        = (float)sprite.texture.width / sprite.texture.height;
        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(aspect * 90f, 90f);
        rt.anchoredPosition = anchoredPos;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show()
    {
        gameObject.SetActive(true);
        _showingGlobal = false;
        SetTabVisuals(true);
        RefreshLocalScores();
    }

    /// <summary>Show the screen with the Global tab pre-selected (used by GameManager.ShowSteamLeaderboard).</summary>
    public void ShowGlobalTab()
    {
        gameObject.SetActive(true);
        _showingGlobal = true;
        SetTabVisuals(false);
        FetchGlobalScores();
    }

    public void Hide()       { gameObject.SetActive(false); }
    public void ShowScores() { Show(); }
}
