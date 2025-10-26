using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public class MenuCallbacks : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Inject(Optional = true)] private NetworkRunner _runner;
        [Inject(Optional = true)] private Startup _startup;

        private void OnEnable()
        {
            if (_runner == null) _runner = FindObjectOfType<NetworkRunner>(true);
            if (_runner != null) _runner.AddCallbacks(this);
        }

        private void OnDisable()
        {
            if (_runner != null) _runner.RemoveCallbacks(this);
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            _startup?.SetSessions(sessionList);
        }

        public void OnConnectedToServer(NetworkRunner runner) {}
        public void OnSceneLoadStart(NetworkRunner runner) {}
        public void OnSceneLoadDone(NetworkRunner runner) {}
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {}
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {}
        public void OnInput(NetworkRunner runner, NetworkInput input) {}
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {}
        public void OnDisconnectedFromServer(NetworkRunner runner) {}
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {}
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
        public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) {}
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) {}
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason){}

      
    }
}
