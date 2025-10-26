using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ChestInventoryServer : NetworkBehaviour, IInventoryContainer
    {
        [SerializeField] private int _size = 24;

        private InventorySlotState[] _slots;
        private int _version;

        [Inject(Optional = true)] private InventoryContainerRegistry _registry;

        [Networked] public int SlotsCapacity { get; private set; }

        public ContainerId Id { get; private set; }
        public int Version => _version;
        public int Capacity => _size;
        public InventorySlotState[] Slots => _slots;

        public override void Spawned()
        {
            if (!Object.HasStateAuthority) return;
            if (_size < 0) _size = 0;
            if (_slots == null || _slots.Length != _size) _slots = new InventorySlotState[_size];
            Id = ContainerId.OfObject(ContainerType.Chest, Object.Id);
            _version = 0;
            SlotsCapacity = _size;
            _registry?.Register(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (!Object.HasStateAuthority) return;
            _registry?.Unregister(Id);
        }

        public bool CanPlayerAccess(PlayerRef player) => true;
        public bool CanAccept(int slotIndex, InventorySlotState incoming) => true;
        public void SetSlot(int slotIndex, InventorySlotState state) => _slots[slotIndex] = state;
        public void IncrementVersion() => _version++;
    }
}
