using Fusion;
using UnityEngine;

namespace Game
{
    public class QuickSlotController : MonoBehaviour
    {
        private InteractionController _ic;
        private InventoryService _inventory;
        private bool _constructed;
        private bool _enabledForLocal;

        public void Construct(InteractionController ic, InventoryService inventory)
        {
            _ic = ic;
            _inventory = inventory;
            _constructed = true;
        }

        public void EnableForLocal()  => _enabledForLocal = true;
        public void DisableForLocal() => _enabledForLocal = false;

        public void ChangeSlotAbsolute(int slot)
        {
            if (!_constructed || !_enabledForLocal) return;
            if (_ic == null || !_ic.Object.HasInputAuthority) return;

            _ic.playerRpcHandler?.RPC_RequestEquipQuickSlot(slot);
        }

        public void ChangeSlotRelative(int delta)
        {
            if (!_constructed || !_enabledForLocal) return;
            if (_ic == null || !_ic.Object.HasInputAuthority || _inventory == null) return;

            var slots = _inventory.GetQuickSlots();
            if (slots == null || slots.Length == 0) return;

            int cur  = _inventory.SelectedQuickSlot < 0 ? 0 : _inventory.SelectedQuickSlot;
            int next = (cur + delta + slots.Length) % slots.Length;

            ChangeSlotAbsolute(next);
        }
    }
}
