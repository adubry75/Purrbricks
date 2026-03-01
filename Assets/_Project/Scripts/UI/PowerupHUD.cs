using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Right-side HUD sidebar (sortingOrder 50).
/// Left 80px column: scrollable inventory (always visible).
/// Right 230px column: active powerup slots (always visible).
/// Both panels show simultaneously — no tab toggling.
/// Hidden on MainMenu; shown when gameplay starts via SetVisible(bool).
/// </summary>
public class PowerupHUD : MonoBehaviour
{
    public static PowerupHUD Instance { get; private set; }

    // ── Header palette ────────────────────────────────────────────────────────
    private static readonly Color HeaderBg  = new Color(0.10f, 0.25f, 0.50f, 0.90f);
    private static readonly Color HeaderTxt = new Color(0.102f, 0.251f, 0.502f); // #1A4080

    // ── Root refs ─────────────────────────────────────────────────────────────
    private Canvas        _canvas;
    private GameObject    _sidebar;       // container for ALL sidebar elements; SetActive = SetVisible
    private RectTransform _listRoot;      // active powerups VLG content
    private RectTransform _invRoot;       // inventory VLG content (inside scroll view)
    private GameObject    _invScrollGO;   // scroll view container

    // ── Powerup colour / label tables ─────────────────────────────────────────
    private static readonly Color[] TypeColors =
    {
        new Color(0.30f, 0.60f, 1.00f),  // 0  WidePaddle
        new Color(1.00f, 0.40f, 0.00f),  // 1  MultiBall
        new Color(0.60f, 0.00f, 1.00f),  // 2  StickyBall
        new Color(1.00f, 0.85f, 0.00f),  // 3  SpeedBall
        new Color(0.10f, 1.00f, 0.30f),  // 4  ExtraLife
        new Color(1.00f, 0.10f, 0.30f),  // 5  Laser
        new Color(1.00f, 0.45f, 0.00f),  // 6  Fireball
        new Color(0.90f, 0.20f, 0.90f),  // 7  BombBrick
        new Color(0.00f, 0.90f, 1.00f),  // 8  ShieldWall
        new Color(0.60f, 0.85f, 1.00f),  // 9  BigBall
        new Color(1.00f, 0.85f, 0.00f),  // 10 ScoreFrenzy
        new Color(0.90f, 0.20f, 0.20f),  // 11 ShrinkPaddle
        new Color(0.40f, 0.90f, 0.10f),  // 12 ZipBall
        new Color(0.65f, 0.10f, 0.80f),  // 13 FlipControls
        new Color(0.20f, 0.75f, 0.35f),  // 14 CursedBall
        new Color(1.00f, 0.25f, 0.50f),  // 15 TinyBall
        new Color(0.55f, 0.55f, 0.60f),  // 16 InvisiBall
        new Color(1.00f, 0.55f, 0.00f),  // 17 DrunkenPaddle
        new Color(0.60f, 0.00f, 1.00f),  // 18 PermanentStickyBall
        new Color(0.20f, 0.25f, 0.65f),  // 19 DrunkVision
        new Color(0.12f, 0.60f, 0.65f),  // 20 GremlinBounces
        new Color(0.80f, 0.10f, 0.10f),  // 21 FlipScreen
    };

    private static readonly string[] TypeLabels =
    {
        "WIDE PADDLE",   // 0
        "MULTI-BALL",    // 1
        "STICKY BALL",   // 2
        "SPEED BALL",    // 3
        "+ LIFE",        // 4
        "LASER",         // 5
        "FIREBALL",      // 6
        "BOMB BRICK",    // 7
        "SHIELD",        // 8
        "BIG BALL",      // 9
        "SCORE FRENZY",  // 10
        "⚠ SHRINK",      // 11
        "⚠ ZIP BALL",    // 12
        "⚠ FLIP CTRL",   // 13
        "⚠ CURSED",      // 14
        "⚠ TINY BALL",   // 15
        "⚠ INVISIBALL",  // 16
        "⚠ DRUNK PAD",   // 17
        "STICKY ∞",      // 18
        "⚠ DRUNK VIS",   // 19
        "⚠ GREMLIN",     // 20
        "⚠ FLIP SCR",    // 21
    };

    private static bool IsBad(PowerupType t) => PowerupRules.IsBad(t);

    // ── Slot data types ───────────────────────────────────────────────────────

    private class Slot
    {
        public GameObject root;
        public Image      timerBar;
        public TMP_Text   timerText;
    }

