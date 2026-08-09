using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.InputSystem;
using System.Collections;
using System;
using UnityEngine.UI;

public class CardUI
{
    public BaseCardData Data;
    public GameObject Object;

    public CardUI(BaseCardData data, GameObject obj)
    {
        Data = data;
        Object = obj;
    }
}

public class CardCanvas : MonoBehaviour
{
    //절대 왕정 aka 중앙 처리 CardCanvas로직
    public GameObject CardPrefab;

    public static CardCanvas instance;

    public static List<CardUI> currentPlayerCards = new();

   [Header("Card Layout")] //카드 배치계획 최종찐최종완료이제그만최종진짜
    public float cardSpacing = 320f;
    public float cardY = -250f;
    public float cardSpawnY = -700f;
    [Header("Card Exit")]
    public float cardExitX = 1000f;
    public float cardExitY = -500f;

    [Header("Dragging")] //카드 드래그 로직
    public float dragDetectionDistance = 200f;
    public float dragSmoothSpeed = 15f;

    public CardUI currentDraggingCard;
    public RectTransform chosenCardArea;
    public CardUI chosenCard;
    public CardUI enemyCard;

    [Header("Battle Animation")]
    public RectTransform battlePlace;

    public float battleCardOffset = 260f;
    public float enemySpawnOffset = 900f;

    public float battleEnterDuration = 0.35f;
    public float battleAttackDuration = 0.12f;
    public float battleRecoilDuration = 0.16f;

    private RectTransform canvasRect;
    
    // 드래그하는 카드가 현재 따라갈 목표 위치
    private Vector2 dragTargetPosition;

    private void Awake()
    {
        instance = this; //싱글톤
        canvasRect = GetComponent<RectTransform>();
    }

    public void SpawnUIPlayerCards(BaseCardData card)
    {
        if (card == null || CardPrefab == null) //카드 유효 검사
            return;

        GameObject cardObj = Instantiate(CardPrefab, transform); //카드 생성

        Transform titleObj = cardObj.transform.Find("Title");
        Transform descObj = cardObj.transform.Find("Desc");
        cardObj.GetComponent<Image>().sprite = card.Image?? default;

        currentPlayerCards.Add(new CardUI(card, cardObj)); //Dictionary에 저장

        if (titleObj != null && titleObj.TryGetComponent<TextMeshProUGUI>(out var title))
        {
            title.text = card.Title;
        }

        if (descObj != null && descObj.TryGetComponent<TextMeshProUGUI>(out var desc))
        {
            desc.text = card.Desc;
        }

        int index = currentPlayerCards.Count - 1; //새로 생긴 카드의 위치

        // 중앙 정렬이 카드 수에 따라 달라지므로 새 카드가 늘어나면 기존 카드도 밀어줘야 한다.
        // 새 카드는 생성 애니메이션으로 따로 처리하니 여기서는 제외.
        for (int i = 0; i < index; i++)
        {
            CardUI oldCard = currentPlayerCards[i];

            if (oldCard?.Object == null)
                continue;

            SetCardPosition(oldCard.Object, i);
        }

        SetCardPosition(cardObj, index, true); //새로 생긴 카드를 적당한 곳에 놓기
    }

    // maxCards가 아니라 실제로 들고 있는 카드 수로 중앙 정렬한다.
    // maxCards로 계산하면 카드가 3장 미만일 때 빈 슬롯만큼 왼쪽으로 치우친다.
    public Vector2 GetCardPosition(int index)
    {
        return GetDynamicCardPosition(index, currentPlayerCards.Count);
    }

    public Vector2 GetDynamicCardPosition(int index, int count) //드래깅 중 카드 위치 변화
    {
        float startX =
            -((count - 1) * cardSpacing) / 2f;

        return new Vector2(
            startX + index * cardSpacing,
            cardY
        );
    }

