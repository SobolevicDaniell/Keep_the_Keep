using UnityEngine;

namespace Game
{
    public class PlaceableBehavior : MonoBehaviour, IHandItemBehavior
    {
        private PlaceableItemSO _so;
        private Transform _handPoint;
        private InteractionController _ic;
        private InventorySlot _slot;

        public PlaceableBehavior Construct(PlaceableItemSO so, Transform handParent, InteractionController ic, InventorySlot slot)
        {
            _so = so;
            _handPoint = handParent;
            _ic = ic;
            _slot = slot;
            return this;
        }

        public void OnEquip() { }
        public void OnUnequip() { }
        public void OnUsePressed() { }
        public void OnUseReleased() { }
        public void OnUseHeld(float delta) { }
        public void OnMuzzleFlash() { }
    }
}
