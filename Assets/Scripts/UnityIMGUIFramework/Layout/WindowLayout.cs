using UnityEngine;

public static class WindowLayout
{
    public static Rect LeftAligned(WindowLayoutConfig config)
    {
        float availableWidth = Mathf.Max(1f, Screen.width - config.horizontalMargin * 2f);
        float width = Mathf.Clamp(
            Screen.width * config.screenWidthRatio,
            config.minimumWidth,
            config.maximumWidth);
        width = Mathf.Min(width, availableWidth);

        float availableHeight = Mathf.Max(1f, Screen.height - config.verticalMargin * 2f);
        float height = Mathf.Min(config.maximumHeight, availableHeight);

        return new Rect(
            config.horizontalMargin,
            (Screen.height - height) * 0.5f,
            width,
            height);
    }

    public static Rect Content(Rect windowRect, Vector2 padding)
    {
        return new Rect(
            windowRect.x + padding.x,
            windowRect.y + padding.y,
            Mathf.Max(1f, windowRect.width - padding.x * 2f),
            Mathf.Max(1f, windowRect.height - padding.y * 2f));
    }

    public static Rect CenteredDialog(Rect parent, DialogLayoutConfig config)
    {
        float availableWidth = Mathf.Max(1f, Screen.width - config.screenMargin * 2f);
        float width = Mathf.Clamp(
            parent.width * config.parentWidthRatio,
            config.minimumWidth,
            config.maximumWidth);
        width = Mathf.Min(width, availableWidth);
        float height = Mathf.Min(config.height, Mathf.Max(1f, Screen.height - config.screenMargin * 2f));

        return new Rect(
            parent.center.x - width * 0.5f,
            parent.center.y - height * 0.5f,
            width,
            height);
    }

    public static Rect ScreenRect()
    {
        return new Rect(Vector2.zero, new Vector2(Screen.width, Screen.height));
    }
}
