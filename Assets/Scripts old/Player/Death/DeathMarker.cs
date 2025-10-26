using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DeathMarker : NetworkBehaviour
    {
        [Networked] public PlayerRef Owner { get; private set; }

        public void Initialize(PlayerRef owner)
        {
            if (Object.HasStateAuthority) Owner = owner;
        }
    }
}
