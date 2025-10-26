using Fusion;
using Game.Network;
using UnityEngine;

namespace Game
{
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerCameraController _cameraController;

        public override void Spawned()
        {
            if (_cameraController != null)
                _cameraController.SetLocal(Object.HasInputAuthority);
        }
    }
}
