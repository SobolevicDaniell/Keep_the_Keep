using Game.UI;
using Zenject;

namespace Game
{
    public sealed class ClientInventoryInstaller : Installer<ClientInventoryInstaller>
    {
        public override void InstallBindings()
        {
            Container.Bind<InventoryClientModel>()
                .AsSingle()
                .NonLazy();

            Container.Bind<InventoryClientFacade>()
                .FromComponentInHierarchy()
                .AsSingle()
                .NonLazy();

            Container.Bind<ContainerViewSessionClient>()
                .AsSingle();

            Container.BindInterfacesAndSelfTo<InventoryService>()
                .AsSingle()
                .NonLazy();
        }
    }
}
