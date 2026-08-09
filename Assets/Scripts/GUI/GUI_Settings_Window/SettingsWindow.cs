using System.Collections.Generic;
using IMGUI;
using UnityEngine;

public class SettingsWindow : MonoBehaviour
{
    public static SettingsWindow instance;

    struct SettingsData
    {
        public float master, music, sfx;
        public bool gangnam, ukulele;
    }

    SettingsData applied, editing;

    GUIBoxLabel border, confirmBorder;
    GUIGroup window, confirm;

    GUILabel masterLabel, musicLabel, sfxLabel;
    GUISlider masterSlider, musicSlider, sfxSlider;
    GUIToggle gangnamToggle, ukuleleToggle;
    GUIButton closeButton, applyButton;

    readonly List<GUIItem> animated = new();
    readonly List<GUIItem> confirmAnimated = new();
    readonly Dictionary<GUIItem, Vector2> home = new();

    GUIStyle windowStyle, borderStyle;
    GUIStyle labelStyle, dirtyLabelStyle;
    GUIStyle buttonStyle, applyStyle, dirtyApplyStyle;
    GUIStyle toggleStyle, dirtyToggleStyle;
    GUIStyle sliderStyle, thumbStyle;

    Rect windowHome, borderHome;
    Rect confirmHome, confirmBorderHome;

    int lastScreenWidth, lastScreenHeight;

    float lastSliderSound;

    bool built;
    bool pendingOpen;

    static readonly Color Obsidian = Hex("#17191D");
    static readonly Color Onyx = Hex("#22262C");
    static readonly Color Ash = Hex("#3A3F46");
    static readonly Color BrilliantWhite = Hex("#F7F8FA");
    static readonly Color ArchitecturalWhite = Hex("#E5E7EB");
    static readonly Color VitalGreen = Hex("#1EC878");

    void Awake()
    {
        instance = this;

        applied.master = Mathf.Clamp01(PlayerPrefs.GetFloat("volume", SoundManager.GeneralVolume));
        applied.music = Mathf.Clamp01(PlayerPrefs.GetFloat("music", SoundManager.MusicVolume));
        applied.sfx = Mathf.Clamp01(PlayerPrefs.GetFloat("sfx", SoundManager.SFXVolume));

        applied.gangnam = PlayerPrefs.GetInt("gangnam", 0) == 1;
        applied.ukulele = PlayerPrefs.GetInt("ukulele", 0) == 1;

        editing = applied;
        ApplySound();
    }

    void OnGUI()
    {
        if (!built)
        {
            BuildStyles();
            BuildUI();

            built = true;

            SetWindowVisible(false);
            SetConfirmVisible(false);

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            if (pendingOpen)
                OpenInternal();

            return;
        }

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            RefreshScreenPosition();
    }

    public void Open()
    {
        if (!built)
        {
            pendingOpen = true;
            return;
        }
        GUIManager.instance.SetBlocked(true);
        OpenInternal();
    }

    void OpenInternal()
    {
        pendingOpen = false;

        SoundManager.AudioShot(Vector3.zero, "On", 0.5f);

        RefreshScreenPosition();

        editing = applied;

        masterSlider.Value = editing.master;
        musicSlider.Value = editing.music;
        sfxSlider.Value = editing.sfx;

        gangnamToggle.Value = editing.gangnam;
        ukuleleToggle.Value = editing.ukulele;

        ResetRoot(border, borderHome);
        ResetRoot(window, windowHome);
        ResetGUI(animated);

        border.Opacity = 0f;
        window.Opacity = 0f;

        SetWindowVisible(true);
        SetSettingsInteractable(true);

        RefreshDirty();

        border.MoveIn(new Vector2(0f, 70f), .42f, ease: TweenHelper.EaseOutExpo);
        window.MoveIn(new Vector2(0f, 55f), .48f, ease: TweenHelper.EaseOutBack);

        border.FadeTo(1f, .32f, ease: TweenHelper.EaseOutSine);
        window.FadeTo(1f, .38f, ease: TweenHelper.EaseOutSine);

        GUITween.Wave(
            animated,
            new Vector2(-70f, 0f),
            .42f,
            .04f,
            TweenHelper.EaseOutBack
        );
    }

    void RequestClose()
    {
        if (IsDirty())
            OpenConfirm();
        else
            CloseWindow();
    }

