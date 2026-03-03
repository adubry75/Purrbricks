using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Steam-only leaderboard screen.
///
/// Board selector: OVERALL + Level 01–80 (per-level).
/// Scope toggle: ALL TIME / WEEKLY / DAILY.
/// Results show all scores in a scroll view and auto-focus around the current user when possible.
/// </summary>
public class HighScoresUI : MonoBehaviour
{
    private Canvas _canvas;

    [Header("Button Sprites")]
    [SerializeField] private Sprite _mainMenuSprite;

    // Dynamic content
    private Text       _boardLabel;
    private Button     _prevBoardBtn;
    private Button     _nextBoardBtn;
    private Image      _allTimeTabBg;
    private Text       _allTimeTabText;
    private Image      _weeklyTabBg;
    private Text       _weeklyTabText;
    private Image      _dailyTabBg;
    private Text       _dailyTabText;
    private ScrollRect _scrollRect;
    private RectTransform _contentRt;
    private GameObject _rowContainer;
    private Text       _statusText;
    private Button     _mainMenuBtn;
    private Button     _backToGameBtn;
    private bool       _returnToVictory;

    // State
    private int  _boardIndex; // 0 = OVERALL, 1..N = level 00..(N-1)
    private LeaderboardTimeScope _scope;
    private bool _fetching;
    private int  _fetchToken;

    // Cached player data – filled from a successful AllTime fetch so Weekly/Daily
    // can generate test data using the same score baseline.
    private int      _cachedMyScore;
    private CSteamID _cachedMySteamId;
    private string   _cachedMyName = "You";

    // 0 = overall + one board per level_XX
    private const int TOTAL_BOARDS = 85;
    private const float ROW_HEIGHT = 44f;
    private const float ROW_SPACING = 4f;
    private const float ROW_PITCH = ROW_HEIGHT + ROW_SPACING;

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

        // ── Scope tabs ────────────────────────────────────────────────────────
        BuildModeTab(panel.transform, "ALL TIME", new Vector2(-240f, 272f), out _allTimeTabBg, out _allTimeTabText, OnAllTimeScope);
        BuildModeTab(panel.transform, "WEEKLY",   new Vector2(   0f, 272f), out _weeklyTabBg,  out _weeklyTabText,  OnWeeklyScope);
        BuildModeTab(panel.transform, "DAILY",    new Vector2( 240f, 272f), out _dailyTabBg,   out _dailyTabText,   OnDailyScope);

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

        // ── Scroll view ────────────────────────────────────────────────────────
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(panel.transform, false);
        var scrollRt = scrollGO.AddComponent<RectTransform>();
        scrollRt.anchorMin = scrollRt.anchorMax = new Vector2(0.5f, 0.5f);
        // Keep the scroll region below the header/divider (so rows appear under the column headers).
        scrollRt.sizeDelta        = new Vector2(800f, 560f);
        scrollRt.anchoredPosition = new Vector2(0f, -100f);

