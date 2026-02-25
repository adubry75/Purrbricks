using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Steam-only leaderboard screen.
///
/// Board selector: OVERALL (global all-time) + Level 01–80 (per-level).
/// Mode toggle: NEARBY (around current user, default) / TOP (top of board, paginated).
///
/// NEARBY automatically falls back to TOP when the user has no score on a board.
/// </summary>
public class HighScoresUI : MonoBehaviour
{
    private enum Mode { Nearby, Top }

    private Canvas _canvas;

    // Dynamic content
    private Text       _boardLabel;
    private Button     _prevBoardBtn;
    private Button     _nextBoardBtn;
    private Image      _nearbyTabBg;
    private Text       _nearbyTabText;
    private Image      _topTabBg;
    private Text       _topTabText;
    private GameObject _rowContainer;
    private Text       _statusText;
    private Button     _prevPageBtn;
    private Button     _nextPageBtn;
    private Text       _pageLabel;

    // State
    private int  _boardIndex; // 0 = OVERALL, 1..N = level 00..(N-1)
    private Mode _mode;
    private int  _page;       // 1-based, TOP mode only
    private bool _fetching;

    // 0 = overall + one board per level_XX
    private const int TOTAL_BOARDS = 85;
    private const int PAGE_SIZE    = 10;
    private const int AROUND_RANGE = 5;    // 5 above + you + 5 below = 11 entries

    private static readonly Color TabActiveColor   = new Color(0.10f, 0.38f, 0.85f, 0.95f);
    private static readonly Color TabInactiveColor = new Color(0.07f, 0.10f, 0.20f, 0.80f);
    private static readonly Color TabActiveText    = Color.white;
    private static readonly Color TabInactiveText  = new Color(0.50f, 0.62f, 0.72f, 0.85f);
    private static readonly Color ColorGold        = new Color(1.00f, 0.84f, 0.10f);
    private static readonly Color ColorSilver      = new Color(0.75f, 0.75f, 0.80f);
    private static readonly Color ColorBronze      = new Color(0.80f, 0.50f, 0.30f);
    private static readonly Color ColorYou         = new Color(1.00f, 0.92f, 0.30f);
    private static readonly Color ColorRowAlt      = new Color(1f, 1f, 1f, 0.04f);

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
        var titleGO = MakeText(panel.transform, "LEADERBOARDS", new Vector2(0f, 418f), 80, ColorGold, true);
        titleGO.AddComponent<Outline>().effectColor = Color.black;

        // ── Board selector ─────────────────────────────────────────────────────
        _prevBoardBtn = MakeArrowButton(panel.transform, "◄", new Vector2(-370f, 340f), OnPrevBoard);
        _nextBoardBtn = MakeArrowButton(panel.transform, "►", new Vector2( 370f, 340f), OnNextBoard);

        var boardLabelGO = MakeText(panel.transform, "OVERALL", new Vector2(0f, 340f), 34, Color.white, false);
        _boardLabel = boardLabelGO.GetComponent<Text>();

        // ── Mode tabs ─────────────────────────────────────────────────────────
        BuildModeTab(panel.transform, "NEARBY", new Vector2(-130f, 272f), out _nearbyTabBg, out _nearbyTabText, OnNearbyMode);
        BuildModeTab(panel.transform, "TOP",    new Vector2( 130f, 272f), out _topTabBg,   out _topTabText,    OnTopMode);

        // ── Column headers ─────────────────────────────────────────────────────
        MakeText(panel.transform, "#",      new Vector2(-310f, 210f), 26, new Color(0.55f, 0.75f, 1f, 0.70f), true);
        MakeText(panel.transform, "PLAYER", new Vector2( -50f, 210f), 26, new Color(0.55f, 0.75f, 1f, 0.70f), true);
        MakeText(panel.transform, "SCORE",  new Vector2( 250f, 210f), 26, new Color(0.55f, 0.75f, 1f, 0.70f), true);

        // Divider
        var divGO  = new GameObject("Divider");
        divGO.transform.SetParent(panel.transform, false);
        var divImg = divGO.AddComponent<Image>();
        divImg.color = new Color(0.35f, 0.70f, 1f, 0.45f);
        var divRt  = divGO.GetComponent<RectTransform>();
        divRt.anchorMin = divRt.anchorMax = new Vector2(0.5f, 0.5f);
        divRt.sizeDelta        = new Vector2(760f, 2f);
        divRt.anchoredPosition = new Vector2(0f, 192f);

