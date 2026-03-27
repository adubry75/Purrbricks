using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overlay that appears when the player presses G during Ready/Playing/Paused.
/// The player types a 4-letter level code and presses Enter to warp to that level.
/// </summary>
public class LevelCodeEntryUI : MonoBehaviour
{
    private Canvas     _canvas;
    private InputField _input;
    private Text       _errorText;
    private bool       _visible;

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    // ── UI construction ────────────────────────────────────────────────────────

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500; // above everything

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        // Dark backdrop — dims the game behind the dialog
        var backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(transform, false);
        var bdImg = backdrop.AddComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.72f);
        var bdRt = backdrop.GetComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero;
        bdRt.anchorMax = Vector2.one;
        bdRt.sizeDelta = Vector2.zero;

        // Dialog box
        var box = new GameObject("Box");
        box.transform.SetParent(transform, false);
        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.05f, 0.08f, 0.15f, 0.97f);
        var boxOl = box.AddComponent<Outline>();
        boxOl.effectColor    = new Color(0.35f, 0.60f, 1f, 0.55f);
        boxOl.effectDistance = new Vector2(2f, -2f);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin        = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax        = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta        = new Vector2(520f, 260f);
        boxRt.anchoredPosition = Vector2.zero;

        // Title
        CreateLabel(box, "ENTER LEVEL CODE", new Vector2(0f, 84f), 44, UIStyle.AccentBlue);

        // Subtitle hint
        CreateLabel(box, "Codes are shown in your HUD during gameplay", new Vector2(0f, 36f), 20,
            new Color(0.55f, 0.65f, 0.75f, 0.80f));

        // Input field
        var inputGO = new GameObject("CodeInput");
        inputGO.transform.SetParent(box.transform, false);

        var inputImg = inputGO.AddComponent<Image>();
        inputImg.color = new Color(0.08f, 0.14f, 0.26f, 0.97f);
        var inputOl = inputGO.AddComponent<Outline>();
        inputOl.effectColor    = new Color(0.35f, 0.70f, 1f, 0.65f);
        inputOl.effectDistance = new Vector2(2f, -2f);

        _input = inputGO.AddComponent<InputField>();
        _input.textComponent  = CreateInputText(inputGO);
        _input.characterLimit = 5;
        _input.text           = "";

        var inputRt = inputGO.GetComponent<RectTransform>();
        inputRt.anchorMin        = new Vector2(0.5f, 0.5f);
        inputRt.anchorMax        = new Vector2(0.5f, 0.5f);
        inputRt.sizeDelta        = new Vector2(220f, 62f);
        inputRt.anchoredPosition = new Vector2(0f, -14f);

        // Error text (hidden by default)
        var errorGO = new GameObject("ErrorText");
        errorGO.transform.SetParent(box.transform, false);
        _errorText           = errorGO.AddComponent<Text>();
        _errorText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _errorText.fontSize  = 22;
        _errorText.fontStyle = FontStyle.Bold;
        _errorText.alignment = TextAnchor.MiddleCenter;
        _errorText.color     = UIStyle.AccentRed;
        _errorText.text      = "";
        var errorRt = errorGO.GetComponent<RectTransform>();
        errorRt.anchorMin        = new Vector2(0.5f, 0.5f);
        errorRt.anchorMax        = new Vector2(0.5f, 0.5f);
        errorRt.sizeDelta        = new Vector2(480f, 32f);
        errorRt.anchoredPosition = new Vector2(0f, -64f);

        // Cancel hint
        CreateLabel(box, "ENTER to warp  ·  ESC to cancel", new Vector2(0f, -96f), 18,
            new Color(0.45f, 0.52f, 0.60f, 0.75f));
    }

    private Text CreateInputText(GameObject parent)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent.transform, false);
        var txt = go.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 42;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = Color.white;
        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 0f);
        rt.offsetMax = new Vector2(-8f, 0f);
        return txt;
    }

    private void CreateLabel(GameObject parent, string text, Vector2 pos, int fontSize, Color color)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent.transform, false);
        var txt = go.AddComponent<Text>();
        txt.text      = text;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = color;
        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(500f, fontSize + 12f);
        rt.anchoredPosition = pos;
    }

    // ── Input handling ─────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_visible) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
            GameManager.Instance?.ResumeAfterCodeEntry();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            TrySubmit();
    }

    private void TrySubmit()
    {
        string code = _input?.text?.Trim().ToUpperInvariant() ?? "";

        // Dev cheat: GO## warps directly to a level by number (1-based)
        if (code.StartsWith("GO") && code.Length >= 3)
        {
            if (int.TryParse(code.Substring(2), out int levelNum))
            {
                int levelClamped = Mathf.Clamp(levelNum - 1, 0, UIStyle.TotalLevels - 1);
                Hide();
                GameManager.Instance?.WarpToLevel(levelClamped);
                return;
            }
        }

        if (code.Length != 4)
        {
            ShowError("Code must be exactly 4 letters.");
            return;
        }

        if (LevelCodeManager.Instance == null ||
            !LevelCodeManager.Instance.TryGetLevelByCode(code, out int levelIndex))
        {
            ShowError("Unknown code — have you reached that level?");
            return;
        }

        Hide();
        GameManager.Instance?.WarpToLevel(levelIndex);
    }

    private void ShowError(string message)
    {
        if (_errorText != null)
            _errorText.text = message;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public bool IsVisible => _visible;

    public void Show()
    {
        _visible = true;
        gameObject.SetActive(true);
        if (_input != null)
        {
            _input.text = "";
            _input.Select();
            _input.ActivateInputField();
        }
        if (_errorText != null)
            _errorText.text = "";
    }

    public void Hide()
    {
        _visible = false;
        gameObject.SetActive(false);
    }
}
