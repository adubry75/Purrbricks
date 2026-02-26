using UnityEngine;

/// <summary>
/// Shared, runtime-safe VFX materials + tiny procedural textures so particles don't render as white squares
/// when no imported sprite/texture is assigned.
/// </summary>
public static class VfxMaterials
{
    private static Texture2D s_softCircleTex;
    private static Material  s_additive;
    private static Material  s_alpha;

    public static Material Additive
    {
        get
        {
            if (s_additive != null) return s_additive;
            s_additive = CreateSpriteMaterial(additive: true);
            return s_additive;
        }
    }

    public static Material Alpha
    {
        get
        {
            if (s_alpha != null) return s_alpha;
            s_alpha = CreateSpriteMaterial(additive: false);
            return s_alpha;
        }
    }

    private static Material CreateSpriteMaterial(bool additive)
    {
        var shader = Shader.Find("Sprites/Default");
        var mat    = new Material(shader);
        mat.mainTexture = GetSoftCircleTex();

        // The built-in Sprites shader supports these blend overrides (used elsewhere in this project).
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        return mat;
    }

    private static Texture2D GetSoftCircleTex()
    {
        if (s_softCircleTex != null) return s_softCircleTex;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false, linear: true);
        tex.name       = "VFX_SoftCircle_64";
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        float inv  = 1f / half;

        // Soft circle with a slightly brighter core.
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - half) * inv;
            float dy = (y - half) * inv;
            float r  = Mathf.Sqrt(dx * dx + dy * dy);

            // Core -> edge falloff (smoothstep-ish).
            float edge = Mathf.Clamp01(1f - Mathf.InverseLerp(0.32f, 1.00f, r));
            float core = Mathf.Clamp01(1f - Mathf.InverseLerp(0.00f, 0.25f, r));
            float a    = Mathf.Clamp01(edge * 0.85f + core * 0.35f);

            byte alpha = (byte)Mathf.RoundToInt(a * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }

        tex.SetPixels32(pixels);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        s_softCircleTex = tex;
        return s_softCircleTex;
    }
}

