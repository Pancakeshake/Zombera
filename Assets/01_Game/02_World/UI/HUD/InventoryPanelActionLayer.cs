using System;
using Zombera.Characters;
using Zombera.Inventory;

namespace Zombera.UI
{
    internal static class InventoryPanelActionLayer
    {
        public static bool TryEquip(EquipmentSystem equipmentSystem, EquipmentSlot slot, ItemDefinition item)
        {
            return equipmentSystem != null && item != null && equipmentSystem.Equip(slot, item);
        }

        public static bool TryUnequip(EquipmentSystem equipmentSystem, ItemDefinition item)
        {
            if (equipmentSystem == null || item == null) return false;

            var equippedItems = equipmentSystem.EquippedItems;
            for (var i = 0; i < equippedItems.Count; i++)
            {
                if (equippedItems[i].item != item) continue;
                return equipmentSystem.Unequip(equippedItems[i].slot);
            }

            return false;
        }

        public static bool TryDrop(
            UnitInventory sourceInventory,
            ItemDefinition item,
            int quantity,
            Func<ItemDefinition, int, bool> spawnDroppedPickup,
            Func<ItemDefinition, string> resolveItemName,
            Action<string> logError)
        {
            if (!ValidateTransferLikeInputs(sourceInventory, item, quantity)) return false;

            if (sourceInventory.GetQuantity(item) < quantity)
            {
                logError?.Invoke($"Cannot drop {quantity}x {ResolveName(item, resolveItemName)}; insufficient quantity.");
                return false;
            }

            if (!sourceInventory.RemoveItem(item, quantity))
            {
                logError?.Invoke($"Failed removing {quantity}x {ResolveName(item, resolveItemName)} before drop.");
                return false;
            }

            if (spawnDroppedPickup != null && spawnDroppedPickup(item, quantity)) return true;

            if (!sourceInventory.AddItem(item, quantity))
                logError?.Invoke($"Drop rollback failed for {quantity}x {ResolveName(item, resolveItemName)}.");

            return false;
        }

        public static bool TryTransfer(
            UnitInventory sourceInventory,
            UnitInventory targetInventory,
            ItemDefinition item,
            int quantity,
            Func<ItemDefinition, string> resolveItemName,
            string targetUnitName,
            Action<string> logWarning,
            Action<string> logError)
        {
            if (!ValidateTransferLikeInputs(sourceInventory, item, quantity) || targetInventory == null ||
                sourceInventory == targetInventory) return false;

            if (sourceInventory.GetQuantity(item) < quantity)
            {
                logWarning?.Invoke($"Cannot transfer {quantity}x {ResolveName(item, resolveItemName)}; source stack too small.");
                return false;
            }

            if (!sourceInventory.RemoveItem(item, quantity))
            {
                logError?.Invoke($"Failed removing {quantity}x {ResolveName(item, resolveItemName)} before transfer.");
                return false;
            }

            if (targetInventory.AddItem(item, quantity)) return true;

            var targetLabel = string.IsNullOrWhiteSpace(targetUnitName) ? "target" : targetUnitName;
            logWarning?.Invoke($"Transfer blocked: {targetLabel} cannot carry {quantity}x {ResolveName(item, resolveItemName)}.");

            if (!sourceInventory.AddItem(item, quantity))
                logError?.Invoke($"Transfer rollback failed for {quantity}x {ResolveName(item, resolveItemName)}.");

            return false;
        }

        private static bool ValidateTransferLikeInputs(UnitInventory sourceInventory, ItemDefinition item, int quantity)
        {
            return sourceInventory != null && item != null && quantity > 0;
        }

        private static string ResolveName(ItemDefinition item, Func<ItemDefinition, string> resolveItemName)
        {
            if (item == null) return "Unknown";
            return resolveItemName != null ? resolveItemName(item) : item.name;
        }
    }
}
