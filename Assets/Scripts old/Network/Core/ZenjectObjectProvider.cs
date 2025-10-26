using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class ZenjectObjectProvider : INetworkObjectProvider
    {
        private readonly DiContainer _container;

        public ZenjectObjectProvider(DiContainer container)
        {
            _container = container;
        }

        public NetworkPrefabId GetPrefabId(NetworkRunner runner, NetworkObjectGuid guid)
        {
            return runner.Prefabs.GetId(guid);
        }

        public NetworkObjectAcquireResult AcquirePrefabInstance(NetworkRunner runner, in NetworkPrefabAcquireContext context, out NetworkObject instance)
        {
            var prefab = runner.Prefabs.Load(context.PrefabId, context.IsSynchronous);
            if (prefab == null)
            {
                instance = null;
                return NetworkObjectAcquireResult.Retry;
            }

            var go = _container.InstantiatePrefab(prefab);
            instance = go.GetComponent<NetworkObject>();
            if (instance == null)
            {
                Object.Destroy(go);
                return NetworkObjectAcquireResult.Failed;
            }

            runner.Prefabs.AddInstance(context.PrefabId);
            return NetworkObjectAcquireResult.Success;
        }

        public void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context)
        {
            if (context.TypeId.IsPrefab)
                runner.Prefabs.RemoveInstance(context.TypeId.AsPrefabId);

            Object.Destroy(context.Object.gameObject);
        }
    }
}
