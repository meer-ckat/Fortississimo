using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

// 플레이어 쪽을 조종하는 에이전트. 상대는 CardManager.EnemyCardSelect()의 무작위 선택 그대로다.
//
// 이 게임은 턴제라 매 프레임 결정을 내리지 않는다. DecisionRequester를 붙이면 안 되고,
// 턴이 준비된 시점(GameManager.BeginTurn)에서 RequestDecision()을 직접 불러야 한다.
public enum AgentSide
{
    Player,     // 학습용. 사람이 드래그로 내던 자리를 대신한다
    Enemy       // 사람과 대전할 때. 관측을 뒤집어 같은 모델을 상대 말에 쓴다
}

public class UkuleleAgent : Agent
{
    [Header("Side")]
    [Tooltip("이 에이전트가 조종할 진영. 게임 규칙이 대칭이라 관측만 뒤집으면 같은 모델을 양쪽에 쓸 수 있다")]
    [SerializeField] private AgentSide side = AgentSide.Player;

    public bool IsPlayerSide => side == AgentSide.Player;

    // Enemy 진영일 때 GameManager가 결과를 받아가는 통로
    private BaseCardData chosenEnemyCard;
    private bool enemyDecisionPending;

    public bool EnemyDecisionReady => !enemyDecisionPending;
    public BaseCardData ChosenEnemyCard => chosenEnemyCard;

    // 손패 상한은 CardManager가 단일 출처다. 여기서 복제하면 조용히 어긋난다
    public const int HandSlots = CardManager.MaxHandSize;

    // 관측 벡터를 실제로 쓰는 건 UkuleleObservation이므로 크기도 거기서 그대로 가져온다.
    // 여기서 같은 식을 다시 계산하면 아래 Initialize 검사가 "코드끼리 어긋난 값"을
    // 통과시켜 버린다 — 인스펙터만 보고 관측이 밀린 걸 못 잡는다.
    // BehaviorParameters의 Vector Observation Space Size에 이 값을 넣어야 한다
    public const int ObservationSize = UkuleleObservation.Size;

    [Header("Reward")]
    [Tooltip("상대와의 전진량 차이가 1칸 벌어질 때마다 주는 보상. 희소 보상만으로는 학습이 느리다")]
    [SerializeField] private float progressReward = 0.05f;
    [Tooltip("완주 승패에 주는 보상")]
    [SerializeField] private float winReward = 1f;
    [Tooltip("매 턴 부과하는 감점. 빨리 끝내도록 유도한다")]
    [SerializeField] private float stepPenalty = -0.002f;

    [Header("Episode")]
    [Tooltip("이 턴 수를 넘기면 무승부로 에피소드를 끊는다. Agent.MaxStep은 Academy 스텝 기준이라 턴제에 안 맞으므로 직접 센다")]
    [SerializeField] private int maxTurns = 300;

    [Header("Watchdog")]
    [Tooltip("이 시간(실시간) 동안 결정 요청이 한 번도 없으면 턴 루프가 죽은 것으로 보고 에피소드를 강제로 다시 연다. 파이썬 쪽 타임아웃(60초)보다 짧아야 한다")]
    [SerializeField] private float decisionTimeoutSeconds = 20f;

    // 관측과 같은 정렬 순서로 손패를 담아두는 버퍼
    private readonly List<BaseCardData> ordered = new List<BaseCardData>();

    private int lastDelta;
    private int turnCount;
    private bool episodeEnding;
    private float lastDecisionTime;

    [Header("Speed")]
    [Tooltip("연출 대기를 전부 제거한다. 학습할 땐 반드시 켜고, 학습된 모델이 노는 걸 눈으로 볼 땐 끈다")]
    [SerializeField] private bool skipAnimations = true;

