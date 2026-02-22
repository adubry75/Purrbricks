using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Victory screen: level complete, score display, confetti, Next Level button.
/// </summary>
public class VictoryUI : MonoBehaviour
{
    private Canvas _canvas;

    private void Awake()
    {
        BuildUI();
        Hide();
    }

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.65f);

        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin       = Vector2.zero;
        panelRt.anchorMax       = Vector2.one;
        panelRt.sizeDelta       = Vector2.zero;
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        CreateText(panel, "LEVEL COMPLETE!", new Vector2(0f,  190f), 90, new Color(0.20f, 1f, 0.45f));
        CreateText(panel, "Level Score: 0",  new Vector2(0f,   95f), 52, Color.white,                    "LevelScoreText");
        CreateText(panel, "Combo Bonus: 0",  new Vector2(0f,   20f), 38, new Color(1f, 0.85f, 0.15f),   "ComboBonusText");
        CreateText(panel, "Best Combo: ×0",  new Vector2(0f,  -45f), 38, new Color(0.45f, 0.85f, 1f),   "BestComboText");

        UIStyle.CreateButton(panel.transform, "Next Level", new Vector2(0f, -145f), new Vector2(320f, 80f), OnNextLevel, UIStyle.AccentGreen);
    }

    private void CreateText(GameObject parent, string text, Vector2 pos, int fontSize, Color color, string objName = null)
    {
        var go = new GameObject(objName ?? text);
        go.transform.SetParent(parent.transform, false);

        var txt = go.AddComponent<Text>();
        txt.text      = text;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = color;

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.sizeDelta       = new Vector2(1000f, fontSize + 30f);
        rt.anchoredPosition = pos;

        var ol = go.AddComponent<Outline>();
        ol.effectColor    = Color.black;
        ol.effectDistance = new Vector2(4f, -4f);
    }

    public void ShowVictory(int levelScore, int comboBonus, int bestCombo)
    {
        gameObject.SetActive(true);

        var t1 = transform.Find("Panel/LevelScoreText")?.GetComponent<Text>();
        if (t1 != null) t1.text = $"Level Score:  {levelScore:N0}";

        var t2 = transform.Find("Panel/ComboBonusText")?.GetComponent<Text>();
        if (t2 != null) t2.text = comboBonus > 0
            ? $"Combo Bonus:  +{comboBonus:N0}"
            : "Combo Bonus:  —";

        var t3 = transform.Find("Panel/BestComboText")?.GetComponent<Text>();
        if (t3 != null) t3.text = bestCombo > 0
            ? $"Best Combo:  ×{bestCombo + 1}"
            : "Best Combo:  —";

        SpawnConfetti();
    }

    private void SpawnConfetti()
    {
        for (int i = 0; i < 3; i++)
            SpawnConfettiEmitter(new Vector3(-3f + i * 3f, -2f, 0f));
    }

    private void SpawnConfettiEmitter(Vector3 position)
    {
        var go = new GameObject("Confetti");
        go.transform.position = position;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(8f, 12f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.10f, 0.25f);
        main.startColor      = new ParticleSystem.MinMaxGradient(Color.white);
        main.gravityModifier = 0.5f;
        main.loop            = false;
        main.useUnscaledTime = true;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 80) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 15f;
        shape.radius    = 0.2f;

        var col     = ps.colorOverLifetime;
        col.enabled = true;
        var grad    = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.2f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 1f, 0.2f), 0.33f),
                new GradientColorKey(new Color(0.2f, 1f, 0.2f), 0.66f),
                new GradientColorKey(new Color(0.2f, 0.5f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer       = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material  = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingOrder = 250;

        Destroy(go, 4f);
    }

    private void OnNextLevel() => GameManager.Instance?.LoadNextLevel();

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
}
