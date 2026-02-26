using UnityEngine;

/// <summary>
/// Multi-layer starfield parallax background.
/// Creates 3 layers of drifting stars at different depths for parallax effect.
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [SerializeField] private int _layerCount = 4;
    [SerializeField] private int _starsPerLayer = 80;
    [SerializeField] private Color _starColor = new Color(0.9f, 0.95f, 1f, 1f);

    private void Start()
    {
        CreateStarfield();
        SetCameraBackground();
    }

    private void CreateStarfield()
    {
        for (int i = 0; i < _layerCount; i++)
        {
            // Depth and speed: farther = slower
            float layerDepth = (i + 1) * 3f;
            float scrollSpeed = 2.15f / (i + 1f);
            float starSize = 0.020f + (i * 0.008f);
            float brightness = 1f - (i * 0.25f);

            CreateStarLayer(i, layerDepth, scrollSpeed, starSize, brightness);
        }
    }

    private void CreateStarLayer(int index, float zDepth, float scrollSpeed, float starSize, float brightness)
    {
        var go = new GameObject($"StarLayer_{index}");
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(0f, 0f, zDepth);

        var ps = go.AddComponent<ParticleSystem>();

        float viewW = 18f, viewH = 12f;
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            viewH = cam.orthographicSize * 2f;
            viewW = viewH * cam.aspect;
        }

        // Main module
        var main = ps.main;
        float travel = Mathf.Max(6f, viewW * 1.6f);
        float life   = Mathf.Clamp(travel / Mathf.Max(0.01f, scrollSpeed), 8f, 48f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.75f, life * 1.05f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(starSize * 0.7f, starSize * 1.25f);
        main.startColor = _starColor * brightness;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = _starsPerLayer;
        main.loop = true;
        main.prewarm = true;
        main.useUnscaledTime = true; // keep drifting during pause/slow-mo/UI

        // Emission: steady rate
        var emission = ps.emission;
        emission.rateOverTime = Mathf.Max(1f, _starsPerLayer / Mathf.Max(1f, life * 0.80f));

        // Shape: spawn across a wide box covering the screen
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(viewW * 1.6f, viewH * 1.6f, 0.1f);

        // Velocity: slow drift left + slight upward
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-scrollSpeed * 1.12f, -scrollSpeed * 0.88f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(scrollSpeed * 0.10f, scrollSpeed * 0.28f);

        // Color twinkle (subtle alpha variation)
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(1f, 0.3f),
                new GradientAlphaKey(0.7f, 0.7f),
                new GradientAlphaKey(0.9f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        // Renderer
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = VfxMaterials.Additive;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = -200 - index; // Far behind everything
    }

    private void SetCameraBackground()
    {
        // Deep blue-purple gradient (camera will show this behind stars)
        var cam = Camera.main;
        if (cam != null)
        {
            cam.backgroundColor = new Color(0.03f, 0.02f, 0.10f); // very dark blue
        }
    }
}
