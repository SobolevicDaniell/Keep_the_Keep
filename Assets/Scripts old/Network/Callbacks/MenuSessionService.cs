using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace Game.Network
{
    public enum SessionCheck { Exists, NotFound, Unknown }

    public sealed class MenuSessionService
    {
       
        public async Task<SessionCheck> Check(string sessionName, float timeoutSeconds = 5f)
        {
            var target = (sessionName ?? string.Empty).Trim();

            var runners = UnityEngine.Object.FindObjectsByType<NetworkRunner>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var r in runners)
            {
                try
                {
                    if (r != null && r.IsRunning)
                        return SessionCheck.Unknown;
                }
                catch
                {
                    return SessionCheck.Unknown;
                }
            }

            var go = new GameObject("MenuQueryRunner");
            var runner = go.AddComponent<NetworkRunner>();
            var cb = go.AddComponent<MenuLobbyCallbacks>();
            cb.Tcs = new TaskCompletionSource<List<SessionInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                runner.ProvideInput = false;
                runner.AddCallbacks(cb);

                try
                {
                    await runner.JoinSessionLobby(SessionLobby.Shared);
                }
                catch
                {
                    return SessionCheck.Unknown;
                }

                var completed = await Task.WhenAny(cb.Tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
                var list = completed == cb.Tcs.Task ? (cb.Tcs.Task.Result ?? new List<SessionInfo>()) : new List<SessionInfo>();

                foreach (var s in list)
                {
                    var name = (s.Name ?? string.Empty).Trim();
                    if (string.Equals(name, target, StringComparison.OrdinalIgnoreCase))
                        return SessionCheck.Exists;
                }

                return SessionCheck.NotFound;
            }
            finally
            {
                try { await runner.Shutdown(false); } catch { }
                runner.RemoveCallbacks(cb);
                UnityEngine.Object.Destroy(go);
            }
        }

        public async Task<bool> Exists(string sessionName)
        {
            return await Check(sessionName) == SessionCheck.Exists;
        }

        public async Task<List<SessionInfo>> FetchSessions(float timeoutSeconds = 5f)
        {
            var runners = UnityEngine.Object.FindObjectsByType<NetworkRunner>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var r in runners)
                if (r != null && r.IsRunning)
                    return new List<SessionInfo>();

            var go = new GameObject("MenuQueryRunner");
            var runner = go.AddComponent<NetworkRunner>();
            go.AddComponent<RunnerAutoShutdown>();
            var cb = go.AddComponent<MenuLobbyCallbacks>();
            cb.Tcs = new TaskCompletionSource<List<SessionInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                runner.ProvideInput = false;
                runner.AddCallbacks(cb);

                await runner.JoinSessionLobby(SessionLobby.Shared);

                var completed = await Task.WhenAny(cb.Tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
                var list = completed == cb.Tcs.Task ? (cb.Tcs.Task.Result ?? new List<SessionInfo>()) : new List<SessionInfo>();
                return list;
            }
            finally
            {
                try { await runner.Shutdown(false); } catch { }
                runner.RemoveCallbacks(cb);
                UnityEngine.Object.Destroy(go);
            }

        }
    }
}