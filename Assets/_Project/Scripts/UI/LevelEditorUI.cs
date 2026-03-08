using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// In-game level editor — grid-based brick editor with palette, properties panel, and save.
/// Editor-only: Save writes directly to Assets/_Project/Resources/Levels/.
/// </summary>
public class LevelEditorUI : MonoBehaviour
{
    // ── Template / powerup lists ───────────────────────────────────────────────
    private static readonly string[] TemplateIds =
    {
        "standard", "red", "blue", "steel", "gem",
        "gold", "purple", "green", "cyan", "dark",
        "ghost", "bumper"
    };

    private static readonly string[] PowerupIds =
    {
        "(none)",
        "WidePaddle", "MultiBall", "StickyBall", "SpeedBall", "ExtraLife",
        "Laser", "Fireball", "BombBrick", "ShieldWall", "BigBall",
        "ScoreFrenzy", "PermanentStickyBall",
        "ShrinkPaddle", "ZipBall", "FlipControls", "CursedBall",
        "TinyBall", "InvisiBall", "DrunkenPaddle", "DrunkVision", "GremlinBounces"
    };

    private static readonly string[] BallColors = { "none", "blue", "red", "green", "yellow" };

    // ── Layout constants ───────────────────────────────────────────────────────
    private const float LEFT_W   = 248f;   // template / powerup palette
    private const float RIGHT_W  = 290f;   // properties panel
    private const float TOP_H    = 72f;    // level metadata bar
    private const float BOT_H    = 62f;    // save / cancel bar
    private const float CELL_W   = 90f;    // brick cell display width (px)
    private const float CELL_H   = 30f;    // brick cell display height (px)
    private const float GAP_X    = 8f;
    private const float GAP_Y    = 8f;
    private const float STEP_X   = CELL_W + GAP_X;
    private const float STEP_Y   = CELL_H + GAP_Y;

    // Empty-cell background colour and occupied-cell selection tint
    private static readonly Color CellEmpty    = new Color(0.08f, 0.10f, 0.18f, 0.55f);
    private static readonly Color CellSelected = new Color(1.00f, 0.85f, 0.10f, 0.30f);

    // ── Runtime state ─────────────────────────────────────────────────────────
    private LevelData      _data;
    private string         _levelId;
    private bool           _isReadOnly;
    private BrickEntryData _selected;
    private int            _selectedCol = -1, _selectedRow = -1;
    private string         _activeTemplate = "standard";

    // ── Bottom bar button refs (toggled based on read-only state) ─────────────
    private Button     _saveBtnRef;
    private GameObject _saveBtnGO;
    private GameObject _cloneBtnGO;
    private GameObject _readOnlyBanner;

    // Drag state
    private bool   _isDragging;
    private bool   _mouseDown;
    private int    _pressCol, _pressRow;
    private Vector2 _pressScreenPos;
    private GameObject _ghost;
    private const float DRAG_THRESHOLD = 10f;

    // ── References ────────────────────────────────────────────────────────────
    private LevelEditorBrowserUI _browser;
    private Canvas               _canvas;
    private RectTransform        _gridPanel;   // parent of all cell GOs
    private Transform            _rootPanel;

    // Cells — created once per OpenLevel, reused across RefreshGrid
    private readonly Dictionary<(int col, int row), Image> _cellImages =
        new Dictionary<(int, int), Image>();
    private readonly Dictionary<(int col, int row), GameObject> _cellGOs =
        new Dictionary<(int, int), GameObject>();

    // ── Top-bar fields ────────────────────────────────────────────────────────
    private InputField _fieldName, _fieldSpeed, _fieldCols, _fieldRows;
    private InputField _fieldBrickW, _fieldBrickH, _fieldGapX, _fieldGapY;
    private InputField _fieldOrder;   // levelOrder for user-created levels

    // ── Bottom-bar community toggle ────────────────────────────────────────────
    private Toggle     _communityToggle;
    private GameObject _communityToggleGO;

    // ── Properties panel widgets ──────────────────────────────────────────────
    // _propTitle intentionally unused — the title label is always visible in the panel
    private InputField _propCol, _propRow, _propTemplate, _propPowerup;
    private InputField _propHp, _propPoints, _propTint, _propReqColor;
    private Toggle     _propIndestructible;
    private Toggle     _propHasMovement;
    private InputField _propMovType, _propMovAmp, _propMovPeriod, _propMovPhase;
    private Toggle     _propHasRotation;
    private InputField _propRotSpeed, _propRotAngle;
    private GameObject _movSection, _rotSection;

    // Active-template indicator in palette
    private Text _activeTemplateLabel;

    // Prism gates panel
    private Transform     _gatesList;
    private RectTransform _propsContentRt;
    private float         _propsBaseContentH;

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        SetupCanvas();
        BuildUI();

        // Auto-wire (SetBrowser called at edit-time by PurrbricksSetup isn't serialized)
        if (_browser == null)
            _browser = Object.FindFirstObjectByType<LevelEditorBrowserUI>(FindObjectsInactive.Include);

