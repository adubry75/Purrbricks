using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows active powerups in the right column with countdown bars.
/// Also shows the inventory (purchasable/dropped powerups) in a narrower column to its left.
/// </summary>
public class PowerupHUD : MonoBehaviour
{
    private Canvas _canvas;
    private RectTransform _listRoot;
    private RectTransform _invRoot;

    private static readonly Color[] TypeColors = new Color[]
    {
        // ── Good ──
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
        // ── Bad ──
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

    private static readonly string[] TypeLabels = new string[]
    {
        // ── Good ──
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
        // ── Bad ──
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

    private static bool IsBadPowerup(PowerupType type)
        => PowerupRules.IsBad(type);

    // Slot UI references for each active powerup
    private class Slot
    {
        public GameObject root;
        public Image timerBar;
        public Text timerText;
    }

    // Inventory slot UI references
    private class InvSlot
    {
        public GameObject root;
        public Text qtyText;
    }

    private readonly Dictionary<PowerupType, Slot>    _slots    = new Dictionary<PowerupType, Slot>();
    private readonly Dictionary<PowerupType, InvSlot> _invSlots = new Dictionary<PowerupType, InvSlot>();

    private void Awake()
    {
        BuildCanvas();
    }

    private void Start()
    {
        if (PowerupManager.Instance != null)
            PowerupManager.Instance.OnPowerupsChanged += Refresh;

        if (PowerupManager.Instance != null)
            PowerupManager.Instance.OnInventoryDrop += OnInventoryDrop;

        if (PurrBucksManager.Instance != null)
            PurrBucksManager.Instance.OnInventoryChanged += RefreshInventory;
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
        // Update timer bars and text every frame for smooth countdown
        if (PowerupManager.Instance != null)
        {
            foreach (var kvp in _slots)
            {
                float remaining = PowerupManager.Instance.GetRemaining(kvp.Key);
                if (float.IsInfinity(remaining))
                {
                    if (kvp.Value.timerBar != null)
                        kvp.Value.timerBar.fillAmount = 1f;
                    if (kvp.Value.timerText != null)
                        kvp.Value.timerText.text = "∞";
                    continue;
                }

                float fraction = remaining / PowerupManager.POWERUP_DURATION;
                if (kvp.Value.timerBar != null)
                    kvp.Value.timerBar.fillAmount = Mathf.Clamp01(fraction);
                if (kvp.Value.timerText != null)
                    kvp.Value.timerText.text = Mathf.CeilToInt(remaining).ToString();
            }
        }

        // Auto-show cursor when mouse is near the sidebar (right 220px)
        bool nearSidebar = Input.mousePosition.x > Screen.width - 220f;
        if (nearSidebar && GameManager.Instance != null &&
            (GameManager.Instance.State == GameState.Playing || GameManager.Instance.State == GameState.Ready))
        {
            Cursor.visible = true;
        }
    }

    private void Refresh()
    {
        if (PowerupManager.Instance == null) return;

        var active = PowerupManager.Instance.GetAllTimers();

        // Add new slots
        foreach (var kvp in active)
        {
            if (!_slots.ContainsKey(kvp.Key))
                AddSlot(kvp.Key);
        }

        // Remove expired slots
        var toRemove = new List<PowerupType>();
        foreach (var kvp in _slots)
        {
            if (!active.ContainsKey(kvp.Key))
                toRemove.Add(kvp.Key);
        }

        foreach (var type in toRemove)
        {
            if (_slots[type].root != null)
                Destroy(_slots[type].root);
            _slots.Remove(type);
        }

        RepositionSlots();
    }

    private void AddSlot(PowerupType type)
    {
        int idx = Mathf.Clamp((int)type, 0, TypeColors.Length - 1);
        Color color = TypeColors[idx];
        string label = idx < TypeLabels.Length ? TypeLabels[idx] : type.ToString().ToUpper();
        bool bad = IsBadPowerup(type);

        var slotGO = new GameObject($"Slot_{type}");
        slotGO.transform.SetParent(_listRoot, false);

        var slotRt = slotGO.AddComponent<RectTransform>();
        slotRt.sizeDelta = new Vector2(220f, 56f);

        // Background — red tint for bad powerups
        var bg = slotGO.AddComponent<Image>();
        bg.color = bad ? new Color(0.25f, 0f, 0f, 0.75f) : new Color(0f, 0f, 0f, 0.65f);

        // Label (no left strip — removed per design)
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(slotGO.transform, false);
        var labelText = labelGO.AddComponent<Text>();
        labelText.text = label;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 12;
        labelText.fontStyle = FontStyle.Bold;
        labelText.color = color;
        labelText.alignment = TextAnchor.UpperLeft;
        var labelRt = labelText.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0.5f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(6f, 0f);
        labelRt.offsetMax = new Vector2(-6f, 0f);

        // Timer text (top right)
        var timerTextGO = new GameObject("TimerText");
        timerTextGO.transform.SetParent(slotGO.transform, false);
        var timerTxt = timerTextGO.AddComponent<Text>();
        timerTxt.text = "10";
        timerTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerTxt.fontSize = 18;
        timerTxt.fontStyle = FontStyle.Bold;
        timerTxt.color = Color.white;
        timerTxt.alignment = TextAnchor.UpperRight;
        var timerTxtRt = timerTxt.GetComponent<RectTransform>();
        timerTxtRt.anchorMin = new Vector2(0f, 0.5f);
        timerTxtRt.anchorMax = new Vector2(1f, 1f);
        timerTxtRt.offsetMin = new Vector2(0f, 0f);
        timerTxtRt.offsetMax = new Vector2(-4f, 0f);

        // Timer bar track (lower half)
        var trackGO = new GameObject("Track");
        trackGO.transform.SetParent(slotGO.transform, false);
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = new Color(1f, 1f, 1f, 0.12f);
        var trackRt = trackGO.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0f, 0f);
        trackRt.anchorMax = new Vector2(1f, 0.48f);
        trackRt.offsetMin = new Vector2(10f, 4f);
        trackRt.offsetMax = new Vector2(-4f, 0f);

        // Timer bar fill
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(trackGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = color;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;
        var fillRt = fillImg.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;

        var slot = new Slot { root = slotGO, timerBar = fillImg, timerText = timerTxt };
        _slots[type] = slot;
    }

    private void RepositionSlots()
    {
        float yOffset = 0f;
        foreach (var kvp in _slots)
        {
            if (kvp.Value.root == null) continue;
            var rt = kvp.Value.root.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0f, -yOffset);
            yOffset += 64f;
        }
    }

    // ── Inventory Column ──────────────────────────────────────────────────────

    private void RefreshInventory()
    {
        if (PurrBucksManager.Instance == null) return;

        var inv = PurrBucksManager.Instance.GetAllInventory();

        // Add/update slots
        foreach (var kvp in inv)
        {
            if (_invSlots.ContainsKey(kvp.Key))
            {
                // Update qty
                if (_invSlots[kvp.Key].qtyText != null)
                    _invSlots[kvp.Key].qtyText.text = $"×{kvp.Value}";
            }
            else
            {
                AddInvSlot(kvp.Key, kvp.Value);
            }
        }

        // Remove slots for types no longer in inventory
        var toRemove = new List<PowerupType>();
        foreach (var kvp in _invSlots)
        {
            if (!inv.ContainsKey(kvp.Key))
                toRemove.Add(kvp.Key);
        }
        foreach (var type in toRemove)
        {
            if (_invSlots[type].root != null)
                Destroy(_invSlots[type].root);
            _invSlots.Remove(type);
        }

        RepositionInvSlots();
    }

    private void AddInvSlot(PowerupType type, int qty)
    {
        int idx = Mathf.Clamp((int)type, 0, TypeColors.Length - 1);
        Color color = TypeColors[idx];
        string label = idx < TypeLabels.Length ? TypeLabels[idx] : type.ToString().ToUpper();

        var slotGO = new GameObject($"InvSlot_{type}");
        slotGO.transform.SetParent(_invRoot, false);

        var slotRt = slotGO.AddComponent<RectTransform>();
        slotRt.sizeDelta = new Vector2(76f, 52f);

        // Background
        var bg = slotGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);

        // Button to use from inventory
        var btn = slotGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        var cols = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        cols.pressedColor     = new Color(0.8f, 0.8f, 0.8f);
        btn.colors = cols;
        var capturedType = type;
        btn.onClick.AddListener(() => PurrBucksManager.Instance?.TryUseFromInventory(capturedType));

        // Name label (abbreviated) — no top strip
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(slotGO.transform, false);
        var labelTxt = labelGO.AddComponent<Text>();
        // Use a short label (first 8 chars or up to the first space/symbol)
        string shortLabel = label.Replace("⚠ ", "").Replace("+ ", "+");
        if (shortLabel.Length > 9) shortLabel = shortLabel.Substring(0, 9);
        labelTxt.text = shortLabel;
        labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelTxt.fontSize = 10;
        labelTxt.fontStyle = FontStyle.Bold;
        labelTxt.alignment = TextAnchor.UpperCenter;
        labelTxt.color = color;
        labelTxt.raycastTarget = false;
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0.45f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(2f, -6f);
        labelRt.offsetMax = new Vector2(-2f, -2f);

        // Qty badge (gold, bottom center)
        var badgeGO = new GameObject("Qty");
        badgeGO.transform.SetParent(slotGO.transform, false);
        var badgeTxt = badgeGO.AddComponent<Text>();
        badgeTxt.text = $"×{qty}";
        badgeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        badgeTxt.fontSize = 16;
        badgeTxt.fontStyle = FontStyle.Bold;
        badgeTxt.alignment = TextAnchor.LowerCenter;
        badgeTxt.color = new Color(1f, 0.85f, 0.10f);
        badgeTxt.raycastTarget = false;
        var badgeRt = badgeGO.GetComponent<RectTransform>();
        badgeRt.anchorMin = Vector2.zero;
        badgeRt.anchorMax = new Vector2(1f, 0.5f);
        badgeRt.offsetMin = new Vector2(2f, 2f);
        badgeRt.offsetMax = new Vector2(-2f, 0f);

        _invSlots[type] = new InvSlot { root = slotGO, qtyText = badgeTxt };
    }

