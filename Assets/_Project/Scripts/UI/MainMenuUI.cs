using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu screen: logo, Play, High Scores, Quit buttons.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    private Canvas _canvas;
    private GameObject _panel;

    private void Awake()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        // Canvas: Screen Space Overlay
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Background panel - offset left to center on playfield (exclude right 1/6th powerup area)
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(transform, false);

        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.7f);

        var panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.sizeDelta = Vector2.zero;
        // Shift left by 1/12th of screen width (half of 1/6th reserved area)
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        // Title "PURRBRICKS"
        CreateTitle();

        // Buttons
        CreateButton("Play", new Vector2(0f, 50f), () => GameManager.Instance?.StartGame());
        CreateButton("High Scores", new Vector2(0f, -50f), () => GameManager.Instance?.ShowHighScores());
        CreateButton("Quit", new Vector2(0f, -150f), QuitGame);
    }

    private void CreateTitle()
    {
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(_panel.transform, false);

        var titleText = titleGO.AddComponent<Text>();
        titleText.text = "PURRBRICKS";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 120;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.4f, 0.8f); // hot pink

        var titleRt = titleText.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(800f, 150f);
        titleRt.anchoredPosition = new Vector2(0f, 250f);

        // Outline
        var outline = titleGO.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(5f, -5f);
    }

    private void CreateButton(string label, Vector2 position, System.Action onClick)
    {
        var btnGO = new GameObject(label + "Button");
        btnGO.transform.SetParent(_panel.transform, false);

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.6f, 1f); // blue

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
        btnRt.sizeDelta = new Vector2(300f, 80f);
        btnRt.anchoredPosition = position;

        // Button text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);

        var btnText = textGO.AddComponent<Text>();
        btnText.text = label;
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 36;
        btnText.fontStyle = FontStyle.Bold;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;

        var textRt = btnText.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
}
