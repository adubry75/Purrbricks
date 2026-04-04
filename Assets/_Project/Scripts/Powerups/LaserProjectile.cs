using UnityEngine;

/// <summary>
/// Upward-moving laser bolt. No rigidbody or trigger — uses Physics2D.OverlapBoxAll
/// each frame to detect hits, which avoids all spawn-overlap false positives.
/// </summary>
public class LaserProjectile : MonoBehaviour
{
    private const float SPEED = 22f;
    private const float MAX_Y = 12f; // auto-destroy above play area

    private static readonly Vector2 OVERLAP_SIZE = new Vector2(0.10f, 0.28f);
    private static Sprite _cachedSprite;

    private void Awake()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GetOrCreateSprite();
        sr.color  = new Color(1f, 0.92f, 0.15f);
        sr.sortingOrder = 5;
        // No rigidbody, no collider — detection is manual in Update
    }

    private void Update()
    {
        transform.position += Vector3.up * SPEED * Time.deltaTime;

        // Auto-destroy if it exits the top of the playfield
        if (transform.position.y > MAX_Y)
        {
            Destroy(gameObject);
            return;
        }

        // Only care about bricks — everything else is ignored.
        // Walls/ceiling are handled by the MAX_Y boundary above.
        var hits = Physics2D.OverlapBoxAll(transform.position, OVERLAP_SIZE, 0f);
        foreach (var col in hits)
        {
            var brick = col.GetComponent<Brick>();
            if (brick != null)
            {
                // Prism-locked bricks can only be broken by a matching ball, not by lasers.
                if (brick.RequiredBallColor != PrismColor.None)
                    continue;

                brick.Hit();
                Destroy(gameObject);
                return;
            }
        }
    }

    private static Sprite GetOrCreateSprite()
    {
        // Re-create if destroyed between editor sessions
        if (_cachedSprite != null) return _cachedSprite;

        int w = 6, h = 28;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float cx = (x + 0.5f) / w;
                float cy = (y + 0.5f) / h;
                float edgeFade = 1f - Mathf.Abs(cx - 0.5f) * 2f;
                float tipFade  = 4f * cy * (1f - cy);
                tex.SetPixel(x, y, new Color(1f, 1f, 0.5f, edgeFade * tipFade));
            }
        }

        tex.Apply();
        _cachedSprite = Sprite.Create(tex, new Rect(0, 0, w, h),
                                      new Vector2(0.5f, 0.5f), 16f);
        return _cachedSprite;
    }
}
