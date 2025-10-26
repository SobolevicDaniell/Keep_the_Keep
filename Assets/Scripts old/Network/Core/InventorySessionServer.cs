using Fusion;
using Zenject;

namespace Game
{
    public sealed class InventorySessionServer
    {
        private readonly InventoryServerService _service;

        [Inject]
        public InventorySessionServer(InventoryServerService service)
        {
            _service = service;
        }

        public bool Open(PlayerRef viewer, ContainerId id, out ContainerSnapshot snapshot)
        {
            return _service.TryOpenContainer(viewer, id, out snapshot, out _);
        }

        public bool Close(PlayerRef viewer, ContainerId id)
        {
            return _service.TryCloseContainer(viewer, id, out _);
        }

        public bool TryOpenContainer(PlayerRef viewer, ContainerId id, out ContainerSnapshot snapshot, out string reason)
        {
            return _service.TryOpenContainer(viewer, id, out snapshot, out reason);
        }

        public bool TryCloseContainer(PlayerRef viewer, ContainerId id, out string reason)
        {
            return _service.TryCloseContainer(viewer, id, out reason);
        }
    }
}
