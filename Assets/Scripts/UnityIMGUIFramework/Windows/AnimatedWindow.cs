using System;
using UnityEngine;

public enum AnimatedWindowState
{
    Hidden,
    OpeningPanel,
    OpeningContent,
    Open,
    ClosingContent,
    ClosingPanel
}

/// <summary>
/// A panel that slides in from off-screen, staggers its content in, and reverses
/// on close. Owns its own rect and position tween; there is no global registry.
///
/// Per-frame contract, in this order:
///   1. BeginFrame(targetRect, guiTime)  - advances the position tween
///   2. TryGetRect(out panelRect)        - pure read, safe on every event
///   3. draw content, then RecordItemCount(n)
///   4. EndFrame(guiTime)                - Repaint only; advances the state machine
///
/// EndFrame runs last on purpose: the content phase duration depends on the item
/// count, which is only known once the content has been laid out this frame.
/// </summary>
public sealed class AnimatedWindow : IDisposable
{
    private readonly AnimatedWindowConfig windowConfig;
    private readonly AnimatedListingConfig listingConfig;

    private AnimatedWindowState state;
    private Rect rect;
    private Rect targetRect;

    private Vector2 tweenFrom;
    private Vector2 tweenTo;
    private double tweenStartTime;
    private float tweenDuration;
    private TweenMethod tweenMethod;
    private bool tweening;

    private double phaseStartTime;
    private int itemCount;
    private Action closeCompletion;

    public bool IsVisible => state != AnimatedWindowState.Hidden;
    public bool IsOpen => state == AnimatedWindowState.Open;
    public bool InputEnabled => IsOpen;

    public AnimatedWindow(AnimatedWindowConfig windowConfig, AnimatedListingConfig listingConfig)
    {
        this.windowConfig = windowConfig.Resolved;
        this.listingConfig = listingConfig.Resolved;
        state = AnimatedWindowState.Hidden;
    }

    public void Open(Rect desiredRect, double guiTime)
    {
        if (IsVisible)
        {
            return;
        }

        targetRect = desiredRect;
        itemCount = 0;
        closeCompletion = null;

        rect = desiredRect;
        rect.x = -desiredRect.width - windowConfig.offscreenMargin;

        StartTween(desiredRect.position, windowConfig.openDuration, windowConfig.openTween, guiTime);

        state = AnimatedWindowState.OpeningPanel;
        phaseStartTime = guiTime;
    }

    public void Close(Action onComplete = null)
    {
        if (!IsVisible || state == AnimatedWindowState.ClosingContent ||
            state == AnimatedWindowState.ClosingPanel)
        {
            return;
        }

        closeCompletion = onComplete;
        state = AnimatedWindowState.ClosingContent;
        phaseStartTime = GUIFrameClock.Capture();
    }

    /// <summary>Advances the position tween. Never invokes user callbacks.</summary>
    public void BeginFrame(Rect desiredTargetRect, double guiTime)
    {
        if (!IsVisible)
        {
            return;
        }

        targetRect = desiredTargetRect;
        EvaluateTween(guiTime);

        // Only a fully open window follows a moving target (screen resize).
        if (state == AnimatedWindowState.Open)
        {
            rect = targetRect;
        }
    }

    /// <summary>
    /// Advances the open/close state machine. Call once per rendered frame, after
    /// the content has been drawn and RecordItemCount has run.
    /// </summary>
    public void EndFrame(double guiTime)
    {
        if (!IsVisible)
        {
            return;
        }

        switch (state)
        {
            case AnimatedWindowState.OpeningPanel:
                if (guiTime - phaseStartTime >= windowConfig.openDuration + windowConfig.contentStartDelay)
                {
                    state = AnimatedWindowState.OpeningContent;
                    phaseStartTime = guiTime;
                }
                break;

            case AnimatedWindowState.OpeningContent:
                if (guiTime - phaseStartTime >= ContentDuration)
                {
                    state = AnimatedWindowState.Open;
                    phaseStartTime = guiTime;
                    rect = targetRect;
                }
                break;

            case AnimatedWindowState.ClosingContent:
                if (guiTime - phaseStartTime >= ContentDuration)
                {
                    BeginPanelClose(guiTime);
                }
                break;

            case AnimatedWindowState.ClosingPanel:
                if (guiTime - phaseStartTime >= windowConfig.closeDuration)
                {
                    FinishClose();
                }
                break;
        }
    }

    /// <summary>Pure read of the current animated rect. Safe on any GUI event.</summary>
    public bool TryGetRect(out Rect value)
    {
        value = IsVisible ? rect : default;
        return IsVisible;
    }

    public WindowAnimationSnapshot GetAnimationSnapshot()
    {
        WindowContentPhase contentPhase;
        switch (state)
        {
            case AnimatedWindowState.OpeningContent:
                contentPhase = WindowContentPhase.Opening;
                break;
            case AnimatedWindowState.Open:
                contentPhase = WindowContentPhase.Visible;
                break;
            case AnimatedWindowState.ClosingContent:
                contentPhase = WindowContentPhase.Closing;
                break;
            default:
                contentPhase = WindowContentPhase.Hidden;
                break;
        }

        return new WindowAnimationSnapshot(contentPhase, phaseStartTime, itemCount, listingConfig);
    }

    public void RecordItemCount(int count)
    {
        itemCount = Mathf.Max(0, count);
    }

    public void Dispose()
    {
        state = AnimatedWindowState.Hidden;
        tweening = false;
        itemCount = 0;
        closeCompletion = null;
    }

    private float ContentDuration => listingConfig.SequenceDuration(itemCount);

    private void StartTween(Vector2 destination, float duration, TweenMethod method, double guiTime)
    {
        tweenFrom = rect.position;
        tweenTo = destination;
        tweenStartTime = guiTime;
        tweenDuration = Mathf.Max(0f, duration);
        tweenMethod = method;
        tweening = true;

        EvaluateTween(guiTime);
    }

    private void EvaluateTween(double guiTime)
    {
        if (!tweening)
        {
            return;
        }

        float progress = tweenDuration <= 0f
            ? 1f
            : Mathf.Clamp01((float)((guiTime - tweenStartTime) / tweenDuration));

        rect.position = Vector2.LerpUnclamped(
            tweenFrom,
            tweenTo,
            IMGUIEase.Evaluate(tweenMethod, progress));

        if (progress >= 1f)
        {
            rect.position = tweenTo;
            tweening = false;
        }
    }

    private void BeginPanelClose(double guiTime)
    {
        Vector2 exitPosition = new Vector2(-rect.width - windowConfig.offscreenMargin, rect.y);
        StartTween(exitPosition, windowConfig.closeDuration, windowConfig.closeTween, guiTime);

        state = AnimatedWindowState.ClosingPanel;
        phaseStartTime = guiTime;
    }

    private void FinishClose()
    {
        state = AnimatedWindowState.Hidden;
        tweening = false;
        itemCount = 0;

        Action completion = closeCompletion;
        closeCompletion = null;
        completion?.Invoke();
    }
}
