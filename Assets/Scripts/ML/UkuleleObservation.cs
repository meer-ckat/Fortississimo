using System;
using System.Collections.Generic;
using UnityEngine;

// 관측 벡터를 만드는 유일한 자리.
//
// 학습은 VectorSensor로, 직접 추론(EnemyPolicy)은 float[]로 받아가는데
// 이 둘이 한 칸이라도 어긋나면 모델은 조용히 엉뚱한 입력을 보고 이상한 수를 둔다.
// 그래서 출력 방법만 델리게이트로 받고 내용은 여기 한 곳에서만 적는다.
public static class UkuleleObservation
{
    public const int HandSlots = CardManager.MaxHandSize;
    public const int FeaturesPerCard = 13;   // RPS one-hot 3 + 이동/블록 4 + 확률 1 + cardType one-hot 5
    // 이벤트는 "지나간 칸"이 아니라 "착지한 칸"만 발동한다. 그래서 관측해야 할 건
    // 경로가 아니라 착지 가능 지점이고, 뒤로 밀리는 착지도 똑같이 중요하다 —
    // moveMe -4, moveOps -4, 가위바위보 패배 -1이 한 턴에 겹칠 수 있다.
    // 앞은 초공간 우회도로(+6)까지 닿아야 한다
    public const int LookaheadBack = 6;
    public const int LookaheadForward = 6;

    // 현재 칸(offset 0)은 이미 발동하고 소비된 뒤라 앞으로의 가치가 없어서 뺀다
    public const int LookaheadCells = LookaheadBack + LookaheadForward;

    // 내 주변과 상대 주변을 둘 다 넣는다. 카드에 moveOps(-4~+2)가 있어서
    // "상대를 나쁜 칸에 밀어넣기"가 핵심 전략인데, 상대 쪽 칸이 안 보이면
    // 그 수를 둘 근거가 관측에 아예 없어서 학습이 불가능하다
    public const int LookaheadSides = 2;

    // 4 + 6*13 + 2*12*5 = 202
    public const int Size = 4 + HandSlots * FeaturesPerCard + LookaheadSides * LookaheadCells * 5;

    // 정규화 상수. 카드 데이터의 실제 최댓값에 맞춰야 한다 —
    // 작게 잡으면 Clamp에서 포화되어 서로 다른 카드가 신경망에게 똑같이 보인다.
    // Special.json 기준: moveMe -4~6, moveOps -4~4, moveBlockMe 0~5, moveBlockOps 0~4.
    // 카드를 추가하다 이 범위를 넘기면 여기도 같이 올려야 한다
    private const float MoveMeScale = 6f;       // 4였을 때 히치하이킹(4) / 긴급탈출(5) / 초공간 우회도로(6)가 전부 1.0
    private const float MoveOpsScale = 4f;
    private const float BlockMeScale = 5f;
    private const float BlockOpsScale = 4f;

    // 블록 카운터는 카드에 적힌 턴수가 그대로 들어간다. 최댓값은 과민성대장증후군의 5턴.
    // 작게 잡으면(3 등) 3턴 남은 상태와 5턴 남은 상태가 똑같이 1.0이라
    // 함정을 얼마나 맞았는지 구분하지 못한다. 여러 장이 겹치면 5를 넘지만 Clamp01로 포화된다
    private const float BlockTimeScale = 5f;

    // 손패를 들어온 순서 그대로 넣으면 같은 카드가 슬롯 위치에 따라 다른 값을 받는다.
    // 신경망에게 "0번 슬롯의 바위"와 "3번 슬롯의 바위"는 완전히 다른 입력이라,
    // 같은 카드의 가치를 슬롯마다 따로 학습해야 한다. 그만큼 학습이 느려지고,
    // 실제로 확률 로그에 "보 0.6% ... 보 9.6%" 같은 모순이 찍혔다.
    //
    // 항상 같은 기준으로 정렬해 넣으면 같은 상태가 항상 같은 관측이 된다.
    // 행동 인덱스도 이 정렬 순서를 가리키므로 GetOrderedHand를 거쳐 카드를 꺼내야 한다.
    private static readonly Comparison<BaseCardData> Canonical = (a, b) =>
    {
        int c = a.RPS.CompareTo(b.RPS);
        if(c != 0) return c;

        c = b.moveMe.CompareTo(a.moveMe);            // 많이 전진하는 카드가 앞
        if(c != 0) return c;

        c = a.moveOps.CompareTo(b.moveOps);          // 상대를 많이 미는 카드가 앞
        if(c != 0) return c;

        c = b.moveBlockOps.CompareTo(a.moveBlockOps);
        if(c != 0) return c;

        c = a.moveBlockMe.CompareTo(b.moveBlockMe);
        if(c != 0) return c;

        return string.CompareOrdinal(a.Title, b.Title);
    };

