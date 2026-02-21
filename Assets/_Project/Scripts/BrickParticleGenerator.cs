using UnityEngine;

/// <summary>
/// Spawns particle burst effects when bricks break.
/// Debris flies out in all directions, matching brick color.
/// </summary>
public static class BrickParticleGenerator
{
    // Cached once — Shader.Find is extremely slow and must never run per-brick
    private static Material s_material;

    private static Material GetMaterial()
    {
        if (s_material == null)
            s_material = new Material(Shader.Find("Sprites/Default"));
        return s_material;
    }

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
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, isSpecial ? 0.25f : 0.15f);
        main.startColor = color;
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

        // Renderer
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetMaterial();
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 10; // Above bricks

        // Auto-destroy after particles finish (max lifetime is 1.2s)
        Object.Destroy(go, 1.5f);
    }
}
