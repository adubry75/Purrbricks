using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameplaySuspensionTests
{
    GameObject go;
    Type gmType => Type.GetType("GameManager, Assembly-CSharp", true);
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    Component Create(string state)
    {
        go = new GameObject("SuspensionTest"); go.SetActive(false);
        var gm = go.AddComponent(gmType);
        gmType.GetField("_state", Flags).SetValue(gm, Enum.Parse(Type.GetType("GameState, Assembly-CSharp"), state));
        return gm;
    }
    [TearDown] public void Cleanup() { if (go != null) UnityEngine.Object.DestroyImmediate(go); Time.timeScale = 1f; }
    [Test] public void RestoreGameplayTimeScale_PreservesPause()
    {
        var gm = Create("Paused"); var restore = gmType.GetMethod("RestoreGameplayTimeScale");
        Assert.That(restore, Is.Not.Null);
        restore.Invoke(gm, null); Assert.That(Time.timeScale, Is.Zero);
    }
    [Test] public void AdvanceDelay_StaysPendingWhilePaused()
    {
        var gm = Create("Paused");
        var wait = gmType.GetMethod("WaitForGameplaySeconds", Flags);
        Assert.That(wait, Is.Not.Null, "Cinematic waits must suspend with gameplay");
        var routine = (IEnumerator)wait.Invoke(gm, new object[] { 0.01f, 0 });
        for (int i = 0; i < 10; i++) Assert.That(routine.MoveNext(), Is.True);
    }
    [Test] public void AdvanceDelay_CancelsWhenRunChanges()
    {
        var gm = Create("Playing"); var wait = gmType.GetMethod("WaitForGameplaySeconds", Flags);
        Assert.That(wait, Is.Not.Null);
        var routine = (IEnumerator)wait.Invoke(gm, new object[] { 100f, -1 });
        Assert.That(routine.MoveNext(), Is.False);
    }
    [TestCase("Ready")]
    [TestCase("Playing")]
    public void EnteringGameplay_DismissesResults(string state)
    {
        var gm = Create("Victory");
        var victoryGO = new GameObject("ResultTest");
        victoryGO.SetActive(false);
        var victory = victoryGO.AddComponent(Type.GetType("VictoryUI, Assembly-CSharp", true));
        gmType.GetField("_victoryUI", Flags).SetValue(gm, victory);
        victoryGO.SetActive(true);
        try
        {
            gmType.GetMethod("SetState").Invoke(gm, new object[] { Enum.Parse(Type.GetType("GameState, Assembly-CSharp"), state) });
            Assert.That(victoryGO.activeSelf, Is.False, "A result screen must not survive entry into gameplay");
        }
        finally { UnityEngine.Object.DestroyImmediate(victoryGO); }
    }}

