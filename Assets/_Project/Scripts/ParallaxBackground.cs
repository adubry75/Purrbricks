using UnityEngine;

/// <summary>
/// Multi-layer starfield parallax background.
/// Creates 3 layers of drifting stars at different depths for parallax effect.
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [Header("Image Parallax (preferred)")]
    [SerializeField] private Sprite _starsFarSprite;
    [SerializeField] private Sprite _nebulaMidSprite;
    [SerializeField] private Sprite _dustNearSprite;

    [SerializeField, Range(0f, 1f)] private float _starsAlpha = 0.85f;
    [SerializeField, Range(0f, 1f)] private float _nebulaAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _dustAlpha   = 0.25f;

    [SerializeField] private float _starsSpeedX  = 0.12f;
    [SerializeField] private float _nebulaSpeedX = 0.28f;
    [SerializeField] private float _dustSpeedX   = 0.55f;
    [SerializeField] private float _driftY       = 0.02f;

    [Header("Fallback Particle Starfield")]
    [SerializeField] private int _layerCount = 4;
    [SerializeField] private int _starsPerLayer = 80;
    [SerializeField] private Color _starColor = new Color(0.9f, 0.95f, 1f, 1f);

    private sealed class ScrollingLayer
    {
        public Transform a;
        public Transform b;
        public float width;
        public Vector2 vel;

        public void Tick(float dt)
        {
            if (a == null || b == null) return;

            Vector3 dv = new Vector3(vel.x, vel.y, 0f) * dt;
            a.position += dv;
            b.position += dv;

            // Wrap on X (leftward scroll). Keep them stitched.
            if (a.position.x <= -width)
                a.position = new Vector3(b.position.x + width, a.position.y, a.position.z);
            if (b.position.x <= -width)
                b.position = new Vector3(a.position.x + width, b.position.y, b.position.z);
        }
    }

    private ScrollingLayer[] _imgLayers;

    private void Start()
    {
        if (!TryCreateImageParallax())
            CreateStarfield();
        SetCameraBackground();
    }

    private void Update()
    {
        if (_imgLayers == null || _imgLayers.Length == 0) return;
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < _imgLayers.Length; i++)
            _imgLayers[i]?.Tick(dt);
    }

    private bool TryCreateImageParallax()
    {
        // Use image parallax when at least one layer sprite is assigned.
        if (_starsFarSprite == null && _nebulaMidSprite == null && _dustNearSprite == null)
            return false;

        var cam = Camera.main;
        if (cam == null || !cam.orthographic)
            return false;

        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;

        // Order: far -> near (more negative order = farther back)
        var layers = new System.Collections.Generic.List<ScrollingLayer>(3);
        int order = -300;

        if (_starsFarSprite != null)
            layers.Add(CreateSpriteLayer("StarsFar", _starsFarSprite, alpha: _starsAlpha, new Vector2(-_starsSpeedX, _driftY * 0.15f), z: 20f, order: order += 1, viewH, viewW));
        if (_nebulaMidSprite != null)
            layers.Add(CreateSpriteLayer("NebulaMid", _nebulaMidSprite, alpha: _nebulaAlpha, new Vector2(-_nebulaSpeedX, _driftY * 0.35f), z: 15f, order: order += 1, viewH, viewW));
        if (_dustNearSprite != null)
            layers.Add(CreateSpriteLayer("DustNear", _dustNearSprite, alpha: _dustAlpha, new Vector2(-_dustSpeedX, _driftY * 0.60f), z: 10f, order: order += 1, viewH, viewW));

        _imgLayers = layers.ToArray();
        return _imgLayers.Length > 0;
    }

    private ScrollingLayer CreateSpriteLayer(
        string name,
        Sprite sprite,
        float alpha,
        Vector2 velocity,
        float z,
        int order,
        float viewH,
        float viewW)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, worldPositionStays: false);
        root.transform.localPosition = new Vector3(0f, 0f, z);

        var a = CreateSpriteTile(root.transform, sprite, alpha, order, "A");
        var b = CreateSpriteTile(root.transform, sprite, alpha, order, "B");

        // Scale to comfortably cover the view.
        float baseH = Mathf.Max(0.0001f, sprite.bounds.size.y);
        float desiredH = viewH * 1.35f;
        float scale = desiredH / baseH;
        a.localScale = b.localScale = Vector3.one * scale;

        float width = Mathf.Max(0.01f, sprite.bounds.size.x * scale);

        // Centered tiles stitched horizontally.
        a.position = new Vector3(0f, 0f, z);
        b.position = new Vector3(width, 0f, z);

        // If the sprite is very narrow, tile more aggressively by increasing speed wrap width.
        // (Two tiles still works; it'll just repeat more often.)

        return new ScrollingLayer
        {
            a = a,
            b = b,
            width = width,
            vel = velocity
        };
    }

    private static Transform CreateSpriteTile(Transform parent, Sprite sprite, float alpha, int order, string suffix)
    {
        var go = new GameObject("Tile_" + suffix);
        go.transform.SetParent(parent, worldPositionStays: true);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = order;
        sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

        return go.transform;
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
        // Keep curve mode consistent across X/Y/Z to avoid Unity runtime errors.
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

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
