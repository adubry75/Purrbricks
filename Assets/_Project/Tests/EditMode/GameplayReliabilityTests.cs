using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameplayReliabilityTests
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    GameObject ballGO, managerGO;
    Type Runtime(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    object Kind(string name) => Enum.Parse(Runtime("PowerupType"), name);
    object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Fields).Invoke(target, args);
    void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    Component Ball()
    {
        ballGO = new GameObject("TestBall");
        ballGO.AddComponent<Rigidbody2D>();
        var ball = ballGO.AddComponent(Runtime("BallController"));
        if (Get(ball, "_rb") == null) Call(ball, "Awake");
        ball.GetType().GetMethod("OnEnable", Fields)?.Invoke(ball, null);
        return ball;
    }
    Component Manager()
    {
        managerGO = new GameObject("TestPowerupManager");
        return managerGO.AddComponent(Runtime("PowerupManager"));
    }
    [TearDown] public void Cleanup()
    {
        if (ballGO != null)
        {
            var ball = ballGO.GetComponent(Runtime("BallController"));
            ball.GetType().GetMethod("OnDisable", Fields)?.Invoke(ball, null);
            UnityEngine.Object.DestroyImmediate(ballGO);
        }
        if (managerGO != null) UnityEngine.Object.DestroyImmediate(managerGO);
        foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name == "TestBall(Clone)") UnityEngine.Object.DestroyImmediate(go);
    }
    [Test] public void TimedStickyExpiry_PreservesPermanentSticky()
    {
        var ball = Ball(); var manager = Manager();
        Call(manager, "Apply", Kind("PermanentStickyBall"));
        Call(manager, "Apply", Kind("StickyBall"));
        var timers = (IDictionary)Call(manager, "GetAllTimers");
        timers[Kind("StickyBall")] = -1f;
        Call(manager, "Update");
        Assert.That(Get(ball, "_isSticky"), Is.True);
        Call(manager, "ResetAll");
        Assert.That(Get(ball, "_isSticky"), Is.False);
    }
    [TestCase("BigBall", "TinyBall")]
    [TestCase("TinyBall", "BigBall")]
    public void LatestSize_RemovesOpposingTimer(string first, string last)
    {
        Ball(); var manager = Manager();
        Call(manager, "Apply", Kind(first)); Call(manager, "Apply", Kind(last));
        Assert.That(Call(manager, "IsActive", Kind(first)), Is.False);
        Assert.That(Call(manager, "IsActive", Kind(last)), Is.True);
    }
    [Test] public void RepeatedBumperBoost_DecaysToNormalSpeed()
    {
        var ball = Ball(); Call(ball, "Launch");
        Call(ball, "TriggerBumperBoost", 0.05f); Call(ball, "TriggerBumperBoost", 0.05f);
        for (int i = 0; i < 10; i++) Call(ball, "FixedUpdate");
        Assert.That((float)Get(ball, "_bumperMultiplier"), Is.EqualTo(1f).Within(0.001f));
    }
    [Test] public void HeldBallClone_HasVelocityAndOnlyOneAimLine()
    {
        var ball = Ball(); Call(ball, "Launch");
        Set(ball, "_isStickyHeld", true); ballGO.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        Call(ball, "SpawnClone", 35f);
        var clone = GameObject.Find("TestBall(Clone)");
        var cloneBall = clone.GetComponent(Runtime("BallController"));
        if (Get(cloneBall, "_aimLineGO") == null) Call(cloneBall, "Awake");
        Assert.That(clone.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude, Is.GreaterThan(1f));
        Assert.That(clone.GetComponentsInChildren<LineRenderer>(true).Length, Is.EqualTo(1));
    }
    [Test] public void InventoryMultiball_RejectsUnlaunchedBall()
    {
        Ball(); var manager = Manager();
        var method = manager.GetType().GetMethod("CanApplyFromInventory");
        Assert.That(method, Is.Not.Null, "Inventory eligibility must be checked before consumption");
        Assert.That(method.Invoke(manager, new object[] { Kind("MultiBall"), null }), Is.False);
    }
}
