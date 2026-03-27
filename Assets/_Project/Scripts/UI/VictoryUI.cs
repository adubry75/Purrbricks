using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Steamworks;

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
    [SerializeField] private Sprite _levelRankingsSprite;

    // Score stat labels
    private Text _levelScoreText;
    private Text _comboBonusText;
    private Text _bestComboText;
    private Text _purrBucksText;

    // Personal best banner (hidden unless score beats previous best)
    private GameObject _newBestBanner;

    // 1-5 community star rating
    private readonly Text[] _starGlyphs = new Text[5];
    private int _currentRating;

    // 1-3 performance star rating
    private readonly Text[] _perfStarGlyphs = new Text[3];
    private readonly GameObject[] _perfStarGOs = new GameObject[3];
    private GameObject _perfStarsSection;
    private Coroutine _starAnimRoutine;

    // Per-call state
    private int _currentLevelScore;
    private string _currentLevelId;
    private int _currentLevelIndex;
    private Coroutine _fireworksRoutine;
    private GameObject _fireworksRoot;
    private GameObject _ratingSectionRoot;

    private static readonly Color ColorGold = new Color(1.00f, 0.84f, 0.10f);
    private static readonly Color ColorGreen = new Color(0.20f, 1f, 0.45f);
    private static readonly Color ColorStarEmpty = new Color(0.45f, 0.45f, 0.50f);

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    // Subscribe/unsubscribe per-activation so the handler is guaranteed to be live
    // when ShowVictory calls AwardLevelComplete (which fires OnRankAwardResolved
    // synchronously in debug builds — Start() would be too late).
    private void OnEnable()
    {
        if (PurrBucksManager.Instance != null)
            PurrBucksManager.Instance.OnRankAwardResolved += OnRankAwardResolvedHandler;
    }

    private void OnDisable()
    {
        if (PurrBucksManager.Instance != null)
            PurrBucksManager.Instance.OnRankAwardResolved -= OnRankAwardResolvedHandler;
    }

    private void OnRankAwardResolvedHandler(int amt)
    {
        if (_purrBucksText == null) return;
        _purrBucksText.text = $"🐾 +{amt} Purr Bucks";
        _purrBucksText.gameObject.SetActive(true);
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

        _panel = new GameObject("Panel");
        _panel.transform.SetParent(transform, false);

        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.68f);

        var panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.sizeDelta = Vector2.zero;
        panelRt.offsetMin = new Vector2(-320f, panelRt.offsetMin.y);
        panelRt.offsetMax = new Vector2(0f, panelRt.offsetMax.y);

        // ── Master vertical layout values ─────────────────────────────────────
        float startY = 265f;
        float titleToStarsGap = 78f;
        float starsToStatsGap = 76f;
        float statRowGap = 46f;
        float purrBucksToBestGap = 58f;
        float bestToHighScoresGap = 62f;
        float highScoresToDualButtonsGap = 82f;
        float dualButtonsToLevelSelectGap = 82f;
        float levelSelectToRatingGap = 95f;

        float currentY = startY;

        // ── Title ─────────────────────────────────────────────────────────────
        CreateText(_panel, "LEVEL COMPLETE!", new Vector2(0f, currentY), 80, ColorGreen);

        // ── Performance stars ─────────────────────────────────────────────────
        currentY -= titleToStarsGap;
        BuildPerfStarsSectionAt(currentY);

        // ── Score stats ───────────────────────────────────────────────────────
        currentY -= starsToStatsGap;

        _levelScoreText = CreateTextGO(
            _panel,
            "Level Score:  0",
            new Vector2(0f, currentY),
            50,
            Color.white,
            "LevelScoreText"
        ).GetComponent<Text>();

        currentY -= statRowGap;
        _comboBonusText = CreateTextGO(
            _panel,
            "Combo Bonus:  —",
            new Vector2(0f, currentY),
            36,
            new Color(1f, 0.85f, 0.15f),
            "ComboBonusText"
        ).GetComponent<Text>();

        currentY -= statRowGap;
        _bestComboText = CreateTextGO(
            _panel,
            "Best Combo:  —",
            new Vector2(0f, currentY),
            36,
            new Color(0.45f, 0.85f, 1f),
            "BestComboText"
        ).GetComponent<Text>();

        currentY -= statRowGap;
        _purrBucksText = CreateTextGO(
            _panel,
            "🐾 +?? PB",
            new Vector2(0f, currentY),
            32,
            ColorGold,
            "PurrBucksText"
        ).GetComponent<Text>();
        _purrBucksText.gameObject.SetActive(false);

        // ── Personal Best banner ──────────────────────────────────────────────
        currentY -= purrBucksToBestGap;

        _newBestBanner = new GameObject("NewBestBanner");
        _newBestBanner.transform.SetParent(_panel.transform, false);

        var nbRt = _newBestBanner.AddComponent<RectTransform>();
        nbRt.anchorMin = new Vector2(0.5f, 0.5f);
        nbRt.anchorMax = new Vector2(0.5f, 0.5f);
        nbRt.sizeDelta = new Vector2(820f, 50f);
        nbRt.anchoredPosition = new Vector2(0f, currentY);

        CreateText(_newBestBanner, "★  NEW PERSONAL BEST!  ★", Vector2.zero, 36, ColorGold);
        _newBestBanner.SetActive(false);

        // ── High Scores button ────────────────────────────────────────────────
        currentY -= bestToHighScoresGap;

        UIStyle.CreateButton(
            _panel.transform,
            "High Scores",
            new Vector2(0f, currentY),
            new Vector2(320f, 65f),
            OnLevelBoard,
            UIStyle.AccentBlue
        );

        // ── Replay / Next row ─────────────────────────────────────────────────
        currentY -= highScoresToDualButtonsGap;

        float sideButtonX = 162f;

        UIStyle.CreateButton(
            _panel.transform,
            "Replay Level",
            new Vector2(-sideButtonX, currentY),
            new Vector2(300f, 65f),
            OnReplayLevel,
            UIStyle.AccentBlue
        );

        var nextLevelBtn = UIStyle.CreateButton(
            _panel.transform,
            "Next Level",
            new Vector2(sideButtonX, currentY),
            new Vector2(300f, 65f),
            OnNextLevel,
            UIStyle.AccentGreen
        );
        _nextLevelBtnGO = nextLevelBtn.gameObject;

        var browseBtn = UIStyle.CreateButton(
            _panel.transform,
            "Browse More",
            new Vector2(sideButtonX, currentY),
            new Vector2(300f, 65f),
            OnBrowseMore,
            UIStyle.AccentGold
        );
        _browseMoreBtnGO = browseBtn.gameObject;
        _browseMoreBtnGO.SetActive(false);

        // ── Level Select ──────────────────────────────────────────────────────
        currentY -= dualButtonsToLevelSelectGap;

        UIStyle.CreateButton(
            _panel.transform,
            "Level Select",
            new Vector2(0f, currentY),
            new Vector2(280f, 55f),
            OnLevelSelect,
            UIStyle.AccentBlue
        );

        // ── Rate This Level ───────────────────────────────────────────────────
        // IMPORTANT: keep your original BuildRatingSection() so click/rating logic still works.
        currentY -= levelSelectToRatingGap;

        BuildRatingSection();

        // Reposition the rating section AFTER it is built.
        // Replace "_ratingSectionRoot" with whatever GameObject/Transform your BuildRatingSection creates.
        if (_ratingSectionRoot != null)
        {
            var ratingRt = _ratingSectionRoot.GetComponent<RectTransform>();
            if (ratingRt != null)
            {
                ratingRt.anchoredPosition = new Vector2(0f, currentY);
            }
        }

        // Ensure personal best banner is visible above other UI
        _newBestBanner.transform.SetAsLastSibling();
    }

    private void BuildPerfStarsSectionAt(float centerY)
    {
        float starSpacing = 74f;

        CreateText(_panel, "★", new Vector2(-starSpacing, centerY), 56, ColorGold);
        CreateText(_panel, "★", new Vector2(0f, centerY), 56, ColorGold);
        CreateText(_panel, "★", new Vector2(starSpacing, centerY), 56, ColorGold);
    }

    private void BuildRatingSectionAt(float centerY)
    {
        CreateText(_panel, "Rate This Level", new Vector2(0f, centerY + 26f), 28, Color.white);

        float starSpacing = 68f;
        float starY = centerY - 18f;

        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * starSpacing;
            CreateText(_panel, "☆", new Vector2(x, starY), 40, Color.white);
        }
    }


    private void BuildPerfStarsSection()
    {
        _perfStarsSection = new GameObject("PerfStarsSection");
        _perfStarsSection.transform.SetParent(_panel.transform, false);
        var sRt = _perfStarsSection.AddComponent<RectTransform>();
        sRt.anchorMin = sRt.anchorMax = new Vector2(0.5f, 0.5f);
        sRt.sizeDelta = Vector2.zero;
        sRt.anchoredPosition = Vector2.zero;

        const float spacing = 88f;
        const float startX = -spacing;   // 3 stars: -88, 0, +88
        const float starY = 195f;

        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject($"PerfStar_{i + 1}");
            go.transform.SetParent(_perfStarsSection.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(80f, 90f);
            rt.anchoredPosition = new Vector2(startX + i * spacing, starY);

            var txt = go.AddComponent<Text>();
            txt.text = "\u2606";   // ☆ hollow
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 72;
            txt.alignment = TextAnchor.UpperCenter;
            txt.color = ColorStarEmpty;
            txt.raycastTarget = false;

            _perfStarGlyphs[i] = txt;
            _perfStarGOs[i] = go;
        }
    }

    private void InitPerfStars()
    {
        for (int i = 0; i < 3; i++)
        {
            if (_perfStarGlyphs[i] == null) continue;
            _perfStarGlyphs[i].text = "\u2606";
            _perfStarGlyphs[i].color = ColorStarEmpty;
            if (_perfStarGOs[i] != null)
                _perfStarGOs[i].transform.localScale = Vector3.one;
        }
    }

    private IEnumerator AnimatePerfStars(int starsEarned)
    {
        // Show unearned stars immediately as hollow so the player sees the full 3-star frame
        for (int i = starsEarned; i < 3; i++)
        {
            if (_perfStarGlyphs[i] != null)
            {
                _perfStarGlyphs[i].text = "\u2606";
                _perfStarGlyphs[i].color = ColorStarEmpty;
            }
        }

        yield return new WaitForSecondsRealtime(0.9f);

        for (int i = 0; i < starsEarned; i++)
        {
            if (_perfStarGlyphs[i] == null || _perfStarGOs[i] == null) continue;

            _perfStarGlyphs[i].text = "\u2605";   // ★ filled
            _perfStarGlyphs[i].color = ColorGold;
            SfxPlayer.Instance?.PlayStarEarned();

            // Pop-in: 0 → 1.4 → 1.0
            var go = _perfStarGOs[i];
            go.transform.localScale = Vector3.zero;
            float elapsed = 0f;
            const float popDur = 0.10f;
            const float settleDur = 0.22f;

            while (elapsed < popDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(0f, 1.4f, elapsed / popDur);
                go.transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(1.4f, 1f, Mathf.SmoothStep(0f, 1f, elapsed / settleDur));
                go.transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            go.transform.localScale = Vector3.one;

            yield return new WaitForSecondsRealtime(0.38f);
        }

        _starAnimRoutine = null;
    }

    private void BuildRatingSection()
    {
        // "Rate This Level" label
        var labelGO = CreateTextGO(_panel, "Rate This Level", new Vector2(0f, -360f), 24,
            new Color(0.65f, 0.65f, 0.80f, 0.85f), "RateLabel");
        labelGO.GetComponent<Text>().raycastTarget = false;

        // 5 star buttons centred horizontally, 80 px apart
        const float spacing = 80f;
        const float startX = -spacing * 2f; // -160, -80, 0, 80, 160

        for (int i = 0; i < 5; i++)
        {
            int starNum = i + 1;   // capture for lambda

            var go = new GameObject($"Star_{starNum}");
            go.transform.SetParent(_panel.transform, false);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);  // transparent hit-area

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(72f, 90f);
            rt.anchoredPosition = new Vector2(startX + i * spacing, -428f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cols = btn.colors;
            cols.normalColor = Color.white;
            cols.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            cols.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = cols;
            btn.onClick.AddListener(() => OnStarClicked(starNum));

            // Star glyph (large ☆/★)
            var glyphGO = new GameObject("Glyph");
            glyphGO.transform.SetParent(go.transform, false);
            var glyphTxt = glyphGO.AddComponent<Text>();
            glyphTxt.text = "\u2606";  // ☆ hollow star
            glyphTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            glyphTxt.fontSize = 62;
            glyphTxt.alignment = TextAnchor.UpperCenter;
            glyphTxt.color = ColorStarEmpty;
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
            numTxt.text = starNum.ToString();
            numTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            numTxt.fontSize = 20;
            numTxt.fontStyle = FontStyle.Bold;
            numTxt.alignment = TextAnchor.MiddleCenter;
            numTxt.color = new Color(1f, 1f, 1f, 0.75f);
            numTxt.raycastTarget = false;
            var numRt = numGO.GetComponent<RectTransform>();
            numRt.anchorMin = Vector2.zero;
            numRt.anchorMax = Vector2.one;
            numRt.sizeDelta = Vector2.zero;
            numRt.anchoredPosition = new Vector2(0f, -50f);  // nudge into the lower body of the star
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void ShowVictory(int levelScore, int comboBonus, int bestCombo, string levelId, int levelIndex)
    {
        _currentLevelScore = levelScore;
        _currentLevelId = levelId;
        _currentLevelIndex = levelIndex;

        // Activate first so OnEnable subscribes to OnRankAwardResolved before
        // AwardLevelComplete fires it (synchronously in debug builds).
        gameObject.SetActive(true);

        // Submit to Steam per-level all-time leaderboard (KeepBest) — daily/weekly now use MySQL.
        if (levelScore > 0)
        {
            string allTimeBoard = PurrbricksLeaderboards.LevelAllTime(levelIndex);
            SteamLeaderboardManager.Instance?.SubmitScore(allTimeBoard, levelScore);
        }

        // Submit to MySQL daily/weekly leaderboard; use the returned ranks to award Purr Bucks.
        int livesLost     = (GameManager.Instance?.LivesAtLevelStart ?? 0) - (GameManager.Instance?.GetLives() ?? 0);
        bool perfectClear = livesLost <= 0;
        _purrBucksText?.gameObject.SetActive(false);

        if (PurrBucksManager.Instance != null && !string.IsNullOrEmpty(levelId))
        {
            if (levelScore > 0 && LevelScoreService.Instance != null)
            {
                ulong  steamId   = SteamworksBootstrap.Instance?.IsSteamAvailable == true
                                   ? SteamUser.GetSteamID().m_SteamID : 0UL;
                string steamName = SteamworksBootstrap.Instance?.IsSteamAvailable == true
                                   ? SteamFriends.GetPersonaName() : "Player";

                var currentLevel = LevelLoader.Instance?.CurrentLevel;
                string scoreGuid = !string.IsNullOrEmpty(currentLevel?.levelGuid)
                                   ? currentLevel.levelGuid : levelId;
                string levelName = !string.IsNullOrEmpty(currentLevel?.displayName)
                                   ? currentLevel.displayName : levelId;

                LevelScoreService.Instance.SubmitScore(scoreGuid, levelName, steamId, steamName, levelScore, result =>
                {
                    PurrBucksManager.Instance?.AwardLevelComplete(
                        levelId, levelIndex, perfectClear, livesLost,
                        result.DailyRank, result.WeeklyRank);
                });
            }
            else
            {
                // Fallback: no score or service unavailable — award without rank bonus.
                PurrBucksManager.Instance.AwardLevelComplete(levelId, levelIndex, perfectClear, livesLost);
            }
        }

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
        if (LevelRatingService.Instance != null)
            _currentRating = LevelRatingService.Instance.GetRating(levelId);
        else
        {
            _currentRating = 0;
            Debug.LogWarning("[VictoryUI] LevelRatingService.Instance is null — cannot read saved rating.");
        }
        UpdateStars(_currentRating);

        // ── Performance stars ────────────────────────────────────────────────
        if (_perfStarsSection != null) _perfStarsSection.SetActive(true);
        int par = GameManager.Instance?.CurrentLevelPar ?? 0;
        int starsEarned = LevelStarsHelper.CalculateStars(levelScore, par);
        LevelStarsHelper.SaveBestStars(levelId, starsEarned);

        InitPerfStars();
        if (_starAnimRoutine != null) StopCoroutine(_starAnimRoutine);
        _starAnimRoutine = StartCoroutine(AnimatePerfStars(starsEarned));

        TutorialManager.Instance?.TriggerIfNew(
            TutorialManager.ID.PerfStars,
            "\u2605 \u2605 \u2605",
            "STAR RATINGS",
            "Your score is compared to the level's base brick value.\n\n" +
            "\u2605\u2606\u2606  Completed = 1 Star\n" +
            "\u2605\u2605\u2606  Score \u2265 1.5\u00d7 par = 2 Stars\n" +
            "\u2605\u2605\u2605  Score \u2265 3\u00d7 par = 3 Stars\n\n" +
            "Stars appear on the Level Select screen.\nCan you purrfect them all?");

        SpawnVictoryFireworks();
    }

    // ── Community Victory ─────────────────────────────────────────────────────

    private CommunityLevelMeta _currentCommunityMeta;
    private bool _isCommunityMode;

    // "Browse More" button GO — only active in community mode
    private GameObject _browseMoreBtnGO;
    private GameObject _nextLevelBtnGO;

    public void ShowCommunityVictory(int levelScore, int comboBonus, int bestCombo, CommunityLevelMeta meta)
    {
        _isCommunityMode = true;
        _currentCommunityMeta = meta;
        _currentLevelId = $"cl_{meta.id}";
        _currentLevelScore = levelScore;

        // Activate first so OnEnable subscribes to OnRankAwardResolved
        gameObject.SetActive(true);

        // Submit score to community Steam boards
        if (levelScore > 0)
        {
            string allTimeBoard = PurrbricksLeaderboards.CommunityAllTime(meta.id);
            string weeklyBoard = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Weekly);
            string dailyBoard = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Daily);

            SteamLeaderboardManager.Instance?.SubmitScore(allTimeBoard, levelScore);
            SteamLeaderboardManager.Instance?.SubmitScore(weeklyBoard, levelScore);
            SteamLeaderboardManager.Instance?.SubmitScore(dailyBoard, levelScore);
        }

        // Award Purr Bucks
        if (PurrBucksManager.Instance != null)
        {
            int livesLost = (GameManager.Instance?.LivesAtLevelStart ?? 0) - (GameManager.Instance?.GetLives() ?? 0);
            bool perfectClear = livesLost <= 0;
            _purrBucksText?.gameObject.SetActive(false);
            PurrBucksManager.Instance.AwardCommunityLevelComplete(meta.id, perfectClear, livesLost);
        }

        if (_levelScoreText != null) _levelScoreText.text = $"Level Score:  {levelScore:N0}";
        if (_comboBonusText != null) _comboBonusText.text = comboBonus > 0
            ? $"Combo Bonus:  +{comboBonus:N0}" : "Combo Bonus:  —";
        if (_bestComboText != null) _bestComboText.text = bestCombo > 0
            ? $"Best Combo:  ×{bestCombo + 1}" : "Best Combo:  —";

        _newBestBanner?.SetActive(false); // no personal best tracking for community levels

        // Hide performance stars — not applicable to community levels
        if (_perfStarsSection != null) _perfStarsSection.SetActive(false);
        if (_starAnimRoutine != null) { StopCoroutine(_starAnimRoutine); _starAnimRoutine = null; }

        // Pre-load community rating
        _currentRating = CommunityLevelService.Instance?.GetMyRating(meta.id) ?? 0;
        UpdateStars(_currentRating);

        // Mark cleared
        CommunityLevelService.Instance?.MarkCleared(meta.id);

        // Swap Next Level → Browse More button
        if (_nextLevelBtnGO != null) _nextLevelBtnGO.SetActive(false);
        if (_browseMoreBtnGO != null) _browseMoreBtnGO.SetActive(true);

        SpawnVictoryFireworks();
    }

    private void OnNextLevel() => GameManager.Instance?.LoadNextLevel();
    private void OnReplayLevel() => GameManager.Instance?.ReplayCurrentLevel();
    private void OnLevelBoard()
    {
        if (_isCommunityMode && _currentCommunityMeta != null)
            GameManager.Instance?.ShowCommunityLevelLeaderboard(_currentCommunityMeta.id);
        else
            GameManager.Instance?.ShowLevelLeaderboard(_currentLevelIndex);
    }

    private void OnBrowseMore()
    {
        Hide();
        var browser = Object.FindFirstObjectByType<CommunityBrowserUI>(FindObjectsInactive.Include);
        if (browser != null) browser.Show();
        else GameManager.Instance?.ShowMainMenu();
    }

    private void OnLevelSelect()
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

    private void OnStarClicked(int starNum)
    {
        // Click the already-active star → clear the rating
        _currentRating = (_currentRating == starNum) ? 0 : starNum;
        UpdateStars(_currentRating);

        if (_isCommunityMode && _currentCommunityMeta != null)
        {
            CommunityLevelService.Instance?.RateLevel(_currentCommunityMeta.id, _currentRating, error =>
            {
                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning($"[VictoryUI] Community rating error: {error}");
            });
        }
        else
        {
            if (LevelRatingService.Instance != null)
                LevelRatingService.Instance.SetRating(_currentLevelId, _currentLevelIndex, _currentRating);
            else
                Debug.LogWarning("[VictoryUI] LevelRatingService.Instance is null — rating NOT saved! " +
                                 "Run 'Purrbricks > Setup Scene' to add the service to the scene.");
        }
    }

    private void UpdateStars(int rating)
    {
        for (int i = 0; i < 5; i++)
        {
            if (_starGlyphs[i] == null) continue;
            bool filled = i < rating;
            _starGlyphs[i].text = filled ? "\u2605" : "\u2606";   // ★ / ☆
            _starGlyphs[i].color = filled ? ColorGold : ColorStarEmpty;
        }
    }

    public void Show() { gameObject.SetActive(true); }

    /// <summary>
    /// Temporarily hide without resetting community mode state (used when opening leaderboard from victory).
    /// Call Show() to restore.
    /// </summary>
    public void HideForLeaderboard() { gameObject.SetActive(false); }

    public void Hide()
    {
        if (_starAnimRoutine != null) { StopCoroutine(_starAnimRoutine); _starAnimRoutine = null; }
        // Reset community mode state so next ShowVictory starts clean
        _isCommunityMode = false;
        _currentCommunityMeta = null;
        if (_nextLevelBtnGO != null) _nextLevelBtnGO.SetActive(true);
        if (_browseMoreBtnGO != null) _browseMoreBtnGO.SetActive(false);
        gameObject.SetActive(false);
    }

    // ── Text helpers ──────────────────────────────────────────────────────────

    private void CreateText(GameObject parent, string text, Vector2 pos, int fontSize, Color color, string objName = null)
        => CreateTextGO(parent, text, pos, fontSize, color, objName);

    private GameObject CreateTextGO(GameObject parent, string text, Vector2 pos, int fontSize, Color color, string objName = null)
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
        rt.sizeDelta = new Vector2(900f, fontSize + 24f);
        rt.anchoredPosition = pos;

        var ol = go.AddComponent<Outline>();
        ol.effectColor = Color.black;
        ol.effectDistance = new Vector2(4f, -4f);

        return go;
    }

    // ── Victory fireworks ─────────────────────────────────────────────────────

    private void SpawnVictoryFireworks()
    {
        if (_fireworksRoutine != null)
            StopCoroutine(_fireworksRoutine);

        if (_fireworksRoot != null)
            Destroy(_fireworksRoot);

        _fireworksRoot = new GameObject("VictoryFireworks");
        _fireworksRoutine = StartCoroutine(FireworksRoutine());
    }

    private IEnumerator FireworksRoutine()
    {
        // Stagger multiple launches so it reads as a "show" instead of a single burst.
        const int launches = 12;
        for (int i = 0; i < launches; i++)
        {
            float x = Random.Range(-5.2f, 5.2f);
            float y = Random.Range(-4.3f, -3.6f);
            SpawnSingleFirework(new Vector3(x, y, 0f), _fireworksRoot.transform);
            yield return new WaitForSecondsRealtime(Random.Range(0.10f, 0.18f));
        }

        // Allow particles to finish, then clean up.
        yield return new WaitForSecondsRealtime(3.2f);
        if (_fireworksRoot != null)
            Destroy(_fireworksRoot);
        _fireworksRoot = null;
        _fireworksRoutine = null;
    }

    private static void SpawnSingleFirework(Vector3 launchPos, Transform parent)
    {
        var root = new GameObject("Firework");
        root.transform.SetParent(parent, worldPositionStays: true);
        root.transform.position = launchPos;

        // Burst (sub-emitter) — the actual "firework" flower.
        var burstGO = new GameObject("Burst");
        burstGO.transform.SetParent(root.transform, worldPositionStays: false);
        burstGO.transform.localPosition = Vector3.zero;
        var burst = burstGO.AddComponent<ParticleSystem>();
        var bMain = burst.main;
        bMain.loop = false;
        bMain.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.35f);
        bMain.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 6.2f);
        bMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        bMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        bMain.gravityModifier = 0.25f;
        bMain.simulationSpace = ParticleSystemSimulationSpace.World;
        bMain.useUnscaledTime = true;

        var bEmission = burst.emission;
        bEmission.enabled = true;
        bEmission.rateOverTime = 0f;
        bEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Random.Range(70, 105)) });

        var bShape = burst.shape;
        bShape.enabled = true;
        bShape.shapeType = ParticleSystemShapeType.Sphere;
        bShape.radius = 0.08f;

        var bNoise = burst.noise;
        bNoise.enabled = true;
        bNoise.strength = 0.60f;
        bNoise.frequency = 1.8f;
        bNoise.scrollSpeed = 0.8f;
        bNoise.quality = ParticleSystemNoiseQuality.Low;

        // Color over lifetime: bright -> transparent.
        var hue = Random.value;
        Color c0 = Color.HSVToRGB(hue, 0.65f, 1.00f);
        Color c1 = Color.HSVToRGB(Mathf.Repeat(hue + 0.10f, 1f), 0.55f, 1.00f);

        var bCol = burst.colorOverLifetime;
        bCol.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 0.55f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.35f, 0.70f), new GradientAlphaKey(0f, 1f) }
        );
        bCol.color = new ParticleSystem.MinMaxGradient(grad);

        var bSize = burst.sizeOverLifetime;
        bSize.enabled = true;
        bSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.05f));

        var bTrails = burst.trails;
        bTrails.enabled = true;
        bTrails.mode = ParticleSystemTrailMode.PerParticle;
        bTrails.ratio = 1f;
        bTrails.lifetime = 0.18f;
        bTrails.minVertexDistance = 0.05f;
        bTrails.dieWithParticles = true;
        bTrails.inheritParticleColor = true;
        bTrails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 0f));

        var bRend = burst.GetComponent<ParticleSystemRenderer>();
        bRend.material = VfxMaterials.Additive;
        bRend.trailMaterial = VfxMaterials.Additive;
        bRend.sortingOrder = 250;

        // Rocket — single particle with a streak; on death it spawns the burst above.
        var rocket = root.AddComponent<ParticleSystem>();
        var rMain = rocket.main;
        rMain.loop = false;
        rMain.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.05f);
        rMain.startSpeed = new ParticleSystem.MinMaxCurve(10.5f, 13.5f);
        rMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        rMain.startColor = new ParticleSystem.MinMaxGradient(c0);
        rMain.gravityModifier = -0.10f; // slight lift
        rMain.simulationSpace = ParticleSystemSimulationSpace.World;
        rMain.useUnscaledTime = true;

        var rEmission = rocket.emission;
        rEmission.enabled = true;
        rEmission.rateOverTime = 0f;
        rEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var rShape = rocket.shape;
        rShape.enabled = true;
        rShape.shapeType = ParticleSystemShapeType.Cone;
        rShape.angle = 2.5f;
        rShape.radius = 0.02f;

        var rTrails = rocket.trails;
        rTrails.enabled = true;
        rTrails.mode = ParticleSystemTrailMode.PerParticle;
        rTrails.ratio = 1f;
        rTrails.lifetime = 0.35f;
        rTrails.minVertexDistance = 0.03f;
        rTrails.dieWithParticles = true;
        rTrails.inheritParticleColor = true;
        rTrails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, 0f));

        // Sub-emitter wiring: rocket death -> burst.
        var sub = rocket.subEmitters;
        sub.enabled = true;
        sub.AddSubEmitter(burst, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);

        var rRend = rocket.GetComponent<ParticleSystemRenderer>();
        rRend.material = VfxMaterials.Additive;
        rRend.trailMaterial = VfxMaterials.Additive;
        rRend.sortingOrder = 250;

        rocket.Play(withChildren: true);

        Object.Destroy(root, 4.0f);
    }
}
