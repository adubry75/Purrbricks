using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Big floating "5x COMBO!" text that appears at combo milestones.
/// </summary>
public class ComboFeedback : MonoBehaviour
{
    private CanvasGroup _canvasGroup;
    private Text _text;
    private float _elapsed;
    private float _lifetime = 1.5f;
    private Vector3 _startScale;

    public static void Show(int comboCount)
    {
        // Only show at milestones
        if (comboCount < 2) return;
        if (comboCount > 2 && comboCount % 5 != 0) return; // 2, 5, 10, 15, 20...

        var go = new GameObject("ComboFeedback", typeof(Canvas), typeof(CanvasGroup));
        var feedback = go.AddComponent<ComboFeedback>();
        feedback.BuildUI(comboCount);
    }

    private void BuildUI(int comboCount)
    {
        _startScale = Vector3.one * 0.8f;

        // Canvas: World Space, positioned above paddle
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(600f, 200f);
        rt.localScale = Vector3.one * 0.008f;

        // Position: center-bottom of screen (above paddle area)
        transform.position = new Vector3(0f, -5f, 0f);

        _canvasGroup = GetComponent<CanvasGroup>();

        // Text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(transform, false);

        _text = textGO.AddComponent<Text>();
        _text.text = $"{comboCount}x COMBO!";
        _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _text.fontSize = 120;
        _text.fontStyle = FontStyle.Bold;
        _text.alignment = TextAnchor.MiddleCenter;

        // Color: bright yellow-orange with gradient
        Color comboColor = comboCount >= 10 ? new Color(1f, 0.3f, 0.8f) : // magenta for high combos
                          comboCount >= 5  ? new Color(1f, 0.6f, 0f)   : // orange
                                             new Color(1f, 1f, 0.2f);    // yellow

        _text.color = comboColor;

        var textRt = _text.GetComponent<RectTransform>();
        textRt.sizeDelta = new Vector2(600f, 200f);
        textRt.anchoredPosition = Vector2.zero;

        // Outline
        var outline = textGO.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(6f, -6f);
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

        // Scale pulse (elastic in, fade out)
        float scale = t < 0.3f
            ? Mathf.Lerp(0.5f, 1.3f, t / 0.3f) // grow fast
            : Mathf.Lerp(1.3f, 1f, (t - 0.3f) / 0.7f); // settle

        transform.localScale = Vector3.one * scale * 0.008f;

        // Float upward slowly
        transform.position += Vector3.up * Time.deltaTime * 0.8f;

        // Fade out after 60%
        _canvasGroup.alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
    }
}
