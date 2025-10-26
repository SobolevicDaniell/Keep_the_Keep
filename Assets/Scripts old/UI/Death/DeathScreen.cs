using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.UI
{
    public sealed class DeathScreen : MonoBehaviour
    {
        [SerializeField] private Button _respawnButton;
        [SerializeField] private float _cameraHeight = 6f;

        [Inject(Optional = true)] private HealthClientModel _model;

        private void OnEnable()
        {
            if (_model == null) return;
            _model.OnChanged += OnChanged;
            OnChanged(_model.Current, _model.Max);
            if (_respawnButton != null)
                _respawnButton.onClick.AddListener(OnRespawnClicked);
        }

        private void OnDisable()
        {
            if (_model != null)
                _model.OnChanged -= OnChanged;
            if (_respawnButton != null)
                _respawnButton.onClick.RemoveListener(OnRespawnClicked);
        }

        private void OnChanged(int cur, int max)
        {
           
        }

        private void OnRespawnClicked()
        {
            var proxy = FindObjectsOfType<PlayerObject>()
                        .FirstOrDefault(p => p.Object != null && p.Object.HasInputAuthority);
            if (proxy != null)
                proxy.RPC_RequestRespawn();
        }
    }
}
