using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(SimpleKCC))]
    public sealed class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private SimpleKCC _kcc;
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private Transform _rotationRoot;

        [Inject] private PlayerStatsSO _stats;
        [Inject(Optional = true)] private InputHandler _inputHandler;

        [Networked] private float Yaw   { get; set; }
        [Networked] private float Pitch { get; set; }

        private Vector3 _planarVelocity;

        public override void Spawned()
        {
            if (_kcc == null) _kcc = GetComponent<SimpleKCC>();
            if (_rotationRoot == null) _rotationRoot = transform;

            if (Object.HasStateAuthority)
            {
                Yaw = _rotationRoot.eulerAngles.y;
                Pitch = 0f;
            }

            _planarVelocity = Vector3.zero;
            _kcc.SetGravity(_stats.gravity);

            if (Object.HasInputAuthority && _inputHandler != null)
                _inputHandler.BindLocalAvatar(_rotationRoot, _cameraRoot);
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out InputData input)) return;

            float dt = Runner.DeltaTime;

            if (Object.HasStateAuthority)
            {
                if (input.hasAngles != 0)
                {
                    Yaw   = Mathf.Repeat(input.yawAbs, 360f);
                    Pitch = Mathf.Clamp(input.pitchAbs, -89f, 89f);
                }
                else
                {
                    Yaw   = Mathf.Repeat(Yaw + input.mouseX, 360f);
                    Pitch = Mathf.Clamp(Pitch - input.mouseY, -89f, 89f);
                }
            }

            Quaternion rotY = Quaternion.Euler(0f, Yaw, 0f);

            Vector2 mv = input.movement;
            if (mv.sqrMagnitude > 1f) mv.Normalize();

            Vector3 desiredPlanar = (rotY * Vector3.forward) * mv.y + (rotY * Vector3.right) * mv.x;
            desiredPlanar *= _stats.moveSpeed;

            if (_kcc.IsGrounded && _kcc.ProjectOnGround(desiredPlanar, out var projected))
                desiredPlanar = projected;

            if (_kcc.IsGrounded)
            {
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

            float jumpImpulse = (_kcc.IsGrounded && input.jump) ? _stats.jumpImpulse : 0f;

            _kcc.Move(_planarVelocity, jumpImpulse);

            if (_rotationRoot != null)
                _rotationRoot.rotation = rotY;
        }

        public override void Render()
        {
            if (_rotationRoot != null)
            {
                if (Object.HasInputAuthority && _inputHandler != null)
                    _rotationRoot.rotation = Quaternion.Euler(0f, _inputHandler.YawLocal, 0f);
                else
                    _rotationRoot.rotation = Quaternion.Euler(0f, Yaw, 0f);
            }

            if (_cameraRoot != null)
            {
                if (Object.HasInputAuthority && _inputHandler != null)
                    _cameraRoot.localRotation = Quaternion.Euler(_inputHandler.PitchLocal, 0f, 0f);
                else
                    _cameraRoot.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
            }
        }
    }
}
