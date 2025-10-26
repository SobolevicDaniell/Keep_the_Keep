using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public enum MainMenuUiPhase
    {
        Main,
        Settings,
        Loading,
        Confirmation
    }

    public sealed class MainMenuUIStateController : MonoBehaviour
    {
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _loadingPanel;
        [SerializeField] private GameObject _confirmationPanel;

        [SerializeField] private Button _openSettingsButton;
        [SerializeField] private Button _backFromSettingsButton;


        public MainMenuUiPhase Phase { get; private set; } = MainMenuUiPhase.Main;

        private void OnEnable()
        {
            if (_openSettingsButton) _openSettingsButton.onClick.AddListener(OnOpenSettings);
            if (_backFromSettingsButton) _backFromSettingsButton.onClick.AddListener(OnBackFromSettings);
            ApplyPhase(Phase);
        }

        private void OnDisable()
        {
            if (_openSettingsButton) _openSettingsButton.onClick.RemoveListener(OnOpenSettings);
            if (_backFromSettingsButton) _backFromSettingsButton.onClick.RemoveListener(OnBackFromSettings);
        }

        public void SetPhase(MainMenuUiPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            ApplyPhase(phase);
        }
        private void ApplyPhase(MainMenuUiPhase phase)
        {
            var showMain = phase == MainMenuUiPhase.Main;
            var showSettings = phase == MainMenuUiPhase.Settings;
            var showLoading = phase == MainMenuUiPhase.Loading;
            var showConfirmation = phase == MainMenuUiPhase.Confirmation;

            var cursor = phase == MainMenuUiPhase.Main || phase == MainMenuUiPhase.Settings || phase == MainMenuUiPhase.Confirmation;
            Cursor.lockState = cursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = cursor;

            if (_mainPanel) _mainPanel.SetActive(showMain);
            if (_settingsPanel) _settingsPanel.SetActive(showSettings);
            if (_loadingPanel) _loadingPanel.SetActive(showLoading);
            if (_confirmationPanel) _confirmationPanel.SetActive(showConfirmation);
        }

        private void OnOpenSettings()
        {
            SetPhase(MainMenuUiPhase.Settings);
        }

        private void OnBackFromSettings()
        {
            SetPhase(MainMenuUiPhase.Main);
        }
    }
}
