#region

using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Zombera.Core;
using Zombera.Debugging;
using Zombera.Inventory;

#endregion

namespace Zombera.Characters
{
    public sealed class DevModeInventoryHelper
    {
        private readonly MonoBehaviour _host;

        public DevModeInventoryHelper(MonoBehaviour host)
        {
            _host = host;
        }

        public void Apply(
            Unit playerUnit, 
            ItemDefinition[] configuredItems,
            int quantityPerItem,
            int quantityPerAmmoItem,
            float minimumWeightLimit,
            bool logInventory)
        {
            if (playerUnit == null || !ShouldApply()) return;

            var inventory = EnsureUnitInventory(playerUnit);
            if (inventory == null)
            {
                Debug.LogWarning("[DevModeInventoryHelper] Failed to resolve UnitInventory for spawned player.", playerUnit);
                return;
            }

            var loadoutItems = ResolveItems(configuredItems);
            if (loadoutItems == null || loadoutItems.Length == 0)
            {
                if (logInventory)
                    Debug.LogWarning("[DevModeInventoryHelper] Dev-mode inventory spawn enabled, but no ItemDefinition assets were found.", _host);
                return;
            }

            var safeQuantityPerItem = Mathf.Max(1, quantityPerItem);
            var safeQuantityPerAmmoItem = Mathf.Max(1, quantityPerAmmoItem);
            var requiredWeightLimit = Mathf.Max(1f, inventory.WeightLimit);

            foreach (var item in loadoutItems.Where(item => item != null))
            {
                var targetQuantity = ResolveTargetQuantity(item, safeQuantityPerItem, safeQuantityPerAmmoItem);
                var currentQuantity = inventory.GetQuantity(item);
                var neededQuantity = Mathf.Max(0, targetQuantity - currentQuantity);
                if (neededQuantity <= 0) continue;

                requiredWeightLimit += Mathf.Max(0f, item.weight) * neededQuantity;
            }

            requiredWeightLimit = Mathf.Max(requiredWeightLimit, minimumWeightLimit);
            if (inventory.WeightLimit < requiredWeightLimit) 
                inventory.SetWeightLimit(requiredWeightLimit);

            var addedOrSatisfied = 0;
            var failedAdds = 0;

            foreach (var item in loadoutItems.Where(item => item != null))
            {
                var targetQuantity = ResolveTargetQuantity(item, safeQuantityPerItem, safeQuantityPerAmmoItem);
                var currentQuantity = inventory.GetQuantity(item);
                var neededQuantity = Mathf.Max(0, targetQuantity - currentQuantity);

                if (neededQuantity <= 0)
                {
                    addedOrSatisfied++;
                    continue;
                }

                var added = inventory.AddItem(item, neededQuantity);
                if (added)
                    addedOrSatisfied++;
                else
                    failedAdds++;
            }

            if (logInventory)
                Debug.Log(
                    $"[DevModeInventoryHelper] Dev-mode spawn inventory applied: Items={loadoutItems.Length}, Satisfied={addedOrSatisfied}, Failed={failedAdds}, " +
                    $"WeightLimit={inventory.WeightLimit:F1}, CurrentWeight={inventory.CurrentWeight:F1}.",
                    playerUnit);
        }

        public static bool ShouldApply()
        {
            if (!IsDevModeEnabled()) return false;

            var debugSettings = DebugManager.Instance != null ? DebugManager.Instance.Settings : null;
            return debugSettings == null || debugSettings.enableDevSpawnFullInventory;
        }

        public static bool IsDevModeEnabled()
        {
            return DebugManager.Instance != null ? DebugManager.Instance.DebugEnabled : Debug.isDebugBuild;
        }

        private static UnitInventory EnsureUnitInventory(Unit unit)
        {
            if (unit == null) return null;

            var inventory = unit.Inventory;
            if (inventory != null) return inventory;

            inventory = unit.GetComponent<UnitInventory>();
            return inventory != null ? inventory : unit.gameObject.AddComponent<UnitInventory>();
        }

        private static ItemDefinition[] ResolveItems(ItemDefinition[] configuredItems)
        {
            var resolved = new List<ItemDefinition>();
            AddUniqueItems(resolved, configuredItems);

#if UNITY_EDITOR
            if (resolved.Count == 0)
            {
                var itemGuids = AssetDatabase.FindAssets("t:ItemDefinition");
                foreach (var itemGuid in itemGuids)
                {
                    var itemPath = AssetDatabase.GUIDToAssetPath(itemGuid);
                    var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
                    if (item != null && !resolved.Contains(item)) resolved.Add(item);
                }
            }
#endif

            if (resolved.Count == 0)
            {
                var loadedItems = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                AddUniqueItems(resolved, loadedItems);
            }

            return resolved.ToArray();
        }

        private static int ResolveTargetQuantity(ItemDefinition item, int quantityPerItem, int quantityPerAmmoItem)
        {
            if (item != null && item.itemType == ItemType.Ammo) return Mathf.Max(1, quantityPerAmmoItem);
            return Mathf.Max(1, quantityPerItem);
        }

        private static void AddUniqueItems(List<ItemDefinition> destination, ItemDefinition[] source)
        {
            if (destination == null || source == null) return;
            foreach (var item in source)
            {
                if (item != null && !destination.Contains(item)) 
                    destination.Add(item);
            }
        }
    }
}
