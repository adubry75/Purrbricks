using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that manages the Purr Bucks meta-currency and powerup inventory.
/// Handles persistence, the HUD balance overlay, async Steam rank awards,
/// the 2% inventory drop mechanic, and tutorial callouts.
/// </summary>
public class PurrBucksManager : MonoBehaviour
{
    public static PurrBucksManager Instance { get; private set; }

    // ── PlayerPrefs keys ─────────────────────────────────────────────────────
    private const string KEY_BALANCE       = "purrBucks";
    private const string KEY_INV_PREFIX    = "inv_";
    private const string KEY_CLEARED_PREFIX= "cleared_";
    private const string KEY_TUT_PB        = "tut_pb_earned";
    private const string KEY_TUT_RADIAL    = "tut_radial_opened";

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action OnBalanceChanged;
    public event Action OnInventoryChanged;
    /// <summary>Fires with the total Purr Bucks awarded for the last level (once rank resolves).</summary>
    public event Action<int> OnRankAwardResolved;

    // ── State ─────────────────────────────────────────────────────────────────
    public int Balance { get; private set; }
    private readonly Dictionary<PowerupType, int> _inventory = new Dictionary<PowerupType, int>();
    private int _pendingAward; // tracks amount awarded so far in current level-complete flow

    // ── HUD overlay ───────────────────────────────────────────────────────────
    private Canvas _hudCanvas;
    private Text   _balanceText;
    private GameObject _hudRoot;

    // ── Tutorial ──────────────────────────────────────────────────────────────
    private Coroutine _tutRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPersistentData();
        BuildHudOverlay();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void LoadPersistentData()
    {
        Balance = PlayerPrefs.GetInt(KEY_BALANCE, 0);

        // Load inventory for every known PowerupType
        foreach (PowerupType type in System.Enum.GetValues(typeof(PowerupType)))
        {
            int qty = PlayerPrefs.GetInt(KEY_INV_PREFIX + (int)type, 0);
            if (qty > 0) _inventory[type] = qty;
        }
    }

    private void SaveBalance()
    {
        PlayerPrefs.SetInt(KEY_BALANCE, Balance);
        PlayerPrefs.Save();
    }

    private void SaveInventory(PowerupType type)
    {
        PlayerPrefs.SetInt(KEY_INV_PREFIX + (int)type, _inventory.TryGetValue(type, out int qty) ? qty : 0);
        PlayerPrefs.Save();
    }

    // ── Currency ──────────────────────────────────────────────────────────────

    public void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        Balance += amount;
        SaveBalance();
        RefreshBalanceText();
        OnBalanceChanged?.Invoke();

