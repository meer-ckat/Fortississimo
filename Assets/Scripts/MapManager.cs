using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public enum EventType
{
    normal,
    good,
    bad,
    super_good,
    super_bad,
}

[Serializable]
public struct Cell
{
    public Vector2 pos;
    public EventType eventType;
    public Color color;
}

public class MapManager : MonoBehaviour
{
    public static MapManager instance;
    [Header("Map")]
    [Tooltip("Resources/Data/Map 안의 파일명(확장자 제외)")]
    [SerializeField] private string mapName = "Stage1";
    private List<Cell> MapData = new List<Cell>();
    [SerializeField] private GameObject PlayerPre;
    [SerializeField] private GameObject EnemyPre;
    [SerializeField] private GameObject CellPre;

    [Header("Move")]
    [SerializeField] private float stepDuration = 0.2f;

    [Header("Goal")]
    [Tooltip("완주로 인정할 바퀴 수. 목표 전진량 = 칸 수 * 이 값")]
    [SerializeField] private int targetLaps = 1;

    public static GameObject player;
    public static GameObject enemy;
    private static int playerPos;
    private static int enemyPos;

    // playerPos는 WrapPos로 감기므로 몇 바퀴 돌았는지 알 수 없다.
    // 완주 판정은 감기지 않는 누적 전진량으로 따로 센다(후퇴하면 같이 깎인다).
    public static int playerProgress;
    public static int enemyProgress;
    public static int TargetProgress { get; private set; }
    public static int CellCount => instance != null ? instance.MapData.Count : 0;

    // true면 플레이어 승. 게임당 한 번만 발행된다
    public static event Action<bool> GameOver;
    public static bool IsGameOver { get; private set; }

    // 에피소드가 리셋될 때마다 올라간다. 리셋 시점에 아직 살아있던 코루틴이
    // 다음 에피소드에 끼어들어 가짜 이동을 넣는 걸 막는 용도
    public static int Generation { get; private set; }

    private Coroutine playerMove;
    private Coroutine enemyMove;

    public bool IsMoving => playerMove != null || enemyMove != null;

    // 스테이지 선택 화면에서 씬 로드 전에 지정한다. 비어있으면 인스펙터의 mapName을 쓴다
    public static string SelectedMap;

    // 이동이 끝난 칸에 걸려 있던 이벤트. normal이 "처리할 게 없다"는 뜻이라
    // 별도의 플래그 없이 이 값 하나로 대기 여부를 표현한다.
    // MoveTo가 기록하고 BattleManager가 TakePendingEvent로 꺼내 간다
    private static EventType playerPendingEvent;
    private static EventType enemyPendingEvent;

    void Awake()
    {
        instance = this;
        LoadMap(string.IsNullOrEmpty(SelectedMap)? mapName : SelectedMap);

        // GameManager.Start()가 IsGameOver를 보고 첫 턴을 돌릴지 정한다.
        // Start끼리는 실행 순서가 보장되지 않으므로 초기화는 반드시 Awake에서 끝내야 한다
        ResetProgress();
    }

    void ResetProgress()
    {
        playerPos = 0; enemyPos = 0;
        playerProgress = 0; enemyProgress = 0;
        IsGameOver = false;

        // static이라 에피소드를 넘어 살아남는다. 안 지우면 지난 판에서 밟은 칸이
        // 다음 판 첫 턴에 발동한다
        ClearPendingEvents();
        TargetProgress = MapData.Count * Mathf.Max(1, targetLaps);
    }

    // 학습 에피소드를 다시 시작한다. 씬을 재로드하면 너무 느리므로 제자리에서 되돌린다
    public void ResetEpisode()
    {
        // 진행 중인 MoveTo 코루틴을 끊는다. MapManager는 이동 말고 다른 코루틴을 쓰지 않는다
        StopAllCoroutines();
        playerMove = null;
        enemyMove = null;

        // BattleManager 쪽 코루틴은 여기서 못 끊는다. 세대 번호를 올려서 스스로 빠지게 한다
        Generation++;

        ResetProgress();

        if(MapData.Count == 0)
            return;

        if(player != null) player.transform.position = MapData[0].pos;
        if(enemy != null) enemy.transform.position = TargetPos(false);
    }

