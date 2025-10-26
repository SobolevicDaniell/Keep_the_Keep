using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Game.UI
{
    public sealed class InventoryTransferController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image _dragIcon;
        [SerializeField] private Canvas _canvas;

        [Header("UI Actions (for auto-wiring created EventSystem)")]
        [SerializeField] private InputActionAsset _uiActionsAsset;
        [SerializeField] private InputActionReference _uiPoint;
        [SerializeField] private InputActionReference _uiLeftClick;
        [SerializeField] private InputActionReference _uiRightClick;
        [SerializeField] private InputActionReference _uiMiddleClick;
        [SerializeField] private InputActionReference _uiScroll;
        [SerializeField] private InputActionReference _uiNavigate;
        [SerializeField] private InputActionReference _uiSubmit;
        [SerializeField] private InputActionReference _uiCancel;

        private InventoryPanel _playerPanel;
        private QuickSlotPanel _quickPanel;
        private OtherInventoryPanel _otherPanel;

        [Inject] private InventoryService _inv;
        [Inject] private ItemDatabaseSO _db;
        [Inject(Optional = true)] private InventoryClientFacade _facade;
        [Inject(Optional = true)] private PlayerStatsSO _stats;

        private Game.InteractionController _ic;
        private Game.PlayerRpcHandler _localRpc;

        private InventorySlotUI _srcSlot;
        private IInventoryPanelUI _srcPanel;
        private InventorySlotUI _hoverSlot;
        private IInventoryPanelUI _hoverPanel;

        private bool _subscribed;

        [SerializeField] private LayerMask _slotsLayers;

        private static readonly List<RaycastResult> _rayResults = new List<RaycastResult>(16);

        public void Initialize(InventoryService inv,
                               InventoryPanel playerPanel,
                               QuickSlotPanel quickPanel,
                               OtherInventoryPanel otherPanel,
                               InteractionController ic,
                               InventoryClientFacade facade)
        {
            _inv = inv ?? _inv;
            _playerPanel = playerPanel;
            _quickPanel = quickPanel;
            _otherPanel = otherPanel;
            _ic = ic ?? ResolveLocalIC();
            _facade = facade ?? _facade;

            EnsureFacadeReady();
            EnsureEventSystemAlive();

            if (_subscribed) UnsubscribeAll();
            SubscribeAll();
            _subscribed = true;

            if (_dragIcon != null)
            {
                _dragIcon.enabled = false;
                _dragIcon.raycastTarget = false;
                _dragIcon.transform.SetAsLastSibling();
            }
        }

        private void OnEnable()
        {
            EnsureEventSystemAlive();
        }

        private void OnDisable()
        {
            if (_subscribed) UnsubscribeAll();
            _subscribed = false;
        }

        private Game.InteractionController ResolveLocalIC()
        {
            if (_ic != null && _ic.Object != null && _ic.Object.HasInputAuthority) return _ic;

            var runner = FindObjectOfType<NetworkRunner>();
            if (runner != null && runner.TryGetPlayerObject(runner.LocalPlayer, out var po) && po != null)
            {
                _ic = po.GetComponent<InteractionController>() ?? po.GetComponentInChildren<InteractionController>(true);
                if (_ic != null) return _ic;
            }

            var all = FindObjectsOfType<Game.InteractionController>(true);
            foreach (var ic in all)
            {
                if (ic != null && ic.Object != null && ic.Object.HasInputAuthority)
                {
                    _ic = ic;
                    return _ic;
                }
            }
            return null;
        }

        private Game.PlayerRpcHandler ResolveLocalRpc()
        {
            if (_localRpc != null && _localRpc.Object != null && _localRpc.Object.HasInputAuthority) return _localRpc;

            var runner = FindObjectOfType<NetworkRunner>();
            if (runner != null && runner.TryGetPlayerObject(runner.LocalPlayer, out var po) && po != null)
            {
                _localRpc = po.GetComponent<PlayerRpcHandler>() ?? po.GetComponentInChildren<PlayerRpcHandler>(true);
                if (_localRpc != null) return _localRpc;
                _ic = po.GetComponentInChildren<InteractionController>(true) ?? _ic;
            }

            var all = FindObjectsOfType<Game.PlayerRpcHandler>(true);
            foreach (var h in all)
            {
                if (h != null && h.Object != null && h.Object.HasInputAuthority)
                {
                    _localRpc = h;
                    return _localRpc;
                }
            }
            return null;
        }

        private void SubscribeAll()
        {
            if (_playerPanel != null)
            {
                _playerPanel.OnSlotBeginDrag += OnBeginDrag;
                _playerPanel.OnSlotEndDrag += OnEndDrag;
                _playerPanel.OnSlotEnter += OnEnterSlot;
                _playerPanel.OnSlotExit += OnExitSlot;
            }
            if (_quickPanel != null)
            {
                _quickPanel.OnSlotBeginDrag += OnBeginDrag;
                _quickPanel.OnSlotEndDrag += OnEndDrag;
                _quickPanel.OnSlotEnter += OnEnterSlot;
                _quickPanel.OnSlotExit += OnExitSlot;
            }
            if (_otherPanel != null)
            {
                _otherPanel.OnSlotBeginDrag += OnBeginDrag;
                _otherPanel.OnSlotEndDrag += OnEndDrag;
                _otherPanel.OnSlotEnter += OnEnterSlot;
                _otherPanel.OnSlotExit += OnExitSlot;
            }
        }

        private void UnsubscribeAll()
        {
            if (_playerPanel != null)
            {
                _playerPanel.OnSlotBeginDrag -= OnBeginDrag;
                _playerPanel.OnSlotEndDrag -= OnEndDrag;
                _playerPanel.OnSlotEnter -= OnEnterSlot;
                _playerPanel.OnSlotExit -= OnExitSlot;
            }
            if (_quickPanel != null)
            {
                _quickPanel.OnSlotBeginDrag -= OnBeginDrag;
                _quickPanel.OnSlotEndDrag -= OnEndDrag;
                _quickPanel.OnSlotEnter -= OnEnterSlot;
                _quickPanel.OnSlotExit -= OnExitSlot;
            }
            if (_otherPanel != null)
            {
                _otherPanel.OnSlotBeginDrag -= OnBeginDrag;
                _otherPanel.OnSlotEndDrag -= OnEndDrag;
                _otherPanel.OnSlotEnter -= OnEnterSlot;
                _otherPanel.OnSlotExit -= OnExitSlot;
            }
        }

        private void Update()
        {
            if (_dragIcon == null || !_dragIcon.enabled) return;

            var pointer = GetPointerPosition();

            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                var cam = _canvas.worldCamera != null ? _canvas.worldCamera : Camera.main;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas.transform as RectTransform,
                    pointer,
                    cam,
                    out var local);
                (_dragIcon.transform as RectTransform).anchoredPosition = local;
            }
            else
            {
                (_dragIcon.transform as RectTransform).position = pointer;
            }
        }

        private void OnBeginDrag(InventorySlotUI slot)
        {
            if (slot == null || slot.Item == null) return;

            _srcSlot = slot;
            _srcPanel = slot.ParentPanel;

            if (_dragIcon != null)
            {
                _dragIcon.sprite = slot.Item.Icon;
                _dragIcon.enabled = true;
                _dragIcon.raycastTarget = false;
                _dragIcon.transform.SetAsLastSibling();
            }
        }

        private void OnEnterSlot(InventorySlotUI slot)
        {
            _hoverSlot = slot;
            _hoverPanel = slot?.ParentPanel;
        }

        private void OnExitSlot(InventorySlotUI slot)
        {
            if (_hoverSlot == slot)
            {
                _hoverSlot = null;
                _hoverPanel = null;
            }
        }

        private void OnEndDrag(InventorySlotUI _)
        {
            if (_dragIcon != null) _dragIcon.enabled = false;
            if (_srcSlot == null || _srcSlot.Item == null) { ResetDrag(); return; }

            if (_hoverSlot == null || _hoverPanel == null)
                TryPickSlotUnderPointer(out _hoverSlot, out _hoverPanel);

            bool overSlot = _hoverSlot != null && _hoverPanel != null;

            if (!overSlot)
            {
                TryWorldDropFromSource();
                ResetDrag();
                return;
            }

            EnsureFacadeReady();
            if (_facade == null) { ResetDrag(); return; }

            ContainerId fromId, toId;

            if (_srcPanel.Kind == PanelKind.Quick)
            {
                if (!TryResolveLocal(ContainerType.PlayerQuick, out fromId)) { ResetDrag(); return; }
            }
            else if (_srcPanel.Kind == PanelKind.Player)
            {
                if (!TryResolveLocal(ContainerType.PlayerMain, out fromId)) { ResetDrag(); return; }
            }
            else if (_srcPanel.Kind == PanelKind.Chest)
            {
                if (_otherPanel == null || _otherPanel.CurrentId.Equals(default)) { ResetDrag(); return; }
                fromId = _otherPanel.CurrentId;
            }
            else { ResetDrag(); return; }

            if (_hoverPanel.Kind == PanelKind.Quick)
            {
                if (!TryResolveLocal(ContainerType.PlayerQuick, out toId)) { ResetDrag(); return; }
            }
            else if (_hoverPanel.Kind == PanelKind.Player)
            {
                if (!TryResolveLocal(ContainerType.PlayerMain, out toId)) { ResetDrag(); return; }
            }
            else if (_hoverPanel.Kind == PanelKind.Chest)
            {
                if (_otherPanel == null || _otherPanel.CurrentId.Equals(default)) { ResetDrag(); return; }
                toId = _otherPanel.CurrentId;
            }
            else { ResetDrag(); return; }

            int fromIdx = _srcSlot.SlotIndex;
            int toIdx = _hoverSlot.SlotIndex;

            if (!IsIndexValid(_srcPanel.Kind, fromIdx) || !IsIndexValid(_hoverPanel.Kind, toIdx)) { ResetDrag(); return; }
            if (fromIdx == toIdx && fromId.Equals(toId)) { ResetDrag(); return; }

            int amount;
            if (_srcPanel.Kind == PanelKind.Quick)
            {
                var qs = _inv.GetQuickSlots();
                amount = Mathf.Max(1, (qs != null && fromIdx >= 0 && fromIdx < qs.Length && qs[fromIdx] != null) ? qs[fromIdx].Count : 1);
            }
            else if (_srcPanel.Kind == PanelKind.Player)
            {
                var inv = _inv.GetInventorySlots();
                amount = Mathf.Max(1, (inv != null && fromIdx >= 0 && fromIdx < inv.Length && inv[fromIdx] != null) ? inv[fromIdx].Count : 1);
            }
            else
            {
                amount = 1;
                if (_otherPanel != null && !_otherPanel.CurrentId.Equals(default))
                {
                    if (_facade.TryGetSnapshotResolved(_otherPanel.CurrentId, out var _, out var _, out var chestSlots) &&
                        chestSlots != null && fromIdx >= 0 && fromIdx < chestSlots.Length && chestSlots[fromIdx] != null)
                    {
                        var cnt = chestSlots[fromIdx].count;
                        amount = Mathf.Max(1, cnt);
                    }
                }
            }

            _facade.Transfer(fromId, fromIdx, toId, toIdx, amount, (ok, msg) => { });
            ResetDrag();
        }

        private void ResetDrag()
        {
            _srcSlot = null;
            _srcPanel = null;
            _hoverSlot = null;
            _hoverPanel = null;
        }

        private void TryWorldDropFromSource()
        {
            _ic = ResolveLocalIC();
            var rpc = ResolveLocalRpc();
            if (_ic == null || rpc == null) return;

            int localIdx = _srcSlot.SlotIndex;

            if (_srcPanel.Kind == PanelKind.Quick)
            {
                if (_inv == null) return;
                var qs = _inv.GetQuickSlots();
                if (qs == null || localIdx < 0 || localIdx >= qs.Length) return;
                var slotRef = qs[localIdx];
                if (slotRef == null || string.IsNullOrEmpty(slotRef.Id) || slotRef.Count <= 0) return;

                GetDropPoint(out var pos, out var fwd);
                rpc.RPC_RequestDrop(pos, fwd, localIdx, slotRef.Count);
                return;
            }

            if (_srcPanel.Kind == PanelKind.Player)
            {
                if (_inv == null) return;
                var inv = _inv.GetInventorySlots();
                if (inv == null || localIdx < 0 || localIdx >= inv.Length) return;
                var slotRef = inv[localIdx];
                if (slotRef == null || string.IsNullOrEmpty(slotRef.Id) || slotRef.Count <= 0) return;

                GetDropPoint(out var pos, out var fwd);
                int quickCap = GetQuickLen();
                rpc.RPC_RequestDrop(pos, fwd, quickCap + localIdx, slotRef.Count);
                return;
            }

            if (_srcPanel.Kind == PanelKind.Chest)
            {
                TryWorldDropFromOther();
                return;
            }
        }

        private void TryWorldDropFromOther()
        {
            _ic = ResolveLocalIC();
            var rpc = ResolveLocalRpc();
            if (_ic == null || rpc == null) return;
            if (_facade == null || _otherPanel == null) return;

            var probe = _otherPanel.CurrentId;
            if (probe.Equals(default)) return;

            if (!_facade.TryGetSnapshotResolved(probe, out var resolvedId, out var version, out var slots)) return;

            int localIdx = _srcSlot.SlotIndex;
            if (slots == null || localIdx < 0 || localIdx >= slots.Length) return;

            var s = slots[localIdx];
            if (s == null) return;

            var idStr = InventorySlotStateAccessor.ReadId(s);
            if (string.IsNullOrEmpty(idStr)) return;

            int amountInContainer = Mathf.Max(1, InventorySlotStateAccessor.ReadCount(s));

            if (!TryFindTargetForItem(idStr, amountInContainer, out var toId, out var toIdx, out var moveAmount))
            {
                GetDropPoint(out var pos0, out var fwd0);
                _facade.RequestDropFromContainer(pos0, fwd0, (int)resolvedId.type, localIdx, 0, resolvedId.ownerRef, resolvedId.objectId);
                return;
            }

            _facade.Transfer(resolvedId, localIdx, toId, toIdx, moveAmount, (ok, msg) =>
            {
                if (!ok) return;

                GetDropPoint(out var pos, out var fwd);
                int globalIdx = toId.type == ContainerType.PlayerQuick ? toIdx : GetQuickLen() + toIdx;
                rpc.RPC_RequestDrop(pos, fwd, globalIdx, moveAmount);
            });
        }

        private bool TryFindTargetForItem(string itemId, int needed, out ContainerId toId, out int toIdx, out int moveAmount)
        {
            moveAmount = 0;
            toIdx = -1;
            toId = default;

            int max = 1;
            var so = _db != null ? _db.Get(itemId) as ItemSO : null;
            if (so != null) max = Mathf.Max(1, so.MaxStack);

            if (TryResolveLocal(ContainerType.PlayerQuick, out var qId))
            {
                if (TryFindSlotForItemIn(ContainerType.PlayerQuick, itemId, max, out var idxQ, out var freeQ))
                {
                    toId = qId; toIdx = idxQ; moveAmount = Mathf.Min(needed, freeQ);
                    if (moveAmount > 0) return true;
                }
            }

            if (TryResolveLocal(ContainerType.PlayerMain, out var mId))
            {
                if (TryFindSlotForItemIn(ContainerType.PlayerMain, itemId, max, out var idxM, out var freeM))
                {
                    toId = mId; toIdx = idxM; moveAmount = Mathf.Min(needed, freeM);
                    if (moveAmount > 0) return true;
                }
            }

            return false;
        }

        private bool TryFindSlotForItemIn(ContainerType type, string itemId, int maxStack, out int index, out int freeCapacity)
        {
            index = -1;
            freeCapacity = 0;

            int selected = _ic != null ? _ic.SelectedQuickIndexNet : -1;

            if (type == ContainerType.PlayerQuick && selected >= 0)
            {
                if (TryGetLocalSlot(type, selected, out var sid, out var scnt))
                {
                    int free = string.IsNullOrEmpty(sid) ? maxStack : (sid == itemId ? Mathf.Max(0, maxStack - scnt) : 0);
                    if (free > 0) { index = selected; freeCapacity = free; return true; }
                }
            }

            int cap = type == ContainerType.PlayerQuick ? GetQuickLen() : GetMainLen();

            for (int i = 0; i < cap; i++)
            {
                if (i == selected && type == ContainerType.PlayerQuick) continue;
                if (TryGetLocalSlot(type, i, out var sid, out var scnt) && sid == itemId)
                {
                    int free = Mathf.Max(0, maxStack - scnt);
                    if (free > 0) { index = i; freeCapacity = free; return true; }
                }
            }

            for (int i = 0; i < cap; i++)
            {
                if (i == selected && type == ContainerType.PlayerQuick) continue;
                if (TryGetLocalSlot(type, i, out var sid, out var scnt))
                {
                    if (string.IsNullOrEmpty(sid) || scnt <= 0)
                    {
                        index = i; freeCapacity = maxStack; return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetLocalSlot(ContainerType type, int idx, out string id, out int count)
        {
            id = null; count = 0;

            if (type == ContainerType.PlayerQuick)
            {
                var qs = _inv?.GetQuickSlots();
                if (qs == null || idx < 0 || idx >= qs.Length) return false;
                var s = qs[idx];
                id = s?.Id;
                count = s != null ? s.Count : 0;
                return true;
            }
            else
            {
                var inv = _inv?.GetInventorySlots();
                if (inv == null || idx < 0 || idx >= inv.Length) return false;
                var s = inv[idx];
                id = s?.Id;
                count = s != null ? s.Count : 0;
                return true;
            }
        }

        private void GetDropPoint(out Vector3 pos, out Vector3 fwd)
        {
            _ic = ResolveLocalIC();
            if (_ic != null)
            {
                pos = _ic.GetDropPointPosition();
                fwd = _ic.transform.forward;
            }
            else
            {
                pos = Vector3.zero;
                fwd = Vector3.forward;
            }
        }

        private bool TryPickSlotUnderPointer(out InventorySlotUI slot, out IInventoryPanelUI panel)
        {
            slot = null; panel = null;

            var es = EventSystem.current;
            if (es == null) return false;

            var ev = new PointerEventData(es) { position = GetPointerPosition() };
            _rayResults.Clear();
            es.RaycastAll(ev, _rayResults);

            bool maskConfigured = _slotsLayers.value != 0;

            for (int i = 0; i < _rayResults.Count; i++)
            {
                var rr = _rayResults[i];
                if (!(rr.module is GraphicRaycaster)) continue;

                var go = rr.gameObject;
                if (!go) continue;

                var s = go.GetComponentInParent<InventorySlotUI>();
                if (s == null) continue;

                if (maskConfigured)
                {
                    if (!AnyParentMatches(go.transform, _slotsLayers) && !AnyParentMatches(s.transform, _slotsLayers))
                        continue;
                }

                slot = s;
                panel = s.ParentPanel ?? go.GetComponentInParent<IInventoryPanelUI>();
                return true;
            }

            return false;
        }

        private static bool AnyParentMatches(Transform t, LayerMask mask)
        {
            while (t != null)
            {
                if ((mask.value & (1 << t.gameObject.layer)) != 0)
                    return true;
                t = t.parent;
            }
            return false;
        }

        private void EnsureFacadeReady()
        {
            if (_facade == null) return;

            _ic = ResolveLocalIC();
            if (_ic == null || _ic.Object == null) return;

            var wantOwner = _ic.Object.InputAuthority;
            if (wantOwner == PlayerRef.None) return;

            bool needBind =
                _facade.localQuick.ownerRef != wantOwner ||
                _facade.localMain.ownerRef != wantOwner;

            if (!needBind) return;

            InventoryRpcRouter router = null;

            router = _ic.GetComponent<InventoryRpcRouter>() ?? _ic.GetComponentInParent<InventoryRpcRouter>();
            if (router == null && _ic.Runner != null &&
                _ic.Runner.TryGetPlayerObject(_ic.Object.InputAuthority, out var po1) && po1 != null)
            {
                router = po1.GetComponentInChildren<InventoryRpcRouter>(true);
            }

            if (router == null)
            {
                var all = FindObjectsOfType<InventoryRpcRouter>(true);
                for (int i = 0; i < all.Length && router == null; i++)
                    if (all[i].Object != null && all[i].Object.InputAuthority == wantOwner)
                        router = all[i];
            }

            if (router != null)
            {
                var poId = default(NetworkId);
                if (router.Runner != null &&
                    router.Runner.TryGetPlayerObject(_ic.Object.InputAuthority, out var po2) && po2 != null)
                {
                    poId = po2.Id;
                }

                _facade.SetLocal(wantOwner, router, poId);
            }
        }

        private bool TryResolveLocal(ContainerType type, out ContainerId id)
        {
            var want = (_ic != null && _ic.Object != null) ? _ic.Object.InputAuthority : PlayerRef.None;

            var owner = PlayerRef.None;
            if (_facade != null)
                owner = (type == ContainerType.PlayerQuick) ? _facade.localQuick.ownerRef : _facade.localMain.ownerRef;

            if (owner == PlayerRef.None || (want != PlayerRef.None && owner != want))
                owner = want;

            if (owner == PlayerRef.None) { id = default; return false; }

            id = new ContainerId { type = type, ownerRef = owner, objectId = default };
            return true;
        }

        private int GetQuickLen()
        {
            if (_facade != null)
            {
                var cap = _facade.GetLocalQuickCapacity();
                if (cap > 0) return cap;
            }
            var qs = _inv?.GetQuickSlots();
            if (qs != null && qs.Length > 0) return qs.Length;

            return _stats != null ? Mathf.Max(0, _stats.quickSlotsCount) : 0;
        }

        private int GetMainLen()
        {
            if (_facade != null)
            {
                var cap = _facade.GetLocalMainCapacity();
                if (cap > 0) return cap;
            }
            var inv = _inv?.GetInventorySlots();
            if (inv != null && inv.Length > 0) return inv.Length;

            return _stats != null ? Mathf.Max(0, _stats.inventorySlotsCount) : 0;
        }

        private int GetOtherLen()
        {
            if (_otherPanel == null) return 0;
            var id = _otherPanel.CurrentId;
            if (id.Equals(default)) return 0;
            return _facade != null ? _facade.GetCapacityImmediate(id) : 0;
        }

        private bool IsIndexValid(PanelKind kind, int idx)
        {
            if (idx < 0) return false;
            if (kind == PanelKind.Quick) return idx < GetQuickLen();
            if (kind == PanelKind.Player) return idx < GetMainLen();
            if (kind == PanelKind.Chest) return idx < GetOtherLen();
            return false;
        }

        private Vector2 GetPointerPosition()
        {
            if (Pointer.current != null) return Pointer.current.position.ReadValue();
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            if (Touchscreen.current != null)
            {
                var t = Touchscreen.current.primaryTouch;
                if (t.press.isPressed) return t.position.ReadValue();
            }
            return Vector2.zero;
        }

        private void EnsureEventSystemAlive()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var ui = go.AddComponent<InputSystemUIInputModule>();

            if (_uiActionsAsset != null) ui.actionsAsset = _uiActionsAsset;
            if (_uiPoint != null) ui.point = _uiPoint;
            if (_uiLeftClick != null) ui.leftClick = _uiLeftClick;
            if (_uiRightClick != null) ui.rightClick = _uiRightClick;
            if (_uiMiddleClick != null) ui.middleClick = _uiMiddleClick;
            if (_uiScroll != null) ui.scrollWheel = _uiScroll;
            if (_uiNavigate != null) ui.move = _uiNavigate;
            if (_uiSubmit != null) ui.submit = _uiSubmit;
            if (_uiCancel != null) ui.cancel = _uiCancel;
        }
    }
}