    void CloseWindow()
    {
        GUIManager.instance.SetBlocked(false);
        SetSettingsInteractable(false);

        SoundManager.AudioShot(Vector3.zero, "Off", 0.5f);

        border.MoveOut(new Vector2(0f, 65f), .36f, ease: TweenHelper.EaseInExpo);
        window.MoveOut(new Vector2(0f, 55f), .38f, ease: TweenHelper.EaseInBack);

        border.FadeTo(0f, .30f, ease: TweenHelper.EaseInSine);
        window.FadeTo(0f, .34f, ease: TweenHelper.EaseInSine);

        int count = animated.Count;

        for (int i = 0; i < count; i++)
        {
            int index = count - i - 1;
            GUIItem item = animated[index];

            float delay = i * .028f;

            item.MoveOut(
                new Vector2(75f, 0f),
                .28f,
                delay,
                TweenHelper.EaseInExpo,
                i == count - 1
                    ? () => SetWindowVisible(false)
                    : null
            );
        }
    }

    void OpenConfirm()
    {
        SetSettingsInteractable(false);

        SoundManager.AudioShot(Vector3.zero, "Move", 0.4f);

        ResetRoot(confirmBorder, confirmBorderHome);
        ResetRoot(confirm, confirmHome);
        ResetGUI(confirmAnimated);

        confirmBorder.Opacity = 0f;
        confirm.Opacity = 0f;

        SetConfirmVisible(true);

        confirmBorder.MoveIn(new Vector2(0f, 40f), .28f, ease: TweenHelper.EaseOutExpo);
        confirm.MoveIn(new Vector2(0f, 35f), .34f, ease: TweenHelper.EaseOutBack);

        confirmBorder.FadeTo(1f, .22f, ease: TweenHelper.EaseOutSine);
        confirm.FadeTo(1f, .28f, ease: TweenHelper.EaseOutSine);

        GUITween.Wave(
            confirmAnimated,
            new Vector2(0f, 26f),
            .30f,
            .05f,
            TweenHelper.EaseOutBack
        );
    }

    void CloseConfirm(bool restoreSettings = true)
    {
        if (restoreSettings)
            SoundManager.AudioShot(Vector3.zero, "Selected", 0.4f);

        confirmBorder.MoveOut(new Vector2(0f, 35f), .22f, ease: TweenHelper.EaseInExpo);
        confirm.MoveOut(new Vector2(0f, 30f), .24f, ease: TweenHelper.EaseInBack);

        confirmBorder.FadeTo(0f, .18f, ease: TweenHelper.EaseInSine);
        confirm.FadeTo(0f, .20f, ease: TweenHelper.EaseInSine);

        int count = confirmAnimated.Count;

        for (int i = 0; i < count; i++)
        {
            int index = count - i - 1;
            GUIItem item = confirmAnimated[index];

            float delay = i * .03f;

            item.MoveOut(
                new Vector2(0f, 25f),
                .20f,
                delay,
                TweenHelper.EaseInExpo,
                i == count - 1
                    ? () =>
                    {
                        SetConfirmVisible(false);

                        if (restoreSettings)
                            SetSettingsInteractable(true);
                    }
                    : null
            );
        }
    }

    void SaveAndExit()
    {
        ApplyChanges(false);
        CloseConfirm(false);
        CloseWindow();
    }

    void ApplyChanges(bool animate = true)
    {
        applied = editing;

        ApplySound();

        SoundManager.AudioShot(Vector3.zero, "Good", 0.6f);

        PlayerPrefs.SetFloat("volume", applied.master);
        PlayerPrefs.SetFloat("music", applied.music);
        PlayerPrefs.SetFloat("sfx", applied.sfx);

        PlayerPrefs.SetInt("gangnam", applied.gangnam ? 1 : 0);
        PlayerPrefs.SetInt("ukulele", applied.ukulele ? 1 : 0);

        PlayerPrefs.Save();

        RefreshDirty();

        if (!animate)
            return;

        Vector2 pos = home[applyButton];

        applyButton.MoveTo(
            pos + Vector2.down * 8f,
            .09f,
            ease: TweenHelper.EaseOutExpo,
            onComplete: () =>
            {
                applyButton.MoveTo(
                    pos,
                    .22f,
                    ease: TweenHelper.EaseOutBack
                );
            }
        );
    }

    void ApplySound()
    {
        SoundManager.SetGeneralVolume(editing.master);
        SoundManager.SetMusicVolume(editing.music);
        SoundManager.SetSFXVolume(editing.sfx);
    }

    void PlaySliderSound()
    {
        if (Time.time - lastSliderSound < .09f)
            return;

        lastSliderSound = Time.time;
        SoundManager.AudioShot(Vector3.zero, "Move", .25f);
    }

