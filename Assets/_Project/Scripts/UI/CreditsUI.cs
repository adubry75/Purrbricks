using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen scrolling credits shown when the player completes every level.
/// Fades to black, plays the GameFinished music track, scrolls the credits,
/// then reveals the Back to Main Menu button.
///
/// Cat avatar sprites (256x256) can be assigned in the Inspector — they will
/// appear above each cat's title card. Leave them null to use text-only layout.
/// Order: Jammies, Squeekers, Daisy, Callie, Sully, Jynx, Nero.
/// </summary>
public class CreditsUI : MonoBehaviour
{
    public static CreditsUI Instance { get; private set; }

    [Header("Cat Avatars (optional 256x256 sprites)")]
    [Tooltip("In order: Jammies, Squeekers, Daisy, Callie, Sully, Jynx, Nero")]
    [SerializeField] private Sprite[] _catAvatars = new Sprite[7];

    private Canvas        _canvas;
    private Image         _fadeOverlay;
    private RectTransform _scrollContainer;
    private GameObject    _mainMenuButton;
    private Coroutine     _creditsRoutine;

    // ── Scroll state ─────────────────────────────────────────────────────────
    private bool  _isScrolling;
    private bool  _scrollComplete;
    private float _scrollY;
    private float _scrollStart;
    private float _scrollEnd;

    // User interaction (drag / wheel)
    private bool  _isDragging;
    private float _dragLastMouseY;
    private float _autoScrollPauseTimer; // counts down after last user input

    // Scroll speed in reference pixels per second (1080p)
    private const float SCROLL_SPEED        = 60f;
    private const float WHEEL_SPEED         = 50f;  // canvas px per scroll notch
    private const float DRAG_SCALE          = 1.8f;  // canvas px per screen px
    private const float AUTO_SCROLL_PAUSE   = 0.5f;  // seconds auto-scroll pauses after user input
    private const float OVERLAY_ALPHA       = 0.82f; // galaxy shows through at ~18% — raise to darken, lower to show more
    private const float FADE_IN_DUR         = 2.0f;
    private const float PRE_SCROLL          = 0.2f;  // pause after fade before scrolling starts
    private const float POST_SCROLL         = 0.2f;  // pause after scroll before button appears
    private const float TOP_PAD             = 100f;  // blank before first item appears
    private const float BOTTOM_PAD          = 100f;  // blank after last item

    // ── Color palette ────────────────────────────────────────────────────────
    private static readonly Color ColGold    = new Color(1.00f, 0.84f, 0.10f);
    private static readonly Color ColCyan    = new Color(0.35f, 0.90f, 1.00f);
    private static readonly Color ColWhite   = Color.white;
    private static readonly Color ColSub     = new Color(0.78f, 0.78f, 0.84f);
    private static readonly Color ColCat     = new Color(1.00f, 0.70f, 0.82f);
    private static readonly Color ColMuted   = new Color(0.52f, 0.52f, 0.57f);
    private static readonly Color ColGreen   = new Color(0.20f, 1.00f, 0.45f);
    private static readonly Color ColDivider = new Color(0.35f, 0.35f, 0.40f);

    // ── Item types ────────────────────────────────────────────────────────────
    private struct CreditItem
    {
        public string    Text;
        public int       FontSize;
        public Color     Color;
        public float     Height;
        public FontStyle Style;
        public bool      IsImage;
        public Sprite    Sprite;
        public Vector2   ImageSize;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
        gameObject.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowCredits(int finalScore)
    {
        gameObject.SetActive(true);
        _mainMenuButton?.SetActive(false);
        _isScrolling   = false;
        _scrollComplete = false;
        if (_fadeOverlay != null) _fadeOverlay.color = new Color(0f, 0f, 0f, 0f);

        if (_creditsRoutine != null) StopCoroutine(_creditsRoutine);
        _creditsRoutine = StartCoroutine(CreditsRoutine(finalScore));
    }

    public void Hide()
    {
        if (_creditsRoutine != null) { StopCoroutine(_creditsRoutine); _creditsRoutine = null; }
        gameObject.SetActive(false);
    }

    private void OnMainMenu()
    {
        Hide();
        GameManager.Instance?.ShowMainMenu();
    }

    // ── Update — handles scroll input once rolling ────────────────────────────

