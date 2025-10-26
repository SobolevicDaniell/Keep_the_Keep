using UnityEngine;

namespace Game
{
    public class DefaultHandBehavior :MonoBehaviour, IHandItemBehavior
    {
        private ItemSO _so;
        private Transform _handPoint;
        public DefaultHandBehavior Construct(ItemSO so, Transform handParent)
        {
            _so = so;
            _handPoint = handParent;
            return this;
        }

        public void OnEquip() { }
        public void OnUnequip() { }
        public void OnUsePressed() { }
        public void OnUseHeld(float d) { }
        public void OnUseReleased() { }

        public void OnMuzzleFlash()
        {
        }

        public void OnUseHeld()
        {
            throw new System.NotImplementedException();
        }
    }
}
