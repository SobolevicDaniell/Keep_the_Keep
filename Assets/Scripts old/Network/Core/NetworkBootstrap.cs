using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkRunner runner;
        [Inject] private INetworkObjectProvider _provider;

        public async Task StartRunner(StartGameArgs args)
        {
            args.ObjectProvider = _provider;
            await runner.StartGame(args);
        }
    }
}
