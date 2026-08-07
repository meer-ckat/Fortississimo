using System;
using UnityEngine;

public sealed class SettingsManager : MonoBehaviour, IGUIWindow
{
    [Header("Reusable IMGUI Configuration")]
    [SerializeField] private AnimatedWindowConfig windowConfig = default;
    [SerializeField] private AnimatedWindowConfig confirmWindowConfig = default;
    [SerializeField] private AnimatedListingConfig listingConfig = default;
    [SerializeField] private WindowLayoutConfig layoutConfig = default;
    [SerializeField] private DialogLayoutConfig dialogLayoutConfig = default;
    [SerializeField] private IMGUITheme theme = default;

    [Header("Optional UGUI Raycast Blocker")]
    [Tooltip("Handed to GUIWindowStack, which owns it for every window. " +
             "Move to a dedicated host component once a second screen exists.")]
    [SerializeField] private CanvasGroup uguiInputBlocker;

    private AnimatedWindow settingsWindow;
    private ConfirmDialog confirmDialog;
    private AnimatedListingStyles styles;
    private readonly GUIScrollState bodyScroll = new GUIScrollState();
    private SettingsData appliedSettings;
    private SettingsData editingSettings;

    public event Action<SettingsData> SettingsApplied;

    public bool IsVisible => settingsWindow != null && settingsWindow.IsVisible;
    public bool IsModal => false;
    public Color ModalDimColor => default; // unused: IsModal is false
    public bool HasUnsavedChanges => !editingSettings.Matches(appliedSettings);
    public SettingsData AppliedSettings => appliedSettings;

    // Editor: populate a freshly added component. Runtime: repair serialized data
    // that is still zeroed. Same value list, one place.
    private void Reset()
    {
        ApplyConfigurationDefaults(overwriteAll: true);
    }

    private void Awake()
    {
        ApplyConfigurationDefaults(overwriteAll: false);

        appliedSettings = SettingsData.CreateDefault();
        editingSettings = appliedSettings;

        settingsWindow = new AnimatedWindow(windowConfig, listingConfig);
        confirmDialog = new ConfirmDialog(
            confirmWindowConfig,
            listingConfig,
            dialogLayoutConfig,
            theme);
    }

    private void OnEnable()
    {
        GUIWindowStack.SetInputBlocker(uguiInputBlocker);
        GUIWindowStack.Add(this);
        GUIWindowStack.Add(confirmDialog);
    }

    private void OnDisable()
    {
        GUIWindowStack.Remove(confirmDialog);
        GUIWindowStack.Remove(this);
        confirmDialog?.Dispose();
        settingsWindow?.Dispose();
    }

    public void OpenSettings()
    {
        if (settingsWindow == null || settingsWindow.IsVisible)
        {
            return;
        }

        editingSettings = appliedSettings;
        bodyScroll.Reset();
        settingsWindow.Open(WindowLayout.LeftAligned(layoutConfig), GUIFrameClock.Capture());
        GUIWindowStack.BringToFront(this);
    }

    public void RequestClose()
    {
        if (settingsWindow == null || !settingsWindow.IsOpen || confirmDialog.IsVisible)
        {
            return;
        }

        if (!HasUnsavedChanges)
        {
            settingsWindow.Close(OnSettingsClosed);
            return;
        }

        confirmDialog.Open(
            SettingsPanelRect,
            "변경 사항이 있습니다.",
            DiscardAndClose,
            null,
            "휘발시키기",
            "보존하기");
    }

    public void ApplySettings()
    {
        if (settingsWindow == null || !settingsWindow.IsOpen ||
            confirmDialog.IsVisible || !HasUnsavedChanges)
        {
            return;
        }

        appliedSettings = editingSettings;
        AudioListener.volume = appliedSettings.masterVolume;
        Screen.fullScreen = appliedSettings.fullscreen;
        SettingsApplied?.Invoke(appliedSettings);
    }

