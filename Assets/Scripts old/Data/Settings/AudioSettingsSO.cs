using UnityEngine;
using UnityEngine.Audio;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/AudioSettingsSO")]
    public class AudioSettingsSO : ScriptableObject
    {
        [Header("Mixer")]
        public AudioMixerGroup audioMixerGroup;
        public string masterVolumeParam = "MasterVolume";
        public string vfxVolumeParam = "VFXVolume";
        public string musicVolumeParam = "MusicVolume";

        [Header("Master dB")]
        public float minMainVolumeDb = -80f;
        public float maxMainVolumeDb = 20f;
        public float defaultMainVolumeDb = 0f;

        [Header("VFX dB")]
        public float minVFXVolumeDb = -80f;
        public float maxVFXVolumeDb = 20f;
        public float defaultVFXVolumeDb = 0f;

        [Header("Music dB")]
        public float minMusicVolumeDb = -80f;
        public float maxMusicVolumeDb = 20f;
        public float defaultMusicVolumeDb = 0f;
    }
}
