using Fusion;
using UnityEngine;
using Zenject;
using Game.Network;
using Game.UI;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerObject : NetworkBehaviour
    {
        [Inject] private PlayerSpawner _spawner;
        [Inject(Optional = true)] private UIController _ui;

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestRespawn(RpcInfo info = default)
        {
            var player = info.Source;
            if (player == PlayerRef.None)
            {
                player = Object != null ? Object.InputAuthority : PlayerRef.None;
                if (player == PlayerRef.None && Runner != null) player = Runner.LocalPlayer;
            }
            if (player == PlayerRef.None) return;

            _spawner.RespawnPlayer(Runner, player);
            
        }


        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        public void RPC_ShowDeath()
        {
            if (_ui != null) _ui.SetPhase(UiPhase.Spawn);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        public void RPC_ShowGameplay()
        {
            if (_ui != null) _ui.SetPhase(UiPhase.Gameplay);
            Debug.Log("RPC_ShowGameplay");
        }



    }
}