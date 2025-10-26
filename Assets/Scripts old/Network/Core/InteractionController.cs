using Fusion;
using UnityEngine;
using Zenject;
using Game.UI;
using Game.Network;

namespace Game
{
    [RequireComponent(typeof(PlayerRpcHandler), typeof(NetworkObject))]
    public class InteractionController : NetworkBehaviour
    {
        [Header("Points")]
        [SerializeField] private Transform _handPoint;
        [SerializeField] private Transform _dropPoint;

        [Header("Ranges / Forces")]
        [SerializeField] private float _rangePick = 4f;
        [SerializeField] private float _rangePlace = 8f;
        [SerializeField] private float _throwForce = 5f;

        [Inject(Optional = true)] private InputHandler _input;
        // [Inject] private InteractionPromptView _prompt;
        [Inject] private InventoryService _inventory;
        [Inject] private HandItemBehaviorFactory _factory;
        [Inject] private ItemDatabaseSO _db;
        [Inject(Optional = true)] private UIHealthView _uiHealth;
        [Inject] private UIController _ui;
        [Inject(Id = "PlayerInventoryPanel")] private InventoryPanel _playerPanel;
        [Inject(Id = "OtherInventoryPanel")] private OtherInventoryPanel _otherPanel;
        [Inject(Optional = true)] private QuickSlotPanel _quickSlotPanel;
        [Inject(Optional = true)] private PlayerCameraController _playerCam;
        [Inject(Optional = true)] private InventoryClientFacade _inventoryFacade;
        [Inject(Optional = true)] private InventoryTransferController _transfer;

        private PlayerRpcHandler _playerRpc;
        private HandItemController _handItemController;
        private ItemEquipController _itemEquip;
        private PickDropController _pickDrop;
        private PlaceItemController _placeItem;
        private QuickSlotController _quickSlot;

        public IHandItemBehavior currentBehavior { get; private set; }

        [Networked] public int netSelectedQuickSlot { get; set; } = -1;
        [Networked] public NetworkId handModelNetId { get; set; }
        [Networked] public int SelectedQuickIndexNet { get; private set; }

        private int _lastSelectedQuickIndexNet = int.MinValue;
        private NetworkObject _handModelNetObj;

        public Transform handPoint => _handPoint;
        public Transform dropPoint => _dropPoint;
        public float range => _rangePick;
        public float PlaceRange => _rangePlace;
        public float ThrowForce => _throwForce;

        private InventoryRpcRouter _router;
        private bool _routerBound;
        private UiPhase _savedPhaseBeforeMenu;


        public Camera camera
        {
            get
            {
                if (_playerCam != null)
                {
                    var cam = _playerCam.GetComponentInChildren<Camera>(true);
                    if (cam != null) return cam;
                }
                return Object.HasInputAuthority ? Camera.main : null;
            }
        }

        public ItemDatabaseSO db => _db;
        public InventoryService inventory => _inventory;
        public UIController uiController => _ui;
        public PlayerRpcHandler playerRpcHandler => _playerRpc;
        public HandItemController handItemController => _handItemController;
        public QuickSlotController quickSlot => _quickSlot;
        public PickDropController pickDrop => _pickDrop;
        public PlaceItemController placeItem => _placeItem;
        public ItemEquipController itemEquip => _itemEquip;

        private bool _localInitialized;
        private string _lastEquipKey;

        public Vector3 GetDropPointPosition()
        {
            return _dropPoint != null ? _dropPoint.position : transform.position + transform.forward;
        }

        public Vector3 GetDropForward()
        {
            return _dropPoint != null ? _dropPoint.forward : transform.forward;
        }

