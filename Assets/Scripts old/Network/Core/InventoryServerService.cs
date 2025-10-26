using System;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class InventoryServerService
    {
        private readonly ItemDatabaseSO _db;

        [Inject] private InventoryViewService _views;

        [Inject(Optional = true)] private InventorySnapshotBuilder _snapshots;
        [Inject(Optional = true)] private NetworkRunner _runner;
        [Inject(Optional = true)] private NetworkRunnerProvider _runnerProvider;
        [Inject(Optional = true)] private InventoryContainerRegistry _registry;


        private readonly Dictionary<ContainerId, HashSet<PlayerRef>> _watchers =
            new Dictionary<ContainerId, HashSet<PlayerRef>>(new ContainerIdComparer());

        public InventoryServerService(ItemDatabaseSO db) { _db = db; }


        public bool TryOpenContainer(PlayerRef requester, ContainerId id,
                             out ContainerSnapshot snap, out string reason)
        {
            snap = default; reason = null;

            if (!TryResolveContainer(id, out var container))
            { reason = "Container not found"; return false; }

            if (!container.CanPlayerAccess(requester))
            { reason = "Access denied"; return false; }

            if (_snapshots == null)
            { reason = "SnapshotBuilder missing"; return false; }

            var realId = container.Id;
            AddWatcher(realId, requester);

            snap = _snapshots.Build(realId);
            if (snap.slots == null)
            { reason = "Pending"; return false; }

            return true;
        }

        public bool TryCloseContainer(PlayerRef requester, ContainerId id, out string reason)
        {
            reason = null;
            if (!TryResolveContainer(id, out var container))
            { reason = "Container not found"; return false; }

            if (!container.CanPlayerAccess(requester))
            { reason = "Access denied"; return false; }

            RemoveWatcher(container.Id, requester);
            return true;
        }

        public IEnumerable<PlayerRef> Watchers(ContainerId id)
        {
            if (_watchers.TryGetValue(id, out var set) && set != null && set.Count > 0)
                return set;
            return System.Array.Empty<PlayerRef>();
        }

        public bool TryTransfer(
    PlayerRef actor,
    ContainerId fromId, int fromIdx,
    ContainerId toId, int toIdx,
    int amount,
    out ContainerDelta fromDelta, out ContainerDelta toDelta, out bool swapped, out string reason)
        {
            fromDelta = null;
            toDelta = null;
            swapped = false;
            reason = "unknown";

            if (fromIdx < 0 || toIdx < 0) { reason = "bad_index"; return false; }

            IInventoryContainer from = null, to = null;

            if (_registry != null)
            {
                if (!_registry.TryGet(fromId, out from) || from == null) from = null;
                if (!_registry.TryGet(toId, out to) || to == null) to = null;
            }
            if (from == null && !TryResolveContainerAny(fromId, out from)) { reason = "from_not_found"; return false; }
            if (to == null && !TryResolveContainerAny(toId, out to)) { reason = "to_not_found"; return false; }

            if (!from.CanPlayerAccess(actor) || !to.CanPlayerAccess(actor)) { reason = "no_access"; return false; }
            if (fromIdx >= from.Capacity || toIdx >= to.Capacity) { reason = "bad_index"; return false; }
            if (fromId.Equals(toId) && fromIdx == toIdx) { reason = "same_slot"; return false; }

            var sFromSrc = from.Slots[fromIdx];
            var sToSrc = to.Slots[toIdx];

            var fromItemId = InventorySlotStateAccessor.ReadId(sFromSrc);
            int fromCnt = Mathf.Max(0, InventorySlotStateAccessor.ReadCount(sFromSrc));
            if (string.IsNullOrEmpty(fromItemId) || fromCnt <= 0) { reason = "empty"; return false; }

            int moveReq = Mathf.Clamp(amount <= 0 ? fromCnt : amount, 1, fromCnt);

            var toItemId = InventorySlotStateAccessor.ReadId(sToSrc);
            int toCnt = Mathf.Max(0, InventorySlotStateAccessor.ReadCount(sToSrc));
            bool toEmpty = string.IsNullOrEmpty(toItemId) || toCnt <= 0;

            int MaxStackOf(string id)
            {
                if (string.IsNullOrEmpty(id) || _db == null) return 1;
                var so = _db.Get(id) as ItemSO;
                return Mathf.Max(1, so != null ? so.MaxStack : 1);
            }

            var newFrom = sFromSrc?.Clone() ?? new InventorySlotState();
            var newTo = sToSrc?.Clone() ?? new InventorySlotState();

            if (toEmpty)
            {
                if (!to.CanAccept(toIdx, sFromSrc)) { reason = "cannot_accept"; return false; }

                int cap = MaxStackOf(fromItemId);
                int moved = Mathf.Min(moveReq, cap);

                InventorySlotStateAccessor.WriteCount(newFrom, fromCnt - moved);
                if (InventorySlotStateAccessor.ReadCount(newFrom) <= 0)
                {
                    InventorySlotStateAccessor.WriteId(newFrom, null);
                    InventorySlotStateAccessor.WriteState(newFrom, null);
                }

                InventorySlotStateAccessor.WriteId(newTo, fromItemId);
                InventorySlotStateAccessor.WriteCount(newTo, moved);
                InventorySlotStateAccessor.WriteState(newTo, InventorySlotStateAccessor.ReadState(sFromSrc)?.Clone());
            }
            else if (toItemId == fromItemId)
            {
                int cap = MaxStackOf(fromItemId);
                int space = Mathf.Max(0, cap - toCnt);

                if (cap <= 1 || space <= 0)
                {
                    if (moveReq != fromCnt) { reason = "partial_swap_not_supported"; return false; }
                    if (!from.CanAccept(fromIdx, sToSrc) || !to.CanAccept(toIdx, sFromSrc)) { reason = "cannot_accept"; return false; }

                    var toCount = InventorySlotStateAccessor.ReadCount(sToSrc);
                    var toState = InventorySlotStateAccessor.ReadState(sToSrc)?.Clone();
                    var fromState = InventorySlotStateAccessor.ReadState(sFromSrc)?.Clone();

                    InventorySlotStateAccessor.WriteId(newFrom, toItemId);
                    InventorySlotStateAccessor.WriteCount(newFrom, toCount);
                    InventorySlotStateAccessor.WriteState(newFrom, toState);

                    InventorySlotStateAccessor.WriteId(newTo, fromItemId);
                    InventorySlotStateAccessor.WriteCount(newTo, fromCnt);
                    InventorySlotStateAccessor.WriteState(newTo, fromState);

                    swapped = true;
                }
                else
                {
                    if (!to.CanAccept(toIdx, sFromSrc)) { reason = "cannot_accept"; return false; }

                    int moved = Mathf.Min(moveReq, space);

                    InventorySlotStateAccessor.WriteCount(newFrom, fromCnt - moved);
                    if (InventorySlotStateAccessor.ReadCount(newFrom) <= 0)
                    {
                        InventorySlotStateAccessor.WriteId(newFrom, null);
                        InventorySlotStateAccessor.WriteState(newFrom, null);
                    }

                    InventorySlotStateAccessor.WriteCount(newTo, toCnt + moved);
                    if (InventorySlotStateAccessor.ReadState(newTo) == null)
                        InventorySlotStateAccessor.WriteState(newTo, InventorySlotStateAccessor.ReadState(sToSrc)?.Clone());
                }
            }
            else
            {
                if (moveReq != fromCnt) { reason = "partial_swap_not_supported"; return false; }
                if (!from.CanAccept(fromIdx, sToSrc) || !to.CanAccept(toIdx, sFromSrc)) { reason = "cannot_accept"; return false; }

                var toCount = InventorySlotStateAccessor.ReadCount(sToSrc);
                var toState = InventorySlotStateAccessor.ReadState(sToSrc)?.Clone();
                var fromState = InventorySlotStateAccessor.ReadState(sFromSrc)?.Clone();

                InventorySlotStateAccessor.WriteId(newFrom, toItemId);
                InventorySlotStateAccessor.WriteCount(newFrom, toCount);
                InventorySlotStateAccessor.WriteState(newFrom, toState);

                InventorySlotStateAccessor.WriteId(newTo, fromItemId);
                InventorySlotStateAccessor.WriteCount(newTo, fromCnt);
                InventorySlotStateAccessor.WriteState(newTo, fromState);

                swapped = true;
            }

            int prevFromVer = from.Version;
            int prevToVer = to.Version;

            from.SetSlot(fromIdx, newFrom);
            from.IncrementVersion();

            to.SetSlot(toIdx, newTo);
            to.IncrementVersion();

            bool sameContainer =
                from.Id.type == to.Id.type &&
                from.Id.ownerRef == to.Id.ownerRef &&
                from.Id.objectId.Equals(to.Id.objectId);

            if (sameContainer)
            {
                fromDelta = new ContainerDelta
                {
                    id = from.Id,
                    fromVersion = prevFromVer,
                    toVersion = from.Version,
                    changes = new[]
                    {
                new SlotChange { index = fromIdx, state = newFrom?.Clone() },
                new SlotChange { index = toIdx,   state = newTo?.Clone()   },
            }
                };
                toDelta = null;

                if (_views != null && fromDelta.changes != null && fromDelta.changes.Length > 0)
                    _views.BroadcastDelta(fromDelta.id, fromDelta.fromVersion, fromDelta.toVersion, fromDelta.changes);
            }
            else
            {
                fromDelta = new ContainerDelta
                {
                    id = from.Id,
                    fromVersion = prevFromVer,
                    toVersion = from.Version,
                    changes = new[] { new SlotChange { index = fromIdx, state = newFrom?.Clone() } }
                };

                toDelta = new ContainerDelta
                {
                    id = to.Id,
                    fromVersion = prevToVer,
                    toVersion = to.Version,
                    changes = new[] { new SlotChange { index = toIdx, state = newTo?.Clone() } }
                };

                if (_views != null)
                {
                    if (fromDelta.changes != null && fromDelta.changes.Length > 0)
                        _views.BroadcastDelta(fromDelta.id, fromDelta.fromVersion, fromDelta.toVersion, fromDelta.changes);
                    if (toDelta.changes != null && toDelta.changes.Length > 0)
                        _views.BroadcastDelta(toDelta.id, toDelta.fromVersion, toDelta.toVersion, toDelta.changes);
                }
            }

            reason = "ok";
            return true;
        }


        private static InventorySlotState[] CloneSlots(InventorySlotState[] src)
        {
            if (src == null) return null;
            var arr = new InventorySlotState[src.Length];
            for (int i = 0; i < src.Length; i++)
                arr[i] = src[i]?.Clone();
            return arr;
        }


        public bool TryAddItemToPlayer(
    PlayerRef player,
    string itemId,
    int amount,
    int ammo,
    out int left,
    out List<ContainerDelta> deltas,
    out string reason)
        {
            left = amount;
            deltas = null;
            reason = null;
            if (left <= 0) return true;

            var so = _db != null ? _db.Get(itemId) : null;
            if (so == null) { reason = "Item not found"; return false; }

            if (!TryGetPlayerContainers(player, out var quick, out var main))
            {
                reason = "Player containers missing";
                return false;
            }

            var list = new List<ContainerDelta>(4);

            if (so.priority == 1 && quick != null && quick.Slots != null && quick.Slots.Length > 0)
            {
                int sel = GetSelectedQuickIndex(player);
                if (sel >= 0 && sel < quick.Slots.Length)
                {
                    left = TryAddToExactSlotIfEmpty(quick, sel, so, left, ammo, out var dsel);
                    if (dsel != null) list.Add(dsel);
                }
            }

            var first = (so.priority == 1) ? quick : main;
            var second = (so.priority == 1) ? main : quick;

            left = TryAddToContainerForDeltas(first, so, left, ammo, out var d1);
            if (d1 != null) list.Add(d1);

            if (left > 0)
            {
                left = TryAddToContainerForDeltas(second, so, left, ammo, out var d2);
                if (d2 != null) list.Add(d2);
            }

            if (list.Count > 0) deltas = list;
            if (left > 0) reason = "Not enough space";

            return true;
        }

        private int TryAddToExactSlotIfEmpty(
    PlayerInventoryServer container,
    int index,
    ItemSO so,
    int left,
    int ammo,
    out ContainerDelta delta)
        {
            delta = null;
            if (container == null || container.Slots == null) return left;
            if (index < 0 || index >= container.Slots.Length) return left;
            if (left <= 0) return left;

            var s = container.Slots[index];
            var sid = InventorySlotStateAccessor.ReadId(s);
            var scnt = InventorySlotStateAccessor.ReadCount(s);
            int max = Mathf.Max(1, so.MaxStack);

            if (string.IsNullOrEmpty(sid) || scnt <= 0)
            {
                int put = Mathf.Min(left, max);
                var ns = s?.Clone() ?? new InventorySlotState();
                InventorySlotStateAccessor.WriteId(ns, so.Id);
                InventorySlotStateAccessor.WriteCount(ns, put);
                InventorySlotStateAccessor.WriteState(ns, new ItemState(ammo));

                int before = container.Version;
                container.SetSlot(index, ns);
                container.IncrementVersion();

                delta = new ContainerDelta
                {
                    id = container.Id,
                    fromVersion = before,
                    toVersion = container.Version,
                    changes = new[] { new SlotChange { index = index, state = ns?.Clone() } }
                };

                return left - put;
            }

            if (sid == so.Id && scnt < max)
            {
                int free = max - scnt;
                int put = Mathf.Min(left, free);

                var ns = s?.Clone() ?? new InventorySlotState();
                InventorySlotStateAccessor.WriteId(ns, sid);
                InventorySlotStateAccessor.WriteCount(ns, scnt + put);
                if (InventorySlotStateAccessor.ReadState(ns) == null)
                    InventorySlotStateAccessor.WriteState(ns, new ItemState(ammo));

                int before = container.Version;
                container.SetSlot(index, ns);
                container.IncrementVersion();

                delta = new ContainerDelta
                {
                    id = container.Id,
                    fromVersion = before,
                    toVersion = container.Version,
                    changes = new[] { new SlotChange { index = index, state = ns?.Clone() } }
                };

                return left - put;
            }

            return left;
        }

        private int GetSelectedQuickIndex(PlayerRef player)
        {
            var runner = ResolveRunner();
            if (runner == null) return -1;

            var all = UnityEngine.Object.FindObjectsOfType<InteractionController>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var ic = all[i];
                if (ic != null && ic.Object != null && ic.Object.InputAuthority == player)
                    return ic.SelectedQuickIndexNet;
            }

            if (runner.TryGetPlayerObject(player, out var no) && no != null)
            {
                var ic = no.GetComponentInChildren<InteractionController>(true);
                if (ic != null) return ic.SelectedQuickIndexNet;
            }

            return -1;
        }

        private static int TryAddToContainerForDeltas(
    PlayerInventoryServer container,
    ItemSO so,
    int left,
    int ammo,
    out ContainerDelta delta)
        {
            delta = null;
            if (container == null || left <= 0) return left;

            var slots = container.Slots;
            if (slots == null || slots.Length == 0) return left;

            int beforeVersion = container.Version;
            var changes = new List<SlotChange>(8);
            int max = Mathf.Max(1, so.MaxStack);

            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                var s = slots[i];
                var sid = InventorySlotStateAccessor.ReadId(s);
                if (!string.IsNullOrEmpty(sid) && sid == so.Id)
                {
                    int cnt = InventorySlotStateAccessor.ReadCount(s);
                    if (cnt < max)
                    {
                        int put = Mathf.Min(left, max - cnt);
                        var ns = s?.Clone() ?? new InventorySlotState();
                        InventorySlotStateAccessor.WriteId(ns, so.Id);
                        InventorySlotStateAccessor.WriteCount(ns, cnt + put);
                        if (InventorySlotStateAccessor.ReadState(ns) == null)
                            InventorySlotStateAccessor.WriteState(ns, new ItemState(ammo));

                        container.SetSlot(i, ns);
                        container.IncrementVersion();

                        left -= put;
                        changes.Add(new SlotChange { index = i, state = ns?.Clone() });
                    }
                }
            }

            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                var s = slots[i];
                var sid = InventorySlotStateAccessor.ReadId(s);
                var scnt = InventorySlotStateAccessor.ReadCount(s);
                if (string.IsNullOrEmpty(sid) || scnt <= 0)
                {
                    int put = Mathf.Min(left, max);
                    var ns = s?.Clone() ?? new InventorySlotState();
                    InventorySlotStateAccessor.WriteId(ns, so.Id);
                    InventorySlotStateAccessor.WriteCount(ns, put);
                    InventorySlotStateAccessor.WriteState(ns, new ItemState(ammo));

                    container.SetSlot(i, ns);
                    container.IncrementVersion();

                    left -= put;
                    changes.Add(new SlotChange { index = i, state = ns?.Clone() });
                }
            }

            if (changes.Count > 0)
            {
                delta = new ContainerDelta
                {
                    id = container.Id,
                    fromVersion = beforeVersion,
                    toVersion = container.Version,
                    changes = changes.ToArray()
                };
            }

            return left;
        }

        public bool TryConsumeAmmoFromQuick(
            PlayerRef player,
            int quickIndex,
            int amount,
            out int newAmmo,
            out ContainerDelta delta,
            out WeaponSO weaponSO,
            out string reason)
        {
            newAmmo = 0;
            delta = null;
            weaponSO = null;
            reason = null;

            if (!TryGetPlayerContainers(player, out var quick, out _))
            {
                reason = "no_containers";
                return false;
            }

            var slots = quick?.Slots;
            if (slots == null || quickIndex < 0 || quickIndex >= slots.Length)
            {
                reason = "bad_index";
                return false;
            }

            var slot = slots[quickIndex];
            var itemId = InventorySlotStateAccessor.ReadId(slot);
            if (string.IsNullOrEmpty(itemId))
            {
                reason = "empty_slot";
                return false;
            }

            var so = _db != null ? _db.Get(itemId) as WeaponSO : null;
            if (so == null)
            {
                reason = "not_a_weapon";
                return false;
            }

            var st = InventorySlotStateAccessor.ReadState(slot);
            int ammo = st?.ammo ?? 0;
            if (ammo < amount)
            {
                reason = "no ammo";
                return false;
            }

            var ns = slot?.Clone() ?? new InventorySlotState();
            var newState = (st != null) ? new ItemState(st) : new ItemState();
            newState.ammo = ammo - amount;
            InventorySlotStateAccessor.WriteId(ns, itemId);
            InventorySlotStateAccessor.WriteCount(ns, Mathf.Max(1, InventorySlotStateAccessor.ReadCount(slot)));
            InventorySlotStateAccessor.WriteState(ns, newState);

            int before = quick.Version;
            quick.SetSlot(quickIndex, ns);
            quick.IncrementVersion();

            newAmmo = newState.ammo;
            weaponSO = so;

            delta = new ContainerDelta
            {
                id = quick.Id,
                fromVersion = before,
                toVersion = quick.Version,
                changes = new[] { new SlotChange { index = quickIndex, state = ns?.Clone() } }
            };

            return true;
        }


        public bool TryReloadWeapon(
    PlayerRef player,
    int quickIndex,
    out int newAmmo,
    out List<ContainerDelta> deltas,
    out string reason)
        {
            newAmmo = 0; deltas = null; reason = null;

            if (!TryGetPlayerContainers(player, out var quick, out var main))
            { reason = "Player containers missing"; return false; }

            var qs = quick.Slots;
            if (qs == null || quickIndex < 0 || quickIndex >= qs.Length)
            { reason = "Bad quick index"; return false; }

            var s = qs[quickIndex];
            var wid = InventorySlotStateAccessor.ReadId(s);
            var wst = InventorySlotStateAccessor.ReadState(s);
            if (string.IsNullOrEmpty(wid)) { reason = "no weapon"; return false; }

            var wSo = _db.Get(wid) as WeaponSO;
            if (wSo == null) { reason = "not a weapon"; return false; }

            ReadWeaponAmmoSpec(wSo, out int magSize, out string ammoItemId);
            if (magSize <= 0) magSize = 30;
            if (string.IsNullOrEmpty(ammoItemId))
            { reason = "weapon has no ammo type"; return false; }

            int curAmmo = wst?.ammo ?? 0;
            int need = magSize - curAmmo;
            if (need <= 0) { newAmmo = curAmmo; deltas = new List<ContainerDelta>(0); return true; }

            var list = new List<ContainerDelta>(2);

            int taken = ConsumeFromContainerById(main, ammoItemId, need, out var mainDelta);
            if (mainDelta != null) list.Add(mainDelta);

            if (taken < need)
            {
                int rest = need - taken;
                int takenQ = ConsumeFromContainerById(quick, ammoItemId, rest, out var quickAmmoDelta, skipIndex: quickIndex);
                taken += takenQ;
                if (quickAmmoDelta != null) list.Add(quickAmmoDelta);
            }

            if (taken <= 0) { reason = "no ammo items"; return false; }

            int before = quick.Version;
            int toPut = Mathf.Min(need, taken);

            var ns = s?.Clone() ?? new InventorySlotState();
            var newState = wst != null ? new ItemState(wst) : new ItemState();
            newState.ammo = curAmmo + toPut;
            InventorySlotStateAccessor.WriteState(ns, newState);

            quick.SetSlot(quickIndex, ns);
            quick.IncrementVersion();

            list.Add(new ContainerDelta
            {
                id = quick.Id,
                fromVersion = before,
                toVersion = quick.Version,
                changes = new[] { new SlotChange { index = quickIndex, state = ns?.Clone() } }
            });

            deltas = list;
            newAmmo = newState.ammo;
            return true;
        }

        private static int ConsumeFromContainerById(
            PlayerInventoryServer container,
            string itemId,
            int need,
            out ContainerDelta delta,
            int skipIndex = -1)
        {
            delta = null;
            if (container == null || need <= 0) return 0;

            var slots = container.Slots;
            if (slots == null || slots.Length == 0) return 0;

            int before = container.Version;
            var changes = new List<SlotChange>(4);
            int taken = 0;

            for (int i = 0; i < slots.Length && taken < need; i++)
            {
                if (i == skipIndex) continue;

                var s = slots[i];
                var sid = InventorySlotStateAccessor.ReadId(s);
                var cnt = InventorySlotStateAccessor.ReadCount(s);

                if (sid == itemId && cnt > 0)
                {
                    int take = Mathf.Min(cnt, need - taken);

                    var ns = s?.Clone() ?? new InventorySlotState();
                    InventorySlotStateAccessor.WriteCount(ns, cnt - take);
                    if (InventorySlotStateAccessor.ReadCount(ns) <= 0)
                    {
                        InventorySlotStateAccessor.WriteId(ns, null);
                        InventorySlotStateAccessor.WriteState(ns, null);
                    }

                    container.SetSlot(i, ns);
                    container.IncrementVersion();
                    taken += take;
                    changes.Add(new SlotChange { index = i, state = ns?.Clone() });
                }
            }

            if (changes.Count > 0)
            {
                delta = new ContainerDelta
                {
                    id = container.Id,
                    fromVersion = before,
                    toVersion = container.Version,
                    changes = changes.ToArray()
                };
            }

            return taken;
        }


        private static InventorySlotState SafeGet(PlayerInventoryServer c, int idx)
        {
            var slots = c.Slots;
            if (slots == null || idx < 0 || idx >= slots.Length) return null;
            return slots[idx];
        }

        public bool TryGetPlayerContainers(PlayerRef player, out PlayerInventoryServer quick, out PlayerInventoryServer main)
        {
            quick = null;
            main = null;

            if (_registry != null)
            {
                var qId = ContainerId.PlayerQuickOf(player);
                var mId = ContainerId.PlayerMainOf(player);
                if (_registry.TryGet(qId, out var q) && q is PlayerInventoryServer qs) quick = qs;
                if (_registry.TryGet(mId, out var m) && m is PlayerInventoryServer ms) main = ms;
                if (quick != null && main != null) return true;
            }

            var runner = ResolveRunner();
            if (runner != null && runner.TryGetPlayerObject(player, out var playerNO) && playerNO != null)
            {
                var children = playerNO.GetComponentsInChildren<PlayerInventoryServer>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    var c = children[i];
                    if (c == null) continue;
                    if (c.Id.type == ContainerType.PlayerQuick) quick = c;
                    else if (c.Id.type == ContainerType.PlayerMain) main = c;
                }
                if (quick != null && main != null) return true;
            }

            var all = UnityEngine.Object.FindObjectsOfType<PlayerInventoryServer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || c.Object == null) continue;
                if (c.Object.InputAuthority != player) continue;
                if (c.Id.type == ContainerType.PlayerQuick) quick = c;
                else if (c.Id.type == ContainerType.PlayerMain) main = c;
            }

            return quick != null && main != null;
        }

        public bool TryResolveContainer(ContainerId id, out PlayerInventoryServer container)
        {
            container = null;

            if (_registry != null && _registry.TryGet(id, out var c) && c is PlayerInventoryServer ps)
            {
                container = ps;
                return true;
            }

            var all = UnityEngine.Object.FindObjectsOfType<PlayerInventoryServer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var srv = all[i];
                if (srv != null && srv.Id.ownerRef == id.ownerRef && srv.Id.type == id.type)
                {
                    container = srv;
                    return true;
                }
            }

            return false;
        }


        private NetworkRunner ResolveRunner()
        {
            if (_runner != null) return _runner;

            if (_runnerProvider != null)
            {
                var t = _runnerProvider.GetType();

                foreach (var propName in new[] { "Runner", "Current", "Instance", "Value" })
                {
                    var p = t.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
                    if (p != null && typeof(NetworkRunner).IsAssignableFrom(p.PropertyType))
                    {
                        if (p.GetValue(_runnerProvider) is NetworkRunner val1) return val1;
                    }
                }
                foreach (var methodName in new[] { "Get", "GetRunner", "GetCurrent", "Resolve", "GetOrCreate" })
                {
                    var m = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                    if (m != null && typeof(NetworkRunner).IsAssignableFrom(m.ReturnType))
                    {
                        if (m.Invoke(_runnerProvider, null) is NetworkRunner val2) return val2;
                    }
                }
            }

            return UnityEngine.Object.FindObjectOfType<NetworkRunner>();
        }

        private void AddWatcher(ContainerId id, PlayerRef viewer)
        {
            if (!_watchers.TryGetValue(id, out var set))
            {
                set = new HashSet<PlayerRef>();
                _watchers[id] = set;
            }
            set.Add(viewer);
        }

        private void RemoveWatcher(ContainerId id, PlayerRef viewer)
        {
            if (_watchers.TryGetValue(id, out var set))
            {
                set.Remove(viewer);
                if (set.Count == 0) _watchers.Remove(id);
            }
        }

        private static void ReadWeaponAmmoSpec(WeaponSO so, out int magSize, out string ammoItemId)
        {
            magSize = 0;
            ammoItemId = null;
            if (so == null) return;

            if (so.maxAmmo > 0) magSize = so.maxAmmo;

            if (so.ammoResource != null)
                ammoItemId = so.ammoResource.Id;

            if (magSize <= 0)
                magSize = ReadIntViaReflection(so, "clipSize", "magSize", "magazineSize", "MagSize", "ClipSize");

            if (string.IsNullOrEmpty(ammoItemId))
            {
                ammoItemId = ReadStringViaReflection(so, "ammoItemId", "ammoId", "AmmoId", "ammo");
                if (string.IsNullOrEmpty(ammoItemId))
                {
                    var itemSO = ReadObjectViaReflection(so, "ammoItem", "AmmoItem", "AmmoSO") as ItemSO;
                    if (itemSO != null) ammoItemId = itemSO.Id;

                    if (string.IsNullOrEmpty(ammoItemId))
                    {
                        var resSO = ReadObjectViaReflection(so, "ammoResource", "AmmoResource") as ResourceSO;
                        if (resSO != null) ammoItemId = resSO.Id;
                    }
                }
            }
        }

        private static int ReadIntViaReflection(object obj, params string[] names)
        {
            var t = obj.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var n in names)
            {
                var f = t.GetField(n, flags);
                if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
                var p = t.GetProperty(n, flags);
                if (p != null && p.CanRead && p.PropertyType == typeof(int)) return (int)p.GetValue(obj);
            }
            return 0;
        }

        private static string ReadStringViaReflection(object obj, params string[] names)
        {
            var t = obj.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var n in names)
            {
                var f = t.GetField(n, flags);
                if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(obj);
                var p = t.GetProperty(n, flags);
                if (p != null && p.CanRead && p.PropertyType == typeof(string)) return (string)p.GetValue(obj);
            }
            return null;
        }

        private static object ReadObjectViaReflection(object obj, params string[] names)
        {
            var t = obj.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var n in names)
            {
                var f = t.GetField(n, flags);
                if (f != null) return f.GetValue(obj);
                var p = t.GetProperty(n, flags);
                if (p != null && p.CanRead) return p.GetValue(obj);
            }
            return null;
        }

        private sealed class ContainerIdComparer : IEqualityComparer<ContainerId>
        {
            public bool Equals(ContainerId a, ContainerId b)
            {
                return a.type == b.type && a.ownerRef == b.ownerRef && a.objectId.Equals(b.objectId);
            }
            public int GetHashCode(ContainerId x)
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + (int)x.type;
                    h = h * 31 + x.ownerRef.GetHashCode();
                    h = h * 31 + x.objectId.GetHashCode();
                    return h;
                }
            }
        }


        public bool TryResolveGlobalIndex(PlayerRef player, int globalIndex, out PlayerInventoryServer container, out int slotIndex)
        {
            container = null;
            slotIndex = -1;

            if (!TryGetPlayerContainers(player, out var quick, out var main))
                return false;

            int qcap = quick?.Slots?.Length ?? 0;
            int mcap = main?.Slots?.Length ?? 0;
            int total = qcap + mcap;

            if (globalIndex < 0 || globalIndex >= total) return false;

            if (globalIndex < qcap)
            {
                container = quick;
                slotIndex = globalIndex;
            }
            else
            {
                container = main;
                slotIndex = globalIndex - qcap;
            }

            return container != null && slotIndex >= 0;
        }
        public bool TryRemove(PlayerRef player, ContainerId containerId, int slotIndex, int count, out List<ContainerDelta> deltas)
        {
            deltas = null;

            if (!TryResolveContainerAny(containerId, out var container)) return false;
            if (!container.CanPlayerAccess(player)) return false;

            var slots = container.Slots;
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return false;

            var s = slots[slotIndex];
            var id = InventorySlotStateAccessor.ReadId(s);
            var cur = InventorySlotStateAccessor.ReadCount(s);
            if (string.IsNullOrEmpty(id) || cur <= 0) return false;

            int remove = Mathf.Clamp(count, 1, cur);
            int before = container.Version;

            var ns = s?.Clone() ?? new InventorySlotState();
            InventorySlotStateAccessor.WriteCount(ns, cur - remove);
            if (InventorySlotStateAccessor.ReadCount(ns) <= 0)
            {
                InventorySlotStateAccessor.WriteId(ns, null);
                InventorySlotStateAccessor.WriteState(ns, null);
            }

            container.SetSlot(slotIndex, ns);
            container.IncrementVersion();

            var delta = new ContainerDelta
            {
                id = container.Id,
                fromVersion = before,
                toVersion = container.Version,
                changes = new[] { new SlotChange { index = slotIndex, state = ns?.Clone() } }
            };

            deltas = new List<ContainerDelta>(1) { delta };

            if (_views != null && delta.changes != null && delta.changes.Length > 0)
                _views.BroadcastDelta(delta.id, delta.fromVersion, delta.toVersion, delta.changes);

            return true;
        }

        public bool ServerClearPlayerContainers(PlayerRef player, out List<ContainerDelta> deltas)
        {
            deltas = null;
            if (!TryGetPlayerContainers(player, out var quick, out var main)) return false;

            var list = new List<ContainerDelta>(2);

            void Clear(PlayerInventoryServer c)
            {
                if (c == null) return;
                var before = c.Version;
                var changes = new List<SlotChange>(c.Capacity);
                var slots = c.Slots;
                if (slots == null) return;

                for (int i = 0; i < slots.Length; i++)
                {
                    var s = slots[i];
                    var id = InventorySlotStateAccessor.ReadId(s);
                    var cnt = InventorySlotStateAccessor.ReadCount(s);
                    if (!string.IsNullOrEmpty(id) && cnt > 0)
                    {
                        var ns = s?.Clone() ?? new InventorySlotState();
                        InventorySlotStateAccessor.WriteId(ns, null);
                        InventorySlotStateAccessor.WriteCount(ns, 0);
                        InventorySlotStateAccessor.WriteState(ns, null);

                        c.SetSlot(i, ns);
                        c.IncrementVersion();
                        changes.Add(new SlotChange { index = i, state = ns.Clone() });
                    }
                }

                if (changes.Count > 0)
                {
                    list.Add(new ContainerDelta
                    {
                        id = c.Id,
                        fromVersion = before,
                        toVersion = c.Version,
                        changes = changes.ToArray()
                    });
                }
            }

            Clear(quick);
            Clear(main);

            deltas = list;
            return true;
        }

        public bool TryResolveContainerAny(ContainerId id, out IInventoryContainer container)
        {
            container = null;

            if (_registry != null && _registry.TryGet(id, out var c) && c != null)
            {
                container = c;
                return true;
            }

            if ((id.type == ContainerType.PlayerQuick || id.type == ContainerType.PlayerMain) && id.ownerRef != PlayerRef.None)
            {
                if (TryGetPlayerContainers(id.ownerRef, out var quick, out var main))
                {
                    container = id.type == ContainerType.PlayerQuick ? quick : main;
                    if (container != null) return true;
                }
            }

            if (!id.objectId.Equals(default))
            {
                var runner = ResolveRunner();
                if (runner != null)
                {
                    var no = runner.FindObject(id.objectId);
                    if (no != null)
                    {
                        container = no.GetComponent<IInventoryContainer>() ?? no.GetComponentInChildren<IInventoryContainer>(true);
                        if (container != null) return true;
                    }
                }
            }

            return false;
        }

    }
}