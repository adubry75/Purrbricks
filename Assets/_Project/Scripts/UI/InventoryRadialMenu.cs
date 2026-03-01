using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Middle-mouse radial selector for the powerup inventory.
/// Hold MMB to open, move mouse to choose slot, release to activate.
/// </summary>
public class InventoryRadialMenu : MonoBehaviour
{
    public static InventoryRadialMenu Instance { get; private set; }

    // Canvas
    private Canvas _canvas;

    // Radial state
    private bool _isOpen;
    private List<(PowerupType type, int qty)> _slots = new List<(PowerupType, int)>();
    private int _hoveredIndex = -1;
    private readonly List<GameObject> _slotGOs = new List<GameObject>();
    private readonly List<Image> _slotImages = new List<Image>();
    private readonly List<Text> _slotLabels = new List<Text>();
    private readonly List<Text> _slotQtyBadges = new List<Text>();

    // Animation
    private Coroutine _animRoutine;
    private GameObject _radialRoot;
    private CanvasGroup _radialGroup;

    // Config
    private const float RADIUS = 180f;
    private const float OPEN_DURATION = 0.12f;
    private const float CLOSE_DURATION = 0.08f;
    private const float HIGHLIGHT_SCALE = 1.15f;
    private const float SLOT_SIZE = 80f;

    // First-time hint
    private Coroutine _hintRoutine;

    // Colors from PowerupHUD (we inline the essential ones here)
    private static readonly Color[] TypeColors = new Color[]
    {
        new Color(0.00f, 0.85f, 1.00f), // WidePaddle (0)
        new Color(1.00f, 0.55f, 0.00f), // MultiBall (1)
        new Color(0.20f, 1.00f, 0.40f), // StickyBall (2)
        new Color(1.00f, 0.80f, 0.00f), // SpeedBall (3)
        new Color(1.00f, 0.20f, 0.20f), // ExtraLife (4)
        new Color(0.30f, 0.70f, 1.00f), // Laser (5)
        new Color(1.00f, 0.40f, 0.10f), // Fireball (6)
        new Color(1.00f, 0.65f, 0.10f), // BombBrick (7)
        new Color(0.10f, 0.90f, 0.90f), // ShieldWall (8)
        new Color(0.55f, 0.30f, 1.00f), // BigBall (9)
        new Color(1.00f, 0.85f, 0.00f), // ScoreFrenzy (10)
        new Color(0.90f, 0.20f, 0.20f), // ShrinkPaddle (11)
        new Color(0.80f, 0.10f, 0.80f), // ZipBall (12)
        new Color(0.70f, 0.00f, 1.00f), // FlipControls (13)
        new Color(0.50f, 0.00f, 0.90f), // CursedBall (14)
        new Color(0.80f, 0.30f, 0.30f), // TinyBall (15)
        new Color(0.40f, 0.40f, 0.40f), // InvisiBall (16)
        new Color(0.30f, 0.70f, 0.30f), // DrunkenPaddle (17)
        new Color(0.60f, 0.40f, 1.00f), // PermanentStickyBall (18)
        new Color(0.20f, 0.60f, 1.00f), // DrunkVision (19)
        new Color(0.10f, 0.80f, 0.50f), // GremlinBounces (20)
        new Color(0.80f, 0.10f, 0.10f), // FlipScreen (21)
    };

    private static readonly string[] TypeLabels = new string[]
    {
        "WIDE",       // WidePaddle (0)
        "MULTIBALL",  // MultiBall (1)
        "STICKY",     // StickyBall (2)
        "SPEED",      // SpeedBall (3)
        "EXTRA LIFE", // ExtraLife (4)
        "LASER",      // Laser (5)
        "FIREBALL",   // Fireball (6)
        "BOMB",       // BombBrick (7)
        "SHIELD",     // ShieldWall (8)
        "BIG BALL",   // BigBall (9)
        "FRENZY",     // ScoreFrenzy (10)
        "SHRINK",     // ShrinkPaddle (11)
        "ZIP",        // ZipBall (12)
        "FLIP CTRL",  // FlipControls (13)
        "CURSED",     // CursedBall (14)
        "TINY",       // TinyBall (15)
        "INVISI",     // InvisiBall (16)
        "DRUNK PDL",  // DrunkenPaddle (17)
        "PERM STICKY",// PermanentStickyBall (18)
        "DRUNK VIS",  // DrunkVision (19)
        "GREMLIN",    // GremlinBounces (20)
        "FLIP SCR",   // FlipScreen (21)
    };

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

