using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class InventoryTransactionTests
{
    [Test] public void MissingPowerupManager_DoesNotConsumeOwnedItem()
    {
        var go = new GameObject("InventoryTest"); go.SetActive(false);
        string key = "inv_1"; bool existed = PlayerPrefs.HasKey(key); int saved = PlayerPrefs.GetInt(key);
        try
        {
            var type = Type.GetType("PurrBucksManager, Assembly-CSharp", true);
            var manager = go.AddComponent(type);
            var powerup = Enum.Parse(Type.GetType("PowerupType, Assembly-CSharp", true), "MultiBall");
            var inventory = (IDictionary)type.GetField("_inventory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            inventory[powerup] = 1;
            var success = (bool)type.GetMethod("TryUseFromInventory").Invoke(manager, new[] { powerup });
            Assert.That(success, Is.False);
            Assert.That(inventory[powerup], Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            if (existed) PlayerPrefs.SetInt(key, saved); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