    // Agent.Awake()도 초기화를 하므로 반드시 base를 부른다
    protected override void Awake()
    {
        base.Awake();

        if(IsPlayerSide) TrainingMode.PlayerAgent = this;
        else TrainingMode.EnemyAgent = this;

        // 연출 스킵은 학습(Player 진영)일 때만 의미가 있다.
        // 사람과 대전하는 Enemy 에이전트가 이걸 켜면 화면이 텅 비어버린다
        if(IsPlayerSide)
        {
            TrainingMode.Enabled = skipAnimations;
            TrainingMode.ApplyLogSettings();
        }

        // 에디터는 포커스를 잃으면 프레임을 안 돌린다. cmd 창이나 브라우저를 클릭하는 순간
        // 결정 요청이 끊기고 파이썬이 60초 뒤 UnityTimeOutException을 낸다.
        // Project Settings의 체크박스에 의존하지 않고 코드에서 강제한다
        Application.runInBackground = true;
    }

    // 인스펙터 값이 코드와 어긋나면 학습이 조용히 망가진다(관측이 밀리거나 잘린다).
    // 여기서 잡아 바로 알려준다
    public override void Initialize()
    {
        var behavior = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        if(behavior == null)
            return;

        int declaredObs = behavior.BrainParameters.VectorObservationSize;
        if(declaredObs != ObservationSize)
        {
            Debug.LogError($"[UkuleleAgent] Vector Observation Space Size가 {declaredObs}로 되어 있음. " +
                           $"{ObservationSize}이어야 한다 (손패 {HandSlots}칸 기준).");
        }

        var spec = behavior.BrainParameters.ActionSpec;
        var branches = spec.BranchSizes;
        string actual = branches == null ? "없음" : string.Join(",", branches);

        // 설정이 맞든 틀리든 항상 남긴다. 학습이 한참 돌다 죽었을 때
        // 그때 스펙이 뭐였는지 모르면 원인을 좁힐 수가 없다.
        // 정보 로그는 학습 중 꺼지므로 Warning으로 띄운다
        Debug.LogWarning($"[UkuleleAgent/{side}] ActionSpec: 관측={declaredObs}, " +
                         $"연속={spec.NumContinuousActions}, 이산=[{actual}] " +
                         $"| skipAnimations={skipAnimations}");

        // 크기 0짜리 브랜치는 파이썬 쪽에서 텐서 폭이 0이 되어
        // torch.multinomial이 "cannot sample n_sample > prob_dist.size(-1)"로 죽는다.
        // 학습이 수백 스텝 돌다 갑자기 터지므로 원인을 찾기가 매우 어렵다
        if(branches != null)
        {
            for(int i = 0; i < branches.Length; i++)
            {
                if(branches[i] == 0)
                {
                    Debug.LogError($"[UkuleleAgent] Branch {i}의 Size가 0임. " +
                                   $"크기 0인 브랜치는 학습 중 파이썬에서 크래시를 일으킨다. " +
                                   $"Branches를 1로, Branch 0 Size를 {HandSlots}로 맞출 것.");
                }
            }
        }

        if(branches == null || branches.Length != 1 || branches[0] != HandSlots)
        {
            Debug.LogError($"[UkuleleAgent] Discrete Branch 설정이 [{actual}]임. " +
                           $"Branches=1, Branch 0 Size={HandSlots}이어야 한다.");
        }

        if(spec.NumContinuousActions != 0)
        {
            Debug.LogError($"[UkuleleAgent] Continuous Actions가 {spec.NumContinuousActions}임. 0이어야 한다.");
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        MapManager.GameOver += OnGameOver;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        MapManager.GameOver -= OnGameOver;

        // static이라 씬을 나가도 남는다. 안 지우면 다음 씬에서 파괴된 에이전트를 붙잡고 있게 된다
        if(TrainingMode.PlayerAgent == this) TrainingMode.PlayerAgent = null;
        if(TrainingMode.EnemyAgent == this) TrainingMode.EnemyAgent = null;
    }

    public override void OnEpisodeBegin()
    {
        lastDelta = 0;
        turnCount = 0;
        episodeEnding = false;
        enemyDecisionPending = false;
        chosenEnemyCard = null;
        lastDecisionTime = Time.unscaledTime;   // 리셋 직후 워치독이 바로 다시 물지 않게

        // 게임 리셋은 Player 진영 에이전트만 책임진다.
        // 사람 vs AI에서는 Enemy 에이전트만 존재하는데, 그쪽이 판을 리셋해 버리면
        // 사람이 플레이하던 게임이 멋대로 처음으로 돌아간다
        if(!IsPlayerSide)
            return;

        // Academy의 첫 리셋은 모든 Awake/Start 뒤에 오므로 보통은 준비돼 있지만,
        // 씬 구성이 어긋나면 여기서 NullReference로 죽는 대신 원인을 알려주는 편이 낫다
        if(MapManager.instance == null || GameManager.gameManager == null)
        {
            Debug.LogError("[UkuleleAgent] MapManager/GameManager가 씬에 없음. 에피소드를 시작할 수 없다");
            return;
        }

        // 말 위치, 전진량, IsGameOver를 되돌리고 손패/블록을 비운 뒤 첫 턴을 연다
        MapManager.instance.ResetEpisode();
        GameManager.gameManager.ResetGame();
    }

    // 실제 내용은 UkuleleObservation에 있다. 직접 추론하는 EnemyPolicy와
    // 한 칸이라도 어긋나면 모델이 엉뚱한 입력을 보게 되므로 출처를 하나로 묶었다
    public override void CollectObservations(VectorSensor sensor)
    {
        UkuleleObservation.Write(IsPlayerSide, sensor.AddObservation);
    }

    private List<BaseCardData> OwnHand => UkuleleObservation.Hand(IsPlayerSide);

    // 손패가 3장 미만이면 빈 슬롯을 고르지 못하게 막는다
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        int count = OwnHand != null ? OwnHand.Count : 0;

        // 전부 막으면 ML-Agents가 예외를 던진다. 빈 손패면 마스킹을 포기한다
        if(count <= 0 || count >= HandSlots)
            return;

        for(int i = count; i < HandSlots; i++)
            actionMask.SetActionEnabled(0, i, false);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if(MapManager.IsGameOver)
            return;

        // 관측이 정렬된 순서로 들어가므로 행동 인덱스도 그 순서를 가리킨다.
        // 실제 손패 리스트 순서로 꺼내면 엉뚱한 카드가 나간다
        UkuleleObservation.GetOrderedHand(IsPlayerSide, ordered);

        // Enemy 진영: 카드를 고르기만 하고 배틀은 GameManager가 연다.
        // 사람의 카드와 짝이 맞아야 하므로 여기서 직접 진행하면 안 된다
        if(!IsPlayerSide)
        {
            if(ordered.Count == 0)
            {
                Debug.LogWarning("[UkuleleAgent/Enemy] 손패가 비어있음. 무작위 기본 카드로 대체한다");
                chosenEnemyCard = CardManager.EnemyCardSelect();
            }
            else
            {
                int pick = Mathf.Clamp(actions.DiscreteActions[0], 0, ordered.Count - 1);
                chosenEnemyCard = ordered[pick];
                CardManager.EnemyCard.Remove(chosenEnemyCard);
            }

            enemyDecisionPending = false;
            return;
        }

        List<BaseCardData> hand = ordered;

        if(episodeEnding)
            return;

        // 그냥 return하면 배틀이 안 열려 TurnEnd도 안 오고, 다음 RequestDecision이 영영 없다.
        // 워치독이 20초 뒤 잡아주지만 그때까지 학습이 멈추므로 여기서 바로 끊는다
        if(hand == null || hand.Count == 0)
        {
            Debug.LogError("[UkuleleAgent] 손패가 비어 결정을 내릴 수 없음. 에피소드를 다시 연다");
            EndEpisode();
            return;
        }

        turnCount++;
        if(turnCount > maxTurns)
        {
            // 무승부. 보상 없이 끊는다
            EndEpisode();
            return;
        }

        int index = Mathf.Clamp(actions.DiscreteActions[0], 0, hand.Count - 1);
        GameManager.gameManager.PlayerCardSelected(hand[index]);
    }

