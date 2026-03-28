using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Gamepad virtual cursor system.
///
/// When a gamepad is active AND the game is not in a play state (Ready/Playing),
/// an on-screen cursor appears. The left stick / d-pad moves it, and the A button
/// clicks whatever the cursor is hovering over.
///
/// This lets every existing menu work with the controller without any per-menu
/// changes — they all already work with the mouse.
///
/// During gameplay the cursor is hidden so it never conflicts with paddle/launch input.
/// </summary>
public class UINavController : MonoBehaviour
{
    public static UINavController Instance { get; private set; }

    private const float CURSOR_SPEED = 950f; // screen pixels per second

    private GameObject    _cursorGO;
    private RectTransform _cursorRt;
    private Vector2       _cursorPos;
    private GameObject    _lastHovered;

    private static readonly List<RaycastResult> _hits = new List<RaycastResult>();

    // ── Bootstrap ─────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("UINavController").AddComponent<UINavController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InputManager.OnSchemeChanged += OnSchemeChanged;

        BuildCursor();
        _cursorPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private void BuildCursor()
    {
        // Dedicated overlay canvas — above all game UI, no GraphicRaycaster so it
        // doesn't eat pointer events from the actual UI canvases below.
        var cvGO = new GameObject("GamepadCursorCanvas");
        cvGO.transform.SetParent(transform, false);
        var cv = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 1000;
        // No CanvasScaler: Constant Pixel Size (default) keeps cursor in screen-space pixels.
        // No GraphicRaycaster: cursor image must not block raycasts to the UI beneath it.

        _cursorGO = new GameObject("Cursor");
        _cursorGO.transform.SetParent(cvGO.transform, false);

        var img = _cursorGO.AddComponent<Image>();
        img.color         = Color.white;
        img.raycastTarget = false; // must not intercept clicks

        var ol = _cursorGO.AddComponent<Outline>();
        ol.effectColor    = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(2f, -2f);

        _cursorRt = _cursorGO.GetComponent<RectTransform>();
        _cursorRt.sizeDelta  = new Vector2(20f, 20f);
        _cursorRt.pivot      = new Vector2(0.5f, 0.5f);
        _cursorRt.anchorMin  = Vector2.zero; // anchor at canvas bottom-left
        _cursorRt.anchorMax  = Vector2.zero; // so anchoredPosition == screen pixels

        // Rotate 45° so the square becomes a diamond — more cursor-like
        _cursorGO.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        _cursorGO.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Move the virtual cursor to the given button when a menu opens.
    /// Call from every menu's Show() method — already wired up.
    /// </summary>
    public static void SetDefault(GameObject go)
    {
        if (go == null || Instance == null) return;
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        // WorldToScreenPoint(null, ...) works correctly for ScreenSpaceOverlay canvases.
        Instance._cursorPos = RectTransformUtility.WorldToScreenPoint(null, rt.position);
    }

    // Kept for API compatibility — no-op in cursor mode.
    public static void ClearSelection() { }

    /// <summary>Current cursor position in screen pixels.</summary>
    public static Vector2 CursorPosition => Instance != null ? Instance._cursorPos : Vector2.zero;

    /// <summary>
    /// Set to true while the radial menu is open so the cursor stays visible
    /// during gameplay. InventoryRadialMenu manages this flag.
    /// </summary>
    public static bool RadialMenuOpen { get; set; }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (InputManager.Actions == null) return;

        bool isGamepad  = InputManager.CurrentScheme == InputScheme.Gamepad;
        bool inGameplay = GameManager.Instance != null && GameManager.Instance.IsPlayingOrReady();
        // Show during menus, OR during gameplay when the radial menu is open
        bool showCursor = isGamepad && (!inGameplay || RadialMenuOpen);

        if (_cursorGO.activeSelf != showCursor)
            _cursorGO.SetActive(showCursor);

        if (showCursor)
        {
            Cursor.visible = false; // hide OS cursor behind our virtual one
            MoveCursor();
            UpdateHover();
            HandleClick();
        }
        else
        {
            ClearHover();
        }
    }

    // ── Cursor movement ───────────────────────────────────────────────────────

    private void MoveCursor()
    {
        var dir = InputManager.Actions.UI.Navigate.ReadValue<Vector2>();
        _cursorPos += dir * CURSOR_SPEED * Time.unscaledDeltaTime;
        _cursorPos.x = Mathf.Clamp(_cursorPos.x, 0f, Screen.width);
        _cursorPos.y = Mathf.Clamp(_cursorPos.y, 0f, Screen.height);
        // anchorMin = (0,0) → anchoredPosition maps directly to screen pixels
        _cursorRt.anchoredPosition = _cursorPos;
    }

    // ── Hover highlight ───────────────────────────────────────────────────────

    private void UpdateHover()
    {
        if (EventSystem.current == null) return;

        var target = TopSelectableUnderCursor();

        if (target == _lastHovered) return;

        if (_lastHovered != null)
            ExecuteEvents.Execute(_lastHovered,
                new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerExitHandler);

        _lastHovered = target;

        if (_lastHovered != null)
            ExecuteEvents.Execute(_lastHovered,
                new PointerEventData(EventSystem.current) { position = _cursorPos },
                ExecuteEvents.pointerEnterHandler);
    }

    private void ClearHover()
    {
        if (_lastHovered == null) return;
        if (EventSystem.current != null)
            ExecuteEvents.Execute(_lastHovered,
                new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerExitHandler);
        _lastHovered = null;
    }

    // ── Click ─────────────────────────────────────────────────────────────────

    private void HandleClick()
    {
        if (!InputManager.Actions.UI.ConfirmUI.WasPerformedThisFrame()) return;
        if (EventSystem.current == null) return;

        Raycast();

        foreach (var h in _hits)
        {
            // Button: use ExecuteHierarchy so clicks on child Text/Image still work
            var pd = new PointerEventData(EventSystem.current)
                { position = _cursorPos, button = PointerEventData.InputButton.Left };

            if (ExecuteEvents.ExecuteHierarchy(h.gameObject, pd, ExecuteEvents.pointerClickHandler))
                return;

            // Slider: set value proportional to cursor position on the track
            var slider = h.gameObject.GetComponentInParent<Slider>();
            if (slider != null)
            {
                var sliderRt = slider.GetComponent<RectTransform>();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    sliderRt, _cursorPos, null, out var local);
                float t = Mathf.InverseLerp(sliderRt.rect.xMin, sliderRt.rect.xMax, local.x);
                slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, t);
                return;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private GameObject TopSelectableUnderCursor()
    {
        Raycast();
        foreach (var h in _hits)
            if (h.gameObject.GetComponentInParent<Selectable>() != null)
                return h.gameObject;
        return null;
    }

    private void Raycast()
    {
        _hits.Clear();
        if (EventSystem.current == null) return;
        EventSystem.current.RaycastAll(
            new PointerEventData(EventSystem.current) { position = _cursorPos }, _hits);
    }

    private void OnSchemeChanged(InputScheme scheme)
    {
        if (scheme != InputScheme.MouseKeyboard) return;
        // Player picked up the mouse — restore OS cursor visibility.
        // GameManager will manage it further from here (cursor play/menu modes).
        bool inGameplay = GameManager.Instance != null && GameManager.Instance.IsPlayingOrReady();
        if (!inGameplay) Cursor.visible = true;
    }
}
