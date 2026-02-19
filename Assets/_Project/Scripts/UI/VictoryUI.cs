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
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Background panel (semi-transparent) - offset left to center on playfield
        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.6f);

        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.sizeDelta = Vector2.zero;
        // Shift left by 1/12th of screen width (half of 1/6th reserved area)
        panelRt.anchoredPosition = new Vector2(-160f, 0f);

        // "LEVEL COMPLETE!" title
        CreateText(panel, "LEVEL COMPLETE!", new Vector2(0f, 150f), 90, new Color(0.2f, 1f, 0.4f));

        // Score display
        CreateText(panel, "Score: 0", new Vector2(0f, 50f), 50, Color.white, "ScoreText");

        // Next Level button
        CreateButton(panel, "Next Level", new Vector2(0f, -100f), OnNextLevel);
    }

    private void CreateText(GameObject parent, string text, Vector2 pos, int fontSize, Color color, string objName = null)
    {
        var go = new GameObject(objName ?? text);
        go.transform.SetParent(parent.transform, false);

        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1000f, fontSize + 30f);
        rt.anchoredPosition = pos;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(4f, -4f);
    }

    private void CreateButton(GameObject parent, string label, Vector2 pos, System.Action onClick)
    {
        var btnGO = new GameObject(label + "Button");
        btnGO.transform.SetParent(parent.transform, false);

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = Color.white; // Base color for button color tinting

        var button = btnGO.AddComponent<Button>();
        button.targetGraphic = btnImg;

        var colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.8f, 0.3f); // green
        colors.highlightedColor = new Color(0.3f, 1f, 0.4f);
        colors.pressedColor = new Color(0.1f, 0.6f, 0.2f);
        button.colors = colors;

        button.onClick.AddListener(() => onClick?.Invoke());

        var btnRt = btnGO.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.sizeDelta = new Vector2(300f, 80f);
        btnRt.anchoredPosition = pos;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);

        var btnText = textGO.AddComponent<Text>();
        btnText.text = label;
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 36;
        btnText.fontStyle = FontStyle.Bold;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;

        var textRt = btnText.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
    }

    public void ShowVictory(int currentScore)
    {
        gameObject.SetActive(true);

        // Update score text
        var scoreText = transform.Find("Panel/ScoreText")?.GetComponent<Text>();
        if (scoreText != null)
            scoreText.text = $"Score: {currentScore:N0}";

        // Spawn confetti celebration
        SpawnConfetti();
    }

    private void SpawnConfetti()
    {
        // Rainbow confetti particles shooting upward from center of playfield
        for (int i = 0; i < 3; i++)
        {
            float xPos = -3f + i * 3f;
            SpawnConfettiEmitter(new Vector3(xPos, -2f, 0f));
        }
    }

    private void SpawnConfettiEmitter(Vector3 position)
    {
        var go = new GameObject("Confetti");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
        main.gravityModifier = 0.5f;
        main.loop = false;
        main.useUnscaledTime = true; // Animate even when Time.timeScale = 0

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 80) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.2f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.2f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 1f, 0.2f), 0.33f),
                new GradientColorKey(new Color(0.2f, 1f, 0.2f), 0.66f),
                new GradientColorKey(new Color(0.2f, 0.4f, 1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingOrder = 250;

        Destroy(go, 4f);
    }

    private void OnNextLevel()
    {
        GameManager.Instance?.LoadNextLevel();
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
}
