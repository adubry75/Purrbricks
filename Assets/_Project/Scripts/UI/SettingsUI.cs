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
    [SerializeField] private Sprite _applySprite;
    [SerializeField] private Sprite _backSprite;

    private Canvas _canvas;
    private bool   _fromPause;
    private Button _doneBtn;

    // Pending values (not applied until Apply is pressed)
    private int   _pendingResIdx;
    private int   _pendingDispIdx;
    private float _pendingMusic;
    private float _pendingSfx;

    private bool _pendingRelative, _pendingReduced;
    private float _pendingMouseSensitivity, _pendingGamepadSensitivity, _pendingDeadzone;
    private Slider _mouseSensitivitySlider, _gamepadSensitivitySlider, _deadzoneSlider;
    private Text _mouseValue, _gamepadValue, _deadzoneValue;
    private Image _absoluteBg, _relativeBg, _effectsFullBg, _effectsReducedBg;
    private readonly List<GameObject> _pages = new List<GameObject>();
    private readonly List<Image> _tabBgs = new List<Image>();

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
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 600;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        var bg = new GameObject("Bg"); bg.transform.SetParent(transform, false);
        bg.AddComponent<Image>().color = new Color(0f, 0.02f, 0.08f, 0.96f);
        StretchFull(bg.GetComponent<RectTransform>());
        var panel = new GameObject("Panel"); panel.transform.SetParent(transform, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(1100f, 980f);
        panel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.14f, 0.98f);
        var title = AddLabel(panel, "SETTINGS", new Vector2(0, 405), 64, UIStyle.AccentGold, true);
        title.alignment = TextAnchor.MiddleCenter;
        var tabs = MakeRow(panel, new Vector2(0, 310), 920, 62);
        string[] names = { "Display", "Audio", "Controls" };
        for (int i = 0; i < names.Length; i++)
        {
            int tab = i;
            var (_, tabBg) = MakeOptionButton(tabs.transform, names[i], () => ShowTab(tab));
            _tabBgs.Add(tabBg);
            var page = new GameObject(names[i] + "Page"); page.transform.SetParent(panel.transform, false);
            var rt = page.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(980, 580); rt.anchoredPosition = new Vector2(0, -25);
            _pages.Add(page);
        }
        var display = _pages[0];
        AddLabel(display, "RESOLUTION", new Vector2(0, 180), 30, LabelColor, true);
        var resRow = MakeRow(display, new Vector2(0, 115), 920, 62);
        for (int i = 0; i < SettingsManager.Resolutions.Length; i++)
        {
            int idx = i;
            var (btn, optionBg) = MakeOptionButton(resRow.transform, SettingsManager.Resolutions[i].label, () => OnResolutionSelected(idx));
            _resButtons.Add(btn); _resBgs.Add(optionBg);
        }
        AddLabel(display, "DISPLAY MODE", new Vector2(0, 5), 30, LabelColor, true);
        var modeRow = MakeRow(display, new Vector2(0, -60), 920, 62);
        for (int i = 0; i < SettingsManager.DisplayModes.Length; i++)
        {
            int idx = i;
            var (btn, optionBg) = MakeOptionButton(modeRow.transform, SettingsManager.DisplayModes[i].label, () => OnDisplayModeSelected(idx));
            _dispButtons.Add(btn); _dispBgs.Add(optionBg);
        }
        AddLabel(display, "Display changes are applied when you choose Done.", new Vector2(0, -185), 26, LabelColor, false);
        var audio = _pages[1];
        AddLabel(audio, "MUSIC VOLUME", new Vector2(0, 160), 30, LabelColor, true);
        _musicPct = AddValue(audio, new Vector2(0, 160));
        _musicSlider = MakeSlider(audio, new Vector2(0, 90), 820, UIStyle.AccentBlue, v =>
        {
            _pendingMusic = v; _musicPct.text = $"{Mathf.RoundToInt(v * 100)}%";
            MusicPlayer.Instance?.SetVolume(v);
        });
        AddLabel(audio, "SFX VOLUME", new Vector2(0, -40), 30, LabelColor, true);
        _sfxPct = AddValue(audio, new Vector2(0, -40));
        _sfxSlider = MakeSlider(audio, new Vector2(0, -110), 820, UIStyle.AccentGreen, v =>
        {
            _pendingSfx = v; _sfxPct.text = $"{Mathf.RoundToInt(v * 100)}%";
            SfxPlayer.Instance?.SetVolume(v);
        });
        var controls = _pages[2];
        AddLabel(controls, "MOUSE MOVEMENT", new Vector2(0, 240), 28, LabelColor, true);
        var mouseRow = MakeRow(controls, new Vector2(0, 184), 820, 52);
        (_, _absoluteBg) = MakeOptionButton(mouseRow.transform, "Absolute position", () => { _pendingRelative = false; RefreshComfort(); });
        (_, _relativeBg) = MakeOptionButton(mouseRow.transform, "Relative movement", () => { _pendingRelative = true; RefreshComfort(); });
        _mouseSensitivitySlider = ComfortSlider(controls, "RELATIVE MOUSE SENSITIVITY", 120, 0.25f, 3f,
            v => { _pendingMouseSensitivity = v; _mouseValue.text = $"{v:0.00}x"; }, out _mouseValue);
        _gamepadSensitivitySlider = ComfortSlider(controls, "CONTROLLER SENSITIVITY", 20, 0.25f, 2f,
            v => { _pendingGamepadSensitivity = v; _gamepadValue.text = $"{v:0.00}x"; }, out _gamepadValue);
        _deadzoneSlider = ComfortSlider(controls, "CONTROLLER DEADZONE", -80, 0.05f, 0.4f,
            v => { _pendingDeadzone = v; _deadzoneValue.text = $"{Mathf.RoundToInt(v * 100)}%"; }, out _deadzoneValue);
        AddLabel(controls, "SHAKE & FLASH", new Vector2(0, -180), 28, LabelColor, true);
        var effectsRow = MakeRow(controls, new Vector2(0, -236), 820, 52);
        (_, _effectsFullBg) = MakeOptionButton(effectsRow.transform, "Full effects", () => { _pendingReduced = false; RefreshComfort(); });
        (_, _effectsReducedBg) = MakeOptionButton(effectsRow.transform, "Reduced effects", () => { _pendingReduced = true; RefreshComfort(); });
        _doneBtn = UIStyle.CreateButton(panel.transform, "Done", new Vector2(0, -416), new Vector2(280, 72), OnDone, UIStyle.AccentGreen);
        ShowTab(0);
    }

    private Text AddValue(GameObject parent, Vector2 position)
    {
        var label = AddLabel(parent, "", position, 28, Color.white, false);
        label.alignment = TextAnchor.MiddleRight;
        return label;
    }

    private Slider ComfortSlider(GameObject parent, string label, float y, float min, float max,
        UnityEngine.Events.UnityAction<float> changed, out Text value)
    {
        AddLabel(parent, label, new Vector2(0, y), 28, LabelColor, true);
        value = AddValue(parent, new Vector2(0, y));
        var slider = MakeSlider(parent, new Vector2(0, y - 44), 820, UIStyle.AccentBlue, changed);
        slider.minValue = min; slider.maxValue = max;
        return slider;
    }

    private void ShowTab(int index)
    {
        for (int i = 0; i < _pages.Count; i++)
        {
            _pages[i].SetActive(i == index);
            _tabBgs[i].color = i == index ? BtnSelected : BtnNormal;
        }
    }

    private void RefreshComfort()
    {
        _absoluteBg.color = _pendingRelative ? BtnNormal : BtnSelected;
        _relativeBg.color = _pendingRelative ? BtnSelected : BtnNormal;
        _effectsFullBg.color = _pendingReduced ? BtnNormal : BtnSelected;
        _effectsReducedBg.color = _pendingReduced ? BtnSelected : BtnNormal;
    }

    public void Show(bool fromPause)
    {
        _fromPause = fromPause;
        gameObject.SetActive(true);
        UINavController.SetDefault(_doneBtn?.gameObject);

        var mgr = SettingsManager.Instance;
        if (mgr == null) return;

        _pendingResIdx  = mgr.ResolutionIndex;
        _pendingDispIdx = mgr.DisplayModeIndex;
        _pendingMusic   = mgr.MusicVolume;
        _pendingSfx     = mgr.SfxVolume;
        _pendingRelative = mgr.MouseRelative; _pendingReduced = mgr.ReducedEffects;
        _pendingMouseSensitivity = mgr.MouseSensitivity;
        _pendingGamepadSensitivity = mgr.GamepadSensitivity; _pendingDeadzone = mgr.GamepadDeadzone;
        _mouseSensitivitySlider.SetValueWithoutNotify(_pendingMouseSensitivity);
        _gamepadSensitivitySlider.SetValueWithoutNotify(_pendingGamepadSensitivity);
        _deadzoneSlider.SetValueWithoutNotify(_pendingDeadzone);
        _mouseValue.text = $"{_pendingMouseSensitivity:0.00}x";
        _gamepadValue.text = $"{_pendingGamepadSensitivity:0.00}x";
        _deadzoneValue.text = $"{Mathf.RoundToInt(_pendingDeadzone * 100)}%";
        RefreshComfort();

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

    private void OnDone()
    {
        var mgr = SettingsManager.Instance;
        if (mgr != null)
        {
            mgr.SetResolutionIndex(_pendingResIdx);
            mgr.SetDisplayModeIndex(_pendingDispIdx);
            mgr.SetMusicVolume(_pendingMusic);
            mgr.SetSfxVolume(_pendingSfx);
            mgr.SetMouseRelative(_pendingRelative); mgr.SetReducedEffects(_pendingReduced);
            mgr.SetMouseSensitivity(_pendingMouseSensitivity);
            mgr.SetGamepadSensitivity(_pendingGamepadSensitivity); mgr.SetGamepadDeadzone(_pendingDeadzone);
            mgr.ApplySettings();
        }

        Hide();
        if (_fromPause)
            GameManager.Instance?.ShowPauseMenu();
        else
            GameManager.Instance?.ShowMainMenu();
    }

    private void Update() { } // Input handled via action subscription

    private void OnEnable()
    {
        if (InputManager.Actions != null)
            InputManager.Actions.UI.CancelUI.performed += OnCancelUIPerformed;
    }

    private void OnDisable()
    {
        if (InputManager.Actions != null)
            InputManager.Actions.UI.CancelUI.performed -= OnCancelUIPerformed;
    }

    private void OnCancelUIPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        OnDone();
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
        txt.fontSize      = 28;
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
        rt.sizeDelta        = new Vector2(width, 44f);
        rt.anchoredPosition = pos;

        var slider      = go.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // Background track
        var bgGO  = new GameObject("Background");
        bgGO.transform.SetParent(go.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color         = new Color(0.10f, 0.12f, 0.22f, 1f);
        bgImg.raycastTarget = true;
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

        slider.SetValueWithoutNotify(1f);
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
