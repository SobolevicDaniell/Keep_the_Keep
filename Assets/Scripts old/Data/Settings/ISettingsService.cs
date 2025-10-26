using System;

namespace Game.Settings
{
    public interface ISettingsService
    {
        float MouseSensitivity { get; }
        void SetMouseSensitivity(float value);
        event Action<float> OnMouseSensitivityChanged;

        float MasterVolume01 { get; }
        float VfxVolume01 { get; }
        float MusicVolume01 { get; }

        void SetMaster01(float value);
        void SetVfx01(float value);
        void SetMusic01(float value);

        event Action<float> OnMaster01Changed;
        event Action<float> OnVfx01Changed;
        event Action<float> OnMusic01Changed;
    }
}
