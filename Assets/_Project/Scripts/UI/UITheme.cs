using UnityEngine;

/// <summary>
/// Scene singleton for UI theming assets (so UIStyle can use shared sprites without per-screen wiring).
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class UITheme : MonoBehaviour
{
    public static UITheme Instance { get; private set; }

    [Header("Buttons")]
    [SerializeField] private Sprite _buttonTemplate;

    public Sprite ButtonTemplate => _buttonTemplate;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
