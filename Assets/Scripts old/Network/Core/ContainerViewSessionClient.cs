using System;
using Fusion;

namespace Game
{
    public sealed class ContainerViewSessionClient
    {
        private readonly InventoryClientFacade _facade;
        private readonly InventoryClientModel _model;

        public ContainerViewSessionClient(InventoryClientFacade facade, InventoryClientModel model)
        {
            _facade = facade;
            _model  = model;
        }

        public void Open(ContainerId id) => _facade.Open(id);
        public void Close(ContainerId id) => _facade.Close(id);
        public ContainerSnapshot Get(ContainerId id) => _model.Get(id);
        public event Action<ContainerId> OnContainerChanged { add => _model.OnContainerChanged += value; remove => _model.OnContainerChanged -= value; }
    }
}
