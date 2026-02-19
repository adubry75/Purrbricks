using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PowerupPickup : MonoBehaviour
{
    private string _powerupId;
    private SpriteRenderer _sr;

    public void Init(string powerupId)
    {
        _powerupId = powerupId;

        // Try to get sprite from registry
        if (PowerupRegistry.Instance != null)
        {
            var def = PowerupRegistry.Instance.Get(powerupId);
            if (def != null && def.pickupSprite != null)
            {
                _sr = GetComponent<SpriteRenderer>();
                if (_sr != null) _sr.sprite = def.pickupSprite;
            }
        }
    }

    private void Awake()
    {
        var rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 1f;

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Paddle"))
        {
            Debug.Log($"Powerup picked up: {_powerupId}");
            // Future: look up PowerupDefinition, instantiate PowerupBase, call Apply()
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("DeathZone"))
        {
            Destroy(gameObject);
        }
    }
}
