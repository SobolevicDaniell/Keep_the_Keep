using Fusion;
using Game.Gameplay;
using UnityEngine;
using Zenject;
using Game.UI;

namespace Game.Network
{
    public class NetworkInstaller : MonoInstaller
    {
        [Header("Player Prefab")]
        [SerializeField] private GameObject _playerPrefab;

        [Header("Inventory")]
        [SerializeField] private ItemDatabaseSO _itemDatabase;

        [Header("Prefabs")]
        [SerializeField] private InventorySlotUI _slotPrefab;
        [SerializeField] private GameObject _deathBoxPrefab;
        [SerializeField] private GameObject _playerObjectPrefab;
        [SerializeField] private NetworkObject _avatarPrefab;

        [Header("UI Panels")]
        [SerializeField] private InventoryPanel _playerInventoryPanel;
        [SerializeField] private OtherInventoryPanel _otherInventoryPanel;

        // [Header("Config")]
        // [SerializeField] private PlayerStatsSO _playerStats;

        public override void InstallBindings()
        {
            Container.BindInstance(_playerPrefab).WithId("PlayerPrefab");
            Container.BindInstance(_deathBoxPrefab).WithId("DeathBoxPrefab");
            Container.BindInstance(_playerObjectPrefab).WithId("PlayerObjectPrefab");
            Container.Bind<NetworkObject>().WithId("AvatarPrefab").FromInstance(_avatarPrefab).AsSingle();

            Container.Bind<ItemDatabaseSO>().FromInstance(_itemDatabase).AsSingle();

            Container.Bind<UIController>().FromComponentInHierarchy().AsSingle();
            Container.Bind<InteractionPromptView>().FromComponentInHierarchy().AsSingle();

            Container.Bind<InventoryPanel>().WithId("PlayerInventoryPanel").FromComponentInHierarchy().AsSingle();
            Container.Bind<OtherInventoryPanel>().WithId("OtherInventoryPanel").FromComponentInHierarchy().AsSingle();
            Container.Bind<QuickSlotPanel>().FromComponentInHierarchy().AsSingle();

            Container.Bind<InputHandler>().FromComponentInHierarchy().AsSingle();
            Container.Bind<HandItemBehaviorFactory>().AsSingle();

            Container.Bind<NetworkRunner>().FromComponentInHierarchy().AsSingle();

            Container.Bind<IPlayerFactory>().To<PlayerFactory>().AsSingle();
            Container.Bind<PlayerSpawner>().AsSingle();

            Container.Bind<INetworkObjectProvider>().To<ZenjectObjectProvider>().AsSingle();

            Container.Bind<GameplayCallbacks>().FromComponentInHierarchy().AsSingle();

            Container.Bind<InventorySlotUI>().WithId("InventorySlotPrefab").FromInstance(_slotPrefab).AsSingle();

            Container.Bind<InventoryViewService>().AsSingle().NonLazy();

            // Container.Bind<PlayerStatsSO>().FromInstance(_playerStats).AsSingle();
            Container.BindInterfacesAndSelfTo<FusionZenjectInjector>().AsSingle();

            Container.Bind<ISpawnPointProvider>().FromComponentInHierarchy().AsSingle();

            ClientInventoryInstaller.Install(Container);
            ServerInventoryInstaller.Install(Container);

            if (!Application.isBatchMode)
            {
                Container.Bind<InventoryTransferController>().FromComponentInHierarchy().AsSingle();
                Container.Bind<UIHealthView>().FromComponentInHierarchy().AsSingle();
                Container.Bind<HealthClientModel>().AsSingle();
            }

            var store = Object.FindObjectOfType<LaunchRequestStore>(true);
            if (store != null)
            {
                Container.Bind<LaunchRequestStore>().FromInstance(store).AsSingle();
            }
        }
    }
}