        public override void Spawned()
        {
            _playerRpc ??= GetComponent<PlayerRpcHandler>();
            _handItemController ??= GetComponent<HandItemController>();
            _itemEquip ??= GetComponent<ItemEquipController>();
            _pickDrop ??= GetComponent<PickDropController>();
            _placeItem ??= GetComponent<PlaceItemController>();
            _quickSlot ??= GetComponent<QuickSlotController>();

            _playerRpc?.Construct(_db, this, _inventory);

            _quickSlot?.Construct(this, _inventory);
            _handItemController?.Construct(_db, _playerRpc, this);
            _placeItem?.Construct(this);
            _itemEquip?.Initialize(_factory, _db, this);
            _pickDrop?.Construct(this, _playerRpc, _inventory, _playerPanel, _otherPanel, _db, _ui, null);

            if (Object.HasInputAuthority && _inventory != null)
                _inventory.OnQuickSlotsChanged += OnQuickSlotsChanged;

            if (_input != null && !_localInitialized)
            {
                _input.OnInventoryToggle += ToggleInventory;
                _input.OnGlobalUiToggleMenu += ToggleMenu;
                _localInitialized = true;
            }

            _transfer?.Initialize(_inventory, _playerPanel, _quickSlotPanel, _otherPanel, this, _inventoryFacade);

            if (Object.HasInputAuthority)
            {
                _quickSlot?.EnableForLocal();
                TryBindRouter();
            }
            else
            {
                _quickSlot?.DisableForLocal();
            }

            var sel = _inventory != null ? _inventory.SelectedQuickSlot : -1;
            if (sel >= 0)
                _playerRpc?.RPC_RequestEquipQuickSlot(sel);

            _savedPhaseBeforeMenu = _ui.Phase;

            if (Object.HasInputAuthority) _ui?.BindPromptSource(_pickDrop);

        }

        private InventoryRpcRouter ResolveRouter()
        {
            if (Runner == null) return null;
            if (!Runner.TryGetPlayerObject(Object.InputAuthority, out var po) || po == null) return null;
            var r = po.GetComponent<InventoryRpcRouter>();
            if (r != null) return r;
            r = po.GetComponentInChildren<InventoryRpcRouter>(true);
            return r;
        }

        private void Update()
        {
            if (Object.HasInputAuthority)
            {
                _pickDrop?.UpdateRaycast();
                if (!_routerBound)
                    TryBindRouter();
            }
        }

        private void TryBindRouter()
        {
            if (!Object.HasInputAuthority) return;
            if (_routerBound) return;

            _router ??= ResolveRouter();
            if (_router == null) return;

            var poId = default(NetworkId);
            if (Runner != null && Runner.TryGetPlayerObject(Object.InputAuthority, out var po) && po != null)
                poId = po.Id;

            _inventoryFacade?.SetLocal(Object.InputAuthority, _router, poId);

            StartCoroutine(_router.RetryOpenContainer((int)ContainerType.PlayerQuick, Object.InputAuthority, poId));
            StartCoroutine(_router.RetryOpenContainer((int)ContainerType.PlayerMain, Object.InputAuthority, poId));

            _routerBound = true;
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnQuickSlotsChanged -= OnQuickSlotsChanged;

            if (_localInitialized && _input != null)
            {
                _input.OnInventoryToggle -= ToggleInventory;
                _input.OnGlobalUiToggleMenu -= ToggleMenu;
            }

            _quickSlot?.DisableForLocal();
            if (Object.HasInputAuthority) _ui?.BindPromptSource(null);
        }

        public override void Render()
        {
            if (!HasInputAuthority) return;

            if (_lastSelectedQuickIndexNet != SelectedQuickIndexNet)
            {
                _lastSelectedQuickIndexNet = SelectedQuickIndexNet;
                _inventory?.ForceSetQuickSlot(SelectedQuickIndexNet);
            }
        }

        public void SetCurrentBehavior(IHandItemBehavior behavior)
        {
            currentBehavior = behavior;
        }

        public void ClearBehavior()
        {
            currentBehavior?.OnUnequip();
            currentBehavior = null;
        }

