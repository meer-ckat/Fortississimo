using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

// 손패 카드를 우클릭하면 설명 툴팁을 띄운다.
// 같은 카드를 다시 누르거나 카드가 없는 곳을 누르면 닫힌다.
//
// 입력을 EventSystem이 아니라 Mouse.current로 직접 본다. CardCanvas의 드래그 처리와
// 같은 방식이라 둘이 같은 좌표계를 쓰고, 툴팁이 클릭을 가로채지도 않는다.
public class CardHighLighter : MonoBehaviour
{
    public static CardHighLighter instance;

    [Tooltip("비워두면 HediffCanvas의 라벨 프리팹을 그대로 쓴다. 툴팁만 다른 모양으로 갈 때만 채운다")]
    public GameObject labelprefab;

    public float maxWidth = 720f;
    public Vector2 padding = new Vector2(24f, 12f);

    [Tooltip("커서와 툴팁 아래변 사이의 간격. Y를 올리면 툴팁이 커서 위로 더 뜬다")]
    public Vector2 cursorOffset = new Vector2(0f, 20f);

    public GameObject currentHighlightLabel;
    public BaseCardData currentHighlightedCard;
    public Vector2 currentMousePosition;

    void Awake()
    {
        instance = this;
    }

    // 씬을 다시 로드해도 static이 살아남아 파괴된 오브젝트를 가리키는 걸 막는다
    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        Mouse currentMouse = Mouse.current;
        if (currentMouse == null)
            return;

        currentMousePosition = currentMouse.position.ReadValue();

        if (currentMouse.rightButton.wasPressedThisFrame)
            ToggleHighlight();

