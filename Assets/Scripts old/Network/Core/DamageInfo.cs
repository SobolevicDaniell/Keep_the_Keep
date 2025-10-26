using Fusion;
using UnityEngine;

namespace Game
{
    public enum DamageKind { Generic, Bullet, Explosion, Fire, Spike, Fall }

    public struct DamageInfo
    {
        public int        amount;
        public DamageKind kind;
        public Vector3    point;
        public Vector3    direction;
        public PlayerRef  source;

        public DamageInfo(int amount, DamageKind kind, Vector3 point, Vector3 direction, PlayerRef source)
        {
            this.amount    = amount;
            this.kind      = kind;
            this.point     = point;
            this.direction = direction;
            this.source    = source;
        }
    }
}