        // ── Row container ──────────────────────────────────────────────────────
        _rowContainer = new GameObject("Rows");
        _rowContainer.transform.SetParent(panel.transform, false);
        var rcRt = _rowContainer.AddComponent<RectTransform>();
        rcRt.anchorMin = rcRt.anchorMax = new Vector2(0.5f, 0.5f);
        rcRt.sizeDelta        = new Vector2(800f, 700f);
        rcRt.anchoredPosition = Vector2.zero;

        // Status text (loading / empty / error)
        var statusGO = new GameObject("Status");
        statusGO.transform.SetParent(_rowContainer.transform, false);
        _statusText                = statusGO.AddComponent<Text>();
        _statusText.font           = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _statusText.fontSize       = 30;
        _statusText.alignment      = TextAnchor.MiddleCenter;
        _statusText.color          = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        _statusText.raycastTarget  = false;
        var statusRt = statusGO.GetComponent<RectTransform>();
        statusRt.anchorMin = statusRt.anchorMax = new Vector2(0.5f, 0.5f);
        statusRt.sizeDelta        = new Vector2(700f, 80f);
        statusRt.anchoredPosition = new Vector2(0f, 100f);

        // ── Pagination (TOP mode only) ─────────────────────────────────────────
        _prevPageBtn = MakeArrowButton(panel.transform, "◄ Prev", new Vector2(-130f, -388f), OnPrevPage);
        _nextPageBtn = MakeArrowButton(panel.transform, "Next ►", new Vector2( 130f, -388f), OnNextPage);

        var pageGO = new GameObject("PageLabel");
        pageGO.transform.SetParent(panel.transform, false);
        _pageLabel                = pageGO.AddComponent<Text>();
        _pageLabel.font           = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _pageLabel.fontSize       = 24;
        _pageLabel.alignment      = TextAnchor.MiddleCenter;
        _pageLabel.color          = new Color(0.6f, 0.7f, 0.9f, 0.8f);
        _pageLabel.raycastTarget  = false;
        var pageRt = pageGO.GetComponent<RectTransform>();
        pageRt.anchorMin = pageRt.anchorMax = new Vector2(0.5f, 0.5f);
        pageRt.sizeDelta        = new Vector2(200f, 40f);
        pageRt.anchoredPosition = new Vector2(0f, -388f);

        // ── Main Menu button ───────────────────────────────────────────────────
        UIStyle.CreateButton(panel.transform, "Main Menu",
            new Vector2(0f, -455f), new Vector2(280f, 70f),
            () => GameManager.Instance?.ShowMainMenu(), UIStyle.AccentBlue);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnPrevBoard()
    {
        _boardIndex = (_boardIndex - 1 + TOTAL_BOARDS) % TOTAL_BOARDS;
        ResetToNearby();
    }

    private void OnNextBoard()
    {
        _boardIndex = (_boardIndex + 1) % TOTAL_BOARDS;
        ResetToNearby();
    }

    private void OnNearbyMode()
    {
        if (_mode == Mode.Nearby) return;
        _mode = Mode.Nearby;
        UpdateModeTabVisuals();
        Fetch();
    }

    private void OnTopMode()
    {
        if (_mode == Mode.Top) return;
        _mode = Mode.Top;
        _page = 1;
        UpdateModeTabVisuals();
        Fetch();
    }

    private void OnPrevPage()
    {
        if (_page <= 1 || _fetching) return;
        _page--;
        Fetch();
    }

    private void OnNextPage()
    {
        if (_fetching) return;
        _page++;
        Fetch();
    }

    // ── Fetch logic ───────────────────────────────────────────────────────────

    private void ResetToNearby()
    {
        _mode = Mode.Nearby;
        _page = 1;
        UpdateBoardLabel();
        UpdateModeTabVisuals();
        Fetch();
    }

    private void Fetch()
    {
        if (_fetching) return;
        _fetching = true;
        SetStatus("Loading...");
        ClearRows();

        string board = BoardName();

        if (_mode == Mode.Nearby)
            SteamLeaderboardManager.Instance?.FetchAroundMe(board, AROUND_RANGE, OnFetchedNearby);
        else
            SteamLeaderboardManager.Instance?.FetchRange(board,
                (_page - 1) * PAGE_SIZE + 1, _page * PAGE_SIZE, OnFetchedTop);

        if (SteamLeaderboardManager.Instance == null)
        {
            _fetching = false;
            SetStatus("Leaderboard service unavailable.\nRun Purrbricks > Setup Scene.");
        }
    }

