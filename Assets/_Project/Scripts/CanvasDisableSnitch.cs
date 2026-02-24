using UnityEngine;
using System.Diagnostics;

public class CanvasDisableSnitch : MonoBehaviour
{
    private Canvas _canvas;
    private bool _lastEnabled;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        if (_canvas == null)
        {
            UnityEngine.Debug.LogError("[CanvasDisableSnitch] No Canvas found on this GameObject.");
            enabled = false;
            return;
        }

        _lastEnabled = _canvas.enabled;
        UnityEngine.Debug.Log($"[CanvasDisableSnitch] Awake. Canvas enabled = {_canvas.enabled} on {gameObject.name}");
    }

    void Update()
    {
        if (_canvas.enabled != _lastEnabled)
        {
            _lastEnabled = _canvas.enabled;

            if (_canvas.enabled == false)
            {
                var st = new StackTrace(true);
                UnityEngine.Debug.LogError(
                    "[CanvasDisableSnitch] Canvas was DISABLED on: " + gameObject.name + "\n" + st
                );

                UnityEngine.Debug.Break(); // pauses editor right when it happens
            }
            else
            {
                UnityEngine.Debug.Log("[CanvasDisableSnitch] Canvas was ENABLED on: " + gameObject.name);
            }
        }
    }
}
