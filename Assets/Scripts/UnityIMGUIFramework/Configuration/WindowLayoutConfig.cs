using System;
using UnityEngine;

[Serializable]
public struct WindowLayoutConfig
{
    [Range(0.1f, 1f)] public float screenWidthRatio;
    [Min(1f)] public float minimumWidth;
    [Min(1f)] public float maximumWidth;
    [Min(1f)] public float maximumHeight;
    [Min(0f)] public float horizontalMargin;
    [Min(0f)] public float verticalMargin;
    public Vector2 contentPadding;

    public bool IsUsable => minimumWidth > 0f && maximumWidth >= minimumWidth && maximumHeight > 0f;

    public static WindowLayoutConfig SettingsDefault => new WindowLayoutConfig
    {
        screenWidthRatio = 0.5f,
        minimumWidth = 460f,
        maximumWidth = 760f,
        maximumHeight = 620f,
        horizontalMargin = 20f,
        verticalMargin = 24f,
        contentPadding = new Vector2(30f, 22f)
    };
}
