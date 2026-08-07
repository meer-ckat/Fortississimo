using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ordered set of live windows, bottom to top. Replaces the flat draw-callback
/// list: with a stack, "which window is on top" is a single answer instead of a
/// property each screen had to hand-wire against every other screen.
///
/// Decisions that belong here, not in a screen:
///   - draw order, and bringing a window to front when it opens
///   - which windows receive input (everything below the topmost modal is gated off)
///   - the modal dim, drawn exactly once behind the topmost modal
///   - which window receives Escape (the topmost visible one)
///   - the UGUI raycast blocker, on while any window is visible
/// </summary>
public static class GUIWindowStack
{
    private static readonly List<IGUIWindow> Windows = new List<IGUIWindow>();

    // Rebuilt each frame so a window may open or close another mid-draw.
    private static readonly List<IGUIWindow> VisibleBuffer = new List<IGUIWindow>();

    private static CanvasGroup inputBlocker;
    private static bool blockerActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Windows.Clear();
        VisibleBuffer.Clear();
        inputBlocker = null;
        blockerActive = false;
    }

    public static int Count => Windows.Count;

    /// <summary>Optional full-screen CanvasGroup that swallows UGUI raycasts.</summary>
    public static void SetInputBlocker(CanvasGroup blocker)
    {
        inputBlocker = blocker;
        // Force, don't compare: a scene blocker may be authored raycast-on.
        ForceApplyBlocker(false);
    }

    public static void Add(IGUIWindow window)
    {
        if (window == null || Windows.Contains(window))
        {
            return;
        }

        Windows.Add(window);
        GUIHost.EnsureInstance();
    }

    public static bool Remove(IGUIWindow window)
    {
        if (window == null || !Windows.Remove(window))
        {
            return false;
        }

        if (Windows.Count == 0)
        {
            ApplyBlocker(false);
        }

        return true;
    }

    /// <summary>Call when a window opens, so the newest window draws on top.</summary>
    public static void BringToFront(IGUIWindow window)
    {
        if (window == null || !Windows.Remove(window))
        {
            return;
        }

        Windows.Add(window);
    }

    /// <summary>Topmost visible window, or null. This is what Escape targets.</summary>
    public static IGUIWindow Top
    {
        get
        {
            for (int i = Windows.Count - 1; i >= 0; i--)
            {
                if (Windows[i].IsVisible)
                {
                    return Windows[i];
                }
            }

            return null;
        }
    }

    public static bool IsTopmost(IGUIWindow window)
    {
        return window != null && ReferenceEquals(Top, window);
    }

    public static bool AnyVisible => Top != null;

    /// <summary>Routes Escape to the topmost visible window. True if consumed.</summary>
    public static bool NotifyCancelPressed()
    {
        IGUIWindow top = Top;
        if (top == null)
        {
            return false;
        }

        top.NotifyCancelPressed();
        return true;
    }

    public static void DrawAll(double guiTime, bool advanceLifecycle)
    {
        VisibleBuffer.Clear();
        for (int i = 0; i < Windows.Count; i++)
        {
            if (Windows[i].IsVisible)
            {
                VisibleBuffer.Add(Windows[i]);
            }
        }

        // Only the topmost modal dims. Two modals used to composite their dim
        // passes together (0.42 over 0.42 reads as 0.66); now the lower one is
        // simply covered by the upper one's single pass.
        int topModal = -1;
        for (int i = VisibleBuffer.Count - 1; i >= 0; i--)
        {
            if (VisibleBuffer[i].IsModal)
            {
                topModal = i;
                break;
            }
        }

        for (int i = 0; i < VisibleBuffer.Count; i++)
        {
            IGUIWindow window = VisibleBuffer[i];

            if (i == topModal)
            {
                IMGUIDrawing.ModalDim(window.ModalDimColor);
            }

            bool inputEnabled = topModal < 0 || i >= topModal;
            window.DrawWindow(guiTime, advanceLifecycle, inputEnabled);
        }

        ApplyBlocker(VisibleBuffer.Count > 0);
        VisibleBuffer.Clear();
    }

    private static void ApplyBlocker(bool blocked)
    {
        if (blockerActive != blocked)
        {
            ForceApplyBlocker(blocked);
        }
    }

    private static void ForceApplyBlocker(bool blocked)
    {
        blockerActive = blocked;

        if (inputBlocker == null)
        {
            return;
        }

        GameObject blockerObject = inputBlocker.gameObject;
        if (blockerObject.activeSelf != blocked)
        {
            blockerObject.SetActive(blocked);
        }

        inputBlocker.blocksRaycasts = blocked;
        inputBlocker.interactable = blocked;
    }
}
