using System.Linq;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace Game.Network
{
    public static class RunnerGuard
    {
        public static async Task KillAllExcept(NetworkRunner keep = null)
        {
            var runners = Object.FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                 .Where(r => r != null && r != keep)
                                 .ToArray();

            foreach (var r in runners)
            {
                try
                {
                    Debug.Log($"[RunnerGuard] Shutdown runner: {r.name} (id={r.GetInstanceID()})");
                    await r.Shutdown(false);
                }
                catch { }
            }
        }

        public static void DumpRunners(string tag)
        {
            var runners = Object.FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"[RunnerGuard] {tag}: found {runners.Length} runners");
            foreach (var r in runners)
            {
                if (r == null) continue;
                bool running;
                try { running = r.IsRunning; } catch { running = true; }
                Debug.Log($"  - {r.name} (id={r.GetInstanceID()}), running={running}, active={r.isActiveAndEnabled}");
            }
        }
    }
}
