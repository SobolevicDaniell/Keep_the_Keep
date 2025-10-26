using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public sealed class GameStartup : MonoBehaviour
    {
        [SerializeField] private NetworkRunner _runner;
        [SerializeField] private SceneRef _thisLevelScene;

        [Inject(Optional = true)] private NetworkSceneManagerDefault _sceneManager;
        [Inject] private INetworkObjectProvider _provider;
        [Inject(Optional = true)] private LaunchRequestStore _store;
        [Inject(Optional = true)] private GameplayCallbacks _gameplayCallbacks;

        private bool _starting;

        private void EnsureSceneManager()
        {
            if (_sceneManager == null)
            {
                _sceneManager = _runner.GetComponent<NetworkSceneManagerDefault>();
                if (_sceneManager == null)
                    _sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }
        }

        private void EnsureStore()
        {
            if (_store == null)
                _store = FindAnyObjectByType<LaunchRequestStore>(FindObjectsInactive.Include);
        }

        private void EnsureRunner()
        {
            if (_runner == null)
                _runner = FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);
        }

        private async void Start()
        {
            if (_starting) return;
            _starting = true;
            try
            {
                EnsureRunner();
                if (_runner == null)
                {
                    Debug.LogError("[GameStartup] NetworkRunner not found");
                    return;
                }

                try { if (_runner.IsRunning) { Debug.Log("[GameStartup] Runner already running"); return; } } catch { }

                RunnerGuard.DumpRunners("Level1 before kill");
                await RunnerGuard.KillAllExcept(_runner);
                RunnerGuard.DumpRunners("Level1 after kill");

                EnsureSceneManager();
                EnsureStore();

                GameMode mode = GameMode.Host;
                string sessionName = "DefaultSession";
                if (_store != null && _store.TryConsume(out var m, out var sn))
                {
                    mode = m;
                    sessionName = string.IsNullOrWhiteSpace(sn) ? sessionName : sn;
                }

                if (_gameplayCallbacks != null)
                    _runner.AddCallbacks(_gameplayCallbacks);

                var args = new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = sessionName,
                    SceneManager = _sceneManager,
                    ObjectProvider = _provider
                };
                if (mode == GameMode.Host)
                {
                    try { await _runner.JoinSessionLobby(SessionLobby.Shared); }
                    catch (System.Exception e) { Debug.LogWarning($"[GameStartup] JoinSessionLobby (host) failed: {e.Message}"); }

                    args.Scene = _thisLevelScene;
                }
                await _runner.StartGame(args);

            }
            finally
            {
                _starting = false;
            }
        }
    }
}