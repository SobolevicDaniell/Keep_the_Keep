using Fusion;
using UnityEngine;

namespace Game.Network
{
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class RunnerAutoShutdown : MonoBehaviour
    {
        private NetworkRunner _runner;

        private void Awake() => _runner = GetComponent<NetworkRunner>();

        private async void OnDisable()
        {
            if (_runner == null) return;
            try { if (_runner.IsRunning) await _runner.Shutdown(false); } catch {}
        }

        private async void OnDestroy()
        {
            if (_runner == null) return;
            try { if (_runner.IsRunning) await _runner.Shutdown(false); } catch {}
        }
    }
}