        public void SetHandModelNetworkInstance(NetworkObject netObj)
        {
            _handModelNetObj = netObj;
            handModelNetId = (netObj != null) ? netObj.Id : default;
        }

        public NetworkObject GetHandModelNetworkInstance()
        {
            if (_handModelNetObj != null) return _handModelNetObj;
            if (handModelNetId != default && Runner != null)
                _handModelNetObj = Runner.FindObject(handModelNetId);
            return _handModelNetObj;
        }

        public void InvokeOnQuickSlotsChanged() => OnQuickSlotsChanged();

        private void ToggleInventory()
        {
            if (!Object.HasInputAuthority) return;
            if (_ui.Phase == UiPhase.Spawn || _ui.Phase == UiPhase.Loading || _ui.Phase == UiPhase.Hidden) return;

            if (_ui.InventoryOpened)
            {
                _ui.SetPhase(UiPhase.Gameplay);
                _pickDrop.CloseOpenedInventories();
            }
            else
            {
                _ui.SetPhase(UiPhase.Inventory);
                _pickDrop.OpenPlayerInventory();
            }
        }

        private void ToggleMenu()
        {
            if (!Object.HasInputAuthority) return;
            if (_ui.Phase == UiPhase.Spawn || _ui.Phase == UiPhase.Loading || _ui.Phase == UiPhase.Hidden) return;

            if (_ui.Phase != UiPhase.Menu)
            {
                _savedPhaseBeforeMenu = _ui.Phase;
                _ui.SetPhase(UiPhase.Menu);
            }
            else
            {
                _ui.SetPhase(_savedPhaseBeforeMenu);
                if (_savedPhaseBeforeMenu == UiPhase.Inventory)
                    _pickDrop?.OpenPlayerInventory();
            }
        }

        private void OnQuickSlotsChanged()
        {
            if (_itemEquip == null || _inventory == null)
                return;

            int idx = _inventory.SelectedQuickSlot;
            var slots = _inventory.GetQuickSlots();

            if (slots == null || idx < 0 || idx >= slots.Length)
            {
                const string noneKey = "-1:null";
                if (_lastEquipKey != noneKey)
                {
                    _itemEquip.Equip(-1, slots);
                    _lastEquipKey = noneKey;
                }
                return;
            }

            var slot = slots[idx];
            var id = slot?.Id;
            string newKey = $"{idx}:{(string.IsNullOrEmpty(id) ? "null" : id)}";

            if (_lastEquipKey == newKey)
                return;

            _itemEquip.Equip(idx, slots);
            _lastEquipKey = newKey;
        }

        public void ServerSetSelectedQuickIndex(int idx)
        {
            if (!Object.HasStateAuthority) return;
            SelectedQuickIndexNet = idx;
        }
        public bool TryOpenContainerAtCrosshair()
        {
            if (_inventoryFacade == null) return false;
            var cam = camera;
            if (cam == null) return false;

            var origin = cam.transform.position;
            var dir = cam.transform.forward;

            if (!Physics.Raycast(origin, dir, out var hit, range, ~0, QueryTriggerInteraction.Collide)) return false;

            var no = hit.collider.GetComponentInParent<NetworkObject>();
            if (no == null) return false;

            var corpse = no.GetComponent<CorpseInventoryServer>();
            if (corpse != null)
            {
                var id = ContainerId.OfObject(ContainerType.Corpse, no.Id);
                _inventoryFacade.Open(id);
                _otherPanel.ShowRemote(id);
                _ui?.SetPhase(UiPhase.OtherInventory);
                return true;
            }

            var chest = no.GetComponent<ChestInventoryServer>();
            if (chest != null)
            {
                var id = ContainerId.OfObject(ContainerType.Chest, no.Id);
                _inventoryFacade.Open(id);
                _otherPanel.ShowRemote(id);
                _ui?.SetPhase(UiPhase.OtherInventory);
                return true;
            }

            return false;
        }



    }
}