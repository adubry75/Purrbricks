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

        // Scores above me: spread from myScore+1 up to 5× myScore (or +100k, whichever is bigger).
        int higherCount = desiredRank - 1;
        int topScore    = Math.Max(myScore * 5, myScore + 100_000);
        int aboveRange  = topScore - myScore; // always > 0

        for (int i = 0; i < higherCount; i++)
        {
            int score = myScore + 1 + rng.Next(aboveRange);
            items.Add(new LeaderboardEntryModel(0, score, FakeSteamId(boardName, i), FakeName(rng, i)));
        }

        // My entry (Steam-backed)
        items.Add(new LeaderboardEntryModel(0, myScore, meEntry.SteamId, meEntry.DisplayName));

        // Scores below me: spread from 1 000 (or myScore/5, whichever is bigger) up to myScore-1.
        int botScore   = Math.Max(1_000, myScore / 5);
        if (botScore >= myScore) botScore = Math.Max(1, myScore - 1); // edge case: very low score
        int belowRange = Math.Max(1, myScore - 1 - botScore);

        for (int i = higherCount; i < FakeCompetitorCount; i++)
        {
            int score = botScore + rng.Next(belowRange + 1);
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
        string[] adj = {
            "Salty", "Spicy", "Crispy", "Soggy", "Crusty", "Flakey", "Chunky", "Sloppy",
            "Wobbly", "Sticky", "Gloomy", "Grumpy", "Dizzy", "Fuzzy", "Rusty", "Sneaky",
            "Chaotic", "Blazing", "Glitchy", "Toxic", "Creamy", "Squishy", "Wicked",
            "Crunchy", "Sweaty", "Stinky", "Funky", "Zippy", "Nasty", "Fancy",
            "Turbo", "Ultra", "Mega", "Dark", "Wild", "Shady", "Gritty", "Slippery",
            "Buttery", "Clammy", "Drenched", "Frosty", "Grimy", "Hollow", "Itchy",
        };
        string[] noun = {
            "Biscuit", "Waffle", "Noodle", "Ferret", "Muffin", "Tortilla", "Narwhal",
            "Walrus", "Baguette", "Cheddar", "Bandit", "Goblin", "Spatula", "Hedgehog",
            "Dongle", "Nugget", "Taco", "Burrito", "Pickle", "Potato", "Cabbage",
            "Salmon", "Turnip", "Bagel", "Pretzel", "Kipper", "Onion", "Crouton",
            "Pancake", "Nacho", "Chimichanga", "Calzone", "Stromboli", "Hoagie",
            "Brisket", "Bratwurst", "Schnitzel", "Haggis", "Lasagna", "Pierogi",
        };
        // Mix of recognisable gamer numbers and plain randoms for variety.
        int[] gamerNums = { 69, 420, 1337, 9000, 9001, 42, 404, 666, 777, 360, 2077, 1984, 808, 101, 247, 911, 007, 1, 2, 3 };

        string a = adj[rng.Next(adj.Length)];
        string n = noun[rng.Next(noun.Length)];
        int    num = rng.Next(4) == 0          // 25 % chance of a gamer number
            ? gamerNums[rng.Next(gamerNums.Length)]
            : rng.Next(10, 9999);

        return $"{a}{n}{num}";
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

