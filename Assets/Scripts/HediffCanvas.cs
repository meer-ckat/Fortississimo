using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HediffCanvas : MonoBehaviour
{
    public GameObject labelprefab;
    public Vector2 padding = new Vector2(24f, 12f);
    public float maxWidth = 720f;
    public float spacing = 8f;
    public static HediffCanvas instance;

    // HashSet 대신 순서가 보장되는 List 사용 (UI 배치 순서 유지용)
    public static List<GameObject> labels = new List<GameObject>();

    float nextY;

    void Awake()
    {
        instance = this;
    }

    // 카메라를 비활성화하면(학습 중 GPU 절약) Camera.main이 null이 되어
    // Camera.main.transform.position에서 NullReferenceException이 났다.
    // 소리 위치일 뿐이라 카메라가 없으면 원점으로 둔다
    static Vector3 ListenerPosition()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    // 라벨 하나를 만들어 크기와 배경색까지 맞춰 돌려준다.
    //
    // labels 목록에는 넣지 않는다 — 목록에 들어가면 UpdateLabelPositions가 위치를 잡아버려서
    // 커서를 따라다녀야 하는 툴팁(CardHighLighter) 같은 용도로는 쓸 수 없다.
    // parent를 주면 그 아래에 붙는다. 다른 캔버스에 붙여야 카드 위에 그려진다
    public GameObject CreateDetachedLabel(string text, Transform parent = null)
    {
        if (labelprefab == null)
        {
            Debug.LogError("[HediffCanvas] labelprefab이 비어있음");
            return null;
        }

        GameObject label = Instantiate(labelprefab);

        if (!label.TryGetComponent(out RectTransform labelTransform))
        {
            Debug.LogError("[HediffCanvas] labelprefab에 RectTransform이 없음");
            Destroy(label);
            return null;
        }

        labelTransform.SetParent(parent != null ? parent : transform, false);

        var textComponent = label.GetComponentInChildren<TextMeshProUGUI>();

        if (textComponent == null)
        {
            Debug.LogError("[HediffCanvas] labelprefab에 TextMeshProUGUI가 없음");
            Destroy(label);
            return null;
        }

        label.TryGetComponent(out Image bgImage);
        textComponent.text = text;

        Vector2 textSize = textComponent.GetPreferredValues(text, maxWidth, 0f);
        textSize.x = Mathf.Min(textSize.x, maxWidth);

        textComponent.rectTransform.sizeDelta = textSize;
        labelTransform.sizeDelta = textSize + padding;

        // 🎨 텍스트 색상을 읽어 배경에 보색 적용
        ApplyComplementaryBackground(textComponent, bgImage);

        return label;
    }

    public GameObject SpawnLabel(string text)
    {
        // 학습 중에는 턴당 3~5개씩 만들어진다. TMP 메시 생성이 싸지 않다.
        // null을 돌려줘도 EditLabel/RemoveLabel이 labels에 없는 걸로 보고 그냥 넘어간다
        if (TrainingMode.Enabled)
            return null;

        GameObject label = CreateDetachedLabel(text);

        if (label == null)
            return null;

        // 화면 밖에서 시작해 UpdateLabelPositions가 제자리로 밀어 넣는다
        label.GetComponent<RectTransform>().anchoredPosition = new Vector2(-1000f, -nextY);

        labels.Add(label);
        SoundManager.AudioShot(ListenerPosition(), "On", 0.5f);

        UpdateLabelPositions();
        return label;
    }

    public GameObject SpawnLabel(string text, float duration)
    {
        GameObject label = SpawnLabel(text);

        // 라벨을 안 만들었으면 지울 코루틴도 띄우지 않는다
        if (label == null)
            return null;

        StartCoroutine(RemoveLabelAfterDelay(label, duration));
        return label;
    }
    private IEnumerator RemoveLabelAfterDelay(GameObject label, float delay)
    {
        yield return new WaitForSeconds(delay);
        RemoveLabel(label);
    }

    public void EditLabel(GameObject label, string newText)
    {
        if (!labels.Contains(label)) return;

        var textComponent = label.GetComponentInChildren<TextMeshProUGUI>();
        var bgImage = label.GetComponent<Image>();
        textComponent.text = newText;

        label.transform.DOPunchPosition(new Vector3(0f, 30f, 0f), 0.4f, 1, 0.5f);

        Vector2 textSize = textComponent.GetPreferredValues(newText, maxWidth, 0f);
        textSize.x = Mathf.Min(textSize.x, maxWidth);

        textComponent.rectTransform.sizeDelta = textSize;
        label.GetComponent<RectTransform>().sizeDelta = textSize + padding;

        ApplyComplementaryBackground(textComponent, bgImage);

        UpdateLabelPositions();
    }

    /// <summary>
    /// TMP 텍스트의 글자 색상 평균을 구해 보색을 배경에 적용합니다.
    /// </summary>
    private void ApplyComplementaryBackground(TextMeshProUGUI tmpText, Image bgImage)
    {
        if (tmpText == null || bgImage == null) return;

        // TMP 메시 정보 강제 업데이트
        tmpText.ForceMeshUpdate();
        TMP_TextInfo textInfo = tmpText.textInfo;

        long rSum = 0, gSum = 0, bSum = 0;
        int visibleCharCount = 0;

        // 글자별 정점 색상 수집
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Color32 charColor = textInfo.meshInfo[materialIndex].colors32[vertexIndex];

            rSum += charColor.r;
            gSum += charColor.g;
            bSum += charColor.b;
            visibleCharCount++;
        }

        // 글자가 없거나 색상이 없을 경우 기본 색상(예: 불투명한 무채색) 처리
        if (visibleCharCount == 0) return;

        // 1. 평균 색상 산출
        Color avgColor = new Color(
            (rSum / visibleCharCount) / 255f,
            (gSum / visibleCharCount) / 255f,
            (bSum / visibleCharCount) / 255f,
            1.0f
        );

        Color.RGBToHSV(avgColor, out float h, out float s, out float v);
        float compHue = (h + 0.5f) % 1.0f;

        s = Mathf.Clamp(s, 0.2f, 0.8f); // 너무 쨍하거나 둔탁하지 않게 조정
        v = v > 0.5f ? 0.2f : 0.9f;     // 글자가 밝으면 배경을 어둡게, 글자가 어두우면 밝게

        Color compColor = Color.HSVToRGB(compHue, s, v);
        compColor.a = 0.85f; // 배경 투명도 살짝 적용

        bgImage.color = compColor;
    }

    public void RemoveLabel(GameObject label)
    {
        if (labels.Contains(label))
        {
            labels.Remove(label);
            
            // anchoredPosition.y를 기반으로 퇴장 애니메이션 진행
            var rect = label.GetComponent<RectTransform>();
            rect.DOAnchorPosX(-1000f, 0.3f).SetEase(Ease.InCubic);
            
            Destroy(label, 0.35f); // 3초 대신 애니메이션 직후 제거
            SoundManager.AudioShot(ListenerPosition(), "Off", 0.5f);
            UpdateLabelPositions();
        }
    }

    private void UpdateLabelPositions()
    {
        nextY = 0f;
        foreach (var label in labels)
        {
            if (label != null)
            {
                var labelTransform = label.GetComponent<RectTransform>();
                labelTransform.DOAnchorPos(new Vector2(0f, -nextY), 0.3f).SetEase(Ease.OutCubic);
                nextY += labelTransform.sizeDelta.y + spacing;
            }
        }
    }

    void Start()
    {
        StartCoroutine(TestSpawnLabelsCoroutine());
    }

    IEnumerator TestSpawnLabelsCoroutine()
    {
        yield return new WaitForSeconds(1f);
        SpawnLabel("<color=blue>테스트 중...</color>", 3f);
        yield return new WaitForSeconds(2f);
        SpawnLabel("<color=green>Current AI Version: </color><color=yellow>UKULELE 300k</color>", 4f);
        yield return new WaitForSeconds(2f);
        SpawnLabel("<color=yellow>Tip:</color>현재 AI는 2시간 반 동안 300,000번의 학습을 통해 만들어진\n이 게임 한정 최?첨단? AI?이다.", 6f);
        yield return new WaitForSeconds(1f);
        for (int i = 0; i < 5; i++)
        {
            SpawnLabel($"<color=magenta>테스트 라벨 {i + 1}</color>", 0.5f);
            yield return new WaitForSeconds(0.1f);
        }
        yield return new WaitForSeconds(1f);    
        SpawnLabel("<color=red>테스트 완료!</color>", 3f);
        yield return new WaitForSeconds(1f);
        SpawnLabel("[INFO] Ukulele. Step: 300000. Time Elapsed: 9087.406 s. Mean Reward: 1.989. Std of Reward: 1.217.", 6f);
    }
}