    void BuildUI()
    {
        const float width = 660f;
        const float height = 540f;
        const float borderSize = 3f;

        float x = (Screen.width - width) * .5f;
        float y = (Screen.height - height) * .5f;

        windowHome = new Rect(x, y, width, height);
        borderHome = Expand(windowHome, borderSize);

        border = Root(
            new GUIBoxLabel(
                GUIContent.none,
                borderHome,
                borderStyle
            ),
            99
        );

        window = Root(
            new GUIGroup(
                new GUIContent("SETTINGS"),
                windowHome,
                "SettingsWindow",
                windowStyle
            ),
            100
        );

        const float left = 55f;
        const float labelWidth = 145f;

        const float sliderX = 215f;
        const float sliderWidth = 385f;
        const float sliderHeight = 24f;

        const float firstY = 125f;
        const float rowGap = 68f;

        closeButton = Add(
            window,
            new GUIButton(
                new GUIContent("X"),
                new Rect(x + width - 58f, y + 18f, 38f, 34f),
                RequestClose,
                buttonStyle
            )
        );

        masterLabel = Add(
            window,
            new GUILabel(
                new GUIContent("전체 볼륨"),
                new Rect(x + left, y + firstY, labelWidth, 30f),
                labelStyle
            )
        );

        masterSlider = Add(
            window,
            new GUISlider(
                new Rect(x + sliderX, y + firstY + 3f, sliderWidth, sliderHeight),
                editing.master,
                0f,
                1f,
                v =>
                {
                    editing.master = v;
                    SoundManager.SetGeneralVolume(v);
                    PlaySliderSound();
                    RefreshDirty();
                },
                sliderStyle,
                thumbStyle
            )
        );

        musicLabel = Add(
            window,
            new GUILabel(
                new GUIContent("음악"),
                new Rect(x + left, y + firstY + rowGap, labelWidth, 30f),
                labelStyle
            )
        );

        musicSlider = Add(
            window,
            new GUISlider(
                new Rect(x + sliderX, y + firstY + rowGap + 3f, sliderWidth, sliderHeight),
                editing.music,
                0f,
                1f,
                v =>
                {
                    editing.music = v;
                    SoundManager.SetMusicVolume(v);
                    PlaySliderSound();
                    RefreshDirty();
                },
                sliderStyle,
                thumbStyle
            )
        );

        sfxLabel = Add(
            window,
            new GUILabel(
                new GUIContent("SFX"),
                new Rect(x + left, y + firstY + rowGap * 2f, labelWidth, 30f),
                labelStyle
            )
        );

        sfxSlider = Add(
            window,
            new GUISlider(
                new Rect(x + sliderX, y + firstY + rowGap * 2f + 3f, sliderWidth, sliderHeight),
                editing.sfx,
                0f,
                1f,
                v =>
                {
                    editing.sfx = v;
                    SoundManager.SetSFXVolume(v);
                    PlaySliderSound();
                    RefreshDirty();
                },
                sliderStyle,
                thumbStyle
            )
        );

        gangnamToggle = Add(
            window,
            new GUIToggle(
                new GUIContent("강남스타일"),
                new Rect(x + left, y + 342f, 240f, 36f),
                editing.gangnam,
                v =>
                {
                editing.gangnam = v;
                SoundManager.AudioShot(Vector3.zero, v ? "On" : "Off", 0.4f);
                RefreshDirty();
                },
                toggleStyle
            )
        );

        ukuleleToggle = Add(
            window,
            new GUIToggle(
                new GUIContent("슈퍼메가우쿨렐레"),
                new Rect(x + left, y + 393f, 280f, 36f),
                editing.ukulele,
                v =>
                {
                editing.ukulele = v;
                SoundManager.AudioShot(Vector3.zero, v ? "On" : "Off", 0.4f);
                RefreshDirty();
                },
                toggleStyle
            )
        );

        applyButton = Add(
            window,
            new GUIButton(
                new GUIContent("APPLY"),
                new Rect(x + width - 220f, y + height - 75f, 165f, 48f),
                () => ApplyChanges(),
                applyStyle
            )
        );

        animated.AddRange(new GUIItem[]
        {
            closeButton,
            masterLabel,
            masterSlider,
            musicLabel,
            musicSlider,
            sfxLabel,
            sfxSlider,
            gangnamToggle,
            ukuleleToggle,
            applyButton
        });

        Remember(animated);
        BuildConfirm();
    }

