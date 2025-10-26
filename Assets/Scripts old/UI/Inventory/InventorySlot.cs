namespace Game
{
    public class InventorySlot
    {
        public string Id;
        public int Count;
        public ItemState State;

        public InventorySlot(string id = null, int count = 0, ItemState state = null)
        {
            Id = id;
            Count = count;
            State = state ?? new ItemState();
        }
    }

}