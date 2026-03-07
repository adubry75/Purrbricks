using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal overlay for publishing or updating a custom level on the community server.
/// Identity is the level's stable GUID — title changes do NOT create duplicate rows.
/// sortingOrder=700 — sits above everything.
/// </summary>
public class CommunityPublishUI : MonoBehaviour
{
    public static CommunityPublishUI Instance { get; private set; }

    private Canvas     _canvas;
    private GameObject _panel;
    private Text       _cardTitle;
    private Text       _publishStatus;
    private InputField _titleField;
    private InputField _descField;
    private Text       _statusText;
    private Button     _submitBtn;
    private Text       _submitBtnLabel;

    private LevelData  _pendingData;

    private static readonly Color ColorGold  = new Color(1.00f, 0.84f, 0.10f);
    private static readonly Color ColorGreen = new Color(0.20f, 1f, 0.45f);
    private static readonly Color ColorRed   = new Color(1f, 0.30f, 0.30f);
    private static readonly Color ColorGray  = new Color(0.60f, 0.60f, 0.70f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        Hide();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(LevelData data, string _unused = null)
    {
        _pendingData = data;

        bool alreadyPublished = CommunityLevelService.Instance != null
                                && CommunityLevelService.Instance.IsPublished(data?.levelGuid ?? "");

        // Card title and submit button label reflect the operation
        if (_cardTitle      != null)
            _cardTitle.text = alreadyPublished ? "UPDATE PUBLISHED LEVEL" : "PUBLISH LEVEL";
        if (_submitBtnLabel != null)
            _submitBtnLabel.text = alreadyPublished ? "UPDATE" : "SUBMIT TO COMMUNITY";

        // Publish status line
        if (_publishStatus != null)
        {
            _publishStatus.text  = alreadyPublished ? "✓  Published" : "Not yet published";
            _publishStatus.color = alreadyPublished ? ColorGreen : ColorGray;
        }

        // Pre-fill title
        if (_titleField != null)
            _titleField.text = data?.displayName ?? "";
        if (_descField  != null)
            _descField.text = "";
        if (_statusText != null) { _statusText.text = ""; _statusText.gameObject.SetActive(false); }
        if (_submitBtn  != null) _submitBtn.interactable = true;

        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 700;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Dark overlay
        _panel = new GameObject("Backdrop");
        _panel.transform.SetParent(transform, false);
        var overlay = _panel.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.80f);
        var overlayRt = _panel.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero; overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;

        // Card
        var card = new GameObject("Card");
        card.transform.SetParent(_panel.transform, false);
        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.08f, 0.10f, 0.20f, 1f);
        card.AddComponent<Outline>().effectColor = ColorGold;
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot     = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(560f, 440f);
        cardRt.anchoredPosition = Vector2.zero;

        // Card title (dynamic: "PUBLISH LEVEL" or "UPDATE PUBLISHED LEVEL")
        _cardTitle = CreateTextGO(card.transform, "PUBLISH LEVEL", new Vector2(0f, 175f), 36, ColorGold).GetComponent<Text>();

        // Publish status ("Not yet published" / "✓ Published")
        _publishStatus = CreateTextGO(card.transform, "Not yet published", new Vector2(0f, 135f), 14, ColorGray).GetComponent<Text>();

        // Title field label + input
        CreateText(card.transform, "Level Title (required, max 64 chars)", new Vector2(0f, 100f), 14, new Color(0.65f, 0.65f, 0.80f));
        _titleField = CreateInputField(card.transform, "Enter a catchy title...", new Vector2(0f, 68f), new Vector2(490f, 38f));
        _titleField.characterLimit = 64;

        // Description field label + input
        CreateText(card.transform, "Description (optional, max 256 chars)", new Vector2(0f, 28f), 14, new Color(0.65f, 0.65f, 0.80f));
        _descField = CreateInputField(card.transform, "Describe your level...", new Vector2(0f, -28f), new Vector2(490f, 70f));
        _descField.lineType = InputField.LineType.MultiLineSubmit;
        _descField.characterLimit = 256;

