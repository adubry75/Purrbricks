// Editor-only setup script — does NOT affect builds
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

public static class PurrbricksSetup
{
    [MenuItem("Purrbricks/Setup Scene")]
    public static void SetupScene()
    {
        // ── 1. BrickTemplate assets ─────────────────────────────────────────
        EnsureDir("Assets/_Project/BrickTemplates");

        // Arcade palette: vivid, saturated colors
        var standard = EnsureTemplate("standard", "Standard", 1,   100,  new Color(0.85f, 0.85f, 0.95f));
        var red      = EnsureTemplate("red",      "Red",      1,   300,  new Color(1.00f, 0.18f, 0.25f));
        var blue     = EnsureTemplate("blue",     "Blue",     1,   200,  new Color(0.20f, 0.55f, 1.00f));
        var steel    = EnsureTemplate("steel",    "Steel",    4,   1000, new Color(0.72f, 0.82f, 0.95f));
        var gem      = EnsureTemplate("gem",      "Gem",      1,  10000, new Color(1.00f, 0.25f, 0.75f));
        // ── New brick types for levels 10-29 ─────────────────────────────────
        var gold     = EnsureTemplate("gold",     "Gold",     2,   500,  new Color(1.00f, 0.78f, 0.05f)); // 2-hit golden brick
        var purple   = EnsureTemplate("purple",   "Purple",   1,   400,  new Color(0.60f, 0.05f, 0.88f)); // vivid purple
        var green    = EnsureTemplate("green",    "Green",    1,   250,  new Color(0.10f, 0.88f, 0.35f)); // vivid green
        var cyan     = EnsureTemplate("cyan",     "Cyan",     1,   200,  new Color(0.10f, 0.88f, 0.95f)); // electric cyan
        var dark     = EnsureTemplate("dark",     "Dark",     3,   750,  new Color(0.25f, 0.10f, 0.42f)); // dark violet, 3-hit

        AssetDatabase.SaveAssets();

        // ── 2. BrickTemplateRegistry ─────────────────────────────────────────
        var regGO = EnsureGO("BrickTemplateRegistry");
        var reg = regGO.GetComponent<BrickTemplateRegistry>();
        if (reg == null) reg = regGO.AddComponent<BrickTemplateRegistry>();

        var regSo = new SerializedObject(reg);
        var tArr = regSo.FindProperty("_templates");
        tArr.arraySize = 10;
        tArr.GetArrayElementAtIndex(0).objectReferenceValue = standard;
        tArr.GetArrayElementAtIndex(1).objectReferenceValue = red;
        tArr.GetArrayElementAtIndex(2).objectReferenceValue = blue;
        tArr.GetArrayElementAtIndex(3).objectReferenceValue = steel;
        tArr.GetArrayElementAtIndex(4).objectReferenceValue = gem;
        tArr.GetArrayElementAtIndex(5).objectReferenceValue = gold;
        tArr.GetArrayElementAtIndex(6).objectReferenceValue = purple;
        tArr.GetArrayElementAtIndex(7).objectReferenceValue = green;
        tArr.GetArrayElementAtIndex(8).objectReferenceValue = cyan;
        tArr.GetArrayElementAtIndex(9).objectReferenceValue = dark;
        regSo.ApplyModifiedProperties();

        // ── 3. BrickSkinRegistry ──────────────────────────────────────────────
        var skinRegGO = EnsureGO("BrickSkinRegistry");
        if (skinRegGO.GetComponent<BrickSkinRegistry>() == null)
            skinRegGO.AddComponent<BrickSkinRegistry>();

        // ── 4. PowerupRegistry ───────────────────────────────────────────────
        var puRegGO = EnsureGO("PowerupRegistry");
        if (puRegGO.GetComponent<PowerupRegistry>() == null)
            puRegGO.AddComponent<PowerupRegistry>();

        // ── 5. LevelLoader ───────────────────────────────────────────────────
        var loaderGO = EnsureGO("LevelLoader");
        var loader = loaderGO.GetComponent<LevelLoader>();
        if (loader == null) loader = loaderGO.AddComponent<LevelLoader>();

        // Find BrickArea (BoxCollider2D named "BrickArea" or the first one found)
        var brickAreaGO = GameObject.Find("BrickArea");
        BoxCollider2D brickArea = brickAreaGO != null
            ? brickAreaGO.GetComponent<BoxCollider2D>()
            : Object.FindFirstObjectByType<BoxCollider2D>();

        // Find Brick prefab
        var brickPrefab = AssetDatabase.LoadAssetAtPath<Brick>("Assets/_Project/Prefabs/Brick.prefab");

        var loaderSo = new SerializedObject(loader);
        if (brickArea  != null) loaderSo.FindProperty("_brickArea").objectReferenceValue  = brickArea;
        if (brickPrefab != null) loaderSo.FindProperty("_brickPrefab").objectReferenceValue = brickPrefab;
        loaderSo.ApplyModifiedProperties();

        // ── 6. Add BrickVisualController to Brick prefab ─────────────────────
        if (brickPrefab != null)
        {
            var prefabPath = "Assets/_Project/Prefabs/Brick.prefab";
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot.GetComponent<BrickVisualController>() == null)
                prefabRoot.AddComponent<BrickVisualController>();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.Log("Added BrickVisualController to Brick prefab.");
        }

