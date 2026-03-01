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

    // ── Tutorial ──────────────────────────────────────────────────────────────
    private Coroutine _tutRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPersistentData();
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

    private void RefreshBalanceText()
    {
        HudController.Instance?.RefreshBalance();
    }

    /// <summary>Balance display is now handled by HudController — this is a no-op kept for call-site compatibility.</summary>
    public void SetVisible(bool visible) { }

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
