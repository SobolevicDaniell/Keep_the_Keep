using System.Collections.Generic;

namespace Game
{
    public sealed class ContainerIdEqualityComparer : IEqualityComparer<ContainerId>
    {
        public bool Equals(ContainerId a, ContainerId b)
            => a.type == b.type && a.ownerRef == b.ownerRef && a.objectId.Equals(b.objectId);

        public int GetHashCode(ContainerId x)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (int)x.type;
                h = h * 31 + x.ownerRef.GetHashCode();
                h = h * 31 + x.objectId.GetHashCode();
                return h;
            }
        }
    }
}
