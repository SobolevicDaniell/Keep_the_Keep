using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KeepTheKeep.UI
{
    public sealed class SessionStarting : MonoBehaviour
    {
        [SerializeField] private Button _gameLocalButton;
        [SerializeField] private Button _onlineButton;
        [SerializeField] private string _singleScene;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnEnable()
        {
            if (_gameLocalButton) _gameLocalButton.onClick.AddListener(StartSingle);
            if (_onlineButton) _onlineButton.onClick.AddListener(StartOnline);
        }

        private void OnDisable()
        {
            if (_gameLocalButton) _gameLocalButton.onClick.RemoveListener(StartSingle);
            if (_onlineButton) _onlineButton.onClick.RemoveListener(StartOnline);
        }

        private void StartSingle()
        {
            if (string.IsNullOrWhiteSpace(_singleScene)) return;
            SceneManager.LoadScene(_singleScene);
        }

        private void StartOnline()
        {
        }
    }
}
