using System;
using UnityEngine;

/// <summary>
/// Yes/No modal. A stack peer, not a child of whoever opened it — the opener no
/// longer calls Draw on it, and no longer has to gate its own input against it.
/// The anchor is supplied as a delegate so the dialog stays centred on a window
/// whose rect is still animating or being resized.
/// </summary>
public sealed class ConfirmDialog : IGUIWindow, IDisposable
{
    private readonly AnimatedWindow window;
    private readonly AnimatedListingConfig listingConfig;
    private readonly DialogLayoutConfig layoutConfig;
    private readonly IMGUITheme theme;

    private Func<Rect> anchorProvider;
    private string message;
    private string yesLabel;
    private string noLabel;
    private Action yesAction;
    private Action noAction;
    private AnimatedListingStyles styles;

    public bool IsVisible => window.IsVisible;
    public bool IsModal => true;
    public Color ModalDimColor => theme.modalDimColor;

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
        Func<Rect> anchor,
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

        anchorProvider = anchor;
        this.message = message ?? string.Empty;
        this.yesLabel = yesLabel ?? string.Empty;
        this.noLabel = noLabel ?? string.Empty;
        yesAction = onYes;
        noAction = onNo;

        window.Open(TargetRect(), GUIFrameClock.Capture());
        GUIWindowStack.BringToFront(this);
    }

    public void DrawWindow(double guiTime, bool advanceLifecycle, bool inputEnabled)
    {
        if (!window.IsVisible)
        {
            return;
        }

        window.BeginFrame(TargetRect(), guiTime);
        if (!window.TryGetRect(out Rect panelRect))
        {
            return;
        }

        EnsureStyles();
        IMGUIDrawing.Panel(panelRect, styles.Panel, theme.panelTint);

        Rect contentRect = WindowLayout.Content(panelRect, layoutConfig.contentPadding);
        AnimatedListing listing = new AnimatedListing(
            contentRect,
            window.GetAnimationSnapshot(),
            listingConfig,
            styles,
            window.InputEnabled && inputEnabled,
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
            Answer(yesAction);
        }
        else if (result == ButtonPairResult.Right)
        {
            Answer(noAction);
        }
    }

    /// <summary>Escape means "No" — the non-destructive answer.</summary>
    public void NotifyCancelPressed()
    {
        if (window.IsOpen)
        {
            Answer(noAction);
        }
    }

    public void Dispose()
    {
        ClearCallbacks();
        window.Dispose();
    }

    private Rect TargetRect()
    {
        Rect anchor = anchorProvider != null ? anchorProvider() : WindowLayout.ScreenRect();
        return WindowLayout.CenteredDialog(anchor, layoutConfig);
    }

    private void Answer(Action callback)
    {
        ClearCallbacks();
        window.Close(callback);
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
