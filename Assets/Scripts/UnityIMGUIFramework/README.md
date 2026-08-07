# Unity IMGUI Framework replacement

This folder is a complete replacement set for the agreed Unity 6.3 IMGUI architecture.
Copy its subfolders into a project folder such as `Assets/Scripts/UI/IMGUI/`.

## Structure

```text
Core/
  GUIFrameClock.cs       One stable unscaled timestamp per rendered frame
  GUIHost.cs             Central OnGUI entry point, drives the stack, owns Escape
  GUIWindowStack.cs      Ordering, input gating, modal dim, UGUI blocker
  IGUIWindow.cs          What the stack needs from a window
  GUIStateScope.cs       Safe GUI color/enabled restoration
  IMGUIEase.cs           TweenMethod enum and shared easing implementation

Configuration/
  IMGUIConfiguration.cs  All five serialized config structs

Layout/
  GUIScrollView.cs       Overflow-only scroll view + retained GUIScrollState
  IMGUIDrawing.cs
  WindowLayout.cs

Windows/
  AnimatedWindow.cs
  ConfirmDialog.cs
  WindowAnimationSnapshot.cs

Widgets/
  AnimatedListing.cs
  AnimatedListingStyles.cs

Screens/Settings/
  SettingsData.cs
  SettingsManager.cs
```

## The window stack

`GUIWindowStack` holds every live window bottom to top. Anything that is a decision
*between* windows lives there, not in a screen:

| Decision | Rule |
|---|---|
| Draw order | Stack order. `BringToFront` on open. |
| Input | Everything below the topmost modal is gated off. |
| Modal dim | One pass, behind the topmost modal only. |
| Escape | Topmost visible window, then consumed. |
| UGUI blocker | On while any window is visible. |

A screen implements `IGUIWindow` and answers only for itself. It never calls `Draw`
on another window and never checks whether another window is open in order to
disable its own controls — `inputEnabled` arrives as a parameter.

Adding a second screen is `GUIWindowStack.Add(this)` in `OnEnable` and `Remove` in
`OnDisable`. Nothing in the existing screens changes.

## Scene setup

1. Add `SettingsManager` to a persistent scene object.
2. Connect an existing UGUI button's `onClick` to `SettingsManager.OpenSettings`.
3. Optionally create a separate full-screen UGUI object with a transparent `Image`
   whose `Raycast Target` is enabled. Add a `CanvasGroup` and assign it to
   `uguiInputBlocker`. Keep this blocker separate from the object containing
   `SettingsManager`, because the stack activates and deactivates it. The field is
   on `SettingsManager` only until a second screen exists; it is handed straight to
   `GUIWindowStack.SetInputBlocker` and belongs on a dedicated host component.
4. `GUIHost` is created automatically. A manually placed `GUIHost` is also supported.

`Reset()` supplies all default layout, animation, and theme values. Existing components
whose serialized configuration is still zeroed are repaired by `Awake()`.

## Settings integration

`ApplySettings()` directly applies master volume and fullscreen. Connect game-specific
music, SFX, screen-shake, persistence, and audio-mixer behavior through the
`SettingsApplied` event or replace the body with the project's existing services.

The UI calls remain deliberately explicit in `DrawSettingsContent()`. Adding a setting
does not require a row index, Y position, GUI id, animation list, or removal list.

## Scrolling

`GUIScrollView.Begin` pushes a clipping region **only when the content actually
overflows**. A scroll view clips to its own rect, which would cut the row entry
animation off at the left edge, so:

| Content | Clipping | Row reveal |
|---|---|---|
| Fits | none pushed | slides in from outside the panel, unchanged |
| Overflows | scroll view | fade only (`clipped: true` drops the horizontal offset) |

Content height is measured during the draw and applied on the next frame, the same
bargain `AnimatedWindow` makes with its item count — a cursor layout cannot know its
extent until the calls have run. One stale frame only affects the scrollbar range.

`GUIScrollState` is retained, so it lives on the screen, not on the per-frame
listing. Call `Reset()` when the window opens.

Split a window across several listings by passing the previous one's `NextIndex` as
the next one's `startIndex`; the stagger then runs continuously instead of restarting
at 0. The settings screen uses this to keep the title bar outside the scroll view so
the close button cannot scroll out of reach.

With the shipped defaults the settings body view is 522px and its six rows measure
328px, so three more rows fit before it begins to scroll.

## Animation behavior

Opening is:

```text
panel from off-screen -> title -> rows in call order -> Apply
```

Closing is:

```text
Apply -> rows in reverse call order -> title -> panel off-screen
```

The confirmation dialog uses the same system and animates message, left button, then
right button. Time is derived from `Time.unscaledTimeAsDouble`; it is never accumulated
inside `OnGUI()`.

## Per-frame contract

`AnimatedWindow` owns its own rect and position tween; there is no global registry.
Callers drive it in this order, and the order matters:

```text
1. BeginFrame(targetRect, guiTime)   advances the position tween, never calls back
2. TryGetRect(out panelRect)         pure read, safe on every GUI event
3. draw content, then RecordItemCount(n)
4. EndFrame(guiTime)                 Repaint only, advances the state machine
```

`EndFrame` runs last on purpose. The content phase length is derived from the item
count, and the item count is only known once the listing has been laid out this
frame. Ticking first would always use the previous frame's count.

All timing flows from `GUIFrameClock.Capture()`, so every animation in a frame shares
one timestamp. Nothing reads `Time.unscaledTimeAsDouble` directly.

## Replacement note

This is a replacement set, so remove or rename older global definitions of
`GUIHost`, `TweenMethod`, `AnimatedWindow`, and related classes before copying these
files. If the project uses namespaces, place the entire set in the same project
namespace rather than mixing global and namespaced types.

## Validation performed

All files compile clean (0 errors) against a minimal Unity API compatibility stub on
.NET SDK 8.0. A headless harness drives that stub and asserts 39 properties:

*Window lifecycle* - the panel starts off-screen and settles exactly on its target
rect; the open sequence takes `openDuration + contentStartDelay +
SequenceDuration(itemCount)` off the current frame's item count; `TryGetRect` has no
side effects and never fires the close callback; the close callback fires exactly once.

*Stack* - windows draw bottom to top and `BringToFront` reorders; hidden windows are
skipped; the modal dim is drawn exactly once even with two modals stacked; everything
below the topmost modal loses input while siblings without a modal keep it; Escape
reaches only the topmost window, falls through when it hides, and is not consumed by
an empty stack; a window may close another mid-draw without invalidating iteration;
the UGUI blocker follows stack occupancy and a raycast-on scene blocker is forced off
when handed over.

*Scrolling* - measured content height matches the row arithmetic; no clipping region
is pushed while the content fits, and `Dispose` balances `BeginScrollView` when one
is; the view tracks content height, reserves the scrollbar gutter, uses local
coordinates, and falls back to a 16px gutter for configs serialized before
`scrollbarWidth` existed; `Reset` returns to the top; a clipped row keeps its x while
an unclipped one slides in; and a listing given `startIndex` is demonstrably later in
the stagger than one that restarts at 0.

Unity import, Console, Play Mode, resolution, and interaction validation still need to
be run in the target project.
