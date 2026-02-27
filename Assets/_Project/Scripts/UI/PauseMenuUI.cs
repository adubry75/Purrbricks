using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pause menu overlay shown when the player presses Escape during gameplay.
/// Provides Resume, Settings, Main Menu, and Quit Game options.
/// ESC key also resumes.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    private Canvas _canvas;

    [Header("Button Sprites")]
    [SerializeField] private Sprite _resumeSprite;
    [SerializeField] private Sprite _settingsSprite;
    [SerializeField] private Sprite _mainMenuSprite;
    [SerializeField] private Sprite _quitSprite;

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 150;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Dark semi-transparent overlay
        var bg = new GameObject("Bg");
        bg.transform.SetParent(transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        var bgRt = bgImg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = bgRt.anchoredPosition = Vector2.zero;

        // Centered card
        var card = new GameObject("Card");
        card.transform.SetParent(transform, false);
        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.04f, 0.06f, 0.13f, 0.97f);
        var cardOl = card.AddComponent<Outline>();
        cardOl.effectColor    = new Color(0.25f, 0.50f, 1f, 0.40f);
        cardOl.effectDistance = new Vector2(2f, -2f);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin        = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax        = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta        = new Vector2(480f, 620f);
        cardRt.anchoredPosition = Vector2.zero;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(card.transform, false);
        var titleTxt = titleGO.AddComponent<Text>();
        titleTxt.text          = "PAUSED";
        titleTxt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize      = 72;
        titleTxt.fontStyle     = FontStyle.Bold;
        titleTxt.alignment     = TextAnchor.MiddleCenter;
        titleTxt.color         = UIStyle.AccentGold;
        titleTxt.raycastTarget = false;
        var titleOl = titleGO.AddComponent<Outline>();
        titleOl.effectColor    = Color.black;
        titleOl.effectDistance = new Vector2(4f, -4f);
        var titleRt = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin        = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax        = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta        = new Vector2(440f, 90f);
        titleRt.anchoredPosition = new Vector2(0f, 245f);

        // Buttons — stacked vertically (5 buttons)
        float btnY  = 138f;
        float step  = 96f;
        const float W = 360f, H = 74f;

        UIStyle.CreateButton(card.transform, "Resume",
            new Vector2(0f, btnY),          new Vector2(W, H),
            () => GameManager.Instance?.ResumeGame(), UIStyle.AccentGreen);

        UIStyle.CreateButton(card.transform, "Settings",
            new Vector2(0f, btnY - step),   new Vector2(W, H),
            () => GameManager.Instance?.ShowSettings(fromPause: true), UIStyle.AccentBlue);

        UIStyle.CreateButton(card.transform, "Level Select",
            new Vector2(0f, btnY - step*2), new Vector2(W, H),
            ShowLevelSelect, UIStyle.AccentBlue);

        UIStyle.CreateButton(card.transform, "Main Menu",
            new Vector2(0f, btnY - step*3), new Vector2(W, H),
            () => GameManager.Instance?.ShowMainMenu(), UIStyle.AccentMagenta);

        UIStyle.CreateButton(card.transform, "Quit Game",
            new Vector2(0f, btnY - step*4), new Vector2(W, H),
            OnQuitGame, UIStyle.AccentRed);
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Escape))
            GameManager.Instance?.ResumeGame();
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
            GameManager.Instance?.WarpToLevel(levelIndex);
        });
    }

    private static void OnQuitGame()
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
