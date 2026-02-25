using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen settings overlay.
/// Handles resolution, display mode, music volume, and SFX volume.
/// Call Show(fromPause: true/false) so the Back button knows where to return.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    private Canvas _canvas;
    private bool   _fromPause;

    // Pending values (not applied until Apply is pressed)
    private int   _pendingResIdx;
    private int   _pendingDispIdx;
    private float _pendingMusic;
    private float _pendingSfx;

    // Resolution selector buttons
    private readonly List<Button> _resButtons  = new List<Button>();
    private readonly List<Image>  _resBgs      = new List<Image>();

    // Display mode selector buttons
    private readonly List<Button> _dispButtons = new List<Button>();
    private readonly List<Image>  _dispBgs     = new List<Image>();

    // Sliders
    private Slider _musicSlider;
    private Slider _sfxSlider;
    private Text   _musicPct;
    private Text   _sfxPct;

    // Style
    private static readonly Color BtnNormal   = new Color(0.07f, 0.10f, 0.20f, 0.90f);
    private static readonly Color BtnSelected = new Color(0.10f, 0.38f, 0.85f, 0.95f);
    private static readonly Color LabelColor  = new Color(0.55f, 0.75f, 1f,   0.85f);

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 600;   // above everything

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Full-screen dark backdrop
        var bg = new GameObject("Bg");
        bg.transform.SetParent(transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0.02f, 0.08f, 0.96f);
        StretchFull(bgImg.GetComponent<RectTransform>());

        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta        = new Vector2(960f, 860f);
        panelRt.anchoredPosition = Vector2.zero;
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.07f, 0.14f, 0.98f);
        var panelOl = panel.AddComponent<Outline>();
        panelOl.effectColor    = new Color(0.25f, 0.50f, 1f, 0.45f);
        panelOl.effectDistance = new Vector2(2f, -2f);

        float y = 370f;

        // ── Title ─────────────────────────────────────────────────────────────
        AddLabel(panel, "SETTINGS", new Vector2(0f, y), 72, UIStyle.AccentGold, bold: true);
        y -= 90f;

        // ── Resolution ────────────────────────────────────────────────────────
        AddLabel(panel, "RESOLUTION", new Vector2(0f, y), 28, LabelColor, bold: true);
        y -= 50f;

        var resRow = MakeRow(panel, new Vector2(0f, y), 900f, 52f);
        for (int i = 0; i < SettingsManager.Resolutions.Length; i++)
        {
            int idx = i;
            var (_, _, label) = SettingsManager.Resolutions[i];
            var (btn, bgImg2) = MakeOptionButton(resRow.transform, label, () => OnResolutionSelected(idx));
            _resButtons.Add(btn);
            _resBgs.Add(bgImg2);
        }
        y -= 78f;

        // ── Display Mode ──────────────────────────────────────────────────────
        AddLabel(panel, "DISPLAY MODE", new Vector2(0f, y), 28, LabelColor, bold: true);
        y -= 50f;

        var dispRow = MakeRow(panel, new Vector2(0f, y), 680f, 52f);
        for (int i = 0; i < SettingsManager.DisplayModes.Length; i++)
        {
            int idx = i;
            var (_, label) = SettingsManager.DisplayModes[i];
            var (btn, bgImg2) = MakeOptionButton(dispRow.transform, label, () => OnDisplayModeSelected(idx));
            _dispButtons.Add(btn);
            _dispBgs.Add(bgImg2);
        }
        y -= 90f;

        // ── Music Volume ──────────────────────────────────────────────────────
        AddLabel(panel, "MUSIC VOLUME", new Vector2(0f, y), 28, LabelColor, bold: true);
        _musicPct = AddLabel(panel, "50%", new Vector2(380f, y), 28, Color.white, bold: false);
        y -= 46f;
        _musicSlider = MakeSlider(panel, new Vector2(0f, y), 820f, UIStyle.AccentBlue, v =>
        {
            _pendingMusic = v;
            if (_musicPct != null) _musicPct.text = $"{Mathf.RoundToInt(v * 100f)}%";
            // Live preview
            MusicPlayer.Instance?.SetVolume(v);
        });
        y -= 80f;

        // ── SFX Volume ────────────────────────────────────────────────────────
        AddLabel(panel, "SFX VOLUME", new Vector2(0f, y), 28, LabelColor, bold: true);
        _sfxPct = AddLabel(panel, "70%", new Vector2(380f, y), 28, Color.white, bold: false);
        y -= 46f;
        _sfxSlider = MakeSlider(panel, new Vector2(0f, y), 820f, UIStyle.AccentGreen, v =>
        {
            _pendingSfx = v;
            if (_sfxPct != null) _sfxPct.text = $"{Mathf.RoundToInt(v * 100f)}%";
            SfxPlayer.Instance?.SetVolume(v);
        });
        y -= 90f;

        // ── Buttons ───────────────────────────────────────────────────────────
        UIStyle.CreateButton(panel.transform, "Apply",
            new Vector2(-145f, y), new Vector2(260f, 68f),
            OnApply, UIStyle.AccentGreen);

        UIStyle.CreateButton(panel.transform, "Back",
            new Vector2( 145f, y), new Vector2(260f, 68f),
            OnBack, UIStyle.AccentBlue);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(bool fromPause)
    {
        _fromPause = fromPause;
        gameObject.SetActive(true);

        var mgr = SettingsManager.Instance;
        if (mgr == null) return;

        _pendingResIdx  = mgr.ResolutionIndex;
        _pendingDispIdx = mgr.DisplayModeIndex;
        _pendingMusic   = mgr.MusicVolume;
        _pendingSfx     = mgr.SfxVolume;

        RefreshSelectors();

        if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(_pendingMusic);
        if (_sfxSlider   != null) _sfxSlider.SetValueWithoutNotify(_pendingSfx);
        if (_musicPct    != null) _musicPct.text = $"{Mathf.RoundToInt(_pendingMusic * 100f)}%";
        if (_sfxPct      != null) _sfxPct.text   = $"{Mathf.RoundToInt(_pendingSfx   * 100f)}%";
    }

    public void Hide() { gameObject.SetActive(false); }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnResolutionSelected(int idx)
    {
        _pendingResIdx = idx;
        RefreshResButtons();
    }

    private void OnDisplayModeSelected(int idx)
    {
        _pendingDispIdx = idx;
        RefreshDispButtons();
    }

    private void OnApply()
    {
        var mgr = SettingsManager.Instance;
        if (mgr == null) return;

        mgr.SetResolutionIndex(_pendingResIdx);
        mgr.SetDisplayModeIndex(_pendingDispIdx);
        mgr.SetMusicVolume(_pendingMusic);
        mgr.SetSfxVolume(_pendingSfx);
        mgr.ApplySettings();
    }

    private void OnBack()
    {
        // Revert any live-previewed volume changes back to saved values
        var mgr = SettingsManager.Instance;
        if (mgr != null)
        {
            MusicPlayer.Instance?.SetVolume(mgr.MusicVolume);
            SfxPlayer.Instance?.SetVolume(mgr.SfxVolume);
        }

        Hide();
        if (_fromPause)
            GameManager.Instance?.ShowPauseMenu();
        else
            GameManager.Instance?.ShowMainMenu();
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Escape)) OnBack();
    }

    // ── Selector refresh ──────────────────────────────────────────────────────

    private void RefreshSelectors()
    {
        RefreshResButtons();
        RefreshDispButtons();
    }

    private void RefreshResButtons()
    {
        for (int i = 0; i < _resBgs.Count; i++)
            if (_resBgs[i] != null) _resBgs[i].color = i == _pendingResIdx ? BtnSelected : BtnNormal;
    }

    private void RefreshDispButtons()
    {
        for (int i = 0; i < _dispBgs.Count; i++)
            if (_dispBgs[i] != null) _dispBgs[i].color = i == _pendingDispIdx ? BtnSelected : BtnNormal;
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private Text AddLabel(GameObject parent, string text, Vector2 pos, int fontSize, Color color, bool bold)
    {
        var go  = new GameObject("Lbl_" + text);
        go.transform.SetParent(parent.transform, false);
        var txt = go.AddComponent<Text>();
        txt.text          = text;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = fontSize;
        txt.fontStyle     = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.alignment     = TextAnchor.MiddleLeft;
        txt.color         = color;
        txt.raycastTarget = false;
        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(820f, fontSize + 12f);
        rt.anchoredPosition = pos;
        return txt;
    }

    private GameObject MakeRow(GameObject parent, Vector2 pos, float width, float height)
    {
        var go = new GameObject("Row");
        go.transform.SetParent(parent.transform, false);
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = true;
        layout.spacing                = 10f;
        layout.padding                = new RectOffset(0, 0, 0, 0);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(width, height);
        rt.anchoredPosition = pos;
        return go;
    }

    private (Button btn, Image bg) MakeOptionButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go  = new GameObject("Opt_" + label);
        go.transform.SetParent(parent, false);
        var bg  = go.AddComponent<Image>();
        bg.color = BtnNormal;
        var ol  = go.AddComponent<Outline>();
        ol.effectColor    = new Color(0.35f, 0.60f, 1f, 0.30f);
        ol.effectDistance = new Vector2(1f, -1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(onClick);
        var cols = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        cols.pressedColor     = new Color(0.85f, 0.85f, 0.85f);
        btn.colors = cols;

        var txtGO = new GameObject("Lbl");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.text          = label;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = 22;
        txt.fontStyle     = FontStyle.Bold;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.color         = Color.white;
        txt.raycastTarget = false;
        var txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = txtRt.anchoredPosition = Vector2.zero;

        return (btn, bg);
    }

    private Slider MakeSlider(GameObject parent, Vector2 pos, float width, Color fillColor,
        UnityEngine.Events.UnityAction<float> onChanged)
    {
        var go = new GameObject("Slider");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(width, 36f);
        rt.anchoredPosition = pos;

        var slider      = go.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // Background track
        var bgGO  = new GameObject("Background");
        bgGO.transform.SetParent(go.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color         = new Color(0.10f, 0.12f, 0.22f, 1f);
        bgImg.raycastTarget = false;
        var bgRt  = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.25f);
        bgRt.anchorMax = new Vector2(1f, 0.75f);
        bgRt.sizeDelta = bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        // Fill area
        var faGO = new GameObject("Fill Area");
        faGO.transform.SetParent(go.transform, false);
        var faRt = faGO.AddComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.25f);
        faRt.anchorMax = new Vector2(1f, 0.75f);
        faRt.offsetMin = new Vector2(5f, 0f);
        faRt.offsetMax = new Vector2(-15f, 0f);

        var fillGO  = new GameObject("Fill");
        fillGO.transform.SetParent(faGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color         = fillColor;
        fillImg.raycastTarget = false;
        var fillRt  = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.sizeDelta = new Vector2(10f, 0f);
        slider.fillRect  = fillRt;

        // Handle slide area
        var hsaGO = new GameObject("Handle Slide Area");
        hsaGO.transform.SetParent(go.transform, false);
        var hsaRt = hsaGO.AddComponent<RectTransform>();
        hsaRt.anchorMin = Vector2.zero;
        hsaRt.anchorMax = Vector2.one;
        hsaRt.offsetMin = new Vector2(10f, 0f);
        hsaRt.offsetMax = new Vector2(-10f, 0f);

        var handleGO  = new GameObject("Handle");
        handleGO.transform.SetParent(hsaGO.transform, false);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = Color.white;
        var handleRt  = handleGO.GetComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0f, 0f);
        handleRt.anchorMax = new Vector2(0f, 1f);
        handleRt.sizeDelta = new Vector2(24f, 0f);
        slider.handleRect    = handleRt;
        slider.targetGraphic = handleImg;

        slider.onValueChanged.AddListener(onChanged);

        return slider;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.sizeDelta        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
