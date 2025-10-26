using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class InventoryViewService
    {
        private readonly Dictionary<ContainerId, HashSet<PlayerRef>> _viewersByContainer = new();
        private readonly Dictionary<PlayerRef, HashSet<ContainerId>> _containersByViewer = new();

        private readonly NetworkRunner _runner;
        private readonly InventoryContainerRegistry _registry;

        [Inject]
        public InventoryViewService([Inject(Optional = true)] NetworkRunner runner, InventoryContainerRegistry registry)
        {
            _runner = runner;
            _registry = registry;
        }


        public void AddViewer(PlayerRef viewer, ContainerId id)
        {
            if (!_viewersByContainer.TryGetValue(id, out var set))
            {
                set = new HashSet<PlayerRef>();
                _viewersByContainer[id] = set;
            }
            set.Add(viewer);

            if (!_containersByViewer.TryGetValue(viewer, out var cs))
            {
                cs = new HashSet<ContainerId>();
                _containersByViewer[viewer] = cs;
            }
            cs.Add(id);
        }

        public void RemoveViewer(PlayerRef viewer, ContainerId id)
        {
            if (_viewersByContainer.TryGetValue(id, out var set))
            {
                set.RemoveWhere(p => p == viewer);
                if (set.Count == 0) _viewersByContainer.Remove(id);
            }
            if (_containersByViewer.TryGetValue(viewer, out var cs))
            {
                cs.Remove(id);
                if (cs.Count == 0) _containersByViewer.Remove(viewer);
            }
        }

        public void RemoveAllForViewer(PlayerRef viewer)
        {
            if (_containersByViewer.TryGetValue(viewer, out var cs))
            {
                foreach (var id in cs)
                {
                    if (_viewersByContainer.TryGetValue(id, out var vs))
                    {
                        vs.RemoveWhere(p => p == viewer);
                        if (vs.Count == 0) _viewersByContainer.Remove(id);
                    }
                }
                _containersByViewer.Remove(viewer);
            }
        }

        public void BroadcastSnapshot(ContainerId id)
        {
            if (!_registry.TryGet(id, out var c) || c == null) return;
            if (!_viewersByContainer.TryGetValue(id, out var set) || set == null || set.Count == 0) return;

            BuildSnapshotArrays(c.Slots, out var capacity, out var itemIds, out var counts, out var ammo, out var durability);

            int t = (int)id.type;
            var o = id.ownerRef;
            var n = id.objectId;
            int version = c.Version;

            foreach (var v in set)
            {
                var router = GetRouterFor(v);
                if (router != null)
                    router.RPC_PushSnapshot(t, o, n, version, capacity, itemIds, counts, ammo, durability);
            }
        }


        public void BroadcastDelta(ContainerId id, int fromVersion, int toVersion, SlotChange[] changes)
        {
            if (!_viewersByContainer.TryGetValue(id, out var set) || set == null || set.Count == 0) return;

            BuildDeltaArrays(changes, out var indices, out var itemIds, out var counts, out var ammo, out var durability);

            int t = (int)id.type;
            var o = id.ownerRef;
            var n = id.objectId;

            foreach (var v in set)
            {
                var router = GetRouterFor(v);
                if (router != null)
                    router.RPC_PushDelta(t, o, n, fromVersion, toVersion, indices, itemIds, counts, ammo, durability);
            }
        }




        public void SendSnapshotTo(PlayerRef target, ContainerSnapshot snap)
        {
            var router = GetRouterFor(target);
            if (router == null) return;

            BuildSnapshotArrays(snap.slots, out var capacity, out var itemIds, out var counts, out var ammo, out var durability);

            int t = (int)snap.id.type;
            var o = snap.id.ownerRef;
            var n = snap.id.objectId;

            router.RPC_PushSnapshot(t, o, n, snap.version, capacity, itemIds, counts, ammo, durability);
        }


        public void SendDeltaTo(PlayerRef target, ContainerDelta delta)
        {
            var router = GetRouterFor(target);
            if (router == null) return;

            BuildDeltaArrays(delta.changes, out var indices, out var itemIds, out var counts, out var ammo, out var durability);

            int t = (int)delta.id.type;
            var o = delta.id.ownerRef;
            var n = delta.id.objectId;

            router.RPC_PushDelta(t, o, n, delta.fromVersion, delta.toVersion, indices, itemIds, counts, ammo, durability);
        }

        private InventoryRpcRouter GetRouterFor(PlayerRef player)
        {
            if (_runner == null) return null;
            var po = _runner.GetPlayerObject(player);
            if (po == null) return null;
            return po.GetComponentInChildren<InventoryRpcRouter>(true);
        }

        private static InventorySlotState[] CloneSlots(InventorySlotState[] src)
        {
            if (src == null) return null;
            var arr = new InventorySlotState[src.Length];
            for (int i = 0; i < src.Length; i++)
                arr[i] = src[i]?.Clone();
            return arr;
        }

        private static void BuildSnapshotArrays(InventorySlotState[] slots, out int capacity, out string[] itemIds, out int[] counts, out int[] ammo, out int[] durability)
        {
            slots ??= System.Array.Empty<InventorySlotState>();
            capacity = slots.Length;

            if (capacity == 0)
            {
                itemIds = System.Array.Empty<string>();
                counts = System.Array.Empty<int>();
                ammo = System.Array.Empty<int>();
                durability = System.Array.Empty<int>();
                return;
            }

            itemIds = new string[capacity];
            counts = new int[capacity];
            ammo = new int[capacity];
            durability = new int[capacity];

            for (int i = 0; i < capacity; i++)
            {
                var s = slots[i];
                if (s == null)
                {
                    itemIds[i] = string.Empty;
                    counts[i] = 0;
                    ammo[i] = 0;
                    durability[i] = 0;
                    continue;
                }

                itemIds[i] = s.itemId ?? string.Empty;
                counts[i] = s.count;
                var st = s.itemState;
                ammo[i] = st != null ? st.ammo : 0;
                durability[i] = st != null ? st.durability : 0;
            }
        }

        private static void BuildDeltaArrays(SlotChange[] changes, out int[] indices, out string[] itemIds, out int[] counts, out int[] ammo, out int[] durability)
        {
            changes ??= System.Array.Empty<SlotChange>();
            int n = changes.Length;

            indices = new int[n];
            itemIds = new string[n];
            counts = new int[n];
            ammo = new int[n];
            durability = new int[n];

            for (int k = 0; k < n; k++)
            {
                indices[k] = changes[k].index;

                var s = changes[k].state;
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

    }
}