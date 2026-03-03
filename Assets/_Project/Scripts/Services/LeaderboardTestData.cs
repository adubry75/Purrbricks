using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public static class LeaderboardTestData
{
    public const int FakeCompetitorCount = 100;

    public static bool Enabled => Debug.isDebugBuild;

    public static void RerollForBoard(string boardName)
    {
        if (!Enabled || string.IsNullOrEmpty(boardName)) return;
        PlayerPrefs.DeleteKey(DesiredRankKey(boardName));
    }

    public static int GetOrRollDesiredRank(string boardName)
    {
        if (!Enabled || string.IsNullOrEmpty(boardName)) return -1;

        string key = DesiredRankKey(boardName);
        int stored = PlayerPrefs.GetInt(key, -1);
        if (stored >= 1 && stored <= FakeCompetitorCount + 1) return stored;

        int desiredRank;
        if (UnityEngine.Random.value < 0.5f)
            desiredRank = UnityEngine.Random.Range(1, 6); // 1..5
        else
            desiredRank = UnityEngine.Random.Range(6, FakeCompetitorCount + 2); // 6..101

        PlayerPrefs.SetInt(key, desiredRank);
        PlayerPrefs.Save();
        return desiredRank;
    }

    public static List<LeaderboardEntryModel> BuildSimulatedBoard(string boardName, LeaderboardEntryModel meEntry)
    {
        if (!Enabled || meEntry == null) return null;

        int desiredRank = Mathf.Clamp(GetOrRollDesiredRank(boardName), 1, FakeCompetitorCount + 1);
        int myScore = meEntry.Score;

        var rng = new System.Random(StableHash32(boardName) ^ myScore);
        var items = new List<LeaderboardEntryModel>(FakeCompetitorCount + 1);

        // Scores strictly above me (so my rank is desiredRank)
        int higherCount = desiredRank - 1;
        for (int i = 0; i < higherCount; i++)
        {
            int delta = 1 + rng.Next(25, 2500);
            int score = myScore + delta + i; // ensure unique and strictly greater
            items.Add(new LeaderboardEntryModel(0, score, FakeSteamId(boardName, i), FakeName(rng, i)));
        }

        // My entry (Steam-backed)
        items.Add(new LeaderboardEntryModel(0, myScore, meEntry.SteamId, meEntry.DisplayName));

        // Scores at/below me
        for (int i = higherCount; i < FakeCompetitorCount; i++)
        {
            int delta = 1 + rng.Next(10, 2000);
            int score = myScore - delta - (i - higherCount);
            items.Add(new LeaderboardEntryModel(0, score, FakeSteamId(boardName, i), FakeName(rng, i)));
        }

        items.Sort((a, b) => b.Score.CompareTo(a.Score));

        for (int i = 0; i < items.Count; i++)
        {
            var e = items[i];
            items[i] = new LeaderboardEntryModel(i + 1, e.Score, e.SteamId, e.DisplayName);
        }

        return items;
    }

    private static string DesiredRankKey(string boardName) => $"LB_SIM_RANK_{boardName}";

    private static CSteamID FakeSteamId(string boardName, int i)
    {
        unchecked
        {
            ulong baseId = 0x110000100000000UL;
            ulong salt = (ulong)StableHash32(boardName);
            return new CSteamID(baseId + (salt * 131UL) + (ulong)i + 1UL);
        }
    }

    private static string FakeName(System.Random rng, int i)
    {
        string[] prefixes = { "PurrBot", "Cat", "Meow", "Whiskers", "Nyan", "Paw", "Furball", "Tuna", "Mittens", "Claw" };
        string[] suffixes = { "Ace", "Pro", "King", "Queen", "Ninja", "Wizard", "Hero", "Ranger", "Bandit", "Prime" };
        string p = prefixes[rng.Next(prefixes.Length)];
        string s = suffixes[rng.Next(suffixes.Length)];
        return $"{p}{s}{i + 1:000}";
    }

    private static int StableHash32(string text)
    {
        unchecked
        {
            const uint fnvOffset = 2166136261;
            const uint fnvPrime = 16777619;
            uint hash = fnvOffset;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= fnvPrime;
            }
            return (int)hash;
        }
    }
}

