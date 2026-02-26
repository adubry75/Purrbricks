using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows active powerups in the right column with countdown bars.
/// Each slot shows: colored icon + name + time remaining bar.
/// </summary>
public class PowerupHUD : MonoBehaviour
{
    private Canvas _canvas;
    private RectTransform _listRoot;

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
        "⚠ DRUNK VISION",// 19
        "⚠ GREMLIN",     // 20
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

    private readonly Dictionary<PowerupType, Slot> _slots = new Dictionary<PowerupType, Slot>();

    private void Awake()
    {
        BuildCanvas();
    }

    private void Start()
    {
        if (PowerupManager.Instance != null)
            PowerupManager.Instance.OnPowerupsChanged += Refresh;
    }

    private void OnDestroy()
    {
        if (PowerupManager.Instance != null)
            PowerupManager.Instance.OnPowerupsChanged -= Refresh;
    }

    private void Update()
    {
        // Update timer bars and text every frame for smooth countdown
        if (PowerupManager.Instance == null) return;

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

            float fraction  = remaining / PowerupManager.POWERUP_DURATION;

            if (kvp.Value.timerBar != null)
                kvp.Value.timerBar.fillAmount = Mathf.Clamp01(fraction);

            if (kvp.Value.timerText != null)
                kvp.Value.timerText.text = Mathf.CeilToInt(remaining).ToString();
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
        slotRt.sizeDelta = new Vector2(200f, 56f);

        // Background — red tint for bad powerups
        var bg = slotGO.AddComponent<Image>();
        bg.color = bad ? new Color(0.25f, 0f, 0f, 0.75f) : new Color(0f, 0f, 0f, 0.65f);

        // Left color accent strip
        var strip = new GameObject("Strip");
        strip.transform.SetParent(slotGO.transform, false);
        var stripImg = strip.AddComponent<Image>();
        stripImg.color = color;
        var stripRt = strip.GetComponent<RectTransform>();
        stripRt.anchorMin = new Vector2(0f, 0f);
        stripRt.anchorMax = new Vector2(0f, 1f);
        stripRt.sizeDelta = new Vector2(6f, 0f);
        stripRt.anchoredPosition = new Vector2(3f, 0f);

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(slotGO.transform, false);
        var labelText = labelGO.AddComponent<Text>();
        labelText.text = label;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 15;
        labelText.fontStyle = FontStyle.Bold;
        labelText.color = color;
        labelText.alignment = TextAnchor.UpperLeft;
        var labelRt = labelText.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0.5f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(12f, 0f);
        labelRt.offsetMax = new Vector2(-8f, 0f);

        // Timer text (top right)
        var timerTextGO = new GameObject("TimerText");
        timerTextGO.transform.SetParent(slotGO.transform, false);
        var timerTxt = timerTextGO.AddComponent<Text>();
        timerTxt.text = "10";
        timerTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerTxt.fontSize = 20;
        timerTxt.fontStyle = FontStyle.Bold;
        timerTxt.color = Color.white;
        timerTxt.alignment = TextAnchor.UpperRight;
        var timerTxtRt = timerTxt.GetComponent<RectTransform>();
        timerTxtRt.anchorMin = new Vector2(0f, 0.5f);
        timerTxtRt.anchorMax = new Vector2(1f, 1f);
        timerTxtRt.offsetMin = new Vector2(0f, 0f);
        timerTxtRt.offsetMax = new Vector2(-6f, 0f);

        // Timer bar track (lower half)
        var trackGO = new GameObject("Track");
        trackGO.transform.SetParent(slotGO.transform, false);
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = new Color(1f, 1f, 1f, 0.12f);
        var trackRt = trackGO.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0f, 0f);
        trackRt.anchorMax = new Vector2(1f, 0.48f);
        trackRt.offsetMin = new Vector2(10f, 4f);
        trackRt.offsetMax = new Vector2(-6f, 0f);

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

    private void BuildCanvas()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Header label
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(transform, false);
        var header = headerGO.AddComponent<Text>();
        header.text = "POWERUPS";
        header.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        header.fontSize = 22;
        header.fontStyle = FontStyle.Bold;
        header.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        header.alignment = TextAnchor.UpperLeft;
        var headerRt = header.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(1f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot     = new Vector2(1f, 1f);
        headerRt.sizeDelta = new Vector2(210f, 30f);
        headerRt.anchoredPosition = new Vector2(-5f, -10f);

        // List root: anchored to top-right
        var listRootGO = new GameObject("SlotList");
        listRootGO.transform.SetParent(transform, false);
        _listRoot = listRootGO.AddComponent<RectTransform>();
        _listRoot.anchorMin = new Vector2(1f, 1f);
        _listRoot.anchorMax = new Vector2(1f, 1f);
        _listRoot.pivot     = new Vector2(1f, 1f);
        _listRoot.anchoredPosition = new Vector2(-5f, -42f);
        _listRoot.sizeDelta = new Vector2(210f, 600f);
    }
}
