using UnityEngine;
using UnityEngine.UI;

/// <summary>Fits the authored arena without changing physics or level coordinates.</summary>
[DefaultExecutionOrder(100)]
public class PlayfieldLayout : MonoBehaviour
{
    Camera target;
    RectTransform[] margins;
    int width, height;
    Vector3 center;
    Vector2 size;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (Camera.main != null && Camera.main.GetComponent<PlayfieldLayout>() == null)
            Camera.main.gameObject.AddComponent<PlayfieldLayout>();
    }

    void Start()
    {
        target = GetComponent<Camera>();
        var left = GameObject.Find("WallLeft")?.GetComponent<Collider2D>();
        var right = GameObject.Find("WallRight")?.GetComponent<Collider2D>();
        var top = GameObject.Find("WallTop")?.GetComponent<Collider2D>();
        if (left == null || right == null || top == null) { enabled = false; return; }
        float xMin = left.bounds.center.x - .5f;
        float xMax = right.bounds.center.x + .5f;
        float yMin = -8.15f;
        float yMax = top.bounds.center.y + 1.3f;
        center = new Vector3((xMin+xMax)*.5f, (yMin+yMax)*.5f, target.transform.position.z);
        size = new Vector2(xMax-xMin, yMax-yMin);
        CreateMargins();
        Fit();
    }

    void Update() { if (width != Screen.width || height != Screen.height) Fit(); }

    void Fit()
    {
        width = Screen.width; height = Screen.height;
        var rect = PlayfieldLayoutMath.Viewport(width, height);
        target.rect = rect;
        SetMargin(0, Vector2.zero, new Vector2(1f, rect.yMin));
        SetMargin(1, new Vector2(0f, rect.yMax), Vector2.one);
        SetMargin(2, new Vector2(0f, rect.yMin), new Vector2(rect.xMin, rect.yMax));
        SetMargin(3, new Vector2(rect.xMax, rect.yMin), new Vector2(1f, rect.yMax));
        float aspect = width * rect.width / (height * rect.height);
        float ortho = Mathf.Max(size.y*.5f, size.x/(2*aspect));
        target.orthographicSize = ortho;
        target.transform.position = center;
        target.GetComponent<CameraShake>()?.SetLayoutPose(center, ortho);
    }
    // A camera only clears its viewport. Opaque UI margins also clear pixels left
    // behind by menus and previous viewport sizes, without another render camera.
    void CreateMargins()
    {
        var root = new GameObject("Playfield Margins", typeof(Canvas));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;
        margins = new RectTransform[4];
        for (int i = 0; i < margins.Length; i++)
        {
            var go = new GameObject("Margin", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root.transform, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.015f, 0.02f, 0.035f, 1f);
            image.raycastTarget = false;
            margins[i] = go.GetComponent<RectTransform>();
        }
    }

    void SetMargin(int index, Vector2 min, Vector2 max)
    {
        margins[index].anchorMin = min;
        margins[index].anchorMax = max;
        margins[index].offsetMin = margins[index].offsetMax = Vector2.zero;
    }}

