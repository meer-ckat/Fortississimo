using UnityEngine;

/// <summary>
/// A window the stack can order, gate input for, and route Escape to.
///
/// The stack owns everything that is a decision *between* windows — draw order,
/// who receives input, who draws the modal dim, who handles Escape, whether the
/// UGUI raycast blocker is on. An implementation owns only its own contents.
/// </summary>
public interface IGUIWindow
{
    /// <summary>Skipped entirely by the stack while false.</summary>
    bool IsVisible { get; }

    /// <summary>
    /// True if this window blocks the ones beneath it. The stack draws one dim
    /// pass behind the topmost modal and disables input for everything below it.
    /// </summary>
    bool IsModal { get; }

    /// <summary>Dim colour behind this window. Read only when IsModal is true.</summary>
    Color ModalDimColor { get; }

    /// <param name="inputEnabled">
    /// False when a modal sits above this window. Implementations must forward this
    /// into their controls rather than deciding for themselves.
    /// </param>
    void DrawWindow(double guiTime, bool advanceLifecycle, bool inputEnabled);

    /// <summary>Escape was pressed and this window was topmost.</summary>
    void NotifyCancelPressed();
}
