using System;
using UnityEngine;

[Serializable]
public struct DialogLayoutConfig
{
    [Range(0.1f, 1f)] public float parentWidthRatio;
    [Min(1f)] public float minimumWidth;
    [Min(1f)] public float maximumWidth;
    [Min(1f)] public float height;
    [Min(0f)] public float screenMargin;
    public Vector2 contentPadding;

    public bool IsUsable => minimumWidth > 0f && maximumWidth >= minimumWidth && height > 0f;

    public static DialogLayoutConfig Default => new DialogLayoutConfig
    {
        parentWidthRatio = 0.74f,
        minimumWidth = 340f,
        maximumWidth = 560f,
        height = 220f,
        screenMargin = 16f,
        contentPadding = new Vector2(28f, 26f)
    };
}
