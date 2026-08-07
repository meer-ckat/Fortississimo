using System;
using System.Threading;
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

public sealed class AnimatedWindow : IDisposable
{
    private static int nextId;

    private readonly string guiId;
    private readonly AnimatedWindowConfig windowConfig;
    private readonly AnimatedListingConfig listingConfig;

    private AnimatedWindowState state;
    private Rect targetRect;
    private double phaseStartTime;
    private int itemCount;
    private Action closeCompletion;

    public AnimatedWindowState State => state;
    public bool IsVisible => state != AnimatedWindowState.Hidden;
    public bool IsOpen => state == AnimatedWindowState.Open;
    public bool IsAnimating => IsVisible && !IsOpen;
    public bool InputEnabled => IsOpen;

    public AnimatedWindow(UnityEngine.Object owner, string readableName,
        AnimatedWindowConfig windowConfig, AnimatedListingConfig listingConfig)
    {
        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        if (string.IsNullOrWhiteSpace(readableName))
        {
            throw new ArgumentException("A readable window name is required.", nameof(readableName));
        }

        this.windowConfig = windowConfig.IsUsable ? windowConfig : AnimatedWindowConfig.Default;
        this.listingConfig = listingConfig.IsUsable ? listingConfig : AnimatedListingConfig.Default;
        guiId = $"{owner.GetInstanceID()}.{readableName}.{Interlocked.Increment(ref nextId)}";
        state = AnimatedWindowState.Hidden;
    }

    public void Open(Rect rect, double guiTime)
    {
        if (IsVisible)
        {
            return;
        }

        targetRect = rect;
        itemCount = 0;
        closeCompletion = null;

        Rect startRect = rect;
        startRect.x = -rect.width - windowConfig.offscreenMargin;
        GUIManager.Register(guiId, startRect);
        GUIManager.MoveTo(guiId, rect.position, windowConfig.openDuration, windowConfig.openTween);

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

    public void Tick(Rect desiredTargetRect, double guiTime)
    {
        if (!IsVisible)
        {
            return;
        }

        targetRect = desiredTargetRect;

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
                if (guiTime - phaseStartTime >= GetContentDuration())
                {
                    state = AnimatedWindowState.Open;
                    phaseStartTime = guiTime;
                    GUIManager.SetRect(guiId, targetRect);
                }
                break;

            case AnimatedWindowState.Open:
                GUIManager.SetRect(guiId, targetRect);
                break;

            case AnimatedWindowState.ClosingContent:
                if (guiTime - phaseStartTime >= GetContentDuration())
                {
                    BeginPanelClose(guiTime);
                }
                break;

            case AnimatedWindowState.ClosingPanel:
                GUIManager.TryGetRect(guiId, out _);
                if (guiTime - phaseStartTime >= windowConfig.closeDuration)
                {
                    FinishClose();
                }
                break;
        }
    }

    public bool TryGetRect(out Rect rect)
    {
        if (!IsVisible)
        {
            rect = default;
            return false;
        }

        return GUIManager.TryGetRect(guiId, out rect);
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
        GUIManager.Remove(guiId);
        state = AnimatedWindowState.Hidden;
        closeCompletion = null;
    }

    private float GetContentDuration()
    {
        if (itemCount == 0)
        {
            return 0f;
        }

        int gaps = Mathf.Max(0, itemCount - 1);
        return listingConfig.itemDuration + gaps * listingConfig.itemStagger;
    }

    private void BeginPanelClose(double guiTime)
    {
        if (!GUIManager.TryGetRect(guiId, out Rect currentRect))
        {
            FinishClose();
            return;
        }

        Vector2 exitPosition = new Vector2(
            -currentRect.width - windowConfig.offscreenMargin,
            currentRect.y);
        GUIManager.MoveTo(guiId, exitPosition, windowConfig.closeDuration, windowConfig.closeTween);
        state = AnimatedWindowState.ClosingPanel;
        phaseStartTime = guiTime;
    }

    private void FinishClose()
    {
        GUIManager.Remove(guiId);
        state = AnimatedWindowState.Hidden;
        itemCount = 0;

        Action completion = closeCompletion;
        closeCompletion = null;
        completion?.Invoke();
    }
}
