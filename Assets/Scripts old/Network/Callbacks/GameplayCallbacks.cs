using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Zenject;
using Game.UI;
using Fusion.Sockets;
using System;

namespace Game.Network
{
    public class GameplayCallbacks : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Inject(Optional = true)] private NetworkRunner _runner;
        [Inject] private PlayerSpawner _playerSpawner;
        [Inject] private InputHandler _inputHandler;
        [Inject] private DiContainer _container;
        [Inject] private UIController uIController;

        private void OnEnable()
        {
            if (_runner != null) _runner.AddCallbacks(this);
        }

        private void OnDisable()
        {
            if (_runner != null) _runner.RemoveCallbacks(this);
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            var all = FindObjectsOfType<NetworkObject>();
            for (int i = 0; i < all.Length; i++)
                _container.InjectGameObject(all[i].gameObject);
            uIController.SetPhase(UiPhase.Spawn);
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            _playerSpawner.EnsurePlayerObject(runner, player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            _playerSpawner.DespawnAvatar(runner, player, null);
            _playerSpawner.RemovePlayerObject(runner, player);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            _inputHandler.ProvideNetworkInput(runner, input);
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
            if (player == runner.LocalPlayer)
            {
                var last = _inputHandler.GetLastInputData();
                input.Set(last);
            }
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnDisconnectedFromServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            if (obj != null) _container.InjectGameObject(obj.gameObject);
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
