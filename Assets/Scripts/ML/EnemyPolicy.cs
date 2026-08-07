using System.Collections.Generic;
using System.Text;
using Unity.InferenceEngine;
using UnityEngine;

// 학습된 모델로 상대 수를 직접 두는 컴포넌트.
//
// ML-Agents의 Agent를 쓰지 않는 이유:
//  1. Agent는 샘플링된 행동만 돌려주고 확률 분포를 안 내준다. 카드별 선택 확률을 보려면
//     softmax 텐서가 필요한데 그건 내부에 갇혀 있다.
//  2. Agent의 결정은 비동기(다음 Academy 스텝)라 코루틴으로 기다려야 한다.
//     여기서는 동기로 끝나므로 턴 흐름이 단순해진다.
//
// 모델의 softmax 출력은 기본 익스포트에 없다. 아래 파이썬으로 한 번 노출시켜야 한다:
//   python -c "import onnx; from onnx import helper, TensorProto; \
//     m=onnx.load(P); m.graph.output.append( \
//       helper.make_tensor_value_info('softmax', TensorProto.FLOAT, ['batch', 6])); onnx.save(m, P)"
[DisallowMultipleComponent]
public class EnemyPolicy : MonoBehaviour
{
    [Header("Model")]
    [Tooltip("Assets/Models 안의 .onnx. softmax가 출력으로 노출돼 있어야 확률 로그가 나온다")]
    [SerializeField] private ModelAsset modelAsset;

    [Header("Behaviour")]
    [Tooltip("켜면 항상 확률이 가장 높은 카드를 낸다. 끄면 확률대로 뽑는다(학습 때와 같은 방식)")]
    [SerializeField] private bool greedy = false;

    [Tooltip("난이도 손잡이. 1 = 학습된 그대로. 값을 올릴수록 분포가 평평해져 약해지고, " +
             "내릴수록 뾰족해져 강해진다. 2~3이면 눈에 띄게 물러지고, 5면 거의 무작위. " +
             "재학습 없이 난이도를 연속적으로 조절할 수 있다")]
    [SerializeField, Range(0.2f, 5f)] private float temperature = 1f;

    [Header("Log")]
    [Tooltip("카드별 선택 확률을 Console에 찍는다")]
    [SerializeField] private bool logProbabilities = true;
    [Tooltip("화면 라벨로도 띄운다")]
    [SerializeField] private bool showOnScreen = true;

    private Worker worker;
    private readonly List<BaseCardData> hand = new List<BaseCardData>();
    private readonly float[] obs = new float[UkuleleObservation.Size];
    private readonly float[] masks = new float[UkuleleObservation.HandSlots];
    private readonly StringBuilder sb = new StringBuilder();

    private void Awake()
    {
        TrainingMode.EnemyPolicy = this;

        if(modelAsset == null)
        {
            Debug.LogError("[EnemyPolicy] Model이 비어있음. 상대는 무작위로 동작한다");
            return;
        }

        worker = new Worker(ModelLoader.Load(modelAsset), BackendType.CPU);

        // Debug.Log는 학습 모드 로그 필터에 삼켜지므로 Warning으로 띄운다.
        // 이 줄이 Console에 안 보이면 컴포넌트가 아예 안 돌고 있는 것이다
        // (오브젝트가 비활성이거나 컴포넌트를 안 붙였거나)
        Debug.LogWarning($"[EnemyPolicy] 준비 완료. Model={modelAsset.name}, Greedy={greedy}");
    }

    private void OnDestroy()
    {
        worker?.Dispose();
        worker = null;

        if(TrainingMode.EnemyPolicy == this)
            TrainingMode.EnemyPolicy = null;
    }

    public bool Ready => worker != null;