    public void SetCardPosition(GameObject obj,int index,bool spawnAnimation = false)
    {
        if (obj == null)
        {
            Debug.LogError($"[CardCanvas.cs 107] obj를 찾으려고 했으나 Null임. return;");
            return;
        }

        if (currentDraggingCard != null && obj == currentDraggingCard.Object)
        {
            return;
        }

        RectTransform rect = obj.GetComponent<RectTransform>();

        if (rect == null)
        {
            Debug.LogError($"[CardCanvas.cs 118]{obj.name}에 RectTransform 없음. return;");
            return;
        }

        Vector2 target = GetCardPosition(index);

        rect.DOKill();

        if (spawnAnimation) //생성 에니메이션이라면 바닥에서 출발
        {
            rect.anchoredPosition =
                new Vector2(target.x, cardSpawnY);

            rect.DOAnchorPos(target, 0.7f)
                .SetEase(Ease.OutBack);
        }
        else //아니라면 원래 위치로 복구하는 에니메이션만
        {
            rect.DOAnchorPos(target, 0.3f)
                .SetEase(Ease.OutCubic);
        }
    }

    //모든 카드를 원래 위치로 복구.
    public void RefreshCardPositions()
    {
        for (int i = 0; i < currentPlayerCards.Count; i++)
        {
            CardUI card = currentPlayerCards[i];

            if (card?.Object == null)
                continue;

            SetCardPosition(card.Object, i);
        }
    }


    private void RefreshCardsWhileDragging()
    {
        int count = currentPlayerCards.Count;

        for (int i = 0; i < count; i++)
        {
            GameObject obj = currentPlayerCards[i].Object;

            if (obj == null || !obj.TryGetComponent(out RectTransform rect))
            {
                Debug.LogError($"[CardCanvas.cs 168]Obj 무효. return;");
                continue;
            }

            Vector2 target =
                GetDynamicCardPosition(i, count);

            rect.DOKill();

            rect.DOAnchorPos(target, 0.25f)
                .SetEase(Ease.OutCubic);
        }
    }

    //드래그 헬퍼
    public static void StartCardDrag(Vector2 mousePos)
    {
        if (instance == null)
            return;

        instance.StartCardDragInternal(mousePos);
    }


    private void StartCardDragInternal(Vector2 mousePos)
    {
        // 배틀이 도는 중에 카드를 집으면 chosenCard를 도로 뺏어오게 된다.
        // 그러면 연출은 이미 파괴한 오브젝트를 붙들고, 손패 데이터와도 어긋난다
        if(GameManager.gameManager != null && GameManager.gameManager.IsTurnResolving)
            return;

        //결정한 카드에 드래그를 시도한다면 드래그 카드로 교체하고 결정 해제.
        if (chosenCard != null && chosenCard.Object.TryGetComponent(out RectTransform ChosenRect))
        {
            bool isInside =
                RectTransformUtility.RectangleContainsScreenPoint(
                    ChosenRect,
                    mousePos,
                    GetUICamera()
                );

            if (isInside)
            {
                currentDraggingCard = chosenCard;
                chosenCard = null;
            }
        }

        if (currentDraggingCard != null)
            return;

        for (int i = currentPlayerCards.Count - 1; i >= 0; i--)
        {
            CardUI card = currentPlayerCards[i];

            if (card?.Object != null && card.Object.TryGetComponent(out RectTransform rect))
            {
                bool isInside =
                    RectTransformUtility.RectangleContainsScreenPoint(
                        rect,
                        mousePos,
                        GetUICamera()
                    );

                if (!isInside)
                    continue;
                currentDraggingCard = card;
                rect.DOKill();
                currentDraggingCard.Object.transform.SetAsLastSibling();
                currentPlayerCards.Remove(currentDraggingCard);

                RefreshCardsWhileDragging();
                UpdateDragTarget(mousePos);
                Debug.Log($"드래그 시작: {card.Data.Title}");

                return;
            }
        }
    }
    public static void EndCardDrag()
    {
        if (instance == null)
            return;

        instance.EndCardDragInternal();
    }

