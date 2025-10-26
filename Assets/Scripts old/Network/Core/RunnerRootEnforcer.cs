using UnityEngine;

namespace Game.Network
{
    public sealed class RunnerRootEnforcer : MonoBehaviour
    {
        private void Awake()
        {
            if (transform.parent != null)
                transform.SetParent(null, true);
        }
    }
}
