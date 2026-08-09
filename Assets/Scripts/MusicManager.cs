using UnityEngine;
using UnityEngine.InputSystem;
using IMGUI;

public class MusicManager : MonoBehaviour
{
    [SerializeField] float showTime = 5f;
    [SerializeField] float fade = 0.5f;
    [SerializeField] Vector2 size = new(430f, 116f);
    [SerializeField] Vector2 miniSize = new(210f, 34f);
    [SerializeField] int barCount = 24;
    [SerializeField] float margin = 12f;
    [SerializeField] float seekStep = 5f;
    [SerializeField] Key toggleKey = Key.M;

    // ponytail: 스펙트럼 게인은 소스/믹서마다 달라서 눈으로 맞춰야 함. 인스펙터에서 조정.
    [SerializeField] float spectrumGain = 140f;

    AudioClip[] playlist;
    AudioClip selected, shown;
    int index;

    bool paused;
    float playGuard;

    GUIGroup panel;
    MarqueeLabel title;
    GUILabel author, timeLabel;
    GUIButton playButton;
    GUIItem[] controls;
    GUIBoxLabel[] bars;

    GUIStyle panelStyle, titleStyle, authorStyle, timeStyle, barStyle, buttonStyle;

    float timer;        // 등장 이후 경과 시간
    float collapseT;    // 접힘 진행도(선형 0~1)
    float collapse;     // 위에 이징을 먹인 값. 레이아웃은 이걸 본다.

    float[] spectrum, levels;
    int[] bandLo, bandHi;

    Vector2 shownPos, dragOffset;
    bool dragging, hasPosition;

    const int SpectrumSize = 256;
    const float DragZoneHeight = 40f;
    const float EnterTime = 0.3f;
    const float CollapseTime = 0.35f;

    static readonly Color BG = new Color32(23, 25, 29, 245);
    static readonly Color White = new Color32(247, 248, 250, 255);
    static readonly Color Gray = new Color32(170, 175, 185, 255);
    static readonly Color BarBG = new Color32(30, 200, 120, 55);

    void Start()
    {
        playlist = Resources.LoadAll<AudioClip>("Audio/Music");

        if (playlist.Length == 0)
        {
            Debug.LogWarning("No music in Resources/Audio/Music");
            return;
        }

        Play(0);
    }

    void Update()
    {
        playGuard -= Time.unscaledDeltaTime;

        AudioClip playing = SoundManager.CurrentMusic;

        // UI 생성에 성공하기 전에는 shown을 갱신하지 않는다.
        if (playing != null && playing != shown)
            Show(playing);

        // 일시정지는 "곡 끝"이 아니다. paused/playGuard 없으면 정지 버튼이 곧 다음곡 버튼이 됨.
        if (selected != null &&
            playing == selected &&
            !paused &&
            playGuard <= 0f &&
            !SoundManager.IsMusicPlaying)
        {
            Next();
            return;
        }

        if (panel == null)
            return;

        if (toggleKey != Key.None &&
            Keyboard.current != null &&
            Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            ToggleVisible();
        }

        if (!panel.isVisible)
            return;

        bool hovering =
            !GUIManager.Blocked &&
            panel.DrawRect.Contains(GUIManager.MousePos);

        timer += Time.unscaledDeltaTime;

        // 접힘은 타이머와 별개. 호버/드래그하면 CollapseTime 안에 원래 크기로 돌아온다.
        bool wantMini = timer >= showTime && !hovering && !dragging;

        collapseT = Mathf.MoveTowards(
            collapseT,
            wantMini ? 1f : 0f,
            Time.unscaledDeltaTime / CollapseTime
        );

        HandleDrag();
        UpdateUI();
    }

    // =========================================================
    // 재생 제어
    // =========================================================

    public void Play(int i)
    {
        if (playlist == null || playlist.Length == 0)
            return;

        index = (i % playlist.Length + playlist.Length) % playlist.Length;
        selected = playlist[index];

        paused = false;
        playGuard = 0.25f;

        SoundManager.PlayMusic(
            selected,
            1f,
            fade,
            false
        );
    }

