using System;
using UnityEngine;

namespace Zombera.Systems
{
    /// <summary>
    ///     Provides a stable unique identity for objects spawned at runtime that need to be persisted.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeSaveId : MonoBehaviour
    {
        [SerializeField] private string id;

        public string Id => id;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString("N");
            }
            
            RuntimeSaveRegistry.Register(this);
        }

        private void OnDestroy()
        {
            RuntimeSaveRegistry.Unregister(this);
        }

        public void SetId(string newId)
        {
            RuntimeSaveRegistry.Unregister(this);
            id = newId;
            RuntimeSaveRegistry.Register(this);
        }
    }
}