    public void ReturnChosenCard()
    {
        if (chosenCard == null)
            return;

        currentPlayerCards.Add(chosenCard);

        chosenCard = null;

        RefreshCardPositions();
    }

    private void SetChosenCard(CardUI newCard)
    {
        if (newCard == null || newCard.Object == null)
            return;

        if (chosenCard != null)
        {
            ReturnChosenCard();
        }

        chosenCard = newCard;

        RectTransform rect =
            chosenCard.Object.GetComponent<RectTransform>();

        if (rect == null)
            return;

        rect.DOKill();

        rect.DOAnchorPos(
            chosenCardArea.anchoredPosition,
            0.3f
        ).SetEase(Ease.OutCubic);
    }

    private void EndCardDragInternal()
    {
        if (currentDraggingCard == null)
            return;

        RectTransform draggingRect =
            currentDraggingCard.Object
                .GetComponent<RectTransform>();
        
        bool isOnChosen =
        RectTransformUtility.RectangleContainsScreenPoint(
            chosenCardArea,
            Mouse.current.position.ReadValue(),
            GetUICamera()
        );

        if(isOnChosen)
        {
            SetChosenCard(currentDraggingCard);
        }
        else
        {
            int targetIndex =
            GetNearestInsertIndex(
                draggingRect.anchoredPosition.x
            );

            currentPlayerCards.Insert(
                targetIndex,
                currentDraggingCard
            );

                    Debug.Log(
                $"카드 드래그 종료: " +
                $"{currentDraggingCard.Data.Title}, " +
                $"Index = {targetIndex}"
            );
        }

        currentDraggingCard = null;

        RefreshCardPositions();
    }

    public void SetResult()
    {
        if(chosenCard == null || GameManager.gameManager == null)
            return;

        // 연타 방지. GameManager에도 같은 잠금이 있지만 여기서 먼저 끊어야
        // 배틀 연출이 끝나기 전(chosenCard가 아직 살아있는 동안) 같은 카드가 두 번 소비되지 않는다
        if(GameManager.gameManager.IsTurnResolving)
            return;

        GameManager.gameManager.PlayerCardSelected(chosenCard);
    }

    private int GetNearestInsertIndex(float x)
    {
        int finalCount = currentPlayerCards.Count + 1;

        float startX =
            -((finalCount - 1) * cardSpacing) / 2f;

        int index = Mathf.RoundToInt(
            (x - startX) / cardSpacing
        );

        return Mathf.Clamp(
            index,
            0,
            currentPlayerCards.Count
        );
    }

