using UnityEngine;

namespace Game
{
    public class ToolBehavior : MonoBehaviour, IHandItemBehavior
    {
        private ToolSO _so;
        private Transform _handPoint;
        private InteractionController _ic;

        public ToolBehavior Construct(ToolSO so, Transform handParent, InteractionController ic)
        {
            _so = so;
            _handPoint = handParent;
            _ic = ic;
            return this;
        }

        public void OnEquip()
        {
        }


        public void OnUnequip()
        {
          
        }

        public void OnUsePressed()
        {
        }

        public void OnUseHeld(float delta) { }

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
