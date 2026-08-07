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
        AnimatedListing listing = new AnimatedListing(
            contentRect,
            settingsWindow.GetAnimationSnapshot(),
            listingConfig,
            styles,
            settingsWindow.InputEnabled && inputEnabled,
            guiTime);

        if (listing.TitleBar("Settings"))
        {
            RequestClose();
        }

        listing.Slider("전체", ref editingSettings.masterVolume, appliedSettings.masterVolume);
        listing.Slider("음악", ref editingSettings.musicVolume, appliedSettings.musicVolume);
        listing.Slider("효과음", ref editingSettings.sfxVolume, appliedSettings.sfxVolume);
        listing.Checkbox(
            "사회적 거리두기 mk 67",
            ref editingSettings.screenShake,
            appliedSettings.screenShake);
        listing.Checkbox(
            "강남스타일",
            ref editingSettings.fullscreen,
            appliedSettings.fullscreen);

        if (listing.Button("Apply Settings", HasUnsavedChanges))
        {
            ApplySettings();
        }

        settingsWindow.RecordItemCount(listing.ItemCount);
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
