using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that displays animated fanfare text when a powerup is collected.
///  - Good powerups: pop-in scale, hold, fade up
///  - Bad powerups: red color, ⚠ prefix
///  - Fury Strike: gold, larger, longer hold
/// </summary>
public class PowerupNotification : MonoBehaviour
{
    public static PowerupNotification Instance { get; private set; }

    private Text _txt;
    private RectTransform _rt;
    private Coroutine _routine;

    // Colors matching PowerupPickup orbs (index = (int)PowerupType)
    private static readonly Color[] TypeColors = new Color[]
    {
        new Color(0.30f, 0.60f, 1.00f),   // WidePaddle   sky-blue
        new Color(1.00f, 0.40f, 0.00f),   // MultiBall    orange
        new Color(0.60f, 0.00f, 1.00f),   // StickyBall   purple
        new Color(1.00f, 0.85f, 0.00f),   // SpeedBall    gold
        new Color(0.10f, 1.00f, 0.30f),   // ExtraLife    green
        new Color(1.00f, 0.10f, 0.30f),   // Laser        crimson
        new Color(1.00f, 0.45f, 0.00f),   // Fireball     fire-orange
        new Color(0.90f, 0.20f, 0.90f),   // BombBrick    magenta
        new Color(0.90f, 0.20f, 0.20f),   // ShrinkPaddle red
        new Color(0.40f, 0.90f, 0.10f),   // ZipBall      sickly-green
        new Color(0.65f, 0.10f, 0.80f),   // FlipControls dark-purple
        new Color(0.20f, 0.75f, 0.35f),   // CursedBall   murky-green
    };

    private static readonly string[] TypeLabels = new string[]
    {
        "WIDE PADDLE",
        "MULTI-BALL!",
        "STICKY BALL",
        "SPEED BALL",
        "+ 1 LIFE!",
        "LASER!",
        "FIREBALL!",
        "BOMB BRICK!",
        "⚠ SHRINK",
        "⚠ ZIP BALL",
        "⚠ FLIP CTRL",
        "⚠ CURSED",
    };

    // Anchor position reset every coroutine run
    private Vector2 _basePos;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
    }

    private void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var go = new GameObject("NotifText");
        go.transform.SetParent(transform, false);

        _txt             = go.AddComponent<Text>();
        _txt.font        = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _txt.fontSize    = 72;
        _txt.fontStyle   = FontStyle.Bold;
        _txt.alignment   = TextAnchor.MiddleCenter;
        _txt.color       = new Color(1f, 1f, 1f, 0f);

        var ol = go.AddComponent<Outline>();
        ol.effectColor    = Color.black;
        ol.effectDistance = new Vector2(4f, -4f);

        var sh = go.AddComponent<Shadow>();
        sh.effectColor    = new Color(0f, 0f, 0f, 0.75f);
        sh.effectDistance = new Vector2(3f, -3f);

        _rt               = _txt.GetComponent<RectTransform>();
        _rt.anchorMin     = new Vector2(0.5f, 0.5f);
        _rt.anchorMax     = new Vector2(0.5f, 0.5f);
        _rt.sizeDelta     = new Vector2(1100f, 130f);
        // Centered on playfield — offset left to account for powerup HUD on right
        _basePos          = new Vector2(-160f, 160f);
        _rt.anchoredPosition = _basePos;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Show a powerup notification by enum type.</summary>
    public void ShowPowerup(PowerupType type)
    {
        int idx   = Mathf.Clamp((int)type, 0, TypeColors.Length - 1);
        Color col = TypeColors[idx];
        string lbl = idx < TypeLabels.Length ? TypeLabels[idx] : type.ToString().ToUpper();
        bool bad = idx >= 8;

        if (bad)
            col = new Color(1f, 0.3f, 0.3f);

        Show(lbl, col, isSpecial: false);
    }

    /// <summary>Show arbitrary text — used for Fury Strike, etc.</summary>
    public void Show(string text, Color color, bool isSpecial = false)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine(text, color, isSpecial));
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    private IEnumerator ShowRoutine(string text, Color color, bool isSpecial)
    {
        _txt.text     = text;
        _txt.fontSize = isSpecial ? 96 : 72;

        float overshoot  = isSpecial ? 1.55f : 1.20f;
        float settle     = isSpecial ? 1.30f : 1.00f;
        float holdTime   = isSpecial ? 1.60f : 0.75f;

        // Reset position
        _rt.anchoredPosition = _basePos;
        _rt.localScale       = Vector3.zero;

        // Pop-in
        float popDuration = 0.16f;
        float elapsed     = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = elapsed / popDuration;
            _rt.localScale = Vector3.one * Mathf.Lerp(0f, overshoot, t);
            _txt.color     = new Color(color.r, color.g, color.b, t);
            yield return null;
        }

        // Settle
        float settleDur = 0.09f;
        elapsed = 0f;
        while (elapsed < settleDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = elapsed / settleDur;
            _rt.localScale = Vector3.one * Mathf.Lerp(overshoot, settle, t);
            _txt.color     = new Color(color.r, color.g, color.b, 1f);
            yield return null;
        }

        _rt.localScale = Vector3.one * settle;

        // If special: gentle pulse while held
        float heldElapsed = 0f;
        while (heldElapsed < holdTime)
        {
            heldElapsed += Time.unscaledDeltaTime;
            if (isSpecial)
            {
                float pulse = settle * (0.95f + 0.05f * Mathf.Sin(heldElapsed * 10f));
                _rt.localScale = Vector3.one * pulse;
            }
            yield return null;
        }

        // Fade out + drift up
        float fadeDur    = 0.40f;
        Vector2 startPos = _rt.anchoredPosition;
        elapsed = 0f;
        while (elapsed < fadeDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = elapsed / fadeDur;
            _txt.color           = new Color(color.r, color.g, color.b, 1f - t);
            _rt.anchoredPosition = startPos + Vector2.up * (50f * t);
            yield return null;
        }

        // Clean up
        _txt.color           = new Color(color.r, color.g, color.b, 0f);
        _rt.anchoredPosition = _basePos;
        _rt.localScale       = Vector3.one;
    }
}
