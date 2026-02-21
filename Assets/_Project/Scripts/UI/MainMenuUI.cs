using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu screen: logo, Play, High Scores, Quit buttons.
/// Uses UIStyle for AAA-quality cyberpunk button aesthetic.
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
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Semi-transparent backdrop offset left (avoids powerup HUD column)
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(transform, false);

        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.72f);

        var panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin       = Vector2.zero;
        panelRt.anchorMax       = Vector2.one;
        panelRt.sizeDelta       = Vector2.zero;
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        CreateTitle();

        UIStyle.CreateButton(_panel.transform, "Play",        new Vector2(0f,  50f), new Vector2(320f, 80f), () => GameManager.Instance?.StartGame(),       UIStyle.AccentMagenta);
        UIStyle.CreateButton(_panel.transform, "High Scores", new Vector2(0f, -50f), new Vector2(320f, 80f), () => GameManager.Instance?.ShowHighScores(), UIStyle.AccentBlue);
        UIStyle.CreateButton(_panel.transform, "Quit",        new Vector2(0f,-150f), new Vector2(320f, 80f), QuitGame,                                       UIStyle.AccentRed);

        CreateSubtitle();
    }

    private void CreateTitle()
    {
        var go = new GameObject("Title");
        go.transform.SetParent(_panel.transform, false);

        var txt = go.AddComponent<Text>();
        txt.text      = "PURRBRICKS";
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 120;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = new Color(1f, 0.35f, 0.82f); // hot pink

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.sizeDelta       = new Vector2(860f, 150f);
        rt.anchoredPosition = new Vector2(0f, 260f);

        var ol  = go.AddComponent<Outline>();
        ol.effectColor    = new Color(0.6f, 0f, 0.35f, 0.9f);
        ol.effectDistance = new Vector2(5f, -5f);

        var sh  = go.AddComponent<Shadow>();
        sh.effectColor    = new Color(1f, 0.2f, 0.6f, 0.35f);
        sh.effectDistance = new Vector2(0f, -8f);
    }

    private void CreateSubtitle()
    {
        var go = new GameObject("Subtitle");
        go.transform.SetParent(_panel.transform, false);

        var txt = go.AddComponent<Text>();
        txt.text      = "BREAK EVERY BRICK. CLAIM EVERY PURR.";
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 26;
        txt.fontStyle = FontStyle.Italic;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = new Color(0.65f, 0.65f, 0.80f, 0.75f);

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.sizeDelta       = new Vector2(800f, 40f);
        rt.anchoredPosition = new Vector2(0f, 190f);
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
