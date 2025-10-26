using System;

namespace Game
{
    public interface IInventory
    {
        InventorySlot[] GetInventorySlots();
        event Action OnInventoryChanged;
        bool TryAddItem(string itemId, int count);
        bool TryRemoveItem(int slotIndex, int count);

        bool TryMoveToSlot(string itemId, int count, int targetSlot, ItemState state = null);

        void RaiseInventoryChanged();
    }
}