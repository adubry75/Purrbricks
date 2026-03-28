using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that manages one-time tutorial popups shown during gameplay.
///
/// Each tutorial is shown at most once per installation — a PlayerPrefs key guards
/// re-display. Multiple tutorials may queue up and are shown one after another.
///
/// To add a new tutorial:
///   1. Add a public const string to <see cref="ID"/>.
///   2. Call <c>TutorialManager.Instance?.TriggerIfNew(ID.YourKey, glyph, title, body)</c>
///      from wherever the event occurs.
///   Done — the system handles queuing, time-pausing, and PlayerPrefs tracking.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    // ── Tutorial IDs (PlayerPrefs keys) ───────────────────────────────────────
    // Add new IDs here when creating new tutorials.
    public static class ID
    {
        public const string LaunchBall    = "tut_launch_ball";
        public const string FuryStrike    = "tut_fury_strike";
        public const string Inventory     = "tut_inventory";
        public const string MultiBallFury = "tut_multiball_fury";
        public const string PerfStars     = "tut_perf_stars";
    }

    // ── Palette ───────────────────────────────────────────────────────────────
    private static readonly Color ColorGold = new Color(1.00f, 0.84f, 0.10f);
    private static readonly Color ColorBody = new Color(0.85f, 0.90f, 0.95f);

    // ── Internal card data ────────────────────────────────────────────────────
    private struct TutorialCard
    {
        public string Id;
        public string Glyph;
        public string Title;
        public string Body;
    }

    private readonly Queue<TutorialCard>  _queue     = new Queue<TutorialCard>();
    private readonly HashSet<string>      _queuedIds = new HashSet<string>();
    private bool  _isShowing;
    private float _savedTimeScale;
    private bool  _savedCursorVisible;
    private CursorLockMode _savedLockState;

    public bool IsShowing => _isShowing;

    // ── UI refs ───────────────────────────────────────────────────────────────
    private Canvas _canvas;
    private GameObject _panelRoot; // parent of backdrop + card; toggled to show/hide
    private GameObject _card;
    private Text   _glyphText;
    private Text   _titleText;
    private Text   _bodyText;

    // RectTransforms needed for dynamic card resizing
    private RectTransform _cardRt;
    private RectTransform _glyphRt;
    private RectTransform _titleRt;
    private RectTransform _dividerRt;
    private RectTransform _bodyRt;
    private RectTransform _okBtnRt;
    private Button        _okBtn;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Show a tutorial popup the first time only (PlayerPrefs guards re-display).
    /// Safe to call every frame or from rapid-fire events — duplicates and already-seen
    /// tutorials are silently discarded.
    /// </summary>
    /// <param name="id">Unique PlayerPrefs key (use a constant from <see cref="ID"/>).</param>
    /// <param name="glyph">Short decorative string shown large above the title (e.g. "★ ★ ★"). Pass null/empty to hide.</param>
    /// <param name="title">Bold heading shown in gold.</param>
    /// <param name="body">Multi-line instructional text.</param>
    public void TriggerIfNew(string id, string glyph, string title, string body)
    {
        if (PlayerPrefs.GetInt(id, 0) != 0) return;  // already seen
        if (_queuedIds.Contains(id))         return;  // already queued
        if (_isShowing && id == GetCurrentId()) return; // currently showing

        _queuedIds.Add(id);
        _queue.Enqueue(new TutorialCard { Id = id, Glyph = glyph, Title = title, Body = body });

        if (!_isShowing) ShowNext();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private string _currentId;

    private string GetCurrentId() => _currentId;

    private void ShowNext()
    {
        if (_queue.Count == 0) return;

        var card = _queue.Dequeue();
        _queuedIds.Remove(card.Id);
        _currentId = card.Id;

        // Mark seen immediately so any triggers that fire while the popup is open
        // don't re-queue this tutorial.
        PlayerPrefs.SetInt(card.Id, 1);
        PlayerPrefs.Save();

        _isShowing = true;
        _savedTimeScale = Time.timeScale;
        Time.timeScale  = 0f;

        _savedCursorVisible = Cursor.visible;
        _savedLockState     = Cursor.lockState;
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;

        if (_glyphText != null)
        {
            _glyphText.text = card.Glyph ?? "";
            _glyphText.gameObject.SetActive(!string.IsNullOrEmpty(card.Glyph));
        }
        if (_titleText != null) _titleText.text = card.Title;
        if (_bodyText  != null) _bodyText.text  = card.Body;

        _panelRoot.SetActive(true);
        UINavController.SetDefault(_okBtn?.gameObject);
        ResizeCard();
    }

    private void OnOkClicked()
    {
        _panelRoot.SetActive(false);
        _currentId  = null;
        _isShowing  = false;
        Time.timeScale   = _savedTimeScale;
        Cursor.visible   = _savedCursorVisible;
        Cursor.lockState = _savedLockState;
        ShowNext(); // chain to next queued tutorial
    }

    // ── Dev helper: reset all tutorial flags (Ctrl+Shift+F11) ───────────────

    private void Update()
    {
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift)
            && Input.GetKeyDown(KeyCode.F11))
            ResetAllTutorials();
    }

    /// <summary>Clears all tutorial seen-flags so they will show again. Dev/QA use only.</summary>
    public void ResetAllTutorials()
    {
        PlayerPrefs.DeleteKey(ID.LaunchBall);
        PlayerPrefs.DeleteKey(ID.FuryStrike);
        PlayerPrefs.DeleteKey(ID.Inventory);
        PlayerPrefs.DeleteKey(ID.MultiBallFury);
        PlayerPrefs.DeleteKey(ID.PerfStars);
        PlayerPrefs.Save();
        Debug.Log("[TutorialManager] All tutorial flags reset.");
    }

    // ── Dynamic card resizing ─────────────────────────────────────────────────

    private void ResizeCard()
    {
        if (_cardRt == null || _bodyRt == null || _bodyText == null) return;

        // Set a generous body height before measuring so the Text wraps at the correct width
        // but doesn't clip vertically.
        _bodyRt.sizeDelta = new Vector2(600f, 2000f);

        // Force a layout rebuild so preferredHeight is accurate for the current text content.
        Canvas.ForceUpdateCanvases();

        float bodyH   = _bodyText.preferredHeight + 4f;  // +4 for sub-pixel safety
        bool hasGlyph = _glyphText != null && _glyphText.gameObject.activeSelf;

        // Fixed spacing constants (canvas units at 1920×1080 reference resolution)
        const float topPad   = 36f;
        const float glyphH   = 68f;
        const float glyphGap = 12f;
        const float titleH   = 64f;
        const float titleGap = 16f;
        const float divH     = 2f;
        const float divGap   = 20f;
        const float bodyGap  = 26f;
        const float btnH     = 60f;
        const float botPad   = 28f;

        float cardH = topPad
                    + (hasGlyph ? glyphH + glyphGap : 0f)
                    + titleH + titleGap + divH + divGap
                    + bodyH + bodyGap + btnH + botPad;

        cardH = Mathf.Max(cardH, 380f);
        _cardRt.sizeDelta = new Vector2(680f, cardH);

        // Lay out children top-down; y is distance from card center (positive = up).
        float y = cardH * 0.5f - topPad;

        if (hasGlyph && _glyphRt != null)
        {
            _glyphRt.anchoredPosition = new Vector2(0f, y - glyphH * 0.5f);
            _glyphRt.sizeDelta        = new Vector2(640f, glyphH);
            y -= glyphH + glyphGap;
        }

        if (_titleRt != null)
        {
            _titleRt.anchoredPosition = new Vector2(0f, y - titleH * 0.5f);
            y -= titleH + titleGap;
        }

        if (_dividerRt != null)
        {
            _dividerRt.anchoredPosition = new Vector2(0f, y - divH * 0.5f);
            y -= divH + divGap;
        }

        if (_bodyRt != null)
        {
            _bodyRt.sizeDelta        = new Vector2(600f, bodyH);
            _bodyRt.anchoredPosition = new Vector2(0f, y - bodyH * 0.5f);
            y -= bodyH + bodyGap;
        }

        if (_okBtnRt != null)
            _okBtnRt.anchoredPosition = new Vector2(0f, y - btnH * 0.5f);
    }

    // ── UI Construction ────────────────────────────────────────────────────────

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 600; // above LevelCodeEntryUI (500) and everything else

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // ── Panel root (backdrop + card hidden together) ───────────────────────
        _panelRoot = new GameObject("PanelRoot");
        _panelRoot.transform.SetParent(transform, false);
        var panelRt = _panelRoot.AddComponent<RectTransform>();
        panelRt.anchorMin        = Vector2.zero;
        panelRt.anchorMax        = Vector2.one;
        panelRt.sizeDelta        = Vector2.zero;
        panelRt.anchoredPosition = Vector2.zero;

        // ── Dark full-screen backdrop ──────────────────────────────────────────
        var backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(_panelRoot.transform, false);
        var bdImg = backdrop.AddComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.72f);
        var bdRt = bdImg.GetComponent<RectTransform>();
        bdRt.anchorMin        = Vector2.zero;
        bdRt.anchorMax        = Vector2.one;
        bdRt.sizeDelta        = Vector2.zero;
        bdRt.anchoredPosition = Vector2.zero;

        // ── Card ──────────────────────────────────────────────────────────────
        _card = new GameObject("Card");
        _card.transform.SetParent(_panelRoot.transform, false);

        var cardImg = _card.AddComponent<Image>();
        cardImg.color = new Color(0.04f, 0.06f, 0.14f, 0.97f);

        var cardOl = _card.AddComponent<Outline>();
        cardOl.effectColor    = new Color(0.30f, 0.65f, 1f, 0.55f);
        cardOl.effectDistance = new Vector2(3f, -3f);

        _cardRt = _card.GetComponent<RectTransform>();
        _cardRt.anchorMin        = new Vector2(0.5f, 0.5f);
        _cardRt.anchorMax        = new Vector2(0.5f, 0.5f);
        _cardRt.pivot            = new Vector2(0.5f, 0.5f);
        _cardRt.sizeDelta        = new Vector2(680f, 440f);  // initial size; ResizeCard() overrides height
        _cardRt.anchoredPosition = new Vector2(-160f, 0f);

        // ── Accent bar at top edge ─────────────────────────────────────────────
        var barGO = new GameObject("AccentBar");
        barGO.transform.SetParent(_card.transform, false);
        var barImg = barGO.AddComponent<Image>();
        barImg.color = new Color(0.18f, 0.38f, 0.80f, 1f);
        barImg.raycastTarget = false;
        var barRt = barGO.GetComponent<RectTransform>();
        barRt.anchorMin        = new Vector2(0f, 1f);
        barRt.anchorMax        = new Vector2(1f, 1f);
        barRt.pivot            = new Vector2(0.5f, 1f);
        barRt.sizeDelta        = new Vector2(0f, 7f);
        barRt.anchoredPosition = Vector2.zero;

        // ── Glyph (decorative large text above title) ──────────────────────────
        var glyphGO = new GameObject("Glyph");
        glyphGO.transform.SetParent(_card.transform, false);
        _glyphText = glyphGO.AddComponent<Text>();
        _glyphText.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _glyphText.fontSize      = 52;
        _glyphText.fontStyle     = FontStyle.Bold;
        _glyphText.alignment     = TextAnchor.MiddleCenter;
        _glyphText.color         = new Color(ColorGold.r, ColorGold.g, ColorGold.b, 0.80f);
        _glyphText.raycastTarget = false;
        _glyphRt = _glyphText.GetComponent<RectTransform>();
        _glyphRt.anchorMin        = new Vector2(0.5f, 0.5f);
        _glyphRt.anchorMax        = new Vector2(0.5f, 0.5f);
        _glyphRt.sizeDelta        = new Vector2(640f, 68f);
        _glyphRt.anchoredPosition = new Vector2(0f, 158f);  // overridden by ResizeCard

        // ── Title ──────────────────────────────────────────────────────────────
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(_card.transform, false);
        _titleText = titleGO.AddComponent<Text>();
        _titleText.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _titleText.fontSize      = 48;
        _titleText.fontStyle     = FontStyle.Bold;
        _titleText.alignment     = TextAnchor.MiddleCenter;
        _titleText.color         = ColorGold;
        _titleText.raycastTarget = false;
        var titleOl = titleGO.AddComponent<Outline>();
        titleOl.effectColor    = Color.black;
        titleOl.effectDistance = new Vector2(3f, -3f);
        _titleRt = _titleText.GetComponent<RectTransform>();
        _titleRt.anchorMin        = new Vector2(0.5f, 0.5f);
        _titleRt.anchorMax        = new Vector2(0.5f, 0.5f);
        _titleRt.sizeDelta        = new Vector2(640f, 64f);
        _titleRt.anchoredPosition = new Vector2(0f, 86f);  // overridden by ResizeCard

        // ── Divider ────────────────────────────────────────────────────────────
        var divGO = new GameObject("Divider");
        divGO.transform.SetParent(_card.transform, false);
        var divImg = divGO.AddComponent<Image>();
        divImg.color = new Color(0.30f, 0.60f, 1f, 0.35f);
        divImg.raycastTarget = false;
        _dividerRt = divGO.GetComponent<RectTransform>();
        _dividerRt.anchorMin        = new Vector2(0.5f, 0.5f);
        _dividerRt.anchorMax        = new Vector2(0.5f, 0.5f);
        _dividerRt.sizeDelta        = new Vector2(580f, 2f);
        _dividerRt.anchoredPosition = new Vector2(0f, 50f);  // overridden by ResizeCard

        // ── Body text ──────────────────────────────────────────────────────────
        var bodyGO = new GameObject("Body");
        bodyGO.transform.SetParent(_card.transform, false);
        _bodyText = bodyGO.AddComponent<Text>();
        _bodyText.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _bodyText.fontSize      = 26;
        _bodyText.alignment     = TextAnchor.MiddleCenter;
        _bodyText.color         = ColorBody;
        _bodyText.lineSpacing   = 1.35f;
        _bodyText.raycastTarget = false;
        _bodyRt = _bodyText.GetComponent<RectTransform>();
        _bodyRt.anchorMin        = new Vector2(0.5f, 0.5f);
        _bodyRt.anchorMax        = new Vector2(0.5f, 0.5f);
        _bodyRt.sizeDelta        = new Vector2(600f, 180f);
        _bodyRt.anchoredPosition = new Vector2(0f, -52f);  // overridden by ResizeCard

        // ── OK / GOT IT button ─────────────────────────────────────────────────
        var okBtn = UIStyle.CreateButton(_card.transform, "GOT IT!",
            new Vector2(0f, -182f), new Vector2(240f, 60f),
            OnOkClicked, UIStyle.AccentGreen);
        _okBtn   = okBtn;
        _okBtnRt = okBtn.GetComponent<RectTransform>();  // overridden by ResizeCard

        // Start hidden
        _panelRoot.SetActive(false);
    }
}
