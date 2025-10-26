using Zenject;
using UnityEngine;
using Game.Settings;

namespace Game
{
    public class ProjectInstaller : MonoInstaller
    {
        [SerializeField] private PlayerStatsSO _playerStats;
        [SerializeField] private AudioSettingsSO _audioSettings;

        public override void InstallBindings()
        {
            Container.BindInstance(_playerStats).AsSingle();
            Container.BindInstance(_audioSettings).AsSingle();
            Container.BindInterfacesTo<SettingsService>().AsSingle().NonLazy();
        }
    }
}
