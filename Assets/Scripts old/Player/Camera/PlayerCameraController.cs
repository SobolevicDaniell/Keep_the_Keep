using Fusion;
using UnityEngine;
using Zenject;
using Game.UI;

namespace Game.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerCameraController : NetworkBehaviour
    {
        [Header("Local Player Camera & Audio")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private AudioListener _audioListener;
        [SerializeField] private GameObject faceObject;

        [Inject(Optional = true)] private UIController _ui;

        private bool _isLocal;
        private bool _suppressForExit;

        public void SetLocal(bool isLocal)
        {
            _isLocal = isLocal;
            ApplyCameras();
        }

        public override void Spawned()
        {
            if (Object.HasInputAuthority)
            {
                _isLocal = true;
                if (faceObject != null) faceObject.SetActive(false);
            }

            if (_ui != null) _ui.OnExitActivated += OnExitActivated;

            ApplyCameras();
        }

        private void OnDestroy()
        {
            if (_ui != null) _ui.OnExitActivated -= OnExitActivated;
        }

        private void OnExitActivated()
        {
            _suppressForExit = true;
            ApplyCameras();
        }

        private void ApplyCameras()
        {
            bool enableLocal = _isLocal && !_suppressForExit;

            _playerCamera.gameObject.SetActive(true);
            _playerCamera.enabled = enableLocal;
            

            if (_audioListener != null)
            {
                _audioListener.enabled = enableLocal;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && _isLocal) ApplyCameras();
        }
    }
}
