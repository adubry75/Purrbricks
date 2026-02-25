using UnityEngine;

public enum PrismColor
{
    None = 0,
    Blue = 1,
    Red = 2,
    Green = 3,
    Pink = 4,
}

public static class PrismColorUtil
{
    public static bool TryParse(string value, out PrismColor color)
    {
        color = PrismColor.None;
        if (string.IsNullOrEmpty(value)) return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "blue":  color = PrismColor.Blue;  return true;
            case "red":   color = PrismColor.Red;   return true;
            case "green": color = PrismColor.Green; return true;
            case "pink":  color = PrismColor.Pink;  return true;
            case "none":  color = PrismColor.None;  return true;
            default: return false;
        }
    }

    public static Color ToUnityColor(PrismColor c)
    {
        switch (c)
        {
            case PrismColor.Blue:  return new Color(0.10f, 0.75f, 1.00f);
            case PrismColor.Red:   return new Color(1.00f, 0.25f, 0.30f);
            case PrismColor.Green: return new Color(0.20f, 1.00f, 0.45f);
            case PrismColor.Pink:  return new Color(1.00f, 0.35f, 0.85f);
            default: return Color.white;
        }
    }
}

