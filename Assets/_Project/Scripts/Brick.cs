using UnityEngine;

public class Brick : MonoBehaviour
{
    [Header("Points")]
    [SerializeField] private int _basePoints = 10;

    [Header("Hit Points")]
    [SerializeField] private int _hitPoints = 1;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer _sr;

    // Optional: simple tinting per HP (1=light, 3=dark)
    [SerializeField] private Color _hp1Color = new Color(0.90f, 0.90f, 0.90f, 1f);
    [SerializeField] private Color _hp2Color = new Color(0.70f, 0.70f, 0.95f, 1f);
    [SerializeField] private Color _hp3Color = new Color(0.95f, 0.70f, 0.70f, 1f);
    [SerializeField] private Color _hp4Color = new Color(0.35f, 0.90f, 0.70f, 1f);

    private void Reset()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        ApplyVisuals();
    }

    public void SetHitPoints(int hp)
    {
        _hitPoints = Mathf.Clamp(hp, 1, 9);
        ApplyVisuals();
    }

    public void Hit()
    {
        _hitPoints--;

        // Score + combo on every successful hit
        GameManager.Instance?.AddScore(_basePoints);
        GameManager.Instance?.IncrementCombo();

        if (_hitPoints > 0)
        {
            // Not destroyed yet, just update visuals + little sound
            ApplyVisuals();
            SfxPlayer.Instance?.PlayBrickHit();
            return;
        }

        // Destroyed
        SfxPlayer.Instance?.PlayBrickBreak();

        LevelManager.Instance?.OnBrickDestroyed();
        Destroy(gameObject);
    }

    private void ApplyVisuals()
    {
        if (_sr == null) return;

        // Choose tint based on HP (cap at 4 for tinting)
        int hp = Mathf.Clamp(_hitPoints, 1, 4);
        switch (hp)
        {
            case 1: _sr.color = _hp1Color; break;
            case 2: _sr.color = _hp2Color; break;
            case 3: _sr.color = _hp3Color; break;
            case 4: _sr.color = _hp4Color; break;
            default: _sr.color = _hp4Color; break;
        }
    }
}
