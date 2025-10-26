using System;
using UnityEngine;
using Zenject;

namespace Game.UI
{
    public enum UiPhase
    {
        Hidden,
        Loading,
        Gameplay,
        Inventory,
        OtherInventory,
        Spawn,
        Menu,
        Exit
    }
    public class UIController : MonoBehaviour
    {
        [SerializeField] private GameObject _deathScreen;
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private GameObject _otherInventoryPanel;
        [SerializeField] private GameObject _crosshair;
        [SerializeField] private GameObject _healthBar;
        [SerializeField] private GameObject _drugIcon;
        [SerializeField] private GameObject _inventortBackground;
        [SerializeField] private GameObject _quickSiotsRoot;
        [SerializeField] private GameObject _quickSiotsBackground;
        [SerializeField] private GameObject _cameraLoading;
        [SerializeField] private GameObject _sceneCamera;
        [SerializeField] private GameObject _manual;
        [SerializeField] private GameObject _menu;
        [SerializeField] private GameObject _hp;
        [SerializeField] private Canvas _canvasLoading;
        [SerializeField] private Canvas _canvasMain;
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private UiPhase _defaultPhase = UiPhase.Gameplay;

        [Inject] private InteractionPromptView _interactionPrompt;

        public UiPhase Phase { get; private set; }
        public bool InventoryOpened => Phase == UiPhase.Inventory || Phase == UiPhase.OtherInventory;

        public event Action<UiPhase, UiPhase> OnPhaseChanged;
        public event Action OnExitActivated;
        private PickDropController _promptSource;

        public void Awake()
        {
            ApplyPhase(_defaultPhase);
            _interactionPrompt.Hide();
        }

        public void SetPhase(UiPhase phase)
        {
            if (Phase == phase) return;
            var prev = Phase;
            ApplyPhase(phase);
            OnPhaseChanged?.Invoke(phase, prev);

        }

        private void ApplyPhase(UiPhase phase)
        {
            Phase = phase;
            Debug.Log($"UI Phase changed-> {phase}");

            var showQuick = phase == UiPhase.Gameplay || phase == UiPhase.Inventory || phase == UiPhase.OtherInventory;
            var quickBackground = phase == UiPhase.Gameplay;
            var showInv = phase == UiPhase.Inventory || phase == UiPhase.OtherInventory;
            var showOtherInv = phase == UiPhase.OtherInventory;
            var showSpawn = phase == UiPhase.Spawn;
            var showPrompt = phase == UiPhase.Gameplay;
            var showDot = phase == UiPhase.Gameplay;
            var drugIcon = phase == UiPhase.Inventory || phase == UiPhase.OtherInventory;
            var healthBar = phase == UiPhase.Gameplay || phase == UiPhase.Inventory || phase == UiPhase.OtherInventory;
            var loading = phase == UiPhase.Loading || phase == UiPhase.Exit;
            var manual = phase == UiPhase.Loading || phase == UiPhase.Spawn;
            var sceneCamera = phase == UiPhase.Spawn;
            var menu = phase == UiPhase.Menu;
            var canvasMain = phase != UiPhase.Loading;
            var hp = phase != UiPhase.Loading;

            if (_quickSiotsRoot != null) _quickSiotsRoot.SetActive(showQuick);
            if (_quickSiotsBackground != null) _quickSiotsBackground.SetActive(quickBackground);
            if (_inventoryPanel != null) _inventoryPanel.SetActive(showInv);
            if (_inventortBackground != null) _inventortBackground.SetActive(showInv);
            if (_otherInventoryPanel != null) _otherInventoryPanel.SetActive(showOtherInv);
            if (_deathScreen != null) _deathScreen.SetActive(showSpawn);
            if (_hp != null) _hp.SetActive(hp);
            if (_healthBar != null) _healthBar.SetActive(healthBar);
            if (_crosshair != null) _crosshair.SetActive(showDot);
            if (_drugIcon != null) _drugIcon.SetActive(drugIcon);
            if (_manual != null) _manual.SetActive(manual);
            if (_menu != null) _menu.SetActive(menu);
            if (_cameraLoading != null) _cameraLoading.SetActive(loading);
            if (_sceneCamera != null) _sceneCamera.SetActive(sceneCamera);
            if (_canvasLoading != null) _canvasLoading.gameObject.SetActive(loading);
            if (_canvasMain != null) _canvasMain.gameObject.SetActive(canvasMain);

            var cursor = phase == UiPhase.Inventory || phase == UiPhase.OtherInventory || phase == UiPhase.Spawn || phase == UiPhase.Hidden || phase == UiPhase.Menu;
            Cursor.lockState = cursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = cursor;

            if (_inputHandler != null) _inputHandler.BlockLook(phase);

            if (phase == UiPhase.Exit) OnExitActivated?.Invoke();
        }
        public void BindPromptSource(PickDropController source)
        {
            if (_promptSource == source) return;

            if (_promptSource != null)
            {
                _promptSource.OnPromptShowRequested -= HandlePromptShow;
                _promptSource.OnPromptHideRequested -= HandlePromptHide;
            }

            _promptSource = source;

            if (_promptSource != null)
            {
                _promptSource.OnPromptShowRequested += HandlePromptShow;
                _promptSource.OnPromptHideRequested += HandlePromptHide;
            }

            HandlePromptHide();
        }
        
        private void HandlePromptShow()
        {
            _interactionPrompt.Show();
        }
        private void HandlePromptHide()
        {
            _interactionPrompt?.Hide();
        }

        private void OnDestroy()
        {
            _promptSource.OnPromptShowRequested -= HandlePromptShow;
            _promptSource.OnPromptHideRequested -= HandlePromptHide;
        }
    }
}