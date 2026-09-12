using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Inventory
{
    /// <summary>
    ///     Central registry for all ItemDefinition assets.
    ///     Provides fast O(1) lookup by itemId at runtime for the save system.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Inventory/Item Save Registry", fileName = "ItemSaveRegistry")]
    public sealed class ItemSaveRegistry : ScriptableObject
    {
        [Tooltip("All items known to the project. Populate via ItemsCatalogBuildTool.")]
        public List<ItemDefinition> items = new();

        private readonly Dictionary<string, ItemDefinition> _lookup = new(StringComparer.OrdinalIgnoreCase);
        private bool _isInitialized;

        public void Initialize()
        {
            _lookup.Clear();
            foreach (var item in items)
            {
                if (item == null) continue;
                if (string.IsNullOrWhiteSpace(item.itemId))
                {
                    Debug.LogWarning($"[ItemSaveRegistry] Item '{item.name}' has no itemId and will be skipped.");
                    continue;
                }

                if (!_lookup.TryAdd(item.itemId, item))
                {
                    Debug.LogWarning($"[ItemSaveRegistry] Duplicate itemId '{item.itemId}' found on '{item.name}'. Existing: '{_lookup[item.itemId].name}'.");
                }
            }
            _isInitialized = true;
        }

        public ItemDefinition GetItem(string itemId)
        {
            if (!_isInitialized) Initialize();
            
            if (string.IsNullOrWhiteSpace(itemId)) return null;
            return _lookup.TryGetValue(itemId, out var item) ? item : null;
        }

        public void Clear()
        {
            items.Clear();
            _lookup.Clear();
            _isInitialized = false;
        }
    }
}