        // Only allow radial in Playing or Ready states
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
            // Escape or right-click cancels without activating anything
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CloseRadial(activate: false);
            }
            else if (Input.GetMouseButtonUp(2))
            {
                CloseRadial(activate: true);
            }
            else
            {
                UpdateHover();
            }
        }
    }

    private void OpenRadial()
    {
        var inv = PurrBucksManager.Instance?.GetAllInventory();
        if (inv == null || inv.Count == 0) return;

        // Collect up to 10 types with qty > 0
        _slots.Clear();
        int count = 0;
        foreach (var kvp in inv)
        {
            if (kvp.Value > 0)
            {
                _slots.Add((kvp.Key, kvp.Value));
                count++;
                if (count >= 10) break;
            }
        }
        if (_slots.Count == 0) return;

        _isOpen = true;
        Time.timeScale = 0f;
        Cursor.visible = true;

        BuildSlots();

        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(AnimateOpen());
    }

    private void BuildSlots()
    {
        // Clear old slots
        if (_radialRoot != null) Destroy(_radialRoot);
        _slotGOs.Clear();
        _slotImages.Clear();
        _slotLabels.Clear();
        _slotQtyBadges.Clear();

        _radialRoot = new GameObject("RadialRoot");
        _radialRoot.transform.SetParent(transform, false);

        // Center on screen
        var rt = _radialRoot.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        _radialGroup = _radialRoot.AddComponent<CanvasGroup>();
        _radialGroup.alpha = 0f;
        _radialGroup.interactable = false;
        _radialGroup.blocksRaycasts = false;

        // Dark background disk
        var bgDisk = new GameObject("BgDisk");
        bgDisk.transform.SetParent(_radialRoot.transform, false);
        var bgImg = bgDisk.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        var bgRt = bgDisk.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.5f, 0.5f);
        bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        float diskSize = RADIUS * 2f + SLOT_SIZE + 20f;
        bgRt.sizeDelta = new Vector2(diskSize, diskSize);
        bgRt.anchoredPosition = Vector2.zero;

        int slotCount = _slots.Count;
        for (int i = 0; i < slotCount; i++)
        {
            float angle = (360f / slotCount) * i - 90f; // start from top
            float rad = angle * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(rad) * RADIUS, Mathf.Sin(rad) * RADIUS);

            var slotGO = new GameObject($"Slot_{i}");
            slotGO.transform.SetParent(_radialRoot.transform, false);

            var slotRt = slotGO.AddComponent<RectTransform>();
            slotRt.anchorMin = new Vector2(0.5f, 0.5f);
            slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.pivot = new Vector2(0.5f, 0.5f);
            slotRt.sizeDelta = new Vector2(SLOT_SIZE, SLOT_SIZE);
            slotRt.anchoredPosition = pos;

            // Background circle
            var bgSlot = slotGO.AddComponent<Image>();
            var ptype = _slots[i].type;
            int typeIndex = (int)ptype;
            Color col = typeIndex < TypeColors.Length ? TypeColors[typeIndex] : Color.white;
            bgSlot.color = new Color(col.r * 0.4f, col.g * 0.4f, col.b * 0.4f, 0.9f);

            // Color strip at top
            var strip = new GameObject("Strip");
            strip.transform.SetParent(slotGO.transform, false);
            var stripImg = strip.AddComponent<Image>();
            stripImg.color = col;
            var stripRt = strip.GetComponent<RectTransform>();
            stripRt.anchorMin = new Vector2(0f, 1f);
            stripRt.anchorMax = new Vector2(1f, 1f);
            stripRt.pivot = new Vector2(0.5f, 1f);
            stripRt.sizeDelta = new Vector2(0f, 6f);
            stripRt.anchoredPosition = Vector2.zero;

            // Name label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(slotGO.transform, false);
            var labelTxt = labelGO.AddComponent<Text>();
            string label = typeIndex < TypeLabels.Length ? TypeLabels[typeIndex] : ptype.ToString().ToUpper();
            labelTxt.text = label;
            labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelTxt.fontSize = 11;
            labelTxt.fontStyle = FontStyle.Bold;
            labelTxt.alignment = TextAnchor.MiddleCenter;
            labelTxt.color = Color.white;
            labelTxt.raycastTarget = false;
            var labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(2f, 10f);
            labelRt.offsetMax = new Vector2(-2f, -8f);

            // Qty badge (gold, top-right)
            var badgeGO = new GameObject("Badge");
            badgeGO.transform.SetParent(slotGO.transform, false);
            var badgeTxt = badgeGO.AddComponent<Text>();
            badgeTxt.text = $"×{_slots[i].qty}";
            badgeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            badgeTxt.fontSize = 14;
            badgeTxt.fontStyle = FontStyle.Bold;
            badgeTxt.alignment = TextAnchor.UpperRight;
            badgeTxt.color = new Color(1f, 0.85f, 0.10f);
            badgeTxt.raycastTarget = false;
            var badgeRt = badgeGO.GetComponent<RectTransform>();
            badgeRt.anchorMin = Vector2.zero;
            badgeRt.anchorMax = Vector2.one;
            badgeRt.offsetMin = new Vector2(2f, 2f);
            badgeRt.offsetMax = new Vector2(-2f, -2f);

            _slotGOs.Add(slotGO);
            _slotImages.Add(bgSlot);
            _slotLabels.Add(labelTxt);
            _slotQtyBadges.Add(badgeTxt);
        }

        _hoveredIndex = -1;
    }

    private void UpdateHover()
    {
        if (_slots.Count == 0) return;

        // Vector from screen center to mouse
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 delta = (Vector2)Input.mousePosition - screenCenter;

        // Only update hover if mouse has moved away from center
        if (delta.magnitude < 20f) return;

        float mouseAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        // Adjust: slots start from top (-90 deg), going clockwise
        // Our slot angle calc: angle = (360/n)*i - 90, using cos/sin (standard math coords)
        int slotCount = _slots.Count;
        float halfStep = 360f / slotCount * 0.5f;

        int newHovered = -1;
        float minDiff = float.MaxValue;
        for (int i = 0; i < slotCount; i++)
        {
            float slotAngle = (360f / slotCount) * i - 90f;
            float diff = Mathf.Abs(Mathf.DeltaAngle(mouseAngle, slotAngle));
            if (diff < halfStep && diff < minDiff)
            {
                minDiff = diff;
                newHovered = i;
            }
        }

        if (newHovered != _hoveredIndex)
        {
            _hoveredIndex = newHovered;
            RefreshSlotHighlights();
        }
    }

    private void RefreshSlotHighlights()
    {
        for (int i = 0; i < _slotGOs.Count; i++)
        {
            bool isHovered = (i == _hoveredIndex);
            int typeIndex = (int)_slots[i].type;
            Color col = typeIndex < TypeColors.Length ? TypeColors[typeIndex] : Color.white;

            if (isHovered)
            {
                _slotImages[i].color = new Color(col.r * 0.7f, col.g * 0.7f, col.b * 0.7f, 1f);
                _slotGOs[i].transform.localScale = Vector3.one * HIGHLIGHT_SCALE;
            }
            else
            {
                _slotImages[i].color = new Color(col.r * 0.4f, col.g * 0.4f, col.b * 0.4f, 0.9f);
                _slotGOs[i].transform.localScale = Vector3.one;
            }
        }
    }

    private void CloseRadial(bool activate)
    {
        if (!_isOpen) return;
        _isOpen = false;

        int activateIndex = _hoveredIndex;
        PowerupType? activateType = (activate && activateIndex >= 0 && activateIndex < _slots.Count)
            ? _slots[activateIndex].type
            : (PowerupType?)null;

        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(AnimateClose(activateType));
    }

    private IEnumerator AnimateOpen()
    {
        if (_radialGroup == null) yield break;

        float t = 0f;
        _radialRoot.transform.localScale = Vector3.zero;
        while (t < OPEN_DURATION)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / OPEN_DURATION);
            _radialGroup.alpha = p;
            _radialRoot.transform.localScale = Vector3.one * p;
            yield return null;
        }
        _radialGroup.alpha = 1f;
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
                _radialGroup.alpha = p;
                _radialRoot.transform.localScale = Vector3.one * p;
                yield return null;
            }
        }

        Time.timeScale = 1f;
        Cursor.visible = false;

        if (_radialRoot != null)
        {
            Destroy(_radialRoot);
            _radialRoot = null;
            _radialGroup = null;
        }
        _slotGOs.Clear();
        _slotImages.Clear();
        _slotLabels.Clear();
        _slotQtyBadges.Clear();
        _hoveredIndex = -1;

        // Activate powerup after restoring time
        if (typeToActivate.HasValue)
        {
            PurrBucksManager.Instance?.TryUseFromInventory(typeToActivate.Value);
        }

        // First-time hint
        if (PurrBucksManager.Instance != null &&
            !PurrBucksManager.Instance.HasSeenTutorial("tut_radial_opened"))
        {
            PurrBucksManager.Instance.MarkTutorialSeen("tut_radial_opened");
            if (_hintRoutine != null) StopCoroutine(_hintRoutine);
            _hintRoutine = StartCoroutine(ShowHint("HOLD MIDDLE MOUSE + MOVE → RELEASE TO ACTIVATE"));
        }

        _animRoutine = null;
    }

    private IEnumerator ShowHint(string message)
    {
        var go = new GameObject("RadialHint");
        go.transform.SetParent(transform, false);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.10f, 0.20f, 0.90f);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(520f, 48f);
        rt.anchoredPosition = new Vector2(0f, -160f);

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.78f, 0.10f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        var txtGO = new GameObject("Txt");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.text = message;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 15;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(1f, 0.92f, 0.40f);
        txt.raycastTarget = false;
        var txtRt = txtGO.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Fade in
        float t = 0f;
        while (t < 0.3f) { t += Time.unscaledDeltaTime; cg.alpha = t / 0.3f; yield return null; }
        cg.alpha = 1f;

        yield return new WaitForSecondsRealtime(2.5f);

        // Fade out
        t = 0f;
        while (t < 0.4f) { t += Time.unscaledDeltaTime; cg.alpha = 1f - t / 0.4f; yield return null; }

        Destroy(go);
        _hintRoutine = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
