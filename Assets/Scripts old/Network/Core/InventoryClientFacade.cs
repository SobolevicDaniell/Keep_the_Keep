using System;
using System.Collections;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed partial class InventoryClientFacade : MonoBehaviour
    {

        [Inject(Optional = true)] private NetworkRunner _runner;
        public event Action<ContainerId> OnContainerChanged;

        private PlayerRef _local;
        private InventoryRpcRouter _localRouter;
        private NetworkId _localPlayerObjectId;
        private PlayerRpcHandler _localRpc;

        [Inject(Optional = true)] private InventoryClientModel _clientModel;

        private int _clientReqSeq;
        private int _req;

        private void OnEnable()
        {
            if (_clientModel != null)
                _clientModel.OnContainerChanged += HandleModelContainerChanged;
        }

        private void OnDisable()
        {
            if (_clientModel != null)
                _clientModel.OnContainerChanged -= HandleModelContainerChanged;
        }

        public void SetLocal(PlayerRef local, InventoryRpcRouter router)
        {
            _local = local;
            _localRouter = router;
            _localRpc = router != null
                ? (router.GetComponent<PlayerRpcHandler>() ?? router.GetComponentInChildren<PlayerRpcHandler>(true))
                : null;
        }

        public void SetLocal(PlayerRef localPlayer, InventoryRpcRouter router, NetworkId localPlayerObjectId)
        {
            _local = localPlayer;
            _localRouter = router;
            _localPlayerObjectId = localPlayerObjectId;
            _localRpc = router != null
                ? (router.GetComponent<PlayerRpcHandler>() ?? router.GetComponentInChildren<PlayerRpcHandler>(true))
                : null;
        }

        public void OpenLocalQuick()
        {
            if (_localRouter == null) return;
            StartCoroutine(_localRouter.RetryOpenContainer((int)ContainerType.PlayerQuick, _local, _localPlayerObjectId));
        }

        public void OpenLocalMain()
        {
            if (_localRouter == null) return;
            StartCoroutine(_localRouter.RetryOpenContainer((int)ContainerType.PlayerMain, _local, _localPlayerObjectId));
        }

        public void Open(ContainerId id)
        {
            var r = GetLocalRouter(); if (r == null) return;
            var t = (int)id.type; var o = id.ownerRef; var n = id.objectId;
            r.RPC_RequestOpenContainer(t, o, n);
        }

        public void Close(ContainerId id)
        {
            var r = GetLocalRouter(); if (r == null) return;
            var t = (int)id.type; var o = id.ownerRef; var n = id.objectId;
            r.RPC_RequestCloseContainer(t, o, n);
        }

        public bool TryGetSnapshot(ContainerId id, out int version, out InventorySlotState[] slots)
        {
            version = 0;
            slots = null;
            if (_clientModel == null) return false;

            var snap = _clientModel.Get(id);
            if (snap == null || snap.slots == null) return false;

            version = snap.version;
            slots = snap.slots;
            return true;
        }

        public bool TryGetSnapshotResolved(ContainerId probe, out ContainerId resolvedId, out int version, out InventorySlotState[] slots)
        {
            resolvedId = probe;
            if (TryGetSnapshot(probe, out version, out slots)) return true;

            version = 0;
            slots = null;
            if (_clientModel != null && _clientModel.TryResolveExistingId(probe, out var rId))
            {
                if (TryGetSnapshot(rId, out version, out slots))
                {
                    resolvedId = rId;
                    return true;
                }
            }
            return false;
        }

        public int GetCapacityImmediate(ContainerId id)
        {
            if (TryGetSnapshot(id, out _, out var slots) && slots != null)
                return slots.Length;

            if (_runner == null || id.objectId == default) return 0;

            var no = _runner.FindObject(id.objectId);
            if (no == null) return 0;

            var corpse = no.GetComponent<CorpseInventoryServer>();
            if (corpse != null) return Mathf.Max(0, corpse.SlotsCapacity);

            var chest = no.GetComponent<ChestInventoryServer>();
            if (chest != null) return Mathf.Max(0, chest.SlotsCapacity);

            return 0;
        }

        public int GetLocalQuickCapacity()
        {
            var id = new ContainerId { type = ContainerType.PlayerQuick, ownerRef = _local, objectId = default };
            return TryGetSnapshot(id, out _, out var slots) ? (slots?.Length ?? 0) : 0;
        }

        public int GetLocalMainCapacity()
        {
            var id = new ContainerId { type = ContainerType.PlayerMain, ownerRef = _local, objectId = default };
            return TryGetSnapshot(id, out _, out var slots) ? (slots?.Length ?? 0) : 0;
        }

        public ContainerId localQuick => new ContainerId { type = ContainerType.PlayerQuick, ownerRef = _local, objectId = default };
        public ContainerId localMain => new ContainerId { type = ContainerType.PlayerMain, ownerRef = _local, objectId = default };

        public void Transfer(ContainerId fromId, int fromIdx, ContainerId toId, int toIdx, int amount, System.Action<bool, string> cb = null)
        {
            var r = GetLocalRouter(); if (r == null) { cb?.Invoke(false, "no_router"); return; }
            r.RPC_RequestTransfer(
                (int)fromId.type, fromId.ownerRef, fromId.objectId, fromIdx,
                (int)toId.type, toId.ownerRef, toId.objectId, toIdx,
                amount, 0);
            cb?.Invoke(true, "sent");
        }

        private void HandleModelContainerChanged(ContainerId id)
        {
            OnContainerChanged?.Invoke(id);
        }
        private InventoryRpcRouter GetLocalRouter()
        {
            if (_localRouter != null && _localRouter.Object != null && _localRouter.Object.HasInputAuthority)
                return _localRouter;

            var runner = _localRouter != null ? _localRouter.Runner : null;
            if (runner != null && runner.TryGetPlayerObject(runner.LocalPlayer, out var po) && po != null)
            {
                _localRouter = po.GetComponent<InventoryRpcRouter>();
                if (_localRouter != null)
                    _localRpc = po.GetComponent<PlayerRpcHandler>() ?? po.GetComponentInChildren<PlayerRpcHandler>(true);
            }
            return _localRouter;
        }
        private PlayerRpcHandler GetLocalRpc()
        {
            if (_localRpc != null && _localRpc.Object != null && _localRpc.Object.HasInputAuthority)
                return _localRpc;

            var r = GetLocalRouter();
            _localRpc = r != null
                ? (r.GetComponent<PlayerRpcHandler>() ?? r.GetComponentInChildren<PlayerRpcHandler>(true))
                : null;
            return _localRpc;
        }
        public void RequestDropFromContainer(Vector3 pos, Vector3 dir, int type, int slotIndex, byte flags, PlayerRef ownerRef, NetworkId objectId)
        {
            var rpc = GetLocalRpc(); if (rpc == null) return;
            rpc.RPC_RequestDropFromContainer(pos, dir, type, slotIndex, flags, ownerRef, objectId);
        }
        
        
    }
}
