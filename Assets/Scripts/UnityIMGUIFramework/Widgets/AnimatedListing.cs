using System;
using UnityEngine;

public enum ButtonPairResult
{
    None,
    Left,
    Right
}

public sealed class AnimatedListing
{
    private readonly struct AnimatedItem
    {
        public readonly Rect rect;
        public readonly float progress;

        public AnimatedItem(Rect rect, float progress)
        {
            this.rect = rect;
            this.progress = progress;
        }
    }

    private readonly Rect contentRect;
    private readonly WindowAnimationSnapshot animation;
    private readonly AnimatedListingConfig config;
    private readonly AnimatedListingStyles styles;
    private readonly bool inputEnabled;
    private readonly double guiTime;

    private float currentY;
    private int itemIndex;

    public int ItemCount => itemIndex;

    public AnimatedListing(
        Rect contentRect,
        WindowAnimationSnapshot animation,
        AnimatedListingConfig config,
        AnimatedListingStyles styles,
        bool inputEnabled,
        double guiTime)
    {
        this.contentRect = contentRect;
        this.animation = animation;
        this.config = config.IsUsable ? config : AnimatedListingConfig.Default;
        this.styles = styles ?? throw new ArgumentNullException(nameof(styles));
        this.inputEnabled = inputEnabled;
        this.guiTime = guiTime;
        currentY = contentRect.y;
        itemIndex = 0;
    }

    public bool TitleBar(string title, string closeLabel = "×")
    {
        Rect row = TakeRow(config.titleHeight, config.sectionSpacing);
        AnimatedItem item = Animate(row);

        Rect closeRect = new Rect(
            item.rect.xMax - config.closeButtonSize,
            item.rect.y,
            config.closeButtonSize,
            config.closeButtonSize);
        Rect titleRect = new Rect(
            item.rect.x,
            item.rect.y,
            Mathf.Max(1f, item.rect.width - config.closeButtonSize - config.columnGap),
            item.rect.height);

        using (new GUIStateScope(item.progress, inputEnabled))
        {
            GUI.Label(titleRect, title, styles.Title);
            return GUI.Button(closeRect, closeLabel, styles.Button);
        }
    }

    public void Label(string text, bool changed = false)
    {
        AnimatedItem item = NextRow();
        using (new GUIStateScope(item.progress, false))
        {
            GUI.Label(item.rect, text, changed ? styles.ChangedLabel : styles.Label);
        }
    }

    public void Message(string text)
    {
        AnimatedItem item = NextRow(config.messageHeight);
        using (new GUIStateScope(item.progress, false))
        {
            GUI.Label(item.rect, text, styles.DialogMessage);
        }
    }

    public void Slider(
        string label,
        ref float value,
        bool changed,
        float minimum = 0f,
        float maximum = 1f,
        Func<float, string> formatter = null)
    {
        AnimatedItem item = NextRow();
        SplitSliderRow(item.rect, out Rect labelRect, out Rect sliderRect, out Rect valueRect);

        using (new GUIStateScope(item.progress, inputEnabled))
        {
            GUI.Label(labelRect, label, changed ? styles.ChangedLabel : styles.Label);
            value = GUI.HorizontalSlider(sliderRect, value, minimum, maximum);
            string valueText = formatter != null
                ? formatter(value)
                : Mathf.RoundToInt(value * 100f) + "%";
            GUI.Label(valueRect, valueText, styles.Value);
        }
    }

    public void Slider(
        string label,
        ref float value,
        float appliedValue,
        float minimum = 0f,
        float maximum = 1f,
        Func<float, string> formatter = null)
    {
        Slider(
            label,
            ref value,
            !Mathf.Approximately(value, appliedValue),
            minimum,
            maximum,
            formatter);
    }

