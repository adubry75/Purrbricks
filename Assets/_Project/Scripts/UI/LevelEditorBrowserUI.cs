using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level Editor browser — paginated grid of level thumbnail cards.
/// Press F1 from the main menu (editor builds only) to open.
/// </summary>
public class LevelEditorBrowserUI : MonoBehaviour
{
    // ── Template thumbnail colours ─────────────────────────────────────────────
    internal static readonly Dictionary<string, Color> TemplateColors =
        new Dictionary<string, Color>
        {
            { "standard", new Color(0.88f, 0.88f, 0.88f) },
            { "red",      new Color(1.00f, 0.30f, 0.30f) },
            { "blue",     new Color(0.30f, 0.50f, 1.00f) },
            { "steel",    new Color(0.55f, 0.55f, 0.65f) },
            { "gem",      new Color(1.00f, 0.30f, 0.90f) },
            { "gold",     new Color(1.00f, 0.75f, 0.20f) },
            { "purple",   new Color(0.70f, 0.20f, 1.00f) },
            { "green",    new Color(0.20f, 0.90f, 0.35f) },
            { "cyan",     new Color(0.20f, 0.90f, 1.00f) },
            { "dark",     new Color(0.40f, 0.10f, 0.60f) },
            { "ghost",    new Color(0.85f, 0.85f, 0.85f, 0.6f) },
            { "bumper",   new Color(1.00f, 0.60f, 0.10f) },
        };

    // ── Layout constants (reference 1920×1080) ────────────────────────────────
    private const int   PAGE_COLS  = 4;
    private const int   PAGE_ROWS  = 4;
    private const int   PAGE_SIZE  = PAGE_COLS * PAGE_ROWS; // 16
    private const float CARD_W     = 420f;
    private const float CARD_H     = 215f;
    private const float CARD_GAP   = 18f;
    private const float THUMB_H    = 162f;
    private const float H_MARGIN   = 40f;
    private const float TOP_BAR_H  = 88f;
    private const float BOT_BAR_H  = 62f;

    // ── State ─────────────────────────────────────────────────────────────────
    private Canvas    _canvas;
    private Transform _rootPanel;
    private Transform _cardsRoot;   // child panel that holds the card GOs for current page
    private Text      _pageLabel;
    private Button    _prevBtn, _nextBtn;

    private LevelEditorUI  _editorUI;
    private System.Action  _onBack;

    // Runtime state to restore when closing the browser (varies based on caller).
    private float _prevTimeScale = 1f;
    private bool  _prevAudioPaused;
    private bool  _hasSavedRuntimeState;

    private readonly List<(string id, LevelData data)> _levels =
        new List<(string, LevelData)>();
    private int _page;

    // ── Level-select mode ─────────────────────────────────────────────────────
    private bool          _isLevelSelectMode;
    private System.Action<int> _onLevelSelected;
    private Text   _headerTitle;
    private Button _newLevelBtn;

    // ── Modal ─────────────────────────────────────────────────────────────────
    private GameObject _modal;
    private InputField _modalId, _modalName, _modalCols, _modalRows;

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        SetupCanvas();
        BuildUI();

        // Auto-wire: PurrbricksSetup calls SetEditorUI at edit-time, but since
        // _editorUI is not [SerializeField] it isn't persisted. Find it at runtime.
        if (_editorUI == null)
            _editorUI = Object.FindFirstObjectByType<LevelEditorUI>(FindObjectsInactive.Include);

