using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GhostRevivalTests
{
    [Test] public void LifeLossOnSameLevel_DoesNotCancelGhostRevival()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var levelType = Type.GetType("LevelManager, Assembly-CSharp", true);
        var gmType = Type.GetType("GameManager, Assembly-CSharp", true);
        var ghostType = Type.GetType("GhostBrick, Assembly-CSharp", true);
        var levelGO = new GameObject("GhostLifeLevelTest"); levelGO.SetActive(false);
        var gmGO = new GameObject("GhostLifeGameTest"); gmGO.SetActive(false);
        var brickGO = new GameObject("GhostLifeBrickTest"); brickGO.SetActive(false);
        var instanceField = gmType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var previousGM = instanceField.GetValue(null);
        bool previousCursor = Cursor.visible;
        var previousLock = Cursor.lockState;
        float previousTimeScale = Time.timeScale;
        try
        {
            var level = levelGO.AddComponent(levelType);
            levelType.GetMethod("Awake", flags).Invoke(level, null);
            levelType.GetMethod("BeginLevel").Invoke(level, new object[] { 1 });
            var gm = gmGO.AddComponent(gmType);
            instanceField.SetValue(null, gm);
            gmType.GetField("_state", flags).SetValue(gm, Enum.Parse(Type.GetType("GameState, Assembly-CSharp"), "Playing"));
            var collider = brickGO.AddComponent<BoxCollider2D>();
            var ghost = brickGO.AddComponent(ghostType);
            ghostType.GetMethod("Awake", flags).Invoke(ghost, null);
            ghostType.GetField("_fadeInSeconds", flags).SetValue(ghost, 0f);
            var routine = (IEnumerator)ghostType.GetMethod("ReviveRoutine", flags).Invoke(ghost, null);
            Assert.That(routine.MoveNext(), Is.True);
            // A lost ball returns Playing to Ready without replacing the level.
            gmType.GetMethod("SetState").Invoke(gm, new[] { Enum.Parse(Type.GetType("GameState, Assembly-CSharp"), "Ready") });
            routine.MoveNext();
            Assert.That(collider.enabled, Is.True, "Life loss must not permanently remove this level's ghost");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(brickGO);
            UnityEngine.Object.DestroyImmediate(levelGO);
            UnityEngine.Object.DestroyImmediate(gmGO);
            instanceField.SetValue(null, previousGM);
            Cursor.visible = previousCursor;
            Cursor.lockState = previousLock;
            Time.timeScale = previousTimeScale;
            levelType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }
    }

    [Test] public void ClearingLevelDuringGhostFade_DoesNotRestoreCollision()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var levelType = Type.GetType("LevelManager, Assembly-CSharp", true);
        var ghostType = Type.GetType("GhostBrick, Assembly-CSharp", true);
        var levelGO = new GameObject("GhostLevelTest"); levelGO.SetActive(false);
        var brickGO = new GameObject("GhostBrickTest"); brickGO.SetActive(false);
        try
        {
            var level = levelGO.AddComponent(levelType);
            levelType.GetMethod("Awake", flags).Invoke(level, null);
            levelType.GetMethod("BeginLevel").Invoke(level, new object[] { 1 });
            var collider = brickGO.AddComponent<BoxCollider2D>();
            var ghost = brickGO.AddComponent(ghostType);
            ghostType.GetMethod("Awake", flags).Invoke(ghost, null);
            ghostType.GetField("_fadeInSeconds", flags).SetValue(ghost, 100f);
            var routine = (IEnumerator)ghostType.GetMethod("ReviveRoutine", flags).Invoke(ghost, null);
            Assert.That(routine.MoveNext(), Is.True); // initial delay
            Assert.That(routine.MoveNext(), Is.True); // fading while one brick remains
            levelType.GetMethod("BeginLevel").Invoke(level, new object[] { 0 });
            ghostType.GetField("_fadeInSeconds", flags).SetValue(ghost, 0f);
            routine.MoveNext();
            Assert.That(collider.enabled, Is.False, "A ghost cannot revive after the remaining brick clears the board");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(brickGO);
            UnityEngine.Object.DestroyImmediate(levelGO);
            levelType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }
    }
}
