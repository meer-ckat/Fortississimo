using System;
using UnityEngine;

public sealed class SettingsManager : MonoBehaviour
{
    [Header("Reusable IMGUI Configuration")]
    [SerializeField] private AnimatedWindowConfig windowConfig = default;
    [SerializeField] private AnimatedWindowConfig confirmWindowConfig = default;
    [SerializeField] private AnimatedListingConfig listingConfig = default;
    [SerializeField] private WindowLayoutConfig layoutConfig = default;
    [SerializeField] private DialogLayoutConfig dialogLayoutConfig = default;
    [SerializeField] private IMGUITheme theme = default;

    [Header("Optional UGUI Raycast Blocker")]
    [SerializeField] private CanvasGroup uguiInputBlocker;

    private AnimatedWindow settingsWindow;
    private ConfirmDialog confirmDialog;
    private AnimatedListingStyles styles;
    private SettingsData appliedSettings;
    private SettingsData editingSettings;

    public event Action<SettingsData> SettingsApplied;

    public bool IsVisible => settingsWindow != null && settingsWindow.IsVisible;
    public bool HasUnsavedChanges => !editingSettings.Matches(appliedSettings);
    public SettingsData AppliedSettings => appliedSettings;

    private void Reset()
    {
        windowConfig = AnimatedWindowConfig.Default;
        confirmWindowConfig = AnimatedWindowConfig.ConfirmDefault;
        listingConfig = AnimatedListingConfig.Default;
        layoutConfig = WindowLayoutConfig.SettingsDefault;
        dialogLayoutConfig = DialogLayoutConfig.Default;
        theme = IMGUITheme.Default;
    }

    private void Awake()
    {
        EnsureConfiguration();
        appliedSettings = SettingsData.CreateDefault();
        editingSettings = appliedSettings;
        settingsWindow = new AnimatedWindow(this, "설정", windowConfig, listingConfig);
        confirmDialog = new ConfirmDialog(
            this,
            confirmWindowConfig,
            listingConfig,
            dialogLayoutConfig,
            theme);
        SetUGUIBlocked(false);
    }

    private void OnEnable()
    {
        GUIHost.Register(DrawGUI);
    }

    private void OnDisable()
    {
        GUIHost.Unregister(DrawGUI);
        confirmDialog?.Dispose();
        settingsWindow?.Dispose();
        SetUGUIBlocked(false);
    }

    public void OpenSettings()
    {
        if (settingsWindow == null || settingsWindow.IsVisible)
        {
            return;
        }

        editingSettings = appliedSettings;
        settingsWindow.Open(WindowLayout.LeftAligned(layoutConfig), GUIFrameClock.Capture());
        SetUGUIBlocked(true);
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

        if (settingsWindow.TryGetRect(out Rect parentRect))
        {
            confirmDialog.Open(
                parentRect,
                "변경 사항이 있습니다.",
                DiscardAndClose,
                null,
                "휘발시키기",
                "보존하기");
        }
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

    private void DrawGUI()
    {
        if (settingsWindow == null || !settingsWindow.IsVisible)
        {
            return;
        }

        double guiTime = GUIFrameClock.Capture();
        bool advanceLifecycle = Event.current.type == UnityEngine.EventType.Repaint;
        Rect targetRect = WindowLayout.LeftAligned(layoutConfig);

        if (advanceLifecycle)
        {
            settingsWindow.Tick(targetRect, guiTime);
        }

        if (!settingsWindow.IsVisible || !settingsWindow.TryGetRect(out Rect panelRect))
        {
            SetUGUIBlocked(false);
            return;
        }

        EnsureStyles();
        IMGUIDrawing.Panel(panelRect, styles.Panel, theme.panelTint);
        DrawSettingsContent(panelRect, guiTime);
        confirmDialog.Draw(panelRect, guiTime, advanceLifecycle);
        SetUGUIBlocked(settingsWindow.IsVisible || confirmDialog.IsVisible);
    }

    private void DrawSettingsContent(Rect panelRect, double guiTime)
    {
        Rect contentRect = WindowLayout.Content(panelRect, layoutConfig.contentPadding);
        AnimatedListing listing = new AnimatedListing(
            contentRect,
            settingsWindow.GetAnimationSnapshot(),
            listingConfig,
            styles,
            settingsWindow.InputEnabled && !confirmDialog.IsVisible,
            guiTime);

        if (listing.TitleBar("Settings"))
        {
            RequestClose();
        }

        listing.Slider(
            "전체",
            ref editingSettings.masterVolume,
            appliedSettings.masterVolume);
        listing.Slider(
            "음악",
            ref editingSettings.musicVolume,
            appliedSettings.musicVolume);
        listing.Slider(
            "효과음",
            ref editingSettings.sfxVolume,
            appliedSettings.sfxVolume);
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
        SetUGUIBlocked(false);
    }

    private void EnsureStyles()
    {
        if (styles == null)
        {
            styles = AnimatedListingStyles.Create(theme);
        }
    }

    private void EnsureConfiguration()
    {
        if (!windowConfig.IsUsable) windowConfig = AnimatedWindowConfig.Default;
        if (!confirmWindowConfig.IsUsable) confirmWindowConfig = AnimatedWindowConfig.ConfirmDefault;
        if (!listingConfig.IsUsable) listingConfig = AnimatedListingConfig.Default;
        if (!layoutConfig.IsUsable) layoutConfig = WindowLayoutConfig.SettingsDefault;
        if (!dialogLayoutConfig.IsUsable) dialogLayoutConfig = DialogLayoutConfig.Default;
        if (!theme.IsUsable) theme = IMGUITheme.Default;
    }

    private void SetUGUIBlocked(bool blocked)
    {
        if (uguiInputBlocker == null)
        {
            return;
        }

        GameObject blockerObject = uguiInputBlocker.gameObject;
        if (blockerObject.activeSelf != blocked)
        {
            blockerObject.SetActive(blocked);
        }

        uguiInputBlocker.blocksRaycasts = blocked;
        uguiInputBlocker.interactable = blocked;
    }
}
