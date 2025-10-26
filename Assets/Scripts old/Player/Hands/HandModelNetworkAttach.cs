// HandModelNetworkAttach.cs
using System.Collections;
using System.Linq;
using Fusion;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(NetworkObject))]
    public class HandModelNetworkAttach : NetworkBehaviour
    {
        public override void Spawned()
        {
            if (!Object.HasStateAuthority)
            {
                StartCoroutine(AttachToHand());
            }
        }

        private IEnumerator AttachToHand()
        {
            InteractionController ic = null;
            while (ic == null)
            {
                ic = FindObjectsOfType<InteractionController>()
                        .FirstOrDefault(x => x.Object.InputAuthority == Object.InputAuthority);
                yield return null;
            }

            transform.SetParent(ic.handPoint, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }
}
