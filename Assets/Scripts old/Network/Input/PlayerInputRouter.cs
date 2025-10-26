using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class PlayerInputRouter : NetworkBehaviour
    {
        [Inject] private InputHandler _input;
        [Inject(Optional = true)] private InventoryClientFacade _inventoryFacade;
        [Inject(Optional = true)] private InventoryService _inventory;

        [SerializeField] private float _placeMaxDistance = 4f;
        [SerializeField] private float _dropCooldown = 0.12f;

        private PlayerRpcHandler _playerRpc;
        private QuickSlotController _quick;
        private PickDropController _pickDrop;
        private InteractionController _ic;

        private float _nextDropTime;
        private bool _isFiring;

        public override void Spawned()
        {
            _playerRpc ??= GetComponent<PlayerRpcHandler>();
            _quick ??= GetComponent<QuickSlotController>();
            _pickDrop ??= GetComponent<PickDropController>();
            _ic ??= GetComponent<InteractionController>();

            InventoryRpcRouter router = null;
            NetworkId poId = default;

            if (Runner != null && Runner.TryGetPlayerObject(Object.InputAuthority, out var po) && po != null)
            {
                router = po.GetComponentInChildren<InventoryRpcRouter>(true);
                poId = po.Id;
            }

            if (router == null)
            {
                var all = FindObjectsOfType<InventoryRpcRouter>(true);
                for (int i = 0; i < all.Length && router == null; i++)
                    if (all[i].Object != null && all[i].Object.HasInputAuthority)
                        router = all[i];
            }

            if (router == null) return;

            if (_inventoryFacade != null)
            {
                _inventoryFacade.SetLocal(Object.InputAuthority, router, poId);
                _inventoryFacade.OpenLocalQuick();
                _inventoryFacade.OpenLocalMain();
            }
            else
            {
                StartCoroutine(router.RetryOpenContainer((int)ContainerType.PlayerQuick, Object.InputAuthority, poId));
                StartCoroutine(router.RetryOpenContainer((int)ContainerType.PlayerMain, Object.InputAuthority, poId));
            }
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.OnReloadPressed += OnReload;
                _input.OnPlacePressed += OnPlace;
                _input.OnQuickDropPressed += OnQuickDrop;
                _input.OnInteractPressed += OnInteract;
                _input.OnUseDown += OnUseDown;
                _input.OnUseUp += OnUseUp;
                _input.OnQuickSlotNext += OnQuickSlotNext;
                _input.OnQuickSlotPrev += OnQuickSlotPrev;
                _input.OnQuickSlotSelect += OnQuickSlotSelect;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.OnReloadPressed -= OnReload;
                _input.OnPlacePressed -= OnPlace;
                _input.OnQuickDropPressed -= OnQuickDrop;
                _input.OnInteractPressed -= OnInteract;
                _input.OnUseDown -= OnUseDown;
                _input.OnUseUp -= OnUseUp;
                _input.OnQuickSlotNext -= OnQuickSlotNext;
                _input.OnQuickSlotPrev -= OnQuickSlotPrev;
                _input.OnQuickSlotSelect -= OnQuickSlotSelect;
            }
        }

        private void Update()
        {
            if (!HasInputAuthority) return;
            if (_input != null && _input.IsBlok) return;

            if (_isFiring)
            {
                var beh = _ic != null ? _ic.currentBehavior : null;
                var wb = beh as WeaponBehavior;
                if (wb != null) wb.OnUseHeld(Time.deltaTime);
                else beh?.OnUseHeld(Time.deltaTime);
            }
        }

        private void OnQuickSlotNext()
        {
            if (!HasInputAuthority) return;
            _quick?.ChangeSlotRelative(1);
        }

        private void OnQuickSlotPrev()
        {
            if (!HasInputAuthority) return;
            _quick?.ChangeSlotRelative(-1);
        }

        private void OnQuickSlotSelect(int index)
        {
            if (!HasInputAuthority) return;
            int quickLen = _inventory?.GetQuickSlots()?.Length ?? 0;
            if (quickLen <= 0) return;

            if (index >= 0)
            {
                _quick?.ChangeSlotAbsolute(index);
            }
            else
            {
                int last = Mathf.Clamp(quickLen - 1, 0, quickLen - 1);
                _quick?.ChangeSlotAbsolute(last);
            }
        }

        private void OnUseDown()
        {
            if (!HasInputAuthority) return;
            if (_input != null && _input.IsBlok) return;

            var beh = _ic != null ? _ic.currentBehavior : null;
            var wb = beh as WeaponBehavior;

            _isFiring = true;
            if (wb != null && wb.IsValid()) wb.OnUsePressed();
            else beh?.OnUsePressed();
        }

        private void OnUseUp()
        {
            if (!HasInputAuthority) return;
            _isFiring = false;
            _ic?.currentBehavior?.OnUseReleased();
        }

        private void OnReload()
        {
            if (!HasInputAuthority) return;
            int idx = _inventory != null ? _inventory.SelectedQuickSlot : -1;
            _playerRpc?.RPC_RequestReload(idx);
        }

        private void OnInteract()
        {
            if (!HasInputAuthority) return;
            if (_ic != null && _ic.TryOpenContainerAtCrosshair()) return;
            _pickDrop?.TryPickAtCrosshair();
        }

        private void OnQuickDrop()
        {
            if (!HasInputAuthority) return;
            if (Time.unscaledTime < _nextDropTime) return;

            _pickDrop?.TryDrop();
            _nextDropTime = Time.unscaledTime + _dropCooldown;
        }

        private void OnPlace()
        {
            if (!HasInputAuthority) return;

            Vector3 origin;
            Vector3 forward;
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam != null) { origin = cam.position; forward = cam.forward; }
            else
            {
                var t = (_ic != null) ? _ic.transform : transform;
                origin = t.position + Vector3.up * 1.6f;
                forward = t.forward;
            }

            Vector3 pos;
            Quaternion rot;
            if (Physics.Raycast(origin, forward, out var hit, _placeMaxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                pos = hit.point;
                var flat = Vector3.ProjectOnPlane(forward, Vector3.up);
                rot = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
            }
            else
            {
                pos = origin + forward.normalized * _placeMaxDistance;
                var flat = Vector3.ProjectOnPlane(forward, Vector3.up);
                rot = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
            }

            _pickDrop?.TryPlaceFromQuickSlot(pos, rot);
        }
    }
}
