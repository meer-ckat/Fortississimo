using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static float GeneralVolume = 0.3f;
    public static SoundManager instance;

    public class PooledAudio
    {
        public AudioSource source;
        public float startTime;
        public float duration; // 재생 시간을 체크하기 위해 추가
        
        public PooledAudio(AudioSource source, float startTime)
        {
            this.source = source;
            this.startTime = startTime;
            this.duration = 0f;
        }
    }
    
    public static List<PooledAudio> adiosPool = new();
    public int maxAdios = 128;

    void Awake()
    {
        instance = this;
        PreWarmPool(10); 
    }

    // 코루틴 대신 매 프레임 끝난 오디오의 클립을 null로 밀어주는 고속 스캔 루틴
    void Update()
    {
        int count = adiosPool.Count;
        for (int i = 0; i < count; i++)
        {
            var p = adiosPool[i];
            // 재생 시간이 만료되었고, 현재 실제로 플레이 중이 아니라면 채널 반환
            if (p.source != null && p.source.clip != null)
            {
                if (Time.time >= p.startTime + p.duration && !p.source.isPlaying)
                {
                    p.source.clip = null; // 오디오 채널 완전 회수
                }
            }
        }
    }

    private void PreWarmPool(int count)
    {
        for (int i = 0; i < count; i++)
        {
            CreateNewAudioSource();
        }
    }

    private PooledAudio CreateNewAudioSource()
    {
        if (adiosPool.Count >= maxAdios) return null;

        var obj = new GameObject($"Audio Pooling {adiosPool.Count}", typeof(AudioSource));
        obj.transform.SetParent(transform);
        
        var aud = obj.GetComponent<AudioSource>();
        aud.playOnAwake = false;
        
        var c = new PooledAudio(aud, Time.time);
        
        adiosPool.Add(c);
        return c;
    }

    // 기존 풀링 안 쓰는 원샷 메서드 (호환성 유지용)
    public static void AudioShot(Vector3 pos, AudioClip clip, float volume = 1f)
    {
        if (TrainingMode.Enabled || clip == null || instance == null) return;

        var obj = new GameObject($"Audio: {clip.name}", typeof(AudioSource));
        obj.transform.position = pos;

        var aud = obj.GetComponent<AudioSource>();
        aud.volume = volume * GeneralVolume;
        aud.playOnAwake = false;
        aud.PlayOneShot(clip);
        Destroy(obj, clip.length); // 기존 코드 누락 보완
    }

    public static void AudioShot(Vector3 pos, string AudioName, float volume = 1f)
    {
        if (TrainingMode.Enabled) return;

        var clip = Resources.Load<AudioClip>($"Audio/{AudioName}");
        if (clip == null)
        {
            Debug.LogWarning($"AudioClip {AudioName} not found in Resources/Audio");
            return;
        }
        AudioShot(pos, clip, volume);
    }

    // 가비지가 아예 없는 완성형 풀링 메서드
    public static void UseAdios(Vector3 position, AudioClip clip, float volume = 1f)
    {
        if (TrainingMode.Enabled || clip == null || instance == null) return;

        PooledAudio availableAud = null;
        int poolCount = adiosPool.Count;

        // 1. LINQ 대신 고속 for문으로 대기 중인 오디오 탐색 (가비지 0%)
        for (int i = 0; i < poolCount; i++)
        {
            if (adiosPool[i].source != null && !adiosPool[i].source.isPlaying)
            {
                availableAud = adiosPool[i];
                break;
            }
        }

        // 2. 대기 소스가 없다면 풀 최대치 안에서 새로 생성
        if (availableAud == null)
        {
            availableAud = instance.CreateNewAudioSource();
        }

        // 3. 풀이 완전히 가득 찼다면 (Sound Stealing)
        // 가장 오래전에 시작된 오디오 소스를 최적화 알고리즘으로 골라내서 뺏어옵니다.
        if (availableAud == null && poolCount > 0)
        {
            float oldestTime = float.MaxValue;
            int oldestIndex = 0;

            for (int i = 0; i < poolCount; i++)
            {
                if (adiosPool[i].source != null && adiosPool[i].startTime < oldestTime)
                {
                    oldestTime = adiosPool[i].startTime;
                    oldestIndex = i;
                }
            }

            availableAud = adiosPool[oldestIndex];
            availableAud.source.Stop(); // 점유 중인 채널 하드웨어 강제 회수
        }

        // 안전 장치    
        if (availableAud == null || availableAud.source == null) return;

        // 4. 컴포넌트 재설정 및 재생
        availableAud.source.gameObject.transform.position = position;
        availableAud.source.clip = clip;
        availableAud.source.volume = volume * GeneralVolume;
        availableAud.startTime = Time.time; 
        availableAud.duration = clip.length; // 길이 저장하여 Update에서 추적
        availableAud.source.Play();
    }
}
