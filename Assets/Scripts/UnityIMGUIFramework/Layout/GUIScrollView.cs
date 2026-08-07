using System;
using UnityEngine;

/// <summary>
/// Scroll position for one view. Retained across frames, so it lives on the screen
/// rather than on the per-frame AnimatedListing.
///
/// Content height is measured during the draw and used on the next frame, the same
/// deal AnimatedWindow makes with its item count: a cursor layout cannot know its
/// own extent until the calls have run. One frame of staleness only affects the
/// scrollbar range, and it self-corrects before the user can act on it.
/// </summary>
public sealed class GUIScrollState
{
    public Vector2 position;

    public float ContentHeight { get; private set; }

    public void SetContentHeight(float height)
    {
        ContentHeight = Mathf.Max(0f, height);
    }

    public bool Overflows(float viewHeight)
    {
        return ContentHeight > viewHeight + 0.5f;
    }

    /// <summary>Call when the owning window opens, so it reopens at the top.</summary>
    public void Reset()
    {
        position = Vector2.zero;
    }
}

public static class GUIScrollView
{
    /// <summary>
    /// Begins a scroll view only when the content actually overflows.
    ///
    /// When it fits, no clipping region is pushed at all and rows keep sliding in
    /// from outside the panel. A scroll view clips to its own rect, which would cut
    /// that entry animation off at the left edge — so when scrolling is active the
    /// listing is told to drop the horizontal offset and reveal by fade alone.
    /// </summary>
    public static Scope Begin(Rect outRect, GUIScrollState state, float scrollbarWidth)
    {
        if (state == null || !state.Overflows(outRect.height))
        {
            return new Scope(outRect, false);
        }

        // scrollbarWidth was added after the config struct shipped, so a component
        // serialized before then deserialises it as 0. Fall back rather than let the
        // content run under the scrollbar.
        float gutter = scrollbarWidth > 0f ? scrollbarWidth : 16f;

        Rect viewRect = new Rect(
            0f,
            0f,
            Mathf.Max(1f, outRect.width - gutter),
            state.ContentHeight);

        state.position = GUI.BeginScrollView(outRect, state.position, viewRect);
        return new Scope(viewRect, true);
    }

    public readonly struct Scope : IDisposable
    {
        /// <summary>Lay the listing out in this rect. Local coordinates while scrolling.</summary>
        public readonly Rect ViewRect;

        /// <summary>True when a clipping scroll view was actually pushed.</summary>
        public readonly bool IsScrolling;

        internal Scope(Rect viewRect, bool isScrolling)
        {
            ViewRect = viewRect;
            IsScrolling = isScrolling;
        }

        public void Dispose()
        {
            if (IsScrolling)
            {
                GUI.EndScrollView();
            }
        }
    }
}
