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
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Background panel - offset left to center on playfield
        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.85f);

        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.sizeDelta = Vector2.zero;
        // Shift left by 1/12th of screen width (half of 1/6th reserved area)
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        // "GAME OVER" title (will be changed to "VICTORY!" if game complete)
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panel.transform, false);

        _titleText = titleGO.AddComponent<Text>();
        _titleText.text = "GAME OVER";
        _titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _titleText.fontSize = 100;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color = new Color(1f, 0.2f, 0.2f);

        var titleRt = _titleText.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(800f, 120f);
        titleRt.anchoredPosition = new Vector2(0f, 200f);

        var outline = titleGO.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        // Score display (will be set dynamically)
        CreateText(panel, "Score: 0", new Vector2(0f, 80f), 60, Color.white, "ScoreText");

        // Name entry panel (shown only if high score)
        _namePanel = new GameObject("NamePanel");
        _namePanel.transform.SetParent(panel.transform, false);

        var namePanelRt = _namePanel.AddComponent<RectTransform>();
        namePanelRt.anchorMin = new Vector2(0.5f, 0.5f);
        namePanelRt.anchorMax = new Vector2(0.5f, 0.5f);
        namePanelRt.sizeDelta = new Vector2(600f, 200f);
        namePanelRt.anchoredPosition = new Vector2(0f, -50f);

        CreateText(_namePanel, "NEW HIGH SCORE!", new Vector2(0f, 60f), 40, Color.yellow);
        CreateText(_namePanel, "Enter your name:", new Vector2(0f, 10f), 30, Color.white);

        CreateNameInput(_namePanel);

        _namePanel.SetActive(false);

        // Buttons
        CreateButton(panel, "Play Again", new Vector2(-150f, -200f), OnPlayAgain);
        CreateButton(panel, "Main Menu", new Vector2(150f, -200f), OnMainMenu);
    }

    private void CreateText(GameObject parent, string text, Vector2 pos, int fontSize, Color color, string objName = null)
    {
        var go = new GameObject(objName ?? text);
        go.transform.SetParent(parent.transform, false);

        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, fontSize + 20f);
        rt.anchoredPosition = pos;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);
    }

    private void CreateNameInput(GameObject parent)
    {
        var inputGO = new GameObject("NameInput");
        inputGO.transform.SetParent(parent.transform, false);

        var inputImg = inputGO.AddComponent<Image>();
        inputImg.color = new Color(0.2f, 0.2f, 0.2f);

        _nameInput = inputGO.AddComponent<InputField>();
        _nameInput.textComponent = CreateInputText(inputGO);
        _nameInput.text = "PLAYER";
        _nameInput.characterLimit = 12;

        var inputRt = inputGO.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0.5f, 0.5f);
        inputRt.anchorMax = new Vector2(0.5f, 0.5f);
        inputRt.sizeDelta = new Vector2(400f, 50f);
        inputRt.anchoredPosition = new Vector2(0f, -40f);
    }

    private Text CreateInputText(GameObject parent)
    {
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(parent.transform, false);

        var txt = textGO.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 32;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 0f);
        rt.offsetMax = new Vector2(-10f, 0f);

        return txt;
    }

    private void CreateButton(GameObject parent, string label, Vector2 pos, System.Action onClick)
    {
        var btnGO = new GameObject(label + "Button");
        btnGO.transform.SetParent(parent.transform, false);

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.6f, 1f);

        var button = btnGO.AddComponent<Button>();
        button.targetGraphic = btnImg;

        var colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.6f, 1f);
        colors.highlightedColor = new Color(0.3f, 0.8f, 1f);
        colors.pressedColor = new Color(0.1f, 0.4f, 0.8f);
        button.colors = colors;

        button.onClick.AddListener(() => onClick?.Invoke());

        var btnRt = btnGO.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.sizeDelta = new Vector2(250f, 70f);
        btnRt.anchoredPosition = pos;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);

        var btnText = textGO.AddComponent<Text>();
        btnText.text = label;
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 32;
        btnText.fontStyle = FontStyle.Bold;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;

        var textRt = btnText.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
    }

    public void ShowGameOver(int finalScore)
    {
        _finalScore = finalScore;
        gameObject.SetActive(true);

        // Set title to "GAME OVER"
        if (_titleText != null)
        {
            _titleText.text = "GAME OVER";
            _titleText.color = new Color(1f, 0.2f, 0.2f); // red
        }

        // Update score text
        var scoreText = transform.Find("Panel/ScoreText")?.GetComponent<Text>();
        if (scoreText != null)
            scoreText.text = $"Final Score: {finalScore:N0}";

        // Show name input if high score
        bool isHighScore = HighScoreManager.Instance?.IsHighScore(finalScore) ?? false;
        _namePanel.SetActive(isHighScore);
    }

    public void ShowGameComplete(int finalScore)
    {
        _finalScore = finalScore;
        gameObject.SetActive(true);

        // Set title to "VICTORY!"
        if (_titleText != null)
        {
            _titleText.text = "VICTORY!";
            _titleText.color = new Color(0.2f, 1f, 0.4f); // green
        }

        // Update score text
        var scoreText = transform.Find("Panel/ScoreText")?.GetComponent<Text>();
        if (scoreText != null)
            scoreText.text = $"Final Score: {finalScore:N0}";

        // Always show name input for game completion
        bool isHighScore = HighScoreManager.Instance?.IsHighScore(finalScore) ?? false;
        _namePanel.SetActive(isHighScore);
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
            string playerName = string.IsNullOrWhiteSpace(_nameInput.text) ? "PLAYER" : _nameInput.text;
            HighScoreManager.Instance.AddScore(playerName, _finalScore);
        }
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
}
