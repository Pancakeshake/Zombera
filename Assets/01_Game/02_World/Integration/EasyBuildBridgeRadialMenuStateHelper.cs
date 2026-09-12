using System;
using System.Collections;
using UnityEngine;
using Zombera.EasyBuildAdapter;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeRadialMenuStateHelper
    {
        internal static bool IsRadialMenuOpen(object radialMenu)
        {
            return EasyBuildFacade.IsMenuOpen(radialMenu as Component);
        }

        internal static int GetBuildCategoryCount(object radialMenu)
        {
            var categories = EasyBuildBridgeReflectionHelper.GetMemberValue(radialMenu, "Categories") as IEnumerable;
            if (categories == null) return 0;

            var count = 0;
            foreach (var category in categories)
            {
                if (category != null)
                    count++;
            }

            return count;
        }

        internal static int GetActiveBuildCategoryIndex(
            object radialMenu,
            Func<object, bool, object> resolveActiveOrFirstCategory)
        {
            if (resolveActiveOrFirstCategory == null) return -1;

            var activeCategory = resolveActiveOrFirstCategory(radialMenu, true);
            if (activeCategory == null) return -1;

            var categories = EasyBuildBridgeReflectionHelper.GetMemberValue(radialMenu, "Categories") as IEnumerable;
            if (categories == null) return -1;

            var index = 0;
            foreach (var category in categories)
            {
                if (ReferenceEquals(category, activeCategory))
                    return index;

                index++;
            }

            return -1;
        }

        internal static bool TrySetActiveBuildCategory(
            object radialMenu,
            int categoryIndex,
            Func<object, int, object> resolveRadialCategoryByIndex)
        {
            if (resolveRadialCategoryByIndex == null) return false;

            var category = resolveRadialCategoryByIndex(radialMenu, categoryIndex);
            if (category == null)
                return false;

            EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "SelectedCategory", category);
            EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "CurrentCategory", category);
            EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "ActiveCategory", category);
            EasyBuildBridgeReflectionHelper.InvokeMethod(radialMenu, "ForceRebuildAllSlots");
            return true;
        }

        internal static bool TryGetBuildItemCountFromActiveCategory(
            object radialMenu,
            Func<object, bool, object> resolveActiveOrFirstCategory,
            out int count)
        {
            count = 0;
            if (resolveActiveOrFirstCategory == null) return false;

            var activeCategory = resolveActiveOrFirstCategory(radialMenu, true);
            if (activeCategory == null) return false;

            var slots = EasyBuildBridgeReflectionHelper.GetMemberValue(activeCategory, "Slots") as IEnumerable;
            if (slots == null) return false;

            // Preserve radial slot index alignment, including placeholder slots.
            foreach (var _ in slots)
                count++;

            return true;
        }
    }
}