    private void Update()
    {
        if (currentDraggingCard == null || Mouse.current == null)
            return;

        RectTransform rect =
            currentDraggingCard.Object.GetComponent<RectTransform>();

        if (rect == null)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            mousePos,
            GetUICamera(),
            out Vector2 localPoint))
        {
            Vector3 target = new Vector3(
                localPoint.x,
                localPoint.y,
                rect.localPosition.z
            );

            float t =
                1f - Mathf.Exp(-dragSmoothSpeed * Time.deltaTime);

            rect.localPosition =
                Vector3.Lerp(rect.localPosition, target, t);
        }
    }

    public void ReturnCardPosition(GameObject obj)
    {
        if (obj == null)
            return;

        // 퇴장 트윈이 0.5초 도는 동안 오브젝트가 살아있는데,
        // 학습 중에는 턴이 초당 수십 번 돌아 그게 계속 쌓인다. 바로 없앤다
        if (TrainingMode.Enabled)
        {
            Destroy(obj);
            return;
        }

        RectTransform rect = obj.GetComponent<RectTransform>();

        if (rect == null)
        {
            Debug.LogError($"{obj.name}에 RectTransform 없음");
            return;
        }

        rect.DOKill();

        Vector2 target = new Vector2(
            cardExitX,
            cardExitY
        );

        rect.DOAnchorPos(target, 0.5f)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                Destroy(obj);
            });
    }

    public GameObject SpawnUIEnemyCard(BaseCardData card)
    {
        if (card == null || CardPrefab == null) //카드 유효 검사
            return null;

        GameObject cardObj = Instantiate(CardPrefab, transform); //카드 생성

        Transform titleObj = cardObj.transform.Find("Title");
        Transform descObj = cardObj.transform.Find("Desc");

        enemyCard = new CardUI(card, cardObj); //Dictionary에 저장

        if (titleObj != null && titleObj.TryGetComponent<TextMeshProUGUI>(out var title))
        {
            title.text = card.Title;
        }

        if (descObj != null && descObj.TryGetComponent<TextMeshProUGUI>(out var desc))
        {
            desc.text = card.Desc;
        }
        cardObj.GetComponent<Image>().sprite = card.Image?? default;
        return cardObj;
    }

    // callback은 배틀 판정(ResolveBattleCor)을 여는 유일한 통로다.
    // 연출을 못 하는 상황에서 그냥 yield break하면 판정도 턴 종료도 영영 안 와서
    // 게임이 멈춘다 — 연출은 포기하되 콜백은 반드시 부른다
    public IEnumerator BattleWithEnemy(BaseCardData enemy, Action callback)
    {
        if (enemy == null)
        {
            Debug.LogError("[BattleWithEnemy] 상대 카드 없음. 연출을 건너뛴다");
            callback?.Invoke();
            yield break;
        }

        if (chosenCard == null || chosenCard.Object == null)
        {
            Debug.LogError("[BattleWithEnemy] chosenCard 없음. 연출을 건너뛴다");
            callback?.Invoke();
            yield break;
        }

        if (battlePlace == null)
        {
            Debug.LogError("[BattleWithEnemy] battlePlace 없음. 연출을 건너뛴다");
            callback?.Invoke();
            yield break;
        }

        GameObject enemyObj = SpawnUIEnemyCard(enemy);

        if (enemyObj == null)
        {
            Debug.LogError("[BattleWithEnemy] 상대 카드 UI 생성 실패. 연출을 건너뛴다");
            callback?.Invoke();
            yield break;
        }

        if (!chosenCard.Object.TryGetComponent(out RectTransform playerRect) ||
            !enemyObj.TryGetComponent(out RectTransform enemyRect))
        {
            Debug.LogError("[BattleWithEnemy] RectTransform 없음. 연출을 건너뛴다");
            callback?.Invoke();
            yield break;
        }

        playerRect.DOKill();
        enemyRect.DOKill();

        enemyRect.SetAsLastSibling();
        playerRect.SetAsLastSibling();

        Vector2 center = battlePlace.anchoredPosition;

        Vector2 playerReady =
            center + Vector2.left * battleCardOffset;

        Vector2 enemyReady =
            center + Vector2.right * battleCardOffset;
        
        Vector3 showPos =
            (Vector3)center + Vector3.down * battleCardOffset + Vector3.forward * -10f;

        // 적 카드 오른쪽 바깥에서 시작
        enemyRect.anchoredPosition =
            enemyReady + Vector2.right * enemySpawnOffset;

        enemyRect.localRotation = Quaternion.identity;
        playerRect.localRotation = Quaternion.identity;

        Image enemyImage = enemyRect.GetComponent<Image>();

        if (enemyImage != null)
            enemyImage.color = Color.softRed;

        Sequence battle = DOTween.Sequence();


        // ==================================================
        // 입장
        // ==================================================

        battle.Append(
            playerRect
                .DOAnchorPos(
                    playerReady,
                    battleEnterDuration
                )
                .SetEase(Ease.OutCubic)
        );

        battle.Join(
            enemyRect
                .DOAnchorPos(
                    enemyReady,
                    battleEnterDuration
                )
                .SetEase(Ease.OutBack)
        );

        battle.AppendInterval(0.5f);

        battle.Append(
            playerRect.DOAnchorPos(showPos,
                battleEnterDuration
            ).SetEase(Ease.InOutQuint)
        );

        battle.Join(
            playerRect.DOScale(2f, 0.2f).SetEase(Ease.InOutQuad)
        );
        battle.JoinCallback(() =>
            MoteManager.instance.SpawnMoteAtScreen(
                "<color=green>안녕 해</color>",
                playerRect.position + new Vector3(-50, 50),
                new Vector2(0.5f, 0.5f),
                2f
            )
        );

        
        battle.AppendInterval(1f);

        battle.Append(
            playerRect.DOAnchorPos(playerReady,
                battleEnterDuration
            ).SetEase(Ease.InOutQuint)
        );

        battle.Join(
            playerRect.DOScale(1f, 0.2f).SetEase(Ease.InOutQuad)
        );

        battle.AppendInterval(0.15f);
        battle.AppendCallback(() =>
        {
            enemyRect.SetAsLastSibling();
        });

        battle.Append(
            enemyRect.DOAnchorPos(showPos,
                battleEnterDuration
            ).SetEase(Ease.InOutQuint)
        );

        battle.Join(
            enemyRect.DOScale(2f, 0.2f).SetEase(Ease.InOutQuad)
        );

        battle.JoinCallback(() =>
            MoteManager.instance.SpawnMoteAtScreen(
                "<color=red>안녕 안해</color>",
                enemyRect.position + new Vector3(50, 50),
                new Vector2(0.5f, 0.5f),
                2f
            )
        );
        battle.AppendInterval(1f);

        battle.Append(
            enemyRect.DOAnchorPos(enemyReady,
                battleEnterDuration
            ).SetEase(Ease.InOutQuint)
        );

        battle.Join(
            enemyRect.DOScale(1f, 0.2f).SetEase(Ease.InOutQuad)
        );

        battle.AppendInterval(0.15f);

        battle.Append(
            playerRect
                .DOAnchorPos(
                    playerReady + Vector2.left * 1000f,
                    0.5f
                )
                .SetEase(Ease.InExpo)
        );

        battle.Join(
            enemyRect
                .DOAnchorPos(
                    enemyReady + Vector2.right * 1000f,
                    0.5f
                )
                .SetEase(Ease.InExpo)
        );

        battle.Join(enemyRect.GetComponent<Image>().DOFade(0, 0.5f));
        battle.Join(playerRect.GetComponent<Image>().DOFade(0,0.5f));

        yield return battle.WaitForCompletion();

        yield return new WaitForSeconds(1f);
        Destroy(enemyRect.gameObject);
        Destroy(chosenCard.Object);

        Debug.Log(
            $"Battle Animation 완료: " +
            $"{chosenCard.Data.Title} VS {enemy.Title}"
        );

        // ==================================================
        // 카드 복귀
        // ==================================================

        

        chosenCard = null;


        // ==================================================
        // 완료 Callback
        // ==================================================

        callback?.Invoke();
    }

    public void SpawnTrapCard(BaseCardData data)
    {
        if (data == null)
            return;

        if (battlePlace == null)
        {
            Debug.LogError("[SpawnTrapCard] battlePlace 없음");
            return;
        }

        GameObject trapObj = SpawnUIEnemyCard(data);

        if (trapObj == null)
            return;

        if (trapObj == null || !trapObj.TryGetComponent(out RectTransform trapRect))
        {
            Debug.LogError("[SpawnTrapCard] RectTransform 없음");
            return;
        }
        
        MoteManager.instance.SpawnMoteAtScreen(
            "<color=orange>함정 카드 발동!</color>",
            trapRect.position + new Vector3(0, 50),
            new Vector2(0.5f, 0.5f),
            2f
        );
        trapObj.GetComponent<Image>().color = Color.orange;    

        trapRect.DOKill();

        trapRect.SetAsLastSibling();

        Vector2 center = battlePlace.anchoredPosition;

        Vector2 trapReady =
            center + Vector2.up * battleCardOffset * 2f;
        
        Vector3 showPos =
            (Vector3)center + Vector3.down * battleCardOffset + Vector3.forward * -10f;

        trapRect.anchoredPosition =
            trapReady;

        Sequence trapSeq = DOTween.Sequence();

        trapSeq.Append(
            trapRect.DOAnchorPos(showPos, 0.5f)
                .SetEase(Ease.OutBack)
        );

        trapSeq.AppendInterval(1f);
        trapSeq.Append(
            trapRect.DOAnchorPos(showPos - Vector3.up * battleCardOffset * 4f, 0.5f)
                .SetEase(Ease.InBack)
        );
        trapSeq.Join(trapRect.GetComponent<Image>().DOFade(0, 0.5f));
        trapSeq.OnComplete(() =>
        {
            Destroy(trapRect.gameObject);
        });
    }
    

    // 에이전트가 낸 카드를 드래그 없이 chosenCard 자리에 올린다.
    //
    // 사람이 낼 때는 EndCardDragInternal → SetChosenCard가 이걸 해주는데,
    // 에이전트 경로에는 드래그 자체가 없어 chosenCard가 비어 있다.
    // 그 상태로 배틀 연출을 켜면 연출할 카드가 없어 BattleWithEnemy가 통째로 빠진다.
    //
    // 손패에서 빼기만 하고 Destroy는 안 한다 — 배틀 연출이 끝내면서 파괴한다
    public bool ChooseCardByData(BaseCardData data)
    {
        if (data == null)
            return false;

        for (int i = 0; i < currentPlayerCards.Count; i++)
        {
            CardUI card = currentPlayerCards[i];

            if (card == null || card.Data != data || card.Object == null)
                continue;

            currentPlayerCards.RemoveAt(i);
            SetChosenCard(card);
            RefreshCardPositions();
            return true;
        }

        Debug.LogWarning($"[CardCanvas] {data.Title}의 UI 카드를 손패에서 못 찾음. 연출 없이 진행한다");
        return false;
    }

    public void RemoveCardUI(BaseCardData data)
    {
        if (data == null)
            return;

        for (int i = 0; i < currentPlayerCards.Count; i++)
        {
            CardUI card = currentPlayerCards[i];

            if (card == null || card.Data != data)
                continue;

            currentPlayerCards.RemoveAt(i);
            ReturnCardPosition(card.Object);
            RefreshCardPositions();

            // 이 카드의 설명 툴팁이 떠 있었다면 같이 닫는다.
            // 안 그러면 손패에 없는 카드의 설명이 화면에 남는다
            if (CardHighLighter.instance != null && CardHighLighter.instance.currentHighlightedCard == data)
                CardHighLighter.instance.ClearHighlight();

            return;
        }
    }

    public void ClearAllCards()
    {
        for (int i = 0; i < currentPlayerCards.Count; i++)
        {
            CardUI card = currentPlayerCards[i];

            if (card?.Object == null)
                continue;

            ReturnCardPosition(card.Object);
        }

        currentPlayerCards.Clear();
        currentDraggingCard = null;

        // 손패를 통째로 비우므로 떠 있던 툴팁도 전부 무효다
        if (CardHighLighter.instance != null)
            CardHighLighter.instance.ClearHighlight();
    }

    private void UpdateDragTarget(Vector2 mouseScreenPosition)
    {
        if (canvasRect == null)
            return;

        if (RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                canvasRect,
                mouseScreenPosition,
                GetUICamera(),
                out Vector2 localPoint))
        {
            dragTargetPosition = localPoint;
        }
    }

    private Camera GetUICamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }
}