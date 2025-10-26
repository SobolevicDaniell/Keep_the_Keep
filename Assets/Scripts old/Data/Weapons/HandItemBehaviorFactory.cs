using System;
using UnityEngine;

namespace Game
{
    public sealed class HandItemBehaviorFactory
    {
        public IHandItemBehavior Create(InteractionController ic, string itemId, int quickSlotIndex)
        {
            if (ic == null || ic.db == null || string.IsNullOrEmpty(itemId))
                return new NoOpBehavior();

            var so = ic.db.Get(itemId);
            if (so == null)
                return new NoOpBehavior();

            IHandItemBehavior behavior;

            if (IsWeaponSO(so))
            {
                var wb = new WeaponBehavior();
                wb.Construct(ic, ic.playerRpcHandler, ic.db, itemId, quickSlotIndex);
                behavior = wb;
            }
            else
            {
                var nb = new NoOpBehavior();
                nb.Construct(ic, ic.playerRpcHandler, ic.db, itemId, quickSlotIndex);
                behavior = nb;
            }

            return behavior;
        }

        private static bool IsWeaponSO(ScriptableObject so)
        {
            var t = so.GetType();
            return t.Name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class NoOpBehavior : IHandItemBehavior
        {
            private InteractionController _ic;
            private string _itemId;

            public void Construct(InteractionController ic, PlayerRpcHandler rpc, ItemDatabaseSO db, string itemId, int quickSlotIndex)
            {
                _ic = ic;
                _itemId = itemId;
            }

            public void OnEquip() { }
            public void OnUnequip() { }

            public void OnUsePressed() { }
            public void OnUseHeld(float dt) { }
            public void OnUseReleased() { }

            public bool IsValid() => _ic != null && !string.IsNullOrEmpty(_itemId);

            public void ServerReload() { }

            public void OnMuzzleFlash()
            {
            }
        }
    }
}
