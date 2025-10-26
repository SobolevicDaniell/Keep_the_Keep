using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class Startup : MonoBehaviour
    {
        [SerializeField] private SceneRef _levelScene;
        [SerializeField] private bool _dontDestroyRunner = true;

        [Inject] private NetworkRunner _runner;
        [Inject(Optional = true)] private NetworkSceneManagerDefault _sceneManager;
        [Inject] private INetworkObjectProvider _provider;

        private readonly List<SessionInfo> _sessions = new();
        private TaskCompletionSource<List<SessionInfo>> _sessionListTcs;

        private void Awake()
        {
            if (_dontDestroyRunner) DontDestroyOnLoad(_runner.gameObject);
            EnsureSceneManager();
        }

        public async Task RefreshSessionList()
        {
            _sessionListTcs = new TaskCompletionSource<List<SessionInfo>>();
            await _runner.JoinSessionLobby(SessionLobby.Shared);
            await _sessionListTcs.Task;
        }

        public async Task<bool> CheckSessionExists(string name)
        {
            _sessionListTcs = new TaskCompletionSource<List<SessionInfo>>();
            await _runner.JoinSessionLobby(SessionLobby.Shared);
            var list = await _sessionListTcs.Task;
            return list.Any(s => s.Name == name);
        }

        public void SetSessions(List<SessionInfo> list)
        {
            _sessions.Clear();
            _sessions.AddRange(list);
            _sessionListTcs?.TrySetResult(list);
            _sessionListTcs = null;
        }

        public async Task BeginSession(GameMode mode, string sessionName)
        {
            EnsureSceneManager();
            _runner.ProvideInput = true;

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                SceneManager = _sceneManager,
                ObjectProvider = _provider
            };

            if (mode == GameMode.Host)
                args.Scene = _levelScene;

            await _runner.StartGame(args);
        }

        public async Task StartHost(string sessionName)
        {
            await BeginSession(GameMode.Host, sessionName);
        }

        public async Task StartClient(string sessionName)
        {
            await BeginSession(GameMode.Client, sessionName);
        }

        private void EnsureSceneManager()
        {
            if (_sceneManager == null)
                _sceneManager = _runner.GetComponent<NetworkSceneManagerDefault>() ?? _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        }
    }
}