        gameObject.SetActive(false);
    }

    public void SetBrowser(LevelEditorBrowserUI browser) => _browser = browser;

    // ── Open / Show / Hide ────────────────────────────────────────────────────
    public void OpenLevel(LevelData data, string levelId, bool readOnly = false)
    {
        _data       = data;
        _levelId    = levelId;
        _isReadOnly = readOnly;
        _selected    = null;
        _selectedCol = -1; _selectedRow = -1;
        _activeTemplate = "standard";

        // Populate top-bar from data
        _fieldName.text  = data.displayName ?? levelId;
        _fieldSpeed.text = data.ballSpeed.ToString("F1");
        var g = data.grid ?? new GridConfig();
        _fieldCols.text   = g.cols.ToString();
        _fieldRows.text   = g.rows.ToString();
        _fieldBrickW.text = g.brickWidth.ToString("F2");
        _fieldBrickH.text = g.brickHeight.ToString("F2");
        _fieldGapX.text   = g.gapX.ToString("F2");
        _fieldGapY.text   = g.gapY.ToString("F2");
        if (_fieldOrder != null)
            _fieldOrder.text = data.levelOrder >= 0 ? data.levelOrder.ToString() : "";

        BuildCells();
        RefreshGrid();
        ClearProps();
        RefreshGatesPanel();
        ApplyReadOnlyMode(_isReadOnly);
        gameObject.SetActive(true);
    }

    private void ApplyReadOnlyMode(bool readOnly)
    {
        // VIEW ONLY banner
        if (_readOnlyBanner != null) _readOnlyBanner.SetActive(readOnly);

        // Bottom-bar button visibility
        if (_saveBtnGO    != null) _saveBtnGO.SetActive(!readOnly);
        if (_cloneBtnGO   != null) _cloneBtnGO.SetActive(readOnly);

        // Community toggle: only show for non-read-only, non-native levels
        bool showCommunity = !readOnly && !IsNativeLevel() && CommunityLevelService.Instance != null;
        if (_communityToggleGO != null) _communityToggleGO.SetActive(showCommunity);
        // Pre-check toggle if level is already published
        if (_communityToggle != null)
        {
            bool alreadyPublished = showCommunity &&
                (CommunityLevelService.Instance?.IsPublished(_data?.levelGuid ?? "") ?? false);
            _communityToggle.isOn = alreadyPublished;
        }

        // Fields: read-only when in view mode
        bool fieldInteract = !readOnly;
        if (_fieldName  != null) _fieldName.interactable  = fieldInteract;
        if (_fieldSpeed != null) _fieldSpeed.interactable = fieldInteract;
        if (_fieldCols  != null) _fieldCols.interactable  = fieldInteract;
        if (_fieldRows  != null) _fieldRows.interactable  = fieldInteract;
        if (_fieldBrickW != null) _fieldBrickW.interactable = fieldInteract;
        if (_fieldBrickH != null) _fieldBrickH.interactable = fieldInteract;
        if (_fieldGapX  != null) _fieldGapX.interactable  = fieldInteract;
        if (_fieldGapY  != null) _fieldGapY.interactable  = fieldInteract;
    }

    private bool IsNativeLevel() => _data?.nativeLevel ?? false;

    private void CloneBuiltInLevel()
    {
        if (_data == null) return;
        // Find the highest numeric index currently in use across ALL level files
        var existing = Resources.LoadAll<TextAsset>("Levels");
        int maxIdx = 0;
        foreach (var a in existing)
        {
            var mx = System.Text.RegularExpressions.Regex.Match(a.name, @"\d+");
            if (mx.Success && int.TryParse(mx.Value, out int i) && i > maxIdx)
                maxIdx = i;
        }
        int newIdx = maxIdx + 1;
        string newId = $"level_{newIdx}";

        string json  = Newtonsoft.Json.JsonConvert.SerializeObject(_data);
        var    clone = Newtonsoft.Json.JsonConvert.DeserializeObject<LevelData>(json);
        clone.id          = newId;
        clone.nativeLevel = false;  // clone is a user level, not a native level
        clone.levelOrder  = -1;
        OpenLevel(clone, newId, readOnly: false);
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

    // ── Update ────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (!gameObject.activeSelf) return;

        // Delete / Escape
        if (Input.GetKeyDown(KeyCode.Delete) && _selected != null)
            DeleteSelected();
        if (Input.GetKeyDown(KeyCode.Escape))
            ClearSelection();

        // Drag detection while LMB is held
        if (_mouseDown && !_isDragging)
        {
            if (Vector2.Distance(Input.mousePosition, _pressScreenPos) > DRAG_THRESHOLD)
            {
                var brick = FindBrick(_pressCol, _pressRow);
                if (brick != null)
                    StartDrag(_pressCol, _pressRow);
                else
                    _mouseDown = false; // nothing to drag
            }
        }

        if (_isDragging)
        {
            UpdateGhost();
            if (!Input.GetMouseButton(0))
                EndDrag();
        }
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

    // ═════════════════════════════════════════════════════════════════════════
    //  UI CONSTRUCTION
    // ═════════════════════════════════════════════════════════════════════════
    private void BuildUI()
    {
        var root = MakePanel(transform, "EditorRoot", new Color(0.04f, 0.06f, 0.12f, 1f));
        Stretch(root);
        _rootPanel = root.transform;

        BuildTopBar();
        BuildLeftPalette();
        BuildRightProps();
        BuildCenterGrid();
        BuildBottomBar();
    }

    // ── Top bar ───────────────────────────────────────────────────────────────
    private void BuildTopBar()
    {
        var bar = MakePanel(_rootPanel, "TopBar", new Color(0.06f, 0.08f, 0.16f, 0.95f));
        var rt  = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0f, TOP_H);

        float x = -860f;  // starting x (relative to bar centre, which is 1920/2 wide)
        float labelY = 14f, fieldY = -14f;

        x = TopField(bar.transform, "Level Name", x, 320f, "displayName",
                     out _fieldName, labelY, fieldY);
        x = TopField(bar.transform, "Ball Speed", x, 110f, "8.5",
                     out _fieldSpeed, labelY, fieldY);
        x = TopField(bar.transform, "Cols", x, 80f, "12",
                     out _fieldCols, labelY, fieldY);
        x = TopField(bar.transform, "Rows", x, 80f, "6",
                     out _fieldRows, labelY, fieldY);
        x = TopField(bar.transform, "Brk W", x, 90f, "1.35",
                     out _fieldBrickW, labelY, fieldY);
        x = TopField(bar.transform, "Brk H", x, 90f, "0.45",
                     out _fieldBrickH, labelY, fieldY);
        x = TopField(bar.transform, "Gap X", x, 85f, "0.08",
                     out _fieldGapX, labelY, fieldY);
        x =  TopField(bar.transform, "Gap Y", x, 85f, "0.16",
                     out _fieldGapY, labelY, fieldY);
             TopField(bar.transform, "Order", x, 70f, "-1",
                     out _fieldOrder, labelY, fieldY);

        // Rebuild-grid button
        UIStyle.CreateButton(bar.transform, "↺ Rebuild Grid",
            new Vector2(830f, 0f), new Vector2(170f, 52f),
            RebuildGrid, UIStyle.AccentBlue);
    }

    /// Creates a labelled InputField in the top bar and returns the next x position.
    private float TopField(Transform bar, string label, float startX, float width,
                           string placeholder, out InputField field, float labelY, float fieldY)
    {
        float cx = startX + width * 0.5f;

        var lbl = new GameObject("Lbl_" + label);
        lbl.transform.SetParent(bar, false);
        var lt = lbl.AddComponent<Text>();
        lt.text = label; lt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lt.fontSize = 13; lt.color = new Color(0.65f, 0.65f, 0.80f);
        lt.alignment = TextAnchor.MiddleCenter;
        var lRt = lt.GetComponent<RectTransform>();
        lRt.anchorMin = lRt.anchorMax = new Vector2(0.5f, 0.5f);
        lRt.pivot = new Vector2(0.5f, 0.5f);
        lRt.anchoredPosition = new Vector2(cx, labelY);
        lRt.sizeDelta = new Vector2(width - 4f, 18f);

        field = CreateInputField(bar, placeholder, new Vector2(cx, fieldY),
                                 new Vector2(width - 4f, 30f));
        return startX + width + 8f;
    }

    // ── Left palette ──────────────────────────────────────────────────────────
    private void BuildLeftPalette()
    {
        // Outer panel (clip area + dark background)
        var pal = MakePanel(_rootPanel, "Palette", new Color(0.06f, 0.08f, 0.16f, 0.95f));
        var palRt = pal.GetComponent<RectTransform>();
        palRt.anchorMin = new Vector2(0f, 0f); palRt.anchorMax = new Vector2(0f, 1f);
        palRt.pivot = new Vector2(0f, 0.5f);
        palRt.anchoredPosition = Vector2.zero;
        palRt.sizeDelta = new Vector2(LEFT_W, -(TOP_H + BOT_H));

        // "Active template" indicator — pinned at top of the outer panel
        var indGO = new GameObject("ActiveIndicator");
        indGO.transform.SetParent(pal.transform, false);
        _activeTemplateLabel = indGO.AddComponent<Text>();
        _activeTemplateLabel.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _activeTemplateLabel.fontSize  = 13;
        _activeTemplateLabel.color     = UIStyle.AccentGold;
        _activeTemplateLabel.alignment = TextAnchor.MiddleCenter;
        _activeTemplateLabel.text      = "Painting: standard";
        var aiRt = _activeTemplateLabel.GetComponent<RectTransform>();
        aiRt.anchorMin = new Vector2(0f, 1f); aiRt.anchorMax = new Vector2(1f, 1f);
        aiRt.pivot = new Vector2(0.5f, 1f);
        aiRt.anchoredPosition = new Vector2(0f, 0f);
        aiRt.sizeDelta = new Vector2(0f, 24f);

        // Scrollable area below the indicator
        const float INDICATOR_H = 26f;
        var scroll = new GameObject("PaletteScroll");
        scroll.transform.SetParent(pal.transform, false);
        var scrollRt = scroll.AddComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(0f, 0f);
        scrollRt.offsetMax = new Vector2(0f, -INDICATOR_H);

        var scrollImg = scroll.AddComponent<Image>();
        scrollImg.color = new Color(0f, 0f, 0f, 0.01f);
        scroll.AddComponent<Mask>().showMaskGraphic = false;

        var scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal       = false;
        scrollRect.vertical         = true;
        scrollRect.scrollSensitivity = 30f;

        // Content holder (will grow to fit all buttons)
        var content = new GameObject("PaletteContent");
        content.transform.SetParent(scroll.transform, false);
        var contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f); contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot     = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = Vector2.zero; // height set below

        scrollRect.content  = contentRt;
        scrollRect.viewport = scrollRt;

        var inner = content.transform;
        float btnY = 0f;  // local Y inside content (top-left origin)

        PaletteSectionLabelLocal(inner, "── TEMPLATES ──", ref btnY);
        foreach (var tid in TemplateIds)
        {
            Color c = LevelEditorBrowserUI.TemplateColors.TryGetValue(tid, out var tc)
                      ? tc : new Color(0.7f, 0.7f, 0.7f);
            string capturedId = tid;
            CreatePaletteButtonLocal(inner, tid, c, ref btnY,
                () => SelectTemplate(capturedId));
        }

        btnY -= 8f;
        PaletteSectionLabelLocal(inner, "── POWERUPS ──", ref btnY);
        foreach (var pid in PowerupIds)
        {
            string capturedPid = pid;
            Color col = pid == "(none)"
                        ? new Color(0.4f, 0.4f, 0.45f) : UIStyle.AccentMagenta;
            CreatePaletteButtonLocal(inner, pid, col, ref btnY,
                () => AssignPowerupToSelected(capturedPid == "(none)" ? null : capturedPid));
        }

        // Set content height so all buttons are reachable
        contentRt.sizeDelta = new Vector2(0f, -btnY + 8f);
    }

    // Palette section label anchored at top-left of content, advances y
    private void PaletteSectionLabelLocal(Transform parent, string txt, ref float y)
    {
        const float H = 22f;
        var go = new GameObject("SecLabel");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 12; t.color = new Color(0.45f, 0.55f, 0.75f);
        t.alignment = TextAnchor.MiddleCenter;
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(0f, H);
        y -= H;
    }

    private void CreatePaletteButtonLocal(Transform parent, string label, Color swatchColor,
                                          ref float y, System.Action onClick)
    {
        const float H = 32f;
        var go = new GameObject("PalBtn_" + label);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.09f, 0.12f, 0.22f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cols = btn.colors;
        cols.normalColor      = new Color(0.09f, 0.12f, 0.22f);
        cols.highlightedColor = new Color(0.14f, 0.18f, 0.32f);
        cols.pressedColor     = new Color(0.04f, 0.06f, 0.12f);
        cols.colorMultiplier  = 1f; btn.colors = cols;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-8f, H);

        // Colour swatch
        var sw   = new GameObject("Swatch");
        sw.transform.SetParent(go.transform, false);
        var swImg = sw.AddComponent<Image>();
        swImg.color = swatchColor;
        var swRt = swImg.GetComponent<RectTransform>();
        swRt.anchorMin = new Vector2(0f, 0f); swRt.anchorMax = new Vector2(0f, 1f);
        swRt.pivot = new Vector2(0f, 0.5f);
        swRt.anchoredPosition = new Vector2(4f, 0f);
        swRt.sizeDelta = new Vector2(18f, -6f);

        // Label text
        var lGO = new GameObject("Label");
        lGO.transform.SetParent(go.transform, false);
        var lt = lGO.AddComponent<Text>();
        lt.text = label; lt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lt.fontSize = 13; lt.fontStyle = FontStyle.Bold;
        lt.color = Color.white; lt.alignment = TextAnchor.MiddleLeft;
        var lRt = lt.GetComponent<RectTransform>();
        lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
        lRt.offsetMin = new Vector2(28f, 0f); lRt.offsetMax = new Vector2(-4f, 0f);

        y -= H + 4f;
    }

    // ── Right properties panel ────────────────────────────────────────────────
    private void BuildRightProps()
    {
        var panel = MakePanel(_rootPanel, "PropsPanel", new Color(0.06f, 0.08f, 0.16f, 0.95f));
        var rt    = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(RIGHT_W, -(TOP_H + BOT_H));

        // Scrollable interior
        var scroll = new GameObject("PropsScroll");
        scroll.transform.SetParent(panel.transform, false);
        var scrollRt2 = scroll.AddComponent<RectTransform>();
        scrollRt2.anchorMin = Vector2.zero; scrollRt2.anchorMax = Vector2.one;
        scrollRt2.offsetMin = scrollRt2.offsetMax = Vector2.zero;
        var scrollImg2 = scroll.AddComponent<Image>();
        scrollImg2.color = new Color(0f, 0f, 0f, 0.01f);
        scroll.AddComponent<Mask>().showMaskGraphic = false;
        var scrollRect2 = scroll.AddComponent<ScrollRect>();
        scrollRect2.horizontal = false; scrollRect2.vertical = true;
        scrollRect2.scrollSensitivity = 30f;

        var propsContent = new GameObject("PropsContent");
        propsContent.transform.SetParent(scroll.transform, false);
        var pcRt = propsContent.AddComponent<RectTransform>();
        pcRt.anchorMin = new Vector2(0f, 1f); pcRt.anchorMax = new Vector2(1f, 1f);
        pcRt.pivot = new Vector2(0.5f, 1f);
        pcRt.anchoredPosition = Vector2.zero; pcRt.sizeDelta = Vector2.zero;
        scrollRect2.content = pcRt; scrollRect2.viewport = scrollRt2;
        _propsContentRt = pcRt;

        var T = propsContent.transform;
        float y = -10f;  // local y inside propsContent

        PropTitle(T, "BRICK PROPERTIES", ref y);

        _propCol      = PropField(T, "Col",              ref y, "0");
        _propRow      = PropField(T, "Row",              ref y, "0");
        _propTemplate = PropField(T, "Template ID",      ref y, "standard");
        _propPowerup  = PropField(T, "Powerup ID",       ref y, "(none)");
        _propHp       = PropField(T, "HP (blank=default)", ref y, "");
        _propPoints   = PropField(T, "Points (blank=default)", ref y, "");
        _propTint     = PropField(T, "Tint (#RRGGBB)",   ref y, "");
        _propReqColor = PropField(T, "Required Ball Color", ref y, "none");
        _propIndestructible = PropToggle(T, "Indestructible", ref y);

        y -= 6f;
        PropSectionDivider(T, "── Movement ──", ref y);
        _propHasMovement = PropToggle(T, "Has Movement", ref y);

        _movSection = new GameObject("MovSection");
        _movSection.transform.SetParent(T, false);
        var movRt = _movSection.AddComponent<RectTransform>();
        movRt.anchorMin = new Vector2(0f, 1f); movRt.anchorMax = new Vector2(1f, 1f);
        movRt.pivot = new Vector2(0.5f, 1f);
        movRt.anchoredPosition = new Vector2(0f, y);
        movRt.sizeDelta = new Vector2(0f, 0f); // height will be set by children

        float my = 0f;
        _propMovType   = PropFieldIn(_movSection.transform, "Type (h/v/circular)", ref my, "horizontal");
        _propMovAmp    = PropFieldIn(_movSection.transform, "Amplitude",  ref my, "1.5");
        _propMovPeriod = PropFieldIn(_movSection.transform, "Period",     ref my, "2.5");
        _propMovPhase  = PropFieldIn(_movSection.transform, "PhaseOffset",ref my, "0");
        movRt.sizeDelta = new Vector2(0f, -my);
        y += my;

        y -= 6f;
        PropSectionDivider(T, "── Rotation ──", ref y);
        _propHasRotation = PropToggle(T, "Has Rotation", ref y);

        _rotSection = new GameObject("RotSection");
        _rotSection.transform.SetParent(T, false);
        var rotRt = _rotSection.AddComponent<RectTransform>();
        rotRt.anchorMin = new Vector2(0f, 1f); rotRt.anchorMax = new Vector2(1f, 1f);
        rotRt.pivot = new Vector2(0.5f, 1f);
        rotRt.anchoredPosition = new Vector2(0f, y);
        rotRt.sizeDelta = Vector2.zero;

        float ry = 0f;
        _propRotSpeed = PropFieldIn(_rotSection.transform, "Rotation Speed", ref ry, "180");
        _propRotAngle = PropFieldIn(_rotSection.transform, "Start Angle",    ref ry, "0");
        rotRt.sizeDelta = new Vector2(0f, -ry);
        y += ry;

        y -= 10f;
        var applyBtn = UIStyle.CreateButton(T, "✓ Apply Changes",
            new Vector2(0f, y - 22f), new Vector2(RIGHT_W - 16f, 44f),
            ApplyProps, UIStyle.AccentGreen);
        LeftAnchorBtn(applyBtn, y - 22f, RIGHT_W - 16f, 44f);
        y -= 54f;

        // "Add Another Brick Here" — places a second brick at the same cell (for multi-brick/movement stacks)
        var addAnotherBtn = UIStyle.CreateButton(T, "＋ Add Another at Same Cell",
            new Vector2(0f, y - 18f), new Vector2(RIGHT_W - 16f, 36f),
            AddAnotherBrickHere, UIStyle.AccentBlue);
        LeftAnchorBtn(addAnotherBtn, y - 18f, RIGHT_W - 16f, 36f);
        y -= 46f;

        // ── Test Level ────────────────────────────────────────────────────────
        y -= 8f;
        PropSectionDivider(T, "── TEST ──", ref y);
        var testBtn = UIStyle.CreateButton(T, "▶ Test Level",
            new Vector2(0f, y - 18f), new Vector2(RIGHT_W - 16f, 36f),
            TestLevel, UIStyle.AccentGreen);
        LeftAnchorBtn(testBtn, y - 18f, RIGHT_W - 16f, 36f);
        y -= 46f;

        // ── Prism Gates ───────────────────────────────────────────────────────
        y -= 8f;
        PropSectionDivider(T, "── PRISM GATES ──", ref y);
        var addGateBtn = UIStyle.CreateButton(T, "＋ Add Prism Gate",
            new Vector2(0f, y - 18f), new Vector2(RIGHT_W - 16f, 36f),
            AddPrismGate, UIStyle.AccentBlue);
        LeftAnchorBtn(addGateBtn, y - 18f, RIGHT_W - 16f, 36f);
        y -= 46f;

        // Dynamic gates list container (rebuilt by RefreshGatesPanel)
        var gatesListGO = new GameObject("GatesList");
        gatesListGO.transform.SetParent(T, false);
        var glRt = gatesListGO.AddComponent<RectTransform>();
        glRt.anchorMin = new Vector2(0f, 1f); glRt.anchorMax = new Vector2(1f, 1f);
        glRt.pivot = new Vector2(0.5f, 1f);
        glRt.anchoredPosition = new Vector2(0f, y);
        glRt.sizeDelta = Vector2.zero;
        _gatesList = gatesListGO.transform;

        _propsBaseContentH = -y + 20f;
        pcRt.sizeDelta = new Vector2(0f, _propsBaseContentH);
    }

    // Helper: make a props-panel field anchored top-left of the right panel
    private InputField PropField(Transform parent, string label, ref float y, string placeholder)
    {
        float startY = y;
        MakePropLabel(parent, label, y);
        y -= 18f;
        var f = CreateInputField(parent, placeholder, new Vector2(LEFT_W * 0.5f, y),
                                 new Vector2(RIGHT_W - 16f, 28f), topLeft: true);
        y -= 32f;
        return f;
    }

    // Same but parented to a sub-container with local y
    private InputField PropFieldIn(Transform parent, string label, ref float y, string placeholder)
    {
        MakePropLabelLocal(parent, label, y);
        y -= 18f;
        var f = CreateInputFieldLocal(parent, placeholder,
                                      new Vector2(8f, y), new Vector2(RIGHT_W - 16f, 28f));
        y -= 32f;
        return f;
    }

    private Toggle PropToggle(Transform parent, string label, ref float y)
    {
        var t = CreateToggle(parent, label, new Vector2(8f, y), true);
        y -= 30f;
        return t;
    }

    private void PropTitle(Transform parent, string txt, ref float y)
    {
        var go = new GameObject("PropTitle");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 16; t.fontStyle = FontStyle.Bold;
        t.color = UIStyle.AccentGold; t.alignment = TextAnchor.MiddleCenter;
        AnchorTopLeft(go, 0f, y, RIGHT_W, 24f);
        y -= 28f;
    }

    private void PropSectionDivider(Transform parent, string txt, ref float y)
    {
        var go = new GameObject("Divider");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 12; t.color = new Color(0.45f, 0.55f, 0.75f);
        t.alignment = TextAnchor.MiddleCenter;
        AnchorTopLeft(go, 0f, y, RIGHT_W, 18f);
        y -= 22f;
    }

    private void MakePropLabel(Transform parent, string txt, float y)
    {
        var go = new GameObject("Lbl_" + txt);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 12; t.color = new Color(0.60f, 0.65f, 0.80f);
        t.alignment = TextAnchor.MiddleLeft;
        AnchorTopLeft(go, 8f, y, RIGHT_W - 16f, 18f);
    }

    private void MakePropLabelLocal(Transform parent, string txt, float y)
    {
        var go = new GameObject("Lbl_" + txt);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 12; t.color = new Color(0.60f, 0.65f, 0.80f);
        t.alignment = TextAnchor.MiddleLeft;
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(8f, y);
        rt.sizeDelta = new Vector2(-16f, 18f);
    }

    private static void AnchorTopLeft(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void LeftAnchorBtn(Button btn, float y, float w, float h)
    {
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(8f, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // ── Centre grid area ──────────────────────────────────────────────────────
    private void BuildCenterGrid()
    {
        // Scrollable container
        var scroll = new GameObject("GridScroll");
        scroll.transform.SetParent(_rootPanel, false);
        var scrollRt = scroll.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(LEFT_W, BOT_H);
        scrollRt.offsetMax = new Vector2(-RIGHT_W, -TOP_H);

        var scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical   = true;

        // Mask
        var maskImg = scroll.AddComponent<Image>();
        maskImg.color = new Color(0f, 0f, 0f, 0.01f); // nearly invisible but needed for Mask
        scroll.AddComponent<Mask>().showMaskGraphic = false;

        // Content panel (grows to fit grid)
        var content = new GameObject("GridContent");
        content.transform.SetParent(scroll.transform, false);
        _gridPanel = content.AddComponent<RectTransform>();
        _gridPanel.anchorMin = new Vector2(0f, 1f);
        _gridPanel.anchorMax = new Vector2(0f, 1f);
        _gridPanel.pivot     = new Vector2(0f, 1f);
        _gridPanel.anchoredPosition = Vector2.zero;

        // Dark background panel behind all cells
        var bg = content.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.13f);

        scrollRect.content  = _gridPanel;
        scrollRect.viewport = scrollRt;
    }

    // ── Bottom bar ────────────────────────────────────────────────────────────
    private void BuildBottomBar()
    {
        var bar = MakePanel(_rootPanel, "BottomBar", new Color(0.06f, 0.08f, 0.16f, 0.95f));
        var rt  = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, BOT_H);

        UIStyle.CreateButton(bar.transform, "✕ Cancel / Back",
            new Vector2(-500f, 0f), new Vector2(220f, 48f),
            ConfirmCancel, UIStyle.AccentRed);

        // VIEW ONLY banner (shown in read-only mode)
        _readOnlyBanner = new GameObject("ReadOnlyBanner");
        _readOnlyBanner.transform.SetParent(bar.transform, false);
        var roBannerImg = _readOnlyBanner.AddComponent<Image>();
        roBannerImg.color = new Color(0.55f, 0.35f, 0f, 0.85f);
        var roBannerRt = _readOnlyBanner.GetComponent<RectTransform>();
        roBannerRt.anchorMin = roBannerRt.anchorMax = new Vector2(0.5f, 0.5f);
        roBannerRt.sizeDelta = new Vector2(380f, 44f);
        roBannerRt.anchoredPosition = new Vector2(0f, 0f);
        var roBannerLblGO = new GameObject("Lbl");
        roBannerLblGO.transform.SetParent(_readOnlyBanner.transform, false);
        var roBannerLbl = roBannerLblGO.AddComponent<Text>();
        roBannerLbl.text = "VIEW ONLY — Built-in levels are read-only";
        roBannerLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        roBannerLbl.fontSize = 16;
        roBannerLbl.fontStyle = FontStyle.Bold;
        roBannerLbl.alignment = TextAnchor.MiddleCenter;
        roBannerLbl.color = UIStyle.AccentGold;
        roBannerLbl.raycastTarget = false;
        var roBannerLblRt = roBannerLblGO.GetComponent<RectTransform>();
        roBannerLblRt.anchorMin = Vector2.zero; roBannerLblRt.anchorMax = Vector2.one;
        roBannerLblRt.offsetMin = new Vector2(8f, 0f); roBannerLblRt.offsetMax = new Vector2(-8f, 0f);
        _readOnlyBanner.SetActive(false);

        // Clone button (shown only in read-only mode)
        _cloneBtnGO = UIStyle.CreateButton(bar.transform, "⎘ Clone as New Level",
            new Vector2(500f, 0f), new Vector2(260f, 48f),
            CloneBuiltInLevel, UIStyle.AccentGold).gameObject;
        _cloneBtnGO.SetActive(false);

        // "Submit to Community" toggle — when checked, Save also opens the publish dialog
        _communityToggle = CreateToggle(bar.transform, "Submit to Community?",
            new Vector2(-120f, 0f));
        _communityToggleGO = _communityToggle.gameObject;
        _communityToggleGO.SetActive(false);

        _saveBtnGO = UIStyle.CreateButton(bar.transform, "✓ Save Level",
            new Vector2(500f, 0f), new Vector2(220f, 48f),
            SaveLevel, UIStyle.AccentGreen).gameObject;
        _saveBtnRef = _saveBtnGO.GetComponent<Button>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  GRID BUILDING & REFRESHING
    // ═════════════════════════════════════════════════════════════════════════

    /// Creates all cell GameObjects based on current grid config. Destroys old ones first.
    private void BuildCells()
    {
        // Destroy previous cells
        foreach (var kvp in _cellGOs)
            if (kvp.Value != null) Destroy(kvp.Value);
        _cellGOs.Clear();
        _cellImages.Clear();

        if (_data == null) return;
        var g = _data.grid ?? new GridConfig();

        // Size the content panel to fit the grid
        float totalW = g.cols * STEP_X + GAP_X;
        float totalH = g.rows * STEP_Y + GAP_Y;
        _gridPanel.sizeDelta = new Vector2(totalW, totalH);

        for (int row = 0; row < g.rows; row++)
        {
            for (int col = 0; col < g.cols; col++)
            {
                float cx = GAP_X + col * STEP_X;
                float cy = -(GAP_Y + row * STEP_Y);

                var cellGO = new GameObject($"Cell_{col}_{row}");
                cellGO.transform.SetParent(_gridPanel, false);

                var img = cellGO.AddComponent<Image>();
                img.color = CellEmpty;

                var rt = cellGO.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(cx, cy);
                rt.sizeDelta = new Vector2(CELL_W, CELL_H);

                // Thin border outline
                var ol = cellGO.AddComponent<Outline>();
                ol.effectColor    = new Color(0.2f, 0.3f, 0.5f, 0.4f);
                ol.effectDistance = new Vector2(1f, -1f);

                // Drag/click handler
                var handler = cellGO.AddComponent<BrickCellHandler>();
                handler.editor = this;
                handler.col    = col;
                handler.row    = row;

                _cellGOs[(col, row)]    = cellGO;
                _cellImages[(col, row)] = img;
            }
        }
    }

    /// Colours all cells from _data.bricks. Shows multi-brick count + movement/rotation badges.
    private void RefreshGrid()
    {
        if (_data == null) return;

        // Reset all cells to empty colour and clear dynamic overlays
        foreach (var kvp in _cellImages)
        {
            kvp.Value.color = CellEmpty;
            ClearCellOverlays(kvp.Value.transform);
        }

        // Group bricks by cell
        var cellGroups = new Dictionary<(int col, int row), List<BrickEntryData>>();
        foreach (var b in _data.bricks)
        {
            var key = (b.col, b.row);
            if (!cellGroups.TryGetValue(key, out var list))
                cellGroups[key] = list = new List<BrickEntryData>();
            list.Add(b);
        }

        // Paint occupied cells
        foreach (var kvp in cellGroups)
        {
            if (!_cellImages.TryGetValue(kvp.Key, out var img)) continue;
            var bricks = kvp.Value;

            // Use selected brick's color if this is the selected cell, else first brick
            BrickEntryData primary = (_selected != null
                && _selectedCol == kvp.Key.col && _selectedRow == kvp.Key.row)
                ? _selected : bricks[0];

            Color c = LevelEditorBrowserUI.TemplateColors.TryGetValue(
                          primary.templateId?.ToLower() ?? "", out var tc)
                      ? tc : new Color(0.7f, 0.7f, 0.7f);

            if (!string.IsNullOrEmpty(primary.tint))
                ColorUtility.TryParseHtmlString(primary.tint, out c);

            img.color = c;

            // Powerup dot for primary brick
            AddPowerupDot(img, primary.powerupId);

            // Count badge if multiple bricks share this cell
            if (bricks.Count > 1)
                AddCountBadge(img, bricks.Count);

            // Movement / rotation badges
            bool anyMov = bricks.Any(b => b.movement != null);
            bool anyRot = bricks.Any(b => b.rotation != null);
            if (anyMov || anyRot)
                AddMovRotBadge(img, anyMov && anyRot ? "MR" : anyMov ? "M" : "R");
        }

        // Re-apply selection highlight
        if (_selectedCol >= 0 && _cellImages.TryGetValue((_selectedCol, _selectedRow), out var selImg))
            selImg.color = Color.Lerp(selImg.color, Color.white, 0.4f);
    }

    private static void ClearCellOverlays(Transform cell)
    {
        for (int i = cell.childCount - 1; i >= 0; i--)
            Destroy(cell.GetChild(i).gameObject);
    }

    private static void AddPowerupDot(Image cellImg, string powerupId)
    {
        if (string.IsNullOrEmpty(powerupId)) return;
        var dot = new GameObject("PuDot");
        dot.transform.SetParent(cellImg.transform, false);
        var dImg = dot.AddComponent<Image>();
        dImg.color = UIStyle.AccentGold;
        var dRt = dImg.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(1f, 1f); dRt.anchorMax = new Vector2(1f, 1f);
        dRt.pivot = new Vector2(1f, 1f);
        dRt.anchoredPosition = new Vector2(-2f, -2f);
        dRt.sizeDelta = new Vector2(8f, 8f);
    }

    private static void AddCountBadge(Image cellImg, int count)
    {
        var go = new GameObject("CntBadge");
        go.transform.SetParent(cellImg.transform, false);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        var rt = bg.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(2f, 2f);
        rt.sizeDelta = new Vector2(22f, 14f);

        var txtGO = new GameObject("T");
        txtGO.transform.SetParent(go.transform, false);
        var t = txtGO.AddComponent<Text>();
        t.text = "×" + count;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 10; t.fontStyle = FontStyle.Bold;
        t.color = UIStyle.AccentGold; t.alignment = TextAnchor.MiddleCenter;
        var tRt = t.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;
    }

    private static void AddMovRotBadge(Image cellImg, string badge)
    {
        var go = new GameObject("MovRotBadge");
        go.transform.SetParent(cellImg.transform, false);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);
        var rt = bg.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-2f, 2f);
        rt.sizeDelta = new Vector2(badge.Length > 1 ? 22f : 14f, 14f);

        var txtGO = new GameObject("T");
        txtGO.transform.SetParent(go.transform, false);
        var t = txtGO.AddComponent<Text>();
        t.text = badge;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 10; t.fontStyle = FontStyle.Bold;
        t.color = new Color(0.3f, 0.85f, 1f);
        t.alignment = TextAnchor.MiddleCenter;
        var tRt = t.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;
    }

    private void RebuildGrid()
    {
        // Apply grid config from top-bar fields to _data
        if (_data == null) return;
        var g = _data.grid ?? (_data.grid = new GridConfig());
        if (int.TryParse(_fieldCols.text, out int cols))   g.cols   = Mathf.Clamp(cols, 1, 30);
        if (int.TryParse(_fieldRows.text, out int rows))   g.rows   = Mathf.Clamp(rows, 1, 30);
        if (float.TryParse(_fieldBrickW.text, out float bw)) g.brickWidth  = Mathf.Max(0.1f, bw);
        if (float.TryParse(_fieldBrickH.text, out float bh)) g.brickHeight = Mathf.Max(0.1f, bh);
        if (float.TryParse(_fieldGapX.text,   out float gx)) g.gapX = Mathf.Max(0f, gx);
        if (float.TryParse(_fieldGapY.text,   out float gy)) g.gapY = Mathf.Max(0f, gy);

        // Remove bricks that fall outside the new grid
        _data.bricks.RemoveAll(b => b.col >= g.cols || b.row >= g.rows);

        ClearSelection();
        BuildCells();
        RefreshGrid();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  CELL INTERACTION  (called from BrickCellHandler)
    // ═════════════════════════════════════════════════════════════════════════

    public void OnCellPointerDown(int col, int row)
    {
        if (_isReadOnly) return;
        _mouseDown      = true;
        _pressCol       = col;
        _pressRow       = row;
        _pressScreenPos = Input.mousePosition;
    }

    public void OnCellPointerUp(int col, int row)
    {
        if (_isReadOnly) return;
        if (_isDragging) return; // EndDrag handles it in Update

        _mouseDown = false;
        OnCellClick(col, row);
    }

    private void OnCellClick(int col, int row)
    {
        var bricksAt = FindBricksAt(col, row);
        if (bricksAt.Count > 0)
        {
            int idx = 0;
            if (col == _selectedCol && row == _selectedRow && _selected != null)
            {
                int prev = bricksAt.IndexOf(_selected);
                idx = (prev >= 0) ? (prev + 1) % bricksAt.Count : 0;
            }
            SelectBrick(bricksAt[idx], col, row);
        }
        else
        {
            // Paint mode: place active template
            PlaceBrick(col, row);
        }
    }

    // ── Selection ─────────────────────────────────────────────────────────────
    private void SelectBrick(BrickEntryData brick, int col, int row)
    {
        _selected    = brick;
        _selectedCol = col;
        _selectedRow = row;
        PopulateProps(brick);
        RefreshGrid();
    }

    private void ClearSelection()
    {
        _selected    = null;
        _selectedCol = -1; _selectedRow = -1;
        ClearProps();
        RefreshGrid();
    }

    // ── Place / Delete ────────────────────────────────────────────────────────
    private void PlaceBrick(int col, int row)
    {
        // Replace existing brick at this cell if present
        _data.bricks.RemoveAll(b => b.col == col && b.row == row);

        var entry = new BrickEntryData
        {
            col        = col,
            row        = row,
            templateId = _activeTemplate
        };
        _data.bricks.Add(entry);
        SelectBrick(entry, col, row);
        RefreshGrid();
    }

    private void DeleteSelected()
    {
        if (_selected == null) return;
        _data.bricks.Remove(_selected);
        ClearSelection();
        RefreshGrid();
    }

    private BrickEntryData FindBrick(int col, int row)
        => _data?.bricks.FirstOrDefault(b => b.col == col && b.row == row);

    private List<BrickEntryData> FindBricksAt(int col, int row)
        => _data?.bricks.Where(b => b.col == col && b.row == row).ToList()
           ?? new List<BrickEntryData>();

    // ── Drag ──────────────────────────────────────────────────────────────────
    private void StartDrag(int col, int row)
    {
        _isDragging = true;
        _pressCol   = col;
        _pressRow   = row;

        // Create ghost image at mouse position
        _ghost = new GameObject("DragGhost");
        _ghost.transform.SetParent(_rootPanel, false);
        var img = _ghost.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.55f);

        // Match the colour of the dragged cell
        if (_cellImages.TryGetValue((col, row), out var src))
            img.color = new Color(src.color.r, src.color.g, src.color.b, 0.65f);

        var rt = _ghost.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(CELL_W, CELL_H);
        rt.anchoredPosition = ScreenToCanvas(Input.mousePosition);
    }

    private void UpdateGhost()
    {
        if (_ghost == null) return;
        var rt = _ghost.GetComponent<RectTransform>();
        rt.anchoredPosition = ScreenToCanvas(Input.mousePosition);
    }

    private void EndDrag()
    {
        _isDragging = false;
        _mouseDown  = false;

        if (_ghost != null) { Destroy(_ghost); _ghost = null; }

        // Find target cell from mouse position
        var (tc, tr) = ScreenToCell(Input.mousePosition);

        if (tc < 0 || tr < 0) return; // outside grid

        var brick = FindBrick(_pressCol, _pressRow);
        if (brick == null) return;

        // Check target is empty (or the same cell)
        if (tc == _pressCol && tr == _pressRow) return;

        // Remove any existing brick at target
        _data.bricks.RemoveAll(b => b.col == tc && b.row == tr);

        brick.col = tc;
        brick.row = tr;

        if (_selectedCol == _pressCol && _selectedRow == _pressRow)
        {
            _selectedCol = tc;
            _selectedRow = tr;
        }

        RefreshGrid();
        if (_selected == brick) PopulateProps(brick);
    }

    /// Converts screen position to a (col, row) inside the grid panel, or (-1,-1) if outside.
    private (int col, int row) ScreenToCell(Vector2 screenPos)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _gridPanel, screenPos, _canvas.worldCamera, out Vector2 local))
            return (-1, -1);

        // _gridPanel pivot is top-left: local.x >= 0, local.y <= 0
        int col = Mathf.FloorToInt((local.x - GAP_X * 0.5f) / STEP_X);
        int row = Mathf.FloorToInt((-local.y - GAP_Y * 0.5f) / STEP_Y);

        int maxCol = (_data?.grid?.cols ?? 12) - 1;
        int maxRow = (_data?.grid?.rows ?? 6)  - 1;

        if (col < 0 || col > maxCol || row < 0 || row > maxRow) return (-1, -1);
        return (col, row);
    }

    private Vector2 ScreenToCanvas(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootPanel as RectTransform, screenPos, _canvas.worldCamera, out Vector2 local);
        return local;
    }

    // ── Template / Powerup selection ──────────────────────────────────────────
    private void SelectTemplate(string templateId)
    {
        _activeTemplate = templateId;
        _activeTemplateLabel.text = "Painting: " + templateId;
    }

    private void AssignPowerupToSelected(string powerupId)
    {
        if (_selected == null) return;
        _selected.powerupId = string.IsNullOrEmpty(powerupId) ? null : powerupId;
        _propPowerup.text   = powerupId ?? "(none)";
        RefreshGrid();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  PROPERTIES PANEL
    // ═════════════════════════════════════════════════════════════════════════

    private void PopulateProps(BrickEntryData b)
    {
        _propCol.text          = b.col.ToString();
        _propRow.text          = b.row.ToString();
        _propTemplate.text     = b.templateId ?? "";
        _propPowerup.text      = b.powerupId  ?? "(none)";
        _propHp.text           = b.hp.HasValue     ? b.hp.Value.ToString()     : "";
        _propPoints.text       = b.points.HasValue ? b.points.Value.ToString() : "";
        _propTint.text         = b.tint            ?? "";
        _propReqColor.text     = b.requiredBallColor ?? "none";
        _propIndestructible.isOn = b.isIndestructible;

        bool hasMov = b.movement != null;
        _propHasMovement.isOn = hasMov;
        _movSection.SetActive(hasMov);
        if (hasMov)
        {
            _propMovType.text   = b.movement.type;
            _propMovAmp.text    = b.movement.amplitude.ToString("F2");
            _propMovPeriod.text = b.movement.period.ToString("F2");
            _propMovPhase.text  = b.movement.phaseOffset.ToString("F2");
        }

        bool hasRot = b.rotation != null;
        _propHasRotation.isOn = hasRot;
        _rotSection.SetActive(hasRot);
        if (hasRot)
        {
            _propRotSpeed.text = b.rotation.speed.ToString("F1");
            _propRotAngle.text = b.rotation.startAngle.ToString("F1");
        }
    }

    private void ClearProps()
    {
        foreach (var f in new[] { _propCol, _propRow, _propTemplate, _propPowerup,
                                   _propHp, _propPoints, _propTint, _propReqColor })
            if (f != null) f.text = "";
        if (_propIndestructible != null) _propIndestructible.isOn = false;
        if (_propHasMovement   != null) _propHasMovement.isOn    = false;
        if (_propHasRotation   != null) _propHasRotation.isOn    = false;
        if (_movSection != null) _movSection.SetActive(false);
        if (_rotSection != null) _rotSection.SetActive(false);
    }

    private void ApplyProps()
    {
        if (_selected == null) return;

        int oldCol = _selected.col, oldRow = _selected.row;

        // Col / Row
        if (int.TryParse(_propCol.text, out int nc)) _selected.col = nc;
        if (int.TryParse(_propRow.text, out int nr)) _selected.row = nr;

        // If position changed, remove any brick that was already at target
        if (_selected.col != oldCol || _selected.row != oldRow)
            _data.bricks.RemoveAll(b => b != _selected && b.col == _selected.col && b.row == _selected.row);

        _selected.templateId = string.IsNullOrWhiteSpace(_propTemplate.text) ? "standard" : _propTemplate.text.Trim();

        string pu = _propPowerup.text?.Trim();
        _selected.powerupId = (string.IsNullOrEmpty(pu) || pu == "(none)") ? null : pu;

        _selected.hp     = int.TryParse(_propHp.text,     out int hp)  ? (int?)hp  : null;
        _selected.points = int.TryParse(_propPoints.text, out int pts) ? (int?)pts : null;

        string tintStr = _propTint.text?.Trim();
        _selected.tint = string.IsNullOrEmpty(tintStr) ? null : tintStr;

        string rc = _propReqColor.text?.Trim().ToLower();
        _selected.requiredBallColor = (string.IsNullOrEmpty(rc) || rc == "none") ? null : rc;

        _selected.isIndestructible = _propIndestructible.isOn;

        // Movement
        if (_propHasMovement.isOn)
        {
            _selected.movement ??= new BrickMovement();
            _selected.movement.type = string.IsNullOrEmpty(_propMovType.text) ? "horizontal" : _propMovType.text.Trim();
            if (float.TryParse(_propMovAmp.text,    out float amp)) _selected.movement.amplitude   = amp;
            if (float.TryParse(_propMovPeriod.text, out float per)) _selected.movement.period      = per;
            if (float.TryParse(_propMovPhase.text,  out float ph))  _selected.movement.phaseOffset = ph;
        }
        else
        {
            _selected.movement = null;
        }

        // Rotation
        if (_propHasRotation.isOn)
        {
            _selected.rotation ??= new BrickRotation();
            if (float.TryParse(_propRotSpeed.text, out float spd)) _selected.rotation.speed      = spd;
            if (float.TryParse(_propRotAngle.text, out float ang)) _selected.rotation.startAngle = ang;
        }
        else
        {
            _selected.rotation = null;
        }

        _selectedCol = _selected.col;
        _selectedRow = _selected.row;
        RefreshGrid();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  SAVE / CANCEL
    // ═════════════════════════════════════════════════════════════════════════

    private void SaveLevel()
    {
        if (_data == null) return;

        // Ensure every saved level has a stable GUID (backward-compat for levels created before GUIDs)
        if (string.IsNullOrEmpty(_data.levelGuid))
            _data.levelGuid = System.Guid.NewGuid().ToString("N");

        ApplyTopBarMetadata(_data);

        var settings = new JsonSerializerSettings
        {
            NullValueHandling    = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Formatting           = Formatting.Indented
        };

        string json = JsonConvert.SerializeObject(_data, settings);

        bool submitToCommunity = _communityToggle != null && _communityToggle.isOn;
        bool wasPublished = CommunityLevelService.Instance?.IsPublished(_data?.levelGuid ?? "") ?? false;

#if UNITY_EDITOR
        string dir = Path.Combine(Application.dataPath, "_Project", "Resources", "Levels");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, _levelId + ".json");
        File.WriteAllText(path, json);
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log($"[LevelEditor] Saved '{_levelId}' → {path}");
#endif

        // Uncheck while published → unpublish from community
        if (wasPublished && !submitToCommunity && CommunityLevelService.Instance != null)
        {
            int serverId = CommunityLevelService.Instance.GetPublishedServerId(_data.levelGuid);
            if (serverId > 0)
                CommunityLevelService.Instance.DeleteLevel(serverId, _ => { });
        }

        if (submitToCommunity)
        {
            // Open the publish dialog before returning to browser
            var publishUI = Object.FindFirstObjectByType<CommunityPublishUI>(FindObjectsInactive.Include);
            if (publishUI != null)
            {
                publishUI.Show(_data, _levelId);
                return; // stay on editor screen until publish dialog closes
            }
        }

        ReturnToBrowser();
    }

    private void ApplyTopBarMetadata(LevelData target)
    {
        if (target == null) return;
        target.displayName = _fieldName.text?.Trim() ?? _levelId;
        if (float.TryParse(_fieldSpeed.text, out float spd)) target.ballSpeed = spd;
        if (_fieldOrder != null)
        {
            if (int.TryParse(_fieldOrder.text?.Trim(), out int ord))
                target.levelOrder = ord;
            else
                target.levelOrder = -1;
        }
    }

    private void TestLevel()
    {
        if (_data == null) return;

        // Commit any pending property edits for the currently selected brick.
        ApplyProps();

        // Test should reflect the current top-bar values without saving to disk.
        var clone = CloneLevelData(_data);
        ApplyTopBarMetadata(clone);

        GameManager.Instance?.StartEditorTestLevel(clone, _levelId, this);
    }

    private static LevelData CloneLevelData(LevelData src)
    {
        if (src == null) return null;

        try
        {
            // Simple deep clone for editor test play (avoids gameplay mutating the editor's live data).
            string json = JsonConvert.SerializeObject(src);
            return JsonConvert.DeserializeObject<LevelData>(json);
        }
        catch
        {
            // Fallback: last resort, use the original reference.
            return src;
        }
    }

    private void ConfirmCancel()
    {
        // For now just go back; a confirmation dialog could be added later
        ReturnToBrowser();
    }

    private void ReturnToBrowser()
    {
        Hide();
        if (_browser != null)
        {
            // Reshow the browser — but don't re-freeze (it's already frozen from
            // when the browser first opened). Bypass Show() to avoid double-freeze.
            _browser.gameObject.SetActive(true);
            _browser.LoadLevelsAndPage();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MULTI-BRICK & PRISM GATE HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    /// Adds a second BrickEntryData at the same (col,row) as the currently selected brick.
    /// This is how overlapping moving-bricks (e.g. circular orbit stacks) are authored.
    private void AddAnotherBrickHere()
    {
        if (_selected == null) return;
        var entry = new BrickEntryData
        {
            col        = _selected.col,
            row        = _selected.row,
            templateId = _activeTemplate
        };
        _data.bricks.Add(entry);
        SelectBrick(entry, entry.col, entry.row);
        RefreshGrid();
    }

    private void AddPrismGate()
    {
        if (_data.prismGates == null) _data.prismGates = new List<PrismGateData>();
        _data.prismGates.Add(new PrismGateData
            { x = 0f, y = 0f, width = 4f, height = 0.35f, postThickness = 0.35f, color = "blue" });
        RefreshGatesPanel();
    }

    private void DeletePrismGate(int idx)
    {
        if (_data?.prismGates == null || idx >= _data.prismGates.Count) return;
        _data.prismGates.RemoveAt(idx);
        RefreshGatesPanel();
    }

    private void RefreshGatesPanel()
    {
        if (_gatesList == null) return;

        // Destroy previous rows
        for (int i = _gatesList.childCount - 1; i >= 0; i--)
            Destroy(_gatesList.GetChild(i).gameObject);

        if (_data?.prismGates == null || _data.prismGates.Count == 0)
        {
            var glRt0 = _gatesList.GetComponent<RectTransform>();
            if (glRt0 != null) glRt0.sizeDelta = Vector2.zero;
            if (_propsContentRt != null) _propsContentRt.sizeDelta = new Vector2(0f, _propsBaseContentH);
            return;
        }

        float gy = 0f;
        for (int i = 0; i < _data.prismGates.Count; i++)
            BuildGateRow(i, ref gy);

        float listH = -gy + 4f;
        var glRt = _gatesList.GetComponent<RectTransform>();
        if (glRt != null) glRt.sizeDelta = new Vector2(0f, listH);
        if (_propsContentRt != null)
            _propsContentRt.sizeDelta = new Vector2(0f, _propsBaseContentH + listH);
    }

    private void BuildGateRow(int idx, ref float gy)
    {
        const float ROW_H = 24f;
        const float GAP   = 4f;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var gate = _data.prismGates[idx];

        gy -= 4f; // small gap between rows

        // Header: "Gate N (color)"  +  [× Delete]
        var hdrGO = new GameObject($"GateHdr_{idx}");
        hdrGO.transform.SetParent(_gatesList, false);
        var hdrTxt = hdrGO.AddComponent<Text>();
        hdrTxt.text      = $"Gate {idx + 1}  ({gate.color})";
        hdrTxt.font      = font;
        hdrTxt.fontSize  = 13;
        hdrTxt.fontStyle = FontStyle.Bold;
        hdrTxt.color     = GateDisplayColor(gate.color);
        hdrTxt.alignment = TextAnchor.MiddleLeft;
        var hdrRt = hdrGO.GetComponent<RectTransform>();
        hdrRt.anchorMin = new Vector2(0f, 1f); hdrRt.anchorMax = new Vector2(0f, 1f);
        hdrRt.pivot = new Vector2(0f, 1f);
        hdrRt.anchoredPosition = new Vector2(6f, gy);
        hdrRt.sizeDelta = new Vector2(RIGHT_W - 52f, ROW_H);

        int capturedIdx = idx;
        var delBtn = UIStyle.CreateButton(_gatesList, "✕",
            Vector2.zero, new Vector2(30f, ROW_H),
            () => DeletePrismGate(capturedIdx), UIStyle.AccentRed);
        var delRt = delBtn.GetComponent<RectTransform>();
        delRt.anchorMin = new Vector2(0f, 1f); delRt.anchorMax = new Vector2(0f, 1f);
        delRt.pivot = new Vector2(0f, 1f);
        delRt.anchoredPosition = new Vector2(RIGHT_W - 40f, gy);
        delRt.sizeDelta = new Vector2(30f, ROW_H);
        gy -= ROW_H + GAP;

        // Row 1: Color [72] | X [50] | Y [50]
        var fColor = MakeGateInlineField(_gatesList, gate.color,
            new Vector2(4f, gy), 72f, ROW_H);
        fColor.onEndEdit.AddListener(v =>
            { if (capturedIdx < _data.prismGates.Count) _data.prismGates[capturedIdx].color = v.Trim(); });

        var fX = MakeGateInlineField(_gatesList, gate.x.ToString("F1"),
            new Vector2(80f, gy), 50f, ROW_H);
        fX.onEndEdit.AddListener(v =>
            { if (capturedIdx < _data.prismGates.Count && float.TryParse(v, out float val)) _data.prismGates[capturedIdx].x = val; });

        var fY = MakeGateInlineField(_gatesList, gate.y.ToString("F1"),
            new Vector2(134f, gy), 50f, ROW_H);
        fY.onEndEdit.AddListener(v =>
            { if (capturedIdx < _data.prismGates.Count && float.TryParse(v, out float val)) _data.prismGates[capturedIdx].y = val; });
        gy -= ROW_H + GAP;

        // Row 2: W [52] | H [52] | Thick [52]
        var fW = MakeGateInlineField(_gatesList, gate.width.ToString("F2"),
            new Vector2(4f, gy), 52f, ROW_H);
        fW.onEndEdit.AddListener(v =>
            { if (capturedIdx < _data.prismGates.Count && float.TryParse(v, out float val)) _data.prismGates[capturedIdx].width = val; });

        var fH = MakeGateInlineField(_gatesList, gate.height.ToString("F2"),
            new Vector2(60f, gy), 52f, ROW_H);
        fH.onEndEdit.AddListener(v =>
            { if (capturedIdx < _data.prismGates.Count && float.TryParse(v, out float val)) _data.prismGates[capturedIdx].height = val; });

        var fT = MakeGateInlineField(_gatesList, gate.postThickness.ToString("F2"),
            new Vector2(116f, gy), 52f, ROW_H);
        fT.onEndEdit.AddListener(v =>
            { if (capturedIdx < _data.prismGates.Count && float.TryParse(v, out float val)) _data.prismGates[capturedIdx].postThickness = val; });
        gy -= ROW_H + 8f;
    }

    private static InputField MakeGateInlineField(Transform parent, string value,
                                                  Vector2 localPos, float w, float h)
    {
        var go = new GameObject("GFld");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.07f, 0.09f, 0.19f);
        go.AddComponent<Outline>().effectColor = new Color(0.25f, 0.35f, 0.55f, 0.4f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = localPos;
        rt.sizeDelta = new Vector2(w, h);
        var f = AttachInputField(go, "", img);
        f.text = value;
        return f;
    }

    private static Color GateDisplayColor(string colorName)
    {
        switch (colorName?.ToLower())
        {
            case "red":    return new Color(1f, 0.4f, 0.4f);
            case "green":  return new Color(0.3f, 0.9f, 0.4f);
            case "yellow": return new Color(1f, 0.9f, 0.2f);
            case "blue":   return new Color(0.3f, 0.65f, 1f);
            default:       return new Color(0.85f, 0.85f, 0.92f);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  UGUI FACTORY HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    private static InputField CreateInputField(Transform parent, string placeholder,
                                               Vector2 pos, Vector2 size,
                                               bool topLeft = false)
    {
        var go  = new GameObject("Field_" + placeholder);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.06f, 0.08f, 0.18f);
        go.AddComponent<Outline>().effectColor = new Color(0.25f, 0.35f, 0.60f, 0.5f);

        var rt = go.GetComponent<RectTransform>();
        if (topLeft)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(8f, pos.y);
            rt.sizeDelta = size;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        return AttachInputField(go, placeholder, img);
    }

    private static InputField CreateInputFieldLocal(Transform parent, string placeholder,
                                                    Vector2 pos, Vector2 size)
    {
        var go  = new GameObject("Field_" + placeholder);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.06f, 0.08f, 0.18f);
        go.AddComponent<Outline>().effectColor = new Color(0.25f, 0.35f, 0.60f, 0.5f);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, pos.y);
        rt.sizeDelta = new Vector2(0f, size.y);

        return AttachInputField(go, placeholder, img);
    }

    private static InputField AttachInputField(GameObject go, string placeholder, Image bgImg)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var phGO  = new GameObject("Placeholder");
        phGO.transform.SetParent(go.transform, false);
        var phTxt = phGO.AddComponent<Text>();
        phTxt.text = placeholder; phTxt.font = font; phTxt.fontSize = 14;
        phTxt.color = new Color(0.40f, 0.45f, 0.55f); phTxt.fontStyle = FontStyle.Italic;
        phTxt.alignment = TextAnchor.MiddleLeft;
        FullStretch(phGO, 6f, 2f, -6f, -2f);

        var txtGO  = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.font = font; txt.fontSize = 14; txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;
        FullStretch(txtGO, 6f, 2f, -6f, -2f);

        var field = go.AddComponent<InputField>();
        field.textComponent = txt;
        field.placeholder   = phTxt;
        field.targetGraphic = bgImg;
        return field;
    }

    private Toggle CreateToggle(Transform parent, string label, Vector2 pos, bool topLeft = false)
    {
        var go = new GameObject("Toggle_" + label);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        if (topLeft)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pos.x, pos.y);
            rt.sizeDelta = new Vector2(0f, 24f);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(200f, 24f);
        }

        // Background box
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(go.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.10f, 0.20f);
        bgGO.AddComponent<Outline>().effectColor = new Color(0.3f, 0.4f, 0.6f, 0.6f);
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0f, 0.5f);
        bgRt.pivot = new Vector2(0f, 0.5f);
        bgRt.anchoredPosition = new Vector2(8f, 0f);
        bgRt.sizeDelta = new Vector2(20f, 20f);

        // Checkmark
        var ckGO  = new GameObject("Checkmark");
        ckGO.transform.SetParent(bgGO.transform, false);
        var ckImg = ckGO.AddComponent<Image>();
        ckImg.color = UIStyle.AccentGreen;
        var ckRt = ckGO.GetComponent<RectTransform>();
        ckRt.anchorMin = Vector2.zero; ckRt.anchorMax = Vector2.one;
        ckRt.offsetMin = new Vector2(3f, 3f); ckRt.offsetMax = new Vector2(-3f, -3f);

        // Label
        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lbl = lblGO.AddComponent<Text>();
        lbl.text = label; lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.fontSize = 13; lbl.color = new Color(0.75f, 0.80f, 0.90f);
        lbl.alignment = TextAnchor.MiddleLeft;
        var lblRt = lbl.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = new Vector2(34f, 0f); lblRt.offsetMax = Vector2.zero;

        var toggle = go.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic       = ckImg;
        toggle.isOn          = false;
        return toggle;
    }

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
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void FullStretch(GameObject go, float l, float b, float r, float t)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(r, t);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Cell pointer-event receiver (separate class in same file)
// ─────────────────────────────────────────────────────────────────────────────
public class BrickCellHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public LevelEditorUI editor;
    public int col, row;

    public void OnPointerDown(PointerEventData e) => editor?.OnCellPointerDown(col, row);
    public void OnPointerUp(PointerEventData e)   => editor?.OnCellPointerUp(col, row);
}
