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
    private Text _titleTxt;

    private Button _resumeBtn;
    private Button _storeBtn;
    private Button _settingsBtn;
    private Button _levelSelectBtn;
    private Button _mainMenuBtn;
    private Button _quitBtn;
    private Button _backToEditorBtn;

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
        cardRt.sizeDelta        = new Vector2(480f, 720f);
        cardRt.anchoredPosition = new Vector2(-160f, 0f);

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(card.transform, false);
        _titleTxt = titleGO.AddComponent<Text>();
        _titleTxt.text          = "PAUSED";
        _titleTxt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _titleTxt.fontSize      = 72;
        _titleTxt.fontStyle     = FontStyle.Bold;
        _titleTxt.alignment     = TextAnchor.MiddleCenter;
        _titleTxt.color         = UIStyle.AccentGold;
        _titleTxt.raycastTarget = false;
        var titleOl = titleGO.AddComponent<Outline>();
        titleOl.effectColor    = Color.black;
        titleOl.effectDistance = new Vector2(4f, -4f);
        var titleRt = _titleTxt.GetComponent<RectTransform>();
        titleRt.anchorMin        = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax        = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta        = new Vector2(440f, 90f);
        titleRt.anchoredPosition = new Vector2(0f, 295f);

        // Buttons — stacked vertically (6 buttons)
        const float W = 360f, H = 74f;

        _resumeBtn = UIStyle.CreateButton(card.transform, "Resume",
            new Vector2(0f, 188f), new Vector2(W, H),
            () => GameManager.Instance?.ResumeGame(), UIStyle.AccentGreen);

        _storeBtn = UIStyle.CreateButton(card.transform, "Store",
            new Vector2(0f, 92f), new Vector2(W, H),
            () => GameManager.Instance?.ShowStore(), UIStyle.AccentGold);

        _settingsBtn = UIStyle.CreateButton(card.transform, "Settings",
            new Vector2(0f, -4f), new Vector2(W, H),
            () => GameManager.Instance?.ShowSettings(fromPause: true), UIStyle.AccentBlue);

        _levelSelectBtn = UIStyle.CreateButton(card.transform, "Level Select",
            new Vector2(0f, -100f), new Vector2(W, H),
            ShowLevelSelect, UIStyle.AccentBlue);

        _mainMenuBtn = UIStyle.CreateButton(card.transform, "Main Menu",
            new Vector2(0f, -196f), new Vector2(W, H),
            () => GameManager.Instance?.ShowMainMenu(), UIStyle.AccentMagenta);

        _quitBtn = UIStyle.CreateButton(card.transform, "Quit Game",
            new Vector2(0f, -292f), new Vector2(W, H),
            OnQuitGame, UIStyle.AccentRed);

        _backToEditorBtn = UIStyle.CreateButton(card.transform, "Back To Editor",
            new Vector2(0f, 40f), new Vector2(W, H),
            () => GameManager.Instance?.ReturnToEditorFromTest(), UIStyle.AccentGreen);
        _backToEditorBtn.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameManager.Instance != null && GameManager.Instance.IsEditorTestMode)
                GameManager.Instance.ReturnToEditorFromTest();
            else
                GameManager.Instance?.ResumeGame();
        }
    }

    private void ApplyMode()
    {
        bool isEditorTest = GameManager.Instance != null && GameManager.Instance.IsEditorTestMode;

        if (_resumeBtn != null)      _resumeBtn.gameObject.SetActive(!isEditorTest);
        if (_storeBtn != null)       _storeBtn.gameObject.SetActive(!isEditorTest);
        if (_settingsBtn != null)    _settingsBtn.gameObject.SetActive(!isEditorTest);
        if (_levelSelectBtn != null) _levelSelectBtn.gameObject.SetActive(!isEditorTest);
        if (_mainMenuBtn != null)    _mainMenuBtn.gameObject.SetActive(!isEditorTest);
        if (_quitBtn != null)        _quitBtn.gameObject.SetActive(!isEditorTest);
        if (_backToEditorBtn != null) _backToEditorBtn.gameObject.SetActive(isEditorTest);
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

    public void Show()
    {
        ApplyMode();
        gameObject.SetActive(true);
    }
    public void Hide() { gameObject.SetActive(false); }
}
