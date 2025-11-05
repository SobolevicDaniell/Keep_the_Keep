using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace KeepTheKeep.UI
{
    public sealed class GameControllerExit : MonoBehaviour
    {
        [SerializeField] private Button _quitGameButton;

        [Inject(Optional = true)] private NetworkRunner _runner;

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
            if (_busy) return;
            _busy = true;
            SetInteractable(false);
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
