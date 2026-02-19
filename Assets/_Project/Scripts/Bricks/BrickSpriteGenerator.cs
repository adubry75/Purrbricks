using UnityEngine;

/// <summary>
/// Generates a single shared grayscale gradient sprite at runtime.
/// Tinting is done via SpriteRenderer.color, so every brick color
/// costs nothing extra — it's just one texture shared by all bricks.
/// </summary>
public static class BrickSpriteGenerator
{
    private static Sprite _shared;

    /// <summary>The shared grayscale sprite (generated once, reused forever).</summary>
    public static Sprite GetShared()
    {
        if (_shared == null)
            _shared = Build();
        return _shared;
    }

    private static Sprite Build()
    {
        const int W = 270;
        const int H = 90;

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color[W * H];

        for (int y = 0; y < H; y++)
        {
            float ny = y / (float)(H - 1); // 0 = bottom, 1 = top

            for (int x = 0; x < W; x++)
            {
                float nx = x / (float)(W - 1); // 0 = left, 1 = right

                // Main gradient: bright top, darker bottom
                float v = Mathf.Lerp(0.38f, 0.90f, Mathf.Pow(ny, 0.60f));

                // Top highlight band — sharp bright rim
                float topBand = Mathf.Clamp01((ny - 0.80f) / 0.20f);
                v = Mathf.Lerp(v, 1.55f, topBand * topBand);

                // Oval gloss spot (upper-left area)
                float gx = (nx - 0.24f) * 3.2f;
                float gy = (ny - 0.68f) * 1.1f;
                float gloss = Mathf.Clamp01(1f - Mathf.Sqrt(gx * gx + gy * gy) / 0.38f);
                v += gloss * 0.52f;

                // Faint horizontal sheen stripe at ~55% height
                float sheen = Mathf.Clamp01(1f - Mathf.Abs(ny - 0.55f) / 0.10f);
                v += sheen * 0.05f;

                // Bottom shadow
                v *= Mathf.Lerp(0.48f, 1f, Mathf.Clamp01(ny / 0.07f));

                // Left/right edge bevel
                float edgeX = Mathf.Min(nx, 1f - nx);
                v *= Mathf.Lerp(0.52f, 1f, Mathf.Clamp01(edgeX / 0.024f));

                v = Mathf.Clamp01(v);
                pixels[y * W + x] = new Color(v, v, v, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        // 6 px border on all sides so Sliced mode can stretch the center cleanly
        return Sprite.Create(
            tex,
            new Rect(0, 0, W, H),
            new Vector2(0.5f, 0.5f),
            100f, 0,
            SpriteMeshType.FullRect,
            new Vector4(6, 6, 6, 6)
        );
    }
}
