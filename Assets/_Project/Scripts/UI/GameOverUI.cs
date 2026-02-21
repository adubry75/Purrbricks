using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Game Over screen: shows final score, name entry if high score, restart buttons.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    private Canvas _canvas;
    private InputField _nameInput;
    private GameObject _namePanel;
    private Text _titleText;
    private int _finalScore;

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
        panelRt.anchorMin       = Vector2.zero;
        panelRt.anchorMax       = Vector2.one;
        panelRt.sizeDelta       = Vector2.zero;
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
        titleRt.anchorMin       = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax       = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta       = new Vector2(800f, 120f);
        titleRt.anchoredPosition = new Vector2(0f, 210f);

        var titleOl = titleGO.AddComponent<Outline>();
        titleOl.effectColor    = Color.black;
        titleOl.effectDistance = new Vector2(4f, -4f);

        // Score
        CreateText(panel, "Score: 0", new Vector2(0f, 90f), 60, Color.white, "ScoreText");

        // High score name entry
        _namePanel = new GameObject("NamePanel");
        _namePanel.transform.SetParent(panel.transform, false);

        var npRt = _namePanel.AddComponent<RectTransform>();
        npRt.anchorMin       = new Vector2(0.5f, 0.5f);
        npRt.anchorMax       = new Vector2(0.5f, 0.5f);
        npRt.sizeDelta       = new Vector2(620f, 200f);
        npRt.anchoredPosition = new Vector2(0f, -40f);

        CreateText(_namePanel, "NEW HIGH SCORE!", new Vector2(0f, 68f), 40, UIStyle.AccentGold);
        CreateText(_namePanel, "Enter your name:", new Vector2(0f, 18f), 30, Color.white);
        CreateNameInput(_namePanel);

        _namePanel.SetActive(false);

        // Buttons
        UIStyle.CreateButton(panel.transform, "Play Again", new Vector2(-160f, -215f), new Vector2(280f, 75f), OnPlayAgain, UIStyle.AccentBlue);
        UIStyle.CreateButton(panel.transform, "Main Menu",  new Vector2( 160f, -215f), new Vector2(280f, 75f), OnMainMenu,  UIStyle.AccentGold);
    }

    private void CreateText(GameObject parent, string text, Vector2 pos, int fontSize, Color color, string objName = null)
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
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.sizeDelta       = new Vector2(800f, fontSize + 20f);
        rt.anchoredPosition = pos;

        var ol = go.AddComponent<Outline>();
        ol.effectColor    = Color.black;
        ol.effectDistance = new Vector2(3f, -3f);
    }

    private void CreateNameInput(GameObject parent)
    {
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
        inputRt.anchorMin       = new Vector2(0.5f, 0.5f);
        inputRt.anchorMax       = new Vector2(0.5f, 0.5f);
        inputRt.sizeDelta       = new Vector2(420f, 55f);
        inputRt.anchoredPosition = new Vector2(0f, -38f);
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

    public void ShowGameOver(int finalScore)
    {
        _finalScore      = finalScore;
        gameObject.SetActive(true);

        if (_titleText != null)
        {
            _titleText.text  = "GAME OVER";
            _titleText.color = UIStyle.AccentRed;
        }

        var st = transform.Find("Panel/ScoreText")?.GetComponent<Text>();
        if (st != null) st.text = $"Final Score: {finalScore:N0}";

        bool isHigh = HighScoreManager.Instance?.IsHighScore(finalScore) ?? false;
        _namePanel.SetActive(isHigh);
    }

    public void ShowGameComplete(int finalScore)
    {
        _finalScore      = finalScore;
        gameObject.SetActive(true);

        if (_titleText != null)
        {
            _titleText.text  = "VICTORY!";
            _titleText.color = UIStyle.AccentGreen;
        }

        var st = transform.Find("Panel/ScoreText")?.GetComponent<Text>();
        if (st != null) st.text = $"Final Score: {finalScore:N0}";

        bool isHigh = HighScoreManager.Instance?.IsHighScore(finalScore) ?? false;
        _namePanel.SetActive(isHigh);
    }

    private void OnPlayAgain()
    {
        SaveScoreIfNeeded();
        GameManager.Instance?.RestartGame();
    }

    private void OnMainMenu()
    {
        SaveScoreIfNeeded();
        GameManager.Instance?.ShowMainMenu();
    }

    private void SaveScoreIfNeeded()
    {
        if (_namePanel.activeSelf && HighScoreManager.Instance != null)
        {
            string name = string.IsNullOrWhiteSpace(_nameInput.text) ? "PLAYER" : _nameInput.text;
            HighScoreManager.Instance.AddScore(name, _finalScore);
        }
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
}
