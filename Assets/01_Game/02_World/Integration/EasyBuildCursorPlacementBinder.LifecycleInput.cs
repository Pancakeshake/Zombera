#region

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

#endregion

namespace Zombera.BuildingSystem
{
    public sealed partial class EasyBuildCursorPlacementBinder
    {
        private void Awake()
        {
            if (Application.isPlaying && disablePlacementDiagnosticsInPlayMode)
            {
                enablePlacementDiagnosticsLogs = false;
                enablePreviewScanDiagnosticsLogs = false;
            }

            ResolveBuildingController();
            ResolveBuildPlacementController();
            EnsureRuntimePlacementFixer();
        }

        private void OnEnable()
        {
            if (!useCursorRayPlacement) return;

            _nextCursorViewEnforceAt = 0f;
            _nextPreviewFixAt = 0f;
            StartCoroutine(ApplyAfterBuildingControllerStart());
        }

        private void LateUpdate()
        {
            EnforceKeyboardBuildToggleBlock();

            var isBuildModeActive = false;
            if (useCursorRayPlacement && enforceCursorViewDuringActiveBuildMode)
                isBuildModeActive = EnsureMouseDrivenViewWhileActive();
            else if (useCursorRayPlacement)
                isBuildModeActive = IsBuildModeCurrentlyActive();

            if (ensureAllRadialSlotsHaveValidParts && !_radialSlotsValidated && isBuildModeActive)
                _radialSlotsValidated = TryRepairRadialMenuSelectionSlots();

            if (!keepPreviewAboveGround) return;
            if (!isBuildModeActive) return;
            if (Time.unscaledTime < _nextPreviewFixAt) return;

            _nextPreviewFixAt = Time.unscaledTime + Mathf.Max(0.05f, previewFixIntervalSeconds);

            FixActivePlacementPreviews();
        }

        private void EnforceKeyboardBuildToggleBlock()
        {
            if (!blockAlternateKeyboardBuildToggle || Keyboard.current == null) return;
            if (!Keyboard.current[blockedKeyboardBuildToggleKey].wasPressedThisFrame) return;

            ResolveBuildingController();
            ForceCloseEasyBuildMode();
        }

        public void ForceClosePlacementPreview()
        {
            ForceCloseEasyBuildMode();
        }

        private void ForceCloseEasyBuildMode()
        {
            _cachedBuildingModeEnumType ??= FindType(BuildingModeEnumTypeName);
            var modeEnumType = _cachedBuildingModeEnumType;

            if (modeEnumType != null && buildingController != null)
            {
                var noneValue = Enum.Parse(modeEnumType, "None");

                // Try method-based mode transitions first; fallback to writing ActiveMode directly.
                InvokeMethod(buildingController, "SetMode", noneValue);
                InvokeMethod(buildingController, "SetBuildingMode", noneValue);
                SetMemberValue(buildingController, "ActiveMode", noneValue);
            }

            var radialType = FindType(BuildingRadialMenuTypeName);
            var radialMenu = GetStaticMemberValue(radialType, "Instance");
            InvokeMethod(radialMenu, "CloseMenu");
        }

        private IEnumerator ApplyAfterBuildingControllerStart()
        {
            yield return null;
            EnsurePlacementViewReady();
        }

        private void ResolveBuildPlacementController()
        {
            if (buildPlacementController != null) return;

            buildPlacementController = GetComponent<BuildPlacementController>();
            if (buildPlacementController != null) return;

            buildPlacementController = GetComponentInParent<BuildPlacementController>();
        }

        private void EnsureRuntimePlacementFixer()
        {
            if (GetComponent<RuntimePlacedStructureFixer>() != null) return;

            // RuntimePlacedStructureFixer elects a global polling owner; avoid adding duplicate pollers up front.
            if (FindFirstObjectByType<RuntimePlacedStructureFixer>() != null) return;

            _ = gameObject.AddComponent<RuntimePlacedStructureFixer>();
        }

        private bool IsMousePlacementAllowed()
        {
            if (!useCursorRayPlacement) return false;

            ResolveBuildingController();
            if (buildingController == null) return false;

            if (!IsEasyBuildModeActive(buildingController)) return false;

            if (!requireLocalBuilderModeForMouseRayPlacement) return true;

            ResolveBuildPlacementController();
            return buildPlacementController != null && buildPlacementController.IsBuildModeActive;
        }

        private bool EnsureMouseDrivenViewWhileActive()
        {
            var isBuildModeActive = IsMousePlacementAllowed();
            if (!isBuildModeActive)
            {
                _wasEasyBuildModeActive = false;
                return false;
            }

            if (_wasEasyBuildModeActive && Time.unscaledTime < _nextCursorViewEnforceAt)
                return true;

            EnsurePlacementViewReady();
            _wasEasyBuildModeActive = true;
            _nextCursorViewEnforceAt = Time.unscaledTime + Mathf.Max(0.05f, enforceCursorViewIntervalSeconds);
            return true;
        }

        private bool IsBuildModeCurrentlyActive()
        {
            return IsMousePlacementAllowed();
        }

        private bool IsEasyBuildModeActive(object controller)
        {
            _cachedBuildingModeEnumType ??= FindType(BuildingModeEnumTypeName);
            var modeEnumType = _cachedBuildingModeEnumType;
            if (modeEnumType == null) return true;

            var activeMode = GetMemberValue(controller, "ActiveMode");
            if (activeMode == null) return true;

            var noneValue = Enum.Parse(modeEnumType, "None");
            return !activeMode.Equals(noneValue);
        }
    }
}
