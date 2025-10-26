using Fusion;
using System;
using UnityEngine;
using Zenject;
using Game.Settings;
using UnityEngine.InputSystem;
using Game.UI;

namespace Game
{
    public sealed class InputHandler : MonoBehaviour
    {
        [Inject] private PlayerStatsSO _stats;
        [Inject] private ISettingsService _settings;

        [SerializeField] private float _wheelCooldown = 0.12f;

        private MainInput _actions;

        private InputAction _actMove;
        private InputAction _actLook;
        private InputAction _actFire;
        private InputAction _actInteract;
        private InputAction _actReload;
        private InputAction _actDrop;
        private InputAction _actPlace;
        private InputAction _actToggleInventory;
        private InputAction _actToggleSettings;
        private InputAction _actScroll;
        private InputAction _actJump;
        private InputAction[] _actQuickSlots;

        private bool _lmbHeld;
        private float _nextWheelTime;
        private InputData _lastInput;

        private Transform _localRotationRoot;
        private Transform _localCameraRoot;

        public float YawLocal { get; private set; }
        public float PitchLocal { get; private set; }
        public bool IsBlok { get; private set; }

        public event Action OnInteractPressed;
        public event Action OnReloadPressed;
        public event Action OnQuickDropPressed;
        public event Action OnPlacePressed;
        public event Action OnUseDown;
        public event Action OnUseUp;
        public event Action OnInventoryToggle;
        public event Action OnGlobalUiToggleMenu;
        public event Action OnQuickSlotNext;
        public event Action OnQuickSlotPrev;
        public event Action<int> OnQuickSlotSelect;

        private void OnEnable()
        {
            if (_actions == null) _actions = new MainInput();
            var g = _actions.Gameplay;

            _actMove = g.Movement;
            _actLook = g.Look;
            _actFire = g.Fire;
            _actInteract = g.Interact;
            _actReload = g.Reload;
            _actDrop = g.Drop;
            _actPlace = g.Place;
            _actToggleInventory = g.ToggleInventory;
            _actToggleSettings = g.ToggleSettings;
            _actScroll = g.Scroll;
            _actJump = g.Jump;

            _actQuickSlots = new[]
            {
                g.QuickSlot1, g.QuickSlot2, g.QuickSlot3, g.QuickSlot4, g.QuickSlot5,
                g.QuickSlot6, g.QuickSlot7, g.QuickSlot8, g.QuickSlot9, g.QuickSlot0
            };

            _actFire.started += OnFireStarted;
            _actFire.canceled += OnFireCanceled;

            _actInteract.performed += _ => OnInteractPressed?.Invoke();
            _actReload.performed += _ => OnReloadPressed?.Invoke();
            _actDrop.performed += _ => OnQuickDropPressed?.Invoke();
            _actPlace.performed += _ => OnPlacePressed?.Invoke();
            _actToggleInventory.performed += _ => OnInventoryToggle?.Invoke();
            _actToggleSettings.performed += _ => OnGlobalUiToggleMenu?.Invoke();

            for (int i = 0; i < 9; i++)
            {
                int idx = i;
                _actQuickSlots[i].performed += _ => OnQuickSlotSelect?.Invoke(idx);
            }
            _actQuickSlots[9].performed += _ => OnQuickSlotSelect?.Invoke(-1);

            _actions.Enable();
        }

        private void OnDisable()
        {
            if (_actions == null) return;

            _actFire.started -= OnFireStarted;
            _actFire.canceled -= OnFireCanceled;

            _actInteract.performed -= _ => OnInteractPressed?.Invoke();
            _actReload.performed -= _ => OnReloadPressed?.Invoke();
            _actDrop.performed -= _ => OnQuickDropPressed?.Invoke();
            _actPlace.performed -= _ => OnPlacePressed?.Invoke();
            _actToggleInventory.performed -= _ => OnInventoryToggle?.Invoke();
            _actToggleSettings.performed -= _ => OnGlobalUiToggleMenu?.Invoke();

            if (_actQuickSlots != null)
            {
                for (int i = 0; i < 9; i++) _actQuickSlots[i].performed -= _ => OnQuickSlotSelect?.Invoke(i);
                _actQuickSlots[9].performed -= _ => OnQuickSlotSelect?.Invoke(-1);
            }

            _actions.Disable();
        }

