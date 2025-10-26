using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(Collider))]
    public sealed class BulletDamageDealer : NetworkBehaviour
    {
        [SerializeField] private LayerMask _targetMask;
        [SerializeField] private DamageKind _kind = DamageKind.Bullet;

        private int _damage = 1;
        public PlayerRef Source { get; set; } = PlayerRef.None;

        public override void Spawned()
        {
            if (Source == PlayerRef.None && Object != null)
                Source = Object.InputAuthority;
        }

        public void Configure(int damage, PlayerRef source)
        {
            _damage = Mathf.Max(0, damage);
            Source = source;
        }

        public void ApplyInitialPhysics(float mass, Vector3 velocity)
        {
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                if (mass > 0f) rb.mass = mass;
                rb.linearVelocity = velocity;
            }
        }

        private bool IsTarget(Collider other) =>
            (_targetMask.value & (1 << other.gameObject.layer)) != 0;

        private void TryHit(Collider other, Vector3 point, Vector3 normal)
        {
            if (!Object.HasStateAuthority) return;
            if (!IsTarget(other)) return;

            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                var b = GetComponentInParent<Bullet>() ?? GetComponentInChildren<Bullet>();
                int amount = b != null ? b.Damage : _damage;

                Vector3 dir = normal.sqrMagnitude > 1e-6f ? -normal.normalized : transform.forward;
                damageable.ApplyDamage(new DamageInfo(amount, _kind, point, dir, Source));
            }

            if (Runner != null && Object != null) Runner.Despawn(Object);
            else Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            var hitPoint = other.ClosestPoint(transform.position);
            var normal   = transform.position - hitPoint; // из цели к пуле
            TryHit(other, hitPoint, normal);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var c = collision.GetContact(0);
            TryHit(collision.collider, c.point, c.normal);
        }
    }
}
