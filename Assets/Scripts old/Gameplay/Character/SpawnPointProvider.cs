using System.Collections.Generic;
using UnityEngine;

namespace Game.Network
{
    public sealed class SpawnPointProvider : MonoBehaviour, ISpawnPointProvider
    {
        [SerializeField] private List<GameObject> _points = new List<GameObject>();
        private int _nextIndex;

        public bool TryGetNext(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            if (_points == null || _points.Count == 0) return false;
            var index = _nextIndex % _points.Count;
            _nextIndex = (_nextIndex + 1) % int.MaxValue;
            var go = _points[index];
            if (go == null) return false;
            var t = go.transform;
            position = t.position;
            rotation = t.rotation;
            return true;
        }

        public void ResetOrder()
        {
            _nextIndex = 0;
        }
    }
}
