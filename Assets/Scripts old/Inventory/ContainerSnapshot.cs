namespace Game
{
    public class ContainerSnapshot
    {
        public ContainerId id;
        public int version;
        public InventorySlotState[] slots;
    }

    public struct SlotChange
    {
        public int index;
        public InventorySlotState state;
    }

    public class ContainerDelta
    {
        public ContainerId id;
        public int fromVersion;
        public int toVersion;
        public SlotChange[] changes;

        public ContainerDelta() {}

        public ContainerDelta(ContainerId id) { this.id = id; }
    }
}
