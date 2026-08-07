using UnityEngine;

// 강화학습은 수십만 턴을 돌려야 하는데 이 게임은 연출 때문에 한 턴이 실시간 5초가 넘는다
// (주사위 2.25초 + 셔플 0.7초 + 배틀 연출 + 칸당 이동 0.2초).
// Enabled가 켜지면 그 대기를 전부 0으로 만든다. 게임 규칙 자체는 건드리지 않는다.
//
// UI(HediffCanvas / MoteManager / CardCanvas)는 그대로 둔다. 호출부가 35곳이라
// 전부 분기하면 깨지기 쉽고, 라벨 몇 개 만드는 비용은 대기 시간에 비하면 무시할 만하다.
// 그래도 느리면 그때 UI 없는 학습 전용 씬을 따로 파는 게 낫다.
public static class TrainingMode
{
    public static bool Enabled;

    // static 필드는 Play를 멈춰도 에디터 메모리에 그대로 남는다.
    // 그래서 에이전트를 꺼도 지난 학습의 Enabled=true가 살아남아
    // 사람이 플레이할 때 카드 UI와 연출이 통째로 사라진다.
    // 매 Play 시작마다 기본값으로 되돌린다 — 켜는 건 UkuleleAgent.Awake의 몫
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlay()
    {
        Enabled = false;
        PlayerAgent = null;
        EnemyAgent = null;
        EnemyPolicy = null;
    }

    // 각 진영을 조종하는 에이전트. UkuleleAgent.Awake()에서 자기 side에 맞게 등록한다.
    // null이면 그쪽은 사람(플레이어) 또는 무작위(상대)가 맡는다.
    //
    // 학습:        PlayerAgent만 존재, 상대는 무작위
    // 사람 vs AI:  EnemyAgent만 존재, 플레이어는 드래그 입력
    public static UkuleleAgent PlayerAgent;
    public static UkuleleAgent EnemyAgent;

    // 사람 vs AI에서 상대를 두는 쪽. EnemyAgent보다 우선한다.
    // 동기로 끝나고 카드별 선택 확률까지 볼 수 있어서 이쪽이 낫다
    public static EnemyPolicy EnemyPolicy;

    // 연출용 시간값을 학습 중에만 0으로 바꾼다
    public static float Duration(float normal) => Enabled ? 0f : normal;

    // 게임 코드는 턴마다 Debug.Log를 10줄 가까이 찍는다. 사람이 플레이할 땐 초당 0.2턴이라
    // 문제가 없지만, 연출을 걷어내면 초당 수십 턴이 돌아 초당 수백 줄이 된다.
    // 에디터의 Debug.Log는 호출마다 스택 트레이스를 캡처하고 Console에 영구 누적하므로
    // 이것만으로 에디터가 얼어붙는다. 학습 중에는 정보 로그를 끊는다.
    //
    // Warning/Error는 남긴다 — UkuleleAgent의 인스펙터 설정 검증과
    // BeginTurn의 이동 지연 경고를 못 보면 원인 파악이 불가능해진다.
    public static void ApplyLogSettings()
    {
        if(!Enabled)
            return;

        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

        // Log보다 우선순위가 높은 것(Warning/Error/Exception)만 통과시킨다
        Debug.unityLogger.filterLogType = LogType.Warning;

        Debug.LogWarning("[TrainingMode] 학습 모드. 정보 로그를 끕니다 (Warning/Error는 계속 표시).");
    }
}