    public void Next() => Play(index + 1);
    public void Previous() => Play(index - 1);

    void TogglePlay()
    {
        if (SoundManager.CurrentMusic == null)
            return;

        paused = SoundManager.IsMusicPlaying;

        if (paused)
            SoundManager.PauseMusic();
        else
            SoundManager.ResumeMusic();
    }

    void Seek(float delta)
    {
        if (SoundManager.CurrentMusic == null)
            return;

        // 클램프는 SoundManager.MusicTime 세터가 한다.
        SoundManager.MusicTime += delta;
    }

    // =========================================================
    // 표시
    // =========================================================

    void ToggleVisible()
    {
        if (panel == null && !EnsureUI())
            return;

        // 접힘은 showTime이 담당한다. 이 키는 완전 숨김/복구 전용.
        if (panel.isVisible)
        {
            panel.isVisible = false;
            dragging = false;
            return;
        }

        timer = 0f;
        collapseT = 0f;
        panel.isVisible = true;
    }

    void Show(AudioClip clip)
    {
        if (!EnsureUI())
            return;

        // 여기까지 성공한 다음에 커밋.
        shown = clip;

        ParseName(
            clip.name,
            out string artist,
            out string song
        );

        title.Content.text = song;
        title.TextWidth = titleStyle.CalcSize(title.Content).x;
        title.Offset = 0f;

        author.Content.text = artist;

        if (!hasPosition)
        {
            shownPos = new Vector2(
                (Screen.width - size.x) * 0.5f,
                Screen.height - size.y - 18f
            );

            hasPosition = true;
        }

        ClampPosition();

        timer = 0f;
        collapseT = 0f;
        dragging = false;
        panel.isVisible = true;
    }

    bool EnsureUI()
    {
        if (panel != null)
            return true;

        if (!GUIStyleMaker.Initialized)
            return false;

        panelStyle = GUIStyleMaker.Box(BG);

        // GUI.skin.label은 wordWrap = true다. 끄지 않으면 긴 제목이 두 줄로 감기면서
        // 위아래가 잘리고, CalcSize도 감긴 폭을 돌려줘 마퀴 판정까지 어긋난다.
        titleStyle = GUIStyleMaker.Label(White, 18, TextAnchor.MiddleLeft).Wrap(false);
        authorStyle = GUIStyleMaker.Label(Gray, 12, TextAnchor.MiddleLeft).Wrap(false);
        timeStyle = GUIStyleMaker.Label(Gray, 12, TextAnchor.MiddleRight).Wrap(false);

        barStyle = GUIStyleMaker.Box(BarBG).NoSpacing();

        buttonStyle = GUIStyleMaker.Button(
            new Color32(255, 255, 255, 16),
            White,
            new Color32(255, 255, 255, 44),
            new Color32(30, 200, 120, 140),
            13
        );

        panel = Widget.Window(
            new GUIContent(),
            new Rect(0, Screen.height, size.x, size.y),
            "NowPlaying",
            panelStyle
        );

        // Add()가 자식 Layer를 부모 값으로 덮으므로 자식 생성 전에 설정.
        panel.SetLayer(500);
        panel.isInteractable = true;
        panel.Mask = true;

        // 패딩은 Layout()이 접힘 정도에 따라 매 프레임 다시 넣는다.

        // ── 배경 레이어 ──
        // GUIGroup은 Childrens 순서대로 그린다. 먼저 등록한 만큼 아래에 깔린다.
        spectrum = new float[SpectrumSize];
        levels = new float[barCount];
        bars = new GUIBoxLabel[barCount];
        bandLo = new int[barCount];
        bandHi = new int[barCount];

        for (int i = 0; i < barCount; i++)
        {
            bars[i] = Widget.BoxLabel(
                panel,
                new GUIContent(),
                Rect.zero,
                barStyle
            );

            // 로그 밴딩. 선형으로 앞 n개만 쓰면 저역만 보인다.
            bandLo[i] = Mathf.FloorToInt(
                Mathf.Pow(SpectrumSize, i / (float)barCount)
            );

            bandHi[i] = Mathf.Clamp(
                Mathf.FloorToInt(
                    Mathf.Pow(SpectrumSize, (i + 1) / (float)barCount)
                ),
                bandLo[i] + 1,
                SpectrumSize
            );
        }

        // ── 전경 레이어 ──
        title = new MarqueeLabel(
            new GUIContent(""),
            Rect.zero,
            titleStyle
        )
        {
            isVisible = true,
            isEnabled = true,
            isInteractable = true,
            Opacity = 1f
        };

        GUIManager.RegisterAndSetParent(panel, title);

        author = Widget.Label(panel, "", Rect.zero, authorStyle);
        timeLabel = Widget.Label(panel, "", Rect.zero, timeStyle);

        controls = new GUIItem[]
        {
            Widget.Button(panel, "|<", Rect.zero, Previous, buttonStyle),
            Widget.Button(panel, "<<", Rect.zero, () => Seek(-seekStep), buttonStyle),
            playButton = Widget.Button(panel, "||", Rect.zero, TogglePlay, buttonStyle),
            Widget.Button(panel, ">>", Rect.zero, () => Seek(seekStep), buttonStyle),
            Widget.Button(panel, ">|", Rect.zero, Next, buttonStyle),
        };

        return true;
    }

