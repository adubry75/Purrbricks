using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton for full-screen flash and vignette effects.
///  - FlashWhite()  — brief white flash (bomb, Fury Strike)
///  - FlashRed()    — red flash (life lost)
///  - SetBadVignette(bool) — pulsing red edge while bad powerups active
/// </summary>
public class ScreenEffects : MonoBehaviour
{
    public static ScreenEffects Instance { get; private set; }

    private Image _flashImg;
    private Image _vignetteImg;
    private Coroutine _flashRoutine;
    private bool _isBadActive;

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
        canvas.sortingOrder = 300;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // ── Vignette overlay (bad powerup pulse) ──────────────────────────────
        var vigGO = new GameObject("Vignette");
        vigGO.transform.SetParent(transform, false);
        _vignetteImg = vigGO.AddComponent<Image>();
        _vignetteImg.color = new Color(0.75f, 0f, 0f, 0f);
        _vignetteImg.raycastTarget = false;
        var vigRt = _vignetteImg.GetComponent<RectTransform>();
        vigRt.anchorMin  = Vector2.zero;
        vigRt.anchorMax  = Vector2.one;
        vigRt.sizeDelta  = Vector2.zero;

        // ── Flash overlay ─────────────────────────────────────────────────────
        var flashGO = new GameObject("Flash");
        flashGO.transform.SetParent(transform, false);
        _flashImg = flashGO.AddComponent<Image>();
        _flashImg.color = new Color(1f, 1f, 1f, 0f);
        _flashImg.raycastTarget = false;
        var flashRt = _flashImg.GetComponent<RectTransform>();
        flashRt.anchorMin = Vector2.zero;
        flashRt.anchorMax = Vector2.one;
        flashRt.sizeDelta = Vector2.zero;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void FlashWhite(float peakAlpha = 0.55f, float duration = 0.30f)
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine(Color.white, peakAlpha, duration));
    }

    public void FlashRed(float peakAlpha = 0.45f, float duration = 0.65f)
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine(new Color(0.9f, 0.04f, 0.04f), peakAlpha, duration));
    }

    public void SetBadVignette(bool active)
    {
        _isBadActive = active;
        if (!active && _vignetteImg != null)
            _vignetteImg.color = new Color(0.75f, 0f, 0f, 0f);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isBadActive || _vignetteImg == null) return;

        // Pulse red edge while bad powerup is active
        float alpha = 0.13f + 0.07f * Mathf.Sin(Time.unscaledTime * 2.8f);
        _vignetteImg.color = new Color(0.75f, 0f, 0f, alpha);
    }

    private IEnumerator FlashRoutine(Color color, float peakAlpha, float duration)
    {
        _flashImg.color = new Color(color.r, color.g, color.b, peakAlpha);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(peakAlpha, 0f, elapsed / duration);
            _flashImg.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        _flashImg.color = new Color(color.r, color.g, color.b, 0f);
    }
}
