using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Static factory for AAA-quality UI buttons — dark cyberpunk aesthetic.
/// All buttons: dark navy background, left accent strip, uppercase bold text.
/// </summary>
public static class UIStyle
{
    // ── Palette ───────────────────────────────────────────────────────────────
    public static readonly Color AccentBlue    = new Color(0.35f, 0.70f, 1.00f);
    public static readonly Color AccentGreen   = new Color(0.20f, 0.92f, 0.45f);
    public static readonly Color AccentRed     = new Color(1.00f, 0.25f, 0.25f);
    public static readonly Color AccentGold    = new Color(1.00f, 0.78f, 0.10f);
    public static readonly Color AccentMagenta = new Color(1.00f, 0.30f, 0.80f);

    private static readonly Color BgNormal     = new Color(0.04f, 0.08f, 0.16f, 0.95f);
    private static readonly Color BgHighlight  = new Color(0.10f, 0.20f, 0.36f, 0.98f);
    private static readonly Color BgPressed    = new Color(0.02f, 0.04f, 0.09f, 1.00f);

    private static Sprite s_cachedTemplate;
    private static bool s_triedTemplate;

    private static Sprite GetButtonTemplate()
    {
        if (s_triedTemplate) return s_cachedTemplate;
        s_triedTemplate = true;

        if (UITheme.Instance != null && UITheme.Instance.ButtonTemplate != null)
        {
            s_cachedTemplate = UITheme.Instance.ButtonTemplate;
            return s_cachedTemplate;
        }

        // Fallback: execution order should make Instance available, but in case UITheme
        // is created dynamically we try to find it once.
        var theme = Object.FindFirstObjectByType<UITheme>(FindObjectsInactive.Include);
        if (theme != null && theme.ButtonTemplate != null)
            s_cachedTemplate = theme.ButtonTemplate;

        return s_cachedTemplate;
    }

    // ── Global Variables TODO MOVE SOMEONE ELSE THIS DOESNT BELONG IN UI ───────────────────────────────────────────────────────────────
    public static int TotalLevels => GameManager.Instance?.LevelCount ?? 0;

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a styled button parented to <paramref name="parent"/>.
    /// </summary>
    /// <param name="parent">Parent transform.</param>
    /// <param name="label">Display text (auto-uppercased).</param>
    /// <param name="position">anchoredPosition relative to parent anchor.</param>
    /// <param name="size">Width × Height in reference pixels.</param>
    /// <param name="onClick">Click callback.</param>
    /// <param name="accent">Left strip + text glow color. Default = AccentBlue.</param>
    public static Button CreateButton(
        Transform parent,
        string label,
        Vector2 position,
        Vector2 size,
        System.Action onClick,
        Color? accent = null)
    {
        Color ac = accent ?? AccentBlue;
        int textSize = Mathf.RoundToInt(size.y * 0.38f);

        // ── Root ──────────────────────────────────────────────────────────────
        var btnGO = new GameObject(label + "Button");
        btnGO.transform.SetParent(parent, false);

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = Color.white; // ColorBlock multiplies against this

        // If a global template sprite exists, use it for all buttons.
        var template = GetButtonTemplate();
        bool usesTemplate = template != null;
        if (usesTemplate)
        {
            btnImg.sprite = template;
            btnImg.type   = Image.Type.Sliced;
        }

        var button = btnGO.AddComponent<Button>();
        button.targetGraphic = btnImg;

        var colors = button.colors;
        if (usesTemplate)
        {
            // Template art provides the look; we just do subtle brighten/darken.
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor     = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor    = new Color(1.08f, 1.08f, 1.08f, 1f);
        }
        else
        {
            colors.normalColor      = BgNormal;
            colors.highlightedColor = BgHighlight;
            colors.pressedColor     = BgPressed;
            colors.selectedColor    = BgHighlight;
        }
        colors.colorMultiplier  = 1f;
        colors.fadeDuration     = 0.06f;
        button.colors = colors;

        button.onClick.AddListener(() => onClick?.Invoke());

        var btnRt = btnGO.GetComponent<RectTransform>();
        btnRt.anchorMin       = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax       = new Vector2(0.5f, 0.5f);
        btnRt.sizeDelta       = size;
        btnRt.anchoredPosition = position;

        if (btnGO.GetComponent<UIHoverFx>() == null)
            btnGO.AddComponent<UIHoverFx>();

        // Subtle border
        var border = btnGO.AddComponent<Outline>();
        border.effectColor    = new Color(ac.r, ac.g, ac.b, 0.55f);
        border.effectDistance = new Vector2(1f, -1f);

        // No accent strip — template art should carry the look.

        // ── Label ─────────────────────────────────────────────────────────────
        var textGO  = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        var txt = textGO.AddComponent<Text>();
        txt.text      = label.ToUpper();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = textSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = Color.white;

        var textRt     = txt.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(12f, 0f);
        textRt.offsetMax = new Vector2(-4f, 0f);

        var textOutline = textGO.AddComponent<Outline>();
        textOutline.effectColor    = new Color(ac.r, ac.g, ac.b, 0.85f);
        textOutline.effectDistance = new Vector2(1f, -1f);

        var shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 0.80f);
        shadow.effectDistance = new Vector2(2f, -2f);

        return button;
    }

    /// <summary>
    /// Creates a simple image-only button parented to <paramref name="parent"/>.
    /// </summary>
    public static Button CreateImageButton(
        Transform parent,
        Sprite sprite,
        Vector2 anchoredPos,
        System.Action onClick)
    {
        if (sprite == null) return null;

        var go = new GameObject("ImageButton");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.80f, 0.80f, 0.80f);
        btn.colors = colors;

        // Fixed height; width from sprite aspect ratio (use rect so atlased sprites behave correctly).
        float aspect = sprite.rect.height > 0f ? (sprite.rect.width / sprite.rect.height) : 1f;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(aspect * 70f, 70f);
        rt.anchoredPosition = anchoredPos;

        if (go.GetComponent<UIHoverFx>() == null)
            go.AddComponent<UIHoverFx>();

        return btn;
    }
}
