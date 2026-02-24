using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Generates and persists a unique 4-letter code per level index.
/// Codes are generated lazily — only when the player first reaches a level.
/// Each installation generates different codes so they can't be shared online.
/// </summary>
public class LevelCodeManager : MonoBehaviour
{
    public static LevelCodeManager Instance { get; private set; }

    // Omit I and O to avoid visual confusion with 1 and 0
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string PrefKeyPrefix = "lvlcode_";
    private const int MaxLevels = 200; // upper bound for loading stored codes

    // Reverse lookup: code string → level index
    private readonly Dictionary<string, int> _codeToLevel = new Dictionary<string, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadExistingCodes();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the 4-letter code for this level, generating and persisting
    /// one on first call (i.e. the first time the player reaches this level).
    /// </summary>
    public string GetOrCreateCode(int levelIndex)
    {
        string key = PrefKeyPrefix + levelIndex;
        string existing = PlayerPrefs.GetString(key, string.Empty);
        if (!string.IsNullOrEmpty(existing)) return existing;

        string code = GenerateUniqueCode();
        _codeToLevel[code] = levelIndex;
        PlayerPrefs.SetString(key, code);
        PlayerPrefs.Save();
        return code;
    }

    /// <summary>
    /// Looks up a code and returns the level index it maps to.
    /// Returns false if the code has never been generated (unknown level).
    /// </summary>
    public bool TryGetLevelByCode(string code, out int levelIndex)
    {
        if (string.IsNullOrWhiteSpace(code)) { levelIndex = -1; return false; }
        return _codeToLevel.TryGetValue(code.Trim().ToUpperInvariant(), out levelIndex);
    }

    // ── Internals ──────────────────────────────────────────────────────────────

    private void LoadExistingCodes()
    {
        for (int i = 0; i < MaxLevels; i++)
        {
            string stored = PlayerPrefs.GetString(PrefKeyPrefix + i, string.Empty);
            if (!string.IsNullOrEmpty(stored))
                _codeToLevel[stored] = i;
        }
    }

    private string GenerateUniqueCode()
    {
        // Retry until we get a code not already assigned (26^4 = ~456k possibilities)
        for (int attempt = 0; attempt < 2000; attempt++)
        {
            string code = RandomCode();
            if (!_codeToLevel.ContainsKey(code)) return code;
        }
        // Extremely unlikely fallback — just return whatever we have
        return RandomCode();
    }

    private string RandomCode()
    {
        var sb = new StringBuilder(4);
        for (int i = 0; i < 4; i++)
            sb.Append(Alphabet[Random.Range(0, Alphabet.Length)]);
        return sb.ToString();
    }
}
