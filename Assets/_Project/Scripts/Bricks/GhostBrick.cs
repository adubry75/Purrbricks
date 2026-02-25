using System.Collections;
using UnityEngine;

/// <summary>
/// A brick that revives after being destroyed:
/// - 10s delay (gone)
/// - 5s fade-in (ghost, non-collidable)
/// - then becomes solid again and counts toward level completion.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Brick))]
public class GhostBrick : MonoBehaviour
{
    [SerializeField] private float _reviveDelaySeconds = 10f;
    [SerializeField] private float _fadeInSeconds = 5f;

    private Brick _brick;
    private Collider2D[] _colliders;
    private SpriteRenderer[] _renderers;
    private BrickMover _mover;
    private BrickVisualController _visual;
    private Vector3 _spawnPos;

    private Coroutine _routine;

    private void Awake()
    {
        _brick = GetComponent<Brick>();
        _colliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
        _renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        _mover = GetComponent<BrickMover>();
        _visual = GetComponent<BrickVisualController>();
        _spawnPos = transform.position;
    }

    public void OnKilled()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ReviveRoutine());
    }

    private IEnumerator ReviveRoutine()
    {
        // Immediately "remove" the brick from play.
        SetCollidable(false);
        SetMoverEnabled(false);
        if (_visual != null) _visual.enabled = false;
        SetVisible(false, 0f);

        yield return new WaitForSeconds(_reviveDelaySeconds);

        // If the level is already cleared, don't bring bricks back.
        if (LevelManager.Instance != null && LevelManager.Instance.BricksRemaining <= 0)
        {
            _routine = null;
            yield break;
        }

        // Start fade-in at original spawn location.
        transform.position = _spawnPos;
        SetVisible(true, 0f);

        float t = 0f;
        while (t < _fadeInSeconds)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.01f, _fadeInSeconds));

            // Ghost fade: visible but non-collidable.
            SetAlpha(a * 0.85f);
            yield return null;
        }

        // Become solid again. Only then does it count toward clearing the level.
        if (LevelManager.Instance != null)
            LevelManager.Instance.OnBrickRevived();

        SetAlpha(1f);
        SetVisible(true, 1f);
        _brick.Revive();
        if (_visual != null) _visual.enabled = true;
        SetMoverEnabled(true);
        SetCollidable(true);

        _routine = null;
    }

    private void SetMoverEnabled(bool on)
    {
        if (_mover != null)
            _mover.enabled = on;
    }

    private void SetCollidable(bool on)
    {
        if (_colliders == null) return;
        foreach (var c in _colliders)
            if (c != null) c.enabled = on;
    }

    private void SetVisible(bool on, float alpha)
    {
        if (_renderers == null) return;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.enabled = on;
            var c = r.color;
            c.a = alpha;
            r.color = c;
        }
    }

    private void SetAlpha(float a)
    {
        if (_renderers == null) return;
        foreach (var r in _renderers)
        {
            if (r == null || !r.enabled) continue;
            var c = r.color;
            c.a = a;
            r.color = c;
        }
    }
}
