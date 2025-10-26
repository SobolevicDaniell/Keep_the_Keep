using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    
    public sealed class FusionZenjectInjector :
        IInitializable, IDisposable, INetworkRunnerCallbacks
    {
        private readonly DiContainer _container;
        private readonly NetworkRunner _runner;

        [Inject]
        public FusionZenjectInjector(DiContainer container, NetworkRunner runner)
        {
            _container = container;
            _runner = runner;
        }

        public void Initialize()
        {
            if (_runner == null)
            {
                Debug.LogError("[FusionZenjectInjector] NetworkRunner is null — cannot subscribe.");
                return;
            }
            _runner.AddCallbacks(this);
        }

        public void Dispose()
        {
            if (_runner != null)
                _runner.RemoveCallbacks(this);
        }

       
        public void OnObjectSpawned(NetworkRunner runner, NetworkObject obj)
        {
            if (obj == null) return;
            try
            {
                _container.InjectGameObject(obj.gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FusionZenjectInjector] Inject failed for '{obj?.name}': {ex}");
            }
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
        }
    }
}