    public void CheckboxChanged(
        string label,
        ref bool value,
        bool changed,
        string enabledLabel = "Enabled",
        string disabledLabel = "Disabled")
    {
        AnimatedItem item = NextRow();
        float labelWidth = GetLabelWidth(item.rect.width);
        Rect labelRect = new Rect(item.rect.x, item.rect.y, labelWidth, item.rect.height);
        Rect toggleRect = new Rect(
            item.rect.xMax - config.toggleWidth,
            item.rect.y,
            config.toggleWidth,
            item.rect.height);

        using (new GUIStateScope(item.progress, inputEnabled))
        {
            GUI.Label(labelRect, label, changed ? styles.ChangedLabel : styles.Label);
            value = GUI.Toggle(toggleRect, value, value ? enabledLabel : disabledLabel);
        }
    }

    public void Checkbox(
        string label,
        ref bool value,
        bool appliedValue,
        string enabledLabel = "Enabled",
        string disabledLabel = "Disabled")
    {
        CheckboxChanged(label, ref value, value != appliedValue, enabledLabel, disabledLabel);
    }

    public bool Button(string label, bool enabled = true)
    {
        AnimatedItem item = NextRow(config.buttonHeight);
        using (new GUIStateScope(item.progress, inputEnabled && enabled))
        {
            return GUI.Button(item.rect, label, styles.Button);
        }
    }

    public ButtonPairResult ButtonPair(
        string leftLabel,
        string rightLabel,
        bool leftEnabled = true,
        bool rightEnabled = true)
    {
        Rect row = TakeRow(config.buttonHeight, config.rowSpacing);
        float buttonWidth = Mathf.Max(1f, (row.width - config.buttonPairGap) * 0.5f);
        Rect leftRect = new Rect(row.x, row.y, buttonWidth, row.height);
        Rect rightRect = new Rect(row.xMax - buttonWidth, row.y, buttonWidth, row.height);

        AnimatedItem left = Animate(leftRect);
        AnimatedItem right = Animate(rightRect);

        bool leftPressed;
        bool rightPressed;

        using (new GUIStateScope(left.progress, inputEnabled && leftEnabled))
        {
            leftPressed = GUI.Button(left.rect, leftLabel, styles.Button);
        }

        using (new GUIStateScope(right.progress, inputEnabled && rightEnabled))
        {
            rightPressed = GUI.Button(right.rect, rightLabel, styles.Button);
        }

        if (leftPressed) return ButtonPairResult.Left;
        if (rightPressed) return ButtonPairResult.Right;
        return ButtonPairResult.None;
    }

    public void Space(float height)
    {
        currentY += Mathf.Max(0f, height);
    }

    private AnimatedItem NextRow()
    {
        return NextRow(config.rowHeight);
    }

    private AnimatedItem NextRow(float height)
    {
        return Animate(TakeRow(height, config.rowSpacing));
    }

    private Rect TakeRow(float height, float spacing)
    {
        Rect rect = new Rect(contentRect.x, currentY, contentRect.width, height);
        currentY += height + spacing;
        return rect;
    }

    private AnimatedItem Animate(Rect rect)
    {
        float progress = animation.GetProgress(itemIndex, guiTime);
        itemIndex++;
        rect.x -= (1f - progress) * config.horizontalOffset;
        return new AnimatedItem(rect, progress);
    }

    private void SplitSliderRow(
        Rect row,
        out Rect labelRect,
        out Rect sliderRect,
        out Rect valueRect)
    {
        float labelWidth = GetLabelWidth(row.width);
        labelRect = new Rect(row.x, row.y, labelWidth, row.height);
        valueRect = new Rect(row.xMax - config.valueWidth, row.y, config.valueWidth, row.height);

        float sliderX = labelRect.xMax + config.columnGap;
        float sliderWidth = Mathf.Max(1f, valueRect.x - config.columnGap - sliderX);
        sliderRect = new Rect(
            sliderX,
            row.y + config.sliderVerticalOffset,
            sliderWidth,
            config.sliderHeight);
    }

    private float GetLabelWidth(float availableWidth)
    {
        return Mathf.Clamp(
            availableWidth * config.labelWidthRatio,
            config.minimumLabelWidth,
            Mathf.Min(config.maximumLabelWidth, availableWidth));
    }
}
