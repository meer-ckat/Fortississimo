using System;
using UnityEngine;

[Serializable]
public struct IMGUITheme
{
    [Header("Typography")]
    [Min(1)] public int titleFontSize;
    [Min(1)] public int labelFontSize;
    [Min(1)] public int valueFontSize;
    [Min(1)] public int buttonFontSize;
    [Min(1)] public int dialogFontSize;

    [Header("Colors")]
    public Color normalTextColor;
    public Color changedTextColor;
    public Color disabledTextColor;
    public Color panelTint;
    public Color modalDimColor;

    [Header("Style Padding")]
    public RectOffset panelPadding;

    public bool IsUsable => titleFontSize > 0 && labelFontSize > 0 && buttonFontSize > 0;

    public static IMGUITheme Default => new IMGUITheme
    {
        titleFontSize = 28,
        labelFontSize = 16,
        valueFontSize = 15,
        buttonFontSize = 15,
        dialogFontSize = 17,
        normalTextColor = Color.white,
        changedTextColor = Color.yellow,
        disabledTextColor = new Color(0.65f, 0.65f, 0.65f, 1f),
        panelTint = new Color(0.12f, 0.12f, 0.16f, 0.98f),
        modalDimColor = new Color(0f, 0f, 0f, 0.42f),
        panelPadding = new RectOffset(12, 12, 12, 12)
    };
}
