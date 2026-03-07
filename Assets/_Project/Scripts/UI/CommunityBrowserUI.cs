using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Community Level Browser — browse, filter, and play user-published levels.
/// Accessible from the Main Menu and Victory screen.
/// sortingOrder=150 — same level as the built-in level browser.
/// </summary>
public class CommunityBrowserUI : MonoBehaviour
{
    public static CommunityBrowserUI Instance { get; private set; }

    // ── Layout constants ───────────────────────────────────────────────────────
    private const int   COLS      = 4;
    private const int   ROWS      = 4;
    private const int   PAGE_SIZE = COLS * ROWS; // 16
    private const float CARD_W    = 420f;
    private const float CARD_H    = 215f;
    private const float CARD_GAP  = 18f;
    private const float THUMB_H   = 110f;
    private const float TOP_H     = 100f;
    private const float BOT_H     = 62f;
    private const float H_MARGIN  = 40f;

    // ── Sort tabs ─────────────────────────────────────────────────────────────
    private static readonly string[] SortKeys   = { "rating", "newest", "oldest", "plays" };
    private static readonly string[] SortLabels = { "Highest Rated", "Newest", "Oldest", "Most Played" };

    // ── State ─────────────────────────────────────────────────────────────────
    private Canvas    _canvas;
    private Transform _root;
    private Transform _cardsRoot;
    private Text      _pageLabel;
    private Button    _prevBtn, _nextBtn;
    private Button[]  _sortBtns;
    private Text      _emptyText;
    private GameObject _loadingSpinner;

    private int    _currentPage   = 1;
    private int    _totalPages    = 1;
    private int    _sortIndex     = 0;
    private bool   _isFetching    = false;
    private System.Action _backAction;

    private float _prevTimeScale;
    private bool  _prevAudioPaused;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        gameObject.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show()
    {
        _prevTimeScale    = Time.timeScale;
        _prevAudioPaused  = AudioListener.pause;
        Time.timeScale    = 0f;
        AudioListener.pause = true;

        gameObject.SetActive(true);
        FetchPage(1);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        Time.timeScale    = _prevTimeScale;
        AudioListener.pause = _prevAudioPaused;
        _backAction?.Invoke();
        _backAction = null;
    }

    public void SetBackAction(System.Action onBack) => _backAction = onBack;

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        _canvas = GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 150;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        var rootGO = MakePanel(transform, "BrowserRoot", new Color(0.04f, 0.06f, 0.12f, 1f));
        Stretch(rootGO);
        _root = rootGO.transform;