    // 아직 처리 안 된 착지 이벤트가 남아있는지. BattleManager가 연쇄를 계속할지 판단한다
    public static bool HasPendingEvent
        => playerPendingEvent != EventType.normal || enemyPendingEvent != EventType.normal;

    // 꺼내면서 지운다. 같은 칸이 두 번 발동하는 걸 막으려면 반드시 꺼낼 때 지워야 한다 —
    // 이벤트 카드가 또 말을 움직이므로, 지우기 전에 다음 착지가 덮어쓸 수 있다
    public static EventType TakePendingEvent(bool forPlayer)
    {
        EventType pending = forPlayer ? playerPendingEvent : enemyPendingEvent;

        if(forPlayer) playerPendingEvent = EventType.normal;
        else enemyPendingEvent = EventType.normal;

        return pending;
    }

    public static void ClearPendingEvents()
    {
        playerPendingEvent = EventType.normal;
        enemyPendingEvent = EventType.normal;
    }

    // 에이전트 관측용. 해당 말 기준 offset칸 앞의 이벤트 타입.
    // forPlayer를 뒤집으면 상대 쪽 시점이 되어 같은 정책을 상대 말에도 쓸 수 있다
    public static EventType EventAtOffset(int offset, bool forPlayer = true)
    {
        if(instance == null || instance.MapData.Count == 0)
            return EventType.normal;

        int basePos = forPlayer ? playerPos : enemyPos;
        return instance.MapData[instance.WrapPos(basePos + offset)].eventType;
    }

    void LoadMap(string name)
    {
        mapName = name;
        MapData = JsonParser.GetMap(name);

        if(MapData.Count == 0)
            Debug.LogError($"맵 \"{name}\"이 비어있어서 말을 배치할 수 없음.");
    }

    void Start()
    {
        if(MapData.Count == 0)
            return;

        SpawnHorse();
        foreach(var Cell in MapData)
        {
            var cell = Cell.pos;
            var obj = Instantiate(CellPre, cell, Quaternion.identity);
            obj.transform.SetParent(transform);

            // JSON의 color를 칸 색으로 반영
            if(obj.TryGetComponent<SpriteRenderer>(out var sr))
                sr.color = Cell.color;
        }
    }

    void SpawnHorse()
    {
        player = Instantiate(PlayerPre, MapData[playerPos].pos, Quaternion.identity);
        enemy = Instantiate(EnemyPre, MapData[enemyPos].pos, Quaternion.identity);

        Debug.Log($"목표 전진량 {TargetProgress}칸 ({MapData.Count}칸 x {Mathf.Max(1, targetLaps)}바퀴)");
    }

    void Update()
    {
        if(player != null && enemy != null && !IsMoving)
            UpdateHorse();
    }

    void UpdateHorse()
    {
        if(((Vector2)player.transform.position - TargetPos(true)).sqrMagnitude > 0.00001f)
        {
            player.transform.position = Vector2.Lerp((Vector2)player.transform.position, TargetPos(true), 0.1f);
        }
        if(((Vector2)enemy.transform.position - TargetPos(false)).sqrMagnitude > 0.00001f)
        {
            enemy.transform.position = Vector2.Lerp((Vector2)enemy.transform.position, TargetPos(false), 0.1f);
        }
    }

    int WrapPos(int pos)
    {
        int count = MapData.Count;
        return ((pos % count) + count) % count;
    }

    // 같은 칸에 겹치면 enemy를 위로 살짝 띄운다
    Vector2 TargetPos(bool isPlayer)
    {
        if(isPlayer) return MapData[playerPos].pos;
        return (playerPos != enemyPos)? MapData[enemyPos].pos : MapData[enemyPos].pos + Vector2.up * 0.5f;
    }

    public void EnemyWin()
    {
        MoveHorses(playerDelta: -1, enemyDelta: 2);
    }

    public void PlayerWin()
    {
        MoveHorses(playerDelta: 2, enemyDelta: -1);
    }

