using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class CorpseSpatial : NetworkBehaviour
    {
        [Networked] public Vector3 SpawnPos { get; private set; }
        [Networked] public Quaternion SpawnRot { get; private set; }
        [Networked] public bool PoseSet { get; private set; }

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public override void Spawned()
        {
            if (PoseSet) ApplyPose();
        }

        public void ServerSetSpawnPose(Vector3 pos, Quaternion rot)
        {
            if (!Object.HasStateAuthority) return;
            SpawnPos = pos;
            SpawnRot = rot;
            PoseSet = true;
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (_rb != null)
            {
                _rb.position = SpawnPos;
                _rb.rotation = SpawnRot;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            else
            {
                transform.SetPositionAndRotation(SpawnPos, SpawnRot);
            }
        }
    }
}
