using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Systems
{
    /// <summary>
    ///     Static registry for all GameObjects that have a RuntimeSaveId component.
    ///     Used by providers to locate and reconcile objects during save/load.
    /// </summary>
    public static class RuntimeSaveRegistry
    {
        private static readonly Dictionary<string, RuntimeSaveId> _registry = new();

        public static void Register(RuntimeSaveId runtimeSaveId)
        {
            if (runtimeSaveId == null || string.IsNullOrWhiteSpace(runtimeSaveId.Id)) return;

            if (_registry.TryGetValue(runtimeSaveId.Id, out var existing) && existing != runtimeSaveId)
            {
                Debug.LogWarning($"[RuntimeSaveRegistry] Duplicate ID detected: {runtimeSaveId.Id}. Overwriting registration.");
            }

            _registry[runtimeSaveId.Id] = runtimeSaveId;
        }

        public static void Unregister(RuntimeSaveId runtimeSaveId)
        {
            if (runtimeSaveId == null || string.IsNullOrWhiteSpace(runtimeSaveId.Id)) return;

            if (_registry.TryGetValue(runtimeSaveId.Id, out var existing) && existing == runtimeSaveId)
            {
                _registry.Remove(runtimeSaveId.Id);
            }
        }

        public static RuntimeSaveId Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return _registry.TryGetValue(id, out var value) ? value : null;
        }

        public static IEnumerable<RuntimeSaveId> GetAll()
        {
            return _registry.Values;
        }

        public static void Clear()
        {
            _registry.Clear();
        }
    }
}