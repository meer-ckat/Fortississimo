# Unity IMGUI Framework replacement

This folder is a complete replacement set for the agreed Unity 6.3 IMGUI architecture.
Copy its subfolders into a project folder such as `Assets/Scripts/UI/IMGUI/`.

## Structure

```text
Core/
  GUIFrameClock.cs       One stable unscaled timestamp per rendered frame
  GUIHost.cs             Central OnGUI entry point
  GUIManager.cs          Low-level Rect registry and position tweening
  GUIStateScope.cs       Safe GUI color/enabled restoration
  IMGUIEase.cs           Shared easing implementation
  TweenMethod.cs

Configuration/
  AnimatedListingConfig.cs
  AnimatedWindowConfig.cs
  DialogLayoutConfig.cs
  IMGUITheme.cs
  WindowLayoutConfig.cs

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

## Replacement note

This is a replacement set, so remove or rename older global definitions of
`GUIHost`, `GUIManager`, `TweenMethod`, `AnimatedWindow`, and related classes before
copying these files. If the project uses namespaces, place the entire set in the same
project namespace rather than mixing global and namespaced types.

## Validation performed

All files were compiled together against a minimal Unity API compatibility stub with
.NET SDK 10.0. No C# errors remained. The actual Unity project was not present in the
workspace, so Unity import, Console, Play Mode, resolution, and interaction validation
still need to be run in the target project.
