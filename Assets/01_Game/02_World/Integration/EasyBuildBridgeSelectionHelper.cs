using System;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeSelectionHelper
    {
        internal static object ResolveRadialCategoryByIndex(object radialMenu, int categoryIndex)
        {
            if (radialMenu == null || categoryIndex < 0) return null;

            var categories = EasyBuildBridgeReflectionHelper.GetMemberValue(radialMenu, "Categories") as System.Collections.IEnumerable;
            if (categories == null) return null;

            var index = 0;
            foreach (var category in categories)
            {
                if (index == categoryIndex) return category;
                index++;
            }

            return null;
        }

        internal static object ResolveActiveOrFirstCategory(object radialMenu, bool assignIfMissing)
        {
            if (radialMenu == null) return null;

            var activeCategory = EasyBuildBridgeReflectionHelper.GetMemberValue(radialMenu, "CurrentCategory")
                                 ?? EasyBuildBridgeReflectionHelper.GetMemberValue(radialMenu, "SelectedCategory")
                                 ?? EasyBuildBridgeReflectionHelper.GetMemberValue(radialMenu, "ActiveCategory");
            if (activeCategory != null) return activeCategory;

            var categories = EasyBuildBridgeReflectionHelper.GetMemberValue(radialMenu, "Categories") as System.Collections.IEnumerable;
            if (categories == null) return null;

            foreach (var category in categories)
            {
                if (category == null) continue;

                if (assignIfMissing)
                {
                    EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "SelectedCategory", category);
                    EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "CurrentCategory", category);
                    EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "ActiveCategory", category);
                }

                return category;
            }

            return null;
        }

        internal static bool TryGetSlotObjectAtIndex(object category, int slotIndex, out object slot)
        {
            slot = null;

            var slots = EasyBuildBridgeReflectionHelper.GetMemberValue(category, "Slots") as System.Collections.IEnumerable;
            if (slots == null) return false;

            var currentIndex = 0;
            foreach (var s in slots)
            {
                if (currentIndex != slotIndex)
                {
                    currentIndex++;
                    continue;
                }

                slot = s;
                return true;
            }

            return false;
        }

        internal static bool TryInvokeRadialSlotSelectionAction(object radialMenu, int hudIndex, Type selectionActionType)
        {
            var activeCategory = ResolveActiveOrFirstCategory(radialMenu, true);

            if (activeCategory != null && TryInvokeCategorySlotAction(radialMenu, activeCategory, hudIndex, selectionActionType))
                return true;

            var categories = EasyBuildBridgeReflectionHelper.GetMemberValue(radialMenu, "Categories") as System.Collections.IEnumerable;
            if (categories == null) return false;

            foreach (var category in categories)
            {
                if (category == null) continue;
                if (TryInvokeCategorySlotAction(radialMenu, category, hudIndex, selectionActionType))
                    return true;
            }

            return false;
        }

        internal static bool TryInvokeCategorySlotAction(object radialMenu, object category, int slotIndex, Type selectionActionType)
        {
            if (category == null || slotIndex < 0) return false;

            if (!TryGetSlotObjectAtIndex(category, slotIndex, out var slot) || slot == null)
                return false;

            var action = EasyBuildBridgeReflectionHelper.GetMemberValue(slot, "Action");
            if (action == null || selectionActionType == null || !selectionActionType.IsInstanceOfType(action))
                return false;

            // Keep radial menu internals in sync with HUD-driven selection.
            EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "SelectedCategory", category);
            EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "CurrentCategory", category);
            EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "SelectedSlot", slot);
            EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "CurrentSlot", slot);
            EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "SelectedAction", action);
            EasyBuildBridgeReflectionHelper.SetMemberValue(radialMenu, "CurrentAction", action);

            EasyBuildBridgeReflectionHelper.InvokeMethod(action, "OnAction");
            EasyBuildBridgeReflectionHelper.InvokeMethod(action, "Execute");
            EasyBuildBridgeReflectionHelper.InvokeMethod(action, "Select");
            EasyBuildBridgeReflectionHelper.InvokeMethod(action, "OnClick");
            return true;
        }

        internal static bool TryGetPartReferenceFromCategorySlot(
            object category,
            int slotIndex,
            Type selectionActionType,
            out string partReference)
        {
            partReference = null;
            if (category == null || slotIndex < 0) return false;
            if (!TryGetSlotObjectAtIndex(category, slotIndex, out var slot) || slot == null) return false;

            var action = EasyBuildBridgeReflectionHelper.GetMemberValue(slot, "Action");
            partReference = GetSelectionActionPartReference(action, selectionActionType);
            return !string.IsNullOrWhiteSpace(partReference);
        }

        internal static string GetSelectionActionPartReference(object action, Type selectionActionType)
        {
            if (action == null || selectionActionType == null) return null;
            if (!selectionActionType.IsInstanceOfType(action)) return null;

            return EasyBuildBridgeReflectionHelper.GetMemberValue(action, "PartReference") as string
                   ?? EasyBuildBridgeReflectionHelper.GetMemberValue(action, "PrefabId") as string
                   ?? EasyBuildBridgeReflectionHelper.GetMemberValue(action, "ID") as string;
        }

        internal static bool TryResolvePartReferenceFromRadialMenu(
            object radialMenu,
            int hudIndex,
            Type selectionActionType,
            bool emitDiagnostics,
            Action<string, string, int, int> logDiagnostic,
            out string partReference)
        {
            partReference = null;

            var activeCategory = ResolveActiveOrFirstCategory(radialMenu, true);

            if (TryResolvePartReferenceFromCategory(
                    activeCategory,
                    hudIndex,
                    "ResolvePartRef.activeCategory",
                    selectionActionType,
                    emitDiagnostics,
                    logDiagnostic,
                    out partReference))
                return true;

            var categories = EasyBuildBridgeReflectionHelper.GetMemberValue(radialMenu, "Categories") as System.Collections.IEnumerable;
            if (categories == null) return false;

            foreach (var category in categories)
            {
                if (category == null || ReferenceEquals(category, activeCategory)) continue;

                if (TryResolvePartReferenceFromCategory(
                        category,
                        hudIndex,
                        "ResolvePartRef.otherCategory",
                        selectionActionType,
                        emitDiagnostics,
                        logDiagnostic,
                        out partReference))
                    return true;
            }

            return false;
        }

        internal static bool TryResolvePartReferenceFromCategory(
            object category,
            int hudIndex,
            string diagnosticTag,
            Type selectionActionType,
            bool emitDiagnostics,
            Action<string, string, int, int> logDiagnostic,
            out string partReference)
        {
            partReference = null;
            if (category == null) return false;
            if (!TryGetPartReferenceFromCategorySlot(category, hudIndex, selectionActionType, out partReference))
                return false;

            if (emitDiagnostics)
                logDiagnostic?.Invoke(diagnosticTag, partReference, hudIndex, -1);

            return true;
        }
    }
}
