using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class InventoryRpcRouter : NetworkBehaviour
    {
        private static readonly Dictionary<PlayerRef, InventoryRpcRouter> _byPlayer = new();
        private static readonly Dictionary<(int type, PlayerRef owner, NetworkId obj), List<PlayerRef>> _pendingOpenByContainer = new();



        [Inject] private InventoryContainerRegistry _registry;
        [Inject] private InventoryViewService _views;
        [Inject(Optional = true)] private InventoryServerService _server;
        [Inject(Optional = true)] private InventorySessionServer _session;
        [Inject(Optional = true)] private InventorySnapshotBuilder _snapshots;
        [Inject(Optional = true)] private InventoryClientFacade _clientFacade;


        // На клиенте
        [Inject(Optional = true)] private InventoryClientModel _clientModel;
        [Inject(Optional = true)] private InventoryService _clientService;

        private bool _lastOk;


        public override void Spawned()
        {
            var key = Object != null ? Object.InputAuthority : PlayerRef.None;
            if (key != PlayerRef.None)
                _byPlayer[key] = this;

            if (Object != null && Object.HasInputAuthority)
            {
                if (_clientFacade != null)
                    _clientFacade.SetLocal(Object.InputAuthority, this);


                StartCoroutine(RetryFullResync());
            }
        }




        public IEnumerator RetryFullResync()
        {
            while (Object == null || !Object.HasInputAuthority)
                yield return null;

            RPC_RequestFullResync();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestFullResync(RpcInfo info = default)
        {
            var viewer = info.Source;
            if (viewer == PlayerRef.None) viewer = Object.InputAuthority;

            var quick = new ContainerId { type = ContainerType.PlayerQuick, ownerRef = viewer, objectId = default };
            var main = new ContainerId { type = ContainerType.PlayerMain, ownerRef = viewer, objectId = default };

            if (_registry.TryGet(quick, out var cq) && cq != null)
            {
                _views.AddViewer(viewer, quick);
                _views.SendSnapshotTo(viewer, new ContainerSnapshot
                {
                    id = quick,
                    version = cq.Version,
                    slots = CloneSlots(cq.Slots)
                });
            }

            if (_registry.TryGet(main, out var cm) && cm != null)
            {
                _views.AddViewer(viewer, main);
                _views.SendSnapshotTo(viewer, new ContainerSnapshot
                {
                    id = main,
                    version = cm.Version,
                    slots = CloneSlots(cm.Slots)
                });
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            var key = Object != null ? Object.InputAuthority : PlayerRef.None;
            if (key != PlayerRef.None)
                _byPlayer.Remove(key);
        }

        [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestOpenContainer(int type, PlayerRef ownerRef, NetworkId objectId, RpcInfo info = default)
        {
            var viewer = info.Source;
            if (viewer == PlayerRef.None)
            {
                var lp = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
                viewer = lp != PlayerRef.None ? lp : Object.InputAuthority;
            }

            var raw = new ContainerId { type = (ContainerType)type, ownerRef = ownerRef, objectId = objectId };
            var id = NormalizeOwnedId(raw, viewer);
            var k = ((int)id.type, id.ownerRef, id.objectId);

            if (!_registry.TryGet(id, out var c) || c == null)
            {
                if (!_pendingOpenByContainer.TryGetValue(k, out var list))
                {
                    list = new List<PlayerRef>(2);
                    _pendingOpenByContainer[k] = list;
                }
                if (!list.Contains(viewer)) list.Add(viewer);
                return;
            }

            if (!c.CanPlayerAccess(viewer)) return;

            _views.AddViewer(viewer, id);
            _views.SendSnapshotTo(viewer, new ContainerSnapshot
            {
                id = id,
                version = c.Version,
                slots = CloneSlots(c.Slots)
            });
        }


       

        public IEnumerator RetryOpenContainer(int type, PlayerRef ownerRef, NetworkId objectId)
        {
            while (Object == null || !Object.HasInputAuthority)
                yield return null;

            RPC_RequestOpenContainer(type, ownerRef, objectId);
        }



        [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestCloseContainer(int type, PlayerRef ownerRef, NetworkId objectId, RpcInfo info = default)
        {
            var viewer = info.Source;
            if (viewer == PlayerRef.None)
            {
                var lp = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
                viewer = lp != PlayerRef.None ? lp : Object.InputAuthority;
            }

            var id = NormalizeOwnedId(new ContainerId
            {
                type = (ContainerType)type,
                ownerRef = ownerRef,
                objectId = objectId
            }, viewer);

            _views.RemoveViewer(viewer, id);
        }

        [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestTransfer(
    int fromType, PlayerRef fromOwner, NetworkId fromObjectId, int fromIdx,
    int toType, PlayerRef toOwner, NetworkId toObjectId, int toIdx,
    int amount, int clientReqId, RpcInfo info = default)
        {
            if (_server == null) { RPC_OpAck(clientReqId, false, "no_server"); return; }

            var actor = info.Source;
            if (actor == PlayerRef.None)
            {
                var lp = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
                actor = lp != PlayerRef.None ? lp : Object.InputAuthority;
            }

            var fromId = NormalizeOwnedId(DecodeId(fromType, fromOwner, fromObjectId), actor);
            var toId = NormalizeOwnedId(DecodeId(toType, toOwner, toObjectId), actor);

            if (!_server.TryTransfer(actor, fromId, fromIdx, toId, toIdx, amount,
                                     out var fromDelta, out var toDelta, out var swapped, out var reason))
            {
                RPC_OpAck(clientReqId, false, reason ?? "denied");
                return;
            }

            RPC_OpAck(clientReqId, true, "ok");

            if (fromDelta != null) BroadcastDeltaFromServer(fromDelta);
            if (toDelta != null) BroadcastDeltaFromServer(toDelta);

            var rpc = GetComponent<PlayerRpcHandler>();
            if (rpc != null) rpc.ServerRefreshHandsFromSelectedQuick(actor);
        }


        private ContainerId NormalizeOwnedId(ContainerId id, PlayerRef viewer)
        {
            switch (id.type)
            {
                case ContainerType.PlayerQuick:
                case ContainerType.PlayerMain:
                    if (id.ownerRef == PlayerRef.None) id.ownerRef = viewer;
                    id.objectId = default;
                    return id;

                case ContainerType.Corpse:
                    id.ownerRef = PlayerRef.None;
                    return id;

                default:
                    return id;
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestPickup(string itemId, int amount, int ammo, int clientReqId, RpcInfo info = default)
        {
            if (_server == null) { RPC_OpAck(clientReqId, false, "no_server"); return; }

            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;

            var ok = _server.TryAddItemToPlayer(actor, itemId, amount, ammo, out var left, out var deltas, out var reason);

            var rpc = GetComponent<PlayerRpcHandler>();
            if (rpc != null) rpc.ServerRefreshHandsFromSelectedQuick(actor);

            if (left > 0 && rpc != null) rpc.ServerDropOverflow(itemId, left, ammo);

            RPC_OpAck(clientReqId, ok && left == 0, reason ?? ((ok && left == 0) ? "ok" : "not_enough_space"));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        public void RPC_PushSnapshot(
    int type, PlayerRef owner, NetworkId objectId, int version,
    int capacity, string[] itemIds, int[] counts, int[] ammo, int[] durability, RpcInfo info = default)
        {
            var id = DecodeId(type, owner, objectId);

            var slots = new InventorySlotState[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var idStr = (itemIds != null && i < itemIds.Length) ? itemIds[i] : null;
                var cnt = (counts != null && i < counts.Length) ? counts[i] : 0;
                var amm = (ammo != null && i < ammo.Length) ? ammo[i] : 0;
                var dur = (durability != null && i < durability.Length) ? durability[i] : 0;

                slots[i] = (string.IsNullOrEmpty(idStr) || cnt <= 0)
                    ? null
                    : new InventorySlotState { itemId = idStr, count = cnt, itemState = new ItemState(amm, dur) };
            }

            var snap = new ContainerSnapshot { id = id, version = version, slots = slots };

            if (_clientService != null) _clientService.ApplySnapshot(snap);
            if (_clientModel != null) _clientModel.ApplySnapshot(snap);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        public void RPC_PushDelta(
    int type, PlayerRef owner, NetworkId objectId,
    int fromVersion, int toVersion,
    int[] indices, string[] itemIds, int[] counts, int[] ammo, int[] durability, RpcInfo info = default)
        {
            var id = new ContainerId { type = (ContainerType)type, ownerRef = owner, objectId = objectId };
            int n = indices != null ? indices.Length : 0;
            var changes = new SlotChange[n];

            for (int k = 0; k < n; k++)
            {
                int idx = indices[k];
                string idStr = (itemIds != null && k < itemIds.Length) ? itemIds[k] : null;
                int cnt = (counts != null && k < counts.Length) ? counts[k] : 0;
                int amm = (ammo != null && k < ammo.Length) ? ammo[k] : 0;
                int dur = (durability != null && k < durability.Length) ? durability[k] : 0;

                InventorySlotState state = (string.IsNullOrEmpty(idStr) || cnt <= 0)
                    ? null
                    : new InventorySlotState { itemId = idStr, count = cnt, itemState = new ItemState(amm, dur) };

                changes[k] = new SlotChange { index = idx, state = state };
            }

            var delta = new ContainerDelta
            {
                id = id,
                fromVersion = fromVersion,
                toVersion = toVersion,
                changes = changes
            };

            _clientService?.ApplyDelta(delta);
            _clientModel?.ApplyDelta(delta);
        }



        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_OpAck(int clientReqId, bool ok, string message, RpcInfo info = default)
        {
            _clientModel?.AckOperation(clientReqId, ok, message);
        }
        


        

        

        private static void BuildDeltaArrays(ContainerDelta delta,
                                             out int[] indices, out string[] itemIds, out int[] counts, out int[] ammo, out int[] durability)
        {
            var ch = delta.changes ?? System.Array.Empty<SlotChange>();
            int n = ch.Length;

            indices = new int[n];
            itemIds = new string[n];
            counts = new int[n];
            ammo = new int[n];
            durability = new int[n];

            for (int k = 0; k < n; k++)
            {
                indices[k] = ch[k].index;

                var s = ch[k].state;
                if (s == null || s.IsEmpty)
                {
                    itemIds[k] = string.Empty;
                    counts[k] = 0;
                    ammo[k] = 0;
                    durability[k] = 0;
                }
                else
                {
                    itemIds[k] = s.itemId ?? string.Empty;
                    counts[k] = s.count;
                    ammo[k] = s.itemState != null ? s.itemState.ammo : 0;
                    durability[k] = s.itemState != null ? s.itemState.durability : 0;
                }
            }
        }

        

        private static ContainerId DecodeId(int type, PlayerRef owner, NetworkId objectId)
        {
            return new ContainerId { type = (ContainerType)type, ownerRef = owner, objectId = objectId };
        }
        public void BroadcastDeltaFromServer(ContainerDelta delta)
        {
            if (delta == null) return;

            BuildDeltaArrays(delta,
                out var indices, out var itemIds, out var counts, out var ammo, out var durability);

            var t = (int)delta.id.type;
            var o = delta.id.ownerRef;
            var n = delta.id.objectId;

            if (_server != null)
            {
                foreach (var watcher in _server.Watchers(delta.id))
                {
                    if (_byPlayer.TryGetValue(watcher, out var router) && router != null)
                    {
                        router.RPC_PushDelta(
                            t, o, n,
                            delta.fromVersion, delta.toVersion,
                            indices, itemIds, counts, ammo, durability
                        );
                    }
                }
            }

            var owner = delta.id.ownerRef;
            if (_byPlayer.TryGetValue(owner, out var ownerRouter) && ownerRouter != null)
            {
                ownerRouter.RPC_PushDelta(
                    t, o, n,
                    delta.fromVersion, delta.toVersion,
                    indices, itemIds, counts, ammo, durability
                );
            }
        }



        public static void ServerNotifyContainerRegistered(ContainerId id)
        {
            var k = ((int)id.type, id.ownerRef, id.objectId);
            if (!_pendingOpenByContainer.TryGetValue(k, out var viewers) || viewers == null || viewers.Count == 0) return;

            for (int i = 0; i < viewers.Count; i++)
            {
                var viewer = viewers[i];
                if (!_byPlayer.TryGetValue(viewer, out var router) || router == null || router.Runner == null) continue;
                if (router._registry == null || router._views == null) continue;
                if (!router._registry.TryGet(id, out var c) || c == null) continue;
                if (!c.CanPlayerAccess(viewer)) continue;

                router._views.AddViewer(viewer, id);
                router._views.SendSnapshotTo(viewer, new ContainerSnapshot
                {
                    id = id,
                    version = c.Version,
                    slots = CloneSlots(c.Slots)
                });
            }

            _pendingOpenByContainer.Remove(k);
        }


        public static void ServerNotifyContainerUnregistered(ContainerId id)
        {
            var k = ((int)id.type, id.ownerRef, id.objectId);
            _pendingOpenByContainer.Remove(k);
        }


        private static InventorySlotState[] CloneSlots(InventorySlotState[] src)
        {
            if (src == null) return null;
            var n = src.Length;
            var dst = new InventorySlotState[n];
            for (int i = 0; i < n; i++) dst[i] = src[i]?.Clone();
            return dst;
        }






    }
}