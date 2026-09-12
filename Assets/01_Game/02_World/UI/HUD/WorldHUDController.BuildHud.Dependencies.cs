#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.UI
{
#pragma warning disable S101 // HUD is an intentional acronym in this project's naming convention
    public sealed partial class WorldHUDController
    {
        private const float BuildDependencyRefreshIntervalSeconds = 1f;
        private PlayerInputController _buildHudPlayerInputController;

        private enum BuildHudDependencyRefreshPolicy
        {
            CachedOnly,
            IfMissing,
            ForceSceneScan
        }


        private void ResolveEasyBuildReferences()
        {
            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.ForceSceneScan);
        }


        private void RefreshBuildDependenciesForTick()
        {
            // Only refresh if something essential is actually missing, and scan sparingly
            if (!NeedsBuildDependencySceneScan()) return;

            if (Time.unscaledTime < _nextBuildModeResolveAt)
                return;

            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.IfMissing);
            _nextBuildModeResolveAt = Time.unscaledTime + BuildDependencyRefreshIntervalSeconds;
        }


        private void RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy refreshPolicy)
        {
            if (easyBuildRadialMenuBridge != null)
                _easyBuildRadialMenuInputBridge = easyBuildRadialMenuBridge;

            if (easyBuildCursorPlacementBinder != null)
                _easyBuildCursorPlacementBinder = easyBuildCursorPlacementBinder;

            var allowSceneScan = refreshPolicy == BuildHudDependencyRefreshPolicy.ForceSceneScan
                                 || (refreshPolicy == BuildHudDependencyRefreshPolicy.IfMissing && NeedsBuildDependencySceneScan());

            var playerInput = ResolveBuildHudPlayerInputController(allowSceneScan);

            ResolveFromTransform(ref _easyBuildRadialMenuInputBridge, easyBuildOwnerTransform);
            ResolveFromTransform(ref _easyBuildCursorPlacementBinder, easyBuildOwnerTransform);

            ResolveFromPlayerInput(ref _easyBuildRadialMenuInputBridge, playerInput);
            ResolveFromPlayerInput(ref _easyBuildCursorPlacementBinder, playerInput);

            if (_legacyBuildPlacementController == null && playerInput != null)
                _legacyBuildPlacementController = playerInput.GetComponent<BuildPlacementController>();

            if (!allowSceneScan)
                return;

            ResolveFromScene(ref _easyBuildRadialMenuInputBridge);
            ResolveFromScene(ref _easyBuildCursorPlacementBinder);

            if (_legacyBuildPlacementController == null)
                _legacyBuildPlacementController = FindFirstObjectByType<BuildPlacementController>(FindObjectsInactive.Include);
        }

        private static void ResolveFromTransform<T>(ref T field, Transform source) where T : Component
        {
            if (field != null || source == null) return;
            field = source.GetComponentInChildren<T>(true);
        }

        private static void ResolveFromPlayerInput<T>(ref T field, PlayerInputController playerInput) where T : Component
        {
            if (field != null || playerInput == null) return;
            field = playerInput.GetComponent<T>() ?? playerInput.GetComponentInChildren<T>(true);
        }

        private static void ResolveFromScene<T>(ref T field) where T : Component
        {
            if (field != null) return;
            field = FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }


        private bool NeedsBuildDependencySceneScan()
        {
            return _easyBuildRadialMenuInputBridge == null
                   || _easyBuildCursorPlacementBinder == null
                   || _legacyBuildPlacementController == null
                   || _buildHudPlayerInputController == null;
        }


        private PlayerInputController ResolveBuildHudPlayerInputController(bool allowSceneScan)
        {
            if (_buildHudPlayerInputController != null)
                return _buildHudPlayerInputController;

            if (!allowSceneScan)
                return null;

            _buildHudPlayerInputController = FindFirstObjectByType<PlayerInputController>(FindObjectsInactive.Include);
            return _buildHudPlayerInputController;
        }


        private bool EnsureLegacyBuildPlacementController()
        {
            RefreshBuildHudDependencies(BuildHudDependencyRefreshPolicy.IfMissing);
            return _legacyBuildPlacementController != null;
        }


        private bool IsBuildModeActive()
        {
            if (_easyBuildCursorPlacementBinder != null &&
                _easyBuildCursorPlacementBinder.isActiveAndEnabled &&
                _easyBuildCursorPlacementBinder.IsEasyBuildPlacementModeActive)
                return true;

            if (_easyBuildRadialMenuInputBridge != null
                && _easyBuildRadialMenuInputBridge.isActiveAndEnabled
                && _easyBuildRadialMenuInputBridge.IsBuildUiActive)
                return true;

            return _legacyBuildPlacementController != null
                   && _legacyBuildPlacementController.isActiveAndEnabled
                   && _legacyBuildPlacementController.IsBuildModeActive;
        }
    }
}
