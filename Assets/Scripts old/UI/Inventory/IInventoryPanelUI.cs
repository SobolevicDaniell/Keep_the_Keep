namespace Game.UI
{
    public enum PanelKind { Player, Quick, Chest }

    public interface IInventoryPanelUI
    {
        PanelKind Kind { get; }
        event System.Action<InventorySlotUI> OnSlotBeginDrag;
        event System.Action<InventorySlotUI> OnSlotEndDrag;
        event System.Action<InventorySlotUI> OnSlotEnter;
        event System.Action<InventorySlotUI> OnSlotExit;

        void RefreshPanel();
        void ClearInventory();
    }
}
