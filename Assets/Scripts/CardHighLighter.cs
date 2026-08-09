using UnityEngine;
using UnityEngine.InputSystem;
using IMGUI;

public class CardHighLighter : MonoBehaviour
{
    public static CardHighLighter instance;

    [Header("Card")]
    [SerializeField] private Sprite cardBack = null;

    [Header("Layout")]
    [SerializeField] private float cardHeight = 260f;
    [SerializeField] private float descWidth = 420f;
    [SerializeField] private float gap = 24f;
    [SerializeField] private float padding = 24f;

    [SerializeField] private float statsGap = 18f;
    [SerializeField] private float statHeight = 24f;
    [SerializeField, Range(0.3f, 0.7f)]
    private float statDividerRatio = 0.52f;
    [SerializeField] private float statDividerGap = 14f;

    [SerializeField] private float screenMargin = 16f;

    [Header("Follow")]
    [SerializeField] private Vector2 cursorOffset =
        new Vector2(28f, 28f);

    [Header("Animation")]
    [SerializeField] private float openDuration = 0.42f;
    [SerializeField] private float closeDuration = 0.32f;
    [SerializeField] private float graceTime = 0.13f;

    [Header("Layer")]
    [SerializeField] private int tooltipLayer = 1000;

    public BaseCardData currentHighlightedCard;

    private CardUI activeCardUI;

    private GUIGroup window;
    private GUIImage cardImage;
    private GUILabel descLabel;

    private readonly GUILabel[] statNames =
        new GUILabel[6];

    private readonly GUILabel[] statValues =
        new GUILabel[6];

    private GUIBoxLabel statDivider;

    private GUIStyle windowStyle;
    private GUIStyle descStyle;
    private GUIStyle statNameStyle;
    private GUIStyle statValueStyle;
    private GUIStyle dividerStyle;

    private Rect sourceCardRect;
    private Rect frozenWindowRect;

    private Vector2 finalWindowSize;
    private Vector2 finalCardSize;

    private float animationProgress;
    private float graceTimer;

    private bool closing;
    private bool stylesReady;

    private static readonly Color Obsidian =
        new Color32(23, 25, 29, 255);

    private static readonly Color BrilliantWhite =
        new Color32(247, 248, 250, 255);

    private static readonly Color DividerColor =
        new Color32(90, 96, 105, 255);


    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        DestroyTooltip();

        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        CardUI hovered =
            FindCardUnderCursor();

