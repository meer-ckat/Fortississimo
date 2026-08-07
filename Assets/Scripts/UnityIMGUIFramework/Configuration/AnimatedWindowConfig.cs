using System;
using UnityEngine;

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