        gameObject.SetActive(false);
    }

    public void SetEditorUI(LevelEditorUI editorUI) => _editorUI = editorUI;
    public void SetBackAction(System.Action onBack) => _onBack = onBack;

    /// <summary>Show the browser as a Level Select screen (in-game, with lock icons).</summary>
    public void ShowAsLevelSelect(System.Action<int> onLevelSelected)
    {
        _isLevelSelectMode = true;
        _onLevelSelected   = onLevelSelected;
        if (_headerTitle  != null) _headerTitle.text = "LEVEL SELECT";
        if (_newLevelBtn  != null) _newLevelBtn.gameObject.SetActive(false);
        Show();
    }

    /// <summary>Show the browser and freeze the game behind it.</summary>
    public void Show()
    {
        // Save runtime state so Back can restore correctly (Victory/Pause both run at timeScale=0).
        _prevTimeScale       = Time.timeScale;
        _prevAudioPaused     = AudioListener.pause;
        _hasSavedRuntimeState = true;

        // Freeze the demo running in the background
        Time.timeScale = 0f;
        AudioListener.pause = true;

        gameObject.SetActive(true);
        LoadLevels();
        ShowPage(0);
    }

    /// <summary>
    /// Refresh levels and return to page 0 without touching Time.timeScale.
    /// Called when returning from LevelEditorUI (already frozen).
    /// </summary>
    public void LoadLevelsAndPage()
    {
        LoadLevels();
        ShowPage(0);
    }

    public void Hide() => gameObject.SetActive(false);

    private void ResetToEditorMode()
    {
        _isLevelSelectMode = false;
        _onLevelSelected   = null;
        if (_headerTitle != null) _headerTitle.text = "LEVEL EDITOR";
        if (_newLevelBtn != null) _newLevelBtn.gameObject.SetActive(true);
    }

    // ── Canvas setup ──────────────────────────────────────────────────────────
    private void SetupCanvas()
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
    }

    // ── UI construction ───────────────────────────────────────────────────────
    private void BuildUI()
    {
        // Full-screen opaque backdrop
        var root = MakePanel(transform, "BrowserRoot", new Color(0.04f, 0.06f, 0.12f, 1f));
        Stretch(root);
        _rootPanel = root.transform;

        BuildHeader();
        BuildCardsArea();
        BuildFooter();
        BuildNewLevelModal();
    }

    private void BuildHeader()
    {
        // Dark strip at the top
        var header = MakePanel(_rootPanel, "Header", new Color(0.06f, 0.08f, 0.16f, 0.95f));
        var hRt = header.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot     = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = Vector2.zero;
        hRt.sizeDelta = new Vector2(0f, TOP_BAR_H);

        // Title label
        var titleGO  = new GameObject("Title");
        titleGO.transform.SetParent(header.transform, false);
        var titleTxt = titleGO.AddComponent<Text>();
        titleTxt.text      = "LEVEL EDITOR";
        titleTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize  = 48;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.color     = UIStyle.AccentGold;
        titleTxt.alignment = TextAnchor.MiddleLeft;
        var titleRt = titleTxt.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(H_MARGIN + 10f, 0f);
        titleRt.offsetMax = Vector2.zero;
        titleGO.AddComponent<Outline>().effectColor = new Color(0.8f, 0.5f, 0f, 0.6f);
        _headerTitle = titleTxt;

        // Buttons on the right side of the header (using UIStyle → anchor at parent centre)
        // Header is 1920 wide, so parent-centre in header = x:0  => +800 = near right edge
        _newLevelBtn = UIStyle.CreateButton(header.transform, "+ New Level",
            new Vector2(630f, 0f), new Vector2(210f, 54f),
            ShowNewLevelModal, UIStyle.AccentGreen);

        UIStyle.CreateButton(header.transform, "← Back",
            new Vector2(860f, 0f), new Vector2(180f, 54f),
            GoBack, UIStyle.AccentRed);
    }

    private void BuildCardsArea()
    {
        var area = new GameObject("CardsArea");
        area.transform.SetParent(_rootPanel, false);
        var areaRt = area.AddComponent<RectTransform>();
        areaRt.anchorMin = Vector2.zero;
        areaRt.anchorMax = Vector2.one;
        areaRt.offsetMin = new Vector2(H_MARGIN, BOT_BAR_H);
        areaRt.offsetMax = new Vector2(-H_MARGIN, -TOP_BAR_H);

        _cardsRoot = area.transform;
    }

    private void BuildFooter()
    {
        // Page indicator text — bottom-centre of rootPanel
        var pgGO = new GameObject("PageLabel");
        pgGO.transform.SetParent(_rootPanel, false);
        _pageLabel = pgGO.AddComponent<Text>();
        _pageLabel.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _pageLabel.fontSize  = 22;
        _pageLabel.color     = new Color(0.65f, 0.65f, 0.80f);
        _pageLabel.alignment = TextAnchor.MiddleCenter;
        var pgRt = _pageLabel.GetComponent<RectTransform>();
        pgRt.anchorMin = new Vector2(0f, 0f);
        pgRt.anchorMax = new Vector2(1f, 0f);
        pgRt.pivot     = new Vector2(0.5f, 0f);
        pgRt.anchoredPosition = new Vector2(0f, 8f);
        pgRt.sizeDelta = new Vector2(0f, 46f);

        // Prev / Next buttons (anchor = 0.5/0.5 of rootPanel → use anchored positions)
        _prevBtn = UIStyle.CreateButton(_rootPanel, "◄ Prev",
            new Vector2(-200f, -(540f - BOT_BAR_H * 0.5f)),
            new Vector2(160f, 44f),
            () => ShowPage(_page - 1), UIStyle.AccentBlue);

        _nextBtn = UIStyle.CreateButton(_rootPanel, "Next ►",
            new Vector2(200f, -(540f - BOT_BAR_H * 0.5f)),
            new Vector2(160f, 44f),
            () => ShowPage(_page + 1), UIStyle.AccentBlue);
    }

    // ── New-level modal ───────────────────────────────────────────────────────
    private void BuildNewLevelModal()
    {
        _modal = MakePanel(_rootPanel, "NewLevelModal", new Color(0f, 0f, 0f, 0.82f));
        Stretch(_modal);
        _modal.SetActive(false);

        // Centred card
        var card = MakePanel(_modal.transform, "Card", new Color(0.08f, 0.10f, 0.20f, 1f));
        var cRt  = card.GetComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot     = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(520f, 380f);
        cRt.anchoredPosition = Vector2.zero;
        card.AddComponent<Outline>().effectColor = UIStyle.AccentGold;

        float y = 160f;
        MakeModalLabel(card.transform, "CREATE NEW LEVEL", 0f, y, 44, UIStyle.AccentGold);
        y -= 70f;
        MakeModalLabel(card.transform, "Level ID  (e.g.  level_89)", -50f, y, 16, Color.white);
        y -= 28f;
        _modalId   = MakeModalField(card.transform, "level_XX", y, 420f);
        y -= 52f;
        MakeModalLabel(card.transform, "Display Name", -80f, y, 16, Color.white);
        y -= 28f;
        _modalName = MakeModalField(card.transform, "My New Level", y, 420f);
        y -= 52f;

        // Cols + Rows on one row
        MakeModalLabel(card.transform, "Cols", -140f, y, 16, Color.white);
        MakeModalLabel(card.transform, "Rows", 30f,   y, 16, Color.white);
        y -= 28f;
        _modalCols = MakeModalFieldAt(card.transform, "12", new Vector2(-100f, y), new Vector2(120f, 36f));
        _modalRows = MakeModalFieldAt(card.transform, "6",  new Vector2(80f,   y), new Vector2(120f, 36f));
        y -= 56f;

        UIStyle.CreateButton(card.transform, "Create", new Vector2(-80f, y),
            new Vector2(160f, 48f), ConfirmNewLevel, UIStyle.AccentGreen);
        UIStyle.CreateButton(card.transform, "Cancel", new Vector2(100f, y),
            new Vector2(160f, 48f), () => _modal.SetActive(false), UIStyle.AccentRed);
    }

    private void MakeModalLabel(Transform parent, string txt, float x, float y,
                                int size, Color color)
    {
        var go = new GameObject("Label_" + txt);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text      = txt;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = size;
        t.fontStyle = FontStyle.Bold;
        t.color     = color;
        t.alignment = TextAnchor.MiddleCenter;
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(440f, size + 6f);
    }

    private InputField MakeModalField(Transform parent, string placeholder, float y, float w)
        => MakeModalFieldAt(parent, placeholder, new Vector2(0f, y), new Vector2(w, 36f));

    private InputField MakeModalFieldAt(Transform parent, string placeholder,
                                        Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Field");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.04f, 0.06f, 0.14f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Outline>().effectColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);

        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(go.transform, false);
        var phTxt = phGO.AddComponent<Text>();
        phTxt.text      = placeholder;
        phTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        phTxt.fontSize  = 16;
        phTxt.color     = new Color(0.45f, 0.45f, 0.55f);
        phTxt.fontStyle = FontStyle.Italic;
        phTxt.alignment = TextAnchor.MiddleLeft;
        FullRect(phGO, 6f, 2f, -6f, -2f);

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 16;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;
        FullRect(txtGO, 6f, 2f, -6f, -2f);

        var field = go.AddComponent<InputField>();
        field.textComponent = txt;
        field.placeholder   = phTxt;
        field.targetGraphic = img;
        return field;
    }

    private void ShowNewLevelModal() => _modal.SetActive(true);

    private void ConfirmNewLevel()
    {
        string id   = (_modalId.text ?? "").Trim();
        string name = (_modalName.text ?? "").Trim();
        if (string.IsNullOrEmpty(id)) { _modalId.text = "level_XX"; return; }
        if (string.IsNullOrEmpty(name)) name = id;

        int cols = int.TryParse(_modalCols.text, out int c) ? Mathf.Clamp(c, 1, 20) : 12;
        int rows = int.TryParse(_modalRows.text, out int r) ? Mathf.Clamp(r, 1, 20) : 6;

        var data = new LevelData
        {
            id          = id,
            displayName = name,
            ballSpeed   = 8.5f,
            grid        = new GridConfig { cols = cols, rows = rows },
            bricks      = new System.Collections.Generic.List<BrickEntryData>()
        };

        _modal.SetActive(false);
        Hide();
        _editorUI?.OpenLevel(data, id);
    }

    // ── Level loading ─────────────────────────────────────────────────────────
    private void LoadLevels()
    {
        _levels.Clear();
        var assets = Resources.LoadAll<TextAsset>("Levels");

        var sorted = assets
            .Select(a =>
            {
                var m = Regex.Match(a.name, @"\d+");
                int idx = m.Success ? int.Parse(m.Value) : 9999;
                return (idx, a);
            })
            .OrderBy(t => t.idx)
            .Select(t => t.a);

        foreach (var asset in sorted)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<LevelData>(asset.text);
                if (data != null) _levels.Add((asset.name, data));
            }
            catch { /* skip bad JSON */ }
        }
    }

    // ── Pagination ────────────────────────────────────────────────────────────
    private void ShowPage(int p)
    {
        int totalPages = Mathf.Max(1, Mathf.CeilToInt(_levels.Count / (float)PAGE_SIZE));
        _page = Mathf.Clamp(p, 0, totalPages - 1);

        // Clear old cards
        for (int i = _cardsRoot.childCount - 1; i >= 0; i--)
            Destroy(_cardsRoot.GetChild(i).gameObject);

        // Available area inside cardsRoot: ~ 1840 × 930 (full – margins)
        // 4 cols × CARD_W + 3 × GAP = 4×420+3×18 = 1734  → centre offset = (1840-1734)/2 = 53
        // 4 rows × CARD_H + 3 × GAP = 4×215+3×18 = 914   → centre offset = (930-914)/2  = 8
        float startX = 53f;
        float startY = -8f;

        int start = _page * PAGE_SIZE;
        int end   = Mathf.Min(start + PAGE_SIZE, _levels.Count);

        for (int i = start; i < end; i++)
        {
            int slot = i - start;
            int col  = slot % PAGE_COLS;
            int row  = slot / PAGE_COLS;
            float cx = startX + col * (CARD_W + CARD_GAP);
            float cy = startY - row * (CARD_H + CARD_GAP);
            CreateCard(_levels[i].id, _levels[i].data, cx, cy);
        }

        _pageLabel.text = $"Page {_page + 1} / {totalPages}";
        _prevBtn.interactable = _page > 0;
        _nextBtn.interactable = _page < totalPages - 1;
    }

    private void CreateCard(string levelId, LevelData data, float x, float y)
    {
        var card = MakePanel(_cardsRoot, "Card_" + levelId,
            new Color(0.08f, 0.10f, 0.20f, 1f));
        var cRt = card.GetComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(0f, 1f);
        cRt.pivot     = new Vector2(0f, 1f);
        cRt.anchoredPosition = new Vector2(x, y);
        cRt.sizeDelta = new Vector2(CARD_W, CARD_H);
        card.AddComponent<Outline>().effectColor = new Color(0.25f, 0.35f, 0.60f, 0.7f);

        // Thumbnail area (top portion)
        var thumbGO = MakePanel(card.transform, "Thumb",
            new Color(0.05f, 0.07f, 0.15f, 1f));
        var tRt = thumbGO.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot     = new Vector2(0f, 1f);
        tRt.anchoredPosition = Vector2.zero;
        tRt.sizeDelta = new Vector2(0f, THUMB_H);

        BuildThumbnail(thumbGO.transform, data, CARD_W, THUMB_H);

        // Label area (bottom portion)
        var labelArea = new GameObject("LabelArea");
        labelArea.transform.SetParent(card.transform, false);
        var laRt = labelArea.AddComponent<RectTransform>();
        laRt.anchorMin = new Vector2(0f, 0f);
        laRt.anchorMax = new Vector2(1f, 1f);
        laRt.offsetMin = new Vector2(6f, 0f);
        laRt.offsetMax = new Vector2(-6f, -(THUMB_H + 2f));

        // ID label (small, grey)
        var idGO  = new GameObject("IdLabel");
        idGO.transform.SetParent(labelArea.transform, false);
        var idTxt = idGO.AddComponent<Text>();
        idTxt.text      = levelId;
        idTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        idTxt.fontSize  = 12;
        idTxt.color     = new Color(0.55f, 0.55f, 0.70f);
        idTxt.alignment = TextAnchor.UpperLeft;
        var idRt = idTxt.GetComponent<RectTransform>();
        idRt.anchorMin = new Vector2(0f, 1f);
        idRt.anchorMax = new Vector2(1f, 1f);
        idRt.pivot     = new Vector2(0f, 1f);
        idRt.anchoredPosition = new Vector2(0f, -2f);
        idRt.sizeDelta = new Vector2(0f, 16f);

        // Display name (slightly larger, white)
        var nameGO  = new GameObject("NameLabel");
        nameGO.transform.SetParent(labelArea.transform, false);
        var nameTxt = nameGO.AddComponent<Text>();
        nameTxt.text      = data.displayName ?? levelId;
        nameTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameTxt.fontSize  = 16;
        nameTxt.fontStyle = FontStyle.Bold;
        nameTxt.color     = Color.white;
        nameTxt.alignment = TextAnchor.UpperLeft;
        var nameRt = nameTxt.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot     = new Vector2(0f, 1f);
        nameRt.anchoredPosition = new Vector2(0f, -18f);
        nameRt.sizeDelta = new Vector2(0f, 20f);

        // Invisible button overlay on the whole card
        var btn = card.AddComponent<Button>();
        var btnImg = card.GetComponent<Image>();
        btn.targetGraphic = btnImg;
        var cols = btn.colors;
        cols.normalColor      = new Color(0.08f, 0.10f, 0.20f, 1f);
        cols.highlightedColor = new Color(0.14f, 0.18f, 0.32f, 1f);
        cols.pressedColor     = new Color(0.05f, 0.07f, 0.15f, 1f);
        cols.colorMultiplier  = 1f;
        btn.colors = cols;

        // Determine lock state (level select mode only)
        var  numMatch   = Regex.Match(levelId, @"\d+");
        int  levelIndex = numMatch.Success ? int.Parse(numMatch.Value) : -1;
        bool isLocked   = _isLevelSelectMode && (GameManager.Instance == null || !GameManager.Instance.IsLevelUnlocked(levelIndex));

        string capturedId   = levelId;
        LevelData capturedData = data;
        btn.onClick.AddListener(() =>
        {
            if (_isLevelSelectMode)
            {
                if (!isLocked)
                {
                    Hide();
                    _onLevelSelected?.Invoke(levelIndex);
                    ResetToEditorMode();
                }
            }
            else
            {
                // Deep-clone the data so edits don't mutate the cached object
                string json  = JsonConvert.SerializeObject(capturedData);
                var    clone = JsonConvert.DeserializeObject<LevelData>(json);
                Hide();
                _editorUI?.OpenLevel(clone, capturedId);
            }
        });

        // Lock overlay (level select mode, locked levels only)
        if (isLocked)
        {
            btn.interactable = false;

            var overlay = MakePanel(card.transform, "LockOverlay", new Color(0f, 0f, 0f, 0.60f));
            Stretch(overlay);

            var lockGO  = new GameObject("LockText");
            lockGO.transform.SetParent(overlay.transform, false);
            var lockTxt = lockGO.AddComponent<Text>();
            lockTxt.text      = "LOCKED";
            lockTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lockTxt.fontSize  = 28;
            lockTxt.fontStyle = FontStyle.Bold;
            lockTxt.color     = new Color(0.9f, 0.3f, 0.3f);
            lockTxt.alignment = TextAnchor.MiddleCenter;
            var lockRt = lockGO.GetComponent<RectTransform>() ?? lockGO.AddComponent<RectTransform>();
            lockRt.anchorMin = Vector2.zero;
            lockRt.anchorMax = Vector2.one;
            lockRt.offsetMin = lockRt.offsetMax = Vector2.zero;
        }
    }

    // ── Thumbnail ─────────────────────────────────────────────────────────────
    private void BuildThumbnail(Transform parent, LevelData data, float w, float h)
    {
        if (data?.bricks == null || data.bricks.Count == 0) return;

        var g = data.grid ?? new GridConfig();

        // Compute scale so the full grid fits inside (w-8) × (h-8) with padding
        float padW = w - 8f;
        float padH = h - 8f;

        float gridWorldW = g.cols * g.brickWidth + (g.cols - 1) * g.gapX;
        float gridWorldH = g.rows * g.brickHeight + (g.rows - 1) * g.gapY;

        float scaleX = padW / gridWorldW;
        float scaleY = padH / gridWorldH;
        float scale  = Mathf.Min(scaleX, scaleY);

        float cellW    = g.brickWidth  * scale;
        float cellH    = g.brickHeight * scale;
        float stepX    = (g.brickWidth  + g.gapX) * scale;
        float stepY    = (g.brickHeight + g.gapY) * scale;

        float totalW = gridWorldW * scale;
        float totalH = gridWorldH * scale;
        float ox = (w - totalW) * 0.5f;       // left edge offset
        float oy = (h - totalH) * 0.5f;       // bottom edge offset (flipped below)

        foreach (var b in data.bricks)
        {
            float bx = ox + b.col * stepX + cellW * 0.5f;
            float by = oy + (g.rows - 1 - b.row) * stepY + cellH * 0.5f;

            var brickGO = new GameObject("TB");
            brickGO.transform.SetParent(parent, false);
            var img = brickGO.AddComponent<Image>();

            // Colour: JSON tint > template colour
            Color color = TemplateColor(b.templateId);
            if (!string.IsNullOrEmpty(b.tint))
                ColorUtility.TryParseHtmlString(b.tint, out color);
            img.color = color;

            var rt = brickGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(bx, by);
            rt.sizeDelta = new Vector2(cellW, cellH);
        }
    }

    internal Color TemplateColor(string templateId)
    {
        if (string.IsNullOrEmpty(templateId)) return new Color(0.7f, 0.7f, 0.7f);
        TemplateColors.TryGetValue(templateId.ToLower(), out Color c);
        return c == default ? new Color(0.7f, 0.7f, 0.7f) : c;
    }

    // ── Navigation ────────────────────────────────────────────────────────────
    private void GoBack()
    {
        // Reset level-select mode so the next open behaves as editor again.
        ResetToEditorMode();

        // Restore runtime state (don't assume we're returning to a running game).
        if (_hasSavedRuntimeState)
        {
            Time.timeScale     = _prevTimeScale;
            AudioListener.pause = _prevAudioPaused;
        }
        else
        {
            Time.timeScale     = 1f;
            AudioListener.pause = false;
        }
        _hasSavedRuntimeState = false;

        Hide();
        _onBack?.Invoke();
    }

    // ── UGUI helpers ──────────────────────────────────────────────────────────
    private static GameObject MakePanel(Transform parent, string name, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void FullRect(GameObject go, float l, float b, float r, float t)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(r, t);
    }
}
