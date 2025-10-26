using System;

namespace Game
{
    [Serializable]
    public class ItemState
    {
        public int ammo;
        public int durability;

        public ItemState() { }
        public ItemState(ItemState other)
        {
            if (other == null) return;
            ammo = other.ammo;
            durability = other.durability;
        }

        public ItemState(int ammo)
        {
            this.ammo = ammo;
        }

        public ItemState(int ammo, int durability)
        {
            this.ammo = ammo;
            this.durability = durability;
        }

        public ItemState Clone() => new ItemState(this);
    }


}