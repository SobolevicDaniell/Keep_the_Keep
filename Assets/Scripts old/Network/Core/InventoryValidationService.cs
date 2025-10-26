using Fusion;
using UnityEngine;

namespace Game
{
    public sealed class InventoryValidationService
    {
        public bool CanTransfer(PlayerRef actor, IInventoryContainer from, int fromIndex, IInventoryContainer to, int toIndex, int amount)
        {
            if (from == null || to == null) return false;
            if (amount <= 0) return false;
            if (!from.CanPlayerAccess(actor)) return false;
            if (!to.CanPlayerAccess(actor)) return false;
            if (fromIndex < 0 || fromIndex >= from.Capacity) return false;
            if (toIndex < 0 || toIndex >= to.Capacity) return false;
            var s = from.Slots[fromIndex];
            if (s == null || s.IsEmpty) return false;
            return to.CanAccept(toIndex, s);
        }
    }
}