        // ── 6b. Add BallTrail to Ball (find in scene) ────────────────────────
        // BallTrail manages its own ParticleSystem internally; no TrailRenderer needed.
        var ball = Object.FindFirstObjectByType<BallController>();
        if (ball != null)
        {
            if (ball.GetComponent<BallTrail>() == null)
                ball.gameObject.AddComponent<BallTrail>();
            Debug.Log("Added BallTrail to Ball.");
        }

        // ── 7. GameManager ───────────────────────────────────────────────────
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            var gmSo = new SerializedObject(gm);
            gmSo.FindProperty("_levelLoader").objectReferenceValue = loader;

            var levelIds = gmSo.FindProperty("_levelIds");

            // Refresh from actual level json files present in Resources/Levels.
            // This keeps demo levels (and any future additions) automatically included.
            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_Project/Resources/Levels" });
            var found = new List<(int index, string id)>(guids.Length);
            var rx = new Regex(@"^level_(\d+)$", RegexOptions.IgnoreCase);
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string name = Path.GetFileNameWithoutExtension(path);
                var m = rx.Match(name);
                if (!m.Success) continue;
                if (int.TryParse(m.Groups[1].Value, out int idx))
                    found.Add((idx, name));
            }
            found.Sort((a, b) => a.index.CompareTo(b.index));

            levelIds.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++)
                levelIds.GetArrayElementAtIndex(i).stringValue = found[i].id;
            gmSo.ApplyModifiedProperties();
        }

        // ── 8. CameraShake on Main Camera ────────────────────────────────────
        var mainCam = Camera.main;
        if (mainCam != null && mainCam.GetComponent<CameraShake>() == null)
        {
            mainCam.gameObject.AddComponent<CameraShake>();
            Debug.Log("Added CameraShake to Main Camera.");
        }

        // ── 9. ParallaxBackground ────────────────────────────────────────────
        var bgGO = EnsureGO("ParallaxBackground");
        if (bgGO.GetComponent<ParallaxBackground>() == null)
        {
            bgGO.AddComponent<ParallaxBackground>();
            Debug.Log("Added ParallaxBackground.");
        }

        // ── 10. HighScoreManager ─────────────────────────────────────────────
        var scoresMgr = EnsureGO("HighScoreManager");
        if (scoresMgr.GetComponent<HighScoreManager>() == null)
        {
            scoresMgr.AddComponent<HighScoreManager>();
            Debug.Log("Added HighScoreManager.");
        }

        // ── 10b. PurrBucksManager ────────────────────────────────────────────
        var purrBucksMgrGO = EnsureGO("PurrBucksManager");
        if (purrBucksMgrGO.GetComponent<PurrBucksManager>() == null)
        {
            purrBucksMgrGO.AddComponent<PurrBucksManager>();
            Debug.Log("Added PurrBucksManager.");
        }

        // ── 10c. StoreUI ─────────────────────────────────────────────────────
        var storeUIGO = EnsureGO("StoreUI");
        if (storeUIGO.GetComponent<StoreUI>() == null)
        {
            storeUIGO.AddComponent<StoreUI>();
            Debug.Log("Added StoreUI.");
        }

        // ── 10d. InventoryRadialMenu ─────────────────────────────────────────
        var radialMenuGO = EnsureGO("InventoryRadialMenu");
        if (radialMenuGO.GetComponent<InventoryRadialMenu>() == null)
        {
            radialMenuGO.AddComponent<InventoryRadialMenu>();
            Debug.Log("Added InventoryRadialMenu.");
        }

        // ── 11a. PowerupManager ──────────────────────────────────────────────
        var puMgrGO = EnsureGO("PowerupManager");
        if (puMgrGO.GetComponent<PowerupManager>() == null)
        {
            puMgrGO.AddComponent<PowerupManager>();
            Debug.Log("Added PowerupManager.");
        }

        // ── 11b. PowerupHUD ──────────────────────────────────────────────────
        var puHudGO = EnsureGO("PowerupHUD");
        if (puHudGO.GetComponent<PowerupHUD>() == null)
        {
            puHudGO.AddComponent<PowerupHUD>();
            Debug.Log("Added PowerupHUD.");
        }

        // ── 11c. ScreenEffects ───────────────────────────────────────────────
        var screenFxGO = EnsureGO("ScreenEffects");
        if (screenFxGO.GetComponent<ScreenEffects>() == null)
        {
            screenFxGO.AddComponent<ScreenEffects>();
            Debug.Log("Added ScreenEffects.");
        }

        // ── 11c2. UITheme (shared UI sprites) ───────────────────────────────
        var themeGO = EnsureGO("UITheme");
        if (themeGO.GetComponent<UITheme>() == null)
        {
            themeGO.AddComponent<UITheme>();
            Debug.Log("Added UITheme.");
        }

        // ── 11d. PowerupNotification ─────────────────────────────────────────
        var puNotifGO = EnsureGO("PowerupNotification");
        if (puNotifGO.GetComponent<PowerupNotification>() == null)
        {
            puNotifGO.AddComponent<PowerupNotification>();
            Debug.Log("Added PowerupNotification.");
        }

        // ── 11e. HavocBar (Fury Strike charge bar) ───────────────────────────
        var havocGO = EnsureGO("HavocBar");
        if (havocGO.GetComponent<HavocBar>() == null)
        {
            havocGO.AddComponent<HavocBar>();
            Debug.Log("Added HavocBar.");
        }

        // ── 11f. MusicPlayer ─────────────────────────────────────────────────
        var musicGO = EnsureGO("MusicPlayer");
        var music = musicGO.GetComponent<MusicPlayer>();
        if (music == null) music = musicGO.AddComponent<MusicPlayer>();

        var musicSo = new SerializedObject(music);

        var menuClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/_Project/Audio/purrbricks-mainmenu-Neon Echo Mainframe.mp3");
        var goClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/_Project/Audio/purrbricks-gameover1.mp3");
        var finishClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/_Project/Audio/purrbricks-levelfinish1.mp3");
        var gp1 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay1.mp3");
        var gp2 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay2.mp3");
        var gp3 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay3.mp3");
        var gp4 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay4.mp3");
        var gp5 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay5.mp3");
        var gp6 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay6.mp3");
        var gp7 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay7.mp3");
        var gp8 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay8.mp3");
        var gp9 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay9.mp3");
        var gp10 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay10.mp3");
        var gp11 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay11.mp3");
        var gp12 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay12.mp3");
        var gp13 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay13.mp3");
        var gp14 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay14.mp3");
        var gp15 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay15.mp3");
        var gp16 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay16.mp3");
        var gp17 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay17.mp3");
        var gp18 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay18.mp3");
        var gp19 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay19.mp3");
        var gp20 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay20.mp3");
        var gp21 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay21.mp3");
        var gp22 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay22.mp3");
        var gp23 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/purrbricks-gameplay23.mp3");

        musicSo.FindProperty("_menuTrack").objectReferenceValue      = menuClip;
        musicSo.FindProperty("_gameOverTrack").objectReferenceValue  = goClip;
        musicSo.FindProperty("_levelFinishTrack").objectReferenceValue = finishClip;

        var gpArr = musicSo.FindProperty("_gameplayTracks");
        gpArr.arraySize = 23;
        gpArr.GetArrayElementAtIndex(0).objectReferenceValue = gp1;
        gpArr.GetArrayElementAtIndex(1).objectReferenceValue = gp2;
        gpArr.GetArrayElementAtIndex(2).objectReferenceValue = gp3;
        gpArr.GetArrayElementAtIndex(3).objectReferenceValue = gp4;
        gpArr.GetArrayElementAtIndex(4).objectReferenceValue = gp5;
        gpArr.GetArrayElementAtIndex(5).objectReferenceValue = gp6;
        gpArr.GetArrayElementAtIndex(6).objectReferenceValue = gp7;
        gpArr.GetArrayElementAtIndex(7).objectReferenceValue = gp8;
        gpArr.GetArrayElementAtIndex(8).objectReferenceValue = gp9;
        gpArr.GetArrayElementAtIndex(9).objectReferenceValue = gp10;
        gpArr.GetArrayElementAtIndex(10).objectReferenceValue = gp11;
        gpArr.GetArrayElementAtIndex(11).objectReferenceValue = gp12;
        gpArr.GetArrayElementAtIndex(12).objectReferenceValue = gp13;
        gpArr.GetArrayElementAtIndex(13).objectReferenceValue = gp14;
        gpArr.GetArrayElementAtIndex(14).objectReferenceValue = gp15;
        gpArr.GetArrayElementAtIndex(15).objectReferenceValue = gp16;
        gpArr.GetArrayElementAtIndex(16).objectReferenceValue = gp17;
        gpArr.GetArrayElementAtIndex(17).objectReferenceValue = gp18;
        gpArr.GetArrayElementAtIndex(18).objectReferenceValue = gp19;
        gpArr.GetArrayElementAtIndex(19).objectReferenceValue = gp20;
        gpArr.GetArrayElementAtIndex(20).objectReferenceValue = gp21;
        gpArr.GetArrayElementAtIndex(21).objectReferenceValue = gp22;
        gpArr.GetArrayElementAtIndex(22).objectReferenceValue = gp23;

        musicSo.ApplyModifiedProperties();
        Debug.Log("Wired MusicPlayer with all audio clips.");

        // ── 11. UI Screens ───────────────────────────────────────────────────
        var mainMenuGO = EnsureGO("MainMenuUI");
        if (mainMenuGO.GetComponent<MainMenuUI>() == null)
        {
            mainMenuGO.AddComponent<MainMenuUI>();
            Debug.Log("Added MainMenuUI.");
        }

        var gameOverGO = EnsureGO("GameOverUI");
        if (gameOverGO.GetComponent<GameOverUI>() == null)
        {
            gameOverGO.AddComponent<GameOverUI>();
            Debug.Log("Added GameOverUI.");
        }

        var victoryGO = EnsureGO("VictoryUI");
        if (victoryGO.GetComponent<VictoryUI>() == null)
        {
            victoryGO.AddComponent<VictoryUI>();
            Debug.Log("Added VictoryUI.");
        }

        var highScoresGO = EnsureGO("HighScoresUI");
        if (highScoresGO.GetComponent<HighScoresUI>() == null)
        {
            highScoresGO.AddComponent<HighScoresUI>();
            Debug.Log("Added HighScoresUI.");
        }

        var steamLbMgrGO = EnsureGO("SteamLeaderboardManager");
        if (steamLbMgrGO.GetComponent<SteamLeaderboardManager>() == null)
        {
            steamLbMgrGO.AddComponent<SteamLeaderboardManager>();
            Debug.Log("Added SteamLeaderboardManager.");
        }

        var achMgrGO = EnsureGO("AchievementManager");
        if (achMgrGO.GetComponent<AchievementManager>() == null)
        {
            achMgrGO.AddComponent<AchievementManager>();
            Debug.Log("Added AchievementManager.");
        }

        var codesMgrGO = EnsureGO("LevelCodeManager");
        if (codesMgrGO.GetComponent<LevelCodeManager>() == null)
        {
            codesMgrGO.AddComponent<LevelCodeManager>();
            Debug.Log("Added LevelCodeManager.");
        }

        var codeEntryGO = EnsureGO("LevelCodeEntryUI");
        if (codeEntryGO.GetComponent<LevelCodeEntryUI>() == null)
        {
            codeEntryGO.AddComponent<LevelCodeEntryUI>();
            Debug.Log("Added LevelCodeEntryUI.");
        }

        var settingsMgrGO = EnsureGO("SettingsManager");
        if (settingsMgrGO.GetComponent<SettingsManager>() == null)
        {
            settingsMgrGO.AddComponent<SettingsManager>();
            Debug.Log("Added SettingsManager.");
        }

        var settingsUIGO = EnsureGO("SettingsUI");
        if (settingsUIGO.GetComponent<SettingsUI>() == null)
        {
            settingsUIGO.AddComponent<SettingsUI>();
            Debug.Log("Added SettingsUI.");
        }

        var pauseMenuGO = EnsureGO("PauseMenuUI");
        if (pauseMenuGO.GetComponent<PauseMenuUI>() == null)
        {
            pauseMenuGO.AddComponent<PauseMenuUI>();
            Debug.Log("Added PauseMenuUI.");
        }

        var levelRatingGO = EnsureGO("LevelRatingService");
        if (levelRatingGO.GetComponent<LevelRatingService>() == null)
        {
            levelRatingGO.AddComponent<LevelRatingService>();
            Debug.Log("Added LevelRatingService.");
        }

        // ── Level Editor UI ──────────────────────────────────────────────────
        var levelEditorGO = EnsureGO("LevelEditorUI");
        var levelEditorUI = levelEditorGO.GetComponent<LevelEditorUI>();
        if (levelEditorUI == null)
        {
            levelEditorUI = levelEditorGO.AddComponent<LevelEditorUI>();
            Debug.Log("Added LevelEditorUI.");
        }

        var levelBrowserGO = EnsureGO("LevelEditorBrowserUI");
        var levelBrowserUI = levelBrowserGO.GetComponent<LevelEditorBrowserUI>();
        if (levelBrowserUI == null)
        {
            levelBrowserUI = levelBrowserGO.AddComponent<LevelEditorBrowserUI>();
            Debug.Log("Added LevelEditorBrowserUI.");
        }

        // Wire them together
        levelBrowserUI.SetEditorUI(levelEditorUI);
        levelEditorUI.SetBrowser(levelBrowserUI);

        // ── 12. Main menu button sprites + ball/paddle sprites ───────────────
        // Refresh asset database so newly-added Art files are recognized
        AssetDatabase.Refresh();

        Sprite LoadSprite(string path)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in all)
                if (a is Sprite s) return s;
            return null;
        }

        Sprite LoadButton(string file)
        {
            // Prefer the new folder, but keep legacy fallback.
            return LoadSprite("Assets/_Project/Art/Buttons/" + file)
                ?? LoadSprite("Assets/_Project/Art/" + file);
        }

        // Shared button template sprite (used by UIStyle.CreateButton)
        var theme = Object.FindFirstObjectByType<UITheme>(FindObjectsInactive.Include);
        if (theme != null)
        {
            var so = new SerializedObject(theme);
            var tmpl = LoadButton("button-template.png");
            if (tmpl != null) so.FindProperty("_buttonTemplate").objectReferenceValue = tmpl;
            so.ApplyModifiedProperties();
        }

        // Parallax background art (optional)
        var parallax = Object.FindFirstObjectByType<ParallaxBackground>(FindObjectsInactive.Include);
        if (parallax != null)
        {
            var so = new SerializedObject(parallax);
            var stars  = LoadSprite("Assets/_Project/Art/stars_far.png");
            var nebula = LoadSprite("Assets/_Project/Art/nebula_mid.png");
            var dust   = LoadSprite("Assets/_Project/Art/dust_near.png");
            if (stars  != null) so.FindProperty("_starsFarSprite").objectReferenceValue   = stars;
            if (nebula != null) so.FindProperty("_nebulaMidSprite").objectReferenceValue = nebula;
            if (dust   != null) so.FindProperty("_dustNearSprite").objectReferenceValue   = dust;
            so.ApplyModifiedProperties();
        }

        var mainMenuUI = Object.FindFirstObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        if (mainMenuUI != null)
        {
            var mmSo = new SerializedObject(mainMenuUI);

            var sp1 = LoadButton("play-button.png");
            var sp2 = LoadButton("highscores-button.png");
            var spS = LoadButton("settings-button.png");
            var sp3 = LoadButton("quit-button.png");

            if (sp1 != null) mmSo.FindProperty("_playSprite").objectReferenceValue        = sp1;
            if (sp2 != null) mmSo.FindProperty("_highScoresSprite").objectReferenceValue  = sp2;
            if (spS != null) mmSo.FindProperty("_settingsSprite").objectReferenceValue    = spS;
            if (sp3 != null) mmSo.FindProperty("_quitSprite").objectReferenceValue        = sp3;
            mmSo.ApplyModifiedProperties();

            if (sp1 != null) Debug.Log("Assigned main menu button sprites.");
            else Debug.LogWarning("Could not find button sprites in Assets/_Project/Art/Buttons/ (or legacy Assets/_Project/Art/).");
        }

        // Assign other UI button sprites (new art in Art/Buttons)
        var victoryUI = Object.FindFirstObjectByType<VictoryUI>(FindObjectsInactive.Include);
        if (victoryUI != null)
        {
            var so = new SerializedObject(victoryUI);
            var next = LoadButton("nextlevel-button.png");
            var replay = LoadButton("replaylevel-button.png");
            var rankings = LoadButton("levelrankings-button.png");
            if (next != null) so.FindProperty("_nextLevelSprite").objectReferenceValue = next;
            if (replay != null) so.FindProperty("_replayLevelSprite").objectReferenceValue = replay;
            if (rankings != null) so.FindProperty("_levelRankingsSprite").objectReferenceValue = rankings;
            so.ApplyModifiedProperties();
        }

        var pauseUI = Object.FindFirstObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
        if (pauseUI != null)
        {
            var so = new SerializedObject(pauseUI);
            var resume = LoadButton("resume-button.png");
            var settings = LoadButton("settings-button.png");
            var mm = LoadButton("mainmenu-button.png");
            var quit = LoadButton("quit-button.png");
            if (resume != null) so.FindProperty("_resumeSprite").objectReferenceValue = resume;
            if (settings != null) so.FindProperty("_settingsSprite").objectReferenceValue = settings;
            if (mm != null) so.FindProperty("_mainMenuSprite").objectReferenceValue = mm;
            if (quit != null) so.FindProperty("_quitSprite").objectReferenceValue = quit;
            so.ApplyModifiedProperties();
        }

        var gameOverUI = Object.FindFirstObjectByType<GameOverUI>(FindObjectsInactive.Include);
        if (gameOverUI != null)
        {
            var so = new SerializedObject(gameOverUI);
            var lb = LoadButton("leaderboard-button.png");
            var mm = LoadButton("mainmenu-button.png");
            if (lb != null) so.FindProperty("_leaderboardSprite").objectReferenceValue = lb;
            if (mm != null) so.FindProperty("_mainMenuSprite").objectReferenceValue = mm;
            so.ApplyModifiedProperties();
        }

        var highScoresUI = Object.FindFirstObjectByType<HighScoresUI>(FindObjectsInactive.Include);
        if (highScoresUI != null)
        {
            var so = new SerializedObject(highScoresUI);
            var mm = LoadButton("mainmenu-button.png");
            if (mm != null) so.FindProperty("_mainMenuSprite").objectReferenceValue = mm;
            so.ApplyModifiedProperties();
        }

        var settingsUI = Object.FindFirstObjectByType<SettingsUI>(FindObjectsInactive.Include);
        if (settingsUI != null)
        {
            var so = new SerializedObject(settingsUI);
            var apply = LoadButton("apply-button.png");
            var back  = LoadButton("back-button.png");
            if (apply != null) so.FindProperty("_applySprite").objectReferenceValue = apply;
            if (back  != null) so.FindProperty("_backSprite").objectReferenceValue  = back;
            so.ApplyModifiedProperties();
        }

        // Ball sprite — assign + auto-fix PPU to match collider
        var ballCtrl = Object.FindFirstObjectByType<BallController>(FindObjectsInactive.Include);
        if (ballCtrl != null)
        {
            var sr = ballCtrl.GetComponent<SpriteRenderer>();
            var ballSprite = LoadSprite("Assets/_Project/Art/Sprites/cat-ball.png");
            if (sr != null && ballSprite != null)
            {
                var srSo = new SerializedObject(sr);
                srSo.FindProperty("m_Sprite").objectReferenceValue = ballSprite;
                srSo.ApplyModifiedProperties();

                // Compute world diameter from collider so the sprite fits the hit area
                float worldDiam = 0.4f; // fallback
                var circleCol = ballCtrl.GetComponent<CircleCollider2D>();
                if (circleCol != null)
                    worldDiam = circleCol.radius * 2f * Mathf.Abs(ballCtrl.transform.lossyScale.x);
                else
                {
                    var boxCol2 = ballCtrl.GetComponent<BoxCollider2D>();
                    if (boxCol2 != null)
                        worldDiam = boxCol2.size.x * Mathf.Abs(ballCtrl.transform.lossyScale.x);
                }
                //SetSpritePPU(ballSprite, worldDiam);
                Debug.Log($"Assigned cat-ball.png and world diameter {worldDiam:F2}u.");
            }
            else if (ballSprite == null)
                Debug.LogWarning("ball.png not found in Assets/_Project/Art/ — import it into Unity first.");
        }

        // Paddle sprite — assign + auto-fix PPU to match collider width
        var paddleCtrl = Object.FindFirstObjectByType<PaddleController>(FindObjectsInactive.Include);
        if (paddleCtrl != null)
        {
            var sr = paddleCtrl.GetComponent<SpriteRenderer>();
            var paddleSprite = LoadSprite("Assets/_Project/Art/Sprites/cat-paddle.png");
            if (sr != null && paddleSprite != null)
            {
                var srSo = new SerializedObject(sr);
                srSo.FindProperty("m_Sprite").objectReferenceValue = paddleSprite;
                srSo.ApplyModifiedProperties();

                // Compute world width from collider
                float worldWidth = 2f; // fallback
                var boxCol2 = paddleCtrl.GetComponent<BoxCollider2D>();
                if (boxCol2 != null)
                    worldWidth = boxCol2.size.x * Mathf.Abs(paddleCtrl.transform.lossyScale.x);
                //SetSpritePPU(paddleSprite, worldWidth);
                Debug.Log($"Assigned cat-paddle.png and world width {worldWidth:F2}u.");
            }
            else if (paddleSprite == null)
                Debug.LogWarning("paddle.png not found in Assets/_Project/Art/ — import it into Unity first.");
        }

        // ── Mark scene dirty ─────────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("Purrbricks setup complete! Press Ctrl+S to save the scene.");

        if (brickArea   == null) Debug.LogWarning("Could not find BrickArea — assign _brickArea on LevelLoader manually.");
        if (brickPrefab == null) Debug.LogWarning("Could not load Brick prefab at Assets/_Project/Prefabs/Brick.prefab.");
        if (gm          == null) Debug.LogWarning("Could not find GameManager in scene.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the Pixels Per Unit on a sprite's texture importer so that
    /// the sprite displays at exactly <paramref name="desiredWorldWidth"/> Unity units wide.
    /// PPU = texture_pixel_width / desired_world_width.
    /// </summary>
    private static void SetSpritePPU(Sprite sprite, float desiredWorldWidth)
    {
        if (sprite == null || desiredWorldWidth <= 0f) return;

        var path = AssetDatabase.GetAssetPath(sprite.texture);
        if (string.IsNullOrEmpty(path)) return;

        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;

        int newPPU = Mathf.Max(1, Mathf.RoundToInt(sprite.texture.width / desiredWorldWidth));
        if (ti.spritePixelsPerUnit == newPPU) return; // already correct

        ti.spritePixelsPerUnit = newPPU;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static GameObject EnsureGO(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        return go;
    }

    private static void EnsureDir(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parts  = path.Split('/');
            var parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }
    }

    private static BrickTemplate EnsureTemplate(
        string id, string displayName, int hp, int pts, Color tint)
    {
        var path     = $"Assets/_Project/BrickTemplates/BrickTemplate_{id}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<BrickTemplate>(path);

        if (existing != null)
        {
            // Update fields in case they changed
            existing.id           = id;
            existing.displayName  = displayName;
            existing.defaultHp    = hp;
            existing.defaultPoints = pts;
            existing.defaultTint  = tint;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var t = ScriptableObject.CreateInstance<BrickTemplate>();
        t.id            = id;
        t.displayName   = displayName;
        t.defaultHp     = hp;
        t.defaultPoints = pts;
        t.defaultTint   = tint;
        AssetDatabase.CreateAsset(t, path);
        return t;
    }
}
#endif
