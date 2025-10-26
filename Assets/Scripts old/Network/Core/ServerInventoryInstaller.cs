using Zenject;

namespace Game
{
    public sealed class ServerInventoryInstaller : Installer<ServerInventoryInstaller>
    {
        public override void InstallBindings()
        {
            Container.Bind<InventoryContainerRegistry>()
                     .AsSingle()
                     .NonLazy();

            Container.Bind<InventoryServerService>().AsSingle();
            Container.Bind<InventorySnapshotBuilder>().AsSingle();
            Container.Bind<InventorySessionServer>().AsSingle();
        }
    }
}
