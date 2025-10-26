using System.Collections.Generic;
using Fusion;

namespace Game
{
    public sealed class InventoryContainerRegistry
    {
        private readonly Dictionary<ContainerId, IInventoryContainer> _containers
            = new Dictionary<ContainerId, IInventoryContainer>(new ContainerIdEqualityComparer());

        private readonly Dictionary<ContainerId, HashSet<PlayerRef>> _watchers
            = new Dictionary<ContainerId, HashSet<PlayerRef>>(new ContainerIdEqualityComparer());

        public void Register(IInventoryContainer container)
        {
            _containers[container.Id] = container;
            if (!_watchers.ContainsKey(container.Id)) _watchers[container.Id] = new HashSet<PlayerRef>();
        }

        public void Unregister(ContainerId id)
        {
            _containers.Remove(id);
            _watchers.Remove(id);
        }

        public bool TryGet(ContainerId id, out IInventoryContainer container)
            => _containers.TryGetValue(id, out container);

        public void AddWatcher(ContainerId id, PlayerRef viewer)
        {
            if (!_watchers.TryGetValue(id, out var set))
            {
                set = new HashSet<PlayerRef>();
                _watchers[id] = set;
            }
            set.Add(viewer);
        }

        public IEnumerable<PlayerRef> Watchers(ContainerId id)
            => _watchers.TryGetValue(id, out var set) ? set : System.Array.Empty<PlayerRef>();
    }
}
