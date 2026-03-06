using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Middle-mouse radial selector for the powerup inventory.
/// Hold MMB to open; hover a tile and release to activate.
/// Good powerups appear on the outer ring, bad/cursed on the inner ring.
/// Releasing while not hovering any tile aborts without activating.
/// </summary>
public class InventoryRadialMenu : MonoBehaviour
{
    public static InventoryRadialMenu Instance { get; private set; }

    // Canvas
    private Canvas _canvas;

    // State
    private bool _isOpen;
    private int  _hoveredIndex = -1;

    // Slot info (rebuilt each open)
    private struct SlotInfo
    {
        public PowerupType type;
        public int         qty;
        public bool        isInner;
    }
    private readonly List<SlotInfo>      _allSlots  = new List<SlotInfo>();
    private readonly List<GameObject>    _slotGOs   = new List<GameObject>();
    private readonly List<RectTransform> _slotRTs   = new List<RectTransform>();
    private readonly List<Image>         _slotBgs   = new List<Image>(); // main bg circle per slot

    // Animation
    private Coroutine   _animRoutine;
    private Coroutine   _hintRoutine;
    private GameObject  _radialRoot;
    private CanvasGroup _radialGroup;

    // Layout
    private const float OUTER_RADIUS    = 330f;
    private const float INNER_RADIUS    = 220f;
    private const float OUTER_SLOT_SIZE = 88f;
    private const float INNER_SLOT_SIZE = 72f;
    private const float HIGHLIGHT_SCALE = 1.18f;
    private const float OPEN_DURATION   = 0.14f;
    private const float CLOSE_DURATION  = 0.09f;

    // Cached procedural sprites
    private static Sprite _circleSprite;
    private static Sprite _ringSprite;

    // ── Colour / label tables ─────────────────────────────────────────────────

    private static readonly Color[] TypeColors =
    {
        new Color(0.00f, 0.85f, 1.00f), // 0  WidePaddle
        new Color(1.00f, 0.55f, 0.00f), // 1  MultiBall
        new Color(0.20f, 1.00f, 0.40f), // 2  StickyBall
        new Color(1.00f, 0.80f, 0.00f), // 3  SpeedBall
        new Color(1.00f, 0.20f, 0.20f), // 4  ExtraLife
        new Color(0.30f, 0.70f, 1.00f), // 5  Laser
        new Color(1.00f, 0.40f, 0.10f), // 6  Fireball
        new Color(1.00f, 0.65f, 0.10f), // 7  BombBrick
        new Color(0.10f, 0.90f, 0.90f), // 8  ShieldWall
        new Color(0.55f, 0.30f, 1.00f), // 9  BigBall
        new Color(1.00f, 0.85f, 0.00f), // 10 ScoreFrenzy
        new Color(0.90f, 0.20f, 0.20f), // 11 ShrinkPaddle
        new Color(0.80f, 0.10f, 0.80f), // 12 ZipBall
        new Color(0.70f, 0.00f, 1.00f), // 13 FlipControls
        new Color(0.50f, 0.00f, 0.90f), // 14 CursedBall
        new Color(0.80f, 0.30f, 0.30f), // 15 TinyBall
        new Color(0.40f, 0.40f, 0.40f), // 16 InvisiBall
        new Color(0.30f, 0.70f, 0.30f), // 17 DrunkenPaddle
        new Color(0.60f, 0.40f, 1.00f), // 18 PermanentStickyBall
        new Color(0.20f, 0.60f, 1.00f), // 19 DrunkVision
        new Color(0.10f, 0.80f, 0.50f), // 20 GremlinBounces
        new Color(0.80f, 0.10f, 0.10f), // 21 FlipScreen
    };

    private static readonly string[] TypeLabels =
    {
        "WIDE PADDLE",  // 0
        "MULTI-BALL",   // 1
        "STICKY BALL",  // 2
        "SPEED BALL",   // 3
        "EXTRA LIFE",   // 4
        "LASER",        // 5
        "FIREBALL",     // 6
        "BOMB BRICK",   // 7
        "SHIELD",       // 8
        "BIG BALL",     // 9
        "FRENZY",       // 10
        "⚠ SHRINK",     // 11
        "⚠ ZIP BALL",   // 12
        "⚠ FLIP CTRL",  // 13
        "⚠ CURSED",     // 14
        "⚠ TINY BALL",  // 15
        "⚠ INVISI",     // 16
        "⚠ DRUNK PAD",  // 17
        "STICKY ∞",     // 18
        "⚠ DRUNK VIS",  // 19
        "⚠ GREMLIN",    // 20
        "⚠ FLIP SCR",   // 21
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCanvas();
    }

