using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Victory screen: level complete, score stats, per-level top-3 leaderboard,
/// optional name entry when a new level best is achieved, and Replay / Next Level buttons.
/// </summary>
public class VictoryUI : MonoBehaviour
{
    private Canvas _canvas;
    private GameObject panel;
    [SerializeField] private Sprite _nextLevelSprite;

    // Score stat labels
    private Text _levelScoreText;
    private Text _comboBonusText;
    private Text _bestComboText;

    // New-best entry section (hidden unless score qualifies)
    private GameObject _newBestSection;
    private InputField _nameInput;

    // Leaderboard rows
    private Text[] _rowTexts = new Text[3];

    // Per-call state
    private int    _currentLevelScore;
    private string _currentLevelId;
    private bool   _scoreSubmitted;

    private static readonly Color ColorGold   = new Color(1.00f, 0.84f, 0.10f);
    private static readonly Color ColorSilver = new Color(0.75f, 0.75f, 0.80f);
    private static readonly Color ColorBronze = new Color(0.80f, 0.50f, 0.30f);

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

        panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.68f);

        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin        = Vector2.zero;
        panelRt.anchorMax        = Vector2.one;
        panelRt.sizeDelta        = Vector2.zero;
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        // ── Title ────────────────────────────────────────────────────────────
        CreateText(panel, "LEVEL COMPLETE!", new Vector2(0f, 200f), 80, new Color(0.20f, 1f, 0.45f));

        // ── Score stats ──────────────────────────────────────────────────────
        _levelScoreText = CreateTextGO(panel, "Level Score:  0",   new Vector2(0f, 125f), 50, Color.white,                    "LevelScoreText").GetComponent<Text>();
        _comboBonusText = CreateTextGO(panel, "Combo Bonus:  —",   new Vector2(0f,  68f), 36, new Color(1f, 0.85f, 0.15f),   "ComboBonusText").GetComponent<Text>();
        _bestComboText  = CreateTextGO(panel, "Best Combo:  —",    new Vector2(0f,  20f), 36, new Color(0.45f, 0.85f, 1f),   "BestComboText").GetComponent<Text>();

        // ── New-best section (hidden by default) ─────────────────────────────
        _newBestSection = new GameObject("NewBestSection");
        _newBestSection.transform.SetParent(panel.transform, false);
        var nbRt = _newBestSection.AddComponent<RectTransform>();
        nbRt.anchorMin        = new Vector2(0.5f, 0.5f);
        nbRt.anchorMax        = new Vector2(0.5f, 0.5f);
        nbRt.sizeDelta        = new Vector2(820f, 105f);
        nbRt.anchoredPosition = new Vector2(0f, -50f);

        CreateText(_newBestSection, "★  NEW LEVEL BEST!  ★", new Vector2(0f, 30f), 40, ColorGold);
        CreateNameEntryRow(_newBestSection);
        _newBestSection.SetActive(false);

        // ── Leaderboard ──────────────────────────────────────────────────────
        CreateText(panel, "── LEVEL TOP 3 ──", new Vector2(0f, -118f), 26, new Color(0.55f, 0.55f, 0.62f));

        Color[] rankColors = { ColorGold, ColorSilver, ColorBronze };
        float[] rowY       = { -153f, -188f, -223f };

        for (int i = 0; i < 3; i++)
        {
            _rowTexts[i] = CreateTextGO(panel, $"#{i + 1}   —", new Vector2(0f, rowY[i]), 31, rankColors[i], $"LeaderRow{i + 1}").GetComponent<Text>();
        }

        // ── Buttons (side by side) ───────────────────────────────────────────
        UIStyle.CreateButton(panel.transform, "Replay Level",
            new Vector2(-162f, -282f), new Vector2(300f, 70f),
            OnReplayLevel, UIStyle.AccentBlue);

        if (_nextLevelSprite != null)
            CreateImageButton(panel.transform, _nextLevelSprite, new Vector2(162f, -282f), OnNextLevel);
        else
            UIStyle.CreateButton(panel.transform, "Next Level",
                new Vector2(162f, -282f), new Vector2(300f, 70f),
                OnNextLevel, UIStyle.AccentGreen);
    }

    private void CreateNameEntryRow(GameObject parent)
    {
        // Input field
        var inputGO = new GameObject("NameInput");
        inputGO.transform.SetParent(parent.transform, false);

        var inputImg = inputGO.AddComponent<Image>();
        inputImg.color = new Color(0.07f, 0.12f, 0.22f, 0.95f);

        var inputOl = inputGO.AddComponent<Outline>();
        inputOl.effectColor    = new Color(0.35f, 0.70f, 1f, 0.6f);
        inputOl.effectDistance = new Vector2(1f, -1f);

        _nameInput                  = inputGO.AddComponent<InputField>();
        _nameInput.textComponent    = CreateInputText(inputGO);
        _nameInput.text             = "PLAYER";
        _nameInput.characterLimit   = 12;

        var inputRt = inputGO.GetComponent<RectTransform>();
        inputRt.anchorMin        = new Vector2(0.5f, 0.5f);
        inputRt.anchorMax        = new Vector2(0.5f, 0.5f);
        inputRt.sizeDelta        = new Vector2(280f, 50f);
        inputRt.anchoredPosition = new Vector2(-90f, -20f);

        // Submit button to the right of the input
        UIStyle.CreateButton(parent.transform, "SUBMIT",
            new Vector2(120f, -20f), new Vector2(140f, 50f),
            OnSubmitScore, UIStyle.AccentGreen);
    }

    private Text CreateInputText(GameObject parent)
    {
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(parent.transform, false);

        var txt = textGO.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 28;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = Color.white;

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 0f);
        rt.offsetMax = new Vector2(-8f, 0f);

        return txt;
    }

    // Convenience overload that discards the returned GO
    private void CreateText(GameObject parent, string text, Vector2 pos, int fontSize, Color color, string objName = null)
    {
        CreateTextGO(parent, text, pos, fontSize, color, objName);
    }

    private GameObject CreateTextGO(GameObject parent, string text, Vector2 pos, int fontSize, Color color, string objName = null)
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
        rt.sizeDelta        = new Vector2(900f, fontSize + 24f);
        rt.anchoredPosition = pos;

        var ol = go.AddComponent<Outline>();
        ol.effectColor    = Color.black;
        ol.effectDistance = new Vector2(4f, -4f);

        return go;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void ShowVictory(int levelScore, int comboBonus, int bestCombo, string levelId)
    {
        _currentLevelScore = levelScore;
        _currentLevelId    = levelId;
        _scoreSubmitted    = false;

        // Submit to Steam per-level leaderboard immediately (KeepBest — no name needed)
        if (levelScore > 0 && !string.IsNullOrEmpty(levelId))
            SteamLeaderboardManager.Instance?.SubmitScore("Purrbricks_" + levelId, levelScore);

        gameObject.SetActive(true);

        if (_levelScoreText != null) _levelScoreText.text = $"Level Score:  {levelScore:N0}";
        if (_comboBonusText != null) _comboBonusText.text = comboBonus > 0
            ? $"Combo Bonus:  +{comboBonus:N0}"
            : "Combo Bonus:  —";
        if (_bestComboText != null) _bestComboText.text = bestCombo > 0
            ? $"Best Combo:  ×{bestCombo + 1}"
            : "Best Combo:  —";

        // Show name-entry section only if it's a new level best
        bool isNewBest = HighScoreManager.Instance != null
            && !string.IsNullOrEmpty(levelId)
            && HighScoreManager.Instance.IsLevelHighScore(levelId, levelScore);

        if (_newBestSection != null) _newBestSection.SetActive(isNewBest);
        if (isNewBest && _nameInput != null)
        {
            _nameInput.text = "PLAYER";
            _nameInput.Select();
        }

        RefreshLeaderboard();
        SpawnConfetti();
    }

    private void RefreshLeaderboard()
    {
        if (string.IsNullOrEmpty(_currentLevelId) || HighScoreManager.Instance == null)
        {
            for (int i = 0; i < 3; i++)
                if (_rowTexts[i] != null) _rowTexts[i].text = $"#{i + 1}   —";
            return;
        }

        var scores = HighScoreManager.Instance.GetTopLevelScores(_currentLevelId);
        for (int i = 0; i < 3; i++)
        {
            if (_rowTexts[i] == null) continue;
            _rowTexts[i].text = i < scores.Count
                ? $"#{i + 1}   {scores[i].playerName}   {scores[i].score:N0}"
                : $"#{i + 1}   —";
        }
    }

    private void Update()
    {
        if (!gameObject.activeSelf || _scoreSubmitted) return;
        if (_newBestSection == null || !_newBestSection.activeSelf) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            OnSubmitScore();
    }

    private void OnSubmitScore()
    {
        if (_scoreSubmitted) return;
        _scoreSubmitted = true;

        if (HighScoreManager.Instance != null && !string.IsNullOrEmpty(_currentLevelId))
        {
            string name = string.IsNullOrWhiteSpace(_nameInput?.text) ? "PLAYER" : _nameInput.text.Trim();
            HighScoreManager.Instance.AddLevelScore(_currentLevelId, name, _currentLevelScore);
        }

        if (_newBestSection != null) _newBestSection.SetActive(false);
        RefreshLeaderboard();
    }

    private void OnNextLevel()   => GameManager.Instance?.LoadNextLevel();
    private void OnReplayLevel() => GameManager.Instance?.ReplayCurrentLevel();

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }

    // ── Confetti ─────────────────────────────────────────────────────────────

    private void SpawnConfetti()
    {
        for (int i = 0; i < 3; i++)
            SpawnConfettiEmitter(new Vector3(-3f + i * 3f, -2f, 0f));
    }

    private void SpawnConfettiEmitter(Vector3 position)
    {
        var go = new GameObject("Confetti");
        go.transform.position = position;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime  = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSpeed     = new ParticleSystem.MinMaxCurve(8f, 12f);
        main.startSize      = new ParticleSystem.MinMaxCurve(0.10f, 0.25f);
        main.startColor     = new ParticleSystem.MinMaxGradient(Color.white);
        main.gravityModifier = 0.5f;
        main.loop           = false;
        main.useUnscaledTime = true;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 80) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 15f;
        shape.radius    = 0.2f;

        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.2f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 1f, 0.2f),   0.33f),
                new GradientColorKey(new Color(0.2f, 1f, 0.2f), 0.66f),
                new GradientColorKey(new Color(0.2f, 0.5f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material     = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingOrder = 250;

        Destroy(go, 4f);
    }

    // ── Image button (for optional Next Level sprite) ─────────────────────────

    private void CreateImageButton(Transform parent, Sprite sprite, Vector2 anchoredPos, UnityAction onClick)
    {
        if (sprite == null) return;

        var go = new GameObject("ImageButton");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.sprite        = sprite;
        img.type          = Image.Type.Simple;
        img.preserveAspect = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor     = new Color(0.80f, 0.80f, 0.80f);
        btn.colors = colors;

        float aspect = (float)sprite.texture.width / sprite.texture.height;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(aspect * 90f, 90f);
        rt.anchoredPosition = anchoredPos;
    }
}
