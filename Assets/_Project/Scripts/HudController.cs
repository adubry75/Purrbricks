using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-building top-bar HUD (sortingOrder 40).
/// Three-zone layout: LeftZone (lives/combo/code), CenterZone (score/level),
/// RightZone (PB balance only).
/// INV / ACTIVE tab switching is handled by PowerupHUD's own sidebar header.
/// All text is TMP; no serialized scene refs required.
/// </summary>
public class HudController : MonoBehaviour
{
    public static HudController Instance { get; private set; }

    // ── Palette ───────────────────────────────────────────────────────────────
    private static readonly Color ColorGold      = new Color(1.00f, 0.85f, 0.10f);
    private static readonly Color ColorCyan      = new Color(0.10f, 0.90f, 1.00f);
    private static readonly Color ColorWhite     = Color.white;
    private static readonly Color ColorGrayLight = new Color(0.75f, 0.75f, 0.80f);
    private static readonly Color ColorNavyBg    = new Color(0.04f, 0.06f, 0.13f, 0.78f);
    private static readonly Color ColorGoldSep   = new Color(1.00f, 0.80f, 0.10f, 0.85f);

    // ── Internal text refs ────────────────────────────────────────────────────
    private TMP_Text _scoreText;
    private TMP_Text _livesText;
    private TMP_Text _comboText;
    private TMP_Text _codeText;
    private TMP_Text _levelText;
    private TMP_Text _centerMessage;
    private TMP_Text _pbText;

    // ── Visibility ────────────────────────────────────────────────────────────
    private GameObject _topBar;
    private bool _isVisible;
    public bool IsVisible => _isVisible;

