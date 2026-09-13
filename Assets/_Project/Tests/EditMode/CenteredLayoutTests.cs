using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CenteredLayoutTests
{
    [TestCase(1920,1080)]
    [TestCase(1280,720)]
    [TestCase(1024,768)]
    [TestCase(854,480)]
    [TestCase(3440,1440)]
    [TestCase(1920,1200)]
    public void ArenaReservesHudSpaceAndHasEqualSideMargins(int width, int height)
    {
        var type = Type.GetType("PlayfieldLayoutMath, Purrbricks.Core");
        Assert.That(type, Is.Not.Null, "Shared arena fitting is missing");
        var method = type.GetMethod("Viewport", BindingFlags.Public | BindingFlags.Static);
        var viewport = (Rect)method.Invoke(null, new object[] { width, height });
        Assert.That(viewport.center.x, Is.EqualTo(0.5f).Within(0.0001));
        Assert.That(viewport.yMin, Is.GreaterThan(0));
        Assert.That(viewport.yMax, Is.LessThan(1));
        Assert.That(viewport.width, Is.GreaterThan(0.5f));
        Assert.That(viewport.height, Is.GreaterThan(0.5f));
        float scale = Mathf.Min(width / 1920f, height / 1080f);
        Assert.That((1-viewport.yMax)*height, Is.GreaterThanOrEqualTo(199*scale));
        Assert.That(viewport.yMin*height, Is.GreaterThanOrEqualTo(125*scale));
    }

    [Test]
    public void FavoriteSlotsRejectDuplicateBindingsAndInvalidSlots()
    {
        var type = Type.GetType("FavoriteSlots, Purrbricks.Core");
        Assert.That(type, Is.Not.Null, "Favorite slot rules are missing");
        var slots = Activator.CreateInstance(type);
        var assign = type.GetMethod("Assign");
        var get = type.GetMethod("Get");
        Assert.That(assign.Invoke(slots, new object[]{0,5}), Is.EqualTo(true));
        Assert.That(assign.Invoke(slots, new object[]{1,5}), Is.EqualTo(true));
        Assert.That(get.Invoke(slots, new object[]{0}), Is.EqualTo(-1));
        Assert.That(get.Invoke(slots, new object[]{1}), Is.EqualTo(5));
        Assert.That(assign.Invoke(slots, new object[]{3,7}), Is.EqualTo(false));
        Assert.That(assign.Invoke(slots, new object[]{2,22}), Is.EqualTo(false));
    }
}
