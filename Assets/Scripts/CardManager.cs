using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class CardManager : MonoBehaviour
{
    // 손패 상한. UkuleleAgent의 관측 크기와 행동 개수가 이 값에 직접 묶여 있으므로
    // 여기 한 곳에서만 정한다. 바꾸면 BehaviorParameters의
    // Vector Observation Space Size와 Branch 0 Size도 같이 고쳐야 한다
    // (에이전트가 Initialize에서 검사해서 안 맞으면 에러를 띄운다)
    public const int MaxHandSize = 6;

    public static CardManager instance;
    public static int maxCards;
    public static List<BaseCardData> MainCardData;
    public static List<BaseCardData> SpecialCardData;
    public static List<BaseCardData> PlayerCard;
    public static List<BaseCardData> EnemyCard;

    //지금은 카드 데이터를 다루는데 이건 그래픽적으로 어려움. Dictionary로 BaseCardData - BaseCardPrefab 연결하겠음
    //상대 카드가 보일려나 모르겠는데 보인다고 가정하면 PlayerCard EnemyCard 둘다 Dictionary로 하겠음
    private void Awake()
    {
        instance = this;
        // GetDeck이 이미 복사본을 넘겨주므로 여기서 또 감쌀 필요는 없다
        MainCardData = CardParser.GetDeck(CardParser.MainDeck);
        SpecialCardData = CardParser.GetDeck(CardParser.SpecialDeck);
        
        PlayerCard = new List<BaseCardData>();
        EnemyCard = new List<BaseCardData>();

        maxCards = MaxHandSize;

        Debug.Log($"카드 데이터 {MainCardData.Count}개 로드됨.");
        Debug.Log($"스페셜 카드 데이터 {SpecialCardData.Count}개 로드됨.");
    }
    
    public static void GivePlayerCard(BaseCardData card)
    {
        if(card == null)
            return;

        PlayerCard.Add(card);

        // 학습 중에는 카드 UI를 아예 만들지 않는다. 턴마다 Instantiate + TMP 텍스트 + DOTween이
        // 도는데 에이전트는 데이터만 보므로 전부 낭비다.
        // (연출을 켜고 학습된 모델이 노는 걸 볼 때는 skipAnimations를 꺼서 정상 표시된다)
        if(!TrainingMode.Enabled)
            CardCanvas.instance.SpawnUIPlayerCards(card);

        // 상한을 넘으면 가장 오래된 카드가 밀려난다.
        // 데이터만 지우면 UI 카드가 화면에 남으므로 canvas 쪽도 같이 정리해야 한다
        if(PlayerCard.Count > maxCards)
        {
            BaseCardData evicted = PlayerCard[0];
            PlayerCard.RemoveAt(0);
            Debug.Log($"손패가 가득 차 밀려난 카드:{evicted.Title}");
            CardCanvas.instance.RemoveCardUI(evicted);
        }
    }

    // 낸 카드를 손패에서 뺀다.
    //
    // 드래그로 낸 경우: 그 카드는 이미 currentPlayerCards에서 빠져 CardCanvas.chosenCard로
    // 옮겨져 있고, 배틀 연출이 끝나면서 Destroy된다. 여기서 UI를 건드리면 안 된다.
    //
    // 에이전트가 낸 경우: 드래그가 없어 chosenCard가 비어 있고 UI 카드가 손패에 그대로 남는다.
    // 게다가 학습 모드는 배틀 연출 자체를 건너뛰므로 Destroy도 안 일어난다.
    // 그래서 UI 카드가 턴당 1장씩 무한히 쌓인다 — 여기서 직접 걷어내야 한다.
    //
    // chosenCard가 null일 때만 지우는 이유: BaseCardData는 덱에서 인스턴스를 공유하므로
    // 손패에 같은 카드가 여러 장이면 참조로는 구분이 안 된다. 드래그 경로에서 이걸 호출하면
    // 아직 들고 있는 다른 장을 지워버린다
    public static void DiscardPlayedPlayerCard(BaseCardData card)
    {
        if(card == null)
            return;

        PlayerCard.Remove(card);

        if(CardCanvas.instance != null && CardCanvas.instance.chosenCard == null)
            CardCanvas.instance.RemoveCardUI(card);
    }

    public static void GiveEnemyCard(BaseCardData card)
    {
        if(card == null)
            return;

        EnemyCard.Add(card);
        Debug.Log($"적이 카드를 받았습니다:{card.Title}");
        if(EnemyCard.Count > maxCards)
        {
            EnemyCard.RemoveAt(0);
        }
    }

    public static void RemovePlayerCard(BaseCardData card)
    {
        if(card == null || PlayerCard.Count <= 0)
            return;
        
        Debug.Log($"플레이어 카드를 강탈했습니다:{card.Title}");
        PlayerCard.Remove(card);
    }
    public static void RemoveEnemyCard(BaseCardData card)
    {
        if(card == null || EnemyCard.Count <= 0)
            return;
        
        Debug.Log($"적 카드를 강탈했습니다:{card.Title}");
        EnemyCard.Remove(card);
    }

    public static void ClearPlayerCard()
    {
        Debug.Log($"플레이어 카드 초기화");
        CardCanvas.instance.ClearAllCards();
        PlayerCard.Clear();
    }

    public static void ClearEnemyCard()
    {
        Debug.Log($"적 카드 초기화");
        EnemyCard.Clear();
    }

    public static bool ChooseRandomCard(out BaseCardData card)
    {
        card = null;

        if (MainCardData == null || MainCardData.Count == 0)
        {
            Debug.LogWarning("Critical Error. There is no MainCardData in Asset/Data/Main OR the script is hardly damaged.");
            return false;
        }

        card = MainCardData[UnityEngine.Random.Range(0, MainCardData.Count)];

        return card != null;
    }

    public static void GiveRandomCardToPlayer()
    {
        if(ChooseRandomCard(out BaseCardData card))
            GivePlayerCard(card);
    }

    public static void GiveRandomCardToEnemy()
    {
        if(ChooseRandomCard(out BaseCardData card))
            GiveEnemyCard(card);
    }

    public static void ChooseRandomSpecialCard(CardType? cardType, out BaseCardData card)
    {
        card = null;

        if (SpecialCardData == null || SpecialCardData.Count == 0)
        {
            Debug.LogWarning("Critical Error. There is no SpecialCardData in Asset/Data/Special OR the script is hardly damaged.");
            return;
        }

        if(cardType == null)
        {
            card = SpecialCardData[UnityEngine.Random.Range(0, SpecialCardData.Count)];
            return;
        }

        var filteredCards = SpecialCardData.FindAll(c => c.cardType == cardType);
        if (filteredCards.Count > 0)
        {
            card = filteredCards[UnityEngine.Random.Range(0, filteredCards.Count)];
        }
    }

    // 이벤트 칸에 도착했을 때 특수 카드를 한 장 뽑는다.
    //
    // Good/SuperGood은 손패로 들어가고 null이 돌아온다 — 호출부가 더 할 일이 없다.
    // Bad/SuperBad(함정)는 손패를 거치지 않고 즉시 발동해야 하므로 카드를 그대로 돌려준다.
    //
    // 여기서 직접 BattleManager.Battle을 열면 안 된다. 이 메서드는 말 이동이 끝난 뒤
    // BattleManager의 이벤트 처리 루프에서 불리고, 발동도 거기서 순서를 지켜 해야 한다.
    // (예전엔 MoveTo 안에서 Battle을 열어 진행 중이던 이동이 잘렸다)
    public static BaseCardData TakeEventCard(bool forPlayer, CardType cardType)
    {
        ChooseRandomSpecialCard(cardType, out BaseCardData card);

        if(card == null)
        {
            Debug.LogWarning($"[CardManager] {cardType} 특수 카드가 없어서 이벤트 칸을 건너뛴다");
            return null;
        }

        bool isTrap = cardType == CardType.Bad || cardType == CardType.SuperBad;

        // 학습 중에는 연출을 전부 건너뛴다. GivePlayerCard가 카드 UI를 막는 것과 같은 이유로
        // 효과음과 함정 카드 연출도 여기서 막아야 한다
        if(!TrainingMode.Enabled)
            SoundManager.AudioShot(Vector3.zero, cardType.ToString(), 1f);

        if(!isTrap)
        {
            if(forPlayer) GivePlayerCard(card);
            else GiveEnemyCard(card);
            return null;
        }

        // 카드 연출은 플레이어 쪽만 띄운다. 상대가 밟은 함정은 카드 이름을 밝히지 않고
        // BattleManager가 "상대가 함정을 밟았다" 모트만 띄운다 —
        // 결과(내 말이 밀린다)는 보여주되 상대 카드 정보는 감추는 선이다
        if(forPlayer && !TrainingMode.Enabled && CardCanvas.instance != null)
            CardCanvas.instance.SpawnTrapCard(card);

        return card;
    }

    public static void GiveSpecialCardToPlayer(BaseCardData card)
    {
        if(card == null)
            return;

        GivePlayerCard(card);
    }

    public static void GiveSpecialCardToEnemy(BaseCardData card)
    {
        if(card == null)
            return;

        GiveEnemyCard(card);
    }

    // 손패는 턴이 끝나도 유지되므로 매 턴 maxCards장을 새로 뿌리면 안 된다.
    // 그러면 새 기본 카드가 이벤트 칸에서 얻은 특수 카드를 즉시 밀어내 버린다.
    // 빈 자리만큼만 채운다.
    public static int PlayerDrawCount => Mathf.Max(0, maxCards - PlayerCard.Count);
    public static int EnemyDrawCount => Mathf.Max(0, maxCards - EnemyCard.Count);

    public static void Shuffle()
    {
        try
        {
            int playerDraw = PlayerDrawCount;
            int enemyDraw = EnemyDrawCount;

            for(int i = 0; i < Mathf.Max(playerDraw, enemyDraw); i++)
            {
                if(i < enemyDraw) GiveRandomCardToEnemy();
                if(i < playerDraw) GiveRandomCardToPlayer();
            }
            Debug.Log($"셔플 완료");
        }
        catch (Exception e)
        {
            Debug.LogWarning("Shuffle Ended with Exception:" + e);
        }
    }

    public IEnumerator ShuffleAnimate(float duration)
    {
        int playerDraw = PlayerDrawCount;
        int enemyDraw = EnemyDrawCount;
        int steps = Mathf.Max(playerDraw, enemyDraw);

        if(steps <= 0)
        {
            Debug.Log($"손패가 이미 가득 참. 보충 없음");
            yield break;
        }

        for(int i = 0; i < steps; i++)
        {
            if(i < enemyDraw) GiveRandomCardToEnemy();
            if(i < playerDraw) GiveRandomCardToPlayer();

            // WaitForSeconds(0)도 한 프레임을 먹는다. 학습 중에는 duration이 0이라
            // 에피소드 첫 턴의 6장 보충이 그냥 6프레임으로 날아간다
            if(duration > 0f)
                yield return new WaitForSeconds(duration / steps);
        }
        Debug.Log($"셔플 완료 (플레이어 {playerDraw}장, 상대 {enemyDraw}장 보충)");
    }

    public static BaseCardData EnemyCardSelect()
    {
        // 보통은 매 턴 상한까지 채워지므로 비어있을 일이 없지만,
        // 비어있으면 Random.Range(0,0)이 0을 돌려주고 EnemyCard[0]에서 터진다.
        // 학습 중엔 예외 하나가 런 전체를 죽이므로 기본 카드로 대체한다
        if(EnemyCard.Count == 0)
        {
            Debug.LogWarning("상대 손패가 비어있음. 기본 카드로 대체함");
            ChooseRandomCard(out BaseCardData fallback);
            return fallback;
        }

        int rand = UnityEngine.Random.Range(0, EnemyCard.Count);
        var card = EnemyCard[rand];
        EnemyCard.RemoveAt(rand);
        return card;
    }
}