    // GameManager가 사람의 카드를 받은 뒤 상대 수를 물어볼 때 부른다.
    // ML-Agents의 결정은 비동기라 OnActionReceived가 다음 Academy 스텝에 온다
    public void RequestEnemyDecision()
    {
        chosenEnemyCard = null;
        enemyDecisionPending = true;
        RequestDecision();
    }

    // 사람이 직접 돌려볼 때(Behavior Type = Heuristic Only) 첫 카드를 낸다
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discrete = actionsOut.DiscreteActions;
        discrete[0] = 0;
    }

    // GameManager.BeginTurn이 턴 준비가 끝나면 부른다.
    // ML-Agents는 RequestDecision 시점에 쌓인 보상을 "직전 행동의 결과"로 보내므로
    // 보상 정산이 반드시 RequestDecision보다 먼저 와야 한다
    public void BeginTurnDecision()
    {
        int delta = MapManager.playerProgress - MapManager.enemyProgress;
        AddReward((delta - lastDelta) * progressReward);
        lastDelta = delta;
        AddReward(stepPenalty);

        lastDecisionTime = Time.unscaledTime;
        RequestDecision();
    }

    // 턴 루프가 죽으면(코루틴이 어딘가에서 빠져나가 RequestDecision까지 못 감)
    // 증상은 60초 뒤 파이썬의 UnityTimeOutException뿐이라 원인 추적이 불가능하다.
    // 여기서 먼저 잡아 로그를 남기고 에피소드를 다시 열어 학습을 이어간다.
    //
    // timeScale이 20배여도 unscaledTime은 실시간이라 기준이 흔들리지 않는다
    private void Update()
    {
        // 워치독은 턴 루프를 소유한 Player 진영(=학습)에서만 의미가 있다.
        // Enemy 진영은 사람이 카드를 낼 때까지 얼마든지 기다려도 정상이다
        if(!IsPlayerSide || !TrainingMode.Enabled || episodeEnding || lastDecisionTime <= 0f)
            return;

        if(Time.unscaledTime - lastDecisionTime < decisionTimeoutSeconds)
            return;

        Debug.LogError($"[UkuleleAgent] {decisionTimeoutSeconds}초 동안 턴이 진행되지 않음 " +
                       $"(IsMoving={MapManager.instance?.IsMoving}, IsGameOver={MapManager.IsGameOver}, " +
                       $"이벤트처리중={BattleManager.instance?.IsResolvingEvents}, " +
                       $"손패={CardManager.PlayerCard?.Count}). 에피소드를 강제 재시작한다.");

        lastDecisionTime = Time.unscaledTime;
        EndEpisode();
    }

    private void OnGameOver(bool playerWins)
    {
        // Enemy 진영은 에피소드를 소유하지 않는다. 사람과 대전 중에 여기서 리셋하면
        // 결과 화면을 보기도 전에 판이 다시 시작된다
        if(!IsPlayerSide || episodeEnding)
            return;

        episodeEnding = true;
        AddReward(playerWins ? winReward : -winReward);

        // GameOver는 MapManager.MoveTo 코루틴 도중에 발행된다.
        // 여기서 바로 EndEpisode()를 부르면 OnEpisodeBegin이 그 코루틴을 실행 중에 정리하게 된다.
        // 한 프레임 미뤄서 이동 코루틴이 현재 프레임을 마치게 둔다
        StartCoroutine(EndEpisodeNextFrame());
    }

    private IEnumerator EndEpisodeNextFrame()
    {
        yield return null;
        EndEpisode();
    }
}
