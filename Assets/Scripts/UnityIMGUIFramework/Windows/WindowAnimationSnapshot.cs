using UnityEngine;

public enum WindowContentPhase
{
    Hidden,
    Opening,
    Visible,
    Closing
}

public readonly struct WindowAnimationSnapshot
{
    public readonly WindowContentPhase phase;
    public readonly double startTime;
    public readonly int totalItems;
    public readonly AnimatedListingConfig config;

    public WindowAnimationSnapshot(
        WindowContentPhase phase,
        double startTime,
        int totalItems,
        AnimatedListingConfig config)
    {
        this.phase = phase;
        this.startTime = startTime;
        this.totalItems = Mathf.Max(0, totalItems);
        this.config = config;
    }

    public float GetProgress(int index, double guiTime)
    {
        switch (phase)
        {
            case WindowContentPhase.Hidden:
                return 0f;
            case WindowContentPhase.Visible:
                return 1f;
        }

        float elapsed = Mathf.Max(0f, (float)(guiTime - startTime));
        if (phase == WindowContentPhase.Opening)
        {
            float local = Mathf.Clamp01(
                (elapsed - Mathf.Max(0, index) * config.itemStagger) / config.itemDuration);
            return IMGUIEase.Evaluate(config.openTween, local);
        }

        int reverseIndex = Mathf.Max(0, totalItems - 1 - index);
        float closing = Mathf.Clamp01(
            (elapsed - reverseIndex * config.itemStagger) / config.itemDuration);
        return 1f - IMGUIEase.Evaluate(config.closeTween, closing);
    }

    public float SequenceDuration
    {
        get
        {
            int gaps = Mathf.Max(0, totalItems - 1);
            return config.itemDuration + gaps * config.itemStagger;
        }
    }
}