    // ── Display state ─────────────────────────────────────────────────────────
    private string _levelInfo = "";
    private string _levelCode = "";
    private int    _lastLives;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCanvas();
    }

    private void Start()
    {
        if (PurrBucksManager.Instance != null)
        {
            PurrBucksManager.Instance.OnBalanceChanged += RefreshBalance;
            RefreshBalance();
        }
    }

    private void OnDestroy()
    {
        if (PurrBucksManager.Instance != null)
            PurrBucksManager.Instance.OnBalanceChanged -= RefreshBalance;
    }

    // ── Canvas builder ────────────────────────────────────────────────────────

    private void BuildCanvas()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0f; // match width

        gameObject.AddComponent<GraphicRaycaster>();

        // ── TopBar — spans only the play area (right edge stops at sidebar) ──
        var topBar = new GameObject("TopBar");
        topBar.transform.SetParent(transform, false);
        var topBarImg = topBar.AddComponent<Image>();
        topBarImg.color = ColorNavyBg;
        topBarImg.raycastTarget = false;
        var topBarRt = topBar.GetComponent<RectTransform>();
        topBarRt.anchorMin = new Vector2(0f, 1f);
        topBarRt.anchorMax = new Vector2(1f, 1f);
        topBarRt.pivot     = new Vector2(0.5f, 1f);
        // Set offsets directly — avoids the sizeDelta+anchoredPosition conflict that caused Left=161, Right=161.
        // Inspector will show: Left=0, Right=322, Height=75.
        topBarRt.offsetMin = new Vector2(0f, -75f);   // left=0, bar is 75px tall
        topBarRt.offsetMax = new Vector2(-322f, 0f);  // right edge stops at sidebar left

        _topBar = topBar;
        _topBar.SetActive(false); // hidden by default; GameManager calls SetVisible(true)

        // ContentRow — HLG that fills TopBar (leave 3px at bottom for separator)
        var row = new GameObject("ContentRow");
        row.transform.SetParent(topBar.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = Vector2.zero;
        rowRt.anchorMax = Vector2.one;
        rowRt.offsetMin = new Vector2(8f, 3f);
        rowRt.offsetMax = new Vector2(-8f, 0f);
        var rowHLG = row.AddComponent<HorizontalLayoutGroup>();
        rowHLG.spacing               = 8f;
        rowHLG.childAlignment        = TextAnchor.MiddleLeft;
        rowHLG.childForceExpandWidth  = false;
        rowHLG.childForceExpandHeight = true;
        rowHLG.childControlWidth      = true;
        rowHLG.childControlHeight     = true;

        BuildLeftZone(row.transform);
        // Flexible spacer — pushes RightZone to the right edge
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(row.transform, false);
        spacer.AddComponent<RectTransform>();
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
        BuildRightZone(row.transform);

        // CenterZone is a full-width transparent overlay on the TopBar so the
        // score text is truly centered over the entire play area.
        // No Image background → clicks pass through to LeftZone / RightZone.
        BuildCenterZone(topBar.transform);

        // ── 3px gold separator at bottom of TopBar ────────────────────────────
        var sep = new GameObject("Separator");
        sep.transform.SetParent(topBar.transform, false);
        var sepRt = sep.AddComponent<RectTransform>();
        sepRt.anchorMin        = new Vector2(0f, 0f);
        sepRt.anchorMax        = new Vector2(1f, 0f);
        sepRt.pivot            = new Vector2(0.5f, 0f);
        sepRt.sizeDelta        = new Vector2(0f, 3f);
        sepRt.anchoredPosition = Vector2.zero;
        var sepImg = sep.AddComponent<Image>();
        sepImg.color = ColorGoldSep;
        sepImg.raycastTarget = false;

        // ── Centre-screen message (GET READY etc.) ────────────────────────────
        var cm = new GameObject("CenterMessage");
        cm.transform.SetParent(transform, false);
        var cmRt = cm.AddComponent<RectTransform>();
        cmRt.anchorMin        = new Vector2(0.5f, 0.5f);
        cmRt.anchorMax        = new Vector2(0.5f, 0.5f);
        cmRt.pivot            = new Vector2(0.5f, 0.5f);
        cmRt.sizeDelta        = new Vector2(700f, 90f);
        cmRt.anchoredPosition = Vector2.zero;
        _centerMessage = cm.AddComponent<TextMeshProUGUI>();
        _centerMessage.fontSize  = 38;
        _centerMessage.fontStyle = FontStyles.Bold;
        _centerMessage.color     = ColorWhite;
        _centerMessage.alignment = TextAlignmentOptions.Center;
        cm.SetActive(false);
    }

    // ── Zone builders ─────────────────────────────────────────────────────────

    private void BuildLeftZone(Transform parent)
    {
        var zone = MakeZone(parent, "LeftZone", 310f);

        var vlg = zone.AddComponent<VerticalLayoutGroup>();
        vlg.spacing               = 2f;
        vlg.childAlignment        = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.padding = new RectOffset(2, 2, 5, 4);

        // Row 1 — Lives | Combo side by side
        var row1 = new GameObject("Row1");
        row1.transform.SetParent(zone.transform, false);
        row1.AddComponent<RectTransform>();
        var r1hlg = row1.AddComponent<HorizontalLayoutGroup>();
        r1hlg.spacing               = 14f;
        r1hlg.childAlignment        = TextAnchor.MiddleLeft;
        r1hlg.childForceExpandWidth  = false;
        r1hlg.childForceExpandHeight = true;
        r1hlg.childControlWidth      = true;
        r1hlg.childControlHeight     = true;
        var r1le = row1.AddComponent<LayoutElement>();
        r1le.flexibleHeight = 1f;

        _livesText = MakeTMP(row1.transform, "Lives: 3", 15, ColorWhite, TextAlignmentOptions.MidlineLeft);
        var livLE = _livesText.gameObject.AddComponent<LayoutElement>();
        livLE.preferredWidth = 110f;
        livLE.flexibleWidth  = 0f;

        _comboText = MakeTMP(row1.transform, "×1", 15, ColorGrayLight, TextAlignmentOptions.MidlineLeft);
        var comLE = _comboText.gameObject.AddComponent<LayoutElement>();
        comLE.flexibleWidth = 1f;

        // Row 2 — Level code (hidden until set)
        _codeText = MakeTMP(zone.transform, "", 11, ColorGrayLight, TextAlignmentOptions.MidlineLeft);
        var codeLE = _codeText.gameObject.AddComponent<LayoutElement>();
        codeLE.preferredHeight = 14f;
        _codeText.gameObject.SetActive(false);
    }

    private void BuildCenterZone(Transform parent)
    {
        // Full-width transparent overlay — no Image, no raycast blocking.
        // Fills the entire TopBar so score/level text centers over the play area.
        var zone = new GameObject("CenterZone");
        zone.transform.SetParent(parent, false);
        var zoneRt = zone.AddComponent<RectTransform>();
        zoneRt.anchorMin        = Vector2.zero;
        zoneRt.anchorMax        = Vector2.one;
        zoneRt.sizeDelta        = Vector2.zero;
        zoneRt.anchoredPosition = Vector2.zero;

        var vlg = zone.AddComponent<VerticalLayoutGroup>();
        vlg.spacing               = 1f;
        vlg.childAlignment        = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.padding = new RectOffset(4, 4, 5, 4);

        _scoreText = MakeTMP(zone.transform, "0", 28, ColorGold, TextAlignmentOptions.Center);
        _scoreText.fontStyle = FontStyles.Bold;
        _scoreText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        _levelText = MakeTMP(zone.transform, "LEVEL 1", 12, ColorGrayLight, TextAlignmentOptions.Center);
        _levelText.gameObject.AddComponent<LayoutElement>().preferredHeight = 15f;
    }

    private void BuildRightZone(Transform parent)
    {
        // Slim right zone — just the PB balance badge
        var zone = MakeZone(parent, "RightZone", 120f);

        var hlg = zone.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing               = 0f;
        hlg.childAlignment        = TextAnchor.MiddleRight;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.padding = new RectOffset(0, 4, 6, 6);

        // PB balance badge (click to open Store)
        var pbGO = new GameObject("PBBalance");
        pbGO.transform.SetParent(zone.transform, false);
        pbGO.AddComponent<RectTransform>();
        var pbBg = pbGO.AddComponent<Image>();
        pbBg.color = new Color(0f, 0f, 0f, 0.40f);
        var pbBtn = pbGO.AddComponent<Button>();
        pbBtn.targetGraphic = pbBg;
        ApplyButtonColors(pbBtn);
        pbBtn.onClick.AddListener(() => GameManager.Instance?.ShowStore());
        _pbText = MakeTMP(pbGO.transform, "🐾 0 PB", 12, ColorGold, TextAlignmentOptions.Center);
        _pbText.raycastTarget = false;
        FillRT(_pbText.GetComponent<RectTransform>());
        var pbLE = pbGO.AddComponent<LayoutElement>();
        pbLE.preferredWidth = 110f;
        pbLE.flexibleWidth  = 0f;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameObject MakeZone(Transform parent, string name, float preferredWidth)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        if (preferredWidth < 0f)
        {
            le.flexibleWidth = 1f;
            le.minWidth      = 100f;
        }
        else
        {
            le.preferredWidth = preferredWidth;
            le.flexibleWidth  = 0f;
        }
        return go;
    }

    private static TMP_Text MakeTMP(
    Transform parent,
    string text,
    int fontSize,
    Color color,
    TextAlignmentOptions align
    )
    {
        var go = new GameObject("TMP");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Orbitron-VariableFont_wght SDF");

        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font;          // 👈 THIS is the missing piece
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;

        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;

        return t;
    }

    private static void ApplyButtonColors(Button btn)
    {
        var c = btn.colors;
        c.normalColor      = Color.white;
        c.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        c.pressedColor     = new Color(0.85f, 0.85f, 0.85f);
        c.colorMultiplier  = 1f;
        c.fadeDuration     = 0.06f;
        btn.colors = c;
    }

    private static void FillRT(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Show or hide the TopBar without deactivating the HudController GO itself.</summary>
    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (_topBar != null) _topBar.SetActive(visible);
        if (!visible && _centerMessage != null)
            _centerMessage.gameObject.SetActive(false);
    }

    public void SetScore(int score)
    {
        if (_scoreText != null) _scoreText.text = score.ToString("N0");
    }

    public void SetLives(int lives)
    {
        _lastLives = lives;
        RefreshLivesText();
    }

    public void SetLevelCode(string code)
    {
        _levelCode = code ?? "";
        RefreshLivesText();
    }

    private void RefreshLivesText()
    {
        if (_livesText != null) _livesText.text = $"♥ {_lastLives}";

        if (_codeText != null)
        {
            bool hasCode = !string.IsNullOrEmpty(_levelCode);
            _codeText.text = hasCode ? $"CODE: {_levelCode}" : "";
            _codeText.gameObject.SetActive(hasCode);
        }
    }

    public void SetLevelInfo(int levelNumber, string levelTitle)
    {
        _levelInfo = string.IsNullOrEmpty(levelTitle)
            ? $"LEVEL {levelNumber}"
            : $"LEVEL {levelNumber}  ·  {levelTitle.ToUpper()}";
        if (_levelText != null) _levelText.text = _levelInfo;
    }

    public void SetState(string state) { /* no-op — state shown via ShowCenter/HideCenter */ }
    public void SetStatus(string status) => SetState(status);
    public void SetLevel(int levelNumber) => SetLevelInfo(levelNumber, "");

    public void ShowCenter(string message)
    {
        if (_centerMessage == null) return;
        _centerMessage.gameObject.SetActive(true);
        _centerMessage.text = message;
    }

    public void HideCenter()
    {
        if (_centerMessage != null)
            _centerMessage.gameObject.SetActive(false);
    }

    public void SetCombo(int combo)
    {
        if (_comboText == null) return;
        int mult = 1 + combo;
        if (mult <= 1)
        {
            _comboText.text     = "×1";
            _comboText.fontSize = 14;
            _comboText.color    = ColorGrayLight;
        }
        else
        {
            _comboText.text     = $"×{mult}";
            _comboText.fontSize = mult >= 8 ? 22 : mult >= 4 ? 19 : 16;
            _comboText.color    = ColorCyan;
        }
    }

    public void RefreshBalance()
    {
        if (_pbText != null && PurrBucksManager.Instance != null)
            _pbText.text = $"🐾 {PurrBucksManager.Instance.Balance} PB";
    }
}