    // =========================================================
    // 입력
    // =========================================================

    void HandleDrag()
    {
        if (GUIManager.Blocked)
        {
            dragging = false;
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        Vector2 p = GUIManager.MousePos;
        Rect r = panel.DrawRect;

        // 상단 텍스트 줄만 손잡이. 버튼 위에서 잡으면 클릭과 싸운다.
        Rect dragZone = new Rect(
            r.x,
            r.y,
            r.width,
            Mathf.Min(DragZoneHeight, r.height)
        );

        if (mouse.leftButton.wasPressedThisFrame &&
            timer >= EnterTime &&
            dragZone.Contains(p))
        {
            dragging = true;
            dragOffset = p - r.position;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
            dragging = false;

        if (!dragging || !mouse.leftButton.isPressed)
            return;

        shownPos = p - dragOffset;

        ClampPosition();
    }

    void ClampPosition()
    {
        shownPos = GUILayoutManager.ClampPosition(
            shownPos,
            size,
            new Rect(
                margin,
                margin,
                Screen.width - margin * 2f,
                Screen.height - margin * 2f
            )
        );
    }

    // =========================================================
    // 렌더
    // =========================================================

    void UpdateUI()
    {
        ClampPosition();

        float enter = Mathf.Clamp01(timer / EnterTime);

        collapse = TweenHelper.EaseInOutQuad(collapseT);

        Vector2 cur = Vector2.Lerp(size, miniSize, collapse);

        float hiddenY = Screen.height + 8f;

        // 있던 자리에서 좌상단을 고정한 채 줄어든다.
        float y = Mathf.LerpUnclamped(
            hiddenY,
            shownPos.y,
            TweenHelper.EaseOutBack(enter)
        );

        panel.SetRect(new Rect(shownPos.x, y, cur.x, cur.y));

        panel.Opacity =
            TweenHelper.EaseOutSine(enter) *
            Mathf.Lerp(1f, 0.55f, collapse);

        Layout();
    }

    void Layout()
    {
        // 미니(34px)에서 상하 패딩 9px씩이면 콘텐츠가 거의 안 남는다.
        float pad = Mathf.Lerp(14f, 6f, collapse);
        float padV = Mathf.Lerp(9f, 5f, collapse);

        GUILayoutManager.SetPadding(panel, pad, pad, padV, padV);

        Rect r = GUILayoutManager.GetContentRect(panel);

        // 1) 배경: 스펙트럼이 패널 전체를 채운다. 접혀도 이것만 남는다.
        LayoutBars(r);

        bool mini = collapse > 0.5f;

        title.isVisible = !mini;
        author.isVisible = !mini;
        timeLabel.isVisible = !mini;

        for (int i = 0; i < controls.Length; i++)
            controls[i].isVisible = !mini;

        if (mini)
            return;

        // 2) 전경
        const float timeWidth = 92f;
        float textWidth = r.width - timeWidth - 12f;

        title.SetRect(new Rect(r.x, r.y, textWidth, 22f));
        author.SetRect(new Rect(r.x, r.y + 21f, textWidth, 16f));
        timeLabel.SetRect(new Rect(r.xMax - timeWidth, r.y + 5f, timeWidth, 22f));

        if (title.TextWidth > title.Rect.width)
        {
            title.Offset = Mathf.Repeat(
                title.Offset + Time.unscaledDeltaTime * 30f,
                title.TextWidth + MarqueeLabel.Gap
            );
        }
        else
        {
            title.Offset = 0f;
        }

        AudioClip clip = SoundManager.CurrentMusic;

        timeLabel.Content.text = clip == null
            ? ""
            : $"{FormatTime(SoundManager.MusicTime)} / {FormatTime(clip.length)}";

        const float bw = 36f, bh = 24f, bgap = 6f;

        float rowWidth =
            bw * controls.Length +
            bgap * (controls.Length - 1);

        float bx = r.x + (r.width - rowWidth) * 0.5f;
        float by = r.y + 44f;

        for (int i = 0; i < controls.Length; i++)
            controls[i].SetRect(new Rect(bx + i * (bw + bgap), by, bw, bh));

        playButton.Content.text =
            SoundManager.IsMusicPlaying ? "||" : ">";
    }

    void LayoutBars(Rect r)
    {
        SoundManager.GetMusicSpectrum(spectrum);

        float gap = Mathf.Clamp(r.width / barCount * 0.25f, 1f, 3f);
        float w = (r.width - gap * (barCount - 1)) / barCount;
        float dt = Time.unscaledDeltaTime;

        for (int i = 0; i < barCount; i++)
        {
            float target = 0f;

            if (SoundManager.IsMusicPlaying)
            {
                float sum = 0f;

                for (int b = bandLo[i]; b < bandHi[i]; b++)
                    sum += spectrum[b];

                float avg = sum / (bandHi[i] - bandLo[i]);

                target = Mathf.Clamp01(
                    Mathf.Pow(avg * spectrumGain, 0.45f)
                );
            }

            // 올라갈 땐 빠르게, 내려올 땐 천천히.
            float k = target > levels[i] ? 30f : 10f;

            levels[i] = Mathf.Lerp(
                levels[i],
                target,
                1f - Mathf.Exp(-k * dt)
            );

            float barHeight = Mathf.Max(2f, levels[i] * r.height);

            bars[i].SetRect(new Rect(
                r.x + i * (w + gap),
                r.yMax - barHeight,
                w,
                barHeight
            ));
        }
    }

    // =========================================================
    // Util
    // =========================================================

    static void ParseName(
        string name,
        out string author,
        out string title)
    {
        int split = name.IndexOf(" - ");

        if (split < 0)
        {
            author = "Unknown Artist";
            title = name;
            return;
        }

        author = name[..split].Trim();
        title = name[(split + 3)..].Trim();
    }

    static string FormatTime(float time)
    {
        int seconds = Mathf.Max(0, Mathf.FloorToInt(time));
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    void OnDestroy()
    {
        // Unregister는 자식까지 재귀로 정리한다.
        if (panel != null)
            GUIManager.Unregister(panel);
    }

    class MarqueeLabel : GUILabel
    {
        public const float Gap = 36f;

        public float Offset;
        public float TextWidth;

        public MarqueeLabel(GUIContent content, Rect rect, GUIStyle style)
            : base(content, rect, style) { }

        public override void Draw()
        {
            if (!isVisible)
                return;

            Rect r = DrawRect;
            GUIStyle style = Style ?? GUI.skin.label;

            if (TextWidth <= r.width)
            {
                GUI.Label(r, Content, style);
                return;
            }

            GUI.BeginGroup(r);

            GUI.Label(new Rect(-Offset, 0f, TextWidth, r.height), Content, style);

            GUI.Label(
                new Rect(TextWidth + Gap - Offset, 0f, TextWidth, r.height),
                Content,
                style
            );

            GUI.EndGroup();
        }
    }
}