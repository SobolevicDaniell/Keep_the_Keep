using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Game
{
    public sealed class InventoryClientModel
    {
        private readonly Dictionary<ContainerId, ContainerSnapshot> _snapshots = new();
        private readonly Dictionary<int, Action<bool, string>> _pending = new();

        public event Action<ContainerId> OnContainerChanged;

        public void ApplySnapshot(ContainerSnapshot snap)
        {
            _snapshots[snap.id] = snap;
            OnContainerChanged?.Invoke(snap.id);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[INV][Client] ApplySnapshot id=({snap.id.type},{snap.id.ownerRef}), ver={snap.version}, slots={snap.slots?.Length ?? 0}, nonEmpty={CountNonEmpty(snap)}");
#endif
        }
        private int CountNonEmpty(ContainerSnapshot snap)
        {
            if (snap.slots == null) return 0;
            int n = 0;
            for (int i = 0; i < snap.slots.Length; i++)
                if (InventorySlotStateAccessor.ReadId(snap.slots[i]) != null) n++;
            return n;
        }

        public void ApplyDelta(ContainerDelta delta)
        {
            if (!_snapshots.TryGetValue(delta.id, out var s)) return;
            if (delta.toVersion <= s.version) return;

            foreach (var ch in delta.changes)
            {
                if (ch.index < 0 || ch.index >= s.slots.Length) continue;
                s.slots[ch.index] = ch.state?.Clone();
            }
            s.version = delta.toVersion;
            OnContainerChanged?.Invoke(delta.id);
        }

        public ContainerSnapshot Get(ContainerId id)
        {
            if (_snapshots.TryGetValue(id, out var s)) return s;
            return null;
        }

        public void TrackOperation(int clientReqId, Action<bool, string> onAck)
        {
            _pending[clientReqId] = onAck;
        }

        public void AckOperation(int clientReqId, bool ok, string message)
        {
            if (_pending.TryGetValue(clientReqId, out var cb))
            {
                _pending.Remove(clientReqId);
                cb?.Invoke(ok, message);
            }
        }
        public bool TryResolveExistingId(ContainerId probe, out ContainerId resolved)
        {
            if (_snapshots.ContainsKey(probe))
            {
                resolved = probe;
                return true;
            }

            foreach (var kv in _snapshots)
            {
                var key = kv.Key;
                if (key.type != probe.type) continue;

                bool objMatch = !Equals(probe.objectId, default(NetworkId)) && Equals(key.objectId, probe.objectId);
                if (objMatch) { resolved = key; return true; }

                bool ownerMatch = !Equals(probe.ownerRef, default(PlayerRef)) && Equals(key.ownerRef, probe.ownerRef);
                if (ownerMatch) { resolved = key; return true; }
            }

            resolved = default;
            return false;
        }

    }
}