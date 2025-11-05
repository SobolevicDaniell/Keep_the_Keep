using UnityEngine;
using Zenject;

namespace Game.Local
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovementLocal : MonoBehaviour
    {
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private Transform _rotationRoot;

        [Inject] private GameObject _player;
        [Inject] private PlayerStatsSO _stats;
        [Inject(Optional = true)] private InputHandlerLocal _input;

        private CharacterController _controller;
        private Vector3 _planarVelocity;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_rotationRoot == null) _rotationRoot = transform;
            _planarVelocity = Vector3.zero;
            if (_input != null) _input.BindLocalAvatar(_rotationRoot, _cameraRoot);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            float yaw = _input != null ? _input.YawLocal : _rotationRoot.eulerAngles.y;
            float pitch = _input != null ? _input.PitchLocal : 0f;

            Quaternion rotY = Quaternion.Euler(0f, yaw, 0f);

            Vector2 mv = _input != null ? _input.Movement : Vector2.zero;
            if (mv.sqrMagnitude > 1f) mv.Normalize();

            Vector3 desiredPlanar = (rotY * Vector3.forward) * mv.y + (rotY * Vector3.right) * mv.x;
            desiredPlanar *= _stats.moveSpeed;

            bool grounded = _controller.isGrounded;
            Vector3 groundNormal = Vector3.up;
            if (grounded && TryGetGroundNormal(out var n)) groundNormal = n;

            if (grounded)
            {
                desiredPlanar = Vector3.ProjectOnPlane(desiredPlanar, groundNormal);
                _planarVelocity = Vector3.MoveTowards(_planarVelocity, desiredPlanar, _stats.groundAcceleration * dt);
                if (mv.sqrMagnitude < 1e-4f)
                {
                    float mag = _planarVelocity.magnitude;
                    mag = Mathf.MoveTowards(mag, 0f, _stats.groundFriction * dt);
                    _planarVelocity = mag > 0f ? _planarVelocity.normalized * mag : Vector3.zero;
                }
            }
            else
            {
                Vector3 delta = desiredPlanar - _planarVelocity;
                float maxDelta = _stats.airAcceleration * dt;
                if (delta.sqrMagnitude > maxDelta * maxDelta) delta = delta.normalized * maxDelta;
                _planarVelocity += delta;
                _planarVelocity *= Mathf.Clamp01(1f - _stats.airDrag * dt);
            }

            bool jump = _input != null && _input.JumpPressed;
            if (grounded)
            {
                if (jump) _verticalVelocity = _stats.jumpImpulse;
                else if (_verticalVelocity < 0f) _verticalVelocity = -2f;
            }

            float gravity = -Mathf.Abs(_stats.gravity);
            _verticalVelocity += gravity * dt;

            Vector3 velocity = _planarVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * dt);

            if (_rotationRoot != null)
                _rotationRoot.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (_cameraRoot != null)
                _cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private bool TryGetGroundNormal(out Vector3 normal)
        {
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return true;
            }
            normal = Vector3.up;
            return false;
        }
    }
}
