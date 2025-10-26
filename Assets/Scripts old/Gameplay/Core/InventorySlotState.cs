using System;

namespace Game
{
    [Serializable]
    public class InventorySlotState
    {
        public string itemId;
        public int count;
        public ItemState itemState;

        public bool IsEmpty => string.IsNullOrEmpty(itemId) || count <= 0;
        public InventorySlotState Clone()
    {
        return new InventorySlotState
        {
            itemId    = this.itemId,
            count     = this.count,
            itemState = this.itemState != null ? new ItemState(this.itemState) : null
        };
    }
    }
}
