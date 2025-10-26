using Fusion;
using System;

namespace Game
{
    public enum ContainerType : byte
    {
        PlayerQuick = 0,
        PlayerMain  = 1,
        Chest       = 2,
        Corpse      = 3,
        Custom      = 4,
    }

    [Serializable]
    public struct ContainerId : IEquatable<ContainerId>
    {
        public ContainerType type;
        public PlayerRef ownerRef;
        public NetworkId objectId;

        public static ContainerId PlayerQuickOf(PlayerRef pref) =>
            new ContainerId { type = ContainerType.PlayerQuick, ownerRef = pref };

        public static ContainerId PlayerMainOf(PlayerRef pref) =>
            new ContainerId { type = ContainerType.PlayerMain, ownerRef = pref };

        public static ContainerId OfObject(ContainerType type, NetworkId objId) =>
            new ContainerId { type = type, objectId = objId };

        public bool Equals(ContainerId other) =>
            type == other.type && ownerRef == other.ownerRef && objectId == other.objectId;

        public override bool Equals(object obj) => obj is ContainerId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)type, ownerRef.RawEncoded, objectId.Raw);
        public override string ToString() => $"{type}({ownerRef.RawEncoded}/{objectId.Raw})";
    }
}
