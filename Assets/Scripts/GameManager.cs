using System;
using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    //Singleton
    public static GameManager gameManager;
    public float remainTime;

    //Events
    public event Action startTurn;
    public event Action endTurn;

    //ref
    private int PlayerPosition;
    private int EnemyPosition;
    public static int playerBlockTime;
    public static int enemyBlockTime;
    GameObject playerBlockHediff;
    GameObject enemyBlockHediff;

    // 상대 수를 누가 두는지 첫 턴에 한 번만 알린다
    bool enemySourceReported;

    // 선택 완료를 연타하면 같은 턴이 여러 번 진행된다.
    // 배틀 코루틴과 TurnEnd → GameStart가 중첩되면서 손패, 말 이동, UI가 전부 깨진다.
    // 카드를 낸 순간 잠그고 턴이 끝날 때 푼다
    bool turnResolving;
    public bool IsTurnResolving => turnResolving;

    void Awake()
    {
        gameManager = this;
        Init();

        // static 이벤트라 씬을 다시 로드해도 구독이 남는다. 람다로 붙이면 해제할 수가 없어서
        // 재시작할 때마다 핸들러가 겹쳐 말이 두 배로 움직인다. 이름 있는 메서드로 붙이고 OnDestroy에서 뗀다
        BattleManager.PlayerWin += OnPlayerWin;
        BattleManager.EnemyWin += OnEnemyWin;
        BattleManager.Stalemate += TurnEnd;
        MapManager.GameOver += OnGameOver;
    }

    void OnDestroy()
    {
        BattleManager.PlayerWin -= OnPlayerWin;
        BattleManager.EnemyWin -= OnEnemyWin;
        BattleManager.Stalemate -= TurnEnd;
        MapManager.GameOver -= OnGameOver;
    }

    // 블록 판정은 반드시 TurnEnd()보다 먼저 해야 한다.
    //
    // TurnEnd()는 곧장 다음 턴을 열면서 블록을 한 칸 깎는다. 그게 판정보다 먼저 돌면
    // 화면에 "블록 1턴 남음"으로 보이던 값이 바로 그 순간 0이 되어 승리가 그대로 통과한다.
    // 플레이어는 분명 막힐 거라고 보고 있던 공격을 그대로 맞는다 —
    // 표시된 숫자와 실제 판정이 항상 1턴씩 어긋나 있었다
    void OnPlayerWin()
    {
        bool blocked = playerBlockTime > 0;

        if(blocked)
        {
            HediffCanvas.instance.SpawnLabel("<color=yellow>플레이어가 이겼으나 무시됨!</color>", 2f);
        }
        else
        {
            HediffCanvas.instance.SpawnLabel("<color=green>플레이어 승리!</color>" , 2f);
            MapManager.instance.PlayerWin();
        }

        TurnEnd();
    }

    void OnEnemyWin()
    {
        bool blocked = enemyBlockTime > 0;

        if(blocked)
        {
            HediffCanvas.instance.SpawnLabel("<color=green>상대가 이겼으나 무시됨!</color>", 2f);
        }
        else
        {
            HediffCanvas.instance.SpawnLabel("<color=red>상대 승리!</color>", 2f);
            MapManager.instance.EnemyWin();
        }

        TurnEnd();
    }

    // 완주 판정은 이동 코루틴 도중에 나오는데, 그 시점엔 TurnEnd()가 이미 다음 턴을 시작해 둔 뒤다.
    // 그래서 이미 뿌려진 손패를 여기서 걷어낸다
    void OnGameOver(bool playerWins)
    {
        CardManager.ClearPlayerCard();
        CardManager.ClearEnemyCard();

        HediffCanvas.instance.SpawnLabel(playerWins
            ? "<size=200%><color=green>완주! 플레이어 승리</color></size>"
            : "<size=200%><color=red>완주! 상대 승리</color></size>");
    }

    public void SetPosition(ref int Transform, int Position)
    {
        Transform = Position;
    }

    public void Init()
    {
        SetPosition(ref PlayerPosition, 0);
        SetPosition(ref EnemyPosition, 0);

        // static이라 씬 재로드 후에도 지난 판의 블록이 남는다
        playerBlockTime = 0;
        enemyBlockTime = 0;
    }

    void Start()
    {
        GameStart();
    }

    private void NotImplemented()
    {
        GameStart();
    }

    public void GameStart()
    {
        // 완주가 나온 뒤에는 턴을 더 돌리지 않는다
        if(MapManager.IsGameOver)
            return;

        // 블록 감소는 TurnEnd로 옮겼다. 여기서 깎으면 승패 판정보다 먼저 돌아
        // 화면에 남아 보이던 블록이 판정 직전에 사라진다
        blockHediffUpdate();
        StartCoroutine(BeginTurn());
    }

    // 말이 움직이는 중이거나 밟은 이벤트 칸(함정 포함)을 처리하는 중이면 아직 턴을 넘기면 안 된다.
    // 이동만 보고 넘기면 에이전트가 함정이 적용되기 전의 위치를 관측한다
    static bool IsBoardBusy =>
        (MapManager.instance != null && MapManager.instance.IsMoving) ||
        (BattleManager.instance != null && BattleManager.instance.IsResolvingEvents);

    IEnumerator BeginTurn()
    {
        yield return CardManager.instance.ShuffleAnimate(TrainingMode.Duration(0.7f));

        // 에이전트가 없으면 사람이 카드를 드래그해 낼 때까지 여기서 끝난다.
        // 연출 스킵 여부(TrainingMode.Enabled)와는 별개다 —
        // 학습된 모델이 연출을 켜고 노는 걸 볼 수도 있어야 한다
        if(TrainingMode.PlayerAgent == null)
            yield break;

        // 승패 처리는 말 이동을 먼저 걸고 TurnEnd()를 부르므로 보통은 여기 올 때
        // 이미 IsMoving이 켜져 있다. 다만 승패 이동이 밟은 이벤트 칸은 JudgeRPS 뒤의
        // ResolveEventsCor에서 처리되는데 그건 다음 프레임에 시작한다.
        // 한 프레임 넘겨야 그게 IsResolvingEvents를 세운 뒤에 아래 대기로 들어간다
        yield return null;

        // WaitUntil로 무한정 기다리면, 이동이 어떤 이유로든 안 끝날 때 학습이 조용히 멈추고
        // 트레이너는 원인을 알 수 없는 UnityTimeOutException만 뱉는다. 상한을 두고 알린다
        const int maxWaitFrames = 300;
        int waited = 0;
        while(IsBoardBusy && waited < maxWaitFrames)
        {
            waited++;
            yield return null;
        }

        if(IsBoardBusy)
            Debug.LogWarning($"[BeginTurn] 이동/이벤트가 {maxWaitFrames}프레임 안에 안 끝남. 그대로 결정을 요청한다");

        if(MapManager.IsGameOver)
            yield break;

        // 보상 정산과 결정 요청을 한 번에. 순서가 중요해서 에이전트 쪽에 묶어 뒀다
        TrainingMode.PlayerAgent.BeginTurnDecision();
    }

    // 에이전트가 에피소드를 다시 열 때 부른다. 씬 재로드 없이 제자리에서 초기화한다
    public void ResetGame()
    {
        StopAllCoroutines();

        // 배틀/이벤트 코루틴은 BattleManager가 들고 있어서 위 StopAllCoroutines로는 안 끊긴다.
        // 안 끊으면 지난 판의 함정이 다음 판 첫 턴에 발동한다
        if(BattleManager.instance != null)
            BattleManager.instance.AbortEvents();

        // 배틀 도중에 리셋되면 TurnEnd가 안 불려 잠금이 남는다
        turnResolving = false;

        CardManager.ClearPlayerCard();
        CardManager.ClearEnemyCard();
        Init();

        GameStart();
    }

    public void TurnEnd()
    {
        // 턴이 끝났으니 다음 카드를 낼 수 있게 푼다
        turnResolving = false;

        // 손패는 유지한다. 낸 카드는 이미 빠져 있다
        // (플레이어는 PlayerCardSelected에서, 상대는 CardManager.EnemyCardSelect에서).
        // 여기서 비워버리면 이벤트 칸에서 얻은 특수 카드가 한 번도 못 쓰이고 사라진다.

        // 한 턴이 끝났으니 블록을 한 칸 깎는다. 승패 판정(OnPlayerWin/OnEnemyWin)이
        // 이미 끝난 뒤라, 플레이어가 화면에서 본 그 값이 그대로 판정에 쓰인다.
        // 무승부로 이 함수에 바로 들어와도 시간은 똑같이 흘러야 하므로 여기서 깎는다
        playerBlockTime = Mathf.Max(0, playerBlockTime - 1);
        enemyBlockTime = Mathf.Max(0, enemyBlockTime - 1);

        blockHediffUpdate();
        NotImplemented();
    }

    // 블록은 배틀 도중(ApplyCardCor)에 걸리는데 blockHediffUpdate는 턴 경계에서만 돈다.
    // 그래서 카드를 낸 뒤 승패 판정까지 수 초 동안 라벨이 옛 값을 보여줬고,
    // 승패 이동으로 밟은 함정의 블록은 다음 턴이 될 때까지 아예 안 보였다
    public void RefreshBlockDisplay() => blockHediffUpdate();

    void blockHediffUpdate()
    {
        if(playerBlockTime > 0)
        {
            if(playerBlockHediff == null)
            {
                playerBlockHediff = HediffCanvas.instance.SpawnLabel($"<color=yellow>플레이어 블록 {playerBlockTime}턴</color>");
            }
            else
            {
                HediffCanvas.instance.EditLabel(playerBlockHediff, $"<color=yellow>플레이어 블록 {playerBlockTime}턴</color>");
            }
        }
        else
        {
            if(playerBlockHediff != null)
            {
                HediffCanvas.instance.RemoveLabel(playerBlockHediff);
                playerBlockHediff = null;
            }
        }

        if(enemyBlockTime > 0)
        {
            if(enemyBlockHediff == null)
            {
                enemyBlockHediff = HediffCanvas.instance.SpawnLabel($"<color=green>상대 블록 {enemyBlockTime}턴</color>");
            }
            else
            {
                HediffCanvas.instance.EditLabel(enemyBlockHediff, $"<color=green>상대 블록 {enemyBlockTime}턴</color>");
            }
        }
        else
        {
            if(enemyBlockHediff != null)
            {
                HediffCanvas.instance.RemoveLabel(enemyBlockHediff);
                enemyBlockHediff = null;
            }
        }
    }

    // UI 경로. 드래그로 고른 카드는 CardCanvas가 chosenCard로 들고 있고,
    // 그 GameObject는 배틀 연출이 끝나면서 파괴한다
    public void PlayerCardSelected(CardUI card)
    {
        if(card == null)
            return;

        PlayerCardSelected(card.Data);
    }

    // 에이전트 경로. UI 오브젝트 없이 데이터만으로 턴을 진행한다
    public void PlayerCardSelected(BaseCardData data)
    {
        // if(MapManager.isMoving)
        //  return;

        // 완주 라벨이 뜬 뒤 남아있던 카드로 한 턴 더 진행되는 걸 막는다
        if(data == null || MapManager.IsGameOver)
            return;

        // 연타 방지. 이게 없으면 배틀 코루틴이 겹쳐 돌면서
        // 상대 손패가 여러 장 빠지고 말이 여러 번 움직인다
        if(turnResolving)
        {
            Debug.Log("이미 이번 턴 카드를 냈음. 무시함");
            return;
        }
        turnResolving = true;

        Debug.Log($"플레이어가 카드 결정함:{data.Title}");
        HediffCanvas.instance.SpawnLabel($"플레이어가 {data.Title} 카드 결정함", 2f);

        // 에이전트가 플레이어 쪽을 잡고 연출까지 켜는 경우(AI 자동 플레이 관전).
        // 드래그가 없어 chosenCard가 비어 있는데, 배틀 연출은 그 카드를 움직여서 보여주므로
        // 그대로 두면 BattleWithEnemy가 카드를 못 찾고 빠진다. 여기서 대신 올려준다.
        //
        // 학습 모드는 연출을 건너뛰니 이 경로가 필요 없고, 사람이 드래그로 냈으면
        // chosenCard가 이미 차 있어 여기 안 걸린다
        if(!TrainingMode.Enabled && CardCanvas.instance != null && CardCanvas.instance.chosenCard == null)
            CardCanvas.instance.ChooseCardByData(data);

        // 낸 카드는 손패에서 뺀다. UI는 배틀 연출이 알아서 파괴한다.
        // 위에서 chosenCard를 채웠으면 UI 카드는 이미 손패에서 빠졌으므로 여기서 또 안 지운다
        CardManager.DiscardPlayedPlayerCard(data);

        StartCoroutine(ResolveEnemyThenBattle(data));
    }

    // 상대 수를 정한 뒤 배틀을 연다.
    // 에이전트가 맡으면 ML-Agents 결정이 비동기라 한 번 기다려야 한다
    IEnumerator ResolveEnemyThenBattle(BaseCardData playerData)
    {
        BaseCardData enemy = null;

        // 왜 확률 로그가 안 나오는지 구분할 수 있게 한 번만 알린다.
        // Warning이라 학습 모드 로그 필터에도 안 걸린다
        if(!enemySourceReported)
        {
            enemySourceReported = true;
            string who = TrainingMode.EnemyPolicy != null ? "EnemyPolicy(모델 추론)"
                       : TrainingMode.EnemyAgent != null ? "EnemyAgent(ML-Agents)"
                       : "무작위";
            Debug.LogWarning($"[GameManager] 상대 수를 두는 주체: {who}");
        }

        // 직접 추론이 우선. 동기로 끝나고 카드별 확률까지 찍어준다
        if(TrainingMode.EnemyPolicy != null)
        {
            enemy = TrainingMode.EnemyPolicy.ChooseCard();

            if(enemy == null)
                Debug.LogWarning("[GameManager] EnemyPolicy가 카드를 못 골랐다. 무작위로 대체한다");
        }

        if(enemy != null)
        {
            // 이미 정해짐
        }
        else if(TrainingMode.EnemyAgent != null)
        {
            TrainingMode.EnemyAgent.RequestEnemyDecision();

            // 모델이 안 붙어 있거나 Behavior Type이 잘못되면 응답이 영영 안 온다.
            // 무한 대기 대신 상한을 두고 무작위로 넘긴다
            const int maxWaitFrames = 120;
            int waited = 0;
            while(!TrainingMode.EnemyAgent.EnemyDecisionReady && waited < maxWaitFrames)
            {
                waited++;
                yield return null;
            }

            enemy = TrainingMode.EnemyAgent.ChosenEnemyCard;

            if(enemy == null)
            {
                Debug.LogWarning("[GameManager] 상대 에이전트가 응답하지 않음. 무작위로 대체한다 " +
                                 "(Model이 비었거나 Behavior Type이 Inference Only가 아닐 수 있음)");
                enemy = CardManager.EnemyCardSelect();
            }
        }
        else
        {
            enemy = CardManager.EnemyCardSelect();
        }

        // 화면에는 띄우지 않는다. 배틀 연출이 상대 카드를 뒤집어 보여주는데
        // 그 전에 이름을 알려주면 연출이 통째로 김빠지고, 상대 정보를 먼저 흘리는 셈이 된다
        Debug.Log($"상대가 {enemy.Title} 카드 결정함");
        BattleManager.instance.Battle(playerData, enemy);
    }

}