        var scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0f, 0f, 0f, 0.0f);
        scrollImg.raycastTarget = false;

        _scrollRect = scrollGO.AddComponent<ScrollRect>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 30f;

        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var viewportRt = viewportGO.AddComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.sizeDelta = Vector2.zero;
        viewportRt.anchoredPosition = Vector2.zero;
        var viewportImg = viewportGO.AddComponent<Image>();
        viewportImg.color = new Color(0f, 0f, 0f, 0.0f);
        viewportImg.raycastTarget = true;
        // RectMask2D avoids stencil/shader edge-cases some pipelines hit with Mask.
        viewportGO.AddComponent<RectMask2D>();

        _rowContainer = new GameObject("Content");
        _rowContainer.transform.SetParent(viewportGO.transform, false);
        _contentRt = _rowContainer.AddComponent<RectTransform>();
        _contentRt.anchorMin = new Vector2(0f, 1f);
        _contentRt.anchorMax = new Vector2(1f, 1f);
        _contentRt.pivot = new Vector2(0.5f, 1f);
        _contentRt.sizeDelta = new Vector2(0f, 0f);
        _contentRt.anchoredPosition = Vector2.zero;

        var vlg = _rowContainer.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = ROW_SPACING;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        var csf = _rowContainer.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scrollRect.viewport = viewportRt;
        _scrollRect.content = _contentRt;

        // Status text (loading / empty / error) (non-scrolling)
        var statusGO = new GameObject("Status");
        statusGO.transform.SetParent(scrollGO.transform, false);
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

        // ── Exit buttons ──────────────────────────────────────────────────────
        _mainMenuBtn = UIStyle.CreateButton(panel.transform, "Main Menu",
            new Vector2(0f, -455f), new Vector2(280f, 70f),
            () => GameManager.Instance?.ShowMainMenu(), UIStyle.AccentBlue);

        _backToGameBtn = UIStyle.CreateButton(panel.transform, "Back To Game",
            new Vector2(0f, -455f), new Vector2(280f, 70f),
            () => GameManager.Instance?.ReturnToVictoryFromLevelBoard(), UIStyle.AccentGreen);

        SetReturnToVictory(false);
    }

    private void SetReturnToVictory(bool on)
    {
        _returnToVictory = on;
        if (_mainMenuBtn != null) _mainMenuBtn.gameObject.SetActive(!on);
        if (_backToGameBtn != null) _backToGameBtn.gameObject.SetActive(on);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnPrevBoard()
    {
        _boardIndex = (_boardIndex - 1 + TOTAL_BOARDS) % TOTAL_BOARDS;
        ResetAndFetch();
    }

    private void OnNextBoard()
    {
        _boardIndex = (_boardIndex + 1) % TOTAL_BOARDS;
        ResetAndFetch();
    }

    private void OnAllTimeScope()
    {
        if (_scope == LeaderboardTimeScope.AllTime) return;
        _scope = LeaderboardTimeScope.AllTime;
        UpdateScopeTabVisuals();
        Fetch();
    }

    private void OnWeeklyScope()
    {
        if (_scope == LeaderboardTimeScope.Weekly) return;
        _scope = LeaderboardTimeScope.Weekly;
        UpdateScopeTabVisuals();
        Fetch();
    }

    private void OnDailyScope()
    {
        if (_scope == LeaderboardTimeScope.Daily) return;
        _scope = LeaderboardTimeScope.Daily;
        UpdateScopeTabVisuals();
        Fetch();
    }

    // ── Fetch logic ───────────────────────────────────────────────────────────

    private void ResetAndFetch()
    {
        UpdateBoardLabel();
        UpdateScopeTabVisuals();
        Fetch();
    }

    private void Fetch()
    {
        // Always start a new fetch — increment token so any in-flight callbacks from the previous
        // fetch will see a mismatch and return early without clobbering the new results.
        _fetchToken++;
        int token = _fetchToken;
        _fetching = true;
        SetStatus("Loading...");
        ClearRows();

        string board = BoardName();
        if (Debug.isDebugBuild)
            Debug.Log($"HighScoresUI: Fetch board='{board}' scope={_scope}");

        // In debug builds, Weekly/Daily boards are generated locally so we never block on a
        // slow FindOrCreateLeaderboard round-trip for date-encoded board names.
        if (LeaderboardTestData.Enabled && _scope != LeaderboardTimeScope.AllTime)
        {
            _fetching = false;
            GenerateTestData(board);
            return;
        }

        if (SteamLeaderboardManager.Instance == null)
        {
            _fetching = false;
            SetStatus("Leaderboard service unavailable.\nRun Purrbricks > Setup Scene.");
            return;
        }

        SteamLeaderboardManager.Instance.FetchAroundMe(board, 0,
            entries => OnFetchedAroundMe(token, board, entries));
        StartCoroutine(FetchTimeout(token));
    }

    private IEnumerator FetchTimeout(int token)
    {
        yield return new WaitForSecondsRealtime(8f);
        if (!_fetching) yield break;
        if (token != _fetchToken) yield break;

        _fetching = false;
        SetStatus("Timed out loading leaderboard.\n(See console for Steam callback logs.)");
    }

    private void OnFetchedAroundMe(int token, string boardName, List<LeaderboardEntryModel> entries)
    {
        if (token != _fetchToken) return; // stale callback — a newer fetch has already started
        _fetching = false;

        if (Debug.isDebugBuild)
            Debug.Log($"HighScoresUI: Fetched board='{boardName}' entries={(entries == null ? -1 : entries.Count)}");

        if (entries == null)
        {
            SetStatus("Steam is not available.\nRun Steam to view leaderboards.");
            return;
        }

        if (entries.Count == 0)
        {
            // User has no score on this board yet.
            // In debug builds generate synthetic data rather than showing "No scores yet".
            if (LeaderboardTestData.Enabled)
            {
                GenerateTestData(boardName);
                return;
            }
            // Production: fall back to the top of the board so at least something is visible.
            _fetching = true;
            SteamLeaderboardManager.Instance?.FetchRange(boardName, 1, 10,
                top => OnFetchedTop(token, boardName, top));
            return;
        }

        // FetchAroundMe(range=0) always returns the current user's entry as entries[0].
        var me = entries[0];

        // Cache the player's score from the AllTime board so Weekly/Daily simulation can
        // use the same baseline score even before those boards have a real Steam entry.
        if (_scope == LeaderboardTimeScope.AllTime)
        {
            _cachedMyScore    = me.Score;
            _cachedMySteamId  = me.SteamId;
            _cachedMyName     = me.DisplayName;
        }

        if (LeaderboardTestData.Enabled && SteamworksBootstrap.Instance?.IsSteamAvailable == true)
        {
            var simulated = LeaderboardTestData.BuildSimulatedBoard(boardName, me);
            if (simulated != null) entries = simulated;
        }

        SetStatus("");
        PopulateRows(entries, highlightMe: true);
    }

    private void OnFetchedTop(int token, string boardName, List<LeaderboardEntryModel> entries)
    {
        if (token != _fetchToken) return; // stale callback
        _fetching = false;

        if (Debug.isDebugBuild)
            Debug.Log($"HighScoresUI: FetchedTop board='{boardName}' entries={(entries == null ? -1 : entries.Count)}");

        if (entries == null)
        {
            SetStatus("Steam is not available.\nRun Steam to view leaderboards.");
            return;
        }

        if (entries.Count == 0)
        {
            SetStatus("No scores yet — be the first!");
            return;
        }

        SetStatus("");
        PopulateRows(entries, highlightMe: true);
    }

    // ── Row rendering ─────────────────────────────────────────────────────────

    private void PopulateRows(List<LeaderboardEntryModel> entries, bool highlightMe)
    {
        CSteamID mySteamId = highlightMe && SteamworksBootstrap.Instance?.IsSteamAvailable == true
            ? SteamUser.GetSteamID()
            : CSteamID.Nil;

        int myIndex = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            var e    = entries[i];
            bool isMe = e.SteamId == mySteamId && mySteamId != CSteamID.Nil;
            if (isMe) myIndex = i;

            CreateRow(e.Rank, e.DisplayName, e.Score, isMe);
        }

        ScrollToIndex(myIndex >= 0 ? myIndex : 0);
    }

    private void CreateRow(int rank, string playerName, int score, bool isMe)
    {
        var rowGO = new GameObject($"Row{rank}");
        rowGO.transform.SetParent(_rowContainer.transform, false);

        var rowImg = rowGO.AddComponent<Image>();
        rowImg.raycastTarget = false;
        rowImg.color = isMe
            ? new Color(0.60f, 0.50f, 0.05f, 0.25f)   // gold tint for current user
            : (rank % 2 == 0 ? ColorRowAlt : Color.clear);

        var rowRt = rowGO.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 1f);
        rowRt.anchorMax = new Vector2(0.5f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.sizeDelta = new Vector2(760f, ROW_HEIGHT);

        var le = rowGO.AddComponent<LayoutElement>();
        le.preferredHeight = ROW_HEIGHT;

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
            Destroy(child.gameObject);
    }

    // ── Test-data generator (debug builds only) ───────────────────────────────

    /// <summary>
    /// Builds a simulated leaderboard populated with fake competitors.
    /// Used in debug builds when the Steam board is empty or for Weekly/Daily scopes
    /// where the real Steam entry may not exist yet (board newly created, upload race).
    /// Falls back to the cached AllTime score so the board is never blank in testing.
    /// </summary>
    private void GenerateTestData(string boardName)
    {
        int      score   = _cachedMyScore > 0 ? _cachedMyScore : 80_000;
        CSteamID steamId = _cachedMySteamId;
        string   name    = _cachedMyName;

        if (SteamworksBootstrap.Instance?.IsSteamAvailable == true)
        {
            steamId = SteamUser.GetSteamID();
            string steamName = SteamFriends.GetPersonaName();
            if (!string.IsNullOrEmpty(steamName)) name = steamName;
        }

        var fakeMe    = new LeaderboardEntryModel(0, score, steamId, name);
        var simulated = LeaderboardTestData.BuildSimulatedBoard(boardName, fakeMe);
        if (simulated != null)
        {
            SetStatus("");
            PopulateRows(simulated, highlightMe: true);
        }
        else
        {
            SetStatus("No scores yet — be the first!");
        }
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

    private void UpdateScopeTabVisuals()
    {
        bool allTime = _scope == LeaderboardTimeScope.AllTime;
        bool weekly  = _scope == LeaderboardTimeScope.Weekly;
        bool daily   = _scope == LeaderboardTimeScope.Daily;

        if (_allTimeTabBg != null) _allTimeTabBg.color = allTime ? TabActiveColor : TabInactiveColor;
        if (_weeklyTabBg  != null) _weeklyTabBg.color  = weekly  ? TabActiveColor : TabInactiveColor;
        if (_dailyTabBg   != null) _dailyTabBg.color   = daily   ? TabActiveColor : TabInactiveColor;

        if (_allTimeTabText != null) _allTimeTabText.color = allTime ? TabActiveText : TabInactiveText;
        if (_weeklyTabText  != null) _weeklyTabText.color  = weekly  ? TabActiveText : TabInactiveText;
        if (_dailyTabText   != null) _dailyTabText.color   = daily   ? TabActiveText : TabInactiveText;
    }

    private void ScrollToIndex(int index)
    {
        if (_scrollRect == null || _scrollRect.viewport == null || _scrollRect.content == null) return;

        Canvas.ForceUpdateCanvases();

        var viewportHeight = ((RectTransform)_scrollRect.viewport).rect.height;
        var contentHeight  = ((RectTransform)_scrollRect.content).rect.height;
        if (contentHeight <= viewportHeight + 0.01f)
        {
            _scrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        float rowCenterFromTop = index * ROW_PITCH + (ROW_HEIGHT * 0.5f);
        float desiredTopScroll = rowCenterFromTop - (viewportHeight * 0.5f);
        float maxScroll        = contentHeight - viewportHeight;
        float t                = Mathf.Clamp01(desiredTopScroll / maxScroll);
        _scrollRect.verticalNormalizedPosition = 1f - t;
    }

    // ── Board naming ──────────────────────────────────────────────────────────

    private string BoardName()
    {
        string allTime = _boardIndex == 0
            ? PurrbricksLeaderboards.OverallAllTime
            : PurrbricksLeaderboards.LevelAllTime(_boardIndex - 1);

        return PurrbricksLeaderboards.Scoped(allTime, _scope);
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

    /// <summary>Opens on the OVERALL board.</summary>
    public void Show()
    {
        SetReturnToVictory(false);
        gameObject.SetActive(true);
        _boardIndex = 0;
        _scope = LeaderboardTimeScope.AllTime;
        ResetAndFetch();
    }

    /// <summary>Alias kept for GameManager compatibility.</summary>
    public void ShowGlobalTab() => Show();

    /// <summary>Opens directly on a specific level's board.</summary>
    public void ShowForLevel(int levelIndex, bool returnToVictory = false)
    {
        SetReturnToVictory(returnToVictory);
        gameObject.SetActive(true);
        _boardIndex = Mathf.Clamp(levelIndex + 1, 0, TOTAL_BOARDS - 1);
        _scope = LeaderboardTimeScope.AllTime;
        ResetAndFetch();
    }

    public void Hide() { gameObject.SetActive(false); }

    // Legacy shim so any old ShowScores() calls still compile
    public void ShowScores() => Show();
}
