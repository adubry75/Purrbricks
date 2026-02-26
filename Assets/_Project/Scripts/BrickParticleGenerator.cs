using UnityEngine;

/// <summary>
/// Spawns particle burst effects when bricks break.
/// Debris flies out in all directions, matching brick color.
/// </summary>
public static class BrickParticleGenerator
{
    /// <summary>
    /// Creates a one-shot particle explosion at the given position.
    /// </summary>
    public static void SpawnBurst(Vector3 position, Color color, int particleCount = 20, bool isSpecial = false)
    {
        var go = new GameObject("BrickParticles");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();

        // Main module
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, isSpecial ? 7f : 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, isSpecial ? 0.18f : 0.12f);
        main.startColor = color;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = false;

        // Emission: single burst
        var emission = ps.emission;
        emission.enabled = true;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, (short)particleCount)
        });

        // Shape: emit in all directions from a small sphere
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        // Organic variation (prevents "perfect radial" look).
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = isSpecial ? 0.55f : 0.35f;
        noise.frequency   = 1.6f;
        noise.scrollSpeed = 0.8f;
        noise.quality     = ParticleSystemNoiseQuality.Low;

        // Color fade over lifetime
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color * 0.4f, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        // Size shrink over lifetime
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        // Streaks on heavier/special breaks
        var trails = ps.trails;
        trails.enabled              = isSpecial;
        trails.mode                 = ParticleSystemTrailMode.PerParticle;
        trails.ratio                = 1f;
        trails.lifetime             = 0.20f;
        trails.minVertexDistance    = 0.04f;
        trails.dieWithParticles     = true;
        trails.inheritParticleColor = true;
        trails.widthOverTrail       = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 0f));

        // Renderer
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = VfxMaterials.Additive;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 10; // Above bricks
        renderer.trailMaterial = VfxMaterials.Additive;

        // Auto-destroy after particles finish (max lifetime is 1.2s)
        Object.Destroy(go, 1.5f);
    }
}
