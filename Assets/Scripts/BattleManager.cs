using UnityEngine;
using System;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    public static event Action PlayerWin;
    public static event Action EnemyWin;
    public static event Action Stalemate;

    public static BattleManager instance;

    // 말(player/enemy)은 월드 오브젝트라 오프셋도 월드 단위여야 한다.
    // 칸 간격이 1.5라 60은 화면 밖(카메라 ortho size 5)으로 날아간다
    private static readonly Vector3 horseMoteOffset = new Vector3(-0.6f, 0.5f, 0f);

    // 블록 감소는 GameManager.TurnEnd에서, 즉 승패 판정이 끝난 뒤에 돈다.
    // 그래서 카드에 적힌 턴수를 그대로 더하면 된다 — 낸 그 턴부터 정확히 N턴을 막는다.
    // (예전에는 감소가 판정보다 먼저 돌아서 +1 보정이 필요했다)
    private static int BlockTurns(int turns) => Mathf.Max(0, turns);

    // 이벤트 칸(함정 포함)을 처리하는 중인지. 이동이 멈췄어도 아직 턴을 넘기면 안 된다 —
    // 에이전트가 함정이 적용되기 전의 판을 보고 수를 두게 된다
    public bool IsResolvingEvents { get; private set; }

    void Awake()
    {
        instance = this;
    }

    // 에피소드 리셋용. 지난 판의 배틀/이벤트 코루틴이 다음 판에 끼어드는 걸 막는다.
    // 세대 검사만으로도 스스로 빠지지만, 그러기까지 한두 프레임 동안 큐를 건드릴 수 있다
    public void AbortEvents()
    {
        StopAllCoroutines();
        IsResolvingEvents = false;
    }

    public void Battle(BaseCardData playerData, BaseCardData enemyData)
    {
        Debug.Log("<color=red> Battle Started </color>");

        // 학습 중에는 배틀 연출(수 초)을 건너뛰고 바로 판정으로 간다.
        // 한쪽 카드가 없으면 연출할 대상 자체가 없으므로 역시 바로 간다
        if(TrainingMode.Enabled || playerData == null || enemyData == null)
        {
            StartCoroutine(ResolveBattleCor(playerData, enemyData));
            return;
        }

        StartCoroutine(
        CardCanvas.instance.BattleWithEnemy(
            enemyData,
            () =>
                {
                    Debug.Log("배틀 애니메이션 끝남!");
                    StartCoroutine(ResolveBattleCor(playerData, enemyData));
                }
            )
        );
    }

    // 확률 카드는 주사위 연출이 끝나야 성패를 알 수 있으므로 코루틴으로 순서를 잡는다.
    // 플레이어 카드 → 밟은 칸 → 상대 카드 → 밟은 칸 → 가위바위보 판정 → 승패 이동이 밟은 칸.
    //
    // 이동과 이벤트를 번갈아 처리하는 이유: 이동이 도는 동안 MoveHorses가 또 불리면
    // StopCoroutine으로 남은 칸이 잘린다. 한 번에 하나씩만 돌게 순서를 강제한다
    IEnumerator ResolveBattleCor(BaseCardData playerData, BaseCardData enemyData)
    {
        // 이 코루틴 도중에 완주가 날 수 있다(확률 카드 이동으로). 그러면 에이전트가
        // 에피소드를 리셋해 버리는데, 여기서 그냥 이어가면 다음 에피소드에 가짜 이동이 들어간다.
        // 시작 시점의 세대를 기억했다가 달라지면 빠진다
        int generation = MapManager.Generation;

        yield return ApplyCardCor(playerData, true, false);
        if(Stale(generation)) yield break;

        yield return ResolveEventsCor(generation);
        if(Stale(generation)) yield break;

        yield return ApplyCardCor(enemyData, false, false);
        if(Stale(generation)) yield break;

        yield return ResolveEventsCor(generation);
        if(Stale(generation)) yield break;

        JudgeRPS(playerData, enemyData);

        // 승패 이동(+2/-1)도 이벤트 칸을 밟을 수 있다.
        // ResolveEventsCor가 그 이동이 멈출 때까지 기다렸다가 처리한다
        yield return ResolveEventsCor(generation);
    }

    private static bool Stale(int generation)
        => MapManager.Generation != generation || MapManager.IsGameOver;

    // 카드 한 장의 효과를 적용한다. 주사위 → 성공이면 이동/블록 → 이동이 끝날 때까지 대기.
    //
    // forPlayer=false면 moveMe/moveOps와 블록이 전부 상대 기준으로 뒤집힌다.
    // 손패 카드와 함정 카드가 같은 경로를 타므로 두 규칙이 어긋날 수 없다
    IEnumerator ApplyCardCor(BaseCardData card, bool forPlayer, bool isTrap)
    {
        // 기본 카드(보/바위/가위)는 probability가 0이다. 이동 효과 없이 가위바위보만 한다
        if(card == null || card.probability <= 0f)
            yield break;

        bool success = false;
        yield return ThrowDiceCor(forPlayer, card.probability, (_, win) => success = win, isTrap);

        int percent = ToPercent(card.probability);
        GameObject horse = forPlayer ? MapManager.player : MapManager.enemy;
        Vector3 where = horse != null ? horse.transform.position + horseMoteOffset : Vector3.zero;

        MoteManager.instance.SpawnMote(OutcomeText(card, forPlayer, isTrap, success, percent),
                                       where, new Vector2(1, 1));

        if(success)
        {
            if(forPlayer)
            {
                MapManager.instance.MoveHorses(card.moveMe, card.moveOps);
                GameManager.playerBlockTime += BlockTurns(card.moveBlockMe);
                GameManager.enemyBlockTime += BlockTurns(card.moveBlockOps);
            }
            else
            {
                MapManager.instance.MoveHorses(card.moveOps, card.moveMe);
                GameManager.playerBlockTime += BlockTurns(card.moveBlockOps);
                GameManager.enemyBlockTime += BlockTurns(card.moveBlockMe);
            }

            // 라벨은 턴 경계에서만 갱신되므로, 여기서 바로 안 밀어주면
            // 배틀 연출이 도는 수 초 동안 옛 값이 그대로 보인다
            if(GameManager.gameManager != null)
                GameManager.gameManager.RefreshBlockDisplay();
        }

        // 이동이 끝나기 전에 다음 MoveHorses가 오면 StopCoroutine으로 남은 칸이 잘린다.
        // 상대 우쿨렐레(+3/-3)가 (+1/-1)로 적용되던 원인
        yield return WaitForHorses();
    }
    private static string OutcomeText(BaseCardData card, bool forPlayer, bool isTrap, bool success, int percent)
    {
        if(isTrap)
        {
            if(success)
                return forPlayer
                    ? $"<color=orange><size=150%>함정! {card.Title}</size></color>"
                    : $"<color=green><size=150%>상대가 함정을 밟았다!</size></color>";

            return forPlayer
                ? $"<color=green><size=150%>함정을 피했다!</size></color>"
                : $"<color=red><size=150%>상대가 함정을 피했다</size></color>";
        }

        if(success)
            return forPlayer
                ? $"<color=green><size=150%>{percent}% 확률로 성공!</size></color>"
                : $"<color=red><size=150%>{percent}% 확률로 성공 ㅋㅋㅋ</size></color>";

        return forPlayer
            ? $"<color=red><size=150%>{percent}% 확률로 실패 ㅋㅋㅋ</size></color>"
            : $"<color=green><size=150%>{percent}% 확률로 실패</size></color>";
    }

    // 이벤트 카드가 또 말을 움직여서 다음 이벤트 칸을 밟는 연쇄는 규칙상 인정한다.
    // 다만 서로를 밀어내는 칸 배치에서는 영원히 안 끝나므로 상한을 둔다
    private const int maxEventChain = 8;

    // 이동이 멈춘 뒤 밟은 칸(함정 포함)을 처리한다. 더 나올 게 없을 때까지 반복한다.
    //
    // 처리하는 동안 IsResolvingEvents로 턴 넘김을 막는다. 이동 여부만 보고 넘기면
    // 에이전트가 함정이 적용되기 전의 판을 보고 수를 두게 된다
    IEnumerator ResolveEventsCor(int generation)
    {
        IsResolvingEvents = true;

        // 코루틴이 중간에 끊겨도(StopCoroutine/에피소드 리셋) 플래그가 켜진 채 남으면
        // 판이 영원히 busy로 보인다. finally로 반드시 내린다
        try
        {
            for(int chain = 0; ; chain++)
            {
                // 이동이 도는 중에 꺼내면 아직 착지하지 않은 칸을 통째로 놓친다
                yield return WaitForHorses();

                if(Stale(generation) || !MapManager.HasPendingEvent)
                    yield break;

                if(chain >= maxEventChain)
                {
                    // 남겨두면 다음 ResolveEventsCor 호출이 이어받아 또 돈다. 여기서 버린다
                    MapManager.ClearPendingEvents();
                    Debug.LogWarning($"[BattleManager] 이벤트 연쇄가 {maxEventChain}번을 넘겨서 끊는다");
                    yield break;
                }

                // 플레이어 먼저, 상대 나중. ApplyCardCor가 각각 이동이 끝날 때까지 기다리므로
                // 두 이벤트가 MoveHorses를 두고 서로의 남은 칸을 자르지 않는다
                yield return ResolvePendingEventCor(true);
                if(Stale(generation)) yield break;

                yield return ResolvePendingEventCor(false);
                if(Stale(generation)) yield break;
            }
        }
        finally
        {
            IsResolvingEvents = false;
        }
    }

    IEnumerator ResolvePendingEventCor(bool forPlayer)
    {
        // 꺼내는 순간 지워진다. 아래 ApplyCardCor가 일으킨 이동의 착지 이벤트가
        // 다음 연쇄 바퀴에서 새로 잡히게 하려면 여기서 비워둬야 한다
        EventType landed = MapManager.TakePendingEvent(forPlayer);

        // normal(= 밟은 게 없음)과 enum에 없는 값이 여기서 같이 걸러진다
        if(!TryEventCardType(landed, out CardType cardType))
            yield break;

        yield return ResolveOneEventCor(cardType, forPlayer);
    }

    IEnumerator ResolveOneEventCor(CardType type, bool forPlayer)
    {
        BaseCardData trap = CardManager.TakeEventCard(forPlayer, type);

        if(trap == null)
            yield break;

        Debug.Log($"<color=orange>함정 발동: {trap.Title} ({(forPlayer ? "플레이어" : "상대")})</color>");

        yield return ApplyCardCor(trap, forPlayer, true);
    }

    private static bool TryEventCardType(EventType eventType, out CardType cardType)
    {
        switch(eventType)
        {
            case EventType.good:       cardType = CardType.Good;      return true;
            case EventType.bad:        cardType = CardType.Bad;       return true;
            case EventType.super_good: cardType = CardType.SuperGood; return true;
            case EventType.super_bad:  cardType = CardType.SuperBad;  return true;
            default:                   cardType = CardType.Good;      return false;
        }
    }

    // MoveHorses는 새 이동을 걸기 전에 진행 중이던 이동을 StopCoroutine으로 끊는다.
    // 그래서 다음 이동을 걸기 전에 현재 이동이 끝나길 기다려야 남은 칸이 보존된다.
    // 무한 대기는 학습을 통째로 멈추게 하므로 상한을 둔다
    IEnumerator WaitForHorses()
    {
        const int maxFrames = 600;
        int waited = 0;

        while(MapManager.instance != null && MapManager.instance.IsMoving && waited < maxFrames)
        {
            waited++;
            yield return null;
        }

        if(waited >= maxFrames)
            Debug.LogWarning($"[BattleManager] 말 이동이 {maxFrames}프레임 안에 안 끝남. 그대로 진행한다");
    }

    void JudgeRPS(BaseCardData playerData, BaseCardData enemyData)
    {
        if(playerData == null || enemyData == null)
        {
            Debug.Log("<color=grey>Battle Ended with Null Card</color>");
            return;
        }

        if(playerData.RPS == enemyData.RPS) //ROCK = 0, PAPER = 1, SCISSORS = 2
        {
            Debug.Log("<color=grey>Stalemate</color>");
            MoteManager.instance.SpawnMote("<color=grey><size=150%>스타일메이트</size></color>", CardCanvas.instance.battlePlace, new Vector2(1,1));
            Stalemate?.Invoke();
        }
        else if(playerData.RPS + 1 == enemyData.RPS)
        {
            MoteManager.instance.SpawnMote("<color=red><size=150%>슈퍼 듀퍼 패배</size></color>", CardCanvas.instance.battlePlace);
            Debug.Log("<color=red>Lose</color>");
            EnemyWin?.Invoke();
        }
        else if(playerData.RPS - 1 == enemyData.RPS)
        {
            MoteManager.instance.SpawnMote("<color=green><size=150%>슈퍼 듀퍼 승리</size></color>", CardCanvas.instance.battlePlace);
            Debug.Log("<color=green>Win</color>");
            PlayerWin?.Invoke();
        }
        else if(playerData.RPS + 1 > enemyData.RPS)
        {
            MoteManager.instance.SpawnMote("<color=red><size=150%>슈퍼 듀퍼 패배</size></color>", CardCanvas.instance.battlePlace);
            Debug.Log("<color=red>Lose</color>");
            EnemyWin?.Invoke();
        }
        else
        {
            MoteManager.instance.SpawnMote("<color=green><size=150%>슈퍼 듀퍼 승리</size></color>", CardCanvas.instance.battlePlace);
            Debug.Log("<color=green>Win</color>");
            PlayerWin?.Invoke();
        }
    }

    // 카드의 probability는 0~1인데 주사위 눈은 0~99라 비교 전에 퍼센트로 맞춰야 한다.
    private static int ToPercent(float probability) => Mathf.RoundToInt(Mathf.Clamp01(probability) * 100f);

    // 10번 굴리는 건 연출이고, 마지막에 나온 눈이 실제 판정이다.
    IEnumerator ThrowDiceCor(bool isPlayer, float probability, Action<bool,bool> callback, bool isTrap = false)
    {
        int percent = ToPercent(probability);

        // 10번 굴리는 건 순수 연출이고 판정은 마지막 눈 하나뿐이다.
        // 학습 중에는 한 번만 굴려 같은 확률분포로 즉시 끝낸다 (턴당 2.25초 절약)
        if(TrainingMode.Enabled)
        {
            callback?.Invoke(isPlayer, percent > UnityEngine.Random.Range(0, 100));
            yield break;
        }

        string color = isPlayer ? "green" : "red";
        string who = isPlayer ? "플레이어" : "상대";
        string what = isTrap ? "함정" : "도박";
        color = isTrap ? "orange" : color;

        HediffCanvas.instance.SpawnLabel($"<size=130%><color={color}>{who}의 {what} 수...</color></size>", 2f);
        var label = HediffCanvas.instance.SpawnLabel($"<size=130%><color={color}></color></size>");

        bool win = false;
        int dice = 0;
        for(int i = 0; i < 10; i++)
        {
            dice = UnityEngine.Random.Range(0, 100);
            win = percent > dice;
            HediffCanvas.instance.EditLabel(label, $"<size=130%><color={color}>{percent}%가 {dice}%보다 크면 성공!</color></size>");
            yield return new WaitForSeconds(i/20f);
        }

        HediffCanvas.instance.EditLabel(label, $"<size=130%><color={color}>{percent}% vs {dice}% → {(win ? "성공" : "실패!")}</color></size>");
        yield return new WaitForSeconds(0.6f);
        HediffCanvas.instance.RemoveLabel(label);

        callback?.Invoke(isPlayer, win);
    }
}
