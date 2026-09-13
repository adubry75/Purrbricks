using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class NineLivesGameplayTests
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    readonly List<GameObject> objects = new List<GameObject>();
    readonly List<Action> restore = new List<Action>();
    Type T(string name) { var type = Type.GetType(name + ", Assembly-CSharp"); Assert.That(type, Is.Not.Null, name + " must exist"); return type; }
    object Kind(string name) => Enum.Parse(T("PowerupType"), name);
    Component Create(string type, bool active = false)
    {
        var go = new GameObject("NineLivesTest_" + type); objects.Add(go); go.SetActive(active);
        var component = go.AddComponent(T(type));
        if (type == "PowerupManager")
        {
            var paddle = Create("PaddleController"); Call(paddle, "Awake"); Set(component, "_paddle", paddle);
        }
        return component;
    }
    void Set(object target, string field, object value) => target.GetType().GetField(field, F).SetValue(target, value);
    object Get(object target, string field) => target.GetType().GetField(field, F).GetValue(target);
    object Call(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, F); Assert.That(method, Is.Not.Null, name + " must exist");
        return method.Invoke(target, args);
    }
    object Property(object target, string name)
    {
        var property = target.GetType().GetProperty(name, F); Assert.That(property, Is.Not.Null, name + " must exist");
        return property.GetValue(target);
    }
    void Singleton(Component component)
    {
        var field = component.GetType().GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = field.GetValue(null); field.SetValue(null, component); restore.Add(() => field.SetValue(null, previous));
    }
    Component Service(int mask)
    {
        var service = Create("NineLivesService"); Singleton(service);
        Set(service, "_eligibleAttempt", true); Set(service, "_activeMask", mask); return service;
    }
    Component Game()
    {
        var game = Create("GameManager"); Singleton(game);
        Set(game, "_state", Enum.Parse(T("GameState"), "Playing")); return game;
    }
    Component Ball()
    {
        var go = new GameObject("NineLivesTest_Ball"); objects.Add(go);
        go.AddComponent<Rigidbody2D>(); go.AddComponent<CircleCollider2D>();
        var ball = go.AddComponent(T("BallController"));
        if (Get(ball, "_rb") == null) Call(ball, "Awake");
        Call(ball, "OnEnable"); return ball;
    }
    [TearDown] public void Cleanup()
    {
        T("ScorePopup").GetMethod("ClearPool").Invoke(null, null);
        foreach (var popup in UnityEngine.Object.FindObjectsByType(T("ScorePopup"), FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(((Component)popup).gameObject);
        foreach (var clone in UnityEngine.Object.FindObjectsByType(T("BallController"), FindObjectsSortMode.None))
            if (clone.name == "NineLivesTest_Ball(Clone)" && !objects.Contains(((Component)clone).gameObject)) objects.Add(((Component)clone).gameObject);
        foreach (var go in objects)
        {
            if (go == null) continue;
            var ball = go.GetComponent(Type.GetType("BallController, Assembly-CSharp"));
            if (ball != null) Call(ball, "OnDisable");
            UnityEngine.Object.DestroyImmediate(go);
        }
        for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
        objects.Clear(); restore.Clear();
    }
    [TestCase("WidePaddle", 12.5f)]
    [TestCase("StickyBall", 12.5f)]
    [TestCase("Fireball", 12.5f)]
    [TestCase("ScoreFrenzy", 12.5f)]
    [TestCase("TinyBall", 8f)]
    [TestCase("FlipScreen", 8f)]
    public void TimedWorldPickup_UsesExactDuration(string kind, float seconds)
    {
        Service((1 << 5) | (1 << 12)); Game();
        var pm = Create("PowerupManager");
        Call(pm, "Apply", Kind(kind));
        Assert.That((float)Call(pm, "GetRemaining", Kind(kind)), Is.EqualTo(seconds).Within(.001f));
    }
    [Test] public void Encore_AppliesOnlyToDuplicateWorldPickup()
    {
        Service((1 << 5) | (1 << 7)); Game();
        var pm = Create("PowerupManager");
        Call(pm, "Apply", Kind("Fireball")); Call(pm, "Apply", Kind("Fireball"));
        Assert.That((float)Call(pm, "GetRemaining", Kind("Fireball")), Is.EqualTo(28f).Within(.001f));
        Assert.That(Call(pm, "TryApplyFromInventory", Kind("Fireball")), Is.True);
        Assert.That((float)Call(pm, "GetRemaining", Kind("Fireball")), Is.EqualTo(40.5f).Within(.001f));
    }
    [Test] public void GeneratedGrant_IsMaximumNotAdditionAndBypassesBonuses()
    {
        Service((1 << 5) | (1 << 7)); Game();
        var pm = Create("PowerupManager");
        Call(pm, "GrantAtLeast", Kind("Fireball"), 4f);
        Call(pm, "GrantAtLeast", Kind("Fireball"), 3f);
        Assert.That((float)Call(pm, "GetRemaining", Kind("Fireball")), Is.EqualTo(4f));
        Call(pm, "GrantAtLeast", Kind("Fireball"), 6f);
        Assert.That((float)Call(pm, "GetRemaining", Kind("Fireball")), Is.EqualTo(6f));
    }
    [Test] public void HelpingPaw_ComposesWithWideAndShrink_AndQuickClawsRetainsBaseRate()
    {
        Service((1 << 10) | (1 << 1));
        var paddle = Create("PaddleController"); paddle.transform.localScale = new Vector3(2f, 1f, 1f);
        Call(paddle, "Awake"); Call(paddle, "SetWide", true); Call(paddle, "SetShrink", true);
        Assert.That(paddle.transform.localScale.x, Is.EqualTo(1.68f).Within(.001f));
        Assert.That((float)Property(paddle, "EffectiveLaserCooldown"), Is.EqualTo(.2625f).Within(.0001f));
    }
    [Test] public void EagerPounce_ChargesTwentyPercentFasterWithoutChangingSpeed()
    {
        var service = Service(1); Game(); var upgraded = Ball();
        Call(upgraded, "Launch");
        for(int i=0;i<20;i++) Call(upgraded, "FixedUpdate");
        float upgradedCharge = (float)Property(upgraded, "RampFraction");
        float upgradedSpeed = upgraded.GetComponent<Rigidbody2D>().linearVelocity.magnitude;
        Set(service, "_activeMask", 0); var plain = Ball(); Call(plain, "Launch");
        for(int i=0;i<20;i++) Call(plain, "FixedUpdate");
        float plainCharge = (float)Property(plain, "RampFraction");
        Assert.That(plainCharge, Is.EqualTo(.015f * Time.fixedDeltaTime * 20).Within(.00001f));
        Assert.That(upgradedCharge, Is.EqualTo(plainCharge * 1.2f).Within(.00001f));
        Assert.That(upgradedSpeed, Is.EqualTo(plain.GetComponent<Rigidbody2D>().linearVelocity.magnitude).Within(.0001f));
    }
    [Test] public void StickyHoldAndPause_DoNotChargeFury()
    {
        Service(1); var gm = Game(); var ball = Ball(); Call(ball, "Launch");
        Set(ball, "_isStickyHeld", true); Call(ball, "FixedUpdate");
        Assert.That((float)Property(ball, "RampFraction"), Is.Zero);
        Set(ball, "_isStickyHeld", false); Set(gm, "_state", Enum.Parse(T("GameState"), "Paused"));
        Call(ball, "FixedUpdate"); Assert.That((float)Property(ball, "RampFraction"), Is.Zero);
    }
    [Test] public void Clone_InheritsIndependentCharge()
    {
        Service(1); Game(); var ball = Ball(); Call(ball, "Launch");
        for(int i=0;i<20;i++) Call(ball, "FixedUpdate");
        float charge = (float)Property(ball, "RampFraction");
        Call(ball, "SpawnClone", 35f);
        var cloneGO = GameObject.Find("NineLivesTest_Ball(Clone)"); objects.Add(cloneGO);
        var clone = cloneGO.GetComponent(T("BallController"));
        Assert.That((float)Property(clone, "RampFraction"), Is.EqualTo(charge).Within(.00001f));
    }
    [Test] public void RescueBounce_PreservesSpeedEffectsAndPointsUpward()
    {
        var gm = Game(); var zone = Create("DeathZone");
        var ball = Ball(); Set(gm, "_ball", ball); Set(gm, "_lives", 3); Call(ball, "SetFireball", true); Call(ball, "SetSpeedBoost", true); Call(ball, "Launch");
        var rb = ball.GetComponent<Rigidbody2D>(); float speed = rb.linearVelocity.magnitude;
        rb.linearVelocity = new Vector2(0, -speed); ball.transform.position = new Vector3(0,-12,0);
        Call(ball, "RescueBounce");
        Assert.That(rb.linearVelocity.y, Is.GreaterThan(0));
        Assert.That(rb.linearVelocity.magnitude, Is.EqualTo(speed).Within(.001f));
        Assert.That((bool)Get(ball, "_isFireball"), Is.True);
        Assert.That(ball.transform.position.y, Is.GreaterThan(-8));
        Call(zone, "OnTriggerEnter2D", ball.GetComponent<CircleCollider2D>());
        Assert.That((int)Get(gm, "_lives"), Is.EqualTo(3), "Duplicate death event immediately after rescue cannot cost a life");
    }
    Component MeteorBuild()
    {
        var service = Service(0);
        var model = Property(service, "Model");
        Set(model, "IntroGranted", true);
        Set(model, "TotalXp", 10290);
        Set(model, "PurchasedMask", 31); // The complete Pounce branch.
        Set(model, "EquippedCapstone", "A5");
        Game();
        Call(service, "BeginAttempt", "meteor-level-one", true);
        return service;
    }

    Component PowerupsSingleton()
    {
        var pm = Create("PowerupManager"); Singleton(pm); return pm;
    }

    [Test] public void Meteor_ContinueCarriesOneReturnToNextLevel()
    {
        var service = MeteorBuild(); var pm = PowerupsSingleton();
        Call(service, "NotifyFuryCompleted");
        Call(service, "ContinueToNextLevel");
        Call(service, "BeginAttempt", "meteor-level-two", true);
        var ball = Ball(); Call(ball, "Launch");
        Assert.That((float)Call(pm, "GetRemaining", Kind("Fireball")), Is.Zero, "Launch alone is not a paddle return");
        Call(service, "NotifyPaddleReturn", ball, false);
        Assert.That((float)Call(pm, "GetRemaining", Kind("Fireball")), Is.EqualTo(4f));
        Assert.That((bool)Get(ball, "_isFireball"), Is.True);
        Call(pm, "ResetAll");
        Call(service, "NotifyPaddleReturn", ball, false);
        Assert.That((float)Call(pm, "GetRemaining", Kind("Fireball")), Is.Zero, "One Fury cannot stockpile multiple Meteor returns");
    }

    [Test] public void Meteor_RestartDoesNotCarryThePreviousFuryReward()
    {
        var service = MeteorBuild(); var pm = PowerupsSingleton();
        Call(service, "NotifyFuryCompleted");
        // Restart/replay begins an attempt without the explicit Continue operation.
        Call(service, "BeginAttempt", "meteor-level-one", true);
        var ball = Ball(); Call(ball, "Launch");
        Call(service, "NotifyPaddleReturn", ball, false);
        Assert.That((float)Call(pm, "GetRemaining", Kind("Fireball")), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Meteor_LifeLossCancelsBothPendingCarryAndArmedReturn(bool afterContinuing)
    {
        var service = MeteorBuild(); var pm = PowerupsSingleton();
        Call(service, "NotifyFuryCompleted");
        if (!afterContinuing) Call(service, "NotifyLifeLost");
        Call(service, "ContinueToNextLevel");
        Call(service, "BeginAttempt", "meteor-level-two", true);
        if (afterContinuing) Call(service, "NotifyLifeLost");
        var ball = Ball(); Call(ball, "Launch");
        Call(service, "NotifyPaddleReturn", ball, false);
        Assert.That((float)Call(pm, "GetRemaining", Kind("Fireball")), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Meteor_CarryRequiresEligibleAttemptAndEquippedMeteor(bool excludedAttempt)
    {
        var service = MeteorBuild(); var pm = PowerupsSingleton();
        Call(service, "NotifyFuryCompleted");
        if (!excludedAttempt)
        {
            var model = Property(service, "Model");
            Set(model, "PurchasedMask", 1023); // Both complete Pounce and Play branches.
            Assert.That(Call(model, "EquipCapstone", "B5"), Is.True);
        }
        Call(service, "ContinueToNextLevel");
        Call(service, "BeginAttempt", "meteor-level-two", !excludedAttempt);
        var ball = Ball(); Call(ball, "Launch");
        Call(service, "NotifyPaddleReturn", ball, false);
        Assert.That((float)Call(pm, "GetRemaining", Kind("Fireball")), Is.Zero);
    }

    [Test] public void BuildChanges_StayFrozenUntilTheNextAttempt()
    {
        var service = MeteorBuild(); var model = Property(service, "Model");
        Call(model, "Respec");
        foreach (var id in new[] { "B1", "B2", "B3", "B4", "B5", "C1" })
            Assert.That(Call(model, "Purchase", id), Is.True);
        Assert.That(Call(service, "IsCapstone", "A5"), Is.True);
        Assert.That(Call(service, "IsCapstone", "B5"), Is.False);
        Assert.That(Call(service, "Has", "C1"), Is.False);
        Assert.That((float)Property(service, "FuryRateMultiplier"), Is.EqualTo(1.2f));
        Call(service, "BeginAttempt", "different-build-level", true);
        Assert.That(Call(service, "IsCapstone", "A5"), Is.False);
        Assert.That(Call(service, "IsCapstone", "B5"), Is.True);
        Assert.That(Call(service, "Has", "C1"), Is.True);
        Assert.That((float)Property(service, "FuryRateMultiplier"), Is.EqualTo(1f));
    }

    [Test] public void NinthLife_SavesOnlyFinalBallOnce()
    {
        var service = Service(1 << 14); Set(service, "_activeCapstone", "C5");
        Game(); var ball = Ball(); Call(ball, "Launch");
        var rb = ball.GetComponent<Rigidbody2D>(); rb.linearVelocity = Vector2.down * 4f;
        Assert.That(Call(service, "TryPreventBallLoss", ball), Is.True);
        Assert.That(rb.linearVelocity.y, Is.GreaterThan(0f));
        rb.linearVelocity = Vector2.down * 4f;
        Assert.That(Call(service, "TryPreventBallLoss", ball), Is.False,
            "The same attempt cannot replenish Ninth Life after its rescue is spent");
    }

    [Test] public void CloneSafety_IsConsumedBeforeNinthLifeRescue()
    {
        var service = Service((1 << 8) | (1 << 14)); Set(service, "_activeCapstone", "C5");
        Game(); var clone = Ball(); Call(clone, "Launch");
        Call(service, "RegisterCloneSafety", clone);
        Assert.That(Call(service, "TryPreventBallLoss", clone), Is.True);
        Assert.That((bool)Get(service, "rescueUsed"), Is.False,
            "A valid clone safety bounce must not spend the final-ball rescue");
        clone.GetComponent<Rigidbody2D>().linearVelocity = Vector2.down * 4f;
        Assert.That(Call(service, "TryPreventBallLoss", clone), Is.True);
        Assert.That((bool)Get(service, "rescueUsed"), Is.True);
    }

    [TestCase(0f, 10f)]
    [TestCase(15f, 15f)]
    public void CatnipParty_GrantsTwoClonesAndFireballWithoutShorteningExistingTimer(float existing, float expected)
    {
        var service = Service((1 << 9) | (1 << 5) | (1 << 7)); Set(service, "_activeCapstone", "B5");
        Game(); var pm = PowerupsSingleton(); var ball = Ball(); Call(ball, "Launch");
        if (existing > 0) Call(pm, "GrantAtLeast", Kind("Fireball"), existing);
        Call(service, "NotifyWorldPickup", Kind("WidePaddle"));
        Call(service, "NotifyWorldPickup", Kind("StickyBall"));
        Call(service, "NotifyWorldPickup", Kind("Laser"));
        Assert.That((float)Call(pm, "GetRemaining", Kind("Fireball")), Is.EqualTo(expected));
        var balls = UnityEngine.Object.FindObjectsByType(T("BallController"), FindObjectsSortMode.None);
        Assert.That(balls.Length, Is.EqualTo(3));
        foreach (var activeBall in balls) Assert.That((bool)Get(activeBall, "_isFireball"), Is.True);
        Call(service, "NotifyWorldPickup", Kind("WidePaddle"));
        Assert.That(UnityEngine.Object.FindObjectsByType(T("BallController"), FindObjectsSortMode.None).Length, Is.EqualTo(3));
    }

    [Test] public void EdgeHunter_FeedbackOnlyWhenOwnedAndBonusAwarded()
    {
        var service = Service(0); var gm = Game(); var ball = Ball(); Call(ball, "Launch");
        Call(service, "NotifyPaddleReturn", ball, true);
        Assert.That(UnityEngine.Object.FindObjectsByType(T("ScorePopup"), FindObjectsSortMode.None).Length, Is.Zero);
        Set(service, "_activeMask", 1 << 2);
        Call(service, "NotifyPaddleReturn", ball, false);
        Assert.That((float)Property(service, "AddedFuryCharge"), Is.Zero);
        Call(service, "NotifyPaddleReturn", ball, true);
        Assert.That((float)Property(service, "AddedFuryCharge"), Is.EqualTo(.03f).Within(.00001f));
        Assert.That(UnityEngine.Object.FindObjectsByType(T("ScorePopup"), FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Call(service, "NotifyPaddleReturn", ball, true);
        Assert.That((float)Property(service, "AddedFuryCharge"), Is.EqualTo(.03f).Within(.00001f));
        Assert.That(UnityEngine.Object.FindObjectsByType(T("ScorePopup"), FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Set(service, "clock", 1f); Set(gm, "_cachedFuryCharge", 1f);
        Call(service, "NotifyPaddleReturn", ball, true);
        Assert.That(UnityEngine.Object.FindObjectsByType(T("ScorePopup"), FindObjectsSortMode.None).Length, Is.EqualTo(1), "Full Fury should not claim another charge bonus");
    }

    [Test] public void CatnipParty_ThreeValidPickupsQueueExactlyOneGrantWithoutALaunchedBall()
    {
        var service = Service(1 << 9); Set(service, "_activeCapstone", "B5"); Game();
        Call(service, "NotifyWorldPickup", Kind("FlipScreen"));
        Assert.That((int)Get(service, "partyCatches"), Is.Zero, "Curses cannot advance Catnip Party");
        Call(service, "NotifyWorldPickup", Kind("WidePaddle"));
        Call(service, "NotifyWorldPickup", Kind("ExtraLife"));
        Call(service, "NotifyWorldPickup", Kind("PermanentStickyBall"));
        Assert.That((int)Get(service, "partyCatches"), Is.EqualTo(3));
        Assert.That((bool)Get(service, "partyUsed"), Is.True);
        Assert.That((bool)Get(service, "partyQueued"), Is.True,
            "Without a launched ball the generated Multiball must remain queued");
        Call(service, "NotifyWorldPickup", Kind("Laser"));
        Assert.That((int)Get(service, "partyCatches"), Is.EqualTo(3));
        Assert.That((bool)Get(service, "partyQueued"), Is.True,
            "Later pickups cannot create a second queued grant in the same attempt");
    }

}