    private void RepositionInvSlots()
    {
        float yOffset = 0f;
        foreach (var kvp in _invSlots)
        {
            if (kvp.Value.root == null) continue;
            var rt = kvp.Value.root.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0f, -yOffset);
            yOffset += 58f;
        }
    }

    // ── Inventory Drop VFX ────────────────────────────────────────────────────

    private void OnInventoryDrop(PowerupType type)
    {
        StartCoroutine(InventoryDropFlyIn(type));
        RefreshInventory();
    }

    private IEnumerator InventoryDropFlyIn(PowerupType type)
    {
        int idx = Mathf.Clamp((int)type, 0, TypeColors.Length - 1);
        Color color = TypeColors[idx];
        string label = idx < TypeLabels.Length ? TypeLabels[idx] : type.ToString();

        // Create a temp floating label
        var go = new GameObject("InvDropFlyIn");
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 36f);
        rt.anchoredPosition = new Vector2(-5f, 0f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);
        bg.raycastTarget = false;

        var txtGO = new GameObject("Txt");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.text = $"{label}  → INV";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 14;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleRight;
        txt.color = color;
        txt.raycastTarget = false;
        var txtRt = txtGO.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(4f, 0f);
        txtRt.offsetMax = new Vector2(-8f, 0f);

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Fade in + slide left
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(-30f, 0f);
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / 0.25f);
            cg.alpha = p;
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, p);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1.0f);

        // Slide out to the right + fade
        Vector2 exitPos = endPos + new Vector2(40f, 0f);
        t = 0f;
        while (t < 0.45f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / 0.45f);
            cg.alpha = 1f - p;
            rt.anchoredPosition = Vector2.Lerp(endPos, exitPos, p);
            yield return null;
        }

        Destroy(go);
    }

    // ── Canvas Builder ────────────────────────────────────────────────────────

    private void BuildCanvas()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // ── Active Powerup column (right, 230px wide) ─────────────────────────
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(transform, false);
        var header = headerGO.AddComponent<Text>();
        header.text = "POWERUPS";
        header.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        header.fontSize = 18;
        header.fontStyle = FontStyle.Bold;
        header.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        header.alignment = TextAnchor.UpperLeft;
        var headerRt = header.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(1f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot     = new Vector2(1f, 1f);
        headerRt.sizeDelta = new Vector2(230f, 30f);
        headerRt.anchoredPosition = new Vector2(-5f, -10f);

        // List root: anchored to top-right
        var listRootGO = new GameObject("SlotList");
        listRootGO.transform.SetParent(transform, false);
        _listRoot = listRootGO.AddComponent<RectTransform>();
        _listRoot.anchorMin = new Vector2(1f, 1f);
        _listRoot.anchorMax = new Vector2(1f, 1f);
        _listRoot.pivot     = new Vector2(1f, 1f);
        _listRoot.anchoredPosition = new Vector2(-5f, -42f);
        _listRoot.sizeDelta = new Vector2(230f, 900f);

        // ── Inventory column (left of active, 80px wide, 3px gap) ─────────────
        // Active column left edge: -5 - 230 = -235 from canvas right.
        // Inventory right edge: -235 - 3 = -238.
        var invHeaderGO = new GameObject("InvHeader");
        invHeaderGO.transform.SetParent(transform, false);
        var invHeader = invHeaderGO.AddComponent<Text>();
        invHeader.text = "INV";
        invHeader.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        invHeader.fontSize = 18;
        invHeader.fontStyle = FontStyle.Bold;
        invHeader.color = new Color(1f, 0.85f, 0.10f, 0.8f);
        invHeader.alignment = TextAnchor.UpperCenter;
        var invHeaderRt = invHeader.GetComponent<RectTransform>();
        invHeaderRt.anchorMin = new Vector2(1f, 1f);
        invHeaderRt.anchorMax = new Vector2(1f, 1f);
        invHeaderRt.pivot     = new Vector2(1f, 1f);
        invHeaderRt.sizeDelta = new Vector2(80f, 30f);
        invHeaderRt.anchoredPosition = new Vector2(-238f, -10f);

        var invRootGO = new GameObject("InvList");
        invRootGO.transform.SetParent(transform, false);
        _invRoot = invRootGO.AddComponent<RectTransform>();
        _invRoot.anchorMin = new Vector2(1f, 1f);
        _invRoot.anchorMax = new Vector2(1f, 1f);
        _invRoot.pivot     = new Vector2(1f, 1f);
        _invRoot.anchoredPosition = new Vector2(-238f, -42f);
        _invRoot.sizeDelta = new Vector2(80f, 900f);
    }
}