    private static readonly List<BaseCardData> scratch = new List<BaseCardData>();

    // 관측과 행동이 공유하는 손패 순서. 호출부는 이 순서로 카드를 고른다
    public static void GetOrderedHand(bool mine, List<BaseCardData> buffer)
    {
        buffer.Clear();

        List<BaseCardData> hand = Hand(mine);
        if(hand == null)
            return;

        buffer.AddRange(hand);
        buffer.Sort(Canonical);
    }

    // mine=false면 모든 값이 상대 시점으로 뒤집힌다.
    // 게임 규칙이 대칭이라 플레이어 시점으로 학습된 정책을 그대로 상대 말에 쓸 수 있다
    public static void Write(bool mine, Action<float> add)
    {
        int cells = Mathf.Max(1, MapManager.CellCount);
        int target = Mathf.Max(1, MapManager.TargetProgress);

        int ownProgress = mine ? MapManager.playerProgress : MapManager.enemyProgress;
        int oppProgress = mine ? MapManager.enemyProgress : MapManager.playerProgress;
        int ownBlock = mine ? GameManager.playerBlockTime : GameManager.enemyBlockTime;
        int oppBlock = mine ? GameManager.enemyBlockTime : GameManager.playerBlockTime;

        // 절대 위치보다 "상대보다 얼마나 앞서 있나"가 핵심 신호다
        add(Mathf.Clamp((float)(ownProgress - oppProgress) / cells, -2f, 2f));
        add(Mathf.Clamp01((float)ownProgress / target));
        add(Mathf.Clamp01(ownBlock / BlockTimeScale));
        add(Mathf.Clamp01(oppBlock / BlockTimeScale));

        // 정렬된 순서로 넣는다. 행동 인덱스도 같은 순서를 가리킨다
        GetOrderedHand(mine, scratch);

        for(int i = 0; i < HandSlots; i++)
        {
            if(i >= scratch.Count)
            {
                // 빈 슬롯은 0으로 채운다. 관측 크기는 항상 고정이어야 한다
                for(int f = 0; f < FeaturesPerCard; f++)
                    add(0f);
                continue;
            }

            BaseCardData c = scratch[i];
            OneHot(add, Mathf.Clamp(c.RPS, 0, 2), 3);
            add(Mathf.Clamp(c.moveMe / MoveMeScale, -1f, 1f));
            add(Mathf.Clamp(c.moveOps / MoveOpsScale, -1f, 1f));
            add(Mathf.Clamp01(c.moveBlockMe / BlockMeScale));
            add(Mathf.Clamp01(c.moveBlockOps / BlockOpsScale));
            add(Mathf.Clamp01(c.probability));
            OneHot(add, (int)c.cardType, 5);
        }

        // 착지할 수 있는 칸의 이벤트 타입. 나쁜 칸을 피하려면 이게 보여야 한다.
        // 내 쪽 먼저, 상대 쪽 나중. 순서 자체는 뭐든 상관없지만 학습과 추론이 같아야 하고,
        // 이 함수가 그 유일한 정의라 여기만 고치면 양쪽이 같이 따라온다
        WriteLookahead(add, mine);
        WriteLookahead(add, !mine);
    }

    // forPlayer 쪽 말 기준 -LookaheadBack ~ +LookaheadForward 칸의 이벤트 타입.
    // mine을 뒤집어 부르면 그대로 상대 쪽 시야가 된다
    private static void WriteLookahead(Action<float> add, bool forPlayer)
    {
        for(int i = -LookaheadBack; i <= LookaheadForward; i++)
        {
            if(i == 0)
                continue;

            OneHot(add, (int)MapManager.EventAtOffset(i, forPlayer), 5);
        }
    }

    public static List<BaseCardData> Hand(bool mine)
        => mine ? CardManager.PlayerCard : CardManager.EnemyCard;

    private static void OneHot(Action<float> add, int value, int range)
    {
        for(int i = 0; i < range; i++)
            add(i == value ? 1f : 0f);
    }
}
