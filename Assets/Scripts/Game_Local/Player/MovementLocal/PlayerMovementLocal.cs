using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Local
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovementLocal : MonoBehaviour
    {
        [SerializeField] private CharacterController _controller;
        [SerializeField] private InputHandlerLocal _input;
        [SerializeField] private Transform _rotationRoot;
        [SerializeField] private Transform _cameraRoot;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7.0f;
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float airControl = 0.5f;

        [Header("Jump/Gravity")]
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -9.81f;

        private Vector3 _velocity;
        private float _currentSpeed;

        private void Awake()
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_input == null) _input = GetComponent<InputHandlerLocal>();
            if (_rotationRoot == null) _rotationRoot = transform;
            if (_cameraRoot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) _cameraRoot = cam.transform;
            }
            if (_input != null) _input.BindLocalAvatar(_rotationRoot, _cameraRoot);
        }

        private void Update()
        {
            if (_input == null || _controller == null) return;

            if (_rotationRoot != null) _rotationRoot.localEulerAngles = new Vector3(0f, _input.YawLocal, 0f);
            if (_cameraRoot != null) _cameraRoot.localEulerAngles = new Vector3(_input.PitchLocal, 0f, 0f);

            bool grounded = _controller.isGrounded;
            if (grounded && _velocity.y < 0f) _velocity.y = -2f;

            Vector2 mv = _input.Movement;
            Vector3 wish = new Vector3(mv.x, 0f, mv.y);
            wish = Vector3.ClampMagnitude(wish, 1f);

            Transform basis = _rotationRoot != null ? _rotationRoot : transform;
            Vector3 worldWish = basis.TransformDirection(wish);

            bool sprint = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            float targetSpeed = sprint ? sprintSpeed : walkSpeed;

            float accel = grounded ? acceleration : acceleration * airControl;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed * worldWish.magnitude, accel * Time.deltaTime);
            Vector3 planar = worldWish.sqrMagnitude > 0f ? worldWish.normalized * _currentSpeed : Vector3.zero;

            if (grounded && _input.JumpPressed)
            {
                _velocity.y = Mathf.Sqrt(-2f * gravity * jumpHeight);
            }

            _velocity.y += gravity * Time.deltaTime;

            Vector3 delta = (planar + new Vector3(0f, _velocity.y, 0f)) * Time.deltaTime;
            _controller.Move(delta);

            if (grounded && worldWish.sqrMagnitude < 0.0001f)
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, accel * Time.deltaTime);
        }
    }
}
