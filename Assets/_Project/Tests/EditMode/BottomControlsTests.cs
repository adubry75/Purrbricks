using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BottomControlsTests
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    GameObject gameObject, hudObject;
    Component game, hud;
    object oldGame, oldHud;
    FieldInfo gameSingleton, hudSingleton;
    bool cursor;
    [SetUp] public void Setup()
    {
        cursor = Cursor.visible;
        gameObject = new GameObject("BottomControlsGame"); gameObject.SetActive(false);
        hudObject = new GameObject("BottomControlsHud"); hudObject.SetActive(false);
        game = gameObject.AddComponent(Type.GetType("GameManager, Assembly-CSharp", true));
        hud = hudObject.AddComponent(Type.GetType("PowerupHUD, Assembly-CSharp", true));
        gameSingleton = game.GetType().GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        hudSingleton = hud.GetType().GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        oldGame = gameSingleton.GetValue(null); oldHud = hudSingleton.GetValue(null);
        gameSingleton.SetValue(null, game); hudSingleton.SetValue(null, hud);
        SetState("Playing");
        hud.GetType().GetMethod("Build", F).Invoke(hud, null);
        hud.GetType().GetMethod("SetVisible").Invoke(hud, new object[]{true});
    }
    void SetState(string state) => game.GetType().GetField("_state", F).SetValue(game, Enum.Parse(Type.GetType("GameState, Assembly-CSharp", true), state));
    bool Flag(object o, string name) => (bool)o.GetType().GetProperty(name).GetValue(o);
    void Toggle() { var method = hud.GetType().GetMethod("ToggleControls"); Assert.That(method, Is.Not.Null); method.Invoke(hud, null); }
    [TearDown] public void Cleanup()
    {
        gameSingleton.SetValue(null, oldGame); hudSingleton.SetValue(null, oldHud);
        UnityEngine.Object.DestroyImmediate(hudObject); UnityEngine.Object.DestroyImmediate(gameObject);
        Time.timeScale = 1f; Cursor.visible = cursor;
    }
    [Test] public void ExplicitControlsPauseGameplayButPermitInventoryUse()
    {
        Toggle();
        Assert.That(Flag(hud,"IsControlMode"), Is.True);
        Assert.That(Flag(game,"IsGameplaySuspended"), Is.True);
        Assert.That(Flag(game,"IsInventoryUseBlocked"), Is.False);
        Assert.That(Time.timeScale, Is.Zero);
        Toggle();
        Assert.That(Flag(hud,"IsControlMode"), Is.False);
        Assert.That(Flag(game,"IsGameplaySuspended"), Is.False);
    }
    [Test] public void ControlsNeverBypassAnotherPause()
    {
        Toggle(); SetState("Paused");
        Assert.That(Flag(game,"IsInventoryUseBlocked"), Is.True);
        hud.GetType().GetMethod("CloseControls").Invoke(hud, null);
        Assert.That(Time.timeScale, Is.Zero);
    }
    [Test] public void BottomButtonsDoNotInterceptGameplayMouse()
    {
        var field = hud.GetType().GetField("controlsGroup", F); Assert.That(field, Is.Not.Null);
        var group = (CanvasGroup)field.GetValue(hud);
        Assert.That(group.blocksRaycasts, Is.False);
        Toggle(); Assert.That(group.blocksRaycasts, Is.True);
        Toggle(); Assert.That(group.blocksRaycasts, Is.False);
        Assert.That(hudObject.transform.Find("CompactHUD/Inventory hint"), Is.Null,
            "Persistent inventory instructions must not overlap the Fury instructions");
    }
}