    private void Update()
    {
        if (!_isScrolling) return;

        bool userActed = false;

        // Mouse wheel: scroll up = forward, scroll down = rewind
        float wheel = Input.mouseScrollDelta.y;
        if (wheel != 0f)
        {
            _scrollY  -= wheel * WHEEL_SPEED;
            userActed  = true;
        }

        // Click + drag: drag mouse up = rewind, drag down = forward
        if (Input.GetMouseButtonDown(0))
        {
            _isDragging    = true;
            _dragLastMouseY = Input.mousePosition.y;
        }
        if (Input.GetMouseButtonUp(0))
            _isDragging = false;

        if (_isDragging)
        {
            float dy        = Input.mousePosition.y - _dragLastMouseY;
            _scrollY       += dy * DRAG_SCALE;   // drag up (positive dy) → rewind
            _dragLastMouseY = Input.mousePosition.y;
            if (Mathf.Abs(dy) > 0.5f) userActed = true;
        }

        // User input pauses auto-scroll for AUTO_SCROLL_PAUSE seconds
        if (userActed)
            _autoScrollPauseTimer = AUTO_SCROLL_PAUSE;

        if (_autoScrollPauseTimer > 0f)
            _autoScrollPauseTimer -= Time.unscaledDeltaTime;
        else
            _scrollY += SCROLL_SPEED * Time.unscaledDeltaTime;

        // Clamp: can't rewind before the very first frame, can't overshoot end
        _scrollY = Mathf.Max(_scrollY, _scrollStart);

        if (_scrollY >= _scrollEnd)
        {
            _scrollY        = _scrollEnd;
            _isScrolling    = false;
            _scrollComplete = true;
        }

        if (_scrollContainer != null)
            _scrollContainer.anchoredPosition = new Vector2(0f, _scrollY);
    }

    // ── Main Coroutine ────────────────────────────────────────────────────────

    private IEnumerator CreditsRoutine(int finalScore)
    {
        // --- 1. Fade to FULL BLACK (alpha 1.0) — covers everything cleanly ---
        float elapsed = 0f;
        while (elapsed < FADE_IN_DUR)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / FADE_IN_DUR);
            if (_fadeOverlay != null) _fadeOverlay.color = new Color(0f, 0f, 0f, t);
            yield return null;
        }
        if (_fadeOverlay != null) _fadeOverlay.color = new Color(0f, 0f, 0f, 1f);

        // --- 2. Now that everything is hidden behind black: tear it all down ---
        GameManager.Instance?.HideAllForCredits();

        // --- 3. Start credits music ---
        MusicPlayer.Instance?.PlayGameFinished();

