using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

/// <summary>
/// Main menu screen: logo, Play, High Scores, Quit buttons.
/// Uses UIStyle for AAA-quality cyberpunk button aesthetic.
/// Assign button sprites via Inspector (PurrbricksSetup does this automatically).
/// Press F1 (editor builds only) to open the Level Editor browser.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Sprite _titleGfx;

    private Canvas _canvas;
    private GameObject _panel;

#if UNITY_EDITOR
    private LevelEditorBrowserUI _levelEditorBrowser;
#endif

    private void Awake()
    {
        BuildUI();
#if UNITY_EDITOR
        _levelEditorBrowser = Object.FindFirstObjectByType<LevelEditorBrowserUI>(FindObjectsInactive.Include);
#endif
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1) && gameObject.activeSelf)
            ShowLevelEditor();
    }

    private void ShowLevelEditor()
    {
        if (_levelEditorBrowser == null)
            _levelEditorBrowser = Object.FindFirstObjectByType<LevelEditorBrowserUI>(FindObjectsInactive.Include);

        if (_levelEditorBrowser == null)
        {
            Debug.LogWarning("[MainMenuUI] LevelEditorBrowserUI not found in scene. Run Purrbricks > Setup Scene.");
            return;
        }

        Hide();
        _levelEditorBrowser.SetBackAction(() =>
        {
            Show();
            GameManager.Instance?.SetState(GameState.MainMenu);
        });
        _levelEditorBrowser.Show();
    }
#endif

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
        panelImg.color = new Color(0f, 0f, 0f, 0f);

        var panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.sizeDelta = Vector2.zero;
        //panelRt.anchoredPosition = new Vector2(0f, 0f);
        panelRt.offsetMin = new Vector2(-320f, panelRt.offsetMin.y);
        panelRt.offsetMax = new Vector2(0f, panelRt.offsetMax.y);

        CreateTitle();

        UIStyle.CreateButton(_panel.transform, "Play",             new Vector2(0f,  -80f), new Vector2(360f, 84f), () => GameManager.Instance?.StartGame(), UIStyle.AccentMagenta);
        UIStyle.CreateButton(_panel.transform, "Level Select",     new Vector2(0f, -170f), new Vector2(360f, 84f), ShowLevelSelect, UIStyle.AccentGreen);
        UIStyle.CreateButton(_panel.transform, "Community Levels", new Vector2(0f, -260f), new Vector2(360f, 84f), ShowCommunityBrowser, UIStyle.AccentGold);
        UIStyle.CreateButton(_panel.transform, "High Scores",      new Vector2(0f, -350f), new Vector2(360f, 84f), () => GameManager.Instance?.ShowHighScores(), UIStyle.AccentBlue);
        UIStyle.CreateButton(_panel.transform, "Settings",         new Vector2(0f, -435f), new Vector2(360f, 84f), () => GameManager.Instance?.ShowSettings(fromPause: false), UIStyle.AccentBlue);
        UIStyle.CreateButton(_panel.transform, "Quit",             new Vector2(0f, -520f), new Vector2(360f, 84f), QuitGame, UIStyle.AccentRed);
#if UNITY_EDITOR
        UIStyle.CreateButton(_panel.transform, "Level Editor [F1]", new Vector2(0f, -600f),
            new Vector2(260f, 48f), ShowLevelEditor, UIStyle.AccentGold);
#endif

        
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

    private void ShowCommunityBrowser()
    {
        var browser = Object.FindFirstObjectByType<CommunityBrowserUI>(FindObjectsInactive.Include);
        if (browser == null) { Debug.LogWarning("[MainMenuUI] CommunityBrowserUI not found. Run Purrbricks > Setup Scene."); return; }
        Hide();
        browser.SetBackAction(Show);
        browser.Show();
    }

    private void ShowLevelSelect()
    {
        var browser = Object.FindFirstObjectByType<LevelEditorBrowserUI>(FindObjectsInactive.Include);
        if (browser == null) return;
        Hide();
        browser.SetBackAction(Show);
        browser.ShowAsLevelSelect(levelIndex =>
        {
            browser.Hide();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameManager.Instance?.StartGameAtLevel(levelIndex);
        });
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
