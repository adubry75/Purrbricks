using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Game Over screen: shows final score, always-visible name entry, and a SUBMIT button.
/// Enter key or SUBMIT saves score (if high score) then shows High Scores directly.
/// No Play Again or Main Menu buttons — navigation happens via HighScoresUI.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    private Canvas     _canvas;
    private InputField _nameInput;
    private Text       _titleText;
    private GameObject _highScoreLabel;
    private int        _finalScore;
    private bool       _submitted;

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.88f);

        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin        = Vector2.zero;
        panelRt.anchorMax        = Vector2.one;
        panelRt.sizeDelta        = Vector2.zero;
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panel.transform, false);

        _titleText           = titleGO.AddComponent<Text>();
        _titleText.text      = "GAME OVER";
        _titleText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _titleText.fontSize  = 100;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color     = UIStyle.AccentRed;

        var titleRt = _titleText.GetComponent<RectTransform>();
        titleRt.anchorMin        = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax        = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta        = new Vector2(800f, 120f);
        titleRt.anchoredPosition = new Vector2(0f, 220f);

        var titleOl = titleGO.AddComponent<Outline>();
        titleOl.effectColor    = Color.black;
        titleOl.effectDistance = new Vector2(4f, -4f);

        // Score
        CreateText(panel, "Score: 0", new Vector2(0f, 100f), 60, Color.white, "ScoreText");

        // ── Name entry panel (always visible) ───────────────────────────────
        var namePanel = new GameObject("NamePanel");
        namePanel.transform.SetParent(panel.transform, false);

        var npRt = namePanel.AddComponent<RectTransform>();
        npRt.anchorMin        = new Vector2(0.5f, 0.5f);
        npRt.anchorMax        = new Vector2(0.5f, 0.5f);
        npRt.sizeDelta        = new Vector2(660f, 210f);
        npRt.anchoredPosition = new Vector2(0f, -55f);

        // "NEW HIGH SCORE!" — shown only when relevant
        _highScoreLabel = CreateText(namePanel, "NEW HIGH SCORE!", new Vector2(0f, 72f), 40, UIStyle.AccentGold);

        CreateText(namePanel, "Enter your name:", new Vector2(0f, 18f), 30, Color.white);

        // Input field (shifted left) + SUBMIT button (right)
        CreateNameInputRow(namePanel);
    }

    private void CreateNameInputRow(GameObject parent)
    {
        // Input field
        var inputGO = new GameObject("NameInput");
        inputGO.transform.SetParent(parent.transform, false);

        var inputImg = inputGO.AddComponent<Image>();
        inputImg.color = new Color(0.07f, 0.12f, 0.22f, 0.95f);

        var inputOl = inputGO.AddComponent<Outline>();
        inputOl.effectColor    = new Color(0.35f, 0.70f, 1f, 0.6f);
        inputOl.effectDistance = new Vector2(1f, -1f);

        _nameInput = inputGO.AddComponent<InputField>();
        _nameInput.textComponent  = CreateInputText(inputGO);
        _nameInput.text           = "PLAYER";
        _nameInput.characterLimit = 12;

        var inputRt = inputGO.GetComponent<RectTransform>();
        inputRt.anchorMin        = new Vector2(0.5f, 0.5f);
        inputRt.anchorMax        = new Vector2(0.5f, 0.5f);
        inputRt.sizeDelta        = new Vector2(340f, 55f);
        inputRt.anchoredPosition = new Vector2(-100f, -42f);

        // SUBMIT button to the right of the input
        UIStyle.CreateButton(parent.transform, "SUBMIT",
            new Vector2(165f, -42f), new Vector2(170f, 55f),
            OnSubmit, UIStyle.AccentGreen);
    }

    private Text CreateInputText(GameObject parent)
    {
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(parent.transform, false);

        var txt = textGO.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 32;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = Color.white;

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 0f);
        rt.offsetMax = new Vector2(-10f, 0f);

        return txt;
    }

    // Returns the created GameObject so callers can store/toggle it
    private GameObject CreateText(GameObject parent, string text, Vector2 pos, int fontSize, Color color, string objName = null)
    {
        var go = new GameObject(objName ?? text);
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
        rt.sizeDelta        = new Vector2(800f, fontSize + 20f);
        rt.anchoredPosition = pos;

        var ol = go.AddComponent<Outline>();
        ol.effectColor    = Color.black;
        ol.effectDistance = new Vector2(3f, -3f);

        return go;
    }

    private void Update()
    {
        if (!gameObject.activeSelf || _submitted) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            OnSubmit();
    }

    private void OnSubmit()
    {
        if (_submitted) return;
        _submitted = true;

        bool isHigh = HighScoreManager.Instance?.IsHighScore(_finalScore) ?? false;
        if (isHigh && HighScoreManager.Instance != null)
        {
            string name = string.IsNullOrWhiteSpace(_nameInput?.text) ? "PLAYER" : _nameInput.text.Trim();
            HighScoreManager.Instance.AddScore(name, _finalScore);
        }

        GameManager.Instance?.ShowHighScoresAfterGameOver();
    }

    public void ShowGameOver(int finalScore)
    {
        _finalScore = finalScore;
        _submitted  = false;
        gameObject.SetActive(true);

        if (_titleText != null)
        {
            _titleText.text  = "GAME OVER";
            _titleText.color = UIStyle.AccentRed;
        }

        var st = transform.Find("Panel/ScoreText")?.GetComponent<Text>();
        if (st != null) st.text = $"Final Score: {finalScore:N0}";

        bool isHigh = HighScoreManager.Instance?.IsHighScore(finalScore) ?? false;
        if (_highScoreLabel != null) _highScoreLabel.SetActive(isHigh);

        _nameInput?.Select();
    }

    public void ShowGameComplete(int finalScore)
    {
        _finalScore = finalScore;
        _submitted  = false;
        gameObject.SetActive(true);

        if (_titleText != null)
        {
            _titleText.text  = "ALL LEVELS CLEARED!";
            _titleText.color = UIStyle.AccentGreen;
        }

        var st = transform.Find("Panel/ScoreText")?.GetComponent<Text>();
        if (st != null) st.text = $"Final Score: {finalScore:N0}";

        bool isHigh = HighScoreManager.Instance?.IsHighScore(finalScore) ?? false;
        if (_highScoreLabel != null) _highScoreLabel.SetActive(isHigh);

        _nameInput?.Select();
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
}
