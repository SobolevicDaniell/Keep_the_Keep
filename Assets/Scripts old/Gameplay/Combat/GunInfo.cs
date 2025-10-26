using UnityEngine;

namespace Game
{
    public sealed class GunInfo : MonoBehaviour
    {
        [SerializeField] private Transform _muzzlePoint;
        public Transform MuzzlePoint => _muzzlePoint;
    }
}
