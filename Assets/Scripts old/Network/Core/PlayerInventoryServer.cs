// Scripts/Inventory/PlayerInventoryServer.cs
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerInventoryServer : NetworkBehaviour, IInventoryContainer
    {
        [SerializeField] private ContainerType _kind = ContainerType.PlayerMain;

        [SerializeField] private PlayerStatsSO _statsSerialized;

        [Inject(Optional = true)] private PlayerStatsSO _statsDI;

        private InventorySlotState[] _slots;
        private int _version;

        [Inject(Optional = true)] private InventoryContainerRegistry _registry;

        public ContainerId Id { get; private set; }
        public int Version => _version;
        public int Capacity => _slots?.Length ?? 0;
        public InventorySlotState[] Slots => _slots;

        public override void Spawned()
        {
            if (!Object.HasStateAuthority) return;

            var so = _statsDI != null ? _statsDI : _statsSerialized;
            var cap = ResolveCapacityFromSO(so, _kind);
            if (cap <= 0) cap = 1;

            var owner = Object.InputAuthority;
            switch (_kind)
            {
                case ContainerType.PlayerMain:
                    Id = ContainerId.PlayerMainOf(owner);
                    break;
                case ContainerType.PlayerQuick:
                    Id = ContainerId.PlayerQuickOf(owner);
                    break;
                default:
                    Id = ContainerId.OfObject(_kind, Object.Id);
                    break;
            }

            if (_slots == null || _slots.Length != cap)
                _slots = new InventorySlotState[cap];

            _version = 0;
            _registry?.Register(this);
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (!Object.HasStateAuthority) return;
            _registry?.Unregister(Id);
        }

        private static int ResolveCapacityFromSO(PlayerStatsSO so, ContainerType kind)
        {
            if (so == null) return 1;
            return kind == ContainerType.PlayerQuick ? so.quickSlotsCount : so.inventorySlotsCount;
        }

        public bool CanPlayerAccess(PlayerRef viewer)
        {
            if (viewer == Id.ownerRef)
                return true;

            return false;
        } 
        public bool CanAccept(int slotIndex, InventorySlotState incoming) => true;

        public void SetSlot(int index, InventorySlotState state)
        {
            if (_slots == null || index < 0 || index >= _slots.Length) return;
            _slots[index] = state?.Clone();
        }

        public void IncrementVersion() => _version++;
    }
}