using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(Rigidbody), typeof(NetworkObject), typeof(Collider))]
    public class Bullet : NetworkBehaviour
    {
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private float drag = 0f;

        [Networked] public int Damage { get; private set; }

        private Rigidbody _rigidbody;
        private TickTimer _timer;

        public override void Spawned()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null) _rigidbody.linearDamping = drag;
            _timer = TickTimer.CreateFromSeconds(Runner, lifetime);
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority && _timer.Expired(Runner))
                Runner.Despawn(Object);
        }

        public void Initialize(int damage)
        {
            if (Object.HasStateAuthority)
                Damage = damage;
        }

        public void InitializeVelocity(Vector3 velocity)
        {
            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null) _rigidbody.linearVelocity = velocity;
        }

        public void SetMass(float mass)
        {
            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();

            if (_rigidbody != null)
                _rigidbody.mass = Mathf.Max(0.001f, mass);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Object == null || Runner == null) return;
            if (!Object.HasStateAuthority) return;

            Runner.Despawn(Object);
        }
    }
}
