using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Game.Local
{
    public sealed class InputHandlerLocal : MonoBehaviour
    {
        [Inject] private PlayerStatsSO _stats;
        [Inject(Optional = true)] private Settings.ISettingsService _settings;

        private MainInput _actions;
        private InputAction _actMove;
        private InputAction _actLook;
        private InputAction _actJump;

        private Transform _rotationRoot;
        private Transform _cameraRoot;

        public Vector2 Movement { get; private set; }
        public bool JumpPressed { get; private set; }
        public float YawLocal { get; private set; }
        public float PitchLocal { get; private set; }
        public bool LookBlocked { get; private set; }

        private void OnEnable()
        {
            if (_actions == null) _actions = new MainInput();
            var g = _actions.Gameplay;
            _actMove = g.Movement;
            _actLook = g.Look;
            _actJump = g.Jump;
            _actions.Enable();
        }

        private void OnDisable()
        {
            if (_actions == null) return;
            _actions.Disable();
        }

        private void Update()
        {
            Vector2 mv = _actMove != null ? _actMove.ReadValue<Vector2>() : Vector2.zero;
            if (mv.sqrMagnitude > 1f) mv.Normalize();
            Movement = mv;

            JumpPressed = _actJump != null && _actJump.IsPressed();

            if (!LookBlocked)
            {
                Vector2 look = _actLook != null ? _actLook.ReadValue<Vector2>() : Vector2.zero;
                float dt = Time.deltaTime;
                if (_stats != null && _stats.keyboardLookSensitivity > 0f && Keyboard.current != null)
                {
                    if (Keyboard.current.rightArrowKey.isPressed) look.x += _stats.keyboardLookSensitivity * dt;
                    if (Keyboard.current.leftArrowKey.isPressed) look.x -= _stats.keyboardLookSensitivity * dt;
                    if (Keyboard.current.upArrowKey.isPressed) look.y += _stats.keyboardLookSensitivity * dt;
                    if (Keyboard.current.downArrowKey.isPressed) look.y -= _stats.keyboardLookSensitivity * dt;
                }

                float sens = _settings != null ? _settings.MouseSensitivity : 1f;
                float dx = look.x * sens;
                float dy = look.y * sens;

                YawLocal = Mathf.Repeat(YawLocal + dx, 360f);
                PitchLocal = Mathf.Clamp(PitchLocal - dy, -89f, 89f);
            }
        }

        public void BindLocalAvatar(Transform rotationRoot, Transform cameraRoot)
        {
            _rotationRoot = rotationRoot;
            _cameraRoot = cameraRoot;
            YawLocal = _rotationRoot ? _rotationRoot.eulerAngles.y : 0f;
            float p = 0f;
            if (_cameraRoot)
            {
                p = _cameraRoot.localEulerAngles.x;
                if (p > 180f) p -= 360f;
            }
            PitchLocal = Mathf.Clamp(p, -89f, 89f);
        }

        public void SetLookBlocked(bool blocked)
        {
            LookBlocked = blocked;
        }
    }
}
