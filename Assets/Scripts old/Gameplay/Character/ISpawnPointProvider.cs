using UnityEngine;

namespace Game.Network
{
    public interface ISpawnPointProvider
    {
        bool TryGetNext(out Vector3 position, out Quaternion rotation);
        void ResetOrder();
    }
}