    private void OnFetchedNearby(List<LeaderboardEntryModel> entries)
    {
        _fetching = false;

        if (entries == null)
        {
            SetStatus("Steam is not available.\nRun Steam to view leaderboards.");
            return;
        }

        if (entries.Count == 0)
        {
            // No score on this board — fall back to TOP silently
            _mode = Mode.Top;
            _page = 1;
            UpdateModeTabVisuals();
            Fetch();
            return;
        }

        SetStatus("");
        PopulateRows(entries, highlightMe: true);
        UpdatePagination(visible: false, hasMore: false);
    }

    private void OnFetchedTop(List<LeaderboardEntryModel> entries)
    {
        _fetching = false;

        if (entries == null)
        {
            SetStatus("Steam is not available.\nRun Steam to view leaderboards.");
            return;
        }

        if (entries.Count == 0)
        {
            if (_page == 1)
                SetStatus("No scores yet — be the first!");
            else
            {
                // Went past the end — back up
                _page = Mathf.Max(1, _page - 1);
                Fetch();
            }
            return;
        }

        SetStatus("");
        PopulateRows(entries, highlightMe: false);
        UpdatePagination(visible: true, hasMore: entries.Count >= PAGE_SIZE);
    }

    // ── Row rendering ─────────────────────────────────────────────────────────

    private void PopulateRows(List<LeaderboardEntryModel> entries, bool highlightMe)
    {
        CSteamID mySteamId = highlightMe && SteamworksBootstrap.Instance?.IsSteamAvailable == true
            ? SteamUser.GetSteamID()
            : CSteamID.Nil;

        for (int i = 0; i < entries.Count; i++)
        {
            var e    = entries[i];
            bool isMe = e.SteamId == mySteamId && mySteamId != CSteamID.Nil;

            float yPos = 160f - i * 48f;
            CreateRow(e.Rank, e.DisplayName, e.Score, yPos, isMe);
        }
    }

    private void CreateRow(int rank, string playerName, int score, float yPos, bool isMe)
    {
        var rowGO = new GameObject($"Row{rank}");
        rowGO.transform.SetParent(_rowContainer.transform, false);

        var rowImg = rowGO.AddComponent<Image>();
        rowImg.color = isMe
            ? new Color(0.60f, 0.50f, 0.05f, 0.25f)   // gold tint for current user
            : (rank % 2 == 0 ? ColorRowAlt : Color.clear);

        var rowRt = rowGO.GetComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta        = new Vector2(760f, 44f);
        rowRt.anchoredPosition = new Vector2(0f, yPos);

        Color rankColor = rank switch
        {
            1 => ColorGold,
            2 => ColorSilver,
            3 => ColorBronze,
            _ => isMe ? ColorYou : new Color(0.65f, 0.65f, 0.70f)
        };

        string nameDisplay = isMe ? $"▶ {playerName} ◀" : playerName;

        CreateCell(rowGO, rank.ToString(),        new Vector2(-310f, 0f), 30, rankColor);
        CreateCell(rowGO, nameDisplay,            new Vector2( -50f, 0f), 26, isMe ? ColorYou : Color.white);
        CreateCell(rowGO, score.ToString("N0"),   new Vector2( 250f, 0f), 30, UIStyle.AccentGreen);
    }

    private void CreateCell(GameObject parent, string text, Vector2 pos, int fontSize, Color color)
    {
        var go  = new GameObject("Cell");
        go.transform.SetParent(parent.transform, false);
        var txt = go.AddComponent<Text>();
        txt.text          = text;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = fontSize;
        txt.fontStyle     = FontStyle.Bold;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.color         = color;
        txt.raycastTarget = false;   // display-only
        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(300f, fontSize + 14f);
        rt.anchoredPosition = pos;
        var ol = go.AddComponent<Outline>();
        ol.effectColor    = Color.black;
        ol.effectDistance = new Vector2(2f, -2f);
    }

