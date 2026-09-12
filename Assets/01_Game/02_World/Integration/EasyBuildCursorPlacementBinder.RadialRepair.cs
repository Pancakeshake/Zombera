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
        ///     Repairs broken/missing part references in radial menu selection slots using
        ///     the currently available building parts from the EasyBuild manager.
        /// </summary>
        private static class RadialMenuSelectionRepairService
        {
            internal static bool TryRepairSlots(
                object radialMenu,
                Type selectionActionType,
                IReadOnlyList<string> availablePartIds,
                Func<string, bool> doesPartReferenceExist)
            {
                var categories = GetMemberValue(radialMenu, "Categories") as IEnumerable;
                if (categories == null) return false;

                var usedPartIds = new HashSet<string>(StringComparer.Ordinal);
                var changed = false;
                var nextFallbackIndex = 0;

                foreach (var category in categories)
                {
                    if (!TryRepairCategorySelectionSlots(
                            category,
                            selectionActionType,
                            availablePartIds,
                            usedPartIds,
                            doesPartReferenceExist,
                            ref nextFallbackIndex))
                        continue;

                    changed = true;
                }

                return changed;
            }

            private static bool TryRepairCategorySelectionSlots(
                object category,
                Type selectionActionType,
                IReadOnlyList<string> availablePartIds,
                ISet<string> usedPartIds,
                Func<string, bool> doesPartReferenceExist,
                ref int nextFallbackIndex)
            {
                if (category == null) return false;

                var slots = GetMemberValue(category, "Slots") as IEnumerable;
                if (slots == null) return false;

                var changed = false;
                foreach (var slot in slots)
                {
                    if (!TryRepairSlotSelectionAction(
                            slot,
                            selectionActionType,
                            availablePartIds,
                            usedPartIds,
                            doesPartReferenceExist,
                            ref nextFallbackIndex))
                        continue;

                    changed = true;
                }

                return changed;
            }

            private static bool TryRepairSlotSelectionAction(
                object slot,
                Type selectionActionType,
                IReadOnlyList<string> availablePartIds,
                ISet<string> usedPartIds,
                Func<string, bool> doesPartReferenceExist,
                ref int nextFallbackIndex)
            {
                if (slot == null) return false;

                var action = GetMemberValue(slot, "Action");
                if (action == null || !selectionActionType.IsInstanceOfType(action)) return false;

                var currentRef = GetMemberValue(action, "PartReference") as string;
                if (!string.IsNullOrWhiteSpace(currentRef) && doesPartReferenceExist(currentRef))
                {
                    usedPartIds.Add(currentRef);
                    return false;
                }

                var fallback = FindNextUnusedValidPartId(
                    availablePartIds,
                    usedPartIds,
                    doesPartReferenceExist,
                    ref nextFallbackIndex);
                if (string.IsNullOrEmpty(fallback)) return false;

                SetMemberValue(action, "PartReference", fallback);
                usedPartIds.Add(fallback);
                return true;
            }

            private static string FindNextUnusedValidPartId(
                IReadOnlyList<string> availablePartIds,
                ISet<string> usedPartIds,
                Func<string, bool> doesPartReferenceExist,
                ref int startIndex)
            {
                if (availablePartIds == null || availablePartIds.Count == 0) return null;

                for (var i = startIndex; i < availablePartIds.Count; i++)
                {
                    var candidate = availablePartIds[i];
                    if (string.IsNullOrWhiteSpace(candidate)) continue;
                    if (usedPartIds.Contains(candidate)) continue;

                    startIndex = i + 1;
                    return candidate;
                }

                for (var i = 0; i < availablePartIds.Count; i++)
                {
                    var candidate = availablePartIds[i];
                    if (string.IsNullOrWhiteSpace(candidate)) continue;
                    if (!doesPartReferenceExist(candidate)) continue;

                    return candidate;
                }

                return null;
            }
        }
    }
}
