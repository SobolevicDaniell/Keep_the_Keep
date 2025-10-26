using Fusion;
using UnityEngine;
using Zenject;
using Game.Network;
using System.Collections.Generic;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerHealthServer : NetworkBehaviour
    {
        [SerializeField] private PlayerStatsSO _statsSerialized;
        [Inject(Optional = true)] private PlayerStatsSO _statsDI;
        [Inject(Optional = true)] private InventoryServerService _inventoryServer;

        [Inject] private PlayerSpawner _spawner;

        [Networked] public int Current { get; private set; }
        [Networked] public int Max { get; private set; }
        [Networked] public NetworkBool IsDead { get; private set; }
        [Networked] private TickTimer _despawnDelay { get; set; }

        private struct PendingTransfer
        {
            public ContainerId FromId;
            public int FromIndex;
            public int Count;
        }

        private bool _corpseTransferPending;
        private List<PendingTransfer> _corpseOps;
        private ContainerId _corpseToId;
        [Networked] private TickTimer _corpseTransferDelay { get; set; }

        public override void Spawned()
        {
            if (!Object.HasStateAuthority) return;
            var so = _statsSerialized != null ? _statsSerialized : _statsDI;
            Max = so != null ? Mathf.Max(1, so.maxHealth) : 100;
            Current = Max;
            IsDead = false;
            _despawnDelay = TickTimer.None;
            _spawner.RegisterAvatar(Object.InputAuthority, gameObject);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (IsDead && _despawnDelay.Expired(Runner))
            {
                var owner = Object.InputAuthority;
                _spawner.DespawnAvatar(Runner, owner, Object);
                _despawnDelay = TickTimer.None;
            }

            if (_corpseTransferPending && _corpseTransferDelay.Expired(Runner))
            {
                var owner = Object.InputAuthority;
                if (_inventoryServer != null && _corpseOps != null)
                {
                    int dst = 0;
                    for (int i = 0; i < _corpseOps.Count; i++)
                    {
                        var op = _corpseOps[i];
                        _inventoryServer.TryTransfer(owner, op.FromId, op.FromIndex, _corpseToId, dst, op.Count, out _, out _, out _, out _);
                        dst++;
                    }
                }
                _corpseOps = null;
                _corpseTransferPending = false;
                _corpseTransferDelay = TickTimer.None;
            }
        }


        public void ApplyDamage(int amount)
        {
            if (!Object.HasStateAuthority || amount <= 0 || IsDead) return;
            Current = Mathf.Max(0, Current - amount);
            if (Current == 0)
            {
                IsDead = true;
                _despawnDelay = TickTimer.CreateFromSeconds(Runner, 0.25f);

                var owner = Object.InputAuthority;
                var po = Runner.GetPlayerObject(owner);
                if (po != null)
                {
                    var proxy = po.GetComponent<PlayerObject>();
                    if (proxy != null)
                    {
                        proxy.RPC_ShowDeath();
                        SpawnCorpseAndDumpInventory();
                    }

                }
            }
        }

        public void ApplyHeal(int amount)
        {
            if (!Object.HasStateAuthority || amount <= 0 || IsDead) return;
            Current = Mathf.Min(Max, Current + amount);
        }

        private void SpawnCorpseAndDumpInventory()
        {
            if (!Object.HasStateAuthority) return;

            var owner = Object.InputAuthority;

            if (_inventoryServer == null) return;
            if (!_inventoryServer.TryGetPlayerContainers(owner, out var quick, out var main)) return;

            int mainOcc = 0;
            if (main != null)
            {
                for (int i = 0; i < main.Capacity; i++)
                {
                    var s = main.Slots[i];
                    if (!string.IsNullOrEmpty(InventorySlotStateAccessor.ReadId(s)) && InventorySlotStateAccessor.ReadCount(s) > 0)
                        mainOcc++;
                }
            }

            int quickOcc = 0;
            if (quick != null)
            {
                for (int i = 0; i < quick.Capacity; i++)
                {
                    var s = quick.Slots[i];
                    if (!string.IsNullOrEmpty(InventorySlotStateAccessor.ReadId(s)) && InventorySlotStateAccessor.ReadCount(s) > 0)
                        quickOcc++;
                }
            }

            int total = mainOcc + quickOcc;
            if (total <= 0) return;

            var pos = transform.position;
            var rot = transform.rotation;

            var corpse = _spawner.SpawnDeathBox(Runner, owner, pos, rot);
            if (corpse == null) return;

            var corpseSrv = corpse.GetComponent<CorpseInventoryServer>();
            if (corpseSrv == null) return;

            corpseSrv.ServerInit(total);

            var toId = ContainerId.OfObject(ContainerType.Corpse, corpse.Id);

            var ops = new List<PendingTransfer>(total);

            if (main != null)
            {
                for (int i = 0; i < main.Capacity; i++)
                {
                    var s = main.Slots[i];
                    var sid = InventorySlotStateAccessor.ReadId(s);
                    var cnt = InventorySlotStateAccessor.ReadCount(s);
                    if (string.IsNullOrEmpty(sid) || cnt <= 0) continue;
                    ops.Add(new PendingTransfer { FromId = main.Id, FromIndex = i, Count = cnt });
                }
            }

            if (quick != null)
            {
                for (int i = 0; i < quick.Capacity; i++)
                {
                    var s = quick.Slots[i];
                    var sid = InventorySlotStateAccessor.ReadId(s);
                    var cnt = InventorySlotStateAccessor.ReadCount(s);
                    if (string.IsNullOrEmpty(sid) || cnt <= 0) continue;
                    ops.Add(new PendingTransfer { FromId = quick.Id, FromIndex = i, Count = cnt });
                }
            }

            _corpseOps = ops;
            _corpseToId = toId;
            _corpseTransferPending = true;
            _corpseTransferDelay = TickTimer.CreateFromTicks(Runner, 1);
        }

    }
}