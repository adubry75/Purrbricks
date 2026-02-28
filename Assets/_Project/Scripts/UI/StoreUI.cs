using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen powerup store overlay.
/// Opened from the HUD balance button or the Pause menu.
/// </summary>
public class StoreUI : MonoBehaviour
{
    private Canvas _canvas;
    private Text   _balanceText;

    // Row buy-button refs so we can update "grayed" state on balance change
    private readonly List<(Button btn, Text btnLabel, int price)> _buyButtons
        = new List<(Button, Text, int)>();

    // Own quantity labels: type → text element
    private readonly Dictionary<PowerupType, Text> _qtyLabels
        = new Dictionary<PowerupType, Text>();

    // ── Store catalog ─────────────────────────────────────────────────────────

    private static readonly (PowerupType type, string name, int price, Color color)[] GoodItems =
    {
        (PowerupType.ExtraLife,          "+ LIFE",        PurrBucksConfig.PRICE_EXTRA_LIFE,       new Color(0.10f, 1.00f, 0.30f)),
        (PowerupType.Fireball,           "FIREBALL",      PurrBucksConfig.PRICE_FIREBALL,         new Color(1.00f, 0.45f, 0.00f)),
        (PowerupType.Laser,              "LASER",         PurrBucksConfig.PRICE_LASER,            new Color(1.00f, 0.10f, 0.30f)),
        (PowerupType.ShieldWall,         "SHIELD WALL",   PurrBucksConfig.PRICE_SHIELD_WALL,      new Color(0.00f, 0.90f, 1.00f)),
        (PowerupType.WidePaddle,         "WIDE PADDLE",   PurrBucksConfig.PRICE_WIDE_PADDLE,      new Color(0.30f, 0.60f, 1.00f)),
        (PowerupType.MultiBall,          "MULTI-BALL",    PurrBucksConfig.PRICE_MULTI_BALL,       new Color(1.00f, 0.40f, 0.00f)),
        (PowerupType.StickyBall,         "STICKY BALL",   PurrBucksConfig.PRICE_STICKY_BALL,      new Color(0.60f, 0.00f, 1.00f)),
        (PowerupType.SpeedBall,          "SPEED BALL",    PurrBucksConfig.PRICE_SPEED_BALL,       new Color(1.00f, 0.85f, 0.00f)),
        (PowerupType.BombBrick,          "BOMB BRICK",    PurrBucksConfig.PRICE_BOMB_BRICK,       new Color(0.90f, 0.20f, 0.90f)),
        (PowerupType.BigBall,            "BIG BALL",      PurrBucksConfig.PRICE_BIG_BALL,         new Color(0.60f, 0.85f, 1.00f)),
        (PowerupType.ScoreFrenzy,        "SCORE FRENZY",  PurrBucksConfig.PRICE_SCORE_FRENZY,     new Color(1.00f, 0.85f, 0.00f)),
        (PowerupType.PermanentStickyBall,"STICKY ∞",      PurrBucksConfig.PRICE_PERMANENT_STICKY, new Color(0.60f, 0.00f, 1.00f)),
    };

    private static readonly (PowerupType type, string name, Color color)[] CursedItems =
    {
        (PowerupType.ShrinkPaddle,  "SHRINK PAD",    new Color(0.90f, 0.20f, 0.20f)),
        (PowerupType.ZipBall,       "ZIP BALL",      new Color(0.40f, 0.90f, 0.10f)),
        (PowerupType.FlipControls,  "FLIP CTRL",     new Color(0.65f, 0.10f, 0.80f)),
        (PowerupType.CursedBall,    "CURSED BALL",   new Color(0.20f, 0.75f, 0.35f)),
        (PowerupType.TinyBall,      "TINY BALL",     new Color(1.00f, 0.25f, 0.50f)),
        (PowerupType.InvisiBall,    "INVISIBALL",    new Color(0.55f, 0.55f, 0.60f)),
        (PowerupType.DrunkenPaddle, "DRUNK PADDLE",  new Color(1.00f, 0.55f, 0.00f)),
        (PowerupType.DrunkVision,   "DRUNK VISION",  new Color(0.20f, 0.25f, 0.65f)),
        (PowerupType.GremlinBounces,"GREMLIN",       new Color(0.12f, 0.60f, 0.65f)),
    };

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    private void Start()
    {
        if (PurrBucksManager.Instance != null)
        {
            PurrBucksManager.Instance.OnBalanceChanged   += RefreshBuyButtons;
            PurrBucksManager.Instance.OnInventoryChanged += RefreshQtyLabels;
        }
    }

