using System.Threading.Tasks;
using Fusion;
using Game.Network;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

namespace Game.UI
{
    public sealed class SessionStarting : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_InputField _sessionInput;
        [SerializeField] private Button _connectButton;
        [SerializeField] private Button _yesButton;
        [SerializeField] private Button _noButton;
        [SerializeField] private TMP_Text _confirmationText;
        [SerializeField] private string _defaultSessionName = "DefaultSession";

        [Header("Scenes")]
        [SerializeField] private string _scenrToLoad = "Level 1";

        [Inject] private MenuSessionService _sessionService;
        [Inject] private LaunchRequestStore _store;
        [Inject] private MainMenuUIStateController _ui;

        private bool _busy;
        private string _pendingName;

        private enum PendingAction
        {
            None,
            JoinClient,
            ShutdownThenJoinClient
        }

        private PendingAction _pendingAction = PendingAction.None;

        private void Awake()
        {
            EnsureDefaultSessionName();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnEnable()
        {
            if (_connectButton) _connectButton.onClick.AddListener(OnConnectClicked);
            if (_yesButton) _yesButton.onClick.AddListener(OnYes);
            if (_noButton) _noButton.onClick.AddListener(OnNo);
            _ui.SetPhase(MainMenuUiPhase.Main);
            EnsureDefaultSessionName();
        }

        private void OnDisable()
        {
            if (_connectButton) _connectButton.onClick.RemoveListener(OnConnectClicked);
            if (_yesButton) _yesButton.onClick.RemoveListener(OnYes);
            if (_noButton) _noButton.onClick.RemoveListener(OnNo);
        }

        private async void OnConnectClicked()
        {
            if (_busy) return;

            var sessionName = (_sessionInput ? _sessionInput.text : string.Empty)?.Trim();
            if (string.IsNullOrEmpty(sessionName)) sessionName = _defaultSessionName;

            _busy = true;
            _ui.SetPhase(MainMenuUiPhase.Loading);

            try
            {
                if (HasActiveRunner())
                {
                    _pendingName = sessionName;
                    _pendingAction = PendingAction.ShutdownThenJoinClient;
                    ShowConfirm($"В этом процессе уже запущена сессия.\nОстановить её и подключиться к «{sessionName}» как клиент?");
                    _busy = false;
                    return;
                }

                var check = await _sessionService.Check(sessionName, 5f);

                if (check == SessionCheck.Exists)
                {
                    _pendingName = sessionName;
                    _pendingAction = PendingAction.JoinClient;
                    ShowConfirm($"Сессия «{sessionName}» найдена. Подключиться как клиент?");
                    _busy = false;
                    return;
                }

                if (check == SessionCheck.Unknown)
                {
                    _pendingName = sessionName;
                    _pendingAction = PendingAction.JoinClient;
                    ShowConfirm($"Не удалось проверить наличие «{sessionName}». Подключиться как клиент?");
                    _busy = false;
                    return;
                }

                var finalCheck = await _sessionService.Check(sessionName, 2f);
                if (finalCheck == SessionCheck.Exists)
                {
                    _pendingName = sessionName;
                    _pendingAction = PendingAction.JoinClient;
                    ShowConfirm($"Сессия «{sessionName}» уже создана. Подключиться как клиент?");
                    _busy = false;
                    return;
                }

                await LoadGameAs(GameMode.Host, sessionName);
            }
            catch
            {
                _pendingName = sessionName;
                _pendingAction = PendingAction.JoinClient;
                ShowConfirm($"Ошибка при проверке «{sessionName}». Подключиться как клиент?");
                _busy = false;
                EnsureDefaultSessionName();
            }
        }

        private async void OnYes()
        {
            if (_busy) return;
            _busy = true;
            _ui.SetPhase(MainMenuUiPhase.Loading);

            var name = !string.IsNullOrWhiteSpace(_pendingName)
                ? _pendingName
                : (!string.IsNullOrWhiteSpace(_sessionInput?.text) ? _sessionInput.text.Trim() : _defaultSessionName);

            var act = _pendingAction;
            _pendingName = null;
            _pendingAction = PendingAction.None;

            try
            {
                switch (act)
                {
                    case PendingAction.ShutdownThenJoinClient:
                        await RunnerGuard.KillAllExcept(null);
                        await LoadGameAs(GameMode.Client, name);
                        break;

                    case PendingAction.JoinClient:
                        await LoadGameAs(GameMode.Client, name);
                        break;

                    default:
                        _ui.SetPhase(MainMenuUiPhase.Main);
                        _busy = false;
                        break;
                }
            }
            catch
            {
                ShowConfirm($"Не удалось подключиться к «{name}». Попробовать снова?");
                _busy = false;
            }
        }

        private void OnNo()
        {
            _pendingName = null;
            _pendingAction = PendingAction.None;
            _ui.SetPhase(MainMenuUiPhase.Main);
        }

        private void ShowConfirm(string message)
        {
            if (_confirmationText) _confirmationText.text = message;
            _ui.SetPhase(MainMenuUiPhase.Confirmation);
        }

        private async Task LoadGameAs(GameMode mode, string sessionName)
        {
            var name = string.IsNullOrWhiteSpace(sessionName) ? _defaultSessionName : sessionName;
            _store.Set(mode, name);
            await RunnerGuard.KillAllExcept(null);
            SceneManager.LoadScene(_scenrToLoad);
        }

        private void EnsureDefaultSessionName()
        {
            if (_sessionInput == null) return;
            var current = _sessionInput.text;
            if (string.IsNullOrWhiteSpace(current)) _sessionInput.text = _defaultSessionName;
        }

        private static bool HasActiveRunner()
        {
            var runners = Object.FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < runners.Length; i++)
            {
                var r = runners[i];
                if (r == null) continue;
                try { if (r.IsRunning) return true; }
                catch { return true; }
            }
            return false;
        }
    }
}
