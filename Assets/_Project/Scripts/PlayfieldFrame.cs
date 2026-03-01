using UnityEngine;

/// <summary>
/// Renders a decorative frame sprite that covers the full camera viewport.
/// Sits at sortingOrder -1 — above the parallax background, below all gameplay objects.
/// Also disables the SpriteRenderer on the plain white wall GameObjects so only the
/// colliders remain (physics unchanged).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayfieldFrame : MonoBehaviour
{
    [Header("Art")]
    [Tooltip("Assign playfield_frame sprite here.")]
    [SerializeField] private Sprite _frameSprite;

    [Header("Walls — hide white border sprites")]
    [Tooltip("Names of wall GameObjects whose SpriteRenderer should be hidden (colliders kept intact).")]
    [SerializeField] private string[] _wallsToHide = { "WallLeft", "WallRight", "WallTop" };

    private SpriteRenderer _sr;

    private void Awake()
    {
        _sr              = GetComponent<SpriteRenderer>();
        _sr.sprite       = _frameSprite;
        _sr.sortingOrder = -1;

        SizeToCamera();
        HideWallSprites();
    }

    // Re-size whenever screen resolution changes (e.g. entering play mode at a different res)
    private void OnEnable() => SizeToCamera();

    private void SizeToCamera()
    {
        if (_frameSprite == null) return;

        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        // Full viewport in world units
        float camH = cam.orthographicSize * 2f;
        float camW = camH * cam.aspect;

        // Native sprite size in world units
        //float nativeW = _frameSprite.rect.width  / _frameSprite.pixelsPerUnit;
        //float nativeH = _frameSprite.rect.height / _frameSprite.pixelsPerUnit;

        // Scale to fill the viewport exactly (non-uniform is fine — the art should match the target aspect ratio)
        //transform.position   = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
        //transform.localScale = new Vector3(camW / nativeW, camH / nativeH, 1f);
    }

    private void HideWallSprites()
    {
        foreach (var wallName in _wallsToHide)
        {
            var go = GameObject.Find(wallName);
            if (go == null) continue;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }
    }
}
