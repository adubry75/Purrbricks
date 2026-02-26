using UnityEngine;

/// <summary>
/// Particle-based ball trail — tiny pixel sprites that linger and wisp organically.
/// Replaces the old solid-cone TrailRenderer.
/// Emission rate and color shift with RampFraction (Fury Strike charge level).
/// </summary>
public class BallTrail : MonoBehaviour
{
    [SerializeField] private float _baseEmissionRate = 55f;
    [SerializeField] private float _maxEmissionRate  = 190f;

    private BallController _ball;
    private ParticleSystem _ps;
    private ParticleSystem.EmissionModule _emission;
    private ParticleSystem.MainModule    _main;

    private void Awake()
    {
        _ball = GetComponent<BallController>();

        // Disable the old TrailRenderer if it exists on this GO
        var tr = GetComponent<TrailRenderer>();
        if (tr != null) tr.enabled = false;

        // Get or add a ParticleSystem
        _ps = GetComponent<ParticleSystem>();
        if (_ps == null) _ps = gameObject.AddComponent<ParticleSystem>();

        SetupParticleSystem();
    }

    private void SetupParticleSystem()
    {
        _main = _ps.main;
        _main.loop             = true;
        _main.startLifetime    = new ParticleSystem.MinMaxCurve(0.10f, 0.26f);
        _main.startSpeed       = new ParticleSystem.MinMaxCurve(0.0f, 0.55f);
        _main.startSize        = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        _main.startColor       = new Color(0.9f, 1f, 1f, 0.85f);
        _main.gravityModifier  = 0f;
        _main.simulationSpace  = ParticleSystemSimulationSpace.World;
        _main.playOnAwake      = false;

        _emission = _ps.emission;
        _emission.enabled      = true;
        _emission.rateOverTime = _baseEmissionRate;

        // Tiny sphere shape — sparks scatter just a few pixels around ball center
        var shape = _ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.07f;

        // Color over lifetime: white-cyan → blue → transparent
        var col = _ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1.0f, 1.0f, 1.0f), 0.00f),
                new GradientColorKey(new Color(0.5f, 0.85f, 1.0f), 0.45f),
                new GradientColorKey(new Color(0.2f, 0.45f, 0.9f), 1.00f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.90f, 0.00f),
                new GradientAlphaKey(0.55f, 0.45f),
                new GradientAlphaKey(0.00f, 1.00f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Shrink as particles age — tiny wisps vanish naturally
        var size = _ps.sizeOverLifetime;
        size.enabled = true;
        size.size    = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0.05f));

        // Noise module gives the "wispy" organic drift
        var noise = _ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.35f;
        noise.frequency   = 1.4f;
        noise.scrollSpeed = 0.6f;
        noise.quality     = ParticleSystemNoiseQuality.Low;

        // Additive blending for a glowing look (also assigns a soft particle texture).
        var rend = _ps.GetComponent<ParticleSystemRenderer>();
        rend.material         = VfxMaterials.Additive;
        rend.sortingLayerName = "Default";
        rend.sortingOrder     = -1; // behind ball sprite

        _ps.Stop();
    }

    private void Update()
    {
        if (_ball == null) return;

        bool launched = _ball.IsLaunched();

        if (launched && !_ps.isPlaying)
            _ps.Play();
        else if (!launched && _ps.isPlaying)
            _ps.Stop();

        if (!launched) return;

        float ramp = _ball.RampFraction;

        // Scale emission with ramp
        _emission.rateOverTime = Mathf.Lerp(_baseEmissionRate, _maxEmissionRate, ramp);

        // Shift start color from white-cyan toward gold as fury builds
        if (ramp > 0.4f)
        {
            float t = (ramp - 0.4f) / 0.6f; // 0→1 across upper 60% of ramp
            _main.startColor = Color.Lerp(
                new Color(0.90f, 1.00f, 1.00f, 0.85f),
                new Color(1.00f, 0.82f, 0.20f, 0.95f),
                t
            );
        }
        else
        {
            _main.startColor = new Color(0.90f, 1.00f, 1.00f, 0.85f);
        }
    }
}
