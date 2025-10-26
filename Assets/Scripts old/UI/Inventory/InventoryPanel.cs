using UnityEngine;
using Zenject;
using System;

namespace Game.UI
{
    public class InventoryPanel : MonoBehaviour, IInventoryPanelUI
    {
        [SerializeField] private Transform _slotsParent;

        private IInventory _inventory;
        private ItemDatabaseSO _database;
        private InventorySlotUI _slotPrefab;
        private InventorySlotUI[] _slotsUI;

        public PanelKind Kind => PanelKind.Player;

        public event Action<InventorySlotUI> OnSlotBeginDrag;
        public event Action<InventorySlotUI> OnSlotEndDrag;
        public event Action<InventorySlotUI> OnSlotEnter;
        public event Action<InventorySlotUI> OnSlotExit;

        private bool _initialized;

        [Inject]
        public void Construct(IInventory inventory, ItemDatabaseSO database, [Inject(Id = "InventorySlotPrefab")] InventorySlotUI slotPrefab)
        {
            _inventory = inventory;
            _database = database;
            _slotPrefab = slotPrefab;
        }

        private void OnEnable()
        {
            if (!_initialized) Initialize();
        }

        private void Initialize()
        {
            if (_inventory == null || _database == null || _slotPrefab == null || _slotsParent == null)
            {
                gameObject.SetActive(false);
                return;
            }

            var slots = _inventory.GetInventorySlots();
            var count = slots != null ? slots.Length : 0;

            CreateSlots(count);

            _inventory.OnInventoryChanged += Refresh;

            _initialized = true;
            Refresh();
        }
        

        public void RefreshPanel() => Refresh();

        private void CreateSlots(int count)
        {
            if (_slotsUI != null && _slotsUI.Length > 0)
            {
                foreach (var s in _slotsUI)
                    if (s != null) Destroy(s.gameObject);
            }

            _slotsUI = new InventorySlotUI[count];

            for (int i = 0; i < count; i++)
            {
                var slot = Instantiate(_slotPrefab, _slotsParent);
                slot.Init(i, _inventory, this);

                slot.OnBeginDrag += HandleBeginDrag;
                slot.OnEndDrag   += HandleEndDrag;
                slot.OnEnter     += HandleEnter;
                slot.OnExit      += HandleExit;

                _slotsUI[i] = slot;
            }
        }

        private void HandleBeginDrag(InventorySlotUI slot) => OnSlotBeginDrag?.Invoke(slot);
        private void HandleEndDrag(InventorySlotUI slot)   => OnSlotEndDrag?.Invoke(slot);
        private void HandleEnter(InventorySlotUI slot)     => OnSlotEnter?.Invoke(slot);
        private void HandleExit(InventorySlotUI slot)      => OnSlotExit?.Invoke(slot);

        public void Refresh()
        {
            var slots = _inventory.GetInventorySlots();
            var need = slots != null ? slots.Length : 0;

            if (_slotsUI == null || _slotsUI.Length != need)
                CreateSlots(need);

            int n = _slotsUI != null ? _slotsUI.Length : 0;
            for (int i = 0; i < n; i++)
            {
                var ui = _slotsUI[i];

                ItemSO item = null;
                int count = 0;
                Game.ItemState state = null;

                if (slots != null && i < slots.Length)
                {
                    var backend = slots[i];
                    if (backend != null && !string.IsNullOrEmpty(backend.Id))
                    {
                        item = _database.Get(backend.Id);
                        count = backend.Count;
                        state = backend.State;
                    }
                }

                ui.Set(item, count, state);
            }
        }

        public void ClearInventory()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;

            _inventory = null;

            if (_slotsUI == null) return;

            foreach (var slotUI in _slotsUI)
                slotUI.Set(null, 0, null);

        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= Refresh;

            if (_slotsUI != null)
            {
                foreach (var slot in _slotsUI)
                {
                    if (slot == null) continue;
                    slot.OnBeginDrag -= HandleBeginDrag;
                    slot.OnEndDrag   -= HandleEndDrag;
                    slot.OnEnter     -= HandleEnter;
                    slot.OnExit      -= HandleExit;
                }
            }
        }
    }
}
