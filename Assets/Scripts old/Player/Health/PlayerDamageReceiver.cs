using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerHealthServer))]
    public sealed class PlayerDamageReceiver : NetworkBehaviour, IDamageable
    {
        private PlayerHealthServer _health;

        private void Awake()
        {
            _health = GetComponent<PlayerHealthServer>();
        }

        public bool ApplyDamage(in DamageInfo info)
        {
            if (_health == null) return false;

            if (Object.HasStateAuthority)
            {
                _health.ApplyDamage(Mathf.Max(0, info.amount));
                return true;
            }

            return false;
        }
    }
}
