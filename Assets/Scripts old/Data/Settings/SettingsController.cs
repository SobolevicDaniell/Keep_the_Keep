using Game.Settings;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.UI
{
    public sealed class SettingsController : MonoBehaviour
    {
        [SerializeField] private Slider _sensitivitySlider;
        [SerializeField] private Slider _sliderMaster;
        [SerializeField] private Slider _sliderVFX;
        [SerializeField] private Slider _sliderMusic;

        [Inject] private ISettingsService _settings;
        [Inject(Optional = true)] private PlayerStatsSO _stats;

        private bool _bound;

        private void OnEnable()
        {
            BindRanges();
            BindValues();
            BindHandlers();
        }

        private void OnDisable()
        {
            UnbindHandlers();
        }

        private void BindRanges()
        {
            if (_sensitivitySlider && _stats)
            {
                _sensitivitySlider.minValue = _stats.minMouseLookSensitivity;
                _sensitivitySlider.maxValue = _stats.maxMouseLookSensitivity;
                _sensitivitySlider.wholeNumbers = false;
            }
            if (_sliderMaster)
            {
                _sliderMaster.minValue = 0f;
                _sliderMaster.maxValue = 1f;
                _sliderMaster.wholeNumbers = false;
            }
            if (_sliderVFX)
            {
                _sliderVFX.minValue = 0f;
                _sliderVFX.maxValue = 1f;
                _sliderVFX.wholeNumbers = false;
            }
            if (_sliderMusic)
            {
                _sliderMusic.minValue = 0f;
                _sliderMusic.maxValue = 1f;
                _sliderMusic.wholeNumbers = false;
            }
        }

        private void BindValues()
        {
            if (_sensitivitySlider) _sensitivitySlider.SetValueWithoutNotify(_settings.MouseSensitivity);
            if (_sliderMaster) _sliderMaster.SetValueWithoutNotify(_settings.MasterVolume01);
            if (_sliderVFX) _sliderVFX.SetValueWithoutNotify(_settings.VfxVolume01);
            if (_sliderMusic) _sliderMusic.SetValueWithoutNotify(_settings.MusicVolume01);
        }

        private void BindHandlers()
        {
            if (_bound) return;
            if (_sensitivitySlider) _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            if (_sliderMaster) _sliderMaster.onValueChanged.AddListener(OnMasterChanged);
            if (_sliderVFX) _sliderVFX.onValueChanged.AddListener(OnVfxChanged);
            if (_sliderMusic) _sliderMusic.onValueChanged.AddListener(OnMusicChanged);
            _bound = true;
        }

        private void UnbindHandlers()
        {
            if (!_bound) return;
            if (_sensitivitySlider) _sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
            if (_sliderMaster) _sliderMaster.onValueChanged.RemoveListener(OnMasterChanged);
            if (_sliderVFX) _sliderVFX.onValueChanged.RemoveListener(OnVfxChanged);
            if (_sliderMusic) _sliderMusic.onValueChanged.RemoveListener(OnMusicChanged);
            _bound = false;
        }

        private void OnSensitivityChanged(float v)
        {
            _settings.SetMouseSensitivity(v);
        }

        private void OnMasterChanged(float v)
        {
            _settings.SetMaster01(v);
        }

        private void OnVfxChanged(float v)
        {
            _settings.SetVfx01(v);
        }

        private void OnMusicChanged(float v)
        {
            _settings.SetMusic01(v);
        }

        public void RefreshUI()
        {
            BindValues();
        }
    }
}
