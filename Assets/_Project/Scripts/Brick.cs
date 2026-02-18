using UnityEngine;

public class Brick : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private int _hitPoints = 1;
    [SerializeField] private int _points = 100;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer _sr;

    // Base tint for “this brick type” (red, blue, gold, etc.)
    private Color _baseTint = Color.white;

    private void Reset()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) _baseTint = _sr.color;
        ApplyVisuals();
    }

    // -------- Called by BrickGridSpawner / Level system --------

    public void SetHitPoints(int hp)
    {
        _hitPoints = Mathf.Clamp(hp, 1, 99);
        ApplyVisuals();
    }

    public void SetPoints(int points)
    {
        _points = Mathf.Max(0, points);
    }

    public void SetTint(Color tint)
    {
        _baseTint = tint;
        ApplyVisuals();
    }

    // -------- Called by BallController when collision happens --------

    public void Hit()
    {
        _hitPoints--;

        // Score + combo per hit (we can change later to "only on destroy")
        GameManager.Instance?.AddScore(_points);
        GameManager.Instance?.IncrementCombo();

        if (_hitPoints > 0)
        {
            ApplyVisuals();
            SfxPlayer.Instance?.PlayBrickHit();
            return;
        }

        // Destroyed
        SfxPlayer.Instance?.PlayBrickBreak(); // if you don't have this, comment it out

        LevelManager.Instance?.OnBrickDestroyed();
        Destroy(gameObject);
    }

    // -------- Visual logic --------

    private void ApplyVisuals()
    {
        if (_sr == null) return;

        // Simple “damage shading”: higher HP looks a bit darker.
        // This does NOT mean HP=Color; it's just a readability hint.
        float shade = Mathf.Clamp01((_hitPoints - 1) * 0.12f);
        Color c = _baseTint * (1f - shade);
        c.a = 1f;

        _sr.color = c;
    }
}
