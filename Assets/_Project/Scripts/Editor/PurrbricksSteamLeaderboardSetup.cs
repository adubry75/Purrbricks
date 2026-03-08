#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

public static class PurrbricksSteamLeaderboardSetup
{
    [MenuItem("Purrbricks/Setup Steam Leaderboard UI")]
    public static void SetupSteamLeaderboardUI()
    {
        EnsureEventSystem();

        var bootstrapGO = EnsureGameObject("SteamworksBootstrap", () => new GameObject("SteamworksBootstrap"));
        EnsureComponent<SteamworksBootstrap>(bootstrapGO);

        var serviceGO = EnsureGameObject("SteamLeaderboardService", () => new GameObject("SteamLeaderboardService"));
        var service = EnsureComponent<SteamLeaderboardService>(serviceGO);

        var uiRoot = EnsureGameObject("SteamHighScoreUI", () => new GameObject("SteamHighScoreUI"));
        var uiDemo = EnsureComponent<LeaderboardUIDemo>(uiRoot);

        var canvasGO = EnsureGameObject("SteamLeaderboardCanvas", () =>
        {
            var go = new GameObject("SteamLeaderboardCanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
            return go;
        });

        canvasGO.transform.SetParent(uiRoot.transform, false);

        var font = FindTemporaryFont();
        var currentY = -20f;

        var scoreInput = CreateInputField(canvasGO.transform, ref currentY, font);
        var buttonRow = CreateButtonRow(canvasGO.transform, ref currentY);
        var submitButton = CreateButton(buttonRow.transform, "Submit Score", font);
        var randomButton = CreateButton(buttonRow.transform, "Submit Random Score", font);
        var refreshButton = CreateButton(buttonRow.transform, "Refresh Top 10", font);
        var aroundButton = CreateButton(buttonRow.transform, "Around Me", font);

        var resultsArea = CreateResultsArea(canvasGO.transform, ref currentY, font);

        var unavailablePanel = CreateUnavailablePanel(canvasGO.transform, font);
        var unavailableText = unavailablePanel.GetComponentInChildren<TextMeshProUGUI>();
        unavailablePanel.SetActive(false);

        HookButton(submitButton, uiDemo.OnSubmitScore);
        HookButton(randomButton, uiDemo.OnSubmitRandomScore);
        HookButton(refreshButton, uiDemo.OnRefreshTop);
        HookButton(aroundButton, uiDemo.OnViewAroundMe);

        AssignSerializedField(uiDemo, "leaderboardService", service);
        AssignSerializedField(uiDemo, "scoreInput", scoreInput);
        AssignSerializedField(uiDemo, "submitButton", submitButton);
        AssignSerializedField(uiDemo, "randomScoreButton", randomButton);
        AssignSerializedField(uiDemo, "refreshTopButton", refreshButton);
        AssignSerializedField(uiDemo, "aroundMeButton", aroundButton);
        AssignSerializedField(uiDemo, "resultsText", resultsArea);
        AssignSerializedField(uiDemo, "steamUnavailablePanel", unavailablePanel);
        AssignSerializedField(uiDemo, "steamUnavailableText", unavailableText);

        uiRoot.SetActive(false);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Steam leaderboard UI setup complete.");
    }

    private static GameObject EnsureGameObject(string name, System.Func<GameObject> factory)
    {
        var existing = GameObject.Find(name);
        if (existing == null)
        {
            existing = factory();
            existing.name = name;
            Undo.RegisterCreatedObjectUndo(existing, "Create Steam leaderboard object");
        }

        return existing;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        Undo.RegisterCompleteObjectUndo(go, "Configure Steam leaderboard component");
        var comp = go.GetComponent<T>();
        if (comp == null)
        {
            comp = go.AddComponent<T>();
        }

        return comp;
    }

    private static TMP_InputField CreateInputField(Transform parent, ref float currentY, TMP_FontAsset font)
    {
        var fieldGO = new GameObject("ScoreInputField", typeof(RectTransform));
        fieldGO.transform.SetParent(parent, false);

        var rect = fieldGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(320, 44);
        rect.anchoredPosition = new Vector2(0, currentY);
        currentY -= 70;

        var image = fieldGO.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.95f);
        var input = fieldGO.AddComponent<TMP_InputField>();
        input.textComponent = CreateTmpText("Text", fieldGO.transform, font, string.Empty, TextAlignmentOptions.Left);
        input.placeholder = CreateTmpText("Placeholder", fieldGO.transform, font, "Enter score...", TextAlignmentOptions.Left);
        input.targetGraphic = image;
        input.textComponent.fontSize = 18;
        //input.placeholder.fontSize = 18;
        return input;
    }

