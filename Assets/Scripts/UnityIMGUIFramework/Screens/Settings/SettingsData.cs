using System;
using UnityEngine;

[Serializable]
public struct SettingsData
{
    [Range(0f, 1f)] public float masterVolume;
    [Range(0f, 1f)] public float musicVolume;
    [Range(0f, 1f)] public float sfxVolume;
    public bool screenShake;
    public bool fullscreen;

    public bool Matches(SettingsData other)
    {
        return Mathf.Approximately(masterVolume, other.masterVolume)
            && Mathf.Approximately(musicVolume, other.musicVolume)
            && Mathf.Approximately(sfxVolume, other.sfxVolume)
            && screenShake == other.screenShake
            && fullscreen == other.fullscreen;
    }

    public static SettingsData CreateDefault()
    {
        return new SettingsData
        {
            masterVolume = 0.8f,
            musicVolume = 0.65f,
            sfxVolume = 0.75f,
            screenShake = true,
            fullscreen = Screen.fullScreen
        };
    }
}
