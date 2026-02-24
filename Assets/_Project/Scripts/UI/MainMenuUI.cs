using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.U2D;
using UnityEngine.UI;

/// <summary>
/// Main menu screen: logo, Play, High Scores, Quit buttons.
/// Uses UIStyle for AAA-quality cyberpunk button aesthetic.
/// Assign button sprites via Inspector (PurrbricksSetup does this automatically).
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Sprite _titleGfx;
    [SerializeField] private Sprite _playSprite;
    [SerializeField] private Sprite _highScoresSprite;
    [SerializeField] private Sprite _quitSprite;

    private Canvas _canvas;
    private GameObject _panel;

    private void Awake()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        _canvas = GetComponent<Canvas>();
        if (_canvas == null)
            _canvas = gameObject.AddComponent<Canvas>();

        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        // Semi-transparent backdrop offset left (avoids powerup HUD column)
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(transform, false);

        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.72f);

        var panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.sizeDelta = Vector2.zero;
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        CreateTitle();

        if (_playSprite != null)
        {
            CreateImageButton(_panel.transform, _playSprite, new Vector2(0f, 60f), () => GameManager.Instance?.StartGame());
            CreateImageButton(_panel.transform, _highScoresSprite, new Vector2(0f, -60f), () => GameManager.Instance?.ShowHighScores());
            CreateImageButton(_panel.transform, _quitSprite, new Vector2(0f, -180f), QuitGame);
        }
        else
        {
            UIStyle.CreateButton(_panel.transform, "Play", new Vector2(0f, 50f), new Vector2(320f, 80f), () => GameManager.Instance?.StartGame(), UIStyle.AccentMagenta);
            UIStyle.CreateButton(_panel.transform, "High Scores", new Vector2(0f, -50f), new Vector2(320f, 80f), () => GameManager.Instance?.ShowHighScores(), UIStyle.AccentBlue);
            UIStyle.CreateButton(_panel.transform, "Quit", new Vector2(0f, -150f), new Vector2(320f, 80f), QuitGame, UIStyle.AccentRed);
        }

        
    }

    private void CreateTitle()
    {
        if (_titleGfx != null)
        {
            var go = new GameObject("Title");
            
            go.transform.SetParent(_panel.transform, false);

            var img = go.AddComponent<Image>();
            img.sprite = _titleGfx;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;

            
            //float aspect = (float)_titleGfx.texture.width / _titleGfx.texture.height;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600f, 500f);
            rt.anchoredPosition = new Vector2(0f, 260f);
        }
        else
        {
            var go = new GameObject("Title");
            go.transform.SetParent(_panel.transform, false);

            var txt = go.AddComponent<Text>();
            txt.text = "PURRBRICKS";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 120;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(1f, 0.35f, 0.82f); // hot pink

            var rt = txt.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(860f, 150f);
            rt.anchoredPosition = new Vector2(0f, 260f);

            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0.6f, 0f, 0.35f, 0.9f);
            ol.effectDistance = new Vector2(5f, -5f);

            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(1f, 0.2f, 0.6f, 0.35f);
            sh.effectDistance = new Vector2(0f, -8f);

            CreateSubtitle();
        }
    }

    private void CreateSubtitle()
    {
        var go = new GameObject("Subtitle");
        go.transform.SetParent(_panel.transform, false);

        var txt = go.AddComponent<Text>();
        txt.text = "BREAK EVERY BRICK. CLAIM EVERY PURR.";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 26;
        txt.fontStyle = FontStyle.Italic;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0.65f, 0.65f, 0.80f, 0.75f);

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, 40f);
        rt.anchoredPosition = new Vector2(0f, 190f);
    }

    private void CreateImageButton(Transform parent, Sprite sprite, Vector2 anchoredPos, UnityAction onClick)
    {
        if (sprite == null) return;

        var go = new GameObject("ImageButton");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.80f, 0.80f, 0.80f);
        btn.colors = colors;

        // Size: fixed height 90, width from sprite aspect ratio
        float aspect = (float)sprite.texture.width / sprite.texture.height;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(aspect * 70f, 70f);
        rt.anchoredPosition = anchoredPos;
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
