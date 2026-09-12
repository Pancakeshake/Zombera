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
        private bool TrySelectPartByManagerIndex(int index)
        {
            var controller = ResolveBuildingControllerInstance();
            var manager = ResolveBuildingManagerInstance();
            return EasyBuildBridgeManagerPartHelper.TrySelectPartByManagerIndex(
                controller,
                manager,
                index,
                ResolvePartIdentifier,
                LogFilteredPlacementDiagnostic);
        }

        private void EnsureSelectedPart()
        {
            var controller = ResolveBuildingControllerInstance();
            if (controller == null) return;
            var ctx = new SelectedPartSyncContext(
                TrySelectPartByHudIndex,
                GetBuildItemCount,
                TryResolveRadialPartReferenceForHudIndex,
                ResolveBuildingManagerInstance,
                EasyBuildBridgeManagerPartHelper.GetManagerPartCount,
                EasyBuildBridgeManagerPartHelper.GetManagerPartByIndex,
                ResolvePartIdentifier,
                LogFilteredPlacementDiagnostic,
                ensureSelectedPartDiagnosticsIntervalSeconds);
            EasyBuildBridgeSelectedPartSyncHelper.EnsureSelectedPart(
                controller,
                ctx,
                ref _currentHudSelectionIndex,
                ref _lastEnsureSelectedPartReference,
                ref _lastEnsureSelectedHudIndex,
                ref _nextEnsureSelectedPartDiagnosticAt);
        }

        private void ForceMousePlacementRefresh()
        {
            _cachedCursorPlacementBinder?.ForceMousePlacementRefresh();
        }

        private void SetPlacementMode()
        {
            EnsurePlacementViewReady();
            _ = TrySetPlacementMode();
        }

        private bool TrySetControllerMode(string modeName)
        {
            var controller = ResolveBuildingControllerInstance();
            return EasyBuildBridgeRuntimeResolverHelper.TrySetControllerMode(
                controller,
                modeName,
                BuildingModeEnumTypeName,
                ref _cachedBuildingModeType);
        }

        private object ResolveBuildingControllerInstance()
        {
            return EasyBuildBridgeRuntimeResolverHelper.ResolveBuildingControllerInstance(
                BuildingControllerTypeName,
                ref _cachedBuildingControllerType,
                ref _cachedBuildingController);
        }

        private object ResolveBuildingManagerInstance()
        {
            return EasyBuildBridgeRuntimeResolverHelper.ResolveBuildingManagerInstance(
                BuildingManagerTypeName,
                ref _cachedBuildingManagerType);
        }

        private bool TryResolveRadialMenu(out object radialMenu)
        {
            return EasyBuildBridgeRuntimeResolverHelper.TryResolveRadialMenu(
                BuildingRadialMenuUiTypeName,
                ref _cachedRadialMenuType,
                ref _cachedRadialMenu,
                out radialMenu);
        }

        private void ApplyRadialMenuRuntimeOverrides(object radialMenu)
        {
            if (radialMenu is not MonoBehaviour radialMenuBehaviour) return;

            ApplyRadialMenuVerticalOffset(radialMenuBehaviour);

            if (radialMenuUsesUnscaledTime)
                ConfigureRadialMenuUnscaledTime(radialMenuBehaviour);
        }

        private void ApplyRadialMenuVerticalOffset(MonoBehaviour radialMenuBehaviour)
        {
            EasyBuildBridgeRuntimeResolverHelper.ApplyRadialMenuVerticalOffset(
                radialMenuBehaviour,
                radialMenuVerticalScreenOffsetPercent,
                ref _cachedRadialMenuRect,
                ref _cachedRadialMenuBaseAnchoredPosition,
                ref _cachedRadialMenuScreenHeight,
                ref _cachedRadialMenuUnscaledConfigured);
        }

        private void ConfigureRadialMenuUnscaledTime(MonoBehaviour radialMenuBehaviour)
        {
            EasyBuildBridgeRuntimeResolverHelper.ConfigureRadialMenuUnscaledTime(
                radialMenuBehaviour,
                ref _cachedRadialMenuUnscaledConfigured);
        }
    }
}