    private class InvSlot
    {
        public GameObject root;
        public TMP_Text   qtyText;
    }

    private readonly Dictionary<PowerupType, Slot>    _slots    = new Dictionary<PowerupType, Slot>();
    private readonly Dictionary<PowerupType, InvSlot> _invSlots = new Dictionary<PowerupType, InvSlot>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCanvas();
    }

    private void Start()
    {
        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.OnPowerupsChanged += Refresh;
            PowerupManager.Instance.OnInventoryDrop   += OnInventoryDrop;
        }
        if (PurrBucksManager.Instance != null)
        {
            PurrBucksManager.Instance.OnInventoryChanged += RefreshInventory;
            RefreshInventory(); // populate with any existing inventory
        }
    }

    private void OnDestroy()
    {
        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.OnPowerupsChanged -= Refresh;
            PowerupManager.Instance.OnInventoryDrop   -= OnInventoryDrop;
        }
        if (PurrBucksManager.Instance != null)
            PurrBucksManager.Instance.OnInventoryChanged -= RefreshInventory;
    }

    private void Update()
    {
        if (PowerupManager.Instance != null)
        {
            foreach (var kvp in _slots)
            {
                float remaining = PowerupManager.Instance.GetRemaining(kvp.Key);
                if (float.IsInfinity(remaining))
                {
                    if (kvp.Value.timerBar  != null) kvp.Value.timerBar.fillAmount = 1f;
                    if (kvp.Value.timerText != null) kvp.Value.timerText.text = "∞";
                    continue;
                }
                float fraction = remaining / PowerupManager.POWERUP_DURATION;
                if (kvp.Value.timerBar  != null) kvp.Value.timerBar.fillAmount = Mathf.Clamp01(fraction);
                if (kvp.Value.timerText != null) kvp.Value.timerText.text = Mathf.CeilToInt(remaining).ToString();
            }
        }

        // Auto-show cursor near right sidebar
        bool nearSidebar = Input.mousePosition.x > Screen.width - 220f;
        if (nearSidebar && GameManager.Instance != null &&
            (GameManager.Instance.State == GameState.Playing || GameManager.Instance.State == GameState.Ready))
        {
            Cursor.visible = true;
        }
    }

    // ── Sidebar visibility ────────────────────────────────────────────────────

    /// <summary>Show or hide the entire sidebar. Called by GameManager on state changes.</summary>
    public void SetVisible(bool visible)
    {
        if (_sidebar != null) _sidebar.SetActive(visible);
    }

    // ── Active powerups column ────────────────────────────────────────────────

    private void Refresh()
    {
        if (PowerupManager.Instance == null) return;
        var active = PowerupManager.Instance.GetAllTimers();

        foreach (var kvp in active)
            if (!_slots.ContainsKey(kvp.Key)) AddSlot(kvp.Key);

        var remove = new List<PowerupType>();
        foreach (var kvp in _slots)
            if (!active.ContainsKey(kvp.Key)) remove.Add(kvp.Key);
        foreach (var t in remove) { if (_slots[t].root != null) Destroy(_slots[t].root); _slots.Remove(t); }
    }

    private void AddSlot(PowerupType type)
    {
        int    idx   = Mathf.Clamp((int)type, 0, TypeColors.Length - 1);
        Color  color = TypeColors[idx];
        string label = idx < TypeLabels.Length ? TypeLabels[idx] : type.ToString().ToUpper();
        bool   bad   = IsBad(type);

        var slotGO = new GameObject($"Slot_{type}");
        slotGO.transform.SetParent(_listRoot, false);

        slotGO.AddComponent<RectTransform>().sizeDelta = new Vector2(220f, 56f);

        var le = slotGO.AddComponent<LayoutElement>();
        le.minHeight       = 56f;
        le.preferredHeight = 56f;
        le.flexibleHeight  = 0f;

        // Background
        var bg = slotGO.AddComponent<Image>();
        bg.color = bad ? new Color(0.25f, 0f, 0f, 0.75f) : new Color(0f, 0f, 0f, 0.65f);

        // Name label — TMP with AutoSize
        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(slotGO.transform, false);
        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text             = label;
        lbl.enableAutoSizing = true;
        lbl.fontSizeMin      = 8;
        lbl.fontSizeMax      = 13;
        lbl.color            = color;
        lbl.alignment        = TextAlignmentOptions.MidlineLeft;
        lbl.raycastTarget    = false;
        lbl.enableWordWrapping = false;
        lbl.overflowMode     = TextOverflowModes.Ellipsis;
        var lblRt = lbl.GetComponent<RectTransform>();
        lblRt.anchorMin = new Vector2(0f, 0.5f);
        lblRt.anchorMax = new Vector2(1f, 1f);
        lblRt.offsetMin = new Vector2(6f, 0f);
        lblRt.offsetMax = new Vector2(-40f, 0f);

        // Timer countdown — TMP, top-right
        var timerGO = new GameObject("TimerText");
        timerGO.transform.SetParent(slotGO.transform, false);
        var timerTxt = timerGO.AddComponent<TextMeshProUGUI>();
        timerTxt.text          = "10";
        timerTxt.fontSize      = 20;
        timerTxt.fontStyle     = FontStyles.Bold;
        timerTxt.color         = Color.white;
        timerTxt.alignment     = TextAlignmentOptions.TopRight;
        timerTxt.raycastTarget = false;
        var timerRt = timerTxt.GetComponent<RectTransform>();
        timerRt.anchorMin = new Vector2(0f, 0.5f);
        timerRt.anchorMax = new Vector2(1f, 1f);
        timerRt.offsetMin = new Vector2(0f, 0f);
        timerRt.offsetMax = new Vector2(-4f, 0f);

        // Glow behind the bar area
        var glow = MakeImage(slotGO.transform, "Glow",
            new Vector2(0f, 0f), new Vector2(1f, 0.50f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Color(color.r, color.g, color.b, 0.12f));
        var glowRt = glow.GetComponent<RectTransform>();
        glowRt.offsetMin = new Vector2(6f, 2f);
        glowRt.offsetMax = new Vector2(-2f, 4f);

        // Timer track
        var trackGO = new GameObject("Track");
        trackGO.transform.SetParent(slotGO.transform, false);
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = new Color(1f, 1f, 1f, 0.12f);
        var trackRt = trackGO.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0f, 0f);
        trackRt.anchorMax = new Vector2(1f, 0.48f);
        trackRt.offsetMin = new Vector2(10f, 4f);
        trackRt.offsetMax = new Vector2(-4f, 0f);

        // Timer fill
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(trackGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color      = color;
        fillImg.type       = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;
        var fillRt = fillImg.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta  = Vector2.zero;

        // Bottom separator
        var sepGO = new GameObject("Sep");
        sepGO.transform.SetParent(slotGO.transform, false);
        var sepRt = sepGO.AddComponent<RectTransform>();
        sepRt.anchorMin        = Vector2.zero;
        sepRt.anchorMax        = new Vector2(1f, 0f);
        sepRt.pivot            = new Vector2(0.5f, 0f);
        sepRt.sizeDelta        = new Vector2(0f, 1f);
        sepRt.anchoredPosition = Vector2.zero;
        var sepImg = sepGO.AddComponent<Image>();
        sepImg.color         = new Color(1f, 1f, 1f, 0.08f);
        sepImg.raycastTarget = false;

        _slots[type] = new Slot { root = slotGO, timerBar = fillImg, timerText = timerTxt };
    }

    // ── Inventory column ──────────────────────────────────────────────────────

    private void RefreshInventory()
    {
        if (PurrBucksManager.Instance == null) return;
        var inv = PurrBucksManager.Instance.GetAllInventory();

        foreach (var kvp in inv)
        {
            if (_invSlots.ContainsKey(kvp.Key))
            {
                if (_invSlots[kvp.Key].qtyText != null)
                    _invSlots[kvp.Key].qtyText.text = $"×{kvp.Value}";
            }
            else
            {
                AddInvSlot(kvp.Key, kvp.Value);
            }
        }

        var remove = new List<PowerupType>();
        foreach (var kvp in _invSlots)
            if (!inv.ContainsKey(kvp.Key)) remove.Add(kvp.Key);
        foreach (var t in remove) { if (_invSlots[t].root != null) Destroy(_invSlots[t].root); _invSlots.Remove(t); }
    }

    private void AddInvSlot(PowerupType type, int qty)
    {
        int   idx   = Mathf.Clamp((int)type, 0, TypeColors.Length - 1);
        Color color = TypeColors[idx];

        var slotGO = new GameObject($"InvSlot_{type}");
        slotGO.transform.SetParent(_invRoot, false);

        slotGO.AddComponent<RectTransform>(); // VLG controls actual size

        var le = slotGO.AddComponent<LayoutElement>();
        le.minHeight       = 80f;
        le.preferredHeight = 80f;
        le.flexibleHeight  = 0f;

        // Outer Image = colored 1px border
        var borderImg = slotGO.AddComponent<Image>();
        borderImg.color         = new Color(color.r, color.g, color.b, 0.55f);
        borderImg.raycastTarget = false;

        // Inner dark background (1px inset from the border)
        var innerBgGO = new GameObject("Bg");
        innerBgGO.transform.SetParent(slotGO.transform, false);
        var innerBg = innerBgGO.AddComponent<Image>();
        innerBg.color = new Color(0f, 0f, 0f, 0.80f);
        var innerRt = innerBg.GetComponent<RectTransform>();
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = new Vector2(1f, 1f);
        innerRt.offsetMax = new Vector2(-1f, -1f);

        // Button — targetGraphic = inner bg so hover/press tints the interior
        var btn = slotGO.AddComponent<Button>();
        btn.targetGraphic = innerBg;
        var cols = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        cols.pressedColor     = new Color(0.75f, 0.75f, 0.75f);
        btn.colors = cols;
        var capturedType = type;
        btn.onClick.AddListener(() => PurrBucksManager.Instance?.TryUseFromInventory(capturedType));

        // Powerup icon (same sprite that drops from bricks)
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(slotGO.transform, false);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget  = false;
        var sprite = PowerupIconRegistry.Instance?.GetIcon(type);
        if (sprite != null)
        {
            iconImg.sprite = sprite;
            iconImg.color  = Color.white;
        }
        else
        {
            iconImg.color = new Color(color.r, color.g, color.b, 0.65f);
        }
        var iconRt = iconImg.GetComponent<RectTransform>();
        iconRt.anchorMin        = new Vector2(0.08f, 0.28f);
        iconRt.anchorMax        = new Vector2(0.92f, 0.97f);
        iconRt.sizeDelta        = Vector2.zero;
        iconRt.anchoredPosition = Vector2.zero;

        // Qty badge — bottom strip, centered, gold bold
        var badgeGO = new GameObject("Qty");
        badgeGO.transform.SetParent(slotGO.transform, false);
        var badge = badgeGO.AddComponent<TextMeshProUGUI>();
        badge.text          = $"×{qty}";
        badge.fontSize      = 13;
        badge.fontStyle     = FontStyles.Bold;
        badge.alignment     = TextAlignmentOptions.Center;
        badge.color         = new Color(1f, 0.85f, 0.10f);
        badge.raycastTarget = false;
        var badgeRt = badge.GetComponent<RectTransform>();
        badgeRt.anchorMin        = new Vector2(0f, 0f);
        badgeRt.anchorMax        = new Vector2(1f, 0.30f);
        badgeRt.sizeDelta        = Vector2.zero;
        badgeRt.anchoredPosition = Vector2.zero;

        _invSlots[type] = new InvSlot { root = slotGO, qtyText = badge };
    }

    // ── Inventory drop VFX ────────────────────────────────────────────────────

    private void OnInventoryDrop(PowerupType type)
    {
        StartCoroutine(InventoryDropFlyIn(type));
        RefreshInventory();
    }

    private IEnumerator InventoryDropFlyIn(PowerupType type)
    {
        int    idx   = Mathf.Clamp((int)type, 0, TypeColors.Length - 1);
        Color  color = TypeColors[idx];
        string label = idx < TypeLabels.Length ? TypeLabels[idx] : type.ToString();

        var go = new GameObject("InvDropFlyIn");
        go.transform.SetParent(transform, false); // attach to canvas root, not sidebar

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0.5f);
        rt.anchorMax        = new Vector2(1f, 0.5f);
        rt.pivot            = new Vector2(1f, 0.5f);
        rt.sizeDelta        = new Vector2(220f, 36f);
        rt.anchoredPosition = new Vector2(-5f, 0f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);
        bg.raycastTarget = false;

        var txtGO = new GameObject("Txt");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text          = $"{label}  → INV";
        txt.fontSize      = 14;
        txt.fontStyle     = FontStyles.Bold;
        txt.alignment     = TextAlignmentOptions.MidlineRight;
        txt.color         = color;
        txt.raycastTarget = false;
        var txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(4f, 0f);
        txtRt.offsetMax = new Vector2(-8f, 0f);

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos   = startPos + new Vector2(-30f, 0f);
        float t = 0f;
        while (t < 0.25f) { t += Time.unscaledDeltaTime; cg.alpha = Mathf.Clamp01(t / 0.25f); rt.anchoredPosition = Vector2.Lerp(startPos, endPos, cg.alpha); yield return null; }

        yield return new WaitForSecondsRealtime(1.0f);

        Vector2 exitPos = endPos + new Vector2(40f, 0f);
        t = 0f;
        while (t < 0.45f) { t += Time.unscaledDeltaTime; float p = Mathf.Clamp01(t / 0.45f); cg.alpha = 1f - p; rt.anchoredPosition = Vector2.Lerp(endPos, exitPos, p); yield return null; }

        Destroy(go);
    }

    // ── Canvas builder ────────────────────────────────────────────────────────

    private void BuildCanvas()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // ── Sidebar container — all sidebar elements live here ─────────────────
        // Fills the full canvas so children can use absolute right-anchored positions.
        // SetActive on this GO controls entire sidebar visibility.
        var sidebarGO = new GameObject("Sidebar");
        sidebarGO.transform.SetParent(transform, false);
        var sidebarRt = sidebarGO.AddComponent<RectTransform>();
        sidebarRt.anchorMin        = Vector2.zero;
        sidebarRt.anchorMax        = Vector2.one;
        sidebarRt.sizeDelta        = Vector2.zero;
        sidebarRt.anchoredPosition = Vector2.zero;
        _sidebar = sidebarGO;
        _sidebar.SetActive(false); // hidden until GameManager calls SetVisible(true)

        // ── Sidebar background ────────────────────────────────────────────────
        var panelGO = new GameObject("SidebarPanel");
        panelGO.transform.SetParent(_sidebar.transform, false);
        var panelRt = panelGO.AddComponent<RectTransform>();
        panelRt.anchorMin        = new Vector2(1f, 0f);
        panelRt.anchorMax        = new Vector2(1f, 1f);
        panelRt.pivot            = new Vector2(1f, 0.5f);
        panelRt.sizeDelta        = new Vector2(322f, 0f);
        panelRt.anchoredPosition = Vector2.zero;
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.04f, 0.06f, 0.13f, 0.90f);
        panelImg.raycastTarget = false;

        // ── ACTIVE column header (static label, not a tab button) ─────────────
        var activeHeaderGO = new GameObject("ActiveHeader");
        activeHeaderGO.transform.SetParent(_sidebar.transform, false);
        var activeHeaderImg = activeHeaderGO.AddComponent<Image>();
        activeHeaderImg.color = HeaderBg;
        activeHeaderImg.raycastTarget = false;
        var activeHeaderRt = activeHeaderGO.GetComponent<RectTransform>();
        activeHeaderRt.anchorMin        = new Vector2(1f, 1f);
        activeHeaderRt.anchorMax        = new Vector2(1f, 1f);
        activeHeaderRt.pivot            = new Vector2(1f, 1f);
        activeHeaderRt.sizeDelta        = new Vector2(233f, 34f);
        activeHeaderRt.anchoredPosition = new Vector2(-5f, -5f);
        var activeLblGO = new GameObject("Label");
        activeLblGO.transform.SetParent(activeHeaderGO.transform, false);
        var activeLbl = activeLblGO.AddComponent<TextMeshProUGUI>();
        activeLbl.text      = "ACTIVE";
        activeLbl.fontSize  = 12;
        activeLbl.fontStyle = FontStyles.Bold;
        activeLbl.alignment = TextAlignmentOptions.Center;
        activeLbl.color     = HeaderTxt;
        activeLbl.raycastTarget = false;
        FillRT(activeLbl.GetComponent<RectTransform>());

        // ── Active Powerup list root — VLG stacks from top ────────────────────
        var listRootGO = new GameObject("SlotList");
        listRootGO.transform.SetParent(_sidebar.transform, false);
        _listRoot = listRootGO.AddComponent<RectTransform>();
        _listRoot.anchorMin        = new Vector2(1f, 1f);
        _listRoot.anchorMax        = new Vector2(1f, 1f);
        _listRoot.pivot            = new Vector2(1f, 1f);
        _listRoot.anchoredPosition = new Vector2(-5f, -42f);
        _listRoot.sizeDelta        = new Vector2(230f, 900f);

        var listVLG = listRootGO.AddComponent<VerticalLayoutGroup>();
        listVLG.childAlignment        = TextAnchor.UpperLeft;
        listVLG.spacing               = 8f;
        listVLG.childForceExpandWidth  = true;
        listVLG.childForceExpandHeight = false;
        listVLG.childControlWidth      = true;
        listVLG.childControlHeight     = true;
        listVLG.padding = new RectOffset(0, 0, 10, 0);

        // ── INV column header (static label, not a tab button) ────────────────
        var invHeaderGO = new GameObject("InvHeader");
        invHeaderGO.transform.SetParent(_sidebar.transform, false);
        var invHeaderImg = invHeaderGO.AddComponent<Image>();
        invHeaderImg.color = HeaderBg;
        invHeaderImg.raycastTarget = false;
        var invHeaderRt = invHeaderGO.GetComponent<RectTransform>();
        invHeaderRt.anchorMin        = new Vector2(1f, 1f);
        invHeaderRt.anchorMax        = new Vector2(1f, 1f);
        invHeaderRt.pivot            = new Vector2(1f, 1f);
        invHeaderRt.sizeDelta        = new Vector2(80f, 34f);
        invHeaderRt.anchoredPosition = new Vector2(-238f, -5f);
        var invLblGO = new GameObject("Label");
        invLblGO.transform.SetParent(invHeaderGO.transform, false);
        var invLbl = invLblGO.AddComponent<TextMeshProUGUI>();
        invLbl.text      = "INV";
        invLbl.fontSize  = 12;
        invLbl.fontStyle = FontStyles.Bold;
        invLbl.alignment = TextAlignmentOptions.Center;
        invLbl.color     = HeaderTxt;
        invLbl.raycastTarget = false;
        FillRT(invLbl.GetComponent<RectTransform>());

        // ── Inventory scroll view ─────────────────────────────────────────────
        // Positioned in the left 80px of the sidebar, top at y=-80 (below TopBar).
        // Scrolls vertically so any number of inventory items can be displayed.
        var scrollGO = new GameObject("InvScrollView");
        scrollGO.transform.SetParent(_sidebar.transform, false);
        var scrollRt = scrollGO.AddComponent<RectTransform>();
        // Anchor to right edge spanning full height, then offset to inv column bounds
        scrollRt.anchorMin = new Vector2(1f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(-318f, 4f);    // left of inv col, small bottom margin
        scrollRt.offsetMax = new Vector2(-238f, -80f);  // right of inv col, top at y=-80
        _invScrollGO = scrollGO;
        // Note: InvScrollView starts active — no tab toggling, both panels always visible

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal        = false;
        scrollRect.vertical          = true;
        scrollRect.scrollSensitivity = 25f;
        scrollRect.movementType      = ScrollRect.MovementType.Clamped;
        scrollRect.inertia           = true;
        scrollRect.decelerationRate  = 0.135f;

        // Viewport — clips the scrolling content
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var viewRt = viewportGO.AddComponent<RectTransform>();
        viewRt.anchorMin        = Vector2.zero;
        viewRt.anchorMax        = Vector2.one;
        viewRt.sizeDelta        = Vector2.zero;
        viewRt.anchoredPosition = Vector2.zero;
        var viewImg = viewportGO.AddComponent<Image>();
        viewImg.color         = Color.white; // must be non-zero alpha for Mask stencil to write
        viewImg.raycastTarget = false;
        var mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // InvList — scrollable content, grows vertically with added items
        var invRootGO = new GameObject("InvList");
        invRootGO.transform.SetParent(viewportGO.transform, false);
        _invRoot = invRootGO.AddComponent<RectTransform>();
        _invRoot.anchorMin        = new Vector2(0f, 1f);
        _invRoot.anchorMax        = new Vector2(1f, 1f);
        _invRoot.pivot            = new Vector2(0.5f, 1f);
        _invRoot.sizeDelta        = new Vector2(0f, 0f); // width fills viewport; height by ContentSizeFitter
        _invRoot.anchoredPosition = Vector2.zero;

        var invVLG = invRootGO.AddComponent<VerticalLayoutGroup>();
        invVLG.childAlignment        = TextAnchor.UpperLeft;
        invVLG.spacing               = 6f;
        invVLG.childForceExpandWidth  = true;
        invVLG.childForceExpandHeight = false;
        invVLG.childControlWidth      = true;
        invVLG.childControlHeight     = true;
        invVLG.padding = new RectOffset(1, 1, 4, 4);

        var csf = invRootGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.content  = _invRoot;
        scrollRect.viewport = viewRt;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static Image MakeImage(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.sizeDelta        = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
        return img;
    }

    private static void FillRT(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.sizeDelta        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