    private void BuildCanvas()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 400;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();
    }

    private void Update()
    {
        if (PurrBucksManager.Instance == null) return;

        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.State != GameState.Playing && gm.State != GameState.Ready) return;

        if (!_isOpen)
        {
            if (Input.GetMouseButtonDown(2))
                OpenRadial();
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
                CloseRadial(activate: false);
            else if (Input.GetMouseButtonUp(2))
                CloseRadial(activate: true);
            else
                UpdateHover();
        }
    }

    // ── Open ──────────────────────────────────────────────────────────────────

    private void OpenRadial()
    {
        var inv = PurrBucksManager.Instance?.GetAllInventory();
        if (inv == null || inv.Count == 0) return;

        var outerList = new List<(PowerupType type, int qty)>();
        var innerList = new List<(PowerupType type, int qty)>();

        foreach (var kvp in inv)
        {
            if (kvp.Value <= 0) continue;
            if (PowerupRules.IsBad(kvp.Key))
                innerList.Add((kvp.Key, kvp.Value));
            else
                outerList.Add((kvp.Key, kvp.Value));
        }

        // Fallback: if no good powerups, display bad ones on the outer ring
        if (outerList.Count == 0)
        {
            outerList.AddRange(innerList);
            innerList.Clear();
        }

        if (outerList.Count == 0) return;

        _allSlots.Clear();
        foreach (var s in outerList) _allSlots.Add(new SlotInfo { type = s.type, qty = s.qty, isInner = false });
        foreach (var s in innerList) _allSlots.Add(new SlotInfo { type = s.type, qty = s.qty, isInner = true  });

        _isOpen = true;
        Time.timeScale = 0f;
        Cursor.visible = true;

        BuildSlots(outerList.Count, innerList.Count);

        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(AnimateOpen());
    }

    // ── Build UI ──────────────────────────────────────────────────────────────

    private void BuildSlots(int outerCount, int innerCount)
    {
        if (_radialRoot != null) Destroy(_radialRoot);
        _slotGOs.Clear();
        _slotRTs.Clear();
        _slotBgs.Clear();

        _radialRoot = new GameObject("RadialRoot");
        _radialRoot.transform.SetParent(transform, false);

        var rootRt = _radialRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta        = Vector2.zero;
        rootRt.anchoredPosition = new Vector2(-160f, 0f);

        _radialGroup = _radialRoot.AddComponent<CanvasGroup>();
        _radialGroup.alpha = 0f;
        _radialGroup.interactable = false;
        _radialGroup.blocksRaycasts = false;

        // ── Dark background disk ──────────────────────────────────────────────
        float diskSize = OUTER_RADIUS * 2f + OUTER_SLOT_SIZE + 44f;
        MakeCircleDecal(_radialRoot.transform, "BgDisk",
                        new Color(0.04f, 0.06f, 0.15f, 0.90f), diskSize);

        // ── Subtle ring guide lines ───────────────────────────────────────────
        float outerGuide = OUTER_RADIUS * 2f + OUTER_SLOT_SIZE * 0.35f;
        MakeRingDecal(_radialRoot.transform, "OuterGuide",
                      new Color(1f, 1f, 1f, 0.06f), outerGuide);

        if (innerCount > 0)
        {
            float innerGuide = INNER_RADIUS * 2f + INNER_SLOT_SIZE * 0.35f;
            MakeRingDecal(_radialRoot.transform, "InnerGuide",
                          new Color(1f, 0.35f, 0.20f, 0.09f), innerGuide);
        }

        // ── Center label ──────────────────────────────────────────────────────
        MakeCenterLabel(_radialRoot.transform, innerCount > 0);

        // ── Outer ring ────────────────────────────────────────────────────────
        for (int i = 0; i < outerCount; i++)
        {
            float angle = 360f / outerCount * i - 90f;
            float rad   = angle * Mathf.Deg2Rad;
            var   pos   = new Vector2(Mathf.Cos(rad) * OUTER_RADIUS,
                                      Mathf.Sin(rad) * OUTER_RADIUS);
            BuildSlotGO(i, pos, _allSlots[i], OUTER_SLOT_SIZE);
        }

        // ── Inner ring ────────────────────────────────────────────────────────
        for (int i = 0; i < innerCount; i++)
        {
            int   allIdx = outerCount + i;
            float angle  = 360f / innerCount * i - 90f;
            float rad    = angle * Mathf.Deg2Rad;
            var   pos    = new Vector2(Mathf.Cos(rad) * INNER_RADIUS,
                                       Mathf.Sin(rad) * INNER_RADIUS);
            BuildSlotGO(allIdx, pos, _allSlots[allIdx], INNER_SLOT_SIZE);
        }

        _hoveredIndex = -1;
    }

    private void BuildSlotGO(int index, Vector2 anchoredPos, SlotInfo info, float slotSize)
    {
        int    typeIdx = (int)info.type;
        Color  col     = typeIdx < TypeColors.Length ? TypeColors[typeIdx] : Color.white;
        string label   = typeIdx < TypeLabels.Length ? TypeLabels[typeIdx] : info.type.ToString().ToUpper();

        var slotGO = new GameObject($"Slot_{index}");
        slotGO.transform.SetParent(_radialRoot.transform, false);

        var slotRt = slotGO.AddComponent<RectTransform>();
        slotRt.anchorMin = slotRt.anchorMax = slotRt.pivot = new Vector2(0.5f, 0.5f);
        slotRt.sizeDelta = new Vector2(slotSize, slotSize);
        slotRt.anchoredPosition = anchoredPos;

        // Rim (slightly larger circle, accent color, sits visually behind bg)
        var rimGO = new GameObject("Rim");
        rimGO.transform.SetParent(slotGO.transform, false);
        var rimImg = rimGO.AddComponent<Image>();
        rimImg.sprite       = GetCircleSprite();
        rimImg.color        = new Color(col.r, col.g, col.b, 0.50f);
        rimImg.raycastTarget = false;
        var rimRt = rimGO.GetComponent<RectTransform>();
        rimRt.anchorMin = rimRt.anchorMax = rimRt.pivot = new Vector2(0.5f, 0.5f);
        rimRt.sizeDelta         = new Vector2(slotSize + 7f, slotSize + 7f);
        rimRt.anchoredPosition  = Vector2.zero;

        // Main background circle (dark, tracked for hover highlights)
        var bgGO = new GameObject("Bg");
        bgGO.transform.SetParent(slotGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.sprite       = GetCircleSprite();
        bgImg.color        = new Color(col.r * 0.22f, col.g * 0.22f, col.b * 0.22f, 0.95f);
        bgImg.raycastTarget = false;
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta        = new Vector2(slotSize, slotSize);
        bgRt.anchoredPosition = Vector2.zero;

        // Icon (upper portion of tile)
        Sprite icon = PowerupIconRegistry.Instance?.GetIcon(info.type);
        if (icon != null)
        {
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(slotGO.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite         = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            var iconRt = iconGO.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.14f, 0.36f);
            iconRt.anchorMax = new Vector2(0.86f, 0.91f);
            iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
        }

        // Name label (bottom portion)
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(slotGO.transform, false);
        var labelTxt = labelGO.AddComponent<Text>();
        labelTxt.text        = label;
        labelTxt.font        = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelTxt.fontSize    = info.isInner ? 8 : 9;
        labelTxt.fontStyle   = FontStyle.Bold;
        labelTxt.alignment   = TextAnchor.LowerCenter;
        labelTxt.color       = Color.white;
        labelTxt.raycastTarget = false;
        var labelRt = labelGO.GetComponent<RectTransform>();
        if (icon != null)
        {
            labelRt.anchorMin = new Vector2(0.05f, 0.04f);
            labelRt.anchorMax = new Vector2(0.95f, 0.38f);
        }
        else
        {
            labelRt.anchorMin = new Vector2(0.05f, 0.18f);
            labelRt.anchorMax = new Vector2(0.95f, 0.82f);
        }
        labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;

        // Quantity badge (top-right, gold)
        var badgeGO = new GameObject("Badge");
        badgeGO.transform.SetParent(slotGO.transform, false);
        var badgeTxt = badgeGO.AddComponent<Text>();
        badgeTxt.text        = $"×{info.qty}";
        badgeTxt.font        = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        badgeTxt.fontSize    = info.isInner ? 11 : 13;
        badgeTxt.fontStyle   = FontStyle.Bold;
        badgeTxt.alignment   = TextAnchor.UpperRight;
        badgeTxt.color       = new Color(1f, 0.88f, 0.10f);
        badgeTxt.raycastTarget = false;
        var badgeRt = badgeGO.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0.52f, 0.60f);
        badgeRt.anchorMax = new Vector2(0.97f, 0.97f);
        badgeRt.offsetMin = badgeRt.offsetMax = Vector2.zero;

        _slotGOs.Add(slotGO);
        _slotRTs.Add(slotRt);
        _slotBgs.Add(bgImg);
    }

    // ── Decorative helpers ────────────────────────────────────────────────────

    private static void MakeCircleDecal(Transform parent, string name, Color color, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite       = GetCircleSprite();
        img.color        = color;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
    }

    private static void MakeRingDecal(Transform parent, string name, Color color, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite       = GetRingSprite();
        img.color        = color;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
    }

    private static void MakeCenterLabel(Transform parent, bool hasInnerRing)
    {
        var go = new GameObject("CenterLabel");
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text         = hasInnerRing ? "POWER-UPS" : "INVENTORY";
        txt.font         = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize     = 11;
        txt.fontStyle    = FontStyle.Bold;
        txt.alignment    = TextAnchor.MiddleCenter;
        txt.color        = new Color(1f, 1f, 1f, 0.28f);
        txt.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(110f, 28f);
        rt.anchoredPosition = hasInnerRing ? new Vector2(0f, 22f) : Vector2.zero;

        if (!hasInnerRing) return;

        // Second label "⚠ CURSED" below center for inner ring
        var go2 = new GameObject("CursedLabel");
        go2.transform.SetParent(parent, false);
        var txt2 = go2.AddComponent<Text>();
        txt2.text         = "⚠ CURSED";
        txt2.font         = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt2.fontSize     = 10;
        txt2.fontStyle    = FontStyle.Bold;
        txt2.alignment    = TextAnchor.MiddleCenter;
        txt2.color        = new Color(1f, 0.45f, 0.25f, 0.40f);
        txt2.raycastTarget = false;
        var rt2 = go2.GetComponent<RectTransform>();
        rt2.anchorMin = rt2.anchorMax = rt2.pivot = new Vector2(0.5f, 0.5f);
        rt2.sizeDelta        = new Vector2(90f, 24f);
        rt2.anchoredPosition = new Vector2(0f, -18f);
    }

    // ── Hover (mouse must physically be over a tile) ───────────────────────────

    private void UpdateHover()
    {
        if (_slotRTs.Count == 0) return;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        // Convert mouse to canvas units relative to the radialRoot's local space
        var rootRt = _radialRoot.GetComponent<RectTransform>();
        Vector2 mouseCanvas = ((Vector2)Input.mousePosition - screenCenter) / _canvas.scaleFactor - rootRt.anchoredPosition;

        int newHovered = -1;
        for (int i = 0; i < _slotRTs.Count; i++)
        {
            float radius = (_allSlots[i].isInner ? INNER_SLOT_SIZE : OUTER_SLOT_SIZE) * 0.5f;
            Vector2 delta = mouseCanvas - _slotRTs[i].anchoredPosition;
            if (delta.sqrMagnitude <= radius * radius)
            {
                newHovered = i;
                break;
            }
        }

        if (newHovered != _hoveredIndex)
        {
            _hoveredIndex = newHovered;
            RefreshHighlights();
        }
    }

    private void RefreshHighlights()
    {
        for (int i = 0; i < _slotBgs.Count; i++)
        {
            int   typeIdx  = (int)_allSlots[i].type;
            Color col      = typeIdx < TypeColors.Length ? TypeColors[typeIdx] : Color.white;
            bool  hovered  = (i == _hoveredIndex);

            _slotBgs[i].color            = hovered
                ? new Color(col.r * 0.72f, col.g * 0.72f, col.b * 0.72f, 1f)
                : new Color(col.r * 0.22f, col.g * 0.22f, col.b * 0.22f, 0.95f);
            _slotGOs[i].transform.localScale = hovered
                ? Vector3.one * HIGHLIGHT_SCALE
                : Vector3.one;
        }
    }

    // ── Close ─────────────────────────────────────────────────────────────────

    private void CloseRadial(bool activate)
    {
        if (!_isOpen) return;
        _isOpen = false;

        // Only activate if the cursor is actually hovering a tile
        PowerupType? activateType = null;
        if (activate && _hoveredIndex >= 0 && _hoveredIndex < _allSlots.Count)
            activateType = _allSlots[_hoveredIndex].type;

        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(AnimateClose(activateType));
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    private IEnumerator AnimateOpen()
    {
        if (_radialGroup == null) yield break;
        _radialRoot.transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < OPEN_DURATION)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / OPEN_DURATION);
            // Ease out with slight overshoot
            float s = 1f - Mathf.Pow(1f - p, 3f);
            _radialGroup.alpha                = Mathf.Clamp01(p * 3f);
            _radialRoot.transform.localScale  = Vector3.one * Mathf.Min(s * 1.06f, 1f + (1f - p) * 0.06f);
            yield return null;
        }
        _radialGroup.alpha               = 1f;
        _radialRoot.transform.localScale = Vector3.one;
        _animRoutine = null;
    }

    private IEnumerator AnimateClose(PowerupType? typeToActivate)
    {
        if (_radialGroup != null)
        {
            float t = 0f;
            while (t < CLOSE_DURATION)
            {
                t += Time.unscaledDeltaTime;
                float p = 1f - Mathf.Clamp01(t / CLOSE_DURATION);
                _radialGroup.alpha               = p;
                _radialRoot.transform.localScale = Vector3.one * p;
                yield return null;
            }
        }

        Time.timeScale = 1f;
        Cursor.visible = false;

        if (_radialRoot != null) { Destroy(_radialRoot); _radialRoot = null; _radialGroup = null; }
        _slotGOs.Clear();
        _slotRTs.Clear();
        _slotBgs.Clear();
        _allSlots.Clear();
        _hoveredIndex = -1;

        if (typeToActivate.HasValue)
            PurrBucksManager.Instance?.TryUseFromInventory(typeToActivate.Value);

        // First-time hint
        if (PurrBucksManager.Instance != null &&
            !PurrBucksManager.Instance.HasSeenTutorial("tut_radial_opened"))
        {
            PurrBucksManager.Instance.MarkTutorialSeen("tut_radial_opened");
            if (_hintRoutine != null) StopCoroutine(_hintRoutine);
            _hintRoutine = StartCoroutine(ShowHint("HOLD MMB  →  HOVER A POWER-UP  →  RELEASE TO USE"));
        }

        _animRoutine = null;
    }

    // ── Hint ──────────────────────────────────────────────────────────────────

    private IEnumerator ShowHint(string message)
    {
        var go = new GameObject("RadialHint");
        go.transform.SetParent(transform, false);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.10f, 0.20f, 0.90f);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(560f, 48f);
        rt.anchoredPosition = new Vector2(0f, -220f);

        var outline = go.AddComponent<Outline>();
        outline.effectColor    = new Color(1f, 0.78f, 0.10f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        var txtGO = new GameObject("Txt");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.text           = message;
        txt.font           = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize       = 15;
        txt.fontStyle      = FontStyle.Bold;
        txt.alignment      = TextAnchor.MiddleCenter;
        txt.color          = new Color(1f, 0.92f, 0.40f);
        txt.raycastTarget  = false;
        var txtRt = txtGO.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        float t = 0f;
        while (t < 0.3f) { t += Time.unscaledDeltaTime; cg.alpha = t / 0.3f; yield return null; }
        cg.alpha = 1f;
        yield return new WaitForSecondsRealtime(2.8f);
        t = 0f;
        while (t < 0.4f) { t += Time.unscaledDeltaTime; cg.alpha = 1f - t / 0.4f; yield return null; }

        Destroy(go);
        _hintRoutine = null;
    }

    // ── Procedural sprites ────────────────────────────────────────────────────

    private static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int D = 128;
        float r = D * 0.5f;
        var tex = new Texture2D(D, D, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[D * D];
        for (int y = 0; y < D; y++)
            for (int x = 0; x < D; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * D + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, D, D), new Vector2(0.5f, 0.5f));
        return _circleSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
        const int D = 128;
        float r = D * 0.5f, inner = r * 0.84f;
        var tex = new Texture2D(D, D, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[D * D];
        for (int y = 0; y < D; y++)
            for (int x = 0; x < D; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - dist) * Mathf.Clamp01(dist - inner);
                px[y * D + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, D, D), new Vector2(0.5f, 0.5f));
        return _ringSprite;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