        // First-earn tutorial
        if (Balance > 0 && !HasSeenTutorial(KEY_TUT_PB))
        {
            MarkTutorialSeen(KEY_TUT_PB);
            if (_tutRoutine != null) StopCoroutine(_tutRoutine);
            _tutRoutine = StartCoroutine(ShowTutorialCallout(
                "PURR BUCKS EARNED!\nTap 🐾 above to open the Store"));
        }
    }

    /// <summary>Returns false if balance is insufficient; true if deducted successfully.</summary>
    public bool TrySpend(int amount)
    {
        if (amount > Balance) return false;
        Balance -= amount;
        SaveBalance();
        RefreshBalanceText();
        OnBalanceChanged?.Invoke();
        return true;
    }

    // ── Inventory ─────────────────────────────────────────────────────────────

    public int GetInventoryCount(PowerupType type)
        => _inventory.TryGetValue(type, out int qty) ? qty : 0;

    public void AddToInventory(PowerupType type, int qty = 1)
    {
        if (qty <= 0) return;
        _inventory[type] = GetInventoryCount(type) + qty;
        SaveInventory(type);
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Consumes one unit from inventory and immediately applies the powerup.
    /// Returns false if inventory is empty.
    /// </summary>
    public bool TryUseFromInventory(PowerupType type)
    {
        int current = GetInventoryCount(type);
        if (current <= 0) return false;

        _inventory[type] = current - 1;
        if (_inventory[type] == 0) _inventory.Remove(type);
        SaveInventory(type);
        OnInventoryChanged?.Invoke();

        PowerupManager.Instance?.Apply(type);
        return true;
    }

    /// <summary>Returns a copy of the inventory (types with qty > 0 only).</summary>
    public Dictionary<PowerupType, int> GetAllInventory()
    {
        var result = new Dictionary<PowerupType, int>();
        foreach (var kvp in _inventory)
            if (kvp.Value > 0) result[kvp.Key] = kvp.Value;
        return result;
    }

    // ── Inventory Drop ────────────────────────────────────────────────────────

    /// <summary>
    /// Rolls the 2% inventory drop chance. Adds to inventory and returns true if the drop
    /// triggers. PowerupManager fires OnInventoryDrop for the VFX.
    /// </summary>
    public bool RollInventoryDrop(PowerupType type)
    {
        if (UnityEngine.Random.value > PurrBucksConfig.INVENTORY_DROP_CHANCE) return false;
        if (PowerupRules.IsBad(type) && !PurrBucksConfig.INVENTORY_DROP_BAD_POWERUPS) return false;

        AddToInventory(type, 1);
        return true;
    }

    // ── Level Complete Award ──────────────────────────────────────────────────

    /// <summary>
    /// Awards Purr Bucks for level completion. Base floor + bonuses paid immediately;
    /// Steam rank tier resolved asynchronously and fire OnRankAwardResolved when done.
    /// </summary>
    public void AwardLevelComplete(string levelId, int levelIndex, bool perfectClear, int livesLost)
    {
        _pendingAward = 0;

        bool isFirstTime = PlayerPrefs.GetInt(KEY_CLEARED_PREFIX + levelId, 0) == 0;
        if (isFirstTime) PlayerPrefs.SetInt(KEY_CLEARED_PREFIX + levelId, 1);

        // ── Immediate awards ──────────────────────────────────────────────────
        int immediate = PurrBucksConfig.REWARD_PARTICIPATION;
        if (perfectClear)  immediate += PurrBucksConfig.REWARD_PERFECT_CLEAR;
        if (isFirstTime)   immediate += PurrBucksConfig.REWARD_FIRST_TIME;

        _pendingAward = immediate;
        AddCurrency(immediate);

        // ── Async Steam rank ──────────────────────────────────────────────────
        StartCoroutine(FetchRankAndAward(levelIndex));
    }

    private IEnumerator FetchRankAndAward(int levelIndex)
    {
        // Give Steam time to process the score upload before querying rank
        yield return new WaitForSecondsRealtime(2f);

        string boardName = $"Purrbricks_level_{levelIndex:D2}";

        bool resolved = false;
        int rankBonus = 0;

        if (SteamLeaderboardManager.Instance != null)
        {
            SteamLeaderboardManager.Instance.FetchAroundMe(boardName, 0, entries =>
            {
                if (entries != null && entries.Count > 0)
                {
                    int rank = entries[0].Rank;
                    if      (rank == 1) rankBonus = PurrBucksConfig.REWARD_FIRST_PLACE  - PurrBucksConfig.REWARD_PARTICIPATION;
                    else if (rank == 2) rankBonus = PurrBucksConfig.REWARD_SECOND_PLACE - PurrBucksConfig.REWARD_PARTICIPATION;
                    else if (rank == 3) rankBonus = PurrBucksConfig.REWARD_THIRD_PLACE  - PurrBucksConfig.REWARD_PARTICIPATION;
                }
                resolved = true;
            });

            // Wait for callback, timeout after 5 seconds
            float elapsed = 0f;
            while (!resolved && elapsed < 5f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (rankBonus > 0)
        {
            _pendingAward += rankBonus;
            AddCurrency(rankBonus);
        }

        OnRankAwardResolved?.Invoke(_pendingAward);
    }

    // ── Tutorial helpers ──────────────────────────────────────────────────────

    public bool HasSeenTutorial(string key) => PlayerPrefs.GetInt(key, 0) == 1;
    public void MarkTutorialSeen(string key) { PlayerPrefs.SetInt(key, 1); PlayerPrefs.Save(); }

    // ── HUD Overlay ───────────────────────────────────────────────────────────

    private void BuildHudOverlay()
    {
        _hudCanvas = gameObject.AddComponent<Canvas>();
        _hudCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _hudCanvas.sortingOrder = 51; // just above PowerupHUD (50)

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Clickable container (top-right corner)
        _hudRoot = new GameObject("PurrBucksBalance");
        _hudRoot.transform.SetParent(transform, false);

        var bg = _hudRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        var rt = _hudRoot.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.sizeDelta        = new Vector2(215f, 30f);
        rt.anchoredPosition = new Vector2(-5f, -5f);

        // Button to open store
        var btn = _hudRoot.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
        colors.pressedColor     = new Color(0.85f, 0.85f, 0.85f);
        btn.colors = colors;
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() => GameManager.Instance?.ShowStore());

        // Paw icon
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(_hudRoot.transform, false);
        var iconTxt = iconGO.AddComponent<Text>();
        iconTxt.text          = "🐾";
        iconTxt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iconTxt.fontSize      = 18;
        iconTxt.alignment     = TextAnchor.MiddleLeft;
        iconTxt.color         = new Color(1f, 0.85f, 0.10f);
        iconTxt.raycastTarget = false;
        var iconRt = iconTxt.GetComponent<RectTransform>();
        iconRt.anchorMin        = new Vector2(0f, 0f);
        iconRt.anchorMax        = new Vector2(0f, 1f);
        iconRt.sizeDelta        = new Vector2(28f, 0f);
        iconRt.anchoredPosition = new Vector2(14f, 0f);

        // Balance text
        var balGO = new GameObject("Balance");
        balGO.transform.SetParent(_hudRoot.transform, false);
        _balanceText = balGO.AddComponent<Text>();
        _balanceText.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _balanceText.fontSize      = 18;
        _balanceText.fontStyle     = FontStyle.Bold;
        _balanceText.alignment     = TextAnchor.MiddleLeft;
        _balanceText.color         = new Color(1f, 0.85f, 0.10f);
        _balanceText.raycastTarget = false;
        var balRt = _balanceText.GetComponent<RectTransform>();
        balRt.anchorMin        = new Vector2(0f, 0f);
        balRt.anchorMax        = new Vector2(1f, 1f);
        balRt.offsetMin        = new Vector2(28f, 0f);
        balRt.offsetMax        = new Vector2(-6f, 0f);

        // Thin gold left border for visual distinction
        var border = new GameObject("Border");
        border.transform.SetParent(_hudRoot.transform, false);
        var bImg = border.AddComponent<Image>();
        bImg.color         = new Color(1f, 0.78f, 0.10f, 0.8f);
        bImg.raycastTarget = false;
        var bRt = border.GetComponent<RectTransform>();
        bRt.anchorMin        = new Vector2(0f, 0f);
        bRt.anchorMax        = new Vector2(0f, 1f);
        bRt.sizeDelta        = new Vector2(4f, 0f);
        bRt.anchoredPosition = new Vector2(2f, 0f);

        RefreshBalanceText();
    }

    private void RefreshBalanceText()
    {
        if (_balanceText != null)
            _balanceText.text = $"{Balance} PB";
    }

    /// <summary>Show/hide the balance overlay — hide during MainMenu, GameOver, etc.</summary>
    public void SetVisible(bool visible)
    {
        if (_hudRoot != null) _hudRoot.SetActive(visible);
    }

    // ── Tutorial callout ──────────────────────────────────────────────────────

    private IEnumerator ShowTutorialCallout(string message)
    {
        // Create a temporary floating text near the balance display
        var go = new GameObject("TutCallout");
        go.transform.SetParent(transform, false);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.10f, 0.20f, 0.92f);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.sizeDelta        = new Vector2(330f, 52f);
        rt.anchoredPosition = new Vector2(-5f, -40f); // just below the balance bar

        var txtGO = new GameObject("Txt");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.text          = message;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = 14;
        txt.fontStyle     = FontStyle.Bold;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.color         = new Color(1f, 0.92f, 0.40f);
        txt.raycastTarget = false;
        var txtRt = txtGO.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        // Add a gold outline accent
        var outline = go.AddComponent<Outline>();
        outline.effectColor    = new Color(1f, 0.78f, 0.10f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Animate in
        var canvasGroup = go.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        float t = 0f;
        while (t < 0.3f) { t += Time.unscaledDeltaTime; canvasGroup.alpha = t / 0.3f; yield return null; }
        canvasGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(4f);

        // Animate out
        t = 0f;
        while (t < 0.4f) { t += Time.unscaledDeltaTime; canvasGroup.alpha = 1f - t / 0.4f; yield return null; }

        Destroy(go);
        _tutRoutine = null;
    }
}
