using UnityEngine;
using Fusion.Addons.Physics;

namespace Game.Network
{
    public sealed class RunnerPhysicsBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var sim3D = GetComponent<RunnerSimulatePhysics3D>();
            if (sim3D == null)
                sim3D = gameObject.AddComponent<RunnerSimulatePhysics3D>();
            sim3D.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateAlways;
        }
    }
}
