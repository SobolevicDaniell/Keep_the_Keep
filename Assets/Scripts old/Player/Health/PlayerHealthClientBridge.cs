using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(PlayerHealthServer))]
    public sealed class PlayerHealthClientBridge : NetworkBehaviour
    {
        [Inject(Optional = true)] private HealthClientModel _model;

        private PlayerHealthServer _srv;
        private int _lastCur = int.MinValue;
        private int _lastMax = int.MinValue;

        private void Awake()
        {
            _srv = GetComponent<PlayerHealthServer>();
        }

        public override void Spawned()
        {
            if (!HasInputAuthority || _model == null || _srv == null) return;
            _lastCur = _srv.Current;
            _lastMax = _srv.Max;
            _model.Apply(_lastCur, _lastMax);
        }

        public override void Render()
        {
            if (!HasInputAuthority || _model == null || _srv == null) return;

            int c = _srv.Current;
            int m = _srv.Max;
            if (c != _lastCur || m != _lastMax)
            {
                _lastCur = c;
                _lastMax = m;
                _model.Apply(c, m);
            }
        }
    }
}
