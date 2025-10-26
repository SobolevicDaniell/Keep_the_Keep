using System;
using System.Linq;
using System.Reflection;
using Fusion;
using UnityEngine;
using Zenject;

namespace Game
{
    public sealed class InventorySnapshotBuilder
    {
        [Inject(Optional = true)] private NetworkRunner _runner;
        [Inject(Optional = true)] private NetworkRunnerProvider _runnerProvider;

        public ContainerSnapshot Build(ContainerId id)
        {
            if (!TryResolveContainer(id, out var container))
                return default;

            var slots = container.Slots;
            var copy  = (slots == null) ? Array.Empty<InventorySlotState>() : CopySlots(slots);
            return CreateSnapshot(id, container.Version, copy);
        }

        private static InventorySlotState[] CopySlots(InventorySlotState[] src)
        {
            var dst = new InventorySlotState[src.Length];
            for (int i = 0; i < src.Length; i++)
                dst[i] = src[i]?.Clone();
            return dst;
        }

        private bool TryResolveContainer(ContainerId id, out PlayerInventoryServer container)
        {
            container = null;

            var runner = ResolveRunner();
            if (runner != null && runner.TryGetPlayerObject(id.ownerRef, out var ownerNO) && ownerNO != null)
            {
                var underPlayerObject = ownerNO.GetComponentsInChildren<PlayerInventoryServer>(true);
                for (int i = 0; i < underPlayerObject.Length; i++)
                {
                    var c = underPlayerObject[i];
                    if (c != null && c.Id.Equals(id)) { container = c; return true; }
                }
                for (int i = 0; i < underPlayerObject.Length; i++)
                {
                    var c = underPlayerObject[i];
                    if (c != null && c.Id.type == id.type && c.Id.ownerRef == id.ownerRef) { container = c; return true; }
                }
            }

            var all = UnityEngine.Object.FindObjectsOfType<PlayerInventoryServer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c != null && c.Id.ownerRef == id.ownerRef && c.Id.type == id.type) { container = c; return true; }
            }

            return false;
        }

        private NetworkRunner ResolveRunner()
        {
            if (_runner != null) return _runner;

            if (_runnerProvider != null)
            {
                var t = _runnerProvider.GetType();

                foreach (var propName in new[] { "Runner", "Current", "Instance", "Value" })
                {
                    var p = t.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
                    if (p != null && typeof(NetworkRunner).IsAssignableFrom(p.PropertyType))
                    {
                        var val = p.GetValue(_runnerProvider) as NetworkRunner;
                        if (val != null) return val;
                    }
                }

                foreach (var methodName in new[] { "Get", "GetRunner", "GetCurrent", "Resolve", "GetOrCreate" })
                {
                    var m = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                    if (m != null && typeof(NetworkRunner).IsAssignableFrom(m.ReturnType))
                    {
                        var val = m.Invoke(_runnerProvider, null) as NetworkRunner;
                        if (val != null) return val;
                    }
                }
            }

            return UnityEngine.Object.FindObjectOfType<NetworkRunner>();
        }

        private static ContainerSnapshot CreateSnapshot(ContainerId id, int version, InventorySlotState[] slots)
        {
            var ctors = typeof(ContainerSnapshot).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var ctor  = ctors.FirstOrDefault(c =>
            {
                var p = c.GetParameters();
                if (p.Length != 3) return false;
                return p[0].ParameterType == typeof(ContainerId)
                    && p[1].ParameterType == typeof(int)
                    && p[2].ParameterType == typeof(InventorySlotState[]);
            });

            if (ctor != null)
                return (ContainerSnapshot)ctor.Invoke(new object[] { id, version, slots });

            var snap = Activator.CreateInstance(typeof(ContainerSnapshot));
            SetMemberIfExists(snap, "id",      id);
            SetMemberIfExists(snap, "Id",      id);
            SetMemberIfExists(snap, "version", version);
            SetMemberIfExists(snap, "Version", version);
            SetMemberIfExists(snap, "slots",   slots);
            SetMemberIfExists(snap, "Slots",   slots);
            return (ContainerSnapshot)snap;
        }

        private static void SetMemberIfExists(object obj, string name, object value)
        {
            var t = obj.GetType();

            var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (prop != null && prop.CanWrite && prop.PropertyType.IsAssignableFrom(value?.GetType() ?? typeof(object)))
            {
                prop.SetValue(obj, value);
                return;
            }

            var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.FieldType.IsAssignableFrom(value?.GetType() ?? typeof(object)))
            {
                field.SetValue(obj, value);
            }
        }
    }
}
