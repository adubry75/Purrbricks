using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Victory screen: shows level score stats, an optional "New Personal Best" banner,
/// and buttons for Level Board / Next Level / Replay.
/// </summary>
public class VictoryUI : MonoBehaviour
{
    private Canvas _canvas;
    private GameObject _panel;
    [SerializeField] private Sprite _nextLevelSprite;
    [SerializeField] private Sprite _replayLevelSprite;

    // Score stat labels
    private Text _levelScoreText;
    private Text _comboBonusText;
    private Text _bestComboText;

    // Personal best banner (hidden unless score beats previous best)
    private GameObject _newBestBanner;

    // Star rating
    private readonly Text[] _starGlyphs = new Text[5];
    private int _currentRating;

    // Per-call state
    private int    _currentLevelScore;
    private string _currentLevelId;
    private int    _currentLevelIndex;

    private static readonly Color ColorGold  = new Color(1.00f, 0.84f, 0.10f);
    private static readonly Color ColorGreen = new Color(0.20f, 1f, 0.45f);
    private static readonly Color ColorStarEmpty = new Color(0.45f, 0.45f, 0.50f);

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

        _panel = new GameObject("Panel");
        _panel.transform.SetParent(transform, false);

        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.68f);

        var panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin        = Vector2.zero;
        panelRt.anchorMax        = Vector2.one;
        panelRt.sizeDelta        = Vector2.zero;
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        // ── Title ────────────────────────────────────────────────────────────
        CreateText(_panel, "LEVEL COMPLETE!", new Vector2(0f, 230f), 80, ColorGreen);

        // ── Score stats ──────────────────────────────────────────────────────
        _levelScoreText = CreateTextGO(_panel, "Level Score:  0",  new Vector2(0f, 148f), 50, Color.white,                  "LevelScoreText").GetComponent<Text>();
        _comboBonusText = CreateTextGO(_panel, "Combo Bonus:  —",  new Vector2(0f,  90f), 36, new Color(1f, 0.85f, 0.15f), "ComboBonusText").GetComponent<Text>();
        _bestComboText  = CreateTextGO(_panel, "Best Combo:  —",   new Vector2(0f,  42f), 36, new Color(0.45f, 0.85f, 1f), "BestComboText").GetComponent<Text>();

        // ── Personal best banner (hidden by default) ──────────────────────────
        _newBestBanner = new GameObject("NewBestBanner");
        _newBestBanner.transform.SetParent(_panel.transform, false);
        var nbRt = _newBestBanner.AddComponent<RectTransform>();
        nbRt.anchorMin        = new Vector2(0.5f, 0.5f);
        nbRt.anchorMax        = new Vector2(0.5f, 0.5f);
        nbRt.sizeDelta        = new Vector2(820f, 55f);
        nbRt.anchoredPosition = new Vector2(0f, -20f);
        CreateText(_newBestBanner, "★  NEW PERSONAL BEST!  ★", new Vector2(0f, 0f), 40, ColorGold);
        _newBestBanner.SetActive(false);

        // ── Buttons ───────────────────────────────────────────────────────────
        // Level Board button (top center)
        UIStyle.CreateButton(_panel.transform, "Level Board",
            new Vector2(0f, -110f), new Vector2(300f, 65f),
            OnLevelBoard, UIStyle.AccentBlue);

        // Next Level / Replay (side by side below)
        if (_nextLevelSprite != null)
            UIStyle.CreateImageButton(_panel.transform, _nextLevelSprite, new Vector2(162f, -210f), OnNextLevel);
        else
            UIStyle.CreateButton(_panel.transform, "Next Level",
                new Vector2(162f, -210f), new Vector2(300f, 70f),
                OnNextLevel, UIStyle.AccentGreen);

        if (_replayLevelSprite != null)
            UIStyle.CreateImageButton(_panel.transform, _replayLevelSprite, new Vector2(-162f, -210f), OnReplayLevel);
        else
            UIStyle.CreateButton(_panel.transform, "Replay Level",
                new Vector2(-162f, -210f), new Vector2(300f, 70f),
                OnReplayLevel, UIStyle.AccentBlue);

        BuildRatingSection();
    }

    private void BuildRatingSection()
    {
        // "Rate This Level" label
        var labelGO = CreateTextGO(_panel, "Rate This Level", new Vector2(0f, -285f), 26,
            new Color(0.65f, 0.65f, 0.80f, 0.85f), "RateLabel");
        labelGO.GetComponent<Text>().raycastTarget = false;

        // 5 star buttons centred horizontally, 80 px apart
        const float spacing   = 80f;
        const float startX    = -spacing * 2f; // -160, -80, 0, 80, 160

        for (int i = 0; i < 5; i++)
        {
            int starNum = i + 1;   // capture for lambda

            var go = new GameObject($"Star_{starNum}");
            go.transform.SetParent(_panel.transform, false);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);  // transparent hit-area

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(72f, 90f);
            rt.anchoredPosition = new Vector2(startX + i * spacing, -355f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cols = btn.colors;
            cols.normalColor      = Color.white;
            cols.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            cols.pressedColor     = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = cols;
            btn.onClick.AddListener(() => OnStarClicked(starNum));

            // Star glyph (large ☆/★)
            var glyphGO = new GameObject("Glyph");
            glyphGO.transform.SetParent(go.transform, false);
            var glyphTxt = glyphGO.AddComponent<Text>();
            glyphTxt.text          = "\u2606";  // ☆ hollow star
            glyphTxt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            glyphTxt.fontSize      = 62;
            glyphTxt.alignment     = TextAnchor.UpperCenter;
            glyphTxt.color         = ColorStarEmpty;
            glyphTxt.raycastTarget = false;
            var glyphRt = glyphGO.GetComponent<RectTransform>();
            glyphRt.anchorMin = Vector2.zero;
            glyphRt.anchorMax = Vector2.one;
            glyphRt.sizeDelta = Vector2.zero;
            _starGlyphs[i] = glyphTxt;

            // Number label inside the star (small, centred)
            var numGO = new GameObject("Num");
            numGO.transform.SetParent(go.transform, false);
            var numTxt = numGO.AddComponent<Text>();
            numTxt.text          = starNum.ToString();
            numTxt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            numTxt.fontSize      = 20;
            numTxt.fontStyle     = FontStyle.Bold;
            numTxt.alignment     = TextAnchor.MiddleCenter;
            numTxt.color         = new Color(1f, 1f, 1f, 0.75f);
            numTxt.raycastTarget = false;
            var numRt = numGO.GetComponent<RectTransform>();
            numRt.anchorMin        = Vector2.zero;
            numRt.anchorMax        = Vector2.one;
            numRt.sizeDelta        = Vector2.zero;
            numRt.anchoredPosition = new Vector2(0f, -6f);  // nudge into the lower body of the star
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void ShowVictory(int levelScore, int comboBonus, int bestCombo, string levelId, int levelIndex)
    {
        _currentLevelScore = levelScore;
        _currentLevelId    = levelId;
        _currentLevelIndex = levelIndex;

        // Submit to Steam per-level leaderboard immediately (KeepBest)
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

        // Personal best check — save locally if new best
        bool isNewBest = false;
        if (HighScoreManager.Instance != null && !string.IsNullOrEmpty(levelId))
        {
            isNewBest = levelScore > HighScoreManager.Instance.GetPersonalBest(levelId);
            if (isNewBest)
                HighScoreManager.Instance.UpdatePersonalBest(levelId, levelScore);
        }
        if (_newBestBanner != null) _newBestBanner.SetActive(isNewBest);

        // Pre-load saved rating for this level
        _currentRating = LevelRatingService.Instance != null
            ? LevelRatingService.Instance.GetRating(levelId)
            : 0;
        UpdateStars(_currentRating);

        SpawnConfetti();
    }

    private void OnNextLevel()   => GameManager.Instance?.LoadNextLevel();
    private void OnReplayLevel() => GameManager.Instance?.ReplayCurrentLevel();
    private void OnLevelBoard()  => GameManager.Instance?.ShowLevelLeaderboard(_currentLevelIndex);

    private void OnStarClicked(int starNum)
    {
        // Click the already-active star → clear the rating
        _currentRating = (_currentRating == starNum) ? 0 : starNum;
        LevelRatingService.Instance?.SetRating(_currentLevelId, _currentLevelIndex, _currentRating);
        UpdateStars(_currentRating);
    }

    private void UpdateStars(int rating)
    {
        for (int i = 0; i < 5; i++)
        {
            if (_starGlyphs[i] == null) continue;
            bool filled         = i < rating;
            _starGlyphs[i].text  = filled ? "\u2605" : "\u2606";   // ★ / ☆
            _starGlyphs[i].color = filled ? ColorGold : ColorStarEmpty;
        }
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }

    // ── Text helpers ──────────────────────────────────────────────────────────

    private void CreateText(GameObject parent, string text, Vector2 pos, int fontSize, Color color, string objName = null)
        => CreateTextGO(parent, text, pos, fontSize, color, objName);

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

    // ── Confetti ──────────────────────────────────────────────────────────────

    private void SpawnConfetti()
    {
        for (int i = 0; i < 3; i++)
            SpawnConfettiEmitter(new Vector3(-3f + i * 3f, -2f, 0f));
    }

    private void SpawnConfettiEmitter(Vector3 position)
    {
        var go   = new GameObject("Confetti");
        go.transform.position = position;
        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(8f, 12f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.10f, 0.25f);
        main.startColor      = new ParticleSystem.MinMaxGradient(Color.white);
        main.gravityModifier = 0.5f;
        main.loop            = false;
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
}
