using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the Fury Strike charge bar at the bottom of the playfield.
/// Reads RampFraction from the primary ball via GameManager.
/// When full, label pulses "FURY STRIKE  [ENTER]" in gold.
/// </summary>
public class HavocBar : MonoBehaviour
{
    public static HavocBar Instance { get; private set; }

    private Image _fill;
    private RectTransform _fillRt;
    private Text _readyLabel;
    private float _displayFraction;

    private static readonly Color ColorLow  = new Color(0.25f, 0.65f, 1.00f);
    private static readonly Color ColorMid  = new Color(1.00f, 0.55f, 0.05f);
    private static readonly Color ColorFull = new Color(1.00f, 0.18f, 0.18f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
    }

    private void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Container — bottom center, offset left to sit on playfield not powerup column
        var container = new GameObject("HavocContainer");
        container.transform.SetParent(transform, false);
        var cRt           = container.AddComponent<RectTransform>();
        cRt.anchorMin     = new Vector2(0.5f, 0f);
        cRt.anchorMax     = new Vector2(0.5f, 0f);
        cRt.pivot         = new Vector2(0.5f, 0f);
        cRt.sizeDelta     = new Vector2(580f, 30f);
        cRt.anchoredPosition = new Vector2(-160f, 12f);

        // ── Track (dark background) ───────────────────────────────────────────
        var trackGO  = new GameObject("Track");
        trackGO.transform.SetParent(container.transform, false);
        var trackImg  = trackGO.AddComponent<Image>();
        trackImg.color = new Color(0.04f, 0.07f, 0.14f, 0.92f);
        var trackRt   = trackGO.GetComponent<RectTransform>();
        trackRt.anchorMin  = Vector2.zero;
        trackRt.anchorMax  = Vector2.one;
        trackRt.sizeDelta  = Vector2.zero;

        var trackBorder = trackGO.AddComponent<Outline>();
        trackBorder.effectColor    = new Color(0.30f, 0.60f, 1f, 0.65f);
        trackBorder.effectDistance = new Vector2(1.5f, -1.5f);

        // ── Fill (anchor-based left-to-right progress bar) ────────────────────
        // anchorMax.x is driven by _displayFraction every Update — starts at 0 width.
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(trackGO.transform, false);
        _fill        = fillGO.AddComponent<Image>();
        _fill.color  = ColorLow;
        _fill.type   = Image.Type.Simple;
        _fillRt      = fillGO.GetComponent<RectTransform>();
        _fillRt.anchorMin = new Vector2(0f, 0f);
        _fillRt.anchorMax = new Vector2(0f, 1f); // 0-width to start; driven in Update
        _fillRt.offsetMin = new Vector2(2f, 2f);
        _fillRt.offsetMax = new Vector2(0f, -2f);

        // ── "FURY" watermark text inside bar ──────────────────────────────────
        var innerLbl    = new GameObject("InnerLabel");
        innerLbl.transform.SetParent(trackGO.transform, false);
        var innerTxt    = innerLbl.AddComponent<Text>();
        innerTxt.text      = "FURY";
        innerTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        innerTxt.fontSize  = 17;
        innerTxt.fontStyle = FontStyle.Bold;
        innerTxt.color     = new Color(1f, 1f, 1f, 0.30f);
        innerTxt.alignment = TextAnchor.MiddleLeft;
        var innerRt     = innerTxt.GetComponent<RectTransform>();
        innerRt.anchorMin  = Vector2.zero;
        innerRt.anchorMax  = Vector2.one;
        innerRt.offsetMin  = new Vector2(8f, 0f);

        // ── "FURY STRIKE [ENTER]" label above bar (gold, appears when full) ───
        var lblGO    = new GameObject("ReadyLabel");
        lblGO.transform.SetParent(container.transform, false);
        _readyLabel          = lblGO.AddComponent<Text>();
        _readyLabel.text     = InputHintService.Get(HintKey.FuryStrikeBar);
        _readyLabel.font     = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _readyLabel.fontSize = 21;
        _readyLabel.fontStyle = FontStyle.Bold;
        _readyLabel.color    = new Color(1f, 0.80f, 0.05f, 0f);
        _readyLabel.alignment = TextAnchor.MiddleCenter;
        var lblRt    = _readyLabel.GetComponent<RectTransform>();
        lblRt.anchorMin     = new Vector2(0f, 1f);
        lblRt.anchorMax     = new Vector2(1f, 1f);
        lblRt.pivot         = new Vector2(0.5f, 0f);
        lblRt.sizeDelta     = new Vector2(0f, 28f);
        lblRt.anchoredPosition = new Vector2(0f, 3f);

        var lblOut = lblGO.AddComponent<Outline>();
        lblOut.effectColor    = Color.black;
        lblOut.effectDistance = new Vector2(2f, -2f);
    }

    private void OnEnable()
    {
        InputManager.OnSchemeChanged += RefreshHints;
        // Sync hint text in case scheme was already set before subscription (e.g. gamepad connected at startup)
        RefreshHints(InputManager.CurrentScheme);
    }

    private void OnDisable()
    {
        InputManager.OnSchemeChanged -= RefreshHints;
    }

    private void RefreshHints(InputScheme _)
    {
        if (_readyLabel != null)
            _readyLabel.text = InputHintService.Get(HintKey.FuryStrikeBar);
    }

    private void Update()
    {
        float target = GameManager.Instance?.GetFuryChargeFraction() ?? 0f;

        _displayFraction = Mathf.Lerp(_displayFraction, target, Time.unscaledDeltaTime * 5f);

        // Grow fill bar left-to-right via anchorMax.x + color ramp
        if (_fill != null && _fillRt != null)
        {
            _fillRt.anchorMax = new Vector2(_displayFraction, 1f);

            Color fc;
            if (_displayFraction < 0.5f)
                fc = Color.Lerp(ColorLow, ColorMid, _displayFraction * 2f);
            else
                fc = Color.Lerp(ColorMid, ColorFull, (_displayFraction - 0.5f) * 2f);

            // Pulse brightness when full
            if (target >= 1f)
                fc *= 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 9f);

            _fill.color = fc;
        }

        // "FURY STRIKE [ENTER]" label
        if (_readyLabel != null)
        {
            float wantAlpha = target >= 1f ? 1f : 0f;
            float curAlpha  = _readyLabel.color.a;
            float newAlpha  = Mathf.Lerp(curAlpha, wantAlpha, Time.unscaledDeltaTime * 7f);

            if (target >= 1f)
                newAlpha *= 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 4.5f);

            _readyLabel.color = new Color(1f, 0.80f, 0.05f, newAlpha);
        }
    }
}