    private void ClearRows()
    {
        foreach (Transform child in _rowContainer.transform)
            if (child.gameObject != _statusText.gameObject)
                Destroy(child.gameObject);
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void SetStatus(string msg)
    {
        if (_statusText == null) return;
        _statusText.text = msg;
        _statusText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }

    private void UpdateBoardLabel()
    {
        if (_boardLabel == null) return;
        _boardLabel.text = GetBoardLabel();
    }

    private void UpdateModeTabVisuals()
    {
        bool nearby = _mode == Mode.Nearby;
        if (_nearbyTabBg  != null) _nearbyTabBg.color  = nearby ? TabActiveColor   : TabInactiveColor;
        if (_topTabBg     != null) _topTabBg.color     = nearby ? TabInactiveColor : TabActiveColor;
        if (_nearbyTabText != null) _nearbyTabText.color = nearby ? TabActiveText   : TabInactiveText;
        if (_topTabText   != null) _topTabText.color   = nearby ? TabInactiveText  : TabActiveText;
    }

    private void UpdatePagination(bool visible, bool hasMore)
    {
        if (_prevPageBtn != null) { _prevPageBtn.gameObject.SetActive(visible); _prevPageBtn.interactable = _page > 1; }
        if (_nextPageBtn != null) { _nextPageBtn.gameObject.SetActive(visible); _nextPageBtn.interactable = hasMore; }
        if (_pageLabel   != null)
        {
            _pageLabel.gameObject.SetActive(visible);
            _pageLabel.text = visible ? $"Page {_page}" : "";
        }
    }

    // ── Board naming ──────────────────────────────────────────────────────────

    private string BoardName()
    {
        if (_boardIndex == 0) return "Purrbricks_HighScores";
        return $"Purrbricks_level_{_boardIndex - 1:D2}";
    }

    private string GetBoardLabel()
    {
        if (_boardIndex == 0) return "OVERALL";
        int levelIndex = _boardIndex - 1;
        string title   = LoadLevelTitle(levelIndex);
        return $"Level {levelIndex + 1:D2}: {title}  ({levelIndex + 1}/80)";
    }

    private static string LoadLevelTitle(int levelIndex)
    {
        var ta = Resources.Load<TextAsset>($"Levels/level_{levelIndex:D2}");
        if (ta == null) return $"Level {levelIndex + 1:D2}";
        try
        {
            var obj = JObject.Parse(ta.text);
            var token = obj["displayName"] ?? obj["title"]; // legacy fallback
            return token?.ToString() ?? $"Level {levelIndex + 1:D2}";
        }
        catch { return $"Level {levelIndex + 1:D2}"; }
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private GameObject MakeText(Transform parent, string text, Vector2 pos, int fontSize, Color color, bool bold)
    {
        var go  = new GameObject(text);
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text           = text;
        txt.font           = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize       = fontSize;
        txt.fontStyle      = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.alignment      = TextAnchor.MiddleCenter;
        txt.color          = color;
        txt.raycastTarget  = false;   // display-only; never intercept clicks
        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(900f, fontSize + 20f);
        rt.anchoredPosition = pos;
        return go;
    }

    private Button MakeArrowButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go  = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);
        var bg  = go.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.10f, 0.20f, 0.80f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(onClick);
        var cols = btn.colors;
        cols.highlightedColor = new Color(0.25f, 0.45f, 0.85f, 0.95f);
        cols.pressedColor     = new Color(0.15f, 0.30f, 0.65f, 0.95f);
        btn.colors = cols;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(140f, 44f);
        rt.anchoredPosition = pos;

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.text          = label;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = 26;
        txt.fontStyle     = FontStyle.Bold;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.color         = Color.white;
        txt.raycastTarget = false;
        var txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = txtRt.anchoredPosition = Vector2.zero;

        return btn;
    }

    private void BuildModeTab(Transform parent, string label, Vector2 pos,
        out Image bgOut, out Text txtOut, UnityEngine.Events.UnityAction onClick)
    {
        var go  = new GameObject("Tab_" + label);
        go.transform.SetParent(parent, false);
        var bg  = go.AddComponent<Image>();
        bg.color = TabInactiveColor;
        bgOut    = bg;

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
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(220f, 50f);
        rt.anchoredPosition = pos;

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var txt           = txtGO.AddComponent<Text>();
        txt.text          = label;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = 26;
        txt.fontStyle     = FontStyle.Bold;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.color         = TabInactiveText;
        txt.raycastTarget = false;
        txtOut            = txt;
        var txtRt         = txt.GetComponent<RectTransform>();
        txtRt.anchorMin   = Vector2.zero;
        txtRt.anchorMax   = Vector2.one;
        txtRt.sizeDelta   = txtRt.anchoredPosition = Vector2.zero;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Opens on the OVERALL board in NEARBY mode.</summary>
    public void Show()
    {
        gameObject.SetActive(true);
        _boardIndex = 0;
        ResetToNearby();
    }

    /// <summary>Alias kept for GameManager compatibility.</summary>
    public void ShowGlobalTab() => Show();

    /// <summary>Opens directly on a specific level's board in NEARBY mode.</summary>
    public void ShowForLevel(int levelIndex)
    {
        gameObject.SetActive(true);
        _boardIndex = Mathf.Clamp(levelIndex + 1, 0, TOTAL_BOARDS - 1);
        ResetToNearby();
    }

    public void Hide() { gameObject.SetActive(false); }

    // Legacy shim so any old ShowScores() calls still compile
    public void ShowScores() => Show();
}
