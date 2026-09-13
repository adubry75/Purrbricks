using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class VfxPoolingTests
{
    [SetUp]
    public void SetUp()
    {
        DestroyEffectObjects();
    }

    private static Type FindRuntimeType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .First(type => type != null);
    }

    private static MethodInfo StaticMethod(Type type, string name)
    {
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    }

    private static MethodInfo RequiredStaticMethod(Type type, string name)
    {
        MethodInfo method = StaticMethod(type, name);
        Assert.That(method, Is.Not.Null, type.Name + "." + name + " must provide the pooling lifecycle operation");
        return method;
    }

    [TearDown]
    public void TearDown()
    {
        StaticMethod(FindRuntimeType("ScorePopup"), "ClearPool")?.Invoke(null, null);
        StaticMethod(FindRuntimeType("BrickParticleGenerator"), "ClearPool")?.Invoke(null, null);

        DestroyEffectObjects();
    }

    [Test]
    public void ScorePopup_ReusesFinishedPopupWithItsExistingTextHierarchy()
    {
        Type popupType = FindRuntimeType("ScorePopup");
        MethodInfo spawn = RequiredStaticMethod(popupType, "Spawn");
        spawn.Invoke(null, new object[] { Vector3.zero, 100, Color.white });

        var first = UnityEngine.Object.FindObjectsByType(popupType, FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Cast<Component>().Single();
        var originalObject = first.gameObject;
        MethodInfo release = popupType.GetMethod("Release", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(release, Is.Not.Null, "ScorePopup.Release must return a finished popup to its pool");
        release.Invoke(first, null);

        spawn.Invoke(null, new object[] { Vector3.one, 250, Color.yellow });

        var reused = UnityEngine.Object.FindObjectsByType(popupType, FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Cast<Component>().Single(component => component.gameObject.activeSelf);
        Assert.That(reused.gameObject, Is.EqualTo(originalObject));
        Assert.AreEqual(1, reused.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length);
        Assert.AreEqual("+250", reused.GetComponentInChildren<UnityEngine.UI.Text>(true).text);
    }

    [Test]
    public void BrickParticles_ReusesFinishedParticleSystemAndReconfiguresBurst()
    {
        Type generatorType = FindRuntimeType("BrickParticleGenerator");
        MethodInfo spawn = RequiredStaticMethod(generatorType, "SpawnBurst");
        spawn.Invoke(null, new object[] { Vector3.zero, Color.red, 12, false });

        ParticleSystem first = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(system => system.name == "BrickParticles");
        var originalObject = first.gameObject;
        Type pooledType = FindRuntimeType("PooledBrickParticles");
        Component pooled = first.GetComponent(pooledType);
        MethodInfo release = pooledType.GetMethod("Release", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(release, Is.Not.Null, "Particle completion must return the effect to its pool");
        release.Invoke(pooled, null);

        spawn.Invoke(null, new object[] { Vector3.one, Color.cyan, 35, true });

        ParticleSystem reused = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(system => system.name == "BrickParticles" && system.gameObject.activeSelf);
        var bursts = new ParticleSystem.Burst[1];
        Assert.That(reused.gameObject, Is.EqualTo(originalObject));
        Assert.AreEqual(1, reused.emission.GetBursts(bursts));
        Assert.AreEqual(35, bursts[0].maxCount);
        Assert.IsTrue(reused.trails.enabled);
    }

    [Test]
    public void ClearPool_DestroysActiveAndInactiveEffectObjects()
    {
        RequiredStaticMethod(FindRuntimeType("ScorePopup"), "Spawn").Invoke(null, new object[] { Vector3.zero, 100, Color.white });
        RequiredStaticMethod(FindRuntimeType("BrickParticleGenerator"), "SpawnBurst").Invoke(null, new object[] { Vector3.zero, Color.red, 12, false });

        RequiredStaticMethod(FindRuntimeType("ScorePopup"), "ClearPool").Invoke(null, null);
        RequiredStaticMethod(FindRuntimeType("BrickParticleGenerator"), "ClearPool").Invoke(null, null);

        Assert.IsEmpty(UnityEngine.Object.FindObjectsByType(FindRuntimeType("ScorePopup"), FindObjectsInactive.Include, FindObjectsSortMode.None));
        Assert.IsFalse(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(system => system.name == "BrickParticles"));
    }

    private static void DestroyEffectObjects()
    {
        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(go => go.name == "ScorePopup" || go.name == "BrickParticles" ||
                                  go.name == "ScorePopupPool" || go.name == "BrickParticlePool"))
            UnityEngine.Object.DestroyImmediate(go);
    }
}

