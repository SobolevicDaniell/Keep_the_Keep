using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

namespace Game.UI
{
    public sealed class GameControllerExit : MonoBehaviour
    {
        [SerializeField] private Button _quitGameButton;
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        [Inject(Optional = true)] private NetworkRunner _runner;
        [Inject(Optional = true)] private UIController _ui;

        private bool _busy;

        private void OnEnable()
        {
            if (_quitGameButton) _quitGameButton.onClick.AddListener(OnQuitGameClicked);
        }

        private void OnDisable()
        {
            if (_quitGameButton) _quitGameButton.onClick.RemoveListener(OnQuitGameClicked);
        }

        private void OnQuitGameClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetInteractable(bool v)
        {
            if (_quitGameButton) _quitGameButton.interactable = v;
        }

        private List<NetworkRunner> CollectRunners()
        {
            var list = new List<NetworkRunner>();
            if (_runner) list.Add(_runner);
            foreach (var r in FindObjectsOfType<NetworkRunner>(true))
                if (r && !list.Contains(r)) list.Add(r);
            return list.Where(r => r).ToList();
        }
    }
}
