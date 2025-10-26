using Fusion;
using UnityEngine;

namespace Game.Network
{
    public sealed class LaunchRequestStore : MonoBehaviour
    {
        public bool HasRequest => _has;
        public GameMode Mode => _mode;
        public string SessionName => _sessionName;

        private bool _has;
        private GameMode _mode;
        private string _sessionName;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public void Set(GameMode mode, string sessionName)
        {
            _mode = mode;
            _sessionName = sessionName;
            _has = true;
        }

        public bool TryConsume(out GameMode mode, out string sessionName)
        {
            if (!_has)
            {
                mode = default;
                sessionName = null;
                return false;
            }

            mode = _mode;
            sessionName = _sessionName;
            _has = false;
            return true;
        }
    }
}
