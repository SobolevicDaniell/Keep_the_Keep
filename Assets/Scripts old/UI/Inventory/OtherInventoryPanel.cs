using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Zenject;

namespace Game.UI
{
    public sealed class OtherInventoryPanel : MonoBehaviour, IInventoryPanelUI
    {
        [SerializeField] private RectTransform _contentRoot;

        [Inject(Optional = true)] private InventoryClientFacade _facade;
        [Inject(Optional = true)] private InventoryService _inv;


        private ItemDatabaseSO _database;
        private InventorySlotUI _slotPrefab;

        private ContainerId _currentId;
        private int _lastVersion = -1;
        private readonly List<InventorySlotUI> _slots = new();
        private bool _subscribed;

        public PanelKind Kind => PanelKind.Chest;
        public ContainerId CurrentId => _currentId;




        public event Action<InventorySlotUI> OnSlotBeginDrag;
        public event Action<InventorySlotUI> OnSlotEndDrag;
        public event Action<InventorySlotUI> OnSlotEnter;
        public event Action<InventorySlotUI> OnSlotExit;

        [Inject]
        public void Construct(ItemDatabaseSO database, [Inject(Id = "InventorySlotPrefab")] InventorySlotUI slotPrefab)
        {
            _database = database;
            _slotPrefab = slotPrefab;
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            TryUnsubscribe();
            Clear();
            _currentId = default;
            _lastVersion = -1;
        }

        private void HandleBeginDrag(InventorySlotUI slot) { OnSlotBeginDrag?.Invoke(slot); }
        private void HandleEndDrag(InventorySlotUI slot) { OnSlotEndDrag?.Invoke(slot); }
        private void HandleSlotEnter(InventorySlotUI slot) { OnSlotEnter?.Invoke(slot); }
        private void HandleSlotExit(InventorySlotUI slot) { OnSlotExit?.Invoke(slot); }


        public void ShowRemote(ContainerId id)
        {
            _currentId = id;
            _lastVersion = -1;

            if (_facade != null)
                _facade.Open(id);

            EnsureCapacityPlaceholders();
            RebuildNow();
        }


        public void HideRemote()
        {
            _currentId = default;
            _lastVersion = -1;
            Clear();
        }

        private void TrySubscribe()
        {
            if (_facade == null || _subscribed) return;
            _facade.OnContainerChanged += OnFacadeContainerChanged;
            _subscribed = true;
        }

        private void TryUnsubscribe()
        {
            if (_facade == null || !_subscribed) return;
            _facade.OnContainerChanged -= OnFacadeContainerChanged;
            _subscribed = false;
        }

        private void OnFacadeContainerChanged(ContainerId id)
        {
            if (!Matches(_currentId, id)) return;
            RebuildNow();
        }

        private void EnsureCapacityPlaceholders()
        {
            if (_facade == null) return;
            if (_currentId.Equals(default)) return;

            int cap = _facade.GetCapacityImmediate(_currentId);
            if (cap <= 0) return;

            if (_slots.Count == cap) return;

            Clear();

            for (int i = 0; i < cap; i++)
            {
                var slot = Instantiate(_slotPrefab, _contentRoot);
                slot.gameObject.SetActive(true);
                slot.Init(i, _inv, this);
                slot.OnBeginDrag += HandleBeginDrag;
                slot.OnEndDrag += HandleEndDrag;
                slot.OnEnter += HandleSlotEnter;
                slot.OnExit += HandleSlotExit;
                _slots.Add(slot);
            }
        }

        private void RebuildNow()
        {
            if (_facade == null) return;
            if (_currentId.Equals(default)) return;

            if (!_facade.TryGetSnapshotResolved(_currentId, out var resolvedId, out var version, out var slots))
                return;

            if (!resolvedId.Equals(_currentId))
                _currentId = resolvedId;

            if (version == _lastVersion && slots != null && _slots.Count == slots.Length)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var sid = InventorySlotStateAccessor.ReadId(slots[i]);
                    var cnt = InventorySlotStateAccessor.ReadCount(slots[i]);
                    var st = InventorySlotStateAccessor.ReadState(slots[i]);

                    ItemSO item = null;
                    if (!string.IsNullOrEmpty(sid) && _database != null)
                        item = _database.Get(sid);

                    _slots[i].Set(item, cnt, st);
                }

                return;
            }

            _lastVersion = version;

            int need = slots != null ? slots.Length : 0;
            if (need <= 0)
            {
                EnsureCapacityPlaceholders();
                return;
            }

            if (_slots.Count != need)
            {
                Clear();
                for (int i = 0; i < need; i++)
                {
                    var slot = Instantiate(_slotPrefab, _contentRoot);
                    slot.gameObject.SetActive(true);
                    slot.Init(i, _inv, this);
                    slot.OnBeginDrag += HandleBeginDrag;
                    slot.OnEndDrag += HandleEndDrag;
                    slot.OnEnter += HandleSlotEnter;
                    slot.OnExit += HandleSlotExit;
                    _slots.Add(slot);
                }
            }
            for (int i = 0; i < need; i++)
            {
                var sid = InventorySlotStateAccessor.ReadId(slots[i]);
                var cnt = InventorySlotStateAccessor.ReadCount(slots[i]);
                var st = InventorySlotStateAccessor.ReadState(slots[i]);

                ItemSO item = null;
                if (!string.IsNullOrEmpty(sid) && _database != null)
                    item = _database.Get(sid);

                _slots[i].Set(item, cnt, st);
            }

        }

        private bool Matches(ContainerId a, ContainerId b)
        {
            if (a.type != b.type) return false;

            bool eq = a.Equals(b);
            bool objMatch = !Equals(a.objectId, default(Fusion.NetworkId)) && Equals(a.objectId, b.objectId);
            bool ownerMatch = !Equals(a.ownerRef, default(Fusion.PlayerRef)) && Equals(a.ownerRef, b.ownerRef);

            return eq || objMatch || ownerMatch;
        }

        private void Clear()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s == null) continue;
                s.OnBeginDrag -= HandleBeginDrag;
                s.OnEndDrag -= HandleEndDrag;
                s.OnEnter -= HandleSlotEnter;
                s.OnExit -= HandleSlotExit;
                if (s.gameObject != null) Destroy(s.gameObject);
            }
            _slots.Clear();
        }

        public void RefreshPanel()
        {
            RebuildNow();
        }

        public void ClearInventory()
        {
            Clear();
        }
    }
}