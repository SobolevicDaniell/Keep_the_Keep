using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.UI
{
    public sealed class UIHealthView : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [Inject(Optional = true)] private HealthClientModel _model;

        private void OnEnable()
        {
            if (_model == null || _slider == null) return;
            _model.OnChanged += OnChanged;
            OnChanged(_model.Current, _model.Max);
        }

        private void OnDisable()
        {
            if (_model == null) return;
            _model.OnChanged -= OnChanged;
        }

        private void OnChanged(int cur, int max)
        {
            _slider.maxValue = max;
            _slider.value    = cur;
        }
    }
}
