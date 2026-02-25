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

    // ── Global Variables TODO MOVE SOMEONE ELSE THIS DOESNT BELONG IN UI ───────────────────────────────────────────────────────────────
    public static readonly int TotalLevels = 84;

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

        var button = btnGO.AddComponent<Button>();
        button.targetGraphic = btnImg;

        var colors = button.colors;
        colors.normalColor      = BgNormal;
        colors.highlightedColor = BgHighlight;
        colors.pressedColor     = BgPressed;
        colors.selectedColor    = BgHighlight;
        colors.colorMultiplier  = 1f;
        colors.fadeDuration     = 0.06f;
        button.colors = colors;

        button.onClick.AddListener(() => onClick?.Invoke());

        var btnRt = btnGO.GetComponent<RectTransform>();
        btnRt.anchorMin       = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax       = new Vector2(0.5f, 0.5f);
        btnRt.sizeDelta       = size;
        btnRt.anchoredPosition = position;

        // Subtle border
        var border = btnGO.AddComponent<Outline>();
        border.effectColor    = new Color(ac.r, ac.g, ac.b, 0.55f);
        border.effectDistance = new Vector2(1f, -1f);

        // ── Left accent strip ─────────────────────────────────────────────────
        var strip    = new GameObject("Strip");
        strip.transform.SetParent(btnGO.transform, false);
        var stripImg = strip.AddComponent<Image>();
        stripImg.color = ac;
        var stripRt  = strip.GetComponent<RectTransform>();
        stripRt.anchorMin       = new Vector2(0f, 0f);
        stripRt.anchorMax       = new Vector2(0f, 1f);
        stripRt.pivot           = new Vector2(0f, 0.5f);
        stripRt.sizeDelta       = new Vector2(5f, -6f);
        stripRt.anchoredPosition = new Vector2(3f, 0f);

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

        return btn;
    }
}
