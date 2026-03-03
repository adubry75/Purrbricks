using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Lightweight "clickable" feel for UI: hover pop + press squash (unscaled time).
/// Works on any GameObject with a RectTransform.
/// </summary>
public sealed class UIHoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float _hoverScale = 1.06f;
    [SerializeField] private float _downScale  = 0.96f;
    [SerializeField] private float _speed      = 18f;

    private RectTransform _rt;
    private Vector3 _baseScale;
    private float _targetMul = 1f;
    private bool _hover;
    private bool _down;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _baseScale = _rt != null ? _rt.localScale : Vector3.one;
    }

    private void OnEnable()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        // Don't "learn" the current scale here — if the GO was disabled mid-lerp
        // (or while pressed/hovered), capturing that value causes cumulative drift.
        if (_rt != null) _rt.localScale = _baseScale;
        _hover = _down = false;
        _targetMul = 1f;
    }

    private void Update()
    {
        if (_rt == null) return;
        float dt = Time.unscaledDeltaTime;
        float mul = _targetMul;
        var target = _baseScale * mul;
        _rt.localScale = Vector3.Lerp(_rt.localScale, target, 1f - Mathf.Exp(-_speed * dt));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hover = true;
        RecomputeTarget();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hover = false;
        _down  = false;
        RecomputeTarget();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _down = true;
        RecomputeTarget();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _down = false;
        RecomputeTarget();
    }

    private void RecomputeTarget()
    {
        if (_down) _targetMul = _downScale;
        else if (_hover) _targetMul = _hoverScale;
        else _targetMul = 1f;
    }
}
