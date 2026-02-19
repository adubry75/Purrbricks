using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated score text that floats upward and fades out.
/// Spawns on brick hit to show points gained.
/// </summary>
public class ScorePopup : MonoBehaviour
{
    private CanvasGroup _canvasGroup;
    private Vector3 _startPos;
    private float _elapsed;

    [SerializeField] private float _lifetime = 1.0f;
    [SerializeField] private float _riseDistance = 0.5f;
    [SerializeField] private float _wiggleAmount = 0.08f;

    /// <summary>Spawns a popup at the given world position.</summary>
    public static void Spawn(Vector3 worldPos, int points, Color color)
    {
        var go = new GameObject("ScorePopup", typeof(Canvas), typeof(CanvasGroup));
        go.transform.position = worldPos + new Vector3(0.25f, 0.3f, 0f);

        var popup = go.AddComponent<ScorePopup>();
        popup.BuildUI(points, color);
    }

    private void BuildUI(int points, Color color)
    {
        _startPos = transform.position;

        // Canvas: World Space, small scale for crisp text
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 120f);
        rt.localScale = Vector3.one * 0.0065f; // scale to fit world units

        _canvasGroup = GetComponent<CanvasGroup>();

        // Text child
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(transform, false);

        var text = textGO.AddComponent<Text>();
        text.text = $"+{points}";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 88;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;

        var textRt = text.GetComponent<RectTransform>();
        textRt.sizeDelta = new Vector2(300f, 120f);
        textRt.anchoredPosition = Vector2.zero;

        // Outline + Shadow for readability over busy backgrounds
        var outline = textGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(4f, -4f);

        var shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = _elapsed / _lifetime;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Ease-out cubic for smooth deceleration
        float easedT = 1f - Mathf.Pow(1f - t, 3f);

        // Float upward
        Vector3 pos = _startPos + Vector3.up * (_riseDistance * easedT);

        // Gentle horizontal wiggle (sine wave, 2 cycles)
        pos.x += Mathf.Sin(t * Mathf.PI * 4f) * _wiggleAmount * (1f - t); // dampen wiggle as it fades

        transform.position = pos;

        // Fade out (start fading faster after 60% of lifetime)
        _canvasGroup.alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
    }
}