    public void DrawWindow(double guiTime, bool advanceLifecycle, bool inputEnabled)
    {
        if (settingsWindow == null || !settingsWindow.IsVisible)
        {
            return;
        }

        settingsWindow.BeginFrame(WindowLayout.LeftAligned(layoutConfig), guiTime);
        if (!settingsWindow.TryGetRect(out Rect panelRect))
        {
            return;
        }

        EnsureStyles();
        IMGUIDrawing.Panel(panelRect, styles.Panel, theme.panelTint);
        DrawSettingsContent(panelRect, guiTime, inputEnabled);

        // Last: the content phase length depends on the item count recorded above.
        if (advanceLifecycle)
        {
            settingsWindow.EndFrame(guiTime);
        }
    }

    public void NotifyCancelPressed()
    {
        RequestClose();
    }

    private Rect SettingsPanelRect()
    {
        return settingsWindow.TryGetRect(out Rect rect)
            ? rect
            : WindowLayout.LeftAligned(layoutConfig);
    }

    private void DrawSettingsContent(Rect panelRect, double guiTime, bool inputEnabled)
    {
        Rect contentRect = WindowLayout.Content(panelRect, layoutConfig.contentPadding);
        WindowAnimationSnapshot animation = settingsWindow.GetAnimationSnapshot();
        bool controlsEnabled = settingsWindow.InputEnabled && inputEnabled;

        // The title bar stays outside the scroll view so the close button cannot
        // scroll out of reach.
        Rect headerRect = new Rect(
            contentRect.x,
            contentRect.y,
            contentRect.width,
            listingConfig.titleHeight + listingConfig.sectionSpacing);

        AnimatedListing header = new AnimatedListing(
            headerRect, animation, listingConfig, styles, controlsEnabled, guiTime);

        if (header.TitleBar("Settings"))
        {
            RequestClose();
        }

        Rect bodyRect = new Rect(
            contentRect.x,
            headerRect.yMax,
            contentRect.width,
            Mathf.Max(1f, contentRect.yMax - headerRect.yMax));

        using (GUIScrollView.Scope scroll =
               GUIScrollView.Begin(bodyRect, bodyScroll, listingConfig.scrollbarWidth))
        {
            // startIndex continues the stagger from the header rather than
            // restarting it, so the title and the first row do not animate together.
            AnimatedListing body = new AnimatedListing(
                scroll.ViewRect,
                animation,
                listingConfig,
                styles,
                controlsEnabled,
                guiTime,
                startIndex: header.NextIndex,
                clipped: scroll.IsScrolling);

            body.Slider("전체", ref editingSettings.masterVolume, appliedSettings.masterVolume);
            body.Slider("음악", ref editingSettings.musicVolume, appliedSettings.musicVolume);
            body.Slider("효과음", ref editingSettings.sfxVolume, appliedSettings.sfxVolume);
            body.Checkbox(
                "사회적 거리두기 mk 67",
                ref editingSettings.screenShake,
                appliedSettings.screenShake);
            body.Checkbox(
                "강남스타일",
                ref editingSettings.fullscreen,
                appliedSettings.fullscreen);

            if (body.Button("Apply Settings", HasUnsavedChanges))
            {
                ApplySettings();
            }

            bodyScroll.SetContentHeight(body.ContentHeight);
            settingsWindow.RecordItemCount(body.NextIndex);
        }
    }

    private void DiscardAndClose()
    {
        editingSettings = appliedSettings;
        settingsWindow.Close(OnSettingsClosed);
    }

    private void OnSettingsClosed()
    {
        editingSettings = appliedSettings;
    }

    private void EnsureStyles()
    {
        if (styles == null)
        {
            styles = AnimatedListingStyles.Create(theme);
        }
    }

    private void ApplyConfigurationDefaults(bool overwriteAll)
    {
        if (overwriteAll || !windowConfig.IsUsable)
        {
            windowConfig = AnimatedWindowConfig.Default;
        }

        if (overwriteAll || !confirmWindowConfig.IsUsable)
        {
            confirmWindowConfig = AnimatedWindowConfig.ConfirmDefault;
        }

        listingConfig = overwriteAll ? AnimatedListingConfig.Default : listingConfig.Resolved;
        layoutConfig = overwriteAll ? WindowLayoutConfig.SettingsDefault : layoutConfig.Resolved;
        dialogLayoutConfig = overwriteAll ? DialogLayoutConfig.Default : dialogLayoutConfig.Resolved;
        theme = overwriteAll ? IMGUITheme.Default : theme.Resolved;
    }
}
