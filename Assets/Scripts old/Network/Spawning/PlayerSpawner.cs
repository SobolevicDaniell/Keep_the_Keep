using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public class PlayerSpawner
    {
        private readonly IPlayerFactory _factory;
        private readonly GameObject _deathBoxPrefab;
        private readonly GameObject _playerObjectPrefab;
        private readonly DiContainer _container;

        [Inject] private InventoryServerService _inventoryServer;
        [Inject] private InventoryViewService _views;
        [Inject] private InventoryContainerRegistry _registry;
        [Inject] private ISpawnPointProvider _spawnPoints;


        [Networked] public int SlotsCapacity { get; private set; }
        [Networked] public Vector3 SpawnPos { get; private set; }
        [Networked] public Quaternion SpawnRot { get; private set; }


        private readonly Dictionary<PlayerRef, NetworkObject> _avatars = new();
        private readonly Dictionary<PlayerRef, NetworkObject> _playerObjects = new();

        public PlayerSpawner(
            IPlayerFactory factory,
            [Inject(Id = "DeathBoxPrefab")] GameObject deathBoxPrefab,
            [Inject(Id = "PlayerObjectPrefab")] GameObject playerObjectPrefab,
            DiContainer container)
        {
            _factory = factory;
            _deathBoxPrefab = deathBoxPrefab;
            _playerObjectPrefab = playerObjectPrefab;
            _container = container;
        }

        public void SpawnAvatar(NetworkRunner runner, PlayerRef player)
        {
            if (_avatars.TryGetValue(player, out var existing) && existing != null) return;
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            if (_spawnPoints != null)
                _spawnPoints.TryGetNext(out pos, out rot);
            var no = _factory.Spawn(player, pos, rot);
            _avatars[player] = no;
            var po = runner.GetPlayerObject(player);
            if (po != null)
            {
                var proxy = po.GetComponent<PlayerObject>();
                proxy.RPC_ShowGameplay();
            }
        }

        public void DespawnAvatar(NetworkRunner runner, PlayerRef player, NetworkObject fallbackAvatar)
        {
            if (!runner.IsServer) return;
            if (_avatars.TryGetValue(player, out var no) && no != null && no.Runner == runner)
            {
                runner.Despawn(no);
                _avatars[player] = null;
                return;
            }
            if (fallbackAvatar != null && fallbackAvatar.Runner == runner)
            {
                runner.Despawn(fallbackAvatar);
            }
            _avatars[player] = null;
        }

        public void EnsurePlayerObject(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            if (_playerObjects.TryGetValue(player, out var existing) && existing != null) return;

            var poExisting = runner.GetPlayerObject(player);
            if (poExisting != null)
            {
                _playerObjects[player] = poExisting;
                return;
            }

            NetworkObject po = null;
            runner.Spawn(
                _playerObjectPrefab.GetComponent<NetworkObject>(),
                Vector3.zero,
                Quaternion.identity,
                player,
                (r, obj) =>
                {
                    _container.InjectGameObject(obj.gameObject);
                    po = obj;
                }
            );
            if (po != null)
            {
                runner.SetPlayerObject(player, po);
                _playerObjects[player] = po;
            }
        }

        public void RemovePlayerObject(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            var po = runner.GetPlayerObject(player);
            if (po != null) runner.Despawn(po);
            _playerObjects[player] = null;
        }

        public NetworkObject SpawnDeathBox(NetworkRunner runner, PlayerRef owner, Vector3 position, Quaternion rotation)
        {
            if (!runner.IsServer) return null;
            NetworkObject spawned = null;
            runner.Spawn(
                _deathBoxPrefab.GetComponent<NetworkObject>(),
                position,
                rotation,
                PlayerRef.None,
                (r, obj) =>
                {
                    _container.InjectGameObject(obj.gameObject);
                    obj.transform.SetPositionAndRotation(position, rotation);
                    var marker = obj.GetComponent<DeathMarker>();
                    if (marker != null) marker.Initialize(owner);
                    var spatial = obj.GetComponent<CorpseSpatial>();
                    if (spatial != null) spatial.ServerSetSpawnPose(position, rotation);
                    var corpse = obj.GetComponent<CorpseInventoryServer>();
                    spawned = obj;
                });
            return spawned;
        }

        public void RegisterAvatar(PlayerRef player, GameObject avatarGo)
        {
            var no = avatarGo != null ? avatarGo.GetComponent<NetworkObject>() : null;
            if (no == null) return;
            _avatars[player] = no;
        }

        public bool IsAvatarSpawned(PlayerRef player)
        {
            return _avatars.TryGetValue(player, out var no) && no != null;
        }

        public void RespawnPlayer(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;

            EnsurePlayerObject(runner, player);

            if (_inventoryServer != null)
            {
                _inventoryServer.ServerClearPlayerContainers(player, out var deltas);

                var routerOwner = runner.TryGetPlayerObject(player, out var po)
                    ? po.GetComponent<InventoryRpcRouter>()
                    : null;

                if (routerOwner != null && deltas != null)
                {
                    for (int i = 0; i < deltas.Count; i++)
                        routerOwner.BroadcastDeltaFromServer(deltas[i]);
                }
            }

            if (!IsAvatarSpawned(player))
                SpawnAvatar(runner, player);

            _views?.RemoveAllForViewer(player);

            ServerPostRespawnSync(runner, player);
        }

        private void ServerPostRespawnSync(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.TryGetPlayerObject(player, out var po) || po == null) return;

            var viewer = player;

            var quick = new ContainerId { type = ContainerType.PlayerQuick, ownerRef = viewer, objectId = default };
            var main = new ContainerId { type = ContainerType.PlayerMain, ownerRef = viewer, objectId = default };

            _views.AddViewer(viewer, quick);
            _views.AddViewer(viewer, main);

            if (_registry != null && _registry.TryGet(quick, out var cq) && cq != null)
            {
                _views.SendSnapshotTo(viewer, new ContainerSnapshot
                {
                    id = quick,
                    version = cq.Version,
                    slots = CloneSlots(cq.Slots)
                });
            }

            if (_registry != null && _registry.TryGet(main, out var cm) && cm != null)
            {
                _views.SendSnapshotTo(viewer, new ContainerSnapshot
                {
                    id = main,
                    version = cm.Version,
                    slots = CloneSlots(cm.Slots)
                });
            }
        }

        private static InventorySlotState[] CloneSlots(InventorySlotState[] src)
        {
            if (src == null) return null;
            var arr = new InventorySlotState[src.Length];
            for (int i = 0; i < src.Length; i++)
                arr[i] = src[i]?.Clone();
            return arr;
        }
       

    }
}