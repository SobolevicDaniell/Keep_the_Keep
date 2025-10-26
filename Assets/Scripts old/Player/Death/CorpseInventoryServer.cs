using System.Runtime.InteropServices;
using Fusion;
using Game.UI;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class CorpseInventoryServer : NetworkBehaviour, IInventoryContainer
    {
        private InventorySlotState[] _slots;
        private int _version;

        [Inject(Optional = true)] private InventoryContainerRegistry _registry;
        [Inject] private UIController _uiController;

        [Networked] public int SlotsCapacity { get; private set; }

        public ContainerId Id { get; private set; }
        public int Version => _version;
        public int Capacity => _slots != null ? _slots.Length : 0;
        public InventorySlotState[] Slots => _slots;

        private int _nonEmptyCount;

        public override void Spawned()
        {
            Id = ContainerId.OfObject(ContainerType.Corpse, Object.Id);
            if (Object.HasStateAuthority)
            {
                if (_slots == null) _slots = System.Array.Empty<InventorySlotState>();
                _version = 0;
                _nonEmptyCount = 0;
                for (int i = 0; i < _slots.Length; i++) if (IsNonEmpty(_slots[i])) _nonEmptyCount++;
                SlotsCapacity = Capacity;
                _registry?.Register(this);
                InventoryRpcRouter.ServerNotifyContainerRegistered(Id);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (!Object.HasStateAuthority) return;
            _registry?.Unregister(Id);
            InventoryRpcRouter.ServerNotifyContainerUnregistered(Id);
        }

        public void ServerInit(int size)
        {
            if (!Object.HasStateAuthority) return;
            size = Mathf.Max(0, size);
            _slots = new InventorySlotState[size];
            _version = 0;
            _nonEmptyCount = 0;
            SlotsCapacity = size;
        }

        private static bool IsNonEmpty(InventorySlotState s)
        {
            if (s == null) return false;
            var id = InventorySlotStateAccessor.ReadId(s);
            var cnt = InventorySlotStateAccessor.ReadCount(s);
            return !string.IsNullOrEmpty(id) && cnt > 0;
        }

        private void TryDespawnWhenEmpty()
        {
            if (!Object.HasStateAuthority) return;
            if (Runner == null) return;
            if (_nonEmptyCount <= 0)
            {
                Runner.Despawn(Object);
                if (_uiController.Phase != UiPhase.Spawn)
                {
                    _uiController.SetPhase(UiPhase.Inventory);
                }
            }
        }

      

        public bool CanPlayerAccess(PlayerRef player) => true;
        public bool CanAccept(int slotIndex, InventorySlotState incoming) => true;

        public void SetSlot(int slotIndex, InventorySlotState state)
        {
            var prev = _slots[slotIndex];
            bool was = IsNonEmpty(prev);
            _slots[slotIndex] = state;
            bool now = IsNonEmpty(state);
            if (was && !now) _nonEmptyCount = Mathf.Max(0, _nonEmptyCount - 1);
            else if (!was && now) _nonEmptyCount++;
            TryDespawnWhenEmpty();
        }

        public void IncrementVersion()
        {
            _version++;
        }

        public void ServerInitFromPlayer(PlayerRef owner)
        {
            if (!Object.HasStateAuthority) return;

            var packed = new System.Collections.Generic.List<InventorySlotState>(32);

            if (_registry != null && _registry.TryGet(ContainerId.PlayerQuickOf(owner), out var quick) && quick != null)
            {
                var src = quick.Slots;
                if (src != null)
                {
                    for (int i = 0; i < src.Length; i++)
                    {
                        var s = src[i];
                        if (s == null) continue;
                        var id = InventorySlotStateAccessor.ReadId(s);
                        var cnt = InventorySlotStateAccessor.ReadCount(s);
                        if (string.IsNullOrEmpty(id) || cnt <= 0) continue;
                        packed.Add(s.Clone());
                    }
                }
            }

            if (_registry != null && _registry.TryGet(ContainerId.PlayerMainOf(owner), out var main) && main != null)
            {
                var src = main.Slots;
                if (src != null)
                {
                    for (int i = 0; i < src.Length; i++)
                    {
                        var s = src[i];
                        if (s == null) continue;
                        var id = InventorySlotStateAccessor.ReadId(s);
                        var cnt = InventorySlotStateAccessor.ReadCount(s);
                        if (string.IsNullOrEmpty(id) || cnt <= 0) continue;
                        packed.Add(s.Clone());
                    }
                }
            }

            var size = Mathf.Max(0, packed.Count);
            SlotsCapacity = size;

            _slots = size > 0 ? new InventorySlotState[size] : System.Array.Empty<InventorySlotState>();
            for (int i = 0; i < size; i++)
                _slots[i] = packed[i];

            _version++;

            var deltas = new System.Collections.Generic.List<ContainerDelta>(2);

            if (_registry != null && _registry.TryGet(ContainerId.PlayerQuickOf(owner), out var quickToClear) && quickToClear != null)
            {
                var before = quickToClear.Version;
                var changes = new System.Collections.Generic.List<SlotChange>(quickToClear.Capacity);
                var slots = quickToClear.Slots;
                for (int i = 0; i < quickToClear.Capacity; i++)
                {
                    var s = slots[i];
                    var id = InventorySlotStateAccessor.ReadId(s);
                    var cnt = InventorySlotStateAccessor.ReadCount(s);
                    if (!string.IsNullOrEmpty(id) && cnt > 0)
                    {
                        quickToClear.SetSlot(i, null);
                        quickToClear.IncrementVersion();
                        changes.Add(new SlotChange { index = i, state = null });
                    }
                }
                if (changes.Count > 0)
                {
                    deltas.Add(new ContainerDelta
                    {
                        id = quickToClear.Id,
                        fromVersion = before,
                        toVersion = quickToClear.Version,
                        changes = changes.ToArray()
                    });
                }
            }

            if (_registry != null && _registry.TryGet(ContainerId.PlayerMainOf(owner), out var mainToClear) && mainToClear != null)
            {
                var before = mainToClear.Version;
                var changes = new System.Collections.Generic.List<SlotChange>(mainToClear.Capacity);
                var slots = mainToClear.Slots;
                for (int i = 0; i < mainToClear.Capacity; i++)
                {
                    var s = slots[i];
                    var id = InventorySlotStateAccessor.ReadId(s);
                    var cnt = InventorySlotStateAccessor.ReadCount(s);
                    if (!string.IsNullOrEmpty(id) && cnt > 0)
                    {
                        mainToClear.SetSlot(i, null);
                        mainToClear.IncrementVersion();
                        changes.Add(new SlotChange { index = i, state = null });
                    }
                }
                if (changes.Count > 0)
                {
                    deltas.Add(new ContainerDelta
                    {
                        id = mainToClear.Id,
                        fromVersion = before,
                        toVersion = mainToClear.Version,
                        changes = changes.ToArray()
                    });
                }
            }

            if (deltas.Count > 0)
            {
                InventoryRpcRouter router = null;
                if (Runner != null && Runner.TryGetPlayerObject(owner, out var po) && po != null)
                    router = po.GetComponent<InventoryRpcRouter>() ?? po.GetComponentInChildren<InventoryRpcRouter>(true);

                if (router != null)
                {
                    for (int i = 0; i < deltas.Count; i++)
                        router.BroadcastDeltaFromServer(deltas[i]);
                }
            }
        }

       
    }
}