    void BuildConfirm()
    {
        const float width = 480f;
        const float height = 235f;
        const float borderSize = 3f;

        float x = (Screen.width - width) * .5f;
        float y = (Screen.height - height) * .5f;

        confirmHome = new Rect(x, y, width, height);
        confirmBorderHome = Expand(confirmHome, borderSize);

        confirmBorder = Root(
            new GUIBoxLabel(
                GUIContent.none,
                confirmBorderHome,
                borderStyle
            ),
            199
        );

        confirm = Root(
            new GUIGroup(
                new GUIContent("변경 사항 있음"),
                confirmHome,
                "SettingsConfirm",
                windowStyle
            ),
            200
        );

        GUILabel text = Add(
            confirm,
            new GUILabel(
                new GUIContent("적용하지 않은 변경 사항이 있습니다."),
                new Rect(x + 38f, y + 82f, 404f, 32f),
                labelStyle
            )
        );

        GUIButton keep = Add(
            confirm,
            new GUIButton(
                new GUIContent("계속하기"),
                new Rect(x + 38f, y + 158f, 175f, 45f),
                () => CloseConfirm(),
                buttonStyle
            )
        );

        GUIButton save = Add(
            confirm,
            new GUIButton(
                new GUIContent("저장하고 나가기"),
                new Rect(x + 232f, y + 158f, 210f, 45f),
                SaveAndExit,
                dirtyApplyStyle
            )
        );

        confirmAnimated.AddRange(new GUIItem[]
        {
            text,
            keep,
            save
        });

        Remember(confirmAnimated);
    }

    void RefreshScreenPosition()
    {
        if (!built)
            return;

        if (Screen.width == lastScreenWidth &&
            Screen.height == lastScreenHeight)
            return;

        KillAllTweens();

        Vector2 newWindowPos = new Vector2(
            (Screen.width - windowHome.width) * .5f,
            (Screen.height - windowHome.height) * .5f
        );

        Vector2 windowDelta = newWindowPos - windowHome.position;

        windowHome.position += windowDelta;
        borderHome.position += windowDelta;

        border.SetPos(border.Pos + windowDelta);
        window.SetPos(window.Pos + windowDelta);

        for (int i = 0; i < animated.Count; i++)
            home[animated[i]] += windowDelta;

        Vector2 newConfirmPos = new Vector2(
            (Screen.width - confirmHome.width) * .5f,
            (Screen.height - confirmHome.height) * .5f
        );

        Vector2 confirmDelta = newConfirmPos - confirmHome.position;

        confirmHome.position += confirmDelta;
        confirmBorderHome.position += confirmDelta;

        confirmBorder.SetPos(confirmBorder.Pos + confirmDelta);
        confirm.SetPos(confirm.Pos + confirmDelta);

        for (int i = 0; i < confirmAnimated.Count; i++)
            home[confirmAnimated[i]] += confirmDelta;

        if (window.isVisible)
        {
            window.Opacity = 1f;
            border.Opacity = 1f;
        }

        if (confirm.isVisible)
        {
            confirm.Opacity = 1f;
            confirmBorder.Opacity = 1f;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    void KillAllTweens()
    {
        GUITween.Kill(border);
        GUITween.Kill(window);
        GUITween.Kill(confirmBorder);
        GUITween.Kill(confirm);

        for (int i = 0; i < animated.Count; i++)
            GUITween.Kill(animated[i]);

        for (int i = 0; i < confirmAnimated.Count; i++)
            GUITween.Kill(confirmAnimated[i]);
    }

    void BuildStyles()
    {
        windowStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 23,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(22, 22, 17, 17)
        };

        windowStyle.normal.background = Tex(Obsidian);
        windowStyle.normal.textColor = BrilliantWhite;

        borderStyle = new GUIStyle(GUI.skin.box);
        borderStyle.normal.background = Tex(ArchitecturalWhite);

        labelStyle = Label(ArchitecturalWhite);
        dirtyLabelStyle = Label(VitalGreen);

        buttonStyle = Button(Onyx, Ash, ArchitecturalWhite);
        applyStyle = Button(Ash, Onyx, BrilliantWhite);
        dirtyApplyStyle = Button(VitalGreen, BrilliantWhite, Obsidian);

        toggleStyle = new GUIStyle(GUI.skin.toggle)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft
        };

        toggleStyle.normal.textColor = ArchitecturalWhite;
        toggleStyle.hover.textColor = BrilliantWhite;
        toggleStyle.active.textColor = BrilliantWhite;

        dirtyToggleStyle = new GUIStyle(toggleStyle);
        dirtyToggleStyle.normal.textColor = VitalGreen;
        dirtyToggleStyle.hover.textColor = VitalGreen;
        dirtyToggleStyle.active.textColor = VitalGreen;

        sliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
        {
            fixedHeight = 7f,
            margin = new RectOffset(),
            padding = new RectOffset(),
            overflow = new RectOffset()
        };

        sliderStyle.normal.background = Tex(Ash);

        thumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
        {
            fixedWidth = 18f,
            fixedHeight = 24f,
            margin = new RectOffset(),
            padding = new RectOffset(),
            overflow = new RectOffset()
        };

        thumbStyle.normal.background = Tex(ArchitecturalWhite);
        thumbStyle.hover.background = Tex(BrilliantWhite);
        thumbStyle.active.background = Tex(VitalGreen);
    }

