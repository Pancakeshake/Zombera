#region

using System;
using System.Collections;
using System.Collections.Generic;

#endregion

namespace Zombera.BuildingSystem
{
    public sealed partial class EasyBuildCursorPlacementBinder
    {
        /// <summary>
        ///     Builds and queries a mapping from part references to HUD box indices,
        ///     and resolves selected part references and slot indices from the radial menu.
        /// </summary>
        private static class RadialHudIndexMapService
        {
            internal static void BuildCategoryMap(
                object radialMenu,
                Type selectionActionType,
                IDictionary<string, int> radialHudIndexByPartReference)
            {
                radialHudIndexByPartReference.Clear();

                var activeCategory = GetMemberValue(radialMenu, "CurrentCategory")
                                     ?? GetMemberValue(radialMenu, "SelectedCategory")
                                     ?? GetMemberValue(radialMenu, "ActiveCategory");

                if (activeCategory != null
                    && TryMapCategorySlots(activeCategory, selectionActionType, radialHudIndexByPartReference))
                    return;

                var categories = GetMemberValue(radialMenu, "Categories") as IEnumerable;
                if (categories == null) return;

                foreach (var category in categories)
                {
                    if (category == null) continue;
                    TryMapCategorySlots(category, selectionActionType, radialHudIndexByPartReference);
                }
            }

            internal static string ResolveSelectedPartReference(object radialMenu, Type selectionActionType)
            {
                return ResolvePartReferenceFromSelectedAction(radialMenu, selectionActionType)
                       ?? ResolvePartReferenceFromSelectedSlot(radialMenu, selectionActionType)
                       ?? ResolvePartReferenceFromCategories(radialMenu, selectionActionType);
            }

            internal static int ResolveSelectedSlotIndex(object radialMenu)
            {
                var directIndex = TryReadIntMember(
                    radialMenu,
                    "SelectedSlotIndex",
                    "CurrentSelectedSlotIndex",
                    "CurrentSlotIndex",
                    "SelectedIndex");

                if (directIndex >= 0) return directIndex;

                var selectedSlot = GetMemberValue(radialMenu, "SelectedSlot")
                                   ?? GetMemberValue(radialMenu, "CurrentSlot");
                if (selectedSlot == null)
                    return ResolveSelectedSlotIndexFromCategories(radialMenu);

                return ResolveSelectedSlotIndexByReference(radialMenu, selectedSlot);
            }

            private static bool TryMapCategorySlots(
                object category,
                Type selectionActionType,
                IDictionary<string, int> radialHudIndexByPartReference)
            {
                if (category == null || selectionActionType == null) return false;

                var slots = GetMemberValue(category, "Slots") as IEnumerable;
                if (slots == null) return false;

                var mappedAny = false;
                var slotIndex = 0;

                foreach (var slot in slots)
                {
                    if (slot != null)
                    {
                        var action = GetMemberValue(slot, "Action");
                        var partReference = GetSelectionActionPartReference(action, selectionActionType);
                        if (!string.IsNullOrWhiteSpace(partReference)
                            && !radialHudIndexByPartReference.ContainsKey(partReference))
                        {
                            radialHudIndexByPartReference[partReference] = slotIndex;
                            mappedAny = true;
                        }
                    }

                    slotIndex++;
                }

                return mappedAny;
            }

            private static string ResolvePartReferenceFromSelectedAction(object radialMenu, Type selectionActionType)
            {
                var selectedAction = GetMemberValue(radialMenu, "SelectedAction")
                                     ?? GetMemberValue(radialMenu, "CurrentAction")
                                     ?? GetMemberValue(radialMenu, "ActiveAction");

                return GetSelectionActionPartReference(selectedAction, selectionActionType);
            }

            private static string ResolvePartReferenceFromSelectedSlot(object radialMenu, Type selectionActionType)
            {
                var selectedSlot = GetMemberValue(radialMenu, "SelectedSlot")
                                   ?? GetMemberValue(radialMenu, "CurrentSlot");
                if (selectedSlot == null) return null;

                var slotAction = GetMemberValue(selectedSlot, "Action");
                return GetSelectionActionPartReference(slotAction, selectionActionType);
            }

            private static string ResolvePartReferenceFromCategories(object radialMenu, Type selectionActionType)
            {
                var categories = GetMemberValue(radialMenu, "Categories") as IEnumerable;
                if (categories == null) return null;

                foreach (var category in categories)
                {
                    var selectedPartReference = ResolvePartReferenceFromCategory(category, selectionActionType);
                    if (!string.IsNullOrWhiteSpace(selectedPartReference))
                        return selectedPartReference;
                }

                return null;
            }

            private static string ResolvePartReferenceFromCategory(object category, Type selectionActionType)
            {
                if (category == null) return null;

                var slots = GetMemberValue(category, "Slots") as IEnumerable;
                if (slots == null) return null;

                foreach (var slot in slots)
                {
                    if (!IsRadialSlotSelected(slot)) continue;

                    var action = GetMemberValue(slot, "Action");
                    var selectedPartReference = GetSelectionActionPartReference(action, selectionActionType);
                    if (!string.IsNullOrWhiteSpace(selectedPartReference))
                        return selectedPartReference;
                }

                return null;
            }

            private static bool IsRadialSlotSelected(object slot)
            {
                return slot != null && TryReadBoolMember(slot, "IsSelected", "Selected", "IsActive");
            }

            private static string GetSelectionActionPartReference(object action, Type selectionActionType)
            {
                if (action == null || selectionActionType == null) return null;
                if (!selectionActionType.IsInstanceOfType(action)) return null;

                return GetMemberValue(action, "PartReference") as string
                       ?? GetMemberValue(action, "PrefabId") as string
                       ?? GetMemberValue(action, "ID") as string;
            }

            private static int ResolveSelectedSlotIndexByReference(object radialMenu, object selectedSlot)
            {
                var categories = GetMemberValue(radialMenu, "Categories") as IEnumerable;
                if (categories == null) return -1;

                foreach (var category in categories)
                {
                    var slotIndex = ResolveSelectedSlotIndexInCategory(category, selectedSlot);
                    if (slotIndex >= 0) return slotIndex;
                }

                return -1;
            }

            private static int ResolveSelectedSlotIndexInCategory(object category, object selectedSlot)
            {
                if (category == null) return -1;

                var slots = GetMemberValue(category, "Slots") as IEnumerable;
                if (slots == null) return -1;

                var index = 0;
                foreach (var slot in slots)
                {
                    if (slot != null && ReferenceEquals(slot, selectedSlot))
                        return index;

                    index++;
                }

                return -1;
            }

            private static int ResolveSelectedSlotIndexFromCategories(object radialMenu)
            {
                var categories = GetMemberValue(radialMenu, "Categories") as IEnumerable;
                if (categories == null) return -1;

                foreach (var category in categories)
                {
                    var slotIndex = ResolveSelectedSlotIndexBySelectionState(category);
                    if (slotIndex >= 0) return slotIndex;
                }

                return -1;
            }

            private static int ResolveSelectedSlotIndexBySelectionState(object category)
            {
                if (category == null) return -1;

                var slots = GetMemberValue(category, "Slots") as IEnumerable;
                if (slots == null) return -1;

                var index = 0;
                foreach (var slot in slots)
                {
                    if (IsRadialSlotSelected(slot))
                        return index;

                    index++;
                }

                return -1;
            }
        }
    }
}
