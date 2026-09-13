using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class NineLivesXpBreakdownTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    [TestCase(-1, 3, 0, 105, "Bricks 0 + Clear 30\nFirst clear 60 + Stars 15")]
    [TestCase(3, 3, 0, 30, "Bricks 0 + Clear 30\nFirst clear 0 + Stars 0")]
    [TestCase(1, 3, 0, 40, "Bricks 0 + Clear 30\nFirst clear 0 + Stars 10")]
    [TestCase(-1, 3, 10250, 40, "Bricks 0 + Clear 30\nFirst clear 10 + Stars 0")]
    [TestCase(-1, 3, 10290, 0, "Bricks 0 + Clear 0\nFirst clear 0 + Stars 0")]
    public void CompletionBreakdown_ReportsActualCredits(int previousStars, int stars, int xp, int expected, string breakdown)
    {
        string[] keys = { "NineLives_Save_v1", "NineLives_Save_v1_backup" };
        var saved = new Dictionary<string, string>();
        foreach (string key in keys) if (PlayerPrefs.HasKey(key)) saved[key] = PlayerPrefs.GetString(key);
        var go = new GameObject("XpBreakdownTest"); go.SetActive(false);
        try
        {
            var type = Type.GetType("NineLivesService, Assembly-CSharp");
            var service = go.AddComponent(type);
            var model = type.GetProperty("Model").GetValue(service);
            model.GetType().GetField("IntroGranted").SetValue(model, true);
            model.GetType().GetField("TotalXp").SetValue(model, xp);
            var ledger = (Dictionary<string, int>)model.GetType().GetField("CreditedStars").GetValue(model);
            if (previousStars >= 0) ledger["test_level"] = previousStars;
            type.GetMethod("BeginAttempt").Invoke(service, new object[] { "test_level", true });
            type.GetMethod("CompleteLevel").Invoke(service, new object[] { stars });
            Assert.That(type.GetProperty("LastAttemptXp").GetValue(service), Is.EqualTo(expected));
            Assert.That(type.GetProperty("LastAttemptXpBreakdown").GetValue(service), Is.EqualTo(breakdown));
            type.GetMethod("CompleteLevel").Invoke(service, new object[] { stars });
            Assert.That(type.GetProperty("LastAttemptXp").GetValue(service), Is.EqualTo(expected), "Duplicate completion must not award twice.");
            type.GetMethod("BeginAttempt").Invoke(service, new object[] { "test_level", true });
            Assert.That(type.GetProperty("LastAttemptXp").GetValue(service), Is.EqualTo(0));
            Assert.That(type.GetProperty("LastAttemptXpBreakdown").GetValue(service), Is.EqualTo("Bricks 0 + Clear 0\nFirst clear 0 + Stars 0"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            foreach (string key in keys) { if (saved.ContainsKey(key)) PlayerPrefs.SetString(key, saved[key]); else PlayerPrefs.DeleteKey(key); }
            PlayerPrefs.Save();
        }
    }
}
