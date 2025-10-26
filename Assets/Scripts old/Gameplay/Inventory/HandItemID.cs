using UnityEngine;
using Zenject;
using Game;

namespace Game
{
    public class HandItemID : MonoBehaviour
    {
        [SerializeField] private string _handID;
        private InventoryService _inv;

        private bool _subscribed;

        [Inject]
        public void Construct(InventoryService inv)
        {
            _inv = inv;
        }

        private void Start()
        {
            if (_inv != null && !_subscribed)
            {
                _inv.OnQuickSlotSelectionChanged += UpdateVisible;
                _subscribed = true;
                UpdateVisible(_inv.SelectedQuickSlot);
            }
        }

        private void OnDestroy()
        {
            if (_subscribed)
                _inv.OnQuickSlotSelectionChanged -= UpdateVisible;
        }

        private void UpdateVisible(int sel)
        {
            if (sel < 0)
            {
                gameObject.SetActive(false);
                return;
            }

            var slot = _inv.GetQuickSlots()[sel];
            bool show = slot.Id != null && slot.Id == _handID;
            gameObject.SetActive(show);
        }
    }
}
