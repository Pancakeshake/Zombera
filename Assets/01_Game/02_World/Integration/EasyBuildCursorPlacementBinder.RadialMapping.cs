#region

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.BuildingSystem
{
    public sealed partial class EasyBuildCursorPlacementBinder
    {
        private bool TryRepairRadialMenuSelectionSlots()
        {
            if (!TryResolveRadialMenuInstance(out var radialMenu)) return false;

            var availablePartIds = BuildAvailablePartIdList();
            if (availablePartIds.Count == 0) return true;

            _cachedSelectionActionType ??= FindType(BuildingSelectionActionTypeName);
            var selectionActionType = _cachedSelectionActionType;
            if (selectionActionType == null) return true;

            var changed = RadialMenuSelectionRepairService.TryRepairSlots(
                radialMenu,
                selectionActionType,
                availablePartIds,
                DoesPartReferenceExist);

            if (changed)
                InvokeMethod(radialMenu, "ForceRebuildAllSlots");

            return true;
        }

        private bool TryResolveRadialMenuInstance(out object radialMenu)
        {
            radialMenu = null;

            _cachedRadialMenuType ??= FindType(BuildingRadialMenuTypeName);
            var radialType = _cachedRadialMenuType;
            if (radialType == null) return false;

            radialMenu = GetStaticMemberValue(radialType, "Instance");
            return radialMenu != null;
        }

        private bool TryResolveRadialMenuAndSelectionActionType(out object radialMenu, out Type selectionActionType)
        {
            radialMenu = null;
            selectionActionType = null;

            if (!TryResolveRadialMenuInstance(out radialMenu))
                return false;

            _cachedSelectionActionType ??= FindType(BuildingSelectionActionTypeName);
            selectionActionType = _cachedSelectionActionType;
            return selectionActionType != null;
        }

        private static List<string> BuildAvailablePartIdList()
        {
            var result = new List<string>();

            var managerType = FindType(BuildingManagerTypeName);
            if (managerType == null) return result;

            var managerInstance = GetStaticMemberValue(managerType, "Instance");
            if (managerInstance == null) return result;

            var partCountObj = InvokeMethodWithReturn(managerInstance, "GetPartCount");
            var partCount = partCountObj is int i ? i : 0;
            if (partCount <= 0) return result;

            for (var idx = 0; idx < partCount; idx++)
            {
                var part = InvokeMethodWithReturn(managerInstance, "GetPartByIndex", idx);
                var prefabId = GetMemberValue(part, "PrefabId") as string;
                if (string.IsNullOrWhiteSpace(prefabId)) continue;

                if (!result.Contains(prefabId))
                    result.Add(prefabId);
            }

            return result;
        }

        private void EnsureRadialHudIndexMap()
        {
            _cachedRadialMenuType ??= FindType(BuildingRadialMenuTypeName);
            var radialType = _cachedRadialMenuType;
            if (radialType == null)
            {
                _radialHudIndexByPartReference.Clear();
                return;
            }

            var radialMenu = GetStaticMemberValue(radialType, "Instance");
            if (radialMenu == null)
            {
                _radialHudIndexByPartReference.Clear();
                return;
            }

            _cachedSelectionActionType ??= FindType(BuildingSelectionActionTypeName);
            var selectionActionType = _cachedSelectionActionType;
            if (selectionActionType == null)
            {
                _radialHudIndexByPartReference.Clear();
                return;
            }

            RadialHudIndexMapService.BuildCategoryMap(radialMenu, selectionActionType, _radialHudIndexByPartReference);
        }

        private string TryResolveSelectedRadialPartReference()
        {
            if (!TryResolveRadialMenuAndSelectionActionType(out var radialMenu, out var selectionActionType))
                return null;

            return RadialHudIndexMapService.ResolveSelectedPartReference(radialMenu, selectionActionType);
        }

        private int TryResolveRadialSelectedSlotIndex()
        {
            if (!TryResolveRadialMenuInstance(out var radialMenu)) return -1;

            return RadialHudIndexMapService.ResolveSelectedSlotIndex(radialMenu);
        }

        private string TryResolveSelectedPartReferenceFromController()
        {
            var selectedPart = TryResolveSelectedPartFromController();
            if (selectedPart == null) return null;

            return GetMemberValue(selectedPart, "PrefabId") as string
                   ?? GetMemberValue(selectedPart, "PartReference") as string
                   ?? GetMemberValue(selectedPart, "ID") as string;
        }

        private string TryResolveSelectedPartDisplayNameFromController()
        {
            var selectedPart = TryResolveSelectedPartFromController();
            if (selectedPart == null) return null;

            var prefabObject = GetMemberValue(selectedPart, "Prefab")
                               ?? GetMemberValue(selectedPart, "PartPrefab")
                               ?? GetMemberValue(selectedPart, "BuildingPrefab")
                               ?? GetMemberValue(selectedPart, "GameObject")
                               ?? GetMemberValue(selectedPart, "PreviewPrefab");

            if (prefabObject is GameObject prefabGo && !string.IsNullOrWhiteSpace(prefabGo.name))
                return prefabGo.name;

            if (prefabObject is Component prefabComponent && prefabComponent.gameObject != null
                                                       && !string.IsNullOrWhiteSpace(prefabComponent.gameObject.name))
                return prefabComponent.gameObject.name;

            return GetMemberValue(selectedPart, "DisplayName") as string
                   ?? GetMemberValue(selectedPart, "Title") as string
                   ?? GetMemberValue(selectedPart, "Name") as string;
        }

        private object TryResolveSelectedPartFromController()
        {
            ResolveBuildingController();
            if (buildingController == null) return null;

            return GetMemberValue(buildingController, "SelectedPart")
                   ?? GetMemberValue(buildingController, "CurrentPart")
                   ?? GetMemberValue(buildingController, "ActivePart");
        }

        private static int MapPartReferenceToHudBoxIndex(string partReference)
        {
            if (string.IsNullOrWhiteSpace(partReference)) return -1;

            var key = partReference.Trim().ToLowerInvariant();

            if (key.Contains("window")) return 1;
            if (key.Contains("door")) return 2;
            if (key.Contains("damage") || key.Contains("broken")) return 3;

            if (key.Contains("skin") || key.Contains("material") || key.Contains("mat"))
            {
                var parsedDigit = TryExtractTrailingOneBasedDigit(key);
                if (parsedDigit >= 1 && parsedDigit <= 5)
                    return 3 + parsedDigit;
            }

            if (key.Contains("wall")) return 0;

            return -1;
        }

        private static int TryExtractTrailingOneBasedDigit(string value)
        {
            if (string.IsNullOrEmpty(value)) return -1;

            for (var i = value.Length - 1; i >= 0; i--)
            {
                if (!char.IsDigit(value[i])) continue;

                var start = i;
                while (start > 0 && char.IsDigit(value[start - 1])) start--;

                var token = value.Substring(start, i - start + 1);
                return int.TryParse(token, out var parsed) ? parsed : -1;
            }

            return -1;
        }

        private static bool DoesPartReferenceExist(string partReference)
        {
            if (string.IsNullOrWhiteSpace(partReference)) return false;

            var registryType = FindType(BuildingPartRegistryTypeName);
            if (registryType == null) return false;

            var registry = GetStaticMemberValue(registryType, "Instance");
            if (registry == null) return false;

            var part = InvokeMethodWithReturn(registry, "GetPartByPrefabId", partReference);
            return part != null;
        }
    }
}