        // --- 4. Fade overlay back down to OVERLAY_ALPHA, revealing the galaxy ---
        elapsed = 0f;
        const float FADE_OUT_DUR = 1.5f;
        while (elapsed < FADE_OUT_DUR)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / FADE_OUT_DUR);
            float alpha = Mathf.Lerp(1f, OVERLAY_ALPHA, t);
            if (_fadeOverlay != null) _fadeOverlay.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
        if (_fadeOverlay != null) _fadeOverlay.color = new Color(0f, 0f, 0f, OVERLAY_ALPHA);

        // --- 5. Brief pause before scroll starts ---
        yield return new WaitForSecondsRealtime(PRE_SCROLL);

        // --- 6. Set scroll positions and hand off to Update() ---
        float contentH  = _scrollContainer != null ? _scrollContainer.sizeDelta.y : 3500f;
        _scrollStart    = -contentH;
        _scrollEnd      = 1080f + 250f;
        _scrollY        = _scrollStart;
        _autoScrollPauseTimer = 0f;
        _isDragging     = false;

        if (_scrollContainer != null)
            _scrollContainer.anchoredPosition = new Vector2(0f, _scrollStart);

        // --- 7. Let Update() drive the scroll; wait until it signals complete ---
        _scrollComplete = false;
        _isScrolling    = true;
        yield return new WaitUntil(() => _scrollComplete);

        // --- 8. Short pause, then reveal button ---
        yield return new WaitForSecondsRealtime(POST_SCROLL);
        _mainMenuButton?.SetActive(true);
        _creditsRoutine = null;
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 300;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        // Full-screen black overlay (starts transparent, faded in by coroutine)
        var bgGO = new GameObject("FadeOverlay");
        bgGO.transform.SetParent(transform, false);
        _fadeOverlay       = bgGO.AddComponent<Image>();
        _fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // Scroll container: anchored at canvas bottom, full width, pivot at bottom
        var scrollGO = new GameObject("ScrollContainer");
        scrollGO.transform.SetParent(transform, false);
        _scrollContainer           = scrollGO.AddComponent<RectTransform>();
        _scrollContainer.anchorMin = new Vector2(0f, 0f);
        _scrollContainer.anchorMax = new Vector2(1f, 0f);
        _scrollContainer.pivot     = new Vector2(0.5f, 0f);

        float totalH = BuildScrollContent(scrollGO);
        _scrollContainer.sizeDelta        = new Vector2(0f, totalH);
        _scrollContainer.anchoredPosition = new Vector2(0f, -totalH);

        // "Back to Main Menu" button — hidden until scroll ends
        var btnGO = UIStyle.CreateButton(transform, "Back to Main Menu",
            Vector2.zero, new Vector2(440f, 80f), OnMainMenu, UIStyle.AccentGold);
        _mainMenuButton = btnGO.gameObject;
        var btnRt = _mainMenuButton.GetComponent<RectTransform>();
        btnRt.anchorMin        = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax        = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = new Vector2(0f, -370f);
        _mainMenuButton.SetActive(false);
    }

    // ── Credits Content ───────────────────────────────────────────────────────

    /// <summary>Populates the scroll container with all credits items, returns total height.</summary>
    private float BuildScrollContent(GameObject container)
    {
        var items = new List<CreditItem>();

        // ─── Helpers ─────────────────────────────────────────────────────────
        void Text(string t, int sz, Color c, float h, FontStyle fs = FontStyle.Bold)
            => items.Add(new CreditItem { Text = t, FontSize = sz, Color = c, Height = h, Style = fs });

        void Sub(string t, int sz = 24, float h = 37f)
            => items.Add(new CreditItem { Text = t, FontSize = sz, Color = ColSub, Height = h, Style = FontStyle.Normal });

        void Muted(string t, int sz = 22, float h = 34f)
            => items.Add(new CreditItem { Text = t, FontSize = sz, Color = ColMuted, Height = h, Style = FontStyle.Normal });

        void MutedItalic(string t, int sz = 22, float h = 34f)
            => items.Add(new CreditItem { Text = t, FontSize = sz, Color = ColMuted, Height = h, Style = FontStyle.Italic });

        void Spacer(float h)
            => items.Add(new CreditItem { Height = h });

        void Divider()
        {
            items.Add(new CreditItem { Text = "- - - - - - - - - - - - - - - - - - -",
                FontSize = 22, Color = ColDivider, Height = 38f, Style = FontStyle.Normal });
        }

        void SectionHeader(string t)
        {
            Divider();
            Text(t, 34, ColCyan, 52f);
            Divider();
        }

        void BigSectionHeader(string t)
        {
            Text("= = = = = = = = = = = = = = = = = = =", 24, new Color(0.50f, 0.42f, 0.05f), 38f, FontStyle.Normal);
            Spacer(8f);
            Text(t, 52, ColGold, 70f);
            Spacer(8f);
            Text("= = = = = = = = = = = = = = = = = = =", 24, new Color(0.50f, 0.42f, 0.05f), 38f, FontStyle.Normal);
        }

        void CatEntry(string jobTitle, string name, string line1, string line2, int avatarIndex)
        {
            Sprite av = (_catAvatars != null && avatarIndex < _catAvatars.Length)
                ? _catAvatars[avatarIndex] : null;
            if (av != null)
                items.Add(new CreditItem { IsImage = true, Sprite = av,
                    ImageSize = new Vector2(130f, 130f), Height = 140f });
            Text(jobTitle, 27, ColCyan, 42f);
            Text(name,     64, ColCat,  82f);
            Sub(line1);
            MutedItalic(line2);
            Spacer(62f);
        }

        // =====================================================================
        //                        THE ACTUAL CREDITS
        // =====================================================================

        // ── Opening ──────────────────────────────────────────────────────────
        Text("C O N G R A T U L A T I O N S", 52, ColGold, 70f);
        Spacer(18f);
        Text("You purr-fected the game.", 36, ColSub, 52f, FontStyle.Italic);
        Spacer(30f);
        Text("All Levels Complete", 64, ColWhite, 84f);
        Spacer(16f);
        Text("· · · fin · · ·", 38, ColMuted, 52f, FontStyle.Italic);
        Spacer(130f);

        // ── Developer ─────────────────────────────────────────────────────────
        SectionHeader("CREATED BY");
        Spacer(22f);
        Text("Andrew Dubry", 76, ColWhite, 96f);
        Sub("Programmer  ·  Game Designer  ·  Cat Wrangler", 28, 44f);
        Spacer(90f);

        // ── AI ───────────────────────────────────────────────────────────────
        SectionHeader("AI PAIR PROGRAMMER");
        Spacer(22f);
        Text("Claude", 64, ColWhite, 82f);
        Sub("(by Anthropic)", 30, 46f);
        Sub("Responsible for ~82% of keystrokes.", 26, 40f);
        MutedItalic("0% of creative vision. 100% of semicolons.");
        Spacer(90f);

        // ── Music ─────────────────────────────────────────────────────────────
        //SectionHeader("MUSIC COMPOSED WITH");
        //Spacer(22f);
        //Text("Suno AI", 64, ColWhite, 82f);
        //MutedItalic("Certified Banger Generator\u2122");
        //Spacer(130f);

        // ── Cats section header ───────────────────────────────────────────────
        BigSectionHeader("THE DEVELOPMENT CATS");
        MutedItalic("( they know what they did )", 26, 40f);
        Spacer(70f);

        // ── The 7 cats ────────────────────────────────────────────────────────
        CatEntry(
            "CHIEF DISRUPTION OFFICER",
            "Jammies",
            "Logged 847 keyboard walk-across incidents.",
            "Still employed. Cuteness is non-negotiable.",
            0);

        CatEntry(
            "SENIOR SLEEP ANALYST",
            "Squeekers",
            "Expert in napping directly on critical hardware.",
            "Sisters with Daisy. Sharing is apparently mandatory.",
            1);

        CatEntry(
            "JUNIOR SLEEP ANALYST  ( Sister of Squeekers )",
            "Daisy",
            "Same litter. Same talent for being in the way.",
            "Certified Advanced Napper since birth.",
            2);

        CatEntry(
            "LEAD DISTRACTION ENGINEER",
            "Callie",
            "Officially reduced developer productivity by 40%.",
            "Unofficially: significantly higher.",
            3);

        CatEntry(
            "EMOTIONAL SUPPORT SPECIALIST",
            "Sully",
            "( a.k.a. \" Soybean \" )",
            "Provided unsolicited lap warmth during every debug session.",
            4);

        CatEntry(
            "NIGHT SHIFT CHAOS COORDINATOR",
            "Jynx",
            "Responsible for all mysterious 3am crashes.",
            "Investigation ongoing. No suspects. Definitely not Jynx.",
            5);

        CatEntry(
            "ROOKIE OF THE YEAR  ( Male )",
            "Nero",
            "( Adopted stray  -  Now reigning supreme )",
            "Arrived with nothing. Currently owns the best sunbeam.",
            6);

        Spacer(30f);
        Text("· · · · · · · · · · · · · · · · · · ·", 22, ColDivider, 36f, FontStyle.Normal);
        Spacer(100f);

        // ── Special Thanks ────────────────────────────────────────────────────
        BigSectionHeader("SPECIAL THANKS");
        Spacer(50f);

        Text("Stack Overflow", 46, ColWhite, 64f);
        MutedItalic("( for answers that made sense circa 2019 )", 26, 40f);
        Spacer(50f);

        Text("Unity Technologies", 46, ColWhite, 64f);
        MutedItalic("( for making us stronger through suffering )", 26, 40f);
        Spacer(50f);

        Text("Steam  &  Valve", 46, ColWhite, 64f);
        MutedItalic("( a surprisingly cat-tolerant platform )", 26, 40f);
        Spacer(70f);

        Text("Y O U .", 78, ColGreen, 98f);
        Sub("Yes, you.  The player who actually made it this far.", 32, 50f);
        MutedItalic("We are genuinely, sincerely impressed.", 28, 44f);
        Spacer(140f);

        // ── Fine Print ────────────────────────────────────────────────────────
        Text("= = = = = = = = = = = = = = = = = = =", 24, new Color(0.30f, 0.30f, 0.34f), 38f, FontStyle.Normal);
        Spacer(8f);
        Text("F  I  N  E     P  R  I  N  T", 32, ColMuted, 48f);
        Spacer(8f);
        Text("= = = = = = = = = = = = = = = = = = =", 24, new Color(0.30f, 0.30f, 0.34f), 38f, FontStyle.Normal);
        Spacer(38f);

        Muted("PURRBRICKS is a work of fiction.");
        Muted("Any resemblance to actual bricks, actual cats,");
        Muted("or actual developer productivity is purely coincidental.");
        Spacer(32f);

        Muted("No cats were harmed in the making of this game.");
        Muted("Several keyboards were sat upon.");
        MutedItalic("The keyboards are fine.  Mostly.");
        Spacer(32f);

        Muted("This game contains approximately 12,000 bricks,");
        Muted("Over 100 levels, and one developer");
        MutedItalic("who definitely should have gone to bed earlier.");
        Spacer(32f);

        Muted("The cats listed above did not receive royalties.");
        Muted("Their demands for premium wet food are under review.");
        MutedItalic("Nero has not read his contract.");
        Spacer(46f);

        Text("PURRBRICKS\u2122  \u00A9  2025  Adam Dubrick", 34, ColWhite, 50f);
        Sub("All Rights Reserved.", 24, 38f);
        MutedItalic("( Except by Nero, who reserves the right to knock");
        MutedItalic("everything off the desk at any time. )");
        Spacer(40f);

        Muted("Rated  E  for  Everyone");
        MutedItalic("( Except the cat who knocked your controller off the table");
        MutedItalic("that cat is rated  M  for  Menace )");
        Spacer(40f);

        Muted("Any bugs discovered during development were investigated by Jynx");
        MutedItalic("and officially classified as  \" Not A Bug, Just Chaos. \"");
        Spacer(40f);

        Muted("The phrase  \" just one more level \"  was responsible for");
        Muted("approximately 73% of all late nights during production.");
        Spacer(40f);

        Muted("This message brought to you by 3am energy drinks,");
        MutedItalic("questionable life choices, and unprompted cat headbutts.");
        Spacer(70f);

        Text("· · · · · · · · · · · · · · · · · · ·", 26, new Color(0.55f, 0.46f, 0.10f), 40f, FontStyle.Normal);
        Spacer(20f);

        // ─────────────────────────────────────────────────────────────────────
        // Calculate total height from items
        float totalH = TOP_PAD + BOTTOM_PAD;
        foreach (var item in items) totalH += item.Height;

        // Place items: first item at highest Y (appears first on screen as container scrolls up)
        // Container pivot = bottom, so Y=0 is container bottom, Y=totalH is container top.
        // Items at HIGH Y appear when container has just started scrolling (they are at screen bottom).
        float cursor = totalH - TOP_PAD; // start cursor near top of container
        foreach (var item in items)
        {
            cursor -= item.Height;
            float centerY = cursor + item.Height * 0.5f;

            if (item.IsImage && item.Sprite != null)
                PlaceImage(container, item.Sprite, new Vector2(0f, centerY), item.ImageSize);
            else if (!string.IsNullOrEmpty(item.Text))
                PlaceText(container, item.Text, new Vector2(0f, centerY),
                          item.FontSize, item.Color, 1600f, item.Style);
        }

        return totalH;
    }

    // ── Placement Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Places a text label inside the scroll container at the given anchored position.
    /// Anchor/pivot: (0.5, 0) = bottom-center of the container.
    /// </summary>
    private static void PlaceText(GameObject parent, string text, Vector2 pos,
                                   int fontSize, Color color, float width, FontStyle style)
    {
        var go = new GameObject("CT");
        go.transform.SetParent(parent.transform, false);

        var txt          = go.AddComponent<Text>();
        txt.text         = text;
        txt.font         = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize     = fontSize;
        txt.fontStyle    = style;
        txt.alignment    = TextAnchor.MiddleCenter;
        txt.color        = color;
        txt.raycastTarget = false;

        var rt              = txt.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(width, fontSize + 22f);
        rt.anchoredPosition = pos;

        var ol            = go.AddComponent<Outline>();
        ol.effectColor    = new Color(0f, 0f, 0f, 0.55f);
        ol.effectDistance = new Vector2(2f, -2f);
    }

    /// <summary>Places a cat avatar image inside the scroll container.</summary>
    private static void PlaceImage(GameObject parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("CatAvatar");
        go.transform.SetParent(parent.transform, false);

        var img            = go.AddComponent<Image>();
        img.sprite         = sprite;
        img.preserveAspect = true;
        img.raycastTarget  = false;

        var rt              = img.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;
    }
}
