using System;
using UnityEngine;

public sealed class ConfirmDialog : IDisposable
{
    private readonly AnimatedWindow window;
    private readonly AnimatedListingConfig listingConfig;
    private readonly DialogLayoutConfig layoutConfig;
    private readonly IMGUITheme theme;

    private string message;
    private string yesLabel;
    private string noLabel;
    private Action yesAction;
    private Action noAction;
    private AnimatedListingStyles styles;

    public bool IsVisible => window.IsVisible;

    public ConfirmDialog(
        AnimatedWindowConfig windowConfig,
        AnimatedListingConfig listingConfig,
        DialogLayoutConfig layoutConfig,
        IMGUITheme theme)
    {
        this.listingConfig = listingConfig.Resolved;
        this.layoutConfig = layoutConfig.Resolved;
        this.theme = theme.Resolved;
        window = new AnimatedWindow(windowConfig, this.listingConfig);
    }

    public void Open(
        Rect parentRect,
        string message,
        Action onYes,
        Action onNo = null,
        string yesLabel = "Yes",
        string noLabel = "No")
    {
        if (window.IsVisible)
        {
            return;
        }

        this.message = message ?? string.Empty;
        this.yesLabel = yesLabel ?? string.Empty;
        this.noLabel = noLabel ?? string.Empty;
        yesAction = onYes;
        noAction = onNo;
        window.Open(WindowLayout.CenteredDialog(parentRect, layoutConfig), GUIFrameClock.Capture());
    }

    public void Draw(Rect parentRect, double guiTime, bool advanceLifecycle)
    {
        if (!window.IsVisible)
        {
            return;
        }

        window.BeginFrame(WindowLayout.CenteredDialog(parentRect, layoutConfig), guiTime);
        if (!window.TryGetRect(out Rect panelRect))
        {
            return;
        }

        EnsureStyles();
        IMGUIDrawing.ModalDim(theme.modalDimColor);
        IMGUIDrawing.Panel(panelRect, styles.Panel, theme.panelTint);

        Rect contentRect = WindowLayout.Content(panelRect, layoutConfig.contentPadding);
        AnimatedListing listing = new AnimatedListing(
            contentRect,
            window.GetAnimationSnapshot(),
            listingConfig,
            styles,
            window.InputEnabled,
            guiTime);

        listing.Message(message);
        ButtonPairResult result = listing.ButtonPair(yesLabel, noLabel);
        window.RecordItemCount(listing.ItemCount);

        if (advanceLifecycle)
        {
            window.EndFrame(guiTime);
        }

        if (result == ButtonPairResult.Left)
        {
            Action callback = yesAction;
            ClearCallbacks();
            window.Close(callback);
        }
        else if (result == ButtonPairResult.Right)
        {
            Action callback = noAction;
            ClearCallbacks();
            window.Close(callback);
        }
    }

    public void Dispose()
    {
        ClearCallbacks();
        window.Dispose();
    }

    private void EnsureStyles()
    {
        if (styles == null)
        {
            styles = AnimatedListingStyles.Create(theme);
        }
    }

    private void ClearCallbacks()
    {
        yesAction = null;
        noAction = null;
    }
}