        // 위치 갱신은 입력 처리 뒤에 온다. 반대로 두면 이번 프레임에 새로 뜬 툴팁이
        // 한 프레임 동안 지난 프레임의 커서 자리에 보인다
        FollowCursor();
    }

    // 커서 아래 카드를 찾아 툴팁을 연다.
    // 같은 카드를 다시 누르면 닫고, 빈 곳을 누르면 그냥 닫는다
    private void ToggleHighlight()
    {
        CardUI hovered = FindCardUnderCursor();
        BaseCardData card = hovered != null ? hovered.Data : null;
        bool sameCard = card != null && card == currentHighlightedCard;

        // 열려 있던 건 무조건 걷어낸다. 다른 카드면 아래에서 새로 뜬다
        ClearHighlight();

        if (card == null || sameCard)
            return;

        HighlightCard(card);
    }

    private CardUI FindCardUnderCursor()
    {
        // 뒤에서부터 본다. 카드가 겹쳐 있으면 위에 그려진 쪽이 잡혀야 한다.
        // CardCanvas의 드래그 집기도 같은 순서로 돈다
        for (int i = CardCanvas.currentPlayerCards.Count - 1; i >= 0; i--)
        {
            CardUI card = CardCanvas.currentPlayerCards[i];

            if (card?.Object == null || !card.Object.TryGetComponent(out RectTransform rect))
                continue;

            // ScreenPointToLocalPointInRectangle은 사각형이 놓인 "평면"에 닿기만 해도 true라
            // 커서가 카드 밖에 있어도 통과한다. 안에 들어왔는지는 이쪽으로 봐야 한다
            if (RectTransformUtility.RectangleContainsScreenPoint(rect, currentMousePosition, GetUICamera()))
                return card;
        }

        return null;
    }

    public void HighlightCard(BaseCardData card)
    {
        if (card == null)
            return;

        GameObject label = SpawnLabel(card.Desc);
        if (label == null)
            return;

        currentHighlightedCard = card;
        currentHighlightLabel = label;

        // 뜨자마자 제자리에 놓는다. 안 그러면 다음 Update까지 프리팹 기본 위치에 한 프레임 보인다
        FollowCursor();
    }

    // 카드가 손패에서 빠지면(내거나 판이 끝나면) 그 카드의 툴팁도 같이 닫아야 한다.
    // 안 그러면 이미 사라진 카드의 설명이 화면에 남는다
    public void ClearHighlight()
    {
        if (currentHighlightLabel != null)
            Destroy(currentHighlightLabel);

        currentHighlightLabel = null;
        currentHighlightedCard = null;
    }

    private void FollowCursor()
    {
        if (currentHighlightLabel == null)
            return;

        if (!currentHighlightLabel.TryGetComponent(out RectTransform labelRect))
            return;

        // 부모 기준으로 변환해야 한다. 캔버스 루트를 기준으로 잡으면
        // 툴팁이 캔버스 바로 아래가 아닐 때 어긋난다
        if (labelRect.parent is not RectTransform parentRect)
        {
            labelRect.position = currentMousePosition + cursorOffset;
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, currentMousePosition, GetUICamera(), out Vector2 local))
            return;

        // cursorOffset은 커서와 툴팁 "아래변" 사이의 간격이다. 가운데 기준으로 잡으면
        // 설명이 길어질수록 툴팁이 아래로 자라 커서와 카드를 덮는다
        Vector2 targetCenter = local + cursorOffset + new Vector2(0f, labelRect.rect.height * 0.5f);

        labelRect.anchoredPosition = SolveAnchoredPosition(parentRect, labelRect, targetCenter);
    }

    // 커서 위치(부모 로컬 좌표)에 툴팁 "가운데"를 놓기 위한 anchoredPosition을 구한다.
    //
    // 좌표계가 세 개라 그냥 대입하면 어긋난다:
    //   - ScreenPointToLocalPointInRectangle의 결과는 부모의 '피벗'이 원점
    //   - anchoredPosition은 라벨의 '앵커'가 원점
    //   - 그리고 anchoredPosition이 옮기는 지점은 라벨의 '피벗'이지 가운데가 아니다
    //
    // Hediff 라벨 프리팹은 앵커·피벗이 둘 다 좌상단(0,1)이라 이 차이가 그대로 드러난다.
    // 가운데라고 가정하면 툴팁이 720x111만큼 오른쪽 아래로 밀려 뜬다
    private static Vector2 SolveAnchoredPosition(RectTransform parentRect, RectTransform labelRect, Vector2 targetCenter)
    {
        Rect parent = parentRect.rect;
        Vector2 labelSize = labelRect.rect.size;

        // 부모 안에 가둔다. 손패는 화면 가장자리에 붙어 있고 설명은 최대 720이라
        // 끝쪽 카드를 누르면 툴팁이 화면 밖으로 나간다.
        // 툴팁이 부모보다 크면 범위가 뒤집히므로 그 축은 건드리지 않는다
        Vector2 half = labelSize * 0.5f;

        if (parent.width >= labelSize.x)
            targetCenter.x = Mathf.Clamp(targetCenter.x, parent.xMin + half.x, parent.xMax - half.x);

        if (parent.height >= labelSize.y)
            targetCenter.y = Mathf.Clamp(targetCenter.y, parent.yMin + half.y, parent.yMax - half.y);

        // 앵커가 늘어나 있으면(stretch) sizeDelta가 크기가 아니라 여백이라 이 계산이 성립하지 않는다.
        // 라벨 프리팹은 점 앵커지만, 나중에 프리셋을 바꿔도 조용히 틀리지 않게 막아둔다
        if (labelRect.anchorMin != labelRect.anchorMax)
            return targetCenter;

        // 앵커 기준점을 부모 로컬 좌표로 옮긴다
        Vector2 anchorRef = new Vector2(
            Mathf.Lerp(parent.xMin, parent.xMax, labelRect.anchorMin.x),
            Mathf.Lerp(parent.yMin, parent.yMax, labelRect.anchorMin.y));

        // 피벗에서 가운데까지의 거리. 피벗이 (0,1)이면 (+w/2, -h/2)
        Vector2 pivotToCenter = new Vector2(
            (0.5f - labelRect.pivot.x) * labelSize.x,
            (0.5f - labelRect.pivot.y) * labelSize.y);

        return targetCenter - pivotToCenter - anchorRef;
    }

    private Camera GetUICamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    // 라벨 만드는 일은 HediffCanvas가 단독으로 한다. 프리팹도 크기 계산도 보색 배경도
    // 거기 있으므로, 여기서 다시 구현하면 프리팹 구조가 바뀔 때 한쪽만 어긋난다.
    //
    // 다만 부모는 이 오브젝트로 준다 — HediffCanvas 아래에 붙이면 다른 캔버스라
    // 카드 뒤에 그려질 수 있다
    public GameObject SpawnLabel(string text)
    {
        GameObject label = BuildLabel(text);

        if (label == null)
            return null;

        // 같은 캔버스 안에서 카드보다 뒤에 그려지지 않게 맨 앞으로 올린다
        label.transform.SetAsLastSibling();

        if (label.TryGetComponent(out RectTransform labelTransform))
        {
            // 크기를 확정한 직후에 재야 ClampInside가 옳은 값을 본다
            LayoutRebuilder.ForceRebuildLayoutImmediate(labelTransform);
            SoundManager.AudioShot(labelTransform.position, "On", 0.5f);
        }

        return label;
    }

    private GameObject BuildLabel(string text)
    {
        // 툴팁 전용 프리팹을 따로 꽂았으면 그쪽을 쓴다
        if (labelprefab != null)
            return InstantiateOwnPrefab(text);

        if (HediffCanvas.instance == null)
        {
            Debug.LogError("[CardHighLighter] labelprefab도 비었고 HediffCanvas도 없어서 툴팁을 못 띄운다");
            return null;
        }

        return HediffCanvas.instance.CreateDetachedLabel(text, transform);
    }

    private GameObject InstantiateOwnPrefab(string text)
    {
        GameObject label = Instantiate(labelprefab);

        if (!label.TryGetComponent(out RectTransform labelTransform))
        {
            Debug.LogError("[CardHighLighter] labelprefab에 RectTransform이 없음");
            Destroy(label);
            return null;
        }

        labelTransform.SetParent(transform, false);

        var textComponent = label.GetComponentInChildren<TextMeshProUGUI>();

        if (textComponent == null)
        {
            Debug.LogError("[CardHighLighter] labelprefab에 TextMeshProUGUI가 없음");
            Destroy(label);
            return null;
        }

        textComponent.text = text;

        Vector2 textSize = textComponent.GetPreferredValues(text, maxWidth, 0f);
        textSize.x = Mathf.Min(textSize.x, maxWidth);

        textComponent.rectTransform.sizeDelta = textSize;
        labelTransform.sizeDelta = textSize + padding;

        return label;
    }
}
