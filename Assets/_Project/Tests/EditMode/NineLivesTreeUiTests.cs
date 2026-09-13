using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class NineLivesTreeUiTests
{
    [Test] public void EveryUpgradeHasAVisibleNonOverlappingSelectionTarget()
    {
        var type = Type.GetType("NineLivesTreeUI, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, "The complete upgrade tree must be available");
        var go = new GameObject("Tree layout test"); go.SetActive(false);
        try
        {
            var ui = go.AddComponent(type);
            type.GetMethod("BuildUI", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, null);
            var nodes = go.GetComponentsInChildren<RectTransform>(true).Where(x => x.name.StartsWith("Node_")).ToArray();
            Assert.That(nodes.Length, Is.EqualTo(15));
            for (int i = 0; i < nodes.Length; i++)
            {
                var rect = new Rect(nodes[i].anchoredPosition - nodes[i].sizeDelta / 2, nodes[i].sizeDelta);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(-900));
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(400));
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(-330));
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(300));
                for (int j = i+1; j<nodes.Length; j++)
                {
                    var other = new Rect(nodes[j].anchoredPosition - nodes[j].sizeDelta / 2, nodes[j].sizeDelta);
                    Assert.That(rect.Overlaps(other), Is.False, nodes[i].name + " overlaps " + nodes[j].name);
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
