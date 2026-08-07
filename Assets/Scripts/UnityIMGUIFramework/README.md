# Unity IMGUI Framework replacement

This folder is a complete replacement set for the agreed Unity 6.3 IMGUI architecture.
Copy its subfolders into a project folder such as `Assets/Scripts/UI/IMGUI/`.

## Structure

```text
Core/
  GUIFrameClock.cs       One stable unscaled timestamp per rendered frame
  GUIHost.cs             Central OnGUI entry point
  GUIStateScope.cs       Safe GUI color/enabled restoration
  IMGUIEase.cs           TweenMethod enum and shared easing implementation

Configuration/
  IMGUIConfiguration.cs  All five serialized config structs

Layout/
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

## Scene setup

1. Add `SettingsManager` to a persistent scene object.
2. Connect an existing UGUI button's `onClick` to `SettingsManager.OpenSettings`.
3. Optionally create a separate full-screen UGUI object with a transparent `Image`
   whose `Raycast Target` is enabled. Add a `CanvasGroup` and assign it to
   `uguiInputBlocker`. Keep this blocker separate from the object containing
   `SettingsManager`, because the manager activates and deactivates it.
4. `GUIHost` is created automatically. A manually placed `GUIHost` is also supported.

`Reset()` supplies all default layout, animation, and theme values. Existing components
whose serialized configuration is still zeroed are repaired by `Awake()`.

## Settings integration

`ApplySettings()` directly applies master volume and fullscreen. Connect game-specific
music, SFX, screen-shake, persistence, and audio-mixer behavior through the
`SettingsApplied` event or replace the body with the project's existing services.

The UI calls remain deliberately explicit in `DrawSettingsContent()`. Adding a setting
does not require a row index, Y position, GUI id, animation list, or removal list.

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
.NET SDK 8.0. A headless harness additionally simulates the full open/close lifecycle
against that stub and asserts: the panel settles exactly on its target rect, the open
sequence takes `panelSlide + contentStartDelay + SequenceDuration(itemCount)` using the
current frame's item count, `TryGetRect` is free of side effects, the close callback
fires exactly once and never from a getter, a zero-item window still opens, and an open
window tracks a moved target rect.

Unity import, Console, Play Mode, resolution, and interaction validation still need to
be run in the target project.
