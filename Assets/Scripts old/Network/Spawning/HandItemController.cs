using System.Reflection;
using Fusion;
using UnityEngine;

namespace Game
{
    public sealed class HandItemController : MonoBehaviour
    {
        private ItemDatabaseSO _db;
        private PlayerRpcHandler _rpc;
        private InteractionController _ic;

        private NetworkObject _currentHandNet;
        private string _currentItemId;

        public void Construct(ItemDatabaseSO db, PlayerRpcHandler rpc, InteractionController ic)
        {
            _db = db;
            _rpc = rpc;
            _ic = ic;
        }

        public void EquipItemServer(string itemId)
        {
            if (_ic == null || _db == null) return;
            if (_ic.Runner == null || !_ic.Runner.IsServer) return;

            if (string.IsNullOrEmpty(itemId))
            {
                UnEquipItemServer();
                return;
            }

            if (_currentHandNet != null)
            {
                if (_currentItemId == itemId) { SyncAttachClientSide(); return; }
                if (_ic.Runner != null) _ic.Runner.Despawn(_currentHandNet);
                _currentHandNet = null;
                _ic.SetHandModelNetworkInstance(null);
            }

            var so = _db.Get(itemId);
            if (so == null) { UnEquipItemServer(); return; }

            if (!TryGetHandModelPrefab(so, out var handPrefab)) { UnEquipItemServer(); return; }

            var pos = _ic.handPoint != null ? _ic.handPoint.position : _ic.transform.position;
            var rot = _ic.handPoint != null ? _ic.handPoint.rotation : _ic.transform.rotation;

            NetworkObject spawned = null;
            _ic.Runner.Spawn(
                handPrefab,
                pos,
                rot,
                _ic.Object.InputAuthority,
                (runner, obj) =>
                {
                    spawned = obj;
                });

            _currentHandNet = spawned;
            _currentItemId = itemId;

            _ic.SetHandModelNetworkInstance(spawned);
            SyncAttachClientSide();
        }

        public void UnEquipItemServer()
        {
            if (_ic == null) return;
            if (_ic.Object == null || !_ic.Object.HasStateAuthority) return;

            if (_currentHandNet != null && _ic.Runner != null)
            {
                _ic.Runner.Despawn(_currentHandNet);
            }
            _currentHandNet = null;
            _currentItemId = null;
            _ic.SetHandModelNetworkInstance(null);
        }

        public void RequestUnEquip()
        {
            if (_rpc != null)
            {
                _rpc.RPC_UnEquipItem();
            }
        }

        private void LateUpdate()
        {
            SyncAttachClientSide();
        }

        private void SyncAttachClientSide()
        {
            var no = _ic != null ? _ic.GetHandModelNetworkInstance() : null;
            var hp = _ic != null ? _ic.handPoint : null;
            if (no == null || hp == null) return;

            var tr = no.transform;
            if (tr.parent == hp) return;

            var worldScale = tr.lossyScale;

            tr.SetParent(hp, false);
            tr.localPosition = Vector3.zero;
            tr.localRotation = Quaternion.identity;
            tr.localScale = new Vector3(
                hp.lossyScale.x != 0f ? worldScale.x / hp.lossyScale.x : worldScale.x,
                hp.lossyScale.y != 0f ? worldScale.y / hp.lossyScale.y : worldScale.y,
                hp.lossyScale.z != 0f ? worldScale.z / hp.lossyScale.z : worldScale.z
            );
        }

        private static bool TryGetHandModelPrefab(ScriptableObject so, out NetworkObject prefab)
        {
            prefab = null;
            if (so == null) return false;

            if (so is IHandModelProvider hp && hp.HandModelNetwork != null)
            {
                prefab = hp.HandModelNetwork;
                return true;
            }

            const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var t = so.GetType();

            string[] names =
            {
                "HandModelNetwork","HandNetwork","FPVNetwork","ViewNetwork","ModelNetwork",
                "HandModelPrefab","HandPrefab","FPVPrefab","ViewPrefab","ModelPrefab"
            };

            for (int i = 0; i < names.Length; i++)
            {
                var f = t.GetField(names[i], BF);
                if (f != null)
                {
                    var v = f.GetValue(so);
                    if (v is NetworkObject no1) { prefab = no1; return true; }
                    if (v is GameObject go1) { prefab = go1.GetComponent<NetworkObject>(); if (prefab != null) return true; }
                }

                var p = t.GetProperty(names[i], BF);
                if (p != null && p.CanRead)
                {
                    var v = p.GetValue(so);
                    if (v is NetworkObject no2) { prefab = no2; return true; }
                    if (v is GameObject go2) { prefab = go2.GetComponent<NetworkObject>(); if (prefab != null) return true; }
                }
            }

            return false;
        }

    }
}