using System;
using UnityEngine;

// All serialized configuration for the IMGUI framework.
// Every struct exposes IsUsable (has this been authored?) and Resolved
// (this, or the built-in default when the serialized data is still zeroed).
// Consumers call Resolved once at construction instead of repeating the
// "IsUsable ? value : Default" ternary at every use site.

[Serializable]
public struct AnimatedWindowConfig
{
    [Min(0f)] public float openDuration;
    [Min(0f)] public float closeDuration;
    [Min(0f)] public float offscreenMargin;
    [Min(0f)] public float contentStartDelay;
    public TweenMethod openTween;
    public TweenMethod closeTween;

    public bool IsUsable => openDuration > 0f && closeDuration > 0f;

    public AnimatedWindowConfig Resolved => IsUsable ? this : Default;

    public static AnimatedWindowConfig Default => new AnimatedWindowConfig
    {
        openDuration = 0.38f,
        closeDuration = 0.32f,
        offscreenMargin = 60f,
        contentStartDelay = 0.12f,
        openTween = TweenMethod.OutBack,
        closeTween = TweenMethod.InCubic
    };

    public static AnimatedWindowConfig ConfirmDefault => new AnimatedWindowConfig
    {
        openDuration = 0.28f,
        closeDuration = 0.24f,
        offscreenMargin = 40f,
        contentStartDelay = 0.08f,
        openTween = TweenMethod.OutBack,
        closeTween = TweenMethod.InCubic
    };
}

[Serializable]
public struct AnimatedListingConfig
{
    [Header("Rows")]
    [Min(1f)] public float rowHeight;
    [Min(0f)] public float rowSpacing;
    [Min(0f)] public float sectionSpacing;
    [Min(1f)] public float titleHeight;
    [Min(1f)] public float messageHeight;

    [Header("Columns")]
    [Range(0.1f, 0.8f)] public float labelWidthRatio;
    [Min(1f)] public float minimumLabelWidth;
    [Min(1f)] public float maximumLabelWidth;
    [Min(1f)] public float valueWidth;
    [Min(0f)] public float columnGap;

    [Header("Controls")]
    [Min(0f)] public float sliderVerticalOffset;
    [Min(1f)] public float sliderHeight;
    [Min(1f)] public float toggleWidth;
    [Min(1f)] public float buttonHeight;
    [Min(1f)] public float closeButtonSize;
    [Min(0f)] public float buttonPairGap;

    [Header("Animation")]
    [Min(0.001f)] public float itemDuration;
    [Min(0f)] public float itemStagger;
    [Min(0f)] public float horizontalOffset;
    public TweenMethod openTween;
    public TweenMethod closeTween;

    public bool IsUsable => rowHeight > 0f && itemDuration > 0f;

    public AnimatedListingConfig Resolved => IsUsable ? this : Default;

    /// <summary>
    /// Wall-clock length of a staggered open or close sequence for the given
    /// number of items. Single source of truth: both the window state machine
    /// and the per-item progress curve derive their timing from this.
    /// </summary>
    public float SequenceDuration(int itemCount)
    {
        if (itemCount <= 0)
        {
            return 0f;
        }

        return itemDuration + (itemCount - 1) * itemStagger;
    }

    public static AnimatedListingConfig Default => new AnimatedListingConfig
    {
        rowHeight = 36f,
        rowSpacing = 18f,
        sectionSpacing = 10f,
        titleHeight = 44f,
        messageHeight = 64f,
        labelWidthRatio = 0.34f,
        minimumLabelWidth = 130f,
        maximumLabelWidth = 190f,
        valueWidth = 60f,
        columnGap = 14f,
        sliderVerticalOffset = 8f,
        sliderHeight = 20f,
        toggleWidth = 170f,
        buttonHeight = 40f,
        closeButtonSize = 36f,
        buttonPairGap = 14f,
        itemDuration = 0.28f,
        itemStagger = 0.065f,
        horizontalOffset = 140f,
        openTween = TweenMethod.OutBack,
        closeTween = TweenMethod.InCubic
    };
}

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

    public WindowLayoutConfig Resolved => IsUsable ? this : SettingsDefault;

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

    public DialogLayoutConfig Resolved => IsUsable ? this : Default;

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
    public Color panelTint;
    public Color modalDimColor;

    [Header("Style Padding")]
    public RectOffset panelPadding;

    public bool IsUsable => titleFontSize > 0 && labelFontSize > 0 && buttonFontSize > 0;

    public IMGUITheme Resolved => IsUsable ? this : Default;

    public static IMGUITheme Default => new IMGUITheme
    {
        titleFontSize = 28,
        labelFontSize = 16,
        valueFontSize = 15,
        buttonFontSize = 15,
        dialogFontSize = 17,
        normalTextColor = Color.white,
        changedTextColor = Color.yellow,
        panelTint = new Color(0.12f, 0.12f, 0.16f, 0.98f),
        modalDimColor = new Color(0f, 0f, 0f, 0.42f),
        panelPadding = new RectOffset(12, 12, 12, 12)
    };
}
