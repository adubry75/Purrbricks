using UnityEngine;

/// <summary>
/// Glowing trail effect behind the ball.
/// Auto-configures a TrailRenderer on Awake.
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class BallTrail : MonoBehaviour
{
    private TrailRenderer _trail;

    private void Awake()
    {
        _trail = GetComponent<TrailRenderer>();
        SetupTrail();
    }

    private void SetupTrail()
    {
        // Trail duration and width
        _trail.time = 0.25f;
        _trail.startWidth = 0.2f;
        _trail.endWidth = 0.02f;
        _trail.minVertexDistance = 0.02f;

        // Material with additive blending for glow effect
        _trail.material = new Material(Shader.Find("Sprites/Default"));
        _trail.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _trail.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive

        // Gradient: bright cyan-white fading to transparent
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.8f, 1f, 1f), 0f),   // bright cyan-white
                new GradientColorKey(new Color(0.2f, 0.6f, 1f), 1f)  // blue
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        _trail.colorGradient = gradient;

        // Sorting
        _trail.sortingLayerName = "Default";
        _trail.sortingOrder = -1; // Behind ball
    }
}
