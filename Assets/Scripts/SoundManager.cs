using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static float GeneralVolume = 0.3f;
    public static float MusicVolume = 1f;
    public static float SFXVolume = 1f;
    public static SoundManager instance;

    [SerializeField] private AudioSource musicSource;
    [SerializeField] private int maxAdios = 128;
    
    public static AudioClip CurrentMusic => instance?.musicSource?.clip;
    public static float MusicTime
{
    get => instance?.musicSource?.time ?? 0f;
    set
    {
        AudioSource s = instance?.musicSource;

        if (s == null || s.clip == null)
            return;

        // 클립 길이를 넘기면 AudioSource.time이 예외를 던진다.
        s.time = Mathf.Clamp(value, 0f, Mathf.Max(0f, s.clip.length - 0.05f));
    }
}

public static void PauseMusic()
{
    if (instance?.musicSource != null)
        instance.musicSource.Pause();
}

public static void ResumeMusic()
{
    if (instance?.musicSource?.clip == null)
        return;

    instance.musicSource.UnPause();
}
    public static bool IsMusicPlaying => instance?.musicSource?.isPlaying ?? false;

    public static void GetMusicSpectrum(float[] data)
    {
        if (instance?.musicSource != null && data != null)
            instance.musicSource.GetSpectrumData(data, 0, FFTWindow.BlackmanHarris);
    }

    private Coroutine musicRoutine;
    private float musicBaseVolume = 1f;
    private float musicFade = 1f;

    public class PooledAudio
    {
        public AudioSource source;
        public float startTime, duration, baseVolume = 1f;

        public PooledAudio(AudioSource source, float startTime)
        {
            this.source = source;
            this.startTime = startTime;
        }
    }

    public static readonly List<PooledAudio> adiosPool = new();

    void Awake()
    {
        instance = this;
        PreWarmPool(10);
        RefreshVolumes();
    }

    void Update()
    {
        foreach (var p in adiosPool)
            if (p.source != null && p.source.clip != null &&
                Time.time >= p.startTime + p.duration && !p.source.isPlaying)
                p.source.clip = null;
    }

    public static void SetGeneralVolume(float v) { GeneralVolume = Mathf.Clamp01(v); RefreshVolumes(); }
    public static void SetMusicVolume(float v) { MusicVolume = Mathf.Clamp01(v); RefreshVolumes(); }
    public static void SetSFXVolume(float v) { SFXVolume = Mathf.Clamp01(v); RefreshVolumes(); }

    public static void SetMusicSource(AudioSource source)
    {
        if (instance == null) return;
        instance.musicSource = source;
        RefreshVolumes();
    }

    static void RefreshVolumes()
    {
        if (instance == null) return;

        if (instance.musicSource != null)
            instance.musicSource.volume =
                instance.musicBaseVolume * instance.musicFade * GeneralVolume * MusicVolume;

        foreach (var p in adiosPool)
            if (p.source != null)
                p.source.volume = p.baseVolume * GeneralVolume * SFXVolume;
    }

    // ───────────── BGM ─────────────

    public static void PlayMusic(string name, float volume = 1f, float fade = 0.5f, bool loop = true)
    {
        var clip = Resources.Load<AudioClip>($"Audio/{name}");

        if (clip == null)
        {
            Debug.LogWarning($"BGM {name} not found in Resources/Audio");
            return;
        }

        PlayMusic(clip, volume, fade, loop);
    }

    public static void PlayMusic(AudioClip clip, float volume = 1f, float fade = 0.5f, bool loop = true)
    {
        if (instance == null || clip == null || TrainingMode.Enabled) return;

        if (instance.musicSource?.clip == clip && instance.musicSource.isPlaying)
            return;

        instance.StartMusicRoutine(clip, volume, fade, loop);
    }

    public static void StopMusic(float fade = 0.5f)
    {
        if (instance?.musicSource == null) return;
        instance.StartMusicRoutine(null, 1f, fade, false);
    }

    void StartMusicRoutine(AudioClip next, float volume, float fade, bool loop)
    {
        if (musicSource == null) return;

        if (musicRoutine != null)
            StopCoroutine(musicRoutine);

        musicRoutine = StartCoroutine(ChangeMusic(next, Mathf.Clamp01(volume), fade, loop));
    }

    IEnumerator ChangeMusic(AudioClip next, float volume, float fade, bool loop)
    {
        if (musicSource.isPlaying)
            yield return FadeMusic(0f, fade);

        musicSource.Stop();
        musicSource.clip = next;

        if (next != null)
        {
            musicSource.loop = loop;
            musicBaseVolume = volume;
            musicSource.Play();
            yield return FadeMusic(1f, fade);
        }

        musicRoutine = null;
    }

    IEnumerator FadeMusic(float target, float duration)
    {
        float from = musicFade;

        if (duration <= 0f)
        {
            musicFade = target;
            RefreshVolumes();
            yield break;
        }

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            musicFade = Mathf.Lerp(from, target, t / duration);
            RefreshVolumes();
            yield return null;
        }

        musicFade = target;
        RefreshVolumes();
    }

    // ───────────── SFX ─────────────

    void PreWarmPool(int count)
    {
        for (int i = 0; i < count; i++)
            CreateNewAudioSource();
    }

    PooledAudio CreateNewAudioSource()
    {
        if (adiosPool.Count >= maxAdios) return null;

        var obj = new GameObject($"Audio Pooling {adiosPool.Count}", typeof(AudioSource));
        obj.transform.SetParent(transform);

        var source = obj.GetComponent<AudioSource>();
        source.playOnAwake = false;

        var pooled = new PooledAudio(source, Time.time);
        adiosPool.Add(pooled);

        return pooled;
    }

    public static void AudioShot(Vector3 pos, string name, float volume = 1f)
    {
        if (TrainingMode.Enabled) return;

        var clip = Resources.Load<AudioClip>($"Audio/{name}");

        if (clip == null)
        {
            Debug.LogWarning($"AudioClip {name} not found in Resources/Audio");
            return;
        }

        AudioShot(pos, clip, volume);
    }

    public static void AudioShot(Vector3 pos, AudioClip clip, float volume = 1f)
    {
        if (TrainingMode.Enabled || clip == null || instance == null) return;

        var obj = new GameObject($"Audio: {clip.name}", typeof(AudioSource));
        obj.transform.position = pos;

        var source = obj.GetComponent<AudioSource>();
        source.volume = volume * GeneralVolume * SFXVolume;
        source.PlayOneShot(clip);

        Destroy(obj, clip.length);
    }

    public static void UseAdios(Vector3 pos, AudioClip clip, float volume = 1f)
    {
        if (TrainingMode.Enabled || clip == null || instance == null) return;

        PooledAudio p = adiosPool.Find(x => x.source != null && !x.source.isPlaying);

        if (p == null)
            p = instance.CreateNewAudioSource();

        if (p == null && adiosPool.Count > 0)
        {
            p = adiosPool[0];

            foreach (var x in adiosPool)
                if (x.source != null && x.startTime < p.startTime)
                    p = x;

            p.source.Stop();
        }

        if (p?.source == null) return;

        p.source.transform.position = pos;
        p.source.clip = clip;
        p.baseVolume = volume;
        p.source.volume = volume * GeneralVolume * SFXVolume;
        p.startTime = Time.time;
        p.duration = clip.length;
        p.source.Play();
    }
}