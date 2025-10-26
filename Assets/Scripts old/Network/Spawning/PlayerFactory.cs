using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public sealed class PlayerFactory : IPlayerFactory
    {
        private readonly NetworkRunner _runner;
        private readonly NetworkObject _avatarPrefab;
        private readonly DiContainer _container;

        [Inject]
        public PlayerFactory(
            NetworkRunner runner,
            [Inject(Id = "AvatarPrefab")] NetworkObject avatarPrefab,
            DiContainer container)
        {
            _runner = runner;
            _avatarPrefab = avatarPrefab;
            _container = container;
        }

        public NetworkObject Spawn(PlayerRef playerRef)
        {
            return Spawn(playerRef, Vector3.zero, Quaternion.identity);
        }

        public NetworkObject Spawn(PlayerRef playerRef, Vector3 position, Quaternion rotation)
        {
            NetworkObject spawned = null;
            _runner.Spawn(
                _avatarPrefab,
                position,
                rotation,
                inputAuthority: playerRef,
                onBeforeSpawned: (r, obj) =>
                {
                    _container.InjectGameObject(obj.gameObject);
                    spawned = obj;
                });
            return spawned;
        }
    }
}
