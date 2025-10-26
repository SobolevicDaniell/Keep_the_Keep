using Fusion;
using UnityEngine;

namespace Game
{
    public sealed class PredictedVisualSmoothing : NetworkBehaviour
    {
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private float _positionSmooth = 25f;
        [SerializeField] private float _rotationSmooth = 25f;

        public override void Render()
        {
            if (!Object.HasInputAuthority) return;
            float dt = Time.deltaTime;
            float kp = 1f - Mathf.Exp(-_positionSmooth * dt);
            float kr = 1f - Mathf.Exp(-_rotationSmooth * dt);
            _visualRoot.position = Vector3.Lerp(_visualRoot.position, transform.position, kp);
            _visualRoot.rotation = Quaternion.Slerp(_visualRoot.rotation, transform.rotation, kr);
        }
    }
}
