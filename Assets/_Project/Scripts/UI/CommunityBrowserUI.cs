using System.Collections.Generic;
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
    private const int   COLS      = 3;
    private const int   ROWS      = 4;
    private const int   PAGE_SIZE = COLS * ROWS; // 12
    private const float CARD_W    = 560f;
    private const float CARD_H    = 210f;
    private const float CARD_GAP  = 24f;
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
            float tx = tabStartX + i * (tabW + 12f) - 960f; // relative to centre
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

        // Loading spinner label
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

        // Empty state label
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
                    ? new Color(0.25f, 0.18f, 0.03f, 1f) // highlighted (dark gold)
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
        // Available area: ~1840 × 930
        // 3 cols × 560 + 2 × 24 = 1728 → startX = (1840-1728)/2 = 56
        // 4 rows × 210 + 3 × 24 = 912  → startY = (930-912)/2 = 9
        float startX = 56f;
        float startY = -9f;

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

        float cx = 10f; // left margin for text

        // Title
        CardLabel(card.transform, meta.title ?? "Untitled",
            new Vector2(cx, -16f), 22, Color.white, TextAnchor.UpperLeft, CARD_W - 20f, 28f);

        // Author
        CardLabel(card.transform, $"by {meta.steamName ?? "Unknown"}",
            new Vector2(cx, -46f), 16, new Color(0.65f, 0.65f, 0.80f), TextAnchor.UpperLeft, CARD_W - 20f, 22f);

        // Stars + rating
        string ratingStr = meta.ratingCount > 0
            ? $"★ {meta.averageRating:F1}  ({meta.ratingCount} ratings)"
            : "No ratings yet";
        CardLabel(card.transform, ratingStr,
            new Vector2(cx, -74f), 16, new Color(1f, 0.84f, 0.10f), TextAnchor.UpperLeft, CARD_W - 20f, 22f);

        // Stats
        CardLabel(card.transform, $"{meta.playCount} plays  |  {meta.brickCount} bricks",
            new Vector2(cx, -98f), 14, new Color(0.55f, 0.55f, 0.65f), TextAnchor.UpperLeft, CARD_W - 20f, 20f);

        // ▶ Play button
        int capturedId = meta.id;
        var playBtn = UIStyle.CreateButton(card.transform, "▶ Play",
            new Vector2(-(CARD_W * 0.5f - 110f), -(CARD_H - 38f)),
            new Vector2(190f, 50f),
            () => OnPlayClicked(capturedId, card), UIStyle.AccentGreen);

        // ⚑ Report button (small, bottom-right)
        var reportBtn = UIStyle.CreateButton(card.transform, "⚑",
            new Vector2(CARD_W * 0.5f - 28f, -(CARD_H - 16f)),
            new Vector2(44f, 32f),
            () => OnReportClicked(capturedId), UIStyle.AccentRed);
        var reportTxt = reportBtn.GetComponentInChildren<Text>();
        if (reportTxt != null) reportTxt.fontSize = 14;
    }

    private void CardLabel(Transform parent, string text, Vector2 pos, int size, Color color,
                           TextAnchor anchor, float width, float height)
    {
        var go = new GameObject("Lbl_" + text.Substring(0, Mathf.Min(text.Length, 12)));
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text      = text;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = size;
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

    private void OnPlayClicked(int id, GameObject card)
    {
        if (CommunityLevelService.Instance == null) return;

        // Show loading text on card
        var btn = card.GetComponentInChildren<Button>();
        if (btn != null) btn.interactable = false;
        var labels = card.GetComponentsInChildren<Text>();
        foreach (var l in labels)
            if (l.text == "▶ Play") { l.text = "Loading..."; break; }

        CommunityLevelService.Instance.FetchLevel(id, (data, meta) =>
        {
            if (data == null || meta == null)
            {
                Debug.LogWarning($"[CommunityBrowserUI] Failed to load community level {id}");
                if (btn != null) btn.interactable = true;
                return;
            }

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
