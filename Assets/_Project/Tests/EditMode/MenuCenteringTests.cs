using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MenuCenteringTests
{
    [TestCase("MainMenuUI")]
    [TestCase("VictoryUI")]
    [TestCase("HighScoresUI")]
    public void FullScreenMenuHasNoLegacySidebarOffset(string name)
    {
        var go = new GameObject("Centering test");
        go.SetActive(false);
        try
        {
            var type = Type.GetType(name + ", Assembly-CSharp", true);
            var component = go.AddComponent(type);
            type.GetMethod("BuildUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
            var panelField = type.GetField("_panel", BindingFlags.Instance | BindingFlags.NonPublic);
            var panel = panelField != null ? (GameObject)panelField.GetValue(component) : go.transform.Find("Panel").gameObject;
            var rect = panel.GetComponent<RectTransform>();
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    [Test]
    public void HudCursorRefreshPreservesCursorAfterShortcutOpensRadial()
    {
        var nav = Type.GetType("UINavController, Assembly-CSharp", true);
        var field = nav.GetProperty("RadialMenuOpen", BindingFlags.Public | BindingFlags.Static);
        bool previous = (bool)field.GetValue(null);
        bool visible = Cursor.visible;
        try
        {
            field.SetValue(null, true);
            Cursor.visible = false;
            var method = Type.GetType("PowerupHUD, Assembly-CSharp", true).GetMethod("RefreshPointerVisibility", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, null);
            Assert.That(Cursor.visible, Is.True);
        }
        finally { field.SetValue(null, previous); Cursor.visible = visible; }
    }}