    void RefreshDirty()
    {
        if (!built)
            return;

        masterLabel.Style = Dirty(editing.master, applied.master)
            ? dirtyLabelStyle
            : labelStyle;

        musicLabel.Style = Dirty(editing.music, applied.music)
            ? dirtyLabelStyle
            : labelStyle;

        sfxLabel.Style = Dirty(editing.sfx, applied.sfx)
            ? dirtyLabelStyle
            : labelStyle;

        gangnamToggle.Style = editing.gangnam != applied.gangnam
            ? dirtyToggleStyle
            : toggleStyle;

        ukuleleToggle.Style = editing.ukulele != applied.ukulele
            ? dirtyToggleStyle
            : toggleStyle;

        applyButton.Style = IsDirty()
            ? dirtyApplyStyle
            : applyStyle;
    }

    bool IsDirty()
    {
        return Dirty(editing.master, applied.master) ||
               Dirty(editing.music, applied.music) ||
               Dirty(editing.sfx, applied.sfx) ||
               editing.gangnam != applied.gangnam ||
               editing.ukulele != applied.ukulele;
    }

    static bool Dirty(float a, float b)
    {
        return !Mathf.Approximately(a, b);
    }

    void SetWindowVisible(bool value)
    {
        border.isVisible = value;
        window.isVisible = value;
    }

    void SetConfirmVisible(bool value)
    {
        confirmBorder.isVisible = value;
        confirm.isVisible = value;
    }

    void SetSettingsInteractable(bool value)
    {
        for (int i = 0; i < animated.Count; i++)
            animated[i].isInteractable = value;
    }

    void ResetGUI(List<GUIItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            GUIItem item = items[i];

            GUITween.Kill(item);

            item.SetPos(home[item]);
            item.RenderScale = Vector2.one;
            item.Opacity = 1f;
        }
    }

    void Remember(List<GUIItem> items)
    {
        for (int i = 0; i < items.Count; i++)
            home[items[i]] = items[i].Pos;
    }

    static void ResetRoot(GUIItem item, Rect rect)
    {
        GUITween.Kill(item);

        item.SetRect(rect);
        item.RenderScale = Vector2.one;
        item.Opacity = 1f;
    }

    T Add<T>(GUIGroup parent, T item) where T : GUIItem
    {
        item.isVisible = true;
        item.isEnabled = true;
        item.isInteractable = true;
        item.Opacity = 1f;

        GUIManager.Register(item);
        parent.Add(item);

        return item;
    }

    static T Root<T>(T item, int layer) where T : GUIItem
    {
        item.Layer = layer;

        item.isVisible = true;
        item.isEnabled = true;
        item.isInteractable = true;
        item.Opacity = 1f;

        GUIManager.Register(item);

        return item;
    }

    static Rect Expand(Rect rect, float amount)
    {
        return new Rect(
            rect.x - amount,
            rect.y - amount,
            rect.width + amount * 2f,
            rect.height + amount * 2f
        );
    }

    static GUIStyle Label(Color color)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft
        };

        style.normal.textColor = color;

        return style;
    }

    static GUIStyle Button(Color normal, Color hover, Color text)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        style.normal.background = Tex(normal);
        style.hover.background = Tex(hover);
        style.active.background = Tex(VitalGreen);

        style.normal.textColor = text;
        style.hover.textColor = BrilliantWhite;
        style.active.textColor = Obsidian;

        return style;
    }

    static Texture2D Tex(Color color)
    {
        Texture2D texture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        texture.SetPixel(0, 0, color);
        texture.Apply();

        return texture;
    }

    static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }
}