        private void Update()
        {
            if (_actScroll != null && Time.unscaledTime >= _nextWheelTime)
            {
                Vector2 s = _actScroll.ReadValue<Vector2>();
                float dy = s.y;
                if (dy > 0.1f)
                {
                    OnQuickSlotPrev?.Invoke();
                    _nextWheelTime = Time.unscaledTime + _wheelCooldown;
                }
                else if (dy < -0.1f)
                {
                    OnQuickSlotNext?.Invoke();
                    _nextWheelTime = Time.unscaledTime + _wheelCooldown;
                }
            }
        }

        private void OnFireStarted(InputAction.CallbackContext _)
        {
            if (!_lmbHeld)
            {
                _lmbHeld = true;
                OnUseDown?.Invoke();
            }
        }

        private void OnFireCanceled(InputAction.CallbackContext _)
        {
            if (_lmbHeld)
            {
                _lmbHeld = false;
                OnUseUp?.Invoke();
            }
        }

        public void BindLocalAvatar(Transform rotationRoot, Transform cameraRoot)
        {
            _localRotationRoot = rotationRoot;
            _localCameraRoot = cameraRoot;
            YawLocal = _localRotationRoot ? _localRotationRoot.eulerAngles.y : 0f;
            float p = 0f;
            if (_localCameraRoot)
            {
                p = _localCameraRoot.localEulerAngles.x;
                if (p > 180f) p -= 360f;
            }
            PitchLocal = Mathf.Clamp(p, -89f, 89f);
        }

        public void BlockLook(UiPhase phase)
        {
            bool block = phase == UiPhase.Inventory || phase == UiPhase.OtherInventory || phase == UiPhase.Menu || phase == UiPhase.Exit;
            if (block == IsBlok) return;
            IsBlok = block;
            if (IsBlok && _lmbHeld)
            {
                _lmbHeld = false;
                OnUseUp?.Invoke();
            }
        }

        public InputData GetLastInputData() => _lastInput;

        public void ProvideNetworkInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new InputData();

            Vector2 move = _actMove != null ? _actMove.ReadValue<Vector2>() : Vector2.zero;
            if (move.sqrMagnitude > 1f) move.Normalize();
            data.movement = move;

            bool jumpPressed = _actJump != null && _actJump.IsPressed();
            data.jump = !IsBlok && jumpPressed;

            float mouseDeltaX = 0f;
            float mouseDeltaY = 0f;

            if (!IsBlok)
            {
                Vector2 look = _actLook != null ? _actLook.ReadValue<Vector2>() : Vector2.zero;

                if (_stats != null && _stats.keyboardLookSensitivity > 0f)
                {
                    float dt = runner != null ? runner.DeltaTime : Time.deltaTime;
                    if (Keyboard.current != null)
                    {
                        if (Keyboard.current.rightArrowKey.isPressed) look.x += _stats.keyboardLookSensitivity * dt;
                        if (Keyboard.current.leftArrowKey.isPressed) look.x -= _stats.keyboardLookSensitivity * dt;
                        if (Keyboard.current.upArrowKey.isPressed) look.y += _stats.keyboardLookSensitivity * dt;
                        if (Keyboard.current.downArrowKey.isPressed) look.y -= _stats.keyboardLookSensitivity * dt;
                    }
                }

                float sens = _settings != null ? _settings.MouseSensitivity : 1f;
                mouseDeltaX = look.x * sens;
                mouseDeltaY = look.y * sens;

                YawLocal = Mathf.Repeat(YawLocal + mouseDeltaX, 360f);
                PitchLocal = Mathf.Clamp(PitchLocal - mouseDeltaY, -89f, 89f);
            }

            data.mouseX = mouseDeltaX;
            data.mouseY = mouseDeltaY;
            data.yawAbs = YawLocal;
            data.pitchAbs = PitchLocal;
            data.hasAngles = 1;

            _lastInput = data;
            input.Set(data);
        }
    }
}