        // Operation status text (shown during/after submit)
        var statusGO = CreateTextGO(card.transform, "", new Vector2(0f, -110f), 17, Color.white, "Status");
        _statusText = statusGO.GetComponent<Text>();
        _statusText.gameObject.SetActive(false);

        // Submit button
        _submitBtn = UIStyle.CreateButton(card.transform, "Submit to Community",
            new Vector2(-75f, -175f), new Vector2(260f, 50f),
            OnSubmit, UIStyle.AccentGreen);
        _submitBtnLabel = _submitBtn.GetComponentInChildren<Text>();

        // Cancel button
        UIStyle.CreateButton(card.transform, "Cancel",
            new Vector2(160f, -175f), new Vector2(140f, 50f),
            Hide, UIStyle.AccentRed);
    }

    private void OnSubmit()
    {
        if (CommunityLevelService.Instance == null)
        {
            ShowStatus("Community service not available.", ColorRed);
            return;
        }

        string title = (_titleField?.text ?? "").Trim();
        if (string.IsNullOrEmpty(title))
        {
            ShowStatus("Please enter a title.", ColorRed);
            return;
        }

        string desc = (_descField?.text ?? "").Trim();

        if (_submitBtn != null) _submitBtn.interactable = false;
        bool isUpdate = CommunityLevelService.Instance.IsPublished(_pendingData?.levelGuid ?? "");
        ShowStatus(isUpdate ? "Updating..." : "Publishing...", Color.white);

        CommunityLevelService.Instance.PublishLevel(_pendingData, title, desc, result =>
        {
            if (!result.Success)
            {
                ShowStatus($"Error: {result.error}", ColorRed);
                if (_submitBtn != null) _submitBtn.interactable = true;
            }
            else
            {
                string verb = result.WasUpdated ? "Updated" : "Published";
                ShowStatus($"{verb}!  Server ID: #{result.serverId}", ColorGreen);
                // Refresh the status label for future opens
                if (_publishStatus != null)
                {
                    _publishStatus.text  = "✓  Published";
                    _publishStatus.color = ColorGreen;
                }
                StartCoroutine(AutoClose(2.5f));
            }
        });
    }

    private IEnumerator AutoClose(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Hide();
    }

    private void ShowStatus(string msg, Color color)
    {
        if (_statusText == null) return;
        _statusText.text  = msg;
        _statusText.color = color;
        _statusText.gameObject.SetActive(true);
    }

    // ── UGUI helpers ──────────────────────────────────────────────────────────

    private void CreateText(Transform parent, string text, Vector2 pos, int fontSize, Color color, string name = null)
        => CreateTextGO(parent, text, pos, fontSize, color, name);

    private GameObject CreateTextGO(Transform parent, string text, Vector2 pos, int fontSize, Color color, string name = null)
    {
        var go = new GameObject(name ?? text);
        go.transform.SetParent(parent, false);
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
        rt.sizeDelta        = new Vector2(510f, fontSize + 20f);
        rt.anchoredPosition = pos;
        return go;
    }

    private InputField CreateInputField(Transform parent, string placeholder, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Field");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.04f, 0.06f, 0.14f);
        go.AddComponent<Outline>().effectColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(go.transform, false);
        var phTxt = phGO.AddComponent<Text>();
        phTxt.text      = placeholder;
        phTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        phTxt.fontSize  = 15;
        phTxt.color     = new Color(0.45f, 0.45f, 0.55f);
        phTxt.fontStyle = FontStyle.Italic;
        phTxt.alignment = TextAnchor.UpperLeft;
        SetStretch(phGO, 6f, 4f, -6f, -4f);

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 15;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.UpperLeft;
        SetStretch(txtGO, 6f, 4f, -6f, -4f);

        var field = go.AddComponent<InputField>();
        field.textComponent = txt;
        field.placeholder   = phTxt;
        field.targetGraphic = img;
        return field;
    }

    private static void SetStretch(GameObject go, float l, float b, float r, float t)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(r, t);
    }
}
