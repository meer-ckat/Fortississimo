using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum MoteSpace
{
    Target, // 대상 Transform을 따라다닌다
    World,  // 고정된 월드 좌표
    Screen  // 고정된 스크린(캔버스) 좌표
}

public class Mote
{
    public TextMeshProUGUI Text;

    public MoteSpace Space;
    public Transform Target;
    public RectTransform TargetRect;
    public Vector3 Position;

    public float Life;
    public float Lifetime;

    public Vector2 Offset;
    public Vector2 Velocity;

    public Mote(
        TextMeshProUGUI text,
        float lifetime,
        Vector2 velocity,
        MoteSpace space,
        Transform target = null,
        Vector3 position = default
        )
    {
        Text = text;
        Lifetime = lifetime;
        Life = lifetime;
        Velocity = velocity;

        Space = space;
        Target = target;
        Position = position;

        if (target != null)
            TargetRect = target.GetComponent<RectTransform>();
    }
}

public class MoteManager : MonoBehaviour
{
    public static MoteManager instance;

    [SerializeField] private Canvas canvas;

    [Header("Mote Text")]
    [SerializeField] private TMP_FontAsset moteFont;
    [SerializeField] private float defaultFontSize = 36f;
    [SerializeField] private Camera worldCamera;

    public float defaultLifetime = 2f;

    private readonly List<Mote> motes = new();

    void Awake()
    {
        instance = this;

        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    void Update()
    {
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            Mote mote = motes[i];

            if (mote.Text == null)
            {
                motes.RemoveAt(i);
                continue;
            }

            mote.Life -= Time.deltaTime;

            if (mote.Life <= 0f)
            {
                Destroy(mote.Text.gameObject);
                motes.RemoveAt(i);
                continue;
            }

            mote.Offset += mote.Velocity * Time.deltaTime;

            UpdateMotePosition(mote);

            mote.Text.alpha = mote.Life / mote.Lifetime;
        }
    }

    // 카메라를 비활성화하면 Camera.main이 null이라 WorldToScreenPoint에서 터진다.
    // 나중에 다시 켤 수도 있으므로 매번 재취득을 시도한다
    private bool EnsureCamera()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        return worldCamera != null;
    }

    private void UpdateMotePosition(Mote mote)
    {
        Vector3 position;

        switch (mote.Space)
        {
            case MoteSpace.Target:
                if (mote.Target == null)
                    return;

                // 캔버스 위 UI라면 그 자체가 이미 스크린 좌표다
                if (mote.TargetRect != null)
                {
                    position = mote.TargetRect.position;
                    break;
                }

                if (!EnsureCamera())
                    return;

                position = worldCamera.WorldToScreenPoint(mote.Target.position);
                break;

            case MoteSpace.World:
                if (!EnsureCamera())
                    return;

                position = worldCamera.WorldToScreenPoint(mote.Position);
                break;

            default: // MoteSpace.Screen — 이미 스크린 좌표
                position = mote.Position;
                break;
        }

        position += (Vector3)mote.Offset;

        // WorldToScreenPoint의 z(카메라와의 거리)를 그대로 쓰면
        // 캔버스 평면에서 벗어나므로 캔버스 z에 맞춘다
        position.z = canvas.transform.position.z;

        mote.Text.rectTransform.position = position;
    }

    private TextMeshProUGUI CreateMoteText(string text)
    {
        TextMeshProUGUI tmp =
            new GameObject(
                "Mote",
                typeof(RectTransform),
                typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();

        RectTransform rect = tmp.rectTransform;

        rect.SetParent(canvas.transform, false);

        // 런타임에 만든 RectTransform은 앵커/피벗이 (0,0)이라
        // position이 "글자 중심"이 아니라 "박스 좌하단"이 된다.
        // 박스 크기의 절반만큼 글자가 밀려나므로 피벗을 가운데로 맞춰야 한다
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;

        // sizeDelta가 (0,0)이면 글자가 잘리므로 넉넉히 잡는다 (NoWrap이라 넘쳐도 그려진다)
        rect.sizeDelta = new Vector2(1000f, 100f);

        if (moteFont != null)
            tmp.font = moteFont;

        tmp.fontSize = defaultFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.text = text;

        return tmp;
    }

    private Mote Spawn(
        string text,
        MoteSpace space,
        Transform target,
        Vector3 position,
        Vector2 velocity,
        float lifetime)
    {
        // 학습 중에는 볼 사람이 없다. 모트 하나마다 TMP 오브젝트가 생기고
        // Update가 수명 내내 위치/알파를 갱신한다.
        // 호출부는 전부 반환값을 안 쓰므로 null을 돌려줘도 안전하다
        if (TrainingMode.Enabled)
            return null;

        Mote mote = new Mote(
            CreateMoteText(text),
            lifetime > 0f ? lifetime : defaultLifetime,
            velocity,
            space,
            target,
            position
        );

        UpdateMotePosition(mote);

        motes.Add(mote);

        return mote;
    }

    /// <summary>대상 Transform을 따라다니는 모트.</summary>
    public Mote SpawnMote(
        string text,
        Transform target,
        Vector2 velocity = default,
        float lifetime = -1f)
        => Spawn(text, MoteSpace.Target, target, default, velocity, lifetime);

    /// <summary>월드 좌표에 고정되는 모트. 카메라를 따라 화면상 위치가 바뀐다.</summary>
    public Mote SpawnMote(
        string text,
        Vector3 worldPosition,
        Vector2 velocity = default,
        float lifetime = -1f)
        => Spawn(text, MoteSpace.World, null, worldPosition, velocity, lifetime);

    /// <summary>
    /// 스크린 좌표에 고정되는 모트.
    /// Screen Space - Overlay 캔버스의 RectTransform.position을 그대로 넘기면 된다.
    /// </summary>
    public Mote SpawnMoteAtScreen(
        string text,
        Vector3 screenPosition,
        Vector2 velocity = default,
        float lifetime = -1f)
        => Spawn(text, MoteSpace.Screen, null, screenPosition, velocity, lifetime);
}
