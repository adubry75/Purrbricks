using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ControlComfortTests
{
    private GameObject _go;
    private object _settings;
    private Type _type;
    private readonly string[] _keys = { "Set_ResW", "Set_MusicVol", "Set_SfxVol", "Set_Display", "Set_MouseRelative", "Set_MouseSensitivity", "Set_GamepadSensitivity", "Set_GamepadDeadzone", "Set_ReducedEffects" };
    private readonly System.Collections.Generic.Dictionary<string, (bool exists, string value)> _saved = new System.Collections.Generic.Dictionary<string, (bool, string)>();
    [SetUp] public void Setup()
    {
        foreach (var key in _keys)
        {
            bool integer = key == "Set_ResW" || key == "Set_Display" || key == "Set_MouseRelative" || key == "Set_ReducedEffects";
            _saved[key] = (PlayerPrefs.HasKey(key), integer ? PlayerPrefs.GetInt(key).ToString() : PlayerPrefs.GetFloat(key).ToString(System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.DeleteKey(key);
        }
        _type = Type.GetType("SettingsManager, Assembly-CSharp", true);
        _go = new GameObject("Settings test"); _go.SetActive(false);
        _settings = _go.AddComponent(_type);
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(_go);
        foreach (var key in _keys)
        {
            var saved = _saved[key];
            if (!saved.exists) PlayerPrefs.DeleteKey(key);
            else if (key == "Set_ResW" || key == "Set_Display" || key == "Set_MouseRelative" || key == "Set_ReducedEffects") PlayerPrefs.SetInt(key, int.Parse(saved.value));
            else PlayerPrefs.SetFloat(key, float.Parse(saved.value, System.Globalization.CultureInfo.InvariantCulture));
        }
    }
    [Test] public void InvalidSavedComfortValuesAreNormalized()
    {
        PlayerPrefs.SetFloat("Set_MouseSensitivity", float.NaN);
        PlayerPrefs.SetFloat("Set_GamepadSensitivity", 8f);
        PlayerPrefs.SetFloat("Set_GamepadDeadzone", -0.1f);
        Call("LoadSettings");
        Assert.That(Value("MouseSensitivity"), Is.EqualTo(1f));
        Assert.That(Value("GamepadSensitivity"), Is.EqualTo(2f));
        Assert.That(Value("GamepadDeadzone"), Is.EqualTo(0.05f));
    }
    private void Call(string method, params object[] args)
    {
        var m = _type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(m, Is.Not.Null, method + " must exist"); m.Invoke(_settings, args);
    }
    private object Value(string property)
    {
        var p = _type.GetProperty(property); Assert.That(p, Is.Not.Null, property + " must exist"); return p.GetValue(_settings);
    }
    [Test] public void FreshInstallUsesBorderlessAndAbsoluteMouse()
    {
        Call("LoadSettings"); Assert.That(Value("DisplayModeIndex"), Is.EqualTo(1));
        Assert.That(Value("MouseRelative"), Is.False); Assert.That(Value("MouseSensitivity"), Is.EqualTo(1f));
    }
    [Test] public void SavedFullscreenChoiceIsPreserved()
    {
        PlayerPrefs.SetInt("Set_Display", 0); Call("LoadSettings"); Assert.That(Value("DisplayModeIndex"), Is.EqualTo(0));
    }
    [Test] public void ComfortPreferencesRoundTripWithBounds()
    {
        Call("SetMouseRelative", true); Call("SetMouseSensitivity", 9f); Call("SetGamepadSensitivity", -1f);
        Call("SetGamepadDeadzone", 0.3f); Call("SetReducedEffects", true); Call("SaveSettings");
        Call("SetMouseRelative", false); Call("SetReducedEffects", false); Call("LoadSettings");
        Assert.That(Value("MouseRelative"), Is.True); Assert.That(Value("ReducedEffects"), Is.True);
        Assert.That(Value("MouseSensitivity"), Is.EqualTo(3f)); Assert.That(Value("GamepadSensitivity"), Is.EqualTo(0.25f));
        Assert.That(Value("GamepadDeadzone"), Is.EqualTo(0.3f).Within(0.001f));
    }
    [TestCase(-6f, 6f)] [TestCase(2f, -2f)]
    public void FlippedMouseReflectsAcrossShiftedUsableBounds(float input, float expected)
    {
        // The usable bounds have midpoint 1, so reflection maps -6 to 8 and 2 to 0.
        var method = Type.GetType("PaddleController, Assembly-CSharp", true).GetMethod("MapAbsoluteMouse", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        Assert.That((float)method.Invoke(null, new object[] { input, -8f, 10f, true, 1f }), Is.EqualTo(expected + 2f).Within(0.001f));
    }
    [TestCase(0.1f, 0f)] [TestCase(0.2f, 0f)] [TestCase(1f, 1f)] [TestCase(-1f, -1f)] [TestCase(0.6f, 0.5f)]
    public void ConfiguredDeadzoneRejectsDriftAndPreservesFullTravel(float input, float expected)
    {
        var method = Type.GetType("PaddleController, Assembly-CSharp", true).GetMethod("ApplyDeadzone", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        Assert.That((float)method.Invoke(null, new object[] { input, 0.2f }), Is.EqualTo(expected).Within(0.001f));
    }
}


