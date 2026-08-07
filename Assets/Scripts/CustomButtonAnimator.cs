using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

[Serializable]
public class TweenNode
{
    public GameObject Object;
    public Vector2 Size;
    public Ease ease = Ease.OutQuad;
    public float duration = 0.2f;
}

public class CustomButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public List<TweenNode> Normal;
    public List<TweenNode> Highlighted;
    public List<TweenNode> Pressed;
    public List<TweenNode> Selected;
    public List<TweenNode> Disabled;

    [Header("Debounce Settings")]
    [SerializeField] private float debounceTime = 0.08f; 

    private bool isHovered = false;
    private bool isPressed = false;
    private bool isSelected = false;
    
    private bool isExitDebouncing = false; 
    private Tween debounceTimerTween = null;

    private void Start()
    {
        ApplyState(Normal);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;

        // 디바운스 잠금이 걸려있다면 하이라이트를 무시합니다.
        if (isExitDebouncing) return;
        
        // 이미 선택된 버튼이거나 클릭 중이라면 하이라이트 연출을 패스합니다.
        if (isSelected || isPressed) return;

        ApplyState(Highlighted);
        SoundManager.AudioShot(transform.position, "Highlighted", 1);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        if (isPressed) return;

        // 이미 선택된 버튼이라면 나갈 때 Selected 상태를 유지, 아니라면 Normal로 복구
        ApplyState(isSelected ? Selected : Normal);

        StartExitDebounce();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 누르는 순간 혹시 돌고 있을지 모르는 디바운스 타이머를 즉시 파괴합니다.
        KillDebounceTimer();

        isPressed = true;
        ApplyState(Pressed);
        SoundManager.AudioShot(transform.position, "Pressed", 1);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;

        bool isRealHovered = eventData.pointerCurrentRaycast.gameObject == this.gameObject || 
                             (eventData.pointerCurrentRaycast.gameObject != null && 
                              eventData.pointerCurrentRaycast.gameObject.transform.IsChildOf(this.transform));

        // 클릭 후 손을 뗐을 때 여전히 버튼 위라면
        if (isRealHovered && isHovered)
        {
            // 만약 클릭으로 인해 Selected가 되었다면 Highlighted 대신 Selected 연출을 지켜줘야 합니다.
            if (isSelected)
            {
                ApplyState(Selected);
            }
            else if (!isExitDebouncing)
            {
                ApplyState(Highlighted);
            }
        }
        else
        {
            isHovered = false;
            ApplyState(isSelected ? Selected : Normal);
            StartExitDebounce();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 클릭이 정상 성사되었으므로 남아있는 디바운스 잠금을 완전히 풀어서 다음 진입을 준비합니다.
        KillDebounceTimer();

        if (isSelected) return;

        isSelected = true;
        ApplyState(Selected);
        SoundManager.AudioShot(transform.position, "Selected", 1);
    }

    private void StartExitDebounce()
    {
        KillDebounceTimer();
        isExitDebouncing = true;

        debounceTimerTween = DOVirtual.DelayedCall(debounceTime, () =>
        {
            isExitDebouncing = false; 
        }, false).SetLink(gameObject);
    }

    private void KillDebounceTimer()
    {
        if (debounceTimerTween != null && debounceTimerTween.IsActive())
        {
            debounceTimerTween.Kill();
        }
        debounceTimerTween = null;
        isExitDebouncing = false; // [핵심 수정] 락 플래그를 확실하게 초기화합니다.
    }

    // 토글 메뉴나 라디오 버튼처럼 외부 매니저가 이 버튼의 선택을 해제할 때 호출되는 메서드
    public void DeselectButton()
    {
        // 선택 해제 시에도 안전하게 디바운스를 리셋합니다.
        KillDebounceTimer();

        if (!isSelected) return;
        isSelected = false;
        
        // 선택이 풀렸을 때 마우스가 위에 있다면 하이라이트로, 없다면 노멀로 복귀
        ApplyState(isHovered ? Highlighted : Normal);
    }

    public void SetDisabled(bool isDisabled)
    {
        enabled = !isDisabled;
        if (isDisabled)
        {
            KillDebounceTimer();
            isHovered = false;
            isPressed = false;
            isSelected = false;
        }
        ApplyState(isDisabled ? Disabled : Normal);
    }

    private void ApplyState(List<TweenNode> stateNodes)
    {
        if (stateNodes == null || stateNodes.Count == 0) return;

        foreach (var node in stateNodes)
        {
            if (node.Object == null) continue;

            RectTransform rectTransform = node.Object.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.DOKill();
                rectTransform.DOSizeDelta(node.Size, node.duration)
                             .SetEase(node.ease)
                             .SetLink(node.Object);
            }
        }
    }

    private void OnDestroy()
    {
        KillDebounceTimer();
    }
}
