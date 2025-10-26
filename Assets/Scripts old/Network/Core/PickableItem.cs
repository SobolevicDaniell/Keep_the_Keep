using Fusion;
using UnityEngine;

namespace Game
{
    public sealed class PickableItem : NetworkBehaviour
    {
        [Networked] public NetworkString<_32> ItemId { get; set; }
        [Networked] public int Count { get; set; }
        [Networked] public int Ammo { get; set; }
        [Networked] public bool Consumed { get; set; }

        [SerializeField] private string _initItemId;
        [SerializeField] private int _initCount;
        [SerializeField] private int _initAmmo;
        private bool _applied;

        public void Initialize(string itemId, int count)
        {
            _initItemId = itemId;
            _initCount = count;
        }

        public void Initialize(string itemId, int count, int ammo)
        {
            _initItemId = itemId;
            _initCount = count;
            _initAmmo = ammo;
        }

        public void ServerInit(string itemId, int count, int ammo)
        {
            _initItemId = itemId;
            _initCount = count;
            _initAmmo = ammo;
            if (Runner != null && HasStateAuthority)
            {
                ItemId = itemId;
                Count = count;
                Ammo = ammo;
                Consumed = false;
                _applied = true;
            }
        }

        public override void Spawned()
        {
            if (HasStateAuthority && !_applied)
            {
                ItemId = _initItemId;
                Count = _initCount;
                Ammo = _initAmmo;
                Consumed = false;
                _applied = true;
            }
        }

        public string GetItemId()
        {
            return Runner == null ? _initItemId : ItemId.ToString();
        }

        public int GetCount()
        {
            return Runner == null ? _initCount : Count;
        }

        public int GetAmmo()
        {
            return Runner == null ? _initAmmo : Ammo;
        }

        public void SetCount(int value)
        {
            _initCount = value;
            if (Runner != null && HasStateAuthority)
                Count = value;
        }

        public bool TryConsumeServer()
        {
            if (!HasStateAuthority) return false;
            if (Consumed) return false;
            Consumed = true;
            return true;
        }
    }
}
