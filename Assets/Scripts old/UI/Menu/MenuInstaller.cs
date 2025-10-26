using Game.UI;
using UnityEngine;
using Zenject;

namespace Game.Network
{
    public sealed class MenuInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<MenuSessionService>().AsSingle();
            var storeGo = new GameObject("LaunchRequestStore");
            var store = storeGo.AddComponent<LaunchRequestStore>();
            Container.Bind<LaunchRequestStore>().FromInstance(store).AsSingle();

            Container.Bind<MainMenuUIStateController>().FromComponentInHierarchy().AsSingle();
            Container.Bind<SessionStarting>().FromComponentInHierarchy().AsSingle();
        }
    }
}
 