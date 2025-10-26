using Fusion;
using UnityEngine;

namespace Game.Network
{
    public interface IPlayerFactory
    {
        NetworkObject Spawn(PlayerRef playerRef, Vector3 position, Quaternion rotation);
    }
}
