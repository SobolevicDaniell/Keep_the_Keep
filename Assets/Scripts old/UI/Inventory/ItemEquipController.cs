using UnityEngine;

namespace Game
{
    public sealed class ItemEquipController : MonoBehaviour
    {
        private HandItemBehaviorFactory _factory;
        private ItemDatabaseSO _db;
        private InteractionController _ic;

        public void Initialize(HandItemBehaviorFactory factory, ItemDatabaseSO itemDatabase, InteractionController interactionController)
        {
            _factory = factory;
            _db = itemDatabase;
            _ic = interactionController;
        }

        public void Equip(int slotIdx, InventorySlot[] quickSlots)
        {
            if (_ic == null) return;
        
            _ic.ClearBehavior();
        
            if (quickSlots == null || slotIdx < 0 || slotIdx >= quickSlots.Length)
            {
                _ic.handItemController?.RequestUnEquip();
                if (_ic.SelectedQuickIndexNet != -1)
                    _ic.playerRpcHandler?.RPC_RequestEquipQuickSlot(-1);
                return;
            }
        
            var slot = quickSlots[slotIdx];
            bool alreadySelected = _ic.SelectedQuickIndexNet == slotIdx;
        
            if (slot == null || string.IsNullOrEmpty(slot.Id))
            {
                _ic.handItemController?.RequestUnEquip();
                if (alreadySelected)
                    _ic.playerRpcHandler?.RPC_RefreshSelectedQuick();
                else
                    _ic.playerRpcHandler?.RPC_RequestEquipQuickSlot(slotIdx);
                return;
            }
        
            string itemId = slot.Id;
        
            var behavior = _factory.Create(_ic, itemId, slotIdx);
            behavior.OnEquip();
            _ic.SetCurrentBehavior(behavior);
        
            if (alreadySelected)
                _ic.playerRpcHandler?.RPC_RefreshSelectedQuick();
            else
                _ic.playerRpcHandler?.RPC_RequestEquipQuickSlot(slotIdx);
        }
        
        
        
        
        
                public void ValidateEquipped(int selectedQuickSlot, InventorySlot[] quickSlots)
        {
            if (quickSlots == null || selectedQuickSlot < 0 || selectedQuickSlot >= quickSlots.Length)
            {
                Equip(-1, quickSlots);
                return;
            }
        
            Equip(selectedQuickSlot, quickSlots);
        }



    }
}