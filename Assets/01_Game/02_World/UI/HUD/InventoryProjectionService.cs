using System;
using System.Collections.Generic;
using Zombera.Inventory;

namespace Zombera.UI
{
    internal static class InventoryProjectionService
    {
        public static void CollectFilteredStacks(
            IReadOnlyList<ItemStack> source,
            string query,
            List<ItemStack> destination,
            Func<ItemDefinition, bool> filter,
            Func<ItemDefinition, string, bool> search)
        {
            if (destination == null) return;
            destination.Clear();

            if (source == null || filter == null || search == null) return;

            for (var i = 0; i < source.Count; i++)
            {
                var stack = source[i];
                if (stack.item == null || stack.quantity <= 0) continue;
                if (!filter(stack.item) || !search(stack.item, query)) continue;

                destination.Add(stack);
            }
        }

        public static void SortStacksForDisplay(
            List<ItemStack> stacks,
            HashSet<ItemDefinition> equippedLookup,
            Func<ItemDefinition, string> resolveName)
        {
            if (stacks == null || stacks.Count <= 1) return;
            stacks.Sort((a, b) => CompareStacksForDisplay(a, b, equippedLookup, resolveName));
        }

        public static int CompareStacksForDisplay(
            ItemStack a,
            ItemStack b,
            HashSet<ItemDefinition> equippedLookup,
            Func<ItemDefinition, string> resolveName)
        {
            var aEquipped = IsEquipped(a.item, equippedLookup);
            var bEquipped = IsEquipped(b.item, equippedLookup);
            if (aEquipped != bEquipped)
                return aEquipped ? -1 : 1;

            var typeCompare = a.item.itemType.CompareTo(b.item.itemType);
            if (typeCompare != 0) return typeCompare;

            var aName = resolveName != null ? resolveName(a.item) : string.Empty;
            var bName = resolveName != null ? resolveName(b.item) : string.Empty;
            return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsEquipped(ItemDefinition item, HashSet<ItemDefinition> equippedLookup)
        {
            return item != null && equippedLookup != null && equippedLookup.Contains(item);
        }

        public static bool IsEquipped(ItemDefinition item, IReadOnlyList<EquipmentSlotBinding> equippedItems)
        {
            if (item == null || equippedItems == null) return false;

            for (var i = 0; i < equippedItems.Count; i++)
                if (equippedItems[i].item == item)
                    return true;

            return false;
        }

        public static void RefreshEquippedItemLookup(
            IReadOnlyList<EquipmentSlotBinding> equippedItems,
            HashSet<ItemDefinition> destination)
        {
            if (destination == null) return;

            destination.Clear();
            if (equippedItems == null) return;

            for (var i = 0; i < equippedItems.Count; i++)
            {
                var equipped = equippedItems[i].item;
                if (equipped != null)
                    destination.Add(equipped);
            }
        }
    }
}
