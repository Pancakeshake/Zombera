using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Zombera.Systems;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.BuildingSystem
{
    public sealed partial class EasyBuildRadialMenuInputBridge
    {
        private int GetBuildItemCountFromManager()
        {
            var manager = ResolveBuildingManagerInstance();
            if (manager == null) return 0;

            var count = Mathf.Max(0, EasyBuildBridgeManagerPartHelper.GetManagerPartCount(manager));
            BridgeLog($"Manager part count resolved to {count}", true);
            return count;
        }

        private bool TrySetControllerModeCandidates(params string[] modeNames)
        {
            return EasyBuildBridgeControllerModeHelper.TrySetControllerModeCandidates(
                modeNames,
                TrySetControllerMode,
                message => BridgeLog(message));
        }

        private string ComposeBuildControlsHelpText()
        {
            var buildingInput = ResolveBuildingInputInstance();
            return EasyBuildBridgeControlsHintHelper.ComposeBuildControlsHelpText(
                buildingInput,
                GetPlacementHeightOffset());
        }

        private object ResolveSelectedPartPlacementSettings()
        {
            var controller = ResolveBuildingControllerInstance();
            return EasyBuildBridgeRuntimeResolverHelper.ResolveSelectedPartPlacementSettings(controller);
        }

        private object ResolveBuildingInputInstance()
        {
            var controller = ResolveBuildingControllerInstance();
            return EasyBuildBridgeRuntimeResolverHelper.ResolveBuildingInputInstance(
                BuildingInputTypeName,
                controller,
                ref _cachedBuildingInputType,
                ref _cachedBuildingInput);
        }


        private bool TryResolveHudSpriteFromLibrary(int hudItemIndex, out Sprite sprite)
        {
            sprite = null;

            if (TryResolveFilteredHudPart(hudItemIndex, out var filteredPart)
                && TryResolveSpriteFromLibrary(filteredPart, out sprite))
                return true;

            var part = ResolveHudPart(hudItemIndex);
            return TryResolveSpriteFromLibrary(part, out sprite);
        }

        private bool TryResolveSpriteFromLibrary(object part, out Sprite sprite)
        {
            sprite = null;

#if UNITY_EDITOR
            if (buildPrefabSpriteLibrary == null)
                EnsureBuildPrefabSpriteLibraryReference();
#endif

            if (buildPrefabSpriteLibrary == null || part == null)
                return false;

            var partReference = ResolvePartIdentifier(part);
            if (string.IsNullOrWhiteSpace(partReference))
                return false;

            return buildPrefabSpriteLibrary.TryGetSprite(partReference, out sprite) && sprite != null;
        }

        private bool TryResolveRadialSlotVisualData(int hudIndex, out string label, out Texture2D texture)
        {
            label = null;
            texture = null;

            if (hudIndex < 0) return false;
            if (!TryResolveRadialMenu(out var radialMenu) || radialMenu == null) return false;

            return EasyBuildBridgeHudVisualHelper.TryResolveRadialSlotVisualData(
                radialMenu,
                hudIndex,
                out label,
                out texture);
        }

        private bool TrySelectPartByHudIndex(int hudIndex)
        {
            if (hudIndex < 0) return false;

            if (TrySelectFilteredPartByHudIndex(hudIndex))
            {
                if (TryGetFilteredCatalogPartReference(hudIndex, out var filteredReference))
                    LogFilteredPlacementDiagnostic("SelectByHud.filtered", filteredReference, hudIndex, -1);

                BridgeLog($"Selection via filtered extension catalog: index={hudIndex}", true);
                return true;
            }

            if (TryInvokeRadialSlotSelectionAction(hudIndex))
            {
                BridgeLog($"Selection via radial slot action: index={hudIndex}", true);
                return true;
            }

            if (TryResolveRadialPartReferenceForHudIndex(hudIndex, out var partReference)
                && TrySelectPartByPartReference(partReference))
            {
                LogFilteredPlacementDiagnostic("SelectByHud.partReference", partReference, hudIndex, -1);
                BridgeLog($"Selection via part reference: index={hudIndex}, reference={partReference}", true);
                return true;
            }

            BridgeLog($"Selection falling back to manager index: {hudIndex}", true);
            return TrySelectPartByManagerIndex(hudIndex);
        }

        private void BridgeLog(string message, bool verbose = false)
        {
            if (!enableBridgeDebugLogs) return;
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            if (verbose && !verboseBridgeDebugLogs) return;

            Debug.Log($"[EasyBuildBridge] {message}", this);
        }

        private void LogFilteredPlacementDiagnostic(string stage, string partReference, int hudIndex, int managerIndex)
        {
            if (!enableBridgeDebugLogs || !enableFilteredPlacementDiagnostics) return;

            var displayToken = ResolveDiagnosticDisplayToken(partReference, managerIndex);
            if (!EasyBuildBridgePlacementDiagnosticsHelper.ShouldEmitFilteredPlacementDiagnostic(
                    stage,
                    partReference,
                    hudIndex,
                    managerIndex,
                    placementDiagnosticsPartFilter,
                    placementDiagnosticsRepeatIntervalSeconds,
                    _nextPlacementDiagnosticAtByKey,
                    displayToken))
                return;

            var displaySuffix = string.IsNullOrWhiteSpace(displayToken) ? string.Empty : $", display={displayToken}";
            BridgeLog(
                $"[PlacementDiag] {stage}: partRef={partReference}{displaySuffix}, hudIndex={hudIndex}, managerIndex={managerIndex}, manualBuild={_manualBuildModeActive}, radialOpen={IsRadialMenuOpen}");
        }

        private string ResolveDiagnosticDisplayToken(string partReference, int managerIndex)
        {
            object part = null;

            if (!string.IsNullOrWhiteSpace(partReference))
                part = ResolvePartByReference(partReference);

            if (part == null && managerIndex >= 0)
                part = ResolvePartByManagerIndex(managerIndex);

            if (part == null) return null;

            var displayName = ResolvePartDisplayName(part);
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName;

            return ResolvePartIdentifier(part);
        }

        private static object ResolveRadialCategoryByIndex(object radialMenu, int categoryIndex)
        {
            return EasyBuildBridgeSelectionHelper.ResolveRadialCategoryByIndex(radialMenu, categoryIndex);
        }

        private object ResolveHudPart(int hudIndex)
        {
            return EasyBuildBridgeHudPartResolverHelper.ResolveHudPart(
                hudIndex,
                this.TryResolveFilteredHudPart,
                this.TryResolveHudPartReferenceWithoutDiagnostics,
                this.ResolvePartByReference,
                this.ResolvePartByManagerIndex);
        }

        private object ResolvePartByManagerIndex(int index)
        {
            var manager = ResolveBuildingManagerInstance();
            return EasyBuildBridgeManagerPartHelper.ResolvePartByManagerIndex(manager, index);
        }

        private object ResolvePartByReference(string partReference)
        {
            var manager = ResolveBuildingManagerInstance();
            return EasyBuildBridgeManagerPartHelper.ResolvePartByReference(
                manager,
                partReference,
                ResolvePartIdentifier);
        }

        private static string ResolvePartIdentifier(object part)
        {
            return EasyBuildBridgePartDataHelper.ResolvePartIdentifier(part);
        }

        private string ResolvePartDisplayName(object part)
        {
            _ = this;

            string TryResolveCatalogPrefabName(string partId)
            {
#if UNITY_EDITOR
                if (!string.IsNullOrWhiteSpace(partId)
                    && _catalogPrefabNameByPartReference.TryGetValue(partId, out var catalogPrefabName)
                    && !string.IsNullOrWhiteSpace(catalogPrefabName))
                    return catalogPrefabName;
#endif
                _ = partId;
                return null;
            }

            return EasyBuildBridgePartDisplayNameHelper.ResolvePartDisplayName(
                part,
                ResolvePartIdentifier,
                TryResolveCatalogPrefabName);
        }

        private bool TryInvokeRadialSlotSelectionAction(int hudIndex)
        {
            if (!TryResolveRadialMenu(out var radialMenu) || radialMenu == null) return false;

            _cachedSelectionActionType ??= EasyBuildBridgeReflectionHelper.FindType(BuildingSelectionActionTypeName);
            if (_cachedSelectionActionType == null) return false;

            return EasyBuildBridgeSelectionHelper.TryInvokeRadialSlotSelectionAction(
                radialMenu,
                hudIndex,
                _cachedSelectionActionType);
        }

        private bool TrySelectPartByPartReference(string partReference)
        {
            var controller = ResolveBuildingControllerInstance();
            var manager = ResolveBuildingManagerInstance();
            return EasyBuildBridgeManagerPartHelper.TrySelectPartByPartReference(
                controller,
                manager,
                partReference,
                ResolvePartIdentifier,
                LogFilteredPlacementDiagnostic);
        }

        private bool TryResolveRadialPartReferenceForHudIndex(int hudIndex, out string partReference,
            bool emitDiagnostics = true)
        {
            partReference = null;
            if (hudIndex < 0) return false;

            if (TryResolvePartReferenceFromFilteredCatalog(hudIndex, emitDiagnostics, out partReference))
                return true;

            return TryResolvePartReferenceFromRadialMenu(hudIndex, emitDiagnostics, out partReference);
        }

        private bool TryResolveHudPartReferenceWithoutDiagnostics(int hudIndex, out string partReference)
        {
            return TryResolveRadialPartReferenceForHudIndex(hudIndex, out partReference, false);
        }

        private bool TryResolvePartReferenceFromFilteredCatalog(int hudIndex, bool emitDiagnostics,
            out string partReference)
        {
            partReference = null;
            if (!TryGetFilteredCatalogPartReference(hudIndex, out partReference)) return false;

            if (emitDiagnostics)
                LogFilteredPlacementDiagnostic("ResolvePartRef.filteredCatalog", partReference, hudIndex, -1);

            return true;
        }

        private bool TryResolvePartReferenceFromRadialMenu(int hudIndex, bool emitDiagnostics,
            out string partReference)
        {
            partReference = null;

            if (!TryResolveRadialMenu(out var radialMenu) || radialMenu == null) return false;

            _cachedSelectionActionType ??= EasyBuildBridgeReflectionHelper.FindType(BuildingSelectionActionTypeName);
            if (_cachedSelectionActionType == null) return false;

            return EasyBuildBridgeSelectionHelper.TryResolvePartReferenceFromRadialMenu(
                radialMenu,
                hudIndex,
                _cachedSelectionActionType,
                emitDiagnostics,
                LogFilteredPlacementDiagnostic,
                out partReference);
        }

        private static object ResolveActiveOrFirstCategory(object radialMenu, bool assignIfMissing)
        {
            return EasyBuildBridgeSelectionHelper.ResolveActiveOrFirstCategory(radialMenu, assignIfMissing);
        }

        private bool IsUsingExtensionFolderFilter()
        {
            return ResolveExtensionFolderFilterState(restrictCatalogToExtensionFolders);
        }

        private static bool ResolveExtensionFolderFilterState(bool restrictToExtensionFolders)
        {
#if UNITY_EDITOR
            return restrictToExtensionFolders;
#else
            _ = restrictToExtensionFolders;
            return false;
#endif
        }

    #if UNITY_EDITOR
        private int ComputeExtensionCatalogSignature()
        {
            return EasyBuildBridgeCatalogHelper.ComputeExtensionCatalogSignature(extensionCatalogFolders);
        }

        private static int ResolveManagerPartCount(object manager)
        {
            return EasyBuildBridgeCatalogHelper.ResolveManagerPartCount(manager);
        }
    #endif

        private int GetFilteredCatalogPartCount()
        {
            RefreshFilteredCatalogPartReferencesIfNeeded();
            return _filteredCatalogPartReferences.Count;
        }

        private bool TryGetFilteredCatalogPartReference(int hudIndex, out string partReference)
        {
            partReference = null;
            if (!IsUsingExtensionFolderFilter()) return false;

            RefreshFilteredCatalogPartReferencesIfNeeded();
            if (hudIndex < 0 || hudIndex >= _filteredCatalogPartReferences.Count) return false;

            partReference = _filteredCatalogPartReferences[hudIndex];
            return !string.IsNullOrWhiteSpace(partReference);
        }

        private bool TryResolveFilteredHudPart(int hudIndex, out object part)
        {
            part = null;
            if (!TryGetFilteredCatalogPartReference(hudIndex, out var partReference)) return false;

            part = ResolvePartByReference(partReference);
            return part != null;
        }

        private bool TrySelectFilteredPartByHudIndex(int hudIndex)
        {
            if (!TryGetFilteredCatalogPartReference(hudIndex, out var partReference)) return false;
            return TrySelectPartByPartReference(partReference);
        }

        private void RefreshFilteredCatalogPartReferencesIfNeeded()
        {
    #if UNITY_EDITOR
            if (!IsUsingExtensionFolderFilter()) return;
            if (ShouldDeferFilteredCatalogRefresh()) return;

            var folderSignature = ComputeExtensionCatalogSignature();
            var manager = ResolveBuildingManagerInstance();
            var managerPartCount = ResolveManagerPartCount(manager);
            if (CanReuseFilteredCatalogSnapshot(folderSignature, manager, managerPartCount)) return;

            ClearFilteredCatalogSnapshotBuffers();

            var folderPrefabIds = CollectExtensionFolderPrefabIds(_catalogPrefabNameByPartReference);
            if (folderPrefabIds.Count == 0)
            {
                CommitFilteredCatalogSnapshot(folderSignature, managerPartCount, false);
                BridgeLog("Extension-folder catalog filter enabled but no prefab IDs were found.");
                return;
            }

            if (manager == null)
            {
                PopulateFilteredCatalogFromFolderIds(folderPrefabIds);
                CommitFilteredCatalogSnapshot(folderSignature, -1, true);

                BridgeLog($"Filtered catalog built from folder IDs only: count={_filteredCatalogPartReferences.Count}");
                return;
            }

            PopulateFilteredCatalogFromManager(manager, managerPartCount, folderPrefabIds);
            CommitFilteredCatalogSnapshot(folderSignature, managerPartCount, true);

            BridgeLog($"Filtered catalog built from extension folders: count={_filteredCatalogPartReferences.Count}");
    #endif
        }

#if UNITY_EDITOR
        private bool ShouldDeferFilteredCatalogRefresh()
        {
            if (!deferExtensionCatalogBuildUntilBuildUiActive || IsBuildUiActive) return false;
            if (_hasFilteredCatalogSnapshot) return true;
            if (Time.unscaledTime < _nextFilteredCatalogRefreshAt) return true;

            _nextFilteredCatalogRefreshAt = Time.unscaledTime + 0.5f;
            return true;
        }

        private bool CanReuseFilteredCatalogSnapshot(int folderSignature, object manager, int managerPartCount)
        {
            var canReuseSnapshot = _hasFilteredCatalogSnapshot
                                   && _filteredCatalogPartReferences.Count > 0
                                   && folderSignature == _lastExtensionCatalogSignature
                                   && managerPartCount == _lastFilteredCatalogManagerPartCount;

            if (!canReuseSnapshot) return false;

            // If manager is available and part count is unchanged, preserve a stable item order.
            if (manager != null) return true;

            // While manager is unavailable, retry periodically to switch from folder-only IDs to manager-backed ordering.
            return Time.unscaledTime < _nextFilteredCatalogRefreshAt;
        }

        private void ClearFilteredCatalogSnapshotBuffers()
        {
            _filteredCatalogPartReferences.Clear();
            _filteredCatalogPartReferenceSet.Clear();
            _catalogPrefabNameByPartReference.Clear();
        }

        private void PopulateFilteredCatalogFromFolderIds(IEnumerable<string> folderPrefabIds)
        {
            EasyBuildBridgeCatalogHelper.PopulateFilteredCatalogFromFolderIds(
                folderPrefabIds,
                _filteredCatalogPartReferenceSet,
                _filteredCatalogPartReferences);
        }

        private void PopulateFilteredCatalogFromManager(object manager, int partCount, ISet<string> folderPrefabIds)
        {
            EasyBuildBridgeCatalogHelper.PopulateFilteredCatalogFromManager(
                manager,
                partCount,
                folderPrefabIds,
                _filteredCatalogPartReferenceSet,
                _filteredCatalogPartReferences);
        }

        private void CommitFilteredCatalogSnapshot(int folderSignature, int managerPartCount, bool hasSnapshot)
        {
            _hasFilteredCatalogSnapshot = hasSnapshot;
            _lastExtensionCatalogSignature = folderSignature;
            _lastFilteredCatalogManagerPartCount = managerPartCount;
            _nextFilteredCatalogRefreshAt = Time.unscaledTime + 2f;
        }

        private HashSet<string> CollectExtensionFolderPrefabIds(Dictionary<string, string> prefabNamesByPartReference)
        {
            return EasyBuildBridgeCatalogHelper.CollectExtensionFolderPrefabIds(
                extensionCatalogFolders,
                prefabNamesByPartReference);
        }

#endif

    }
}