    private void OnDestroy()
    {
        if (PurrBucksManager.Instance != null)
        {
            PurrBucksManager.Instance.OnBalanceChanged   -= RefreshBuyButtons;
            PurrBucksManager.Instance.OnInventoryChanged -= RefreshQtyLabels;
        }
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 175;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // ── Full-screen dark overlay ──────────────────────────────────────────
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(transform, false);
        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.85f);
        var overlayRt = overlayImg.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = overlayRt.anchoredPosition = Vector2.zero;

        // ── Card ─────────────────────────────────────────────────────────────
        var card = new GameObject("Card");
        card.transform.SetParent(transform, false);
        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.04f, 0.06f, 0.13f, 0.97f);
        var cardOl = card.AddComponent<Outline>();
        cardOl.effectColor    = new Color(0.25f, 0.50f, 1f, 0.35f);
        cardOl.effectDistance = new Vector2(2f, -2f);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin        = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax        = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta        = new Vector2(860f, 900f);
        cardRt.anchoredPosition = Vector2.zero;

        // ── Card header ───────────────────────────────────────────────────────
        MakeLabel(card.transform, "🐾 PURR BUCKS STORE", new Vector2(0f, 422f),
            new Vector2(700f, 50f), 36, FontStyle.Bold, UIStyle.AccentGold);

        // Live balance display (top right of card)
        var balGO = new GameObject("Balance");
        balGO.transform.SetParent(card.transform, false);
        _balanceText = balGO.AddComponent<Text>();
        _balanceText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _balanceText.fontSize  = 28;
        _balanceText.fontStyle = FontStyle.Bold;
        _balanceText.alignment = TextAnchor.MiddleRight;
        _balanceText.color     = UIStyle.AccentGold;
        _balanceText.raycastTarget = false;
        var balRt = _balanceText.GetComponent<RectTransform>();
        balRt.anchorMin        = new Vector2(0.5f, 0.5f);
        balRt.anchorMax        = new Vector2(0.5f, 0.5f);
        balRt.sizeDelta        = new Vector2(260f, 40f);
        balRt.anchoredPosition = new Vector2(295f, 422f);
        RefreshBalanceDisplay();

        // Close button
        UIStyle.CreateButton(card.transform, "✕ Close",
            new Vector2(0f, -415f), new Vector2(200f, 54f),
            () => GameManager.Instance?.HideStore(), UIStyle.AccentRed);

        // ── Scrollable content area ───────────────────────────────────────────
        var scroll = BuildScrollRect(card.transform, new Vector2(0f, -5f), new Vector2(820f, 780f));
        var content = scroll.content;

        float rowY = -10f;
        const float ROW_H   = 48f;
        const float SECTION_H = 34f;
        const float GAP     = 6f;

        // Good powerups section
        rowY = AddSectionHeader(content, "GOOD POWERUPS", rowY, SECTION_H,
            new Color(0.15f, 0.60f, 0.25f, 0.85f));
        rowY -= GAP;

        foreach (var item in GoodItems)
        {
            AddRow(content, item.type, item.name, item.price, item.color, false, ref rowY, ROW_H, GAP);
        }

        rowY -= 8f;
        AddDivider(content, rowY, 2f, new Color(0.5f, 0.1f, 0.1f, 0.5f));
        rowY -= 10f;

        // Cursed powerups section
        rowY = AddSectionHeader(content, "⚠ CURSED POWERUPS", rowY, SECTION_H,
            new Color(0.40f, 0.05f, 0.05f, 0.90f));
        rowY -= GAP;

        foreach (var item in CursedItems)
        {
            AddRow(content, item.type, "☠ " + item.name, PurrBucksConfig.PRICE_CURSED,
                item.color, true, ref rowY, ROW_H, GAP);
        }

        // Set content height so scroll works.
        // Keep sizeDelta.x = 0 so the content stretches to match the scroll viewport width
        // (anchorMin.x=0, anchorMax.x=1 already handle width; setting it to a fixed value
        //  would add to the parent width and make everything 1640px wide).
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.sizeDelta = new Vector2(0f, Mathf.Abs(rowY) + 20f);
    }

    private float AddSectionHeader(Transform parent, string title, float startY,
        float height, Color bgColor)
    {
        var go = new GameObject("SectionHeader_" + title);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var rt = img.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.sizeDelta        = new Vector2(0f, height);
        rt.anchoredPosition = new Vector2(0f, startY);

        var lbl = new GameObject("Label");
        lbl.transform.SetParent(go.transform, false);
        var txt = lbl.AddComponent<Text>();
        txt.text          = title;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = 18;
        txt.fontStyle     = FontStyle.Bold;
        txt.alignment     = TextAnchor.MiddleLeft;
        txt.color         = Color.white;
        txt.raycastTarget = false;
        var lblRt = txt.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = new Vector2(12f, 0f);
        lblRt.offsetMax = Vector2.zero;

        return startY - height;
    }

    private void AddDivider(Transform parent, float y, float height, Color color)
    {
        var go = new GameObject("Divider");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt = img.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.sizeDelta        = new Vector2(-20f, height);
        rt.anchoredPosition = new Vector2(0f, y);
    }

    private void AddRow(Transform parent, PowerupType type, string name, int price,
        Color accentColor, bool cursed, ref float rowY, float rowH, float gap)
    {
        var row = new GameObject($"Row_{type}");
        row.transform.SetParent(parent, false);

        var rowBg = row.AddComponent<Image>();
        rowBg.color = cursed
            ? new Color(0.18f, 0.03f, 0.03f, 0.75f)
            : new Color(0.06f, 0.08f, 0.14f, 0.70f);

        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin        = new Vector2(0f, 1f);
        rowRt.anchorMax        = new Vector2(1f, 1f);
        rowRt.pivot            = new Vector2(0.5f, 1f);
        rowRt.sizeDelta        = new Vector2(0f, rowH);
        rowRt.anchoredPosition = new Vector2(0f, rowY);

        // Left color accent strip (6px, full height)
        var strip = new GameObject("Strip");
        strip.transform.SetParent(row.transform, false);
        var stripImg = strip.AddComponent<Image>();
        stripImg.color         = accentColor;
        stripImg.raycastTarget = false;
        var stripRt = strip.GetComponent<RectTransform>();
        stripRt.anchorMin = new Vector2(0f, 0f);
        stripRt.anchorMax = new Vector2(0f, 1f);
        // offsetMin/offsetMax with same-X anchors: left=offsetMin.x, right=offsetMax.x
        stripRt.offsetMin = new Vector2(0f, 0f);
        stripRt.offsetMax = new Vector2(6f, 0f);

        // ── Row columns (right-edge anchored so they're independent of content width) ──
        // Layout (from left):  [6px strip] [Name: 10→-440 from right] [Price: -430→-290] [Own: -280→-140] [BUY: -130→-10]

        // Name: stretch from left (10px indent) to 440px from right
        MakeRowText(row.transform, name,
            new Vector2(0f, 0f), new Vector2(1f, 1f),   // full stretch anchors
            new Vector2(10f, 2f), new Vector2(-440f, -2f),
            15, accentColor, TextAnchor.MiddleLeft);

        // Price "🐾 XX PB": 140px wide, anchored to right edge, 290→430px from right
        MakeRowText(row.transform, $"🐾 {price} PB",
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-430f, 2f), new Vector2(-290f, -2f),
            14, UIStyle.AccentGold, TextAnchor.MiddleCenter);

        // Own count "Own: N": 140px wide, anchored to right edge, 140→280px from right
        var ownLabel = MakeRowText(row.transform,
            $"Own: {PurrBucksManager.Instance?.GetInventoryCount(type) ?? 0}",
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-280f, 2f), new Vector2(-140f, -2f),
            14, new Color(0.75f, 0.75f, 0.75f), TextAnchor.MiddleCenter);
        _qtyLabels[type] = ownLabel;

        // BUY button: 120px wide, right-anchored, 10px from right edge
        int capturedPrice = price;
        PowerupType capturedType = type;

        var btnGO = new GameObject("BuyBtn");
        btnGO.transform.SetParent(row.transform, false);
        var btnBg = btnGO.AddComponent<Image>();
        btnBg.color = UIStyle.AccentGreen;
        var btnRt = btnGO.GetComponent<RectTransform>();
        btnRt.anchorMin        = new Vector2(1f, 0.5f);
        btnRt.anchorMax        = new Vector2(1f, 0.5f);
        btnRt.pivot            = new Vector2(1f, 0.5f);
        btnRt.sizeDelta        = new Vector2(120f, 38f);
        btnRt.anchoredPosition = new Vector2(-10f, 0f);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        btn.onClick.AddListener(() => OnBuyClicked(capturedType, capturedPrice));

        var btnLabelGO = new GameObject("BtnLabel");
        btnLabelGO.transform.SetParent(btnGO.transform, false);
        var btnLabel = btnLabelGO.AddComponent<Text>();
        btnLabel.text          = "BUY";
        btnLabel.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnLabel.fontSize      = 15;
        btnLabel.fontStyle     = FontStyle.Bold;
        btnLabel.alignment     = TextAnchor.MiddleCenter;
        btnLabel.color         = Color.white;
        btnLabel.raycastTarget = false;
        var btnLabelRt = btnLabel.GetComponent<RectTransform>();
        btnLabelRt.anchorMin = Vector2.zero;
        btnLabelRt.anchorMax = Vector2.one;
        btnLabelRt.sizeDelta = Vector2.zero;

        _buyButtons.Add((btn, btnLabel, capturedPrice));

        rowY -= (rowH + gap);
    }

    private void OnBuyClicked(PowerupType type, int price)
    {
        if (PurrBucksManager.Instance == null) return;
        if (!PurrBucksManager.Instance.TrySpend(price)) return;
        PurrBucksManager.Instance.AddToInventory(type, 1);
    }

    // ── Live refresh ──────────────────────────────────────────────────────────

    private void RefreshBalanceDisplay()
    {
        if (_balanceText != null && PurrBucksManager.Instance != null)
            _balanceText.text = $"🐾 {PurrBucksManager.Instance.Balance} PB";
    }

    private void RefreshBuyButtons()
    {
        RefreshBalanceDisplay();
        if (PurrBucksManager.Instance == null) return;
        int balance = PurrBucksManager.Instance.Balance;
        foreach (var (btn, lbl, price) in _buyButtons)
        {
            bool canAfford = balance >= price;
            btn.interactable = canAfford;
            lbl.text = canAfford ? "BUY" : "Need PB";
        }
    }

    private void RefreshQtyLabels()
    {
        if (PurrBucksManager.Instance == null) return;
        foreach (var kvp in _qtyLabels)
        {
            int qty = PurrBucksManager.Instance.GetInventoryCount(kvp.Key);
            if (kvp.Value != null) kvp.Value.text = $"Own: {qty}";
        }
    }

    // ── Show / Hide ───────────────────────────────────────────────────────────

    public void Show()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f;
        RefreshBuyButtons();
        RefreshQtyLabels();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // ── Scroll rect builder ───────────────────────────────────────────────────

    private ScrollRect BuildScrollRect(Transform parent, Vector2 anchoredPos, Vector2 size)
    {
        var scrollGO = new GameObject("Scroll");
        scrollGO.transform.SetParent(parent, false);
        var scrollRt = scrollGO.AddComponent<RectTransform>();
        scrollRt.anchorMin        = new Vector2(0.5f, 0.5f);
        scrollRt.anchorMax        = new Vector2(0.5f, 0.5f);
        scrollRt.sizeDelta        = size;
        scrollRt.anchoredPosition = anchoredPos;

        var scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0f, 0f, 0f, 0.01f); // nearly transparent mask base

        var mask = scrollGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical   = true;
        scroll.scrollSensitivity = 30f;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRt = contentGO.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot     = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 1200f); // will be resized after rows added
        contentRt.anchoredPosition = Vector2.zero;

        scroll.content = contentRt;

        return scroll;
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void MakeLabel(Transform parent, string text, Vector2 pos, Vector2 size,
        int fontSize, FontStyle style, Color color)
    {
        var go = new GameObject("Label_" + text.Substring(0, Mathf.Min(8, text.Length)));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text          = text;
        t.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize      = fontSize;
        t.fontStyle     = style;
        t.alignment     = TextAnchor.MiddleCenter;
        t.color         = color;
        t.raycastTarget = false;
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;
    }

    private Text MakeRowText(Transform parent, string text,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        int fontSize, Color color, TextAnchor align)
    {
        var go = new GameObject("RowTxt");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text          = text;
        t.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize      = fontSize;
        t.alignment     = align;
        t.color         = color;
        t.raycastTarget = false;
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return t;
    }
}
