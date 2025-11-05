using Zenject;
using UnityEngine;

namespace Game.Local
{
    public class LocalGameInstaller : MonoInstaller
    {
        [SerializeField] private PlayerStatsSO _playerStats;
        [SerializeField] private GameObject _character;

        public override void InstallBindings()
        {
            Container.BindInstance(_playerStats).AsSingle();
            Container.BindInstance(_character).AsSingle();
        }
    }
}