    private static GameObject CreateButtonRow(Transform parent, ref float currentY)
    {
        var rowGO = new GameObject("ButtonRow", typeof(RectTransform));
        rowGO.transform.SetParent(parent, false);

        var rect = rowGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(660, 60);
        rect.anchoredPosition = new Vector2(0, currentY);
        currentY -= 80;

        var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;

        return rowGO;
    }

    private static Button CreateButton(Transform parent, string label, TMP_FontAsset font)
    {
        var buttonGO = new GameObject(label.Replace(" ", "") + "Button", typeof(RectTransform));
        buttonGO.transform.SetParent(parent, false);

        var image = buttonGO.AddComponent<Image>();
        image.color = new Color(0.15f, 0.16f, 0.2f, 0.95f);
        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = image;

        var rect = buttonGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(150, 40);

        var labelText = CreateTmpText("Label", buttonGO.transform, font, label, TextAlignmentOptions.Center);
        labelText.fontSize = 20;
        labelText.color = Color.white;

        return button;
    }

    private static TextMeshProUGUI CreateResultsArea(Transform parent, ref float currentY, TMP_FontAsset font)
    {
        var resultGO = new GameObject("LeaderboardResults", typeof(RectTransform));
        resultGO.transform.SetParent(parent, false);

        var rect = resultGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(640, 300);
        rect.anchoredPosition = new Vector2(0, currentY);
        currentY -= 340;

        var background = resultGO.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);

        var text = CreateTmpText("ResultsText", resultGO.transform, font, "Scores will appear here.", TextAlignmentOptions.TopLeft);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontSize = 18;
        text.margin = new Vector4(10, 10, 10, 10);

        return text;
    }

    private static GameObject CreateUnavailablePanel(Transform parent, TMP_FontAsset font)
    {
        var panelGO = new GameObject("SteamUnavailablePanel", typeof(RectTransform));
        panelGO.transform.SetParent(parent, false);

        var rect = panelGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(640, 42);
        rect.anchoredPosition = new Vector2(0, 30);

        var image = panelGO.AddComponent<Image>();
        image.color = new Color(0.18f, 0.05f, 0.06f, 0.85f);

        var text = CreateTmpText("SteamUnavailableText", panelGO.transform, font, "Steam not available. Start Steam or use steam_appid.txt (4459470) for editor testing.", TextAlignmentOptions.Center);
        text.fontSize = 16;
        text.color = Color.white;

        return panelGO;
    }

    private static TextMeshProUGUI CreateTmpText(string name, Transform parent, TMP_FontAsset font, string content, TextAlignmentOptions alignment)
    {
        var textGO = new GameObject(name, typeof(RectTransform));
        textGO.transform.SetParent(parent, false);

        var rect = textGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8, 8);
        rect.offsetMax = new Vector2(-8, -8);

        var text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.alignment = alignment;
        text.font = font;
        if (font == null)
            text.enableAutoSizing = true;
        return text;
    }

    private static TMP_FontAsset FindTemporaryFont()
    {
        var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null)
                return font;
        }

        Debug.LogWarning("No TMP_FontAsset found; TextMeshPro elements may not display correctly.");
        return null;
    }

    private static void HookButton(Button button, UnityAction action)
    {
        if (button == null || action == null)
            return;

        Undo.RecordObject(button, "Hook leaderboard button");
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        EditorUtility.SetDirty(button);
    }

    private static void AssignSerializedField(Object target, string fieldName, Object value)
    {
        if (target == null || string.IsNullOrEmpty(fieldName))
            return;

        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }
    }
}
#endif
