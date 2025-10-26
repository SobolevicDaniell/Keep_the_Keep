using System.Collections;
using System.Linq;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Gameplay
{
    public class PickableSpawner : MonoBehaviour
    {
        [Inject(Optional = true)] private NetworkRunner _runner;
        [Inject(Optional = true)] private ItemDatabaseSO _database;

        [Header("Какой предмет спавним")]
        [SerializeField] private string _itemId;

        [Header("Желаемое количество (≤ MaxStack)")]
        [SerializeField] private int _requestedCount = 1;

        private bool _spawned;
        private Coroutine _routine;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _routine = StartCoroutine(CoSpawnWhenReady());
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
        }

        private IEnumerator CoSpawnWhenReady()
        {
            while (_runner == null) { _runner = FindObjectOfType<NetworkRunner>(true); yield return null; }
            while (!_runner.IsRunning) yield return null;
            while (_database == null) yield return null;
            if (!HasAuthority(_runner)) yield break;
            if (_spawned) yield break;
            SpawnPickable();
            _spawned = true;
        }

        private bool HasAuthority(NetworkRunner runner)
        {
            return runner.IsServer || runner.IsSharedModeMasterClient;
        }

        private void OnValidate()
        {
            if (_database == null) return;
            var names = new string[_database.Items.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = _database.Items[i].Id;
            if (!names.Contains(_itemId) && names.Length > 0)
                _itemId = names[0];
            if (_requestedCount < 1) _requestedCount = 1;
        }

        private void SpawnPickable()
        {
            if (_database == null) return;
            var itemDef = _database.Get(_itemId);
            if (itemDef == null) return;

            var prefab = itemDef.Prefab;
            if (prefab == null) return;

            var netObj = prefab.GetComponent<NetworkObject>();
            if (netObj == null) return;

            var count = Mathf.Clamp(_requestedCount, 1, itemDef.MaxStack);

            _runner.Spawn(
                netObj,
                transform.position,
                transform.rotation,
                PlayerRef.None,
                onBeforeSpawned: (runner, spawnedObj) =>
                {
                    var pickable = spawnedObj.GetComponent<PickableItem>();
                    if (pickable != null) pickable.ServerInit(_itemId, count, 0);
                    spawnedObj.GetComponent<Rigidbody>().isKinematic = true;
                }
            );
        }
    }
}
