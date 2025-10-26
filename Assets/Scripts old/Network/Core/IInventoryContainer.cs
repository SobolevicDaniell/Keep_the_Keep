using Fusion;

namespace Game
{
    public interface IInventoryContainer
    {
        ContainerId Id { get; }
        int Version { get; }
        int Capacity { get; }
        InventorySlotState[] Slots { get; }

        bool CanPlayerAccess(PlayerRef player);
        bool CanAccept(int slotIndex, InventorySlotState incoming);
        void SetSlot(int slotIndex, InventorySlotState state);
        void IncrementVersion();
    }
}
