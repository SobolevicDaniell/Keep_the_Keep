using UnityEngine;
using Zenject;
using System;

namespace Game.UI
{
    public sealed class QuickSlotPanel : MonoBehaviour, IInventoryPanelUI
    {
        [SerializeField] private Transform _slotsParent;

        public PanelKind Kind => PanelKind.Quick;

        private Game.InventoryService _inv;
        private ItemDatabaseSO _db;
        private InventorySlotUI _slotPrefab;
        private InventorySlotUI[] _slots;

        public event Action<InventorySlotUI> OnSlotBeginDrag;
        public event Action<InventorySlotUI> OnSlotEndDrag;
        public event Action<InventorySlotUI> OnSlotEnter;
        public event Action<InventorySlotUI> OnSlotExit;

        private bool _initialized;
        private InventorySlotUI _draggingSlot;
        private InventorySlotUI _targetSlot;

        [Inject(Optional = true)] private InventoryClientFacade _facade;

        [Inject]
        public void Construct(Game.InventoryService inv, ItemDatabaseSO db, [Inject(Id = "InventorySlotPrefab")] InventorySlotUI slotPrefab)
        {
            _inv = inv;
            _db = db;
            _slotPrefab = slotPrefab;
        }
        private void OnEnable()
        {
            if (!_initialized) Initialize();
        }

        private void Initialize()
        {
            if (_inv == null || _db == null || _slotPrefab == null || _slotsParent == null)
            {
                gameObject.SetActive(false);
                return;
            }

            var slots = _inv.GetQuickSlots();
            var count = slots != null ? slots.Length : 0;

            CreateSlots(count);

            _inv.OnQuickSlotsChanged += Refresh;
            _inv.OnQuickSlotSelectionChanged += OnQuickSlotChanged;

            _initialized = true;

            Refresh();
            OnQuickSlotChanged(_inv.SelectedQuickSlot);
        }

        private void CreateSlots(int cap)
        {
            if (_slots != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    var s = _slots[i];
                    if (s == null) continue;

                    s.OnBeginDrag -= HandleBeginDrag;
                    s.OnEndDrag -= HandleEndDrag;
                    s.OnEnter -= HandleSlotEnter;
                    s.OnExit -= HandleSlotExit;

                    Destroy(s.gameObject);
                }
            }

            cap = Mathf.Max(0, cap);
            _slots = new InventorySlotUI[cap];

            for (int i = 0; i < cap; i++)
            {
                var slot = Instantiate(_slotPrefab, _slotsParent);
                slot.gameObject.SetActive(true);
                slot.Init(i, _inv, this);
                slot.SetActive(false);

                slot.OnBeginDrag += HandleBeginDrag;
                slot.OnEndDrag += HandleEndDrag;
                slot.OnEnter += HandleSlotEnter;
                slot.OnExit += HandleSlotExit;

                _slots[i] = slot;
            }
        }

        private void HandleBeginDrag(InventorySlotUI slot)
        {
            _draggingSlot = slot;
            OnSlotBeginDrag?.Invoke(slot);
        }

        private void HandleEndDrag(InventorySlotUI slot)
        {
            OnSlotEndDrag?.Invoke(slot);
            _draggingSlot = null;
            _targetSlot = null;
        }

        private void HandleSlotEnter(InventorySlotUI slot)
        {
            _targetSlot = slot;
            OnSlotEnter?.Invoke(slot);
        }

        private void HandleSlotExit(InventorySlotUI slot)
        {
            if (_targetSlot == slot) _targetSlot = null;
            OnSlotExit?.Invoke(slot);
        }

        public void RefreshPanel() => Refresh();

        public void Refresh()
        {
            var backend = _inv.GetQuickSlots();
            var need = backend != null ? backend.Length : 0;

            if (_slots == null || _slots.Length != need)
                CreateSlots(need);

            int n = _slots != null ? _slots.Length : 0;
            for (int i = 0; i < n; i++)
            {
                var ui = _slots[i];
                var slot = backend != null && i < backend.Length ? backend[i] : null;

                ItemSO item = null;
                int count = 0;
                ItemState state = null;

                if (slot != null && !string.IsNullOrEmpty(slot.Id))
                {
                    item = _db.Get(slot.Id);
                    count = slot.Count;
                    state = slot.State;
                }

                ui.Set(item, count, state);
            }
        }

        private void OnQuickSlotChanged(int selected)
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
                _slots[i]?.SetActive(selected >= 0 && i == selected);
        }

        public void ClearInventory()
        {
            if (_inv != null)
            {
                _inv.OnQuickSlotsChanged -= Refresh;
                _inv.OnQuickSlotSelectionChanged -= OnQuickSlotChanged;
            }
            _inv = null;

            if (_slots == null) return;
            foreach (var slotUI in _slots)
                if (slotUI != null) slotUI.Set(null, 0, null);
        }

        private void OnDestroy()
        {
            if (_inv != null)
            {
                _inv.OnQuickSlotsChanged -= Refresh;
                _inv.OnQuickSlotSelectionChanged -= OnQuickSlotChanged;
            }
        }
    }
}