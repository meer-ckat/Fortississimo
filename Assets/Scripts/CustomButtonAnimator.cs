using System;
using System.Collections.Generic;
using DG.Tweening;
using IMGUI;
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

public class CustomButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public List<TweenNode> Normal;
    public List<TweenNode> Highlighted;
    public List<TweenNode> Pressed;
    public List<TweenNode> Disabled;

    [Header("Debounce Settings")]
    [SerializeField] private float debounceTime = 0.08f; 

    private bool isPressed = false;
    
    private bool isExitDebouncing = false; 
    private Tween debounceTimerTween = null;

    private void Start()
    {
        ApplyState(Normal);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(GUIManager.Blocked) return;

        // 디바운스 잠금이 걸려있다면 하이라이트를 무시합니다.
        if (isExitDebouncing) return;
        
        // 클릭 중이라면 하이라이트 연출을 패스합니다.
        if (isPressed) return;

        ApplyState(Highlighted);
        SoundManager.AudioShot(transform.position, "Highlighted", 1);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isPressed) return;

        ApplyState(Normal);

        StartExitDebounce();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if(GUIManager.Blocked) return;
        // 누르는 순간 혹시 돌고 있을지 모르는 디바운스 타이머를 즉시 파괴합니다.
        KillDebounceTimer();

        isPressed = true;
        ApplyState(Pressed);
        SoundManager.AudioShot(transform.position, "Pressed", 1);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if(GUIManager.Blocked) return;
        isPressed = false;

        GameObject hit = eventData.pointerCurrentRaycast.gameObject;
        bool isRealHovered = hit == this.gameObject || (hit != null && hit.transform.IsChildOf(this.transform));

        // 클릭 후 손을 뗐을 때 여전히 버튼 위라면
        if (isRealHovered)
        {
            if (!isExitDebouncing)
            {
                ApplyState(Highlighted);
            }
        }
        else
        {
            ApplyState(Normal);
            StartExitDebounce();
        }
    }

    private void StartExitDebounce()
    {
        
        KillDebounceTimer();
        isExitDebouncing = true;

        debounceTimerTween = DOVirtual.DelayedCall(debounceTime, () =>
        {
            isExitDebouncing = false;

            // 디바운스가 풀리는 시점에도 여전히 버튼 위에 마우스가 있다면 하이라이트를 복구합니다.
            // (디바운스 중 진입은 무시되므로 이 재적용이 없으면 버튼이 Normal에 갇힙니다)
            if (!isPressed && IsPointerOverButton())
            {
                ApplyState(Highlighted);
            }
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

    // 토글 메뉴나 라디오 버튼처럼 외부 매니저가 선택을 해제할 때 호출되는 메서드 (Selected 제거로 더 이상 상태 없음)
    public void DeselectButton()
    {
        KillDebounceTimer();

        ApplyState(IsPointerOverButton() ? Highlighted : Normal);
    }

    public void SetDisabled(bool isDisabled)
    {
        enabled = !isDisabled;
        if (isDisabled)
        {
            KillDebounceTimer();
            isPressed = false;
        }
        ApplyState(isDisabled ? Disabled : Normal);
    }

    // 이벤트 시스템의 enter/exit 추적과 무관하게, 실제 마우스 위치로 버튼 위인지 판단합니다.
    // 자식 객체가 레이캐스트를 가로채도(부모가 Exit을 받아도) 올바른 hover 상태를 얻습니다.
    private bool IsPointerOverButton()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null) return false;

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.rootCanvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, cam);
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