        HandleHover(hovered);
        UpdateAnimation();
    }


    // ─────────────────────────────────────
    // Hover
    // ─────────────────────────────────────

    private void HandleHover(CardUI hovered)
    {
        if (activeCardUI == null)
        {
            if (hovered != null)
                Open(hovered);

            return;
        }

        bool same =
            hovered != null &&
            hovered.Data == currentHighlightedCard;

        if (same)
        {
            graceTimer = 0f;

            // 닫히던 중 원래 카드로 돌아오면
            // 현재 progress에서 그대로 다시 펼쳐진다.
            if (closing)
                closing = false;

            return;
        }

        if (closing)
            return;

        graceTimer +=
            Time.unscaledDeltaTime;

        if (graceTimer >= graceTime)
            BeginClose();
    }


    // 기존 외부 코드 호환용.
    public void ClearHighlight(
        bool immediate = false)
    {
        graceTimer = 0f;

        if (immediate)
        {
            DestroyTooltip();
            return;
        }

        if (activeCardUI != null)
            BeginClose();
    }


    // ─────────────────────────────────────
    // Open / Close
    // ─────────────────────────────────────

    private void Open(CardUI cardUI)
    {
        if(GUIManager.Blocked) return;
        if (cardUI == null ||
            cardUI.Object == null ||
            cardUI.Data == null)
            return;

        if (!EnsureStyles())
            return;

        if (!cardUI.Object.TryGetComponent(
                out RectTransform rect))
            return;

        activeCardUI = cardUI;
        currentHighlightedCard =
            cardUI.Data;

        closing = false;
        graceTimer = 0f;
        animationProgress = 0f;

        sourceCardRect =
            GetIMGUIRect(rect);

        CalculateFinalSize();
        CreateTooltip();
        RenderTooltip();

        SoundManager.AudioShot(
            transform.position,
            "On",
            0.5f
        );
    }

    private void BeginClose()
    {
        if (activeCardUI == null ||
            closing)
            return;

        // 카드가 애니메이션 등으로 이동했을 수 있으니
        // 접히기 직전에 원래 카드 위치를 갱신.
        if (activeCardUI.Object != null &&
            activeCardUI.Object.TryGetComponent(
                out RectTransform rect))
        {
            sourceCardRect =
                GetIMGUIRect(rect);
        }

        // 닫는 동안에는 툴팁이 마우스를 쫓지 않는다.
        frozenWindowRect =
            GetFinalWindowRect();

        closing = true;
        graceTimer = 0f;
    }

    private void UpdateAnimation()
    {
        if (activeCardUI == null)
            return;

        if (closing)
        {
            animationProgress -=
                Time.unscaledDeltaTime /
                Mathf.Max(
                    0.001f,
                    closeDuration
                );

            if (animationProgress <= 0f)
            {
                animationProgress = 0f;

                DestroyTooltip();

                // 닫힌 바로 그 순간 다른 카드 위에 있으면
                // 그 카드 Tooltip을 바로 펼친다.
                CardUI hovered =
                    FindCardUnderCursor();

                if (hovered != null)
                    Open(hovered);

                return;
            }
        }
        else
        {
            animationProgress +=
                Time.unscaledDeltaTime /
                Mathf.Max(
                    0.001f,
                    openDuration
                );

            animationProgress =
                Mathf.Clamp01(
                    animationProgress
                );
        }

        RenderTooltip();
    }


    // ─────────────────────────────────────
    // Create
    // ─────────────────────────────────────

    private void CreateTooltip()
    {
        window = Widget.Window(
            GUIContent.none,
            sourceCardRect,
            "CardHighlight",
            windowStyle
        );

        window.Layer =
            tooltipLayer;

        window.isInteractable =
            false;

        window.Opacity = 0f;


        Sprite firstImage =
            cardBack != null
                ? cardBack
                : currentHighlightedCard.Image;

        cardImage = Widget.Image(
            firstImage,
            sourceCardRect,
            ScaleMode.ScaleToFit
        );

        cardImage.Layer =
            tooltipLayer + 1;

        cardImage.isInteractable =
            false;

        cardImage.Opacity = 0f;


        descLabel = Widget.Label(
            new GUIContent(
                currentHighlightedCard.Desc ?? ""
            ),
            TinyRect(
                sourceCardRect.center
            ),
            descStyle
        );

        descLabel.Layer =
            tooltipLayer + 2;

        descLabel.isInteractable =
            false;

        descLabel.Opacity = 0f;


        CreateStatLabels();
    }

    private void CreateStatLabels()
    {
        BaseCardData c =
            currentHighlightedCard;

        CreateStat(
            0,
            "RPS",
            FormatRPS(c.RPS),
            "이 카드가 가위바위보 판정에서 사용하는 값입니다."
        );

        CreateStat(
            1,
            "성공 확률",
            $"<color=#F1FA8C>{Mathf.RoundToInt(c.probability * 100f)}%</color>",
            "카드 효과가 성공할 확률입니다."
        );

        CreateStat(
            2,
            "내 이동",
            FormatMove(
                c.moveMe,
                true
            ),
            "자신이 이동하는 칸 수입니다. 양수면 앞으로, 음수면 뒤로 이동합니다."
        );

        CreateStat(
            3,
            "상대 이동",
            FormatMove(
                c.moveOps,
                false
            ),
            "상대를 이동시키는 칸 수입니다. 음수면 상대를 뒤로 밀어냅니다."
        );

        CreateStat(
            4,
            "내 RPS 차단",
            FormatBlock(
                c.moveBlockMe,
                false
            ),
            "이 턴 수만큼 자신이 RPS에서 승리할 수 없습니다."
        );

        CreateStat(
            5,
            "상대 RPS 차단",
            FormatBlock(
                c.moveBlockOps,
                true
            ),
            "이 턴 수만큼 상대가 RPS에서 승리할 수 없습니다."
        );


        statDivider = Widget.BoxLabel(
            GUIContent.none,
            TinyRect(
                sourceCardRect.center
            ),
            dividerStyle
        );

        statDivider.Layer =
            tooltipLayer + 3;

        statDivider.isInteractable =
            false;

        statDivider.Opacity = 0f;
    }

    private void CreateStat(
        int index,
        string name,
        string value,
        string tooltip)
    {
        statNames[index] =
            Widget.Label(
                new GUIContent(
                    name,
                    tooltip
                ),
                TinyRect(
                    sourceCardRect.center
                ),
                statNameStyle
            );

        statValues[index] =
            Widget.Label(
                new GUIContent(
                    value,
                    tooltip
                ),
                TinyRect(
                    sourceCardRect.center
                ),
                statValueStyle
            );


        statNames[index].Layer =
            tooltipLayer + 4;

        statValues[index].Layer =
            tooltipLayer + 4;


        statNames[index].isInteractable =
            false;

        statValues[index].isInteractable =
            false;


        statNames[index].Opacity = 0f;
        statValues[index].Opacity = 0f;
    }


    // ─────────────────────────────────────
    // Animation
    // ─────────────────────────────────────

    private void RenderTooltip()
    {
        if (window == null ||
            cardImage == null ||
            descLabel == null)
            return;

        float p =
            Mathf.Clamp01(
                animationProgress
            );


        // ═════════════════════════════════
        // Window
        // ═════════════════════════════════

        Rect targetWindow =
            closing
                ? frozenWindowRect
                : GetFinalWindowRect();

        float windowT =
            TweenHelper.EaseOutExpo(p);

        window.SetRect(
            LerpRect(
                sourceCardRect,
                targetWindow,
                windowT
            )
        );

        window.Opacity =
            TweenHelper.EaseOutSine(
                Mathf.Clamp01(
                    p * 2.2f
                )
            );


        // ═════════════════════════════════
        // Card movement
        // ═════════════════════════════════

        Rect targetCard =
            GetFinalCardRect(
                targetWindow
            );

        float cardMoveT =
            TweenHelper.EaseOutBack(p);

        Rect currentCard =
            LerpRect(
                sourceCardRect,
                targetCard,
                cardMoveT
            );

        cardImage.SetRect(
            currentCard
        );


        // ═════════════════════════════════
        // Card Flip
        //
        // 1 → 0 → 1
        // 중간에서 Back → Front
        // ═════════════════════════════════

        float flipRaw =
            Mathf.Clamp01(
                p / 0.72f
            );

        float flipT =
            TweenHelper.EaseInOutSine(
                flipRaw
            );

        float flipScale =
            Mathf.Abs(
                Mathf.Cos(
                    flipT *
                    Mathf.PI
                )
            );

        cardImage.RenderScale =
            new Vector2(
                Mathf.Max(
                    0.001f,
                    flipScale
                ),
                1f
            );


        if (flipT < 0.5f)
        {
            cardImage.SetSprite(
                cardBack != null
                    ? cardBack
                    : currentHighlightedCard.Image
            );
        }
        else
        {
            cardImage.SetSprite(
                currentHighlightedCard.Image
            );
        }


        cardImage.Opacity =
            TweenHelper.EaseOutSine(
                Mathf.Clamp01(
                    p * 3f
                )
            );


        // ═════════════════════════════════
        // Description
        //
        // 실제 카드 위치에서 시작해 펼쳐진다.
        // ═════════════════════════════════

        Rect targetDesc =
            GetFinalDescRect(
                targetWindow,
                targetCard
            );

        Rect descOrigin =
            TinyRect(
                currentCard.center
            );

        float descRaw =
            Mathf.InverseLerp(
                0.12f,
                1f,
                p
            );

        float descT =
            TweenHelper.EaseOutExpo(
                descRaw
            );

        descLabel.SetRect(
            LerpRect(
                descOrigin,
                targetDesc,
                descT
            )
        );

        descLabel.Opacity =
            TweenHelper.EaseOutSine(
                descRaw
            );


        // ═════════════════════════════════
        // Stats Area
        // ═════════════════════════════════

        Rect statArea =
            GetStatArea(
                targetWindow,
                targetCard,
                targetDesc
            );


        // 세로 Divider.
        // 중앙에서 위/아래로 펼쳐진다.
        if (statDivider != null)
        {
            Rect targetDivider =
                GetStatDividerRect(
                    statArea
                );

            float dividerRaw =
                Mathf.InverseLerp(
                    0.23f,
                    0.70f,
                    p
                );

            float dividerT =
                TweenHelper.EaseOutExpo(
                    dividerRaw
                );

            Rect collapsed =
                new Rect(
                    targetDivider.x,
                    targetDivider.center.y,
                    targetDivider.width,
                    0f
                );

            statDivider.SetRect(
                LerpRect(
                    collapsed,
                    targetDivider,
                    dividerT
                )
            );

            statDivider.Opacity =
                TweenHelper.EaseOutSine(
                    dividerRaw
                );
        }


        // Name | Value
        for (int i = 0;
             i < statNames.Length;
             i++)
        {
            GUILabel name =
                statNames[i];

            GUILabel value =
                statValues[i];

            if (name == null ||
                value == null)
                continue;


            Rect targetName =
                GetStatNameRect(
                    statArea,
                    i
                );

            Rect targetValue =
                GetStatValueRect(
                    statArea,
                    i
                );


            Rect origin =
                TinyRect(
                    currentCard.center
                );


            float start =
                0.27f +
                i * 0.035f;

            float raw =
                Mathf.InverseLerp(
                    start,
                    1f,
                    p
                );

            float t =
                TweenHelper.EaseOutExpo(
                    raw
                );


            name.SetRect(
                LerpRect(
                    origin,
                    targetName,
                    t
                )
            );

            value.SetRect(
                LerpRect(
                    origin,
                    targetValue,
                    t
                )
            );


            float alpha =
                TweenHelper.EaseOutSine(
                    raw
                );

            name.Opacity = alpha;
            value.Opacity = alpha;
        }
    }


    // ─────────────────────────────────────
    // Layout
    // ─────────────────────────────────────

    private Rect GetFinalWindowRect()
    {
        Rect rect =
            new Rect(
                GUIManager.MousePos +
                cursorOffset,

                finalWindowSize
            );

        return GUILayoutManager
            .ClampRectToScreen(
                rect,
                screenMargin
            );
    }

    private Rect GetFinalCardRect(
        Rect windowRect)
    {
        float y =
            windowRect.y +
            (
                windowRect.height -
                finalCardSize.y
            ) * 0.5f;

        return new Rect(
            windowRect.x +
            padding,

            y,

            finalCardSize.x,
            finalCardSize.y
        );
    }

    private Rect GetFinalDescRect(
        Rect windowRect,
        Rect cardRect)
    {
        float x =
            cardRect.xMax +
            gap;

        float width =
            windowRect.xMax -
            padding -
            x;


        GUIContent content =
            new GUIContent(
                currentHighlightedCard.Desc ?? ""
            );


        float height =
            descStyle.CalcHeight(
                content,
                width
            );


        return new Rect(
            x,
            windowRect.y +
            padding,

            width,
            height
        );
    }


    private Rect GetStatArea(
        Rect windowRect,
        Rect cardRect,
        Rect descRect)
    {
        return new Rect(
            descRect.x,

            descRect.yMax +
            statsGap,

            descRect.width,

            statHeight *
            statNames.Length
        );
    }


    private Rect GetStatNameRect(
        Rect statArea,
        int index)
    {
        float dividerX =
            statArea.x +
            statArea.width *
            statDividerRatio;

        return new Rect(
            statArea.x,

            statArea.y +
            index *
            statHeight,

            dividerX -
            statArea.x -
            statDividerGap,

            statHeight
        );
    }


    private Rect GetStatValueRect(
        Rect statArea,
        int index)
    {
        float dividerX =
            statArea.x +
            statArea.width *
            statDividerRatio;

        return new Rect(
            dividerX +
            statDividerGap,

            statArea.y +
            index *
            statHeight,

            statArea.xMax -
            dividerX -
            statDividerGap,

            statHeight
        );
    }


    private Rect GetStatDividerRect(
        Rect statArea)
    {
        float x =
            statArea.x +
            statArea.width *
            statDividerRatio;

        return new Rect(
            x,
            statArea.y + 2f,
            2f,
            statArea.height - 4f
        );
    }


    private void CalculateFinalSize()
    {
        float aspect = 0.7f;

        Sprite image =
            currentHighlightedCard.Image;


        if (image != null &&
            image.rect.height > 0f)
        {
            aspect =
                image.rect.width /
                image.rect.height;
        }
        else if (
            sourceCardRect.height > 0f)
        {
            aspect =
                sourceCardRect.width /
                sourceCardRect.height;
        }


        float imageHeight =
            cardHeight;

        float imageWidth =
            Mathf.Clamp(
                imageHeight *
                aspect,

                140f,
                230f
            );


        finalCardSize =
            new Vector2(
                imageWidth,
                imageHeight
            );


        float availableDescWidth =
            Screen.width
            - screenMargin * 2f
            - padding * 2f
            - imageWidth
            - gap;


        float actualDescWidth =
            Mathf.Max(
                100f,
                Mathf.Min(
                    descWidth,
                    availableDescWidth
                )
            );


        GUIContent content =
            new GUIContent(
                currentHighlightedCard.Desc ?? ""
            );


        float textHeight =
            descStyle.CalcHeight(
                content,
                actualDescWidth
            );


        float statsHeight =
            statHeight *
            statNames.Length;


        float rightHeight =
            textHeight +
            statsGap +
            statsHeight;


        float bodyHeight =
            Mathf.Max(
                imageHeight,
                rightHeight
            );


        finalWindowSize =
            new Vector2(
                padding * 2f +
                imageWidth +
                gap +
                actualDescWidth,

                padding * 2f +
                bodyHeight
            );
    }


    // ─────────────────────────────────────
    // Formatting
    // ─────────────────────────────────────

    private static string FormatRPS(
        int value)
    {
        if (!System.Enum.IsDefined(
                typeof(RPSType),
                value))
        {
            return
                $"<color=#888888>{value}</color>";
        }


        RPSType rps =
            (RPSType)value;


        return rps switch
        {
            RPSType.Rock =>
                "<color=#FFB86C>바위</color>",

            RPSType.Paper =>
                "<color=#8BE9FD>보</color>",

            RPSType.Scissor =>
                "<color=#FF79C6>가위</color>",

            _ =>
                $"<color=#888888>{value}</color>"
        };
    }


    private static string FormatMove(
        int value,
        bool self)
    {
        if (value == 0)
            return
                "<color=#888888>0칸</color>";


        // 자신:
        // + 이동 = 유리
        //
        // 상대:
        // - 이동 = 상대를 뒤로 보내므로 유리
        bool beneficial =
            self
                ? value > 0
                : value < 0;


        string color =
            beneficial
                ? "#50FA7B"
                : "#FF5555";


        string sign =
            value > 0
                ? "+"
                : "";


        return
            $"<color={color}>{sign}{value}칸</color>";
    }


    private static string FormatBlock(
        int turns,
        bool opponent)
    {
        if (turns <= 0)
            return
                "<color=#888888>0턴</color>";


        string color =
            opponent
                ? "#50FA7B"
                : "#FF5555";


        return
            $"<color={color}>{turns}턴</color>";
    }


    // ─────────────────────────────────────
    // Card Detection
    // ─────────────────────────────────────

    private CardUI FindCardUnderCursor()
    {
        if (CardCanvas.currentPlayerCards == null)
            return null;


        Mouse mouse =
            Mouse.current;

        if (mouse == null)
            return null;


        // UGUI Screen 좌표.
        Vector2 screenMouse =
            mouse.position.ReadValue();


        for (
            int i =
                CardCanvas
                .currentPlayerCards
                .Count - 1;

            i >= 0;

            i--)
        {
            CardUI card =
                CardCanvas
                .currentPlayerCards[i];


            if (card?.Object == null)
                continue;


            if (!card.Object
                .TryGetComponent(
                    out RectTransform rect))
                continue;


            Camera cam =
                GetUICamera(rect);


            if (RectTransformUtility
                .RectangleContainsScreenPoint(
                    rect,
                    screenMouse,
                    cam))
            {
                return card;
            }
        }


        return null;
    }


    // ─────────────────────────────────────
    // UGUI Rect → IMGUI Rect
    // ─────────────────────────────────────

    private static Rect GetIMGUIRect(
        RectTransform rect)
    {
        Vector3[] corners =
            new Vector3[4];


        rect.GetWorldCorners(
            corners
        );


        Camera cam =
            GetUICamera(rect);


        Vector2 min =
            new Vector2(
                float.MaxValue,
                float.MaxValue
            );


        Vector2 max =
            new Vector2(
                float.MinValue,
                float.MinValue
            );


        for (int i = 0;
             i < corners.Length;
             i++)
        {
            Vector2 p =
                RectTransformUtility
                    .WorldToScreenPoint(
                        cam,
                        corners[i]
                    );


            min =
                Vector2.Min(
                    min,
                    p
                );


            max =
                Vector2.Max(
                    max,
                    p
                );
        }


        // UGUI:
        // 좌하단 = 0
        //
        // IMGUI:
        // 좌상단 = 0
        return new Rect(
            min.x,
            Screen.height -
            max.y,

            max.x -
            min.x,

            max.y -
            min.y
        );
    }


    private static Camera GetUICamera(
        RectTransform rect)
    {
        Canvas canvas =
            rect.GetComponentInParent<
                Canvas>();


        if (canvas == null)
            return null;


        Canvas root =
            canvas.rootCanvas;


        if (root.renderMode ==
            RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }


        return root.worldCamera;
    }


    // ─────────────────────────────────────
    // Style
    // ─────────────────────────────────────

    private bool EnsureStyles()
    {
        if (stylesReady)
            return true;


        if (!GUIStyleMaker.Initialized)
            return false;


        windowStyle =
            GUIStyleMaker.Box(
                Obsidian,
                BrilliantWhite
            );


        descStyle =
            GUIStyleMaker.Label(
                BrilliantWhite,
                17,
                TextAnchor.UpperLeft
            )
            .Wrap(true)
            .RichText(true);


        statNameStyle =
            GUIStyleMaker.Label(
                BrilliantWhite,
                15,
                TextAnchor.MiddleLeft
            )
            .RichText(true);


        statValueStyle =
            GUIStyleMaker.Label(
                BrilliantWhite,
                15,
                TextAnchor.MiddleLeft
            )
            .RichText(true);


        dividerStyle =
            GUIStyleMaker.Box(
                DividerColor
            );


        stylesReady = true;

        return true;
    }


    // ─────────────────────────────────────
    // Cleanup
    // ─────────────────────────────────────

    private void DestroyTooltip()
    {
        if (window != null)
            GUIManager.Unregister(
                window
            );


        if (cardImage != null)
            GUIManager.Unregister(
                cardImage
            );


        if (descLabel != null)
            GUIManager.Unregister(
                descLabel
            );


        if (statDivider != null)
            GUIManager.Unregister(
                statDivider
            );


        for (int i = 0;
             i < statNames.Length;
             i++)
        {
            if (statNames[i] != null)
            {
                GUIManager.Unregister(
                    statNames[i]
                );
            }


            if (statValues[i] != null)
            {
                GUIManager.Unregister(
                    statValues[i]
                );
            }


            statNames[i] = null;
            statValues[i] = null;
        }


        window = null;
        cardImage = null;
        descLabel = null;
        statDivider = null;

        activeCardUI = null;

        currentHighlightedCard =
            null;

        animationProgress = 0f;
        graceTimer = 0f;

        closing = false;
    }


    // ─────────────────────────────────────
    // Utility
    // ─────────────────────────────────────

    private static Rect TinyRect(
        Vector2 center)
    {
        Vector2 size =
            new Vector2(
                2f,
                2f
            );

        return new Rect(
            center -
            size * 0.5f,

            size
        );
    }


    private static Rect LerpRect(
        Rect from,
        Rect to,
        float t)
    {
        return new Rect(
            Vector2.LerpUnclamped(
                from.position,
                to.position,
                t
            ),

            Vector2.LerpUnclamped(
                from.size,
                to.size,
                t
            )
        );
    }
}