    // 상대 손패에서 낼 카드를 고르고, 그 카드를 손패에서 뺀다.
    // 못 고르면 null을 돌려주고 호출부가 무작위로 넘긴다
    public BaseCardData ChooseCard()
    {
        // 관측이 정렬된 순서로 들어가므로 카드도 그 순서로 꺼내야 한다.
        // 실제 손패 리스트 순서로 꺼내면 확률과 다른 카드가 나간다
        UkuleleObservation.GetOrderedHand(false, hand);

        if(worker == null || hand.Count == 0)
            return null;

        // 관측은 UkuleleObservation이 단독으로 만든다. 학습 때와 같은 코드라 어긋날 수 없다.
        // mine=false로 넣으면 전부 상대 시점으로 뒤집힌다
        int cursor = 0;
        UkuleleObservation.Write(false, v => obs[cursor++] = v);

        if(cursor != obs.Length)
        {
            Debug.LogError($"[EnemyPolicy] 관측 길이가 {cursor}. {obs.Length}여야 한다");
            return null;
        }

        // 1 = 낼 수 있음, 0 = 막힘. 손패보다 뒤쪽 슬롯은 존재하지 않으므로 막는다
        for(int i = 0; i < masks.Length; i++)
            masks[i] = i < hand.Count ? 1f : 0f;

        float[] probs;
        using(var obsTensor = new Tensor<float>(new TensorShape(1, obs.Length), obs))
        using(var maskTensor = new Tensor<float>(new TensorShape(1, masks.Length), masks))
        using(var memTensor = new Tensor<float>(new TensorShape(1, 1, 0)))
        {
            // 입력 순서는 모델 선언 순서와 같아야 한다: obs_0, action_masks, recurrent_in.
            // recurrent_in은 memory_size가 0이라 어떤 노드도 쓰지 않지만 입력으로는 선언돼 있다
            worker.Schedule(obsTensor, maskTensor, memTensor);

            if(worker.PeekOutput("softmax") is not Tensor<float> softmax)
            {
                Debug.LogError("[EnemyPolicy] 모델에 softmax 출력이 없음. " +
                               "확률을 보려면 onnx에 softmax를 출력으로 추가해야 한다");
                return null;
            }

            probs = softmax.DownloadToArray();
        }

        ApplyTemperature(probs, hand.Count);

        int pick = greedy ? ArgMax(probs, hand.Count) : Sample(probs, hand.Count);
        pick = Mathf.Clamp(pick, 0, hand.Count - 1);

        BaseCardData chosen = hand[pick];

        // hand는 정렬된 사본이다. 실제 손패에서 빼야 한다
        CardManager.EnemyCard.Remove(chosen);
        hand.RemoveAt(pick);

        Report(probs, hand, chosen, pick);
        return chosen;
    }

    // 확률 분포를 p^(1/T)로 다시 정규화한다.
    //   T = 1  → 그대로
    //   T > 1  → 평평해짐. 나쁜 카드도 곧잘 내게 되어 약해진다
    //   T < 1  → 뾰족해짐. 최선수에 더 몰려 강해진다
    // 재학습 없이 난이도를 연속적으로 바꾸는 표준적인 방법이다
    private void ApplyTemperature(float[] probs, int count)
    {
        if(Mathf.Approximately(temperature, 1f) || count <= 0)
            return;

        float inv = 1f / Mathf.Max(0.01f, temperature);
        float total = 0f;

        for(int i = 0; i < count && i < probs.Length; i++)
        {
            probs[i] = Mathf.Pow(Mathf.Max(probs[i], 1e-8f), inv);
            total += probs[i];
        }

        if(total <= 0f)
            return;

        for(int i = 0; i < count && i < probs.Length; i++)
            probs[i] /= total;
    }

    private static int ArgMax(float[] probs, int count)
    {
        int best = 0;
        for(int i = 1; i < count && i < probs.Length; i++)
            if(probs[i] > probs[best]) best = i;
        return best;
    }

    // 확률대로 뽑는다. 마스킹된 뒤쪽 슬롯은 애초에 0에 가까우므로 count까지만 본다
    private static int Sample(float[] probs, int count)
    {
        float total = 0f;
        for(int i = 0; i < count && i < probs.Length; i++)
            total += probs[i];

        if(total <= 0f)
            return UnityEngine.Random.Range(0, count);

        // Unity.InferenceEngine에도 Random이 있어서 정규화된 이름을 쓴다
        float roll = UnityEngine.Random.value * total;
        for(int i = 0; i < count && i < probs.Length; i++)
        {
            roll -= probs[i];
            if(roll <= 0f) return i;
        }
        return count - 1;
    }

    // hand에서 이미 chosen이 제거된 상태로 들어온다. 원래 순서를 복원해서 찍는다
    private void Report(float[] probs, List<BaseCardData> handAfter, BaseCardData chosen, int pickIndex)
    {
        if(!logProbabilities && !showOnScreen)
            return;

        sb.Clear();
        sb.Append("[상대 AI] ");

        int total = handAfter.Count + 1;
        for(int i = 0; i < total; i++)
        {
            BaseCardData card = (i == pickIndex) ? chosen : handAfter[i < pickIndex ? i : i - 1];
            float p = i < probs.Length ? probs[i] * 100f : 0f;
            bool picked = i == pickIndex;

            sb.Append(picked ? "▶ " : "  ");
            sb.Append($"{card.Title} {p:F1}%");
            if(i < total - 1) sb.Append(picked ? "  |  " : " | ");
        }

        string line = sb.ToString();

        if(logProbabilities)
        {
            // 학습 모드 필터가 켜져 있으면 Debug.Log가 통째로 삼켜진다.
            // 이 로그는 사용자가 요청한 출력이라 어느 쪽이든 반드시 보여야 한다
            if(TrainingMode.Enabled) Debug.LogWarning(line);
            else Debug.Log(line);
        }

        if(showOnScreen && HediffCanvas.instance != null)
            HediffCanvas.instance.SpawnLabel($"<color=red><size=60%>{line}</size></color>", 6.5f);
    }
}
