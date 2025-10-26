using System;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;

namespace Game.Settings
{
    public sealed class SettingsService : ISettingsService, IInitializable, IDisposable
    {
        private readonly PlayerStatsSO _stats;
        private readonly Game.AudioSettingsSO _audio;

        private const string KEY_MOUSE = "settings.mouseSensitivity";
        private const string KEY_MASTER = "settings.audio.master01";
        private const string KEY_VFX = "settings.audio.vfx01";
        private const string KEY_MUSIC = "settings.audio.music01";

        public float MouseSensitivity { get; private set; }
        public event Action<float> OnMouseSensitivityChanged;

        public float MasterVolume01 { get; private set; }
        public float VfxVolume01 { get; private set; }
        public float MusicVolume01 { get; private set; }

        public event Action<float> OnMaster01Changed;
        public event Action<float> OnVfx01Changed;
        public event Action<float> OnMusic01Changed;

        public SettingsService(PlayerStatsSO stats, Game.AudioSettingsSO audio)
        {
            _stats = stats;
            _audio = audio;
        }

        public void Initialize()
        {
            var d = _stats.defaultMouseLookSensitivity;
            var min = _stats.minMouseLookSensitivity;
            var max = _stats.maxMouseLookSensitivity;
            var loaded = PlayerPrefs.HasKey(KEY_MOUSE) ? PlayerPrefs.GetFloat(KEY_MOUSE, d) : d;
            MouseSensitivity = Mathf.Clamp(loaded, min, max);

            MasterVolume01 = Load01(KEY_MASTER, InvLerp(_audio.minMainVolumeDb, _audio.maxMainVolumeDb, _audio.defaultMainVolumeDb));
            VfxVolume01 = Load01(KEY_VFX, InvLerp(_audio.minVFXVolumeDb, _audio.maxVFXVolumeDb, _audio.defaultVFXVolumeDb));
            MusicVolume01 = Load01(KEY_MUSIC, InvLerp(_audio.minMusicVolumeDb, _audio.maxMusicVolumeDb, _audio.defaultMusicVolumeDb));

            ApplyMixer(_audio.masterVolumeParam, Lerp(_audio.minMainVolumeDb, _audio.maxMainVolumeDb, MasterVolume01));
            ApplyMixer(_audio.vfxVolumeParam, Lerp(_audio.minVFXVolumeDb, _audio.maxVFXVolumeDb, VfxVolume01));
            ApplyMixer(_audio.musicVolumeParam, Lerp(_audio.minMusicVolumeDb, _audio.maxMusicVolumeDb, MusicVolume01));
        }

        public void SetMouseSensitivity(float value)
        {
            var min = _stats.minMouseLookSensitivity;
            var max = _stats.maxMouseLookSensitivity;
            var v = Mathf.Clamp(value, min, max);
            if (Mathf.Approximately(v, MouseSensitivity)) return;
            MouseSensitivity = v;
            PlayerPrefs.SetFloat(KEY_MOUSE, v);
            PlayerPrefs.Save();
            OnMouseSensitivityChanged?.Invoke(v);
        }

        public void SetMaster01(float value)
        {
            var v = Mathf.Clamp01(value);
            if (Mathf.Approximately(v, MasterVolume01)) return;
            MasterVolume01 = v;
            PlayerPrefs.SetFloat(KEY_MASTER, v);
            PlayerPrefs.Save();
            var db = Lerp(_audio.minMainVolumeDb, _audio.maxMainVolumeDb, v);
            ApplyMixer(_audio.masterVolumeParam, db);
            OnMaster01Changed?.Invoke(v);
        }

        public void SetVfx01(float value)
        {
            var v = Mathf.Clamp01(value);
            if (Mathf.Approximately(v, VfxVolume01)) return;
            VfxVolume01 = v;
            PlayerPrefs.SetFloat(KEY_VFX, v);
            PlayerPrefs.Save();
            var db = Lerp(_audio.minVFXVolumeDb, _audio.maxVFXVolumeDb, v);
            ApplyMixer(_audio.vfxVolumeParam, db);
            OnVfx01Changed?.Invoke(v);
        }

        public void SetMusic01(float value)
        {
            var v = Mathf.Clamp01(value);
            if (Mathf.Approximately(v, MusicVolume01)) return;
            MusicVolume01 = v;
            PlayerPrefs.SetFloat(KEY_MUSIC, v);
            PlayerPrefs.Save();
            var db = Lerp(_audio.minMusicVolumeDb, _audio.maxMusicVolumeDb, v);
            ApplyMixer(_audio.musicVolumeParam, db);
            OnMusic01Changed?.Invoke(v);
        }

        public void Dispose() { }

        private float Load01(string key, float @default)
        {
            var v = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key, @default) : @default;
            return Mathf.Clamp01(v);
        }

        private float Lerp(float a, float b, float t) => a + (b - a) * Mathf.Clamp01(t);
        private float InvLerp(float a, float b, float v) => Mathf.Approximately(a, b) ? 0f : Mathf.Clamp01((v - a) / (b - a));

        private void ApplyMixer(string param, float db)
        {
            if (_audio == null || _audio.audioMixerGroup == null) return;
            var m = _audio.audioMixerGroup.audioMixer;
            if (m == null || string.IsNullOrEmpty(param)) return;
            m.SetFloat(param, db);
        }
    }
}
