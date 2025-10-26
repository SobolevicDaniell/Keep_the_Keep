using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Game
{
    public class InventoryService : IInventory, IDisposable
    {

        public event Action OnQuickSlotsChanged;
        public event Action OnInventoryChanged;
        public event Action<int> OnQuickSlotSelectionChanged;

        public int SelectedQuickSlot { get; private set; } = -1;

        private InventorySlot[] _quickSlots;
        private InventorySlot[] _inventorySlots;

        private readonly ItemDatabaseSO _db;
        private readonly PlayerStatsSO _stats;

        [Inject(Optional = true)] private ContainerViewSessionClient _view;
        [Inject(Optional = true)] private InventoryClientFacade _facade;


        private bool _resizedToServer;
        private bool _subscribed;

        [Inject]
        public InventoryService(ItemDatabaseSO db, PlayerStatsSO stats)
        {
            _db = db;
            _stats = stats;

            int quickCap = Mathf.Max(1, _stats != null ? _stats.quickSlotsCount : 1);
            int mainCap = Mathf.Max(1, _stats != null ? _stats.inventorySlotsCount : 1);

            _quickSlots = new InventorySlot[quickCap];
            _inventorySlots = new InventorySlot[mainCap];

            for (int i = 0; i < _quickSlots.Length; i++) _quickSlots[i] = new InventorySlot(null, 0);
            for (int i = 0; i < _inventorySlots.Length; i++) _inventorySlots[i] = new InventorySlot(null, 0);

            if (_view != null)
            {
                _view.OnContainerChanged += OnContainerChangedFromServer_ResizeOnce;
                _subscribed = true;
            }
        }

        public void Dispose()
        {
            if (_subscribed && _view != null)
            {
                _view.OnContainerChanged -= OnContainerChangedFromServer_ResizeOnce;
                _subscribed = false;
            }
        }

        private void OnContainerChangedFromServer_ResizeOnce(ContainerId id)
        {
            if (_facade == null) return;
            if (_resizedToServer) return;

            int quickCap = Mathf.Max(1, _facade.GetLocalQuickCapacity());
            int mainCap = Mathf.Max(1, _facade.GetLocalMainCapacity());

            if (quickCap > 0 && (_quickSlots == null || _quickSlots.Length != quickCap))
            {
                var arr = new InventorySlot[quickCap];
                int old = _quickSlots != null ? _quickSlots.Length : 0;
                for (int i = 0; i < quickCap; i++)
                    arr[i] = i < old ? _quickSlots[i] : new InventorySlot(null, 0);
                _quickSlots = arr;
            }

            if (mainCap > 0 && (_inventorySlots == null || _inventorySlots.Length != mainCap))
            {
                var arr = new InventorySlot[mainCap];
                int old = _inventorySlots != null ? _inventorySlots.Length : 0;
                for (int i = 0; i < mainCap; i++)
                    arr[i] = i < old ? _inventorySlots[i] : new InventorySlot(null, 0);
                _inventorySlots = arr;
            }

            _resizedToServer = true;
            OnQuickSlotsChanged?.Invoke();
            OnInventoryChanged?.Invoke();
        }

        public InventorySlot[] GetQuickSlots() => _quickSlots;
        public InventorySlot[] GetInventorySlots() => _inventorySlots;

        public void ToggleQuickSlot(int idx)
        {
            SelectedQuickSlot = (SelectedQuickSlot == idx) ? -1 : idx;
            OnQuickSlotSelectionChanged?.Invoke(SelectedQuickSlot);
        }

        public void ForceSetQuickSlot(int idx)
        {
            if (idx == SelectedQuickSlot) return;
            SelectedQuickSlot = idx;
            OnQuickSlotSelectionChanged?.Invoke(SelectedQuickSlot);
        }

        public bool MoveQuickSlot(int from, int to)
        {
            if (from == to) return false;
            if (from < 0 || from >= _quickSlots.Length) return false;
            if (to < 0 || to >= _quickSlots.Length) return false;

            (_quickSlots[from], _quickSlots[to]) = (_quickSlots[to], _quickSlots[from]);

        
            OnQuickSlotsChanged?.Invoke();
            OnQuickSlotSelectionChanged?.Invoke(SelectedQuickSlot);
            return true;
        }

        public int HandlePick(string itemId, int count, int ammo)
        {
            var item = _db.Get(itemId);
            if (item == null) return count;

            int rem = count;

            if (item.priority == 1)
            {
                rem = TryQuick(item, rem, ammo);
                if (rem > 0) rem = TryInventory(item, rem, ammo);
            }
            else
            {
                rem = TryInventory(item, rem, ammo);
                if (rem > 0) rem = TryQuick(item, rem, ammo);
            }

            return rem;
        }

        private int TryQuick(ItemSO item, int rem, int ammo = 0)
        {
            if (rem <= 0) return 0;

            if (item.MaxStack > 1)
            {
                foreach (var slot in _quickSlots)
                {
                    if (rem == 0) break;
                    if (slot.Id == item.Id && slot.Count < item.MaxStack)
                    {
                        int can = Mathf.Min(rem, item.MaxStack - slot.Count);
                        slot.Count += can;
                        rem -= can;
                    }
                }
            }

            foreach (var slot in _quickSlots)
            {
                if (rem == 0) break;
                if (slot.Id == null)
                {
                    int toPut = Mathf.Min(rem, item.MaxStack);
                    slot.Id = item.Id;
                    slot.Count = toPut;
                    slot.State = new ItemState(ammo);
                    rem -= toPut;
                }
            }

            OnQuickSlotsChanged?.Invoke();
            return rem < 0 ? 0 : rem;
        }

        private int TryInventory(ItemSO item, int rem, int ammo = 0)
        {
            if (rem <= 0) return 0;

            if (item.MaxStack > 1)
            {
                foreach (var slot in _inventorySlots)
                {
                    if (rem == 0) break;
                    if (slot.Id == item.Id && slot.Count < item.MaxStack)
                    {
                        int can = Mathf.Min(rem, item.MaxStack - slot.Count);
                        slot.Count += can;
                        rem -= can;
                    }
                }
            }

            foreach (var slot in _inventorySlots)
            {
                if (rem == 0) break;
                if (slot.Id == null)
                {
                    int toPut = Mathf.Min(rem, item.MaxStack);
                    slot.Id = item.Id;
                    slot.Count = toPut;
                    slot.State = new ItemState(ammo);
                    rem -= toPut;
                }
            }

            OnInventoryChanged?.Invoke();
            return rem < 0 ? 0 : rem;
        }

        public void RaiseQuickSlotsChanged() => OnQuickSlotsChanged?.Invoke();
        public void RaiseQuickSlotSelectionChanged(int sel) => OnQuickSlotSelectionChanged?.Invoke(sel);
        public void RaiseInventoryChanged() => OnInventoryChanged?.Invoke();

        public int GetResourceCount(string resourceId)
        {
            int c = 0;
            foreach (var s in _quickSlots) if (s.Id == resourceId) c += s.Count;
            foreach (var s in _inventorySlots) if (s.Id == resourceId) c += s.Count;
            return c;
        }

        public bool SpendResource(string resourceId, int amount)
        {
            var list = new List<InventorySlot>(_quickSlots.Length + _inventorySlots.Length);
            list.AddRange(_quickSlots);
            list.AddRange(_inventorySlots);

            int need = amount;
            foreach (var s in list)
            {
                if (need <= 0) break;
                if (s.Id == resourceId && s.Count > 0)
                {
                    int take = Mathf.Min(s.Count, need);
                    s.Count -= take;
                    need -= take;
                    if (s.Count <= 0)
                    {
                        s.Id = null;
                        s.State = null;
                    }
                }
            }
            OnQuickSlotsChanged?.Invoke();
            OnInventoryChanged?.Invoke();
            return need <= 0;
        }

        public InventorySlot FindResourceSlot(string resourceId)
        {
            foreach (var s in _quickSlots) if (s.Id == resourceId && s.Count > 0) return s;
            foreach (var s in _inventorySlots) if (s.Id == resourceId && s.Count > 0) return s;
            return null;
        }

        public IEnumerable<InventorySlot> FindAllResourceSlots(string resourceId)
        {
            foreach (var s in _quickSlots) if (s.Id == resourceId && s.Count > 0) yield return s;
            foreach (var s in _inventorySlots) if (s.Id == resourceId && s.Count > 0) yield return s;
        }

        public bool TryAddItem(string itemId, int count)
        {
            var so = _db.Get(itemId);
            if (so == null) return false;

            int left = count;

            foreach (var s in _quickSlots)
            {
                if (left == 0) break;
                if (s.Id == itemId && s.Count < so.MaxStack)
                {
                    int can = Mathf.Min(left, so.MaxStack - s.Count);
                    s.Count += can;
                    left -= can;
                }
            }
            foreach (var s in _inventorySlots)
            {
                if (left == 0) break;
                if (s.Id == itemId && s.Count < so.MaxStack)
                {
                    int can = Mathf.Min(left, so.MaxStack - s.Count);
                    s.Count += can;
                    left -= can;
                }
            }
            foreach (var s in _quickSlots)
            {
                if (left == 0) break;
                if (s.Id == null)
                {
                    int put = Mathf.Min(left, so.MaxStack);
                    s.Id = itemId; s.Count = put;
                    left -= put;
                }
            }
            foreach (var s in _inventorySlots)
            {
                if (left == 0) break;
                if (s.Id == null)
                {
                    int put = Mathf.Min(left, so.MaxStack);
                    s.Id = itemId; s.Count = put;
                    left -= put;
                }
            }

            OnQuickSlotsChanged?.Invoke();
            OnInventoryChanged?.Invoke();
            return left == 0;
        }

        public bool TryMoveToSlot(string itemId, int count, int targetIndex, ItemState state = null)
        {
            if (targetIndex < 0) return false;
            int qLen = _quickSlots.Length;

            InventorySlot[] arr;
            int idx;

            if (targetIndex < qLen)
            {
                arr = _quickSlots;
                idx = targetIndex;
            }
            else
            {
                idx = targetIndex - qLen;
                if (idx < 0 || idx >= _inventorySlots.Length) return false;
                arr = _inventorySlots;
            }

            var so = _db.Get(itemId);
            if (so == null) return false;

            var slot = arr[idx];

            if (slot.Id == null)
            {
                slot.Id = itemId;
                slot.Count = Mathf.Min(count, so.MaxStack);
                slot.State = state != null ? new ItemState(state) : null;
                if (arr == _quickSlots) OnQuickSlotsChanged?.Invoke(); else OnInventoryChanged?.Invoke();
                return true;
            }

            if (slot.Id == itemId && slot.Count < so.MaxStack)
            {
                int can = Mathf.Min(count, so.MaxStack - slot.Count);
                slot.Count += can;
                if (slot.State == null && state != null) slot.State = new ItemState(state);
                if (arr == _quickSlots) OnQuickSlotsChanged?.Invoke(); else OnInventoryChanged?.Invoke();
                return can == count;
            }

            return false;
        }

        public bool TryRemoveItem(int targetIndex, int amount)
        {
            if (targetIndex < 0) return false;
            int qLen = _quickSlots.Length;

            InventorySlot[] arr;
            int idx;

            if (targetIndex < qLen)
            {
                arr = _quickSlots;
                idx = targetIndex;
            }
            else
            {
                idx = targetIndex - qLen;
                if (idx < 0 || idx >= _inventorySlots.Length) return false;
                arr = _inventorySlots;
            }

            var slot = arr[idx];
            if (slot.Id == null || slot.Count < amount) return false;

            slot.Count -= amount;
            if (slot.Count <= 0)
            {
                slot.Id = null;
                slot.State = null;
                slot.Count = 0;
            }

            if (arr == _quickSlots) OnQuickSlotsChanged?.Invoke(); else OnInventoryChanged?.Invoke();
            return true;
        }

        public void ApplySnapshot(ContainerSnapshot snap)
        {
            if (snap == null || snap.slots == null) return;

            var arr = new InventorySlot[snap.slots.Length];
            for (int i = 0; i < snap.slots.Length; i++)
            {
                var s = snap.slots[i];
                if (s == null || s.IsEmpty)
                {
                    arr[i] = new InventorySlot(null, 0);
                }
                else
                {
                    arr[i] = new InventorySlot
                    {
                        Id = s.itemId,
                        Count = s.count,
                        State = s.itemState != null ? new ItemState(s.itemState) : null
                    };
                }
            }

            switch (snap.id.type)
            {
                case ContainerType.PlayerMain:
                    _inventorySlots = arr;
                    OnInventoryChanged?.Invoke();
                    break;

                case ContainerType.PlayerQuick:
                    _quickSlots = arr;
                    OnQuickSlotsChanged?.Invoke();
                    break;
            }
        }

        public void ApplyDelta(ContainerDelta delta)
        {
            if (delta == null || delta.changes == null) return;

            InventorySlot[] slots = null;
            if (delta.id.type == ContainerType.PlayerQuick) slots = GetQuickSlots();
            else if (delta.id.type == ContainerType.PlayerMain) slots = GetInventorySlots();
            if (slots == null) return;

            var changes = delta.changes;
            for (int i = 0; i < changes.Length; i++)
            {
                var c = changes[i];
                int idx = c.index;
                if (idx < 0 || idx >= slots.Length) continue;

                var dest = slots[idx];
                var incoming = c.state;

                var id = InventorySlotStateAccessor.ReadId(incoming);
                var cnt = InventorySlotStateAccessor.ReadCount(incoming);
                var ist = InventorySlotStateAccessor.ReadState(incoming);

                if (string.IsNullOrEmpty(id) || cnt <= 0)
                {
                    dest.Id = null;
                    dest.Count = 0;
                    dest.State = null;
                }
                else
                {
                    dest.Id = id;
                    dest.Count = cnt;
                    if (ist != null)
                    {
                        dest.State = new ItemState(ist);
                    }
                    else
                    {
                        if (dest.State == null) dest.State = new ItemState();
                    }
                }
            }

            if (delta.id.type == ContainerType.PlayerQuick) RaiseQuickSlotsChanged();
            else RaiseInventoryChanged();
        }

    }
}