using System;
using System.Reflection;
using Fusion;
using UnityEngine;
using Zenject;
using System.Collections.Generic;


namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerRpcHandler : NetworkBehaviour
    {

        private InteractionController _ic;
        private ItemDatabaseSO _db;
        private InventoryService _inventory;

        [Inject(Optional = true)] private InventoryServerService _invServer;
        [Inject(Optional = true)] private InventoryRpcRouter _invRouter;

        [Networked] private TickTimer _fireCooldown { get; set; }

        public override void Spawned()
        {
            _invRouter = ResolveServerRouter();

            if (Object.HasStateAuthority)
            {
                var ic = GetComponent<InteractionController>();
                ic?.ServerSetSelectedQuickIndex(-1);
            }

            if (Object.HasInputAuthority && _invRouter != null)
            {
                StartCoroutine(_invRouter.RetryFullResync());
            }
        }

        public void Construct(ItemDatabaseSO db, InteractionController ic, InventoryService inventory)
        {
            _db = db;
            _ic = ic;
            _inventory = inventory;
        }

        private InventoryRpcRouter ResolveServerRouter()
        {
            if (_invRouter != null) return _invRouter;
            var r = Runner != null && Runner.TryGetPlayerObject(Object.InputAuthority, out var po) && po != null
                ? (po.GetComponent<InventoryRpcRouter>() ?? po.GetComponentInChildren<InventoryRpcRouter>(true))
                : null;
            if (r != null) _invRouter = r;
            return r;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_UnEquipItem(RpcInfo info = default)
        {
            if (!Object.HasStateAuthority) return;

            var ic = _ic ??= GetComponent<InteractionController>();
            if (ic == null) return;

            ic.handItemController?.UnEquipItemServer();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestReload(int clientSlotIndex, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _invServer == null) return;

            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            var ic = _ic ??= GetComponent<InteractionController>();
            if (ic == null) return;

            int selected = ic.SelectedQuickIndexNet;
            if (selected < 0) selected = Mathf.Max(-1, clientSlotIndex);
            if (selected < 0) return;

            if (_invServer.TryReloadWeapon(actor, selected, out var newAmmo, out var deltas, out var reason))
            {
                var router = ResolveServerRouter();
                if (router != null && deltas != null)
                {
                    for (int i = 0; i < deltas.Count; i++)
                        router.BroadcastDeltaFromServer(deltas[i]);
                }
                RPC_SetSlotAmmo(selected, newAmmo);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPlaceObject(string itemId, Vector3 position, Quaternion rotation, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || Runner == null || _db == null) return;
            var so = _db.Get(itemId);
            if (so == null) return;

            if (!TryGetNetworkPrefab(so, out var placeablePrefab,
                "PlaceableNetwork", "PlaceNetwork", "WorldPlaceable", "WorldPrefab",
                "PlaceablePrefab", "PlacePrefab", "WorldPlace", "Prefab"))
                return;

            Runner.Spawn(placeablePrefab, position, rotation, Object.InputAuthority);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestDrop(Vector3 pos, Vector3 fwd, int fromGlobalIndex, int count, RpcInfo info = default)
        {
            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            if (!Object.HasStateAuthority || Runner == null || _invServer == null || _db == null)
                return;

            var ic = _ic ??= GetComponent<InteractionController>();
            if (ic == null) return;

            PlayerInventoryServer container = null;
            int slotIndex = -1;

            if (fromGlobalIndex >= 0)
            {
                if (!_invServer.TryResolveGlobalIndex(actor, fromGlobalIndex, out container, out slotIndex))
                    return;
            }
            else
            {
                int selected = ic.SelectedQuickIndexNet;
                if (selected < 0) return;
                if (!_invServer.TryResolveGlobalIndex(actor, selected, out container, out slotIndex))
                    return;
            }

            if (container == null || slotIndex < 0 || container.Slots == null || slotIndex >= container.Slots.Length)
                return;

            var slot = container.Slots[slotIndex];
            var itemId = InventorySlotStateAccessor.ReadId(slot);
            if (string.IsNullOrEmpty(itemId))
                return;

            var so = _db.Get(itemId);
            if (so == null)
                return;

            var st = InventorySlotStateAccessor.ReadState(slot);
            int ammo = st?.ammo ?? 0;

            int currentCount = InventorySlotStateAccessor.ReadCount(slot);
            int dropCount = Mathf.Clamp(count > 0 ? count : currentCount, 1, currentCount);

            if (!_invServer.TryRemove(actor, container.Id, slotIndex, dropCount, out var deltas))
                return;

            var router = ResolveServerRouter();
            if (router != null && deltas != null)
            {
                for (int i = 0; i < deltas.Count; i++)
                    router.BroadcastDeltaFromServer(deltas[i]);
            }

            ServerRefreshHandsFromSelectedQuick(actor);

            if (!TryGetNetworkPrefab(so, out var pickablePrefab,
                "PickableNetwork", "PickupNetwork", "WorldDrop", "WorldPrefab",
                "PickablePrefab", "PickupPrefab", "WorldPickable", "Prefab"))
                return;

            var dir = fwd.sqrMagnitude > 0f ? fwd.normalized : Vector3.forward;
            var rot = Quaternion.LookRotation(dir);

            var spawned = Runner.Spawn(
                pickablePrefab, pos, rot, PlayerRef.None,
                onBeforeSpawned: (runner, netObj) =>
                {
                    var pick = netObj.GetComponentInChildren<PickableItem>(true);
                    if (pick != null)
                    {
                        pick.ServerInit(itemId, dropCount, ammo);
                    }
                    else
                    {
                        TryInitWorldItemDeep(netObj.gameObject, itemId, dropCount, ammo);
                    }
                });

            if (spawned != null && spawned.TryGetComponent<Rigidbody>(out var rb))
            {
                float force = ic != null ? ic.ThrowForce : 5f;
                rb.AddForce(dir * force, ForceMode.VelocityChange);
            }
        }



        private void TryInitWorldItemDeep(GameObject root, string itemId, int count, int ammo)
        {
            var pick = root.GetComponentInChildren<PickableItem>(true);
            if (pick != null)
            {
                if (Runner != null && HasStateAuthority) pick.ServerInit(itemId, count, ammo);
                else pick.Initialize(itemId, count, ammo);
                return;
            }

            var comps = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i]; if (c == null) continue;
                var t = c.GetType();
                WriteString(t, c, itemId, "ItemId", "itemId", "Id", "id");
                WriteInt(t, c, count, "Count", "count", "Stack", "stack");
                WriteInt(t, c, ammo, "Ammo", "ammo", "Bullets", "bullets");
            }
        }


        private void ServerTryPick(NetworkObject no, PlayerRef actor)
        {
            if (no == null || _invServer == null) return;

            if (!TryReadWorldItem(no, out var itemId, out var amount, out var ammo))
                return;

            if (_invServer.TryAddItemToPlayer(actor, itemId, amount, ammo, out var left, out var deltas, out var reason))
            {
                var router = _invRouter ?? ResolveServerRouter();
                if (router != null && deltas != null)
                {
                    for (int i = 0; i < deltas.Count; i++)
                        router.BroadcastDeltaFromServer(deltas[i]);
                }

                var extra = ServerPrioritizeSelectedQuick(actor, itemId);
                if (router != null && extra != null)
                {
                    for (int i = 0; i < extra.Count; i++)
                        router.BroadcastDeltaFromServer(extra[i]);
                }

                var pick = no.GetComponentInChildren<PickableItem>(true);
                if (pick == null) return;

                if (left < amount)
                {
                    if (left <= 0)
                    {
                        Debug.Log($"ServerPickResult actor={actor} item={itemId} consumed_all");
                        Runner.Despawn(no);
                    }
                    else
                    {
                        Debug.Log($"ServerPickResult actor={actor} item={itemId} newCountN={left}");
                        pick.SetCount(left);
                    }
                }
            }
            else
            {
                Debug.Log($"ServerPickFailed actor={actor} item={itemId} reason={reason}");
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestPickByTarget(NetworkId targetId, float maxDist, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _invServer == null || Runner == null) return;

            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            if (!Runner.TryFindObject(targetId, out var no) || no == null) return;

            var pick = no.GetComponentInChildren<PickableItem>(true);
            if (pick == null) return;

            var ic = _ic ??= GetComponent<InteractionController>();
            float range = ic != null ? ic.range : Mathf.Max(1f, maxDist);
            var pos = transform.position;
            if ((no.transform.position - pos).sqrMagnitude > (range + 0.5f) * (range + 0.5f)) return;

            Debug.Log($"ServerPickAttempt actor={actor} item={pick.GetItemId()} countN={pick.GetCount()} net={no.Id}");

            ServerTryPick(no, actor);
        }



        private List<ContainerDelta> ServerPrioritizeSelectedQuick(PlayerRef actor, string itemId)
        {
            if (Runner == null || _invServer == null || string.IsNullOrEmpty(itemId)) return null;
            if (!Runner.TryGetPlayerObject(actor, out var playerNO) || playerNO == null) return null;

            var ic = playerNO.GetComponentInChildren<InteractionController>(true);
            if (ic == null) return null;

            int sel = ic.SelectedQuickIndexNet;
            if (sel < 0) return null;

            if (!_invServer.TryResolveGlobalIndex(actor, sel, out var quick, out var qIdx)) return null;
            if (quick == null || quick.Slots == null || qIdx < 0 || qIdx >= quick.Slots.Length) return null;

            var qState = quick.Slots[qIdx];
            var qId = InventorySlotStateAccessor.ReadId(qState);
            var qCnt = InventorySlotStateAccessor.ReadCount(qState);

            bool canAccept = string.IsNullOrEmpty(qId) || qId == itemId;
            if (!canAccept) return null;

            PlayerInventoryServer main = null;
            TryGetPlayerContainers(actor, out var quickC, out main);

            int srcIdx = -1;
            PlayerInventoryServer src = null;

            if (quickC != null && quickC.Slots != null)
            {
                for (int i = 0; i < quickC.Slots.Length; i++)
                {
                    if (i == qIdx) continue;
                    var s = quickC.Slots[i];
                    if (InventorySlotStateAccessor.ReadId(s) == itemId && InventorySlotStateAccessor.ReadCount(s) > 0)
                    {
                        src = quickC; srcIdx = i; break;
                    }
                }
            }

            if (src == null && main != null && main.Slots != null)
            {
                for (int i = 0; i < main.Slots.Length; i++)
                {
                    var s = main.Slots[i];
                    if (InventorySlotStateAccessor.ReadId(s) == itemId && InventorySlotStateAccessor.ReadCount(s) > 0)
                    {
                        src = main; srcIdx = i; break;
                    }
                }
            }

            if (src == null || srcIdx < 0) return null;

            int amount = InventorySlotStateAccessor.ReadCount(src.Slots[srcIdx]);
            if (amount <= 0) return null;

            if (_invServer.TryTransfer(
                    actor,
                    src.Id, srcIdx,
                    quick.Id, qIdx,
                    amount,
                    out var fromDelta, out var toDelta, out var swapped, out var reason))
            {
                var list = new List<ContainerDelta>(2);
                if (fromDelta != null) list.Add(fromDelta);
                if (toDelta != null) list.Add(toDelta);
                return list.Count > 0 ? list : null;
            }

            return null;
        }

      

        private Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                var r = FindChildRecursive(c, name);
                if (r != null) return r;
            }
            return null;
        }


        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestShoot(string itemId, int clientSlotIndex, Vector3 clientMuzzlePos, Vector3 clientMuzzleFwd, int seed, bool isAuto, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || _invServer == null || Runner == null) return;

            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            var ic = _ic ??= GetComponent<InteractionController>();
            if (ic == null) return;

            int selected = ic.SelectedQuickIndexNet;
            if (selected < 0) selected = Mathf.Max(-1, clientSlotIndex);
            if (selected < 0) return;

            if (!_fireCooldown.ExpiredOrNotRunning(Runner)) return;

            if (!_invServer.TryConsumeAmmoFromQuick(actor, selected, 1, out var newAmmo, out var quickDelta, out var weaponSO, out var reason))
                return;

            // КД по серверу
            float rate = 0f;
            if (weaponSO != null)
                rate = isAuto ? weaponSO.fireRate : (weaponSO.fireRateSingle > 0f ? weaponSO.fireRateSingle : weaponSO.fireRate);
            if (rate <= 0f) rate = 10f;
            _fireCooldown = TickTimer.CreateFromSeconds(Runner, 1f / Mathf.Max(0.01f, rate));

            // 1) Нормализуем forward, если нужно
            Vector3 forward = NormalizeSafe(clientMuzzleFwd, ic != null ? ic.transform.forward : Vector3.forward);

            // 2) Берём origin от клиента, если он валиден; иначе — минимальный фоллбек
            Vector3 origin = IsFinite(clientMuzzlePos)
                ? clientMuzzlePos
                : ResolveServerFallbackOrigin(ic, forward);

            // Спавн снаряда / хитскан
            if (weaponSO != null && weaponSO.bulletPrefab != null)
            {
                var prefabNo = weaponSO.bulletPrefab.GetComponent<NetworkObject>();
                if (prefabNo != null)
                {
                    Runner.Spawn(
                        prefabNo,
                        origin,
                        Quaternion.LookRotation(forward),
                        actor,
                        (runner, obj) =>
                        {
                            var b = obj.GetComponent<Bullet>();
                            if (b != null)
                            {
                                b.Initialize(Mathf.RoundToInt(weaponSO.bulletDamage));
                                b.InitializeVelocity(forward * weaponSO.bulletSpeed);
                                b.SetMass(Mathf.Max(0.01f, weaponSO.bulletMass));
                            }

                            var dds = obj.GetComponentsInChildren<BulletDamageDealer>(true);
                            for (int i = 0; i < dds.Length; i++)
                                dds[i].Configure(Mathf.RoundToInt(weaponSO.bulletDamage), actor);

                            if (obj.TryGetComponent<Rigidbody>(out var rb))
                                rb.linearVelocity = forward * weaponSO.bulletSpeed;
                        });
                }
            }
            else
            {
                if (Runner.LagCompensation.Raycast(origin, forward, 1000f, actor, out var hit))
                {
                    var damageable = hit.GameObject != null ? hit.GameObject.GetComponentInParent<IDamageable>() : null;
                    if (damageable != null)
                    {
                        int dmg = weaponSO != null ? Mathf.RoundToInt(weaponSO.bulletDamage) : 10;
                        var infoDmg = new DamageInfo(dmg, DamageKind.Bullet, hit.Point, forward, actor);
                        damageable.ApplyDamage(infoDmg);
                    }
                }
            }

            var router = ResolveServerRouter();
            if (router != null && quickDelta != null)
                router.BroadcastDeltaFromServer(quickDelta);

            RPC_SetSlotAmmo(selected, newAmmo);
        }

        private static bool IsFinite(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        private static Vector3 NormalizeSafe(Vector3 v, Vector3 fallback)
        {
            if (!IsFinite(v)) return fallback.normalized;
            float m2 = v.sqrMagnitude;
            if (m2 < 1e-10f) return fallback.normalized;
            return v / Mathf.Sqrt(m2);
        }

        private static Vector3 ResolveServerFallbackOrigin(InteractionController ic, Vector3 forward)
        {
            var basePos = ic != null ? ic.transform.position : Vector3.zero;
            return basePos + (forward.sqrMagnitude > 0f ? forward : Vector3.forward) * 0.5f;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_SetSlotAmmo(int slotIndex, int ammo, RpcInfo info = default)
        {
            if (_inventory == null) return;
            var qs = _inventory.GetQuickSlots();
            if (qs == null || slotIndex < 0 || slotIndex >= qs.Length) return;

            var s = qs[slotIndex];
            if (s == null) return;
            if (s.State == null) s.State = new ItemState();
            s.State.ammo = ammo;

            _inventory.RaiseQuickSlotsChanged();
        }



        private static bool TryGetNetworkPrefab(ScriptableObject so, out NetworkObject prefab, params string[] candidateNames)
        {
            prefab = null;
            if (so == null) return false;

            if (so is ItemSO iso && iso.Prefab != null)
            {
                prefab = iso.Prefab.GetComponent<NetworkObject>();
                if (prefab != null) return true;
            }

            var t = so.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var name in candidateNames)
            {
                var f = t.GetField(name, flags);
                if (f != null)
                {
                    var v = f.GetValue(so);
                    if (v is NetworkObject no1) { prefab = no1; return true; }
                    if (v is GameObject go1) { prefab = go1.GetComponent<NetworkObject>(); if (prefab != null) return true; }
                }

                var p = t.GetProperty(name, flags);
                if (p != null && p.CanRead)
                {
                    var v = p.GetValue(so);
                    if (v is NetworkObject no2) { prefab = no2; return true; }
                    if (v is GameObject go2) { prefab = go2.GetComponent<NetworkObject>(); if (prefab != null) return true; }
                }
            }

            return false;
        }

      
        private static void WriteString(Type t, object obj, string value, params string[] names)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int i = 0; i < names.Length; i++)
            {
                var f = t.GetField(names[i], BF);
                if (f != null && f.FieldType == typeof(string)) { f.SetValue(obj, value); return; }
                var p = t.GetProperty(names[i], BF);
                if (p != null && p.CanWrite && p.PropertyType == typeof(string)) { p.SetValue(obj, value); return; }
            }
        }

        private static void WriteInt(Type t, object obj, int value, params string[] names)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (int i = 0; i < names.Length; i++)
            {
                var f = t.GetField(names[i], BF);
                if (f != null && f.FieldType == typeof(int)) { f.SetValue(obj, value); return; }
                var p = t.GetProperty(names[i], BF);
                if (p != null && p.CanWrite && p.PropertyType == typeof(int)) { p.SetValue(obj, value); return; }
            }
        }

        bool TryReadWorldItem(NetworkObject obj, out string itemId, out int count, out int ammo)
        {
            itemId = null;
            count = 0;
            ammo = 0;
            if (obj == null) return false;

            var pick = obj.GetComponentInParent<PickableItem>() ?? obj.GetComponentInChildren<PickableItem>(true);
            if (pick == null) return false;

            itemId = pick.GetItemId();
            count = pick.GetCount();
            ammo = pick.GetAmmo();

            return !string.IsNullOrEmpty(itemId) && count > 0;
        }

        
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RequestEquipQuickSlot(int quickIndex, RpcInfo info = default)
        {
            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            var ic = GetComponent<InteractionController>();
            if (ic == null || _invServer == null) return;

            int current = ic.SelectedQuickIndexNet;
            if (quickIndex >= 0 && current == quickIndex)
            {
                ic.ServerSetSelectedQuickIndex(-1);
                ic.handItemController?.UnEquipItemServer();
                return;
            }

            ic.ServerSetSelectedQuickIndex(Mathf.Max(-1, quickIndex));

            string itemId = null;

            if (quickIndex >= 0 && _invServer.TryResolveGlobalIndex(actor, quickIndex, out var container, out int slotIndex))
            {
                var slots = container.Slots;
                if (slots != null && slotIndex >= 0 && slotIndex < slots.Length)
                {
                    var s = slots[slotIndex];
                    itemId = InventorySlotStateAccessor.ReadId(s);
                }
            }

            if (string.IsNullOrEmpty(itemId))
                ic.handItemController?.UnEquipItemServer();
            else
                ic.handItemController?.EquipItemServer(itemId);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_RefreshSelectedQuick(RpcInfo info = default)
        {
            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            var ic = GetComponent<InteractionController>();
            if (ic == null || _invServer == null) return;

            int selected = ic.SelectedQuickIndexNet;

            string itemId = null;

            if (selected >= 0 && _invServer.TryResolveGlobalIndex(actor, selected, out var container, out int slotIndex))
            {
                var slots = container.Slots;
                if (slots != null && slotIndex >= 0 && slotIndex < slots.Length)
                {
                    var s = slots[slotIndex];
                    itemId = InventorySlotStateAccessor.ReadId(s);
                }
            }

            if (string.IsNullOrEmpty(itemId))
                ic.handItemController?.UnEquipItemServer();
            else
                ic.handItemController?.EquipItemServer(itemId);
        }


        public bool TryGetPlayerContainers(PlayerRef player, out PlayerInventoryServer quick, out PlayerInventoryServer main)
        {
            quick = null; main = null;
            var runner = Runner != null ? Runner : FindObjectOfType<NetworkRunner>();
            if (runner == null) return false;

            if (!runner.TryGetPlayerObject(player, out var playerNO) || playerNO == null)
                return false;

            var all = playerNO.GetComponentsInChildren<PlayerInventoryServer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null) continue;
                if (c.Id.type == ContainerType.PlayerQuick) quick = c;
                else if (c.Id.type == ContainerType.PlayerMain) main = c;
            }
            return quick != null && main != null;
        }

        public void ServerRefreshHandsFromSelectedQuick(PlayerRef actor)
        {
            if (Runner == null) return;
            if (!Runner.TryGetPlayerObject(actor, out var playerNO) || playerNO == null) return;

            var ic = playerNO.GetComponentInChildren<InteractionController>(true);
            if (ic == null) return;

            int sel = ic.SelectedQuickIndexNet;
            if (sel < 0)
            {
                ic.handItemController?.UnEquipItemServer();
                return;
            }

            if (!_invServer.TryResolveGlobalIndex(actor, sel, out var container, out int slotIndex))
            {
                ic.handItemController?.UnEquipItemServer();
                return;
            }

            var slots = container.Slots;
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            {
                ic.handItemController?.UnEquipItemServer();
                return;
            }

            var id = InventorySlotStateAccessor.ReadId(slots[slotIndex]);
            var cnt = InventorySlotStateAccessor.ReadCount(slots[slotIndex]);

            if (string.IsNullOrEmpty(id) || cnt <= 0)
                ic.handItemController?.UnEquipItemServer();
            else
                ic.handItemController?.EquipItemServer(id);
        }



        public void ServerDropOverflow(string itemId, int count, int ammo)
        {
            if (!Object.HasStateAuthority || Runner == null || _db == null) return;

            if (!TryGetNetworkPrefab(_db.Get(itemId), out var pickablePrefab,
                "PickableNetwork", "PickupNetwork", "WorldDrop", "WorldPrefab",
                "PickablePrefab", "PickupPrefab", "WorldPickable", "Prefab"))
                return;

            var pos = _ic != null ? _ic.GetDropPointPosition() : transform.position + transform.forward;
            var fwd = _ic != null ? _ic.GetDropForward() : transform.forward;
            var dir = fwd.sqrMagnitude > 0f ? fwd.normalized : Vector3.forward;
            var rot = Quaternion.LookRotation(dir);

            var spawned = Runner.Spawn(
                pickablePrefab, pos, rot, PlayerRef.None,
                onBeforeSpawned: (runner, netObj) =>
                {
                    var pick = netObj.GetComponentInChildren<PickableItem>(true);
                    if (pick != null)
                    {
                        pick.ServerInit(itemId, count, ammo);
                    }
                    else
                    {
                        TryInitWorldItemDeep(netObj.gameObject, itemId, count, ammo);
                    }
                });

            if (spawned != null && spawned.TryGetComponent<Rigidbody>(out var rb))
            {
                float force = _ic != null ? _ic.ThrowForce : 5f;
                rb.AddForce(dir * force, ForceMode.VelocityChange);
            }
        }

        [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestDropFromContainer(Vector3 pos, Vector3 dir, int type, int slotIndex, byte flags, PlayerRef ownerRef, NetworkId objectId, RpcInfo info = default)
        {
            var actor = info.Source;
            if (actor == PlayerRef.None) actor = Object.InputAuthority;
            if (actor != Object.InputAuthority) return;

            if (!Object.HasStateAuthority || Runner == null || _invServer == null || _db == null) return;

            var ic = _ic ??= GetComponent<InteractionController>();
            if (ic == null) return;

            var cid = new ContainerId { type = (ContainerType)type, ownerRef = ownerRef, objectId = objectId };

            IInventoryContainer container = null;
            if (!_invServer.TryResolveContainerAny(cid, out container)) return;
            if (!container.CanPlayerAccess(actor)) return;

            var slots = container.Slots;
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;

            var slot = slots[slotIndex];
            var itemId = InventorySlotStateAccessor.ReadId(slot);
            if (string.IsNullOrEmpty(itemId)) return;

            var st = InventorySlotStateAccessor.ReadState(slot);
            int ammo = st?.ammo ?? 0;

            int currentCount = Mathf.Max(1, InventorySlotStateAccessor.ReadCount(slot));

            const byte DROP_ONE = 1 << 0;
            const byte DROP_HALF = 1 << 1;
            int dropCount = currentCount;
            if ((flags & DROP_ONE) != 0) dropCount = 1;
            else if ((flags & DROP_HALF) != 0) dropCount = Mathf.Max(1, currentCount / 2);

            if (!_invServer.TryRemove(actor, cid, slotIndex, dropCount, out var deltas)) return;

            var router = ResolveServerRouter();
            if (router != null && deltas != null)
            {
                for (int i = 0; i < deltas.Count; i++)
                    router.BroadcastDeltaFromServer(deltas[i]);
            }

            var so = _db.Get(itemId);
            if (so == null) return;

            if (!TryGetNetworkPrefab(so, out var pickablePrefab,
                "PickableNetwork", "PickupNetwork", "WorldDrop", "WorldPrefab",
                "PickablePrefab", "PickupPrefab", "WorldPickable", "Prefab"))
                return;

            var forward = dir.sqrMagnitude > 0f ? dir.normalized : Vector3.forward;
            var rot = Quaternion.LookRotation(forward);

            var spawned = Runner.Spawn(
                pickablePrefab, pos, rot, PlayerRef.None,
                onBeforeSpawned: (runner, netObj) =>
                {
                    var pick = netObj.GetComponentInChildren<PickableItem>(true);
                    if (pick != null)
                        pick.ServerInit(itemId, dropCount, ammo);
                    else
                        TryInitWorldItemDeep(netObj.gameObject, itemId, dropCount, ammo);
                });

            if (spawned != null && spawned.TryGetComponent<Rigidbody>(out var rb))
            {
                float force = ic != null ? ic.ThrowForce : 5f;
                rb.AddForce(forward * force, ForceMode.VelocityChange);
            }
        }

    }
}