    public void MoveHorses(int playerDelta, int enemyDelta)
    {
        // 완주가 확정된 뒤 뒤늦게 도착한 이동 요청은 무시한다
        if(IsGameOver)
            return;

        if(playerMove != null) StopCoroutine(playerMove);
        if(enemyMove != null) StopCoroutine(enemyMove);

        playerMove = playerDelta != 0 ? StartCoroutine(MoveTo(true, playerDelta)) : null;
        enemyMove = enemyDelta != 0 ? StartCoroutine(MoveTo(false, enemyDelta)) : null;
    }

    // delta 칸만큼 한 칸씩 순서대로 이동
    IEnumerator MoveTo(bool isPlayer, int delta)
    {
        Transform horse = (isPlayer? player : enemy).transform;
        int dir = (delta >= 0)? 1 : -1;
        int steps = Mathf.Abs(delta);

        // 학습 중에는 칸마다 한 프레임씩 쓰지 않는다.
        // 한 턴이 15~25프레임인데 이동만 최대 6프레임이라 여기가 가장 큰 덩어리다.
        // 칸 계산은 그대로 한 칸씩 돌린다 — CheckGoal이 매 칸 돌아야 완주 시점이 안 밀린다.
        //
        // yield를 한 번은 반드시 해야 한다. 코루틴이 동기적으로 끝나버리면
        // MoveHorses의 playerMove 대입이 코루틴 종료 뒤에 일어나 IsMoving이 영원히 true로 남는다
        if(TrainingMode.Enabled)
        {
            for(int step = 0; step < steps; step++)
            {
                Advance(isPlayer, dir);
                CheckGoal();
            }

            horse.position = TargetPos(isPlayer);
            yield return null;
        }
        else
        {
            for(int step = 0; step < steps; step++)
            {
                Advance(isPlayer, dir);
                CheckGoal();

                horse.DOMove(TargetPos(isPlayer), stepDuration).SetEase(Ease.InOutExpo);
                yield return new WaitForSeconds(stepDuration);
                horse.position = TargetPos(isPlayer);
                SoundManager.AudioShot(horse.position, "Move", 1f);
            }
        }

        EventType landed = MapData[isPlayer ? playerPos : enemyPos].eventType;
        if(landed != EventType.normal)
        {
            Debug.Log($"{(isPlayer ? "플레이어" : "적")}가 {landed} 이벤트 칸에 도착했습니다.");
            if(isPlayer) playerPendingEvent = landed;
            else enemyPendingEvent = landed;
        }

        if(isPlayer) playerMove = null;
        else enemyMove = null;
    }

    // 한 칸 전진(또는 후퇴). 위치는 감기지만 전진량은 감기지 않는다 — 완주 판정이 그 값을 본다
    private static void Advance(bool isPlayer, int dir)
    {
        if(isPlayer)
        {
            playerPos = instance.WrapPos(playerPos + dir);
            playerProgress += dir;
        }
        else
        {
            enemyPos = instance.WrapPos(enemyPos + dir);
            enemyProgress += dir;
        }
    }

    // 플레이어와 상대 이동 코루틴이 동시에 돌기 때문에 양쪽에서 불린다.
    // IsGameOver 플래그로 GameOver가 두 번 발행되는 걸 막는다
    void CheckGoal()
    {
        if(IsGameOver || TargetProgress <= 0)
            return;

        bool playerDone = playerProgress >= TargetProgress;
        bool enemyDone = enemyProgress >= TargetProgress;

        if(!playerDone && !enemyDone)
            return;

        IsGameOver = true;

        // 한 카드가 양쪽을 동시에 골인시킬 수 있다(예: 우쿨렐레). 더 많이 간 쪽이 이기고,
        // 그것도 같으면 플레이어를 우선한다
        bool playerWins = playerDone && (!enemyDone || playerProgress >= enemyProgress);

        Debug.Log($"<color=cyan>게임 종료. 플레이어 {playerProgress} / 상대 {enemyProgress} (목표 {TargetProgress}) → {(playerWins ? "플레이어" : "상대")} 승</color>");
        GameOver?.Invoke(playerWins);
    }
}