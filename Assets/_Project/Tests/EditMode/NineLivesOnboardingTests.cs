using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class NineLivesOnboardingTests
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const string SaveKey = "NineLives_Save_v1";
    const string BackupKey = "NineLives_Save_v1_backup";
    GameObject serviceObject;
    bool hadSave, hadBackup;
    string originalSave, originalBackup;

    Type ServiceType => Type.GetType("NineLivesService, Assembly-CSharp");

    object CreateService()
    {
        serviceObject = new GameObject("NineLivesOnboardingTest");
        serviceObject.SetActive(false);
        return serviceObject.AddComponent(ServiceType);
    }

    object SavedData(object service)
    {
        return ServiceType.GetField("save", Fields).GetValue(service);
    }

    [SetUp]
    public void SetUp()
    {
        hadSave = PlayerPrefs.HasKey(SaveKey);
        hadBackup = PlayerPrefs.HasKey(BackupKey);
        originalSave = hadSave ? PlayerPrefs.GetString(SaveKey) : null;
        originalBackup = hadBackup ? PlayerPrefs.GetString(BackupKey) : null;
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.DeleteKey(BackupKey);
    }

    [TearDown]
    public void TearDown()
    {
        if (serviceObject != null) UnityEngine.Object.DestroyImmediate(serviceObject);
        if (hadSave) PlayerPrefs.SetString(SaveKey, originalSave);
        else PlayerPrefs.DeleteKey(SaveKey);
        if (hadBackup) PlayerPrefs.SetString(BackupKey, originalBackup);
        else PlayerPrefs.DeleteKey(BackupKey);
        PlayerPrefs.Save();
    }

    [Test]
    public void CampaignIntroductionUsesStableLevelIdWhenDisplayOrderChanges()
    {
        object service = CreateService();
        ServiceType.GetMethod("InitializeCampaign").Invoke(service, new object[] { new[] { "fortress", "diamond_rain" } });

        object data = SavedData(service);
        string introId = (string)data.GetType().GetField("IntroLevelId", Fields).GetValue(data);
        Assert.That(introId, Is.EqualTo("diamond_rain"));
    }

    [Test]
    public void PendingMigrationWelcomeSurvivesReloadUntilDismissed()
    {
        PlayerPrefs.SetString(SaveKey,
            "{\"Model\":{\"Version\":1,\"IntroGranted\":true,\"TotalXp\":240,\"PurchasedMask\":0," +
            "\"EquippedCapstone\":\"\",\"StarterPowerup\":0,\"MigrationCompleted\":true,\"CreditedStars\":{}}," +
            "\"IntroLevelId\":\"diamond_rain\",\"IntroductionSeen\":true," +
            "\"MigrationWelcomePending\":true,\"MigrationWelcomePoints\":2}");

        object service = CreateService();
        ServiceType.GetMethod("Load", Fields).Invoke(service, null);
        ServiceType.GetMethod("InitializeCampaign").Invoke(service, new object[] { new[] { "diamond_rain", "fortress" } });

        PropertyInfo pending = ServiceType.GetProperty("HasPendingWelcome", Fields);
        Assert.That(pending, Is.Not.Null, "Persisted migration welcome state must be exposed to the menu trigger");
        Assert.That((bool)pending.GetValue(service), Is.True);
        Assert.That((string)ServiceType.GetProperty("WelcomeMessage", Fields).GetValue(service), Does.Contain("2 skill points"));
    }
    const string ValidRecoverySave = "{\"Model\":{\"Version\":1,\"IntroGranted\":true,\"TotalXp\":240,\"PurchasedMask\":1,\"MigrationCompleted\":true},\"IntroLevelId\":\"diamond_rain\"}";

    [Test]
    public void MissingModelPrimaryFallsBackToValidBackup()
    {
        PlayerPrefs.SetString(SaveKey, "{}");
        PlayerPrefs.SetString(BackupKey, ValidRecoverySave);
        object service = CreateService();
        ServiceType.GetMethod("Load", Fields).Invoke(service, null);
        var model = (NineLivesModel)ServiceType.GetProperty("Model").GetValue(service);
        Assert.That(model.TotalXp, Is.EqualTo(240));
        Assert.That(model.Has("A1"), Is.True);
    }

    [Test]
    public void FlushAfterMalformedPrimaryRecoveryPreservesValidBackup()
    {
        PlayerPrefs.SetString(SaveKey, "{broken-json");
        PlayerPrefs.SetString(BackupKey, ValidRecoverySave);
        object service = CreateService();
        ServiceType.GetMethod("Load", Fields).Invoke(service, null);
        var model = (NineLivesModel)ServiceType.GetProperty("Model").GetValue(service);
        Assert.That(model.TotalXp, Is.EqualTo(240));
        model.AddXp(30);
        ServiceType.GetMethod("Save", Fields).Invoke(service, new object[] { true });
        Assert.That(PlayerPrefs.GetString(BackupKey), Is.EqualTo(ValidRecoverySave));
        Assert.That(PlayerPrefs.GetString(SaveKey), Does.Contain("\"TotalXp\":270"));
    }
}
