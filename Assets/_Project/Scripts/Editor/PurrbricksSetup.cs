// Editor-only setup script — does NOT affect builds
#if UNITY_EDITOR
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
        var ball = Object.FindFirstObjectByType<BallController>();
        if (ball != null)
        {
            if (ball.GetComponent<TrailRenderer>() == null)
                ball.gameObject.AddComponent<TrailRenderer>();
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
            // Always refresh to 30 levels
            levelIds.arraySize = 30;
            for (int i = 0; i < 30; i++)
                levelIds.GetArrayElementAtIndex(i).stringValue = $"level_{i:D2}";
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

        // ── 12. Mark scene dirty ─────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("Purrbricks setup complete! Press Ctrl+S to save the scene.");

        if (brickArea   == null) Debug.LogWarning("Could not find BrickArea — assign _brickArea on LevelLoader manually.");
        if (brickPrefab == null) Debug.LogWarning("Could not load Brick prefab at Assets/_Project/Prefabs/Brick.prefab.");
        if (gm          == null) Debug.LogWarning("Could not find GameManager in scene.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
