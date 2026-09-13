using UnityEngine;

public static class PlayfieldLayoutMath
{
    public static Rect Viewport(int width, int height)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        float scale = Mathf.Min(width / 1920f, height / 1080f);
        return new Rect(24 * scale / width, 126 * scale / height,
            1 - 48 * scale / width, 1 - 326 * scale / height);
    }
}