        BuildHeader();
        BuildCardsArea();
        BuildFooter();
    }

    private void BuildHeader()
    {
        var header = MakePanel(_root, "Header", new Color(0.06f, 0.08f, 0.16f, 0.95f));
        var hRt    = header.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 1f); hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot     = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = Vector2.zero;
        hRt.sizeDelta = new Vector2(0f, TOP_H);

        // Title
        var titleGO  = new GameObject("Title");
        titleGO.transform.SetParent(header.transform, false);
        var titleTxt = titleGO.AddComponent<Text>();
        titleTxt.text      = "COMMUNITY LEVELS";
        titleTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize  = 48;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.color     = UIStyle.AccentGold;
        titleTxt.alignment = TextAnchor.MiddleLeft;
        var titleRt = titleTxt.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0f); titleRt.anchorMax = new Vector2(0.3f, 1f);
        titleRt.offsetMin = new Vector2(H_MARGIN + 10f, 0f); titleRt.offsetMax = Vector2.zero;
        titleGO.AddComponent<Outline>().effectColor = new Color(0.8f, 0.5f, 0f, 0.6f);

        // Sort tab buttons
        _sortBtns = new Button[SortKeys.Length];
        float tabW = 200f, tabH = 50f;
        float tabStartX = 400f;
        for (int i = 0; i < SortKeys.Length; i++)
        {
            int captured = i;
            float tx = tabStartX + i * (tabW + 12f) - 960f;
            var btn = UIStyle.CreateButton(header.transform, SortLabels[i],
                new Vector2(tx + tabW * 0.5f, 0f), new Vector2(tabW, tabH),
                () => OnSortTabClicked(captured), UIStyle.AccentBlue);
            _sortBtns[i] = btn;
        }

        UpdateSortTabVisuals();

        // Back button
        UIStyle.CreateButton(header.transform, "← Back",
            new Vector2(860f, 0f), new Vector2(160f, 50f),
            Hide, UIStyle.AccentRed);
    }

    private void BuildCardsArea()
    {
        var area = new GameObject("CardsArea");
        area.transform.SetParent(_root, false);
        var aRt = area.AddComponent<RectTransform>();
        aRt.anchorMin = Vector2.zero; aRt.anchorMax = Vector2.one;
        aRt.offsetMin = new Vector2(H_MARGIN, BOT_H);
        aRt.offsetMax = new Vector2(-H_MARGIN, -TOP_H);

        _cardsRoot = area.transform;

        // Loading spinner
        var spinGO = new GameObject("LoadingSpinner");
        spinGO.transform.SetParent(area.transform, false);
        var spinTxt = spinGO.AddComponent<Text>();
        spinTxt.text      = "Loading...";
        spinTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        spinTxt.fontSize  = 40;
        spinTxt.fontStyle = FontStyle.Bold;
        spinTxt.alignment = TextAnchor.MiddleCenter;
        spinTxt.color     = new Color(0.65f, 0.65f, 0.80f);
        var spinRt = spinTxt.GetComponent<RectTransform>();
        spinRt.anchorMin = Vector2.zero; spinRt.anchorMax = Vector2.one;
        spinRt.sizeDelta = Vector2.zero;
        _loadingSpinner = spinGO;
        spinGO.SetActive(false);

        // Empty state
        var emptyGO = new GameObject("EmptyText");
        emptyGO.transform.SetParent(area.transform, false);
        _emptyText = emptyGO.AddComponent<Text>();
        _emptyText.text      = "No community levels found.";
        _emptyText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _emptyText.fontSize  = 32;
        _emptyText.fontStyle = FontStyle.Bold;
        _emptyText.alignment = TextAnchor.MiddleCenter;
        _emptyText.color     = new Color(0.55f, 0.55f, 0.65f);
        var emptyRt = _emptyText.GetComponent<RectTransform>();
        emptyRt.anchorMin = Vector2.zero; emptyRt.anchorMax = Vector2.one;
        emptyRt.sizeDelta = Vector2.zero;
        _emptyText.gameObject.SetActive(false);
    }

    private void BuildFooter()
    {
        var pgGO = new GameObject("PageLabel");
        pgGO.transform.SetParent(_root, false);
        _pageLabel = pgGO.AddComponent<Text>();
        _pageLabel.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _pageLabel.fontSize  = 22;
        _pageLabel.color     = new Color(0.65f, 0.65f, 0.80f);
        _pageLabel.alignment = TextAnchor.MiddleCenter;
        var pgRt = _pageLabel.GetComponent<RectTransform>();
        pgRt.anchorMin = new Vector2(0f, 0f); pgRt.anchorMax = new Vector2(1f, 0f);
        pgRt.pivot     = new Vector2(0.5f, 0f);
        pgRt.anchoredPosition = new Vector2(0f, 8f);
        pgRt.sizeDelta = new Vector2(0f, 46f);

        _prevBtn = UIStyle.CreateButton(_root, "◄ Prev",
            new Vector2(-200f, -(540f - BOT_H * 0.5f)),
            new Vector2(160f, 44f),
            () => FetchPage(_currentPage - 1), UIStyle.AccentBlue);

        _nextBtn = UIStyle.CreateButton(_root, "Next ►",
            new Vector2(200f, -(540f - BOT_H * 0.5f)),
            new Vector2(160f, 44f),
            () => FetchPage(_currentPage + 1), UIStyle.AccentBlue);
    }

    // ── Data fetching ─────────────────────────────────────────────────────────

    private void FetchPage(int page)
    {
        if (_isFetching) return;
        if (CommunityLevelService.Instance == null) { ShowEmpty(); return; }

        page = Mathf.Clamp(page, 1, Mathf.Max(1, _totalPages));
        _currentPage = page;
        _isFetching  = true;

        ClearCards();
        _loadingSpinner?.SetActive(true);
        _emptyText?.gameObject.SetActive(false);

        CommunityLevelService.Instance.FetchLevels(
            SortKeys[_sortIndex], _currentPage, PAGE_SIZE, result =>
            {
                _isFetching = false;
                _loadingSpinner?.SetActive(false);

                if (result == null || result.levels == null || result.levels.Count == 0)
                {
                    ShowEmpty();
                    return;
                }

                _totalPages = Mathf.Max(1, Mathf.CeilToInt(result.total / (float)PAGE_SIZE));
                PopulateCards(result.levels);
                UpdatePagination();
            });
    }

    private void OnSortTabClicked(int idx)
    {
        if (_sortIndex == idx) return;
        _sortIndex = idx;
        UpdateSortTabVisuals();
        FetchPage(1);
    }

    private void UpdateSortTabVisuals()
    {
        if (_sortBtns == null) return;
        for (int i = 0; i < _sortBtns.Length; i++)
        {
            if (_sortBtns[i] == null) continue;
            var img = _sortBtns[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == _sortIndex)
                    ? new Color(0.25f, 0.18f, 0.03f, 1f)
                    : new Color(0.07f, 0.09f, 0.18f, 1f);
        }
    }

    // ── Card population ───────────────────────────────────────────────────────

    private void ClearCards()
    {
        for (int i = _cardsRoot.childCount - 1; i >= 0; i--)
        {
            var c = _cardsRoot.GetChild(i).gameObject;
            if (c != _loadingSpinner && c != (_emptyText?.gameObject))
                Destroy(c);
        }
    }

    private void PopulateCards(List<CommunityLevelMeta> levels)
    {
        // Available area: ~1840 × 918 (1920 - 2×40 wide, 1080 - 100 - 62 tall)
        // 4 cols × 420 + 3 × 18 = 1734  → startX = (1840-1734)/2 = 53
        // 4 rows × 215 + 3 × 18 = 914   → startY = (918-914)/2  = 2 → use -2 (top-aligned)
        float startX = 53f;
        float startY = -2f;

        for (int i = 0; i < levels.Count; i++)
        {
            int col = i % COLS;
            int row = i / COLS;
            float cx = startX + col * (CARD_W + CARD_GAP);
            float cy = startY - row * (CARD_H + CARD_GAP);
            CreateCard(levels[i], cx, cy);
        }

        _emptyText?.gameObject.SetActive(false);
    }

    private void CreateCard(CommunityLevelMeta meta, float x, float y)
    {
        var card = MakePanel(_cardsRoot, $"Card_{meta.id}", new Color(0.08f, 0.10f, 0.20f, 1f));
        var cRt  = card.GetComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(0f, 1f);
        cRt.pivot     = new Vector2(0f, 1f);
        cRt.anchoredPosition = new Vector2(x, y);
        cRt.sizeDelta = new Vector2(CARD_W, CARD_H);
        card.AddComponent<Outline>().effectColor = new Color(0.25f, 0.35f, 0.60f, 0.7f);

        // ── Thumbnail area (top THUMB_H pixels) ─────────────────────────────
        var thumbGO = MakePanel(card.transform, "Thumb", new Color(0.05f, 0.07f, 0.15f, 1f));
        var tRt = thumbGO.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot     = new Vector2(0f, 1f);
        tRt.anchoredPosition = Vector2.zero;
        tRt.sizeDelta = new Vector2(0f, THUMB_H);

        // Try to render thumbnail from jsonData
        if (!string.IsNullOrEmpty(meta.jsonData))
        {
            try
            {
                var levelData = JsonConvert.DeserializeObject<LevelData>(meta.jsonData);
                if (levelData != null)
                    LevelEditorBrowserUI.BuildThumbnail(thumbGO.transform, levelData, CARD_W, THUMB_H);
            }
            catch { /* skip bad JSON — thumbnail stays blank */ }
        }

        // ── Info area (below thumbnail) ──────────────────────────────────────
        float infoTop = -(THUMB_H + 4f);   // y from top of card
        float cx = 8f;                     // left margin

        // Title
        CardLabel(card.transform, meta.title ?? "Untitled",
            new Vector2(cx, infoTop), 16, Color.white, TextAnchor.UpperLeft,
            CARD_W - 16f, 20f, FontStyle.Bold);

        // Author
        CardLabel(card.transform, $"by {meta.steamName ?? "Unknown"}",
            new Vector2(cx, infoTop - 22f), 13, new Color(0.65f, 0.65f, 0.80f),
            TextAnchor.UpperLeft, CARD_W - 16f, 17f);

        // Stars
        string ratingStr = meta.ratingCount > 0
            ? $"★ {meta.averageRating:F1}  ({meta.ratingCount})"
            : "No ratings yet";
        CardLabel(card.transform, ratingStr,
            new Vector2(cx, infoTop - 41f), 13, new Color(1f, 0.84f, 0.10f),
            TextAnchor.UpperLeft, CARD_W - 16f, 17f);

        // Stats
        CardLabel(card.transform, $"{meta.playCount} plays  |  {meta.brickCount} bricks",
            new Vector2(cx, infoTop - 60f), 12, new Color(0.50f, 0.50f, 0.62f),
            TextAnchor.UpperLeft, CARD_W - 16f, 16f);

        // ── Report button (bottom-right, tiny) ──────────────────────────────
        // UIStyle buttons use center anchor (0.5,0.5) → position is relative to card center
        // Card center in local space (pivot top-left): (CARD_W/2, -CARD_H/2)
        // Want button center at ~(CARD_W-46, -(CARD_H-11)) from top-left
        // → offset from card center: (CARD_W/2 - 46, -(CARD_H/2 - 11))
        int capturedId = meta.id;
        var reportBtn = UIStyle.CreateButton(card.transform, "Report",
            new Vector2(CARD_W * 0.5f - 46f, -(CARD_H * 0.5f - 11f)),
            new Vector2(76f, 22f),
            () => OnReportClicked(capturedId), UIStyle.AccentRed);
        var reportTxt = reportBtn.GetComponentInChildren<Text>();
        if (reportTxt != null) reportTxt.fontSize = 11;

        // ── Whole-card click = play ──────────────────────────────────────────
        var btn = card.AddComponent<Button>();
        var btnImg = card.GetComponent<Image>();
        btn.targetGraphic = btnImg;
        var cols = btn.colors;
        cols.normalColor      = new Color(0.08f, 0.10f, 0.20f, 1f);
        cols.highlightedColor = new Color(0.14f, 0.18f, 0.32f, 1f);
        cols.pressedColor     = new Color(0.05f, 0.07f, 0.14f, 1f);
        cols.colorMultiplier  = 1f;
        btn.colors = cols;

        int capturedMetaId = meta.id;
        btn.onClick.AddListener(() => OnCardClicked(capturedMetaId, card, btn));
    }

    private void CardLabel(Transform parent, string text, Vector2 pos, int size, Color color,
                           TextAnchor anchor, float width, float height,
                           FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("Lbl_" + text.Substring(0, Mathf.Min(text.Length, 12)));
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text      = text;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = size;
        txt.fontStyle = style;
        txt.alignment = anchor;
        txt.color     = color;
        txt.raycastTarget = false;
        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, height);
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void OnCardClicked(int id, GameObject card, Button btn)
    {
        if (CommunityLevelService.Instance == null) return;

        // Disable card interaction while loading
        btn.interactable = false;
        var titleLabels = card.GetComponentsInChildren<Text>();
        foreach (var l in titleLabels)
        {
            if (l.gameObject.name.StartsWith("Lbl_") && l.fontStyle == FontStyle.Bold)
            {
                l.text = "Loading...";
                break;
            }
        }

        CommunityLevelService.Instance.FetchLevel(id, (data, meta) =>
        {
            if (data == null || meta == null)
            {
                Debug.LogWarning($"[CommunityBrowserUI] Failed to load community level {id}");
                btn.interactable = true;
                return;
            }

            // Clear back action BEFORE Hide() so it doesn't re-show the main menu
            _backAction = null;
            GameManager.Instance?.StartCommunityLevel(meta, data);
            Hide();
        });
    }

    private void OnReportClicked(int id)
    {
        CommunityLevelService.Instance?.ReportLevel(id, () =>
            Debug.Log($"[CommunityBrowserUI] Reported level {id}"));
    }

    // ── Pagination helpers ────────────────────────────────────────────────────

    private void UpdatePagination()
    {
        _pageLabel.text = $"Page {_currentPage} / {_totalPages}";
        _prevBtn.interactable = _currentPage > 1;
        _nextBtn.interactable = _currentPage < _totalPages;
    }

    private void ShowEmpty()
    {
        ClearCards();
        _emptyText?.gameObject.SetActive(true);
        _totalPages  = 1;
        _currentPage = 1;
        UpdatePagination();
    }

    // ── UGUI helpers ──────────────────────────────────────────────────────────

    private static GameObject MakePanel(Transform parent, string name, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
