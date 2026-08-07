using System;
using UnityEngine;

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
