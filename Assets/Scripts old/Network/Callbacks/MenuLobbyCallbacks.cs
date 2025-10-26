using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Game.Network
{
    public sealed class MenuLobbyCallbacks : MonoBehaviour, INetworkRunnerCallbacks
    {
        public TaskCompletionSource<List<SessionInfo>> Tcs;

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            Tcs?.TrySetResult(sessionList);
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Tcs?.TrySetResult(new List<SessionInfo>());
        }

        public void OnConnectedToServer(NetworkRunner r) {}
        public void OnDisconnectedFromServer(NetworkRunner r) {}
        public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest req, byte[] token) {}
        public void OnConnectFailed(NetworkRunner r, NetAddress addr, NetConnectFailedReason reason) { Tcs?.TrySetResult(new List<SessionInfo>()); }
        public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr msg) {}
        public void OnCustomAuthenticationResponse(NetworkRunner r, System.Collections.Generic.Dictionary<string, object> data) {}
        public void OnHostMigration(NetworkRunner r, HostMigrationToken t) {}
        public void OnReliableDataReceived(NetworkRunner r, PlayerRef p, ReliableKey k, System.ArraySegment<byte> data) {}
        public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey k, float progress) {}
        public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) {}
        public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) {}
        public void OnPlayerJoined(NetworkRunner r, PlayerRef p) {}
        public void OnPlayerLeft(NetworkRunner r, PlayerRef p) {}
        public void OnInput(NetworkRunner r, NetworkInput input) {}
        public void OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) {}
        public void OnConnectedToServer(NetworkRunner r, NetAddress addr) {}
        public void OnSceneLoadStart(NetworkRunner r) {}
        public void OnSceneLoadDone(NetworkRunner r) {}

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
        }
    }
}
