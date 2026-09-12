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
        private void OnValidate()
        {
            EnsurePrefabModularCatalogFolderConfigured();
#if UNITY_EDITOR
            EnsureBuildPrefabSpriteLibraryReference();
#endif
        }

#if UNITY_EDITOR
        private void EnsureBuildPrefabSpriteLibraryReference()
        {
            if (buildPrefabSpriteLibrary != null) return;

            var generatedLibrary = AssetDatabase.LoadAssetAtPath<BuildPrefabSpriteLibrary>(GeneratedSpriteLibraryAssetPath);
            if (generatedLibrary != null)
                buildPrefabSpriteLibrary = generatedLibrary;
        }
#endif

        private void Awake()
        {
            EnsurePrefabModularCatalogFolderConfigured();

            if (Application.isPlaying && disableBridgeDiagnosticsInPlayMode)
            {
                enableBridgeDebugLogs = false;
                verboseBridgeDebugLogs = false;
                enableFilteredPlacementDiagnostics = false;
            }

#if UNITY_EDITOR
            EnsureBuildPrefabSpriteLibraryReference();
#endif

            if (!enforceBKeyOnlyMenuAccess) return;

            toggleRadialMenuWithKeyboard = true;
            toggleRadialMenuKey = Key.B;
            rightClickTogglesRadialMenu = false;
            requireLocalBuilderModeForRadialToggle = false;
        }

        private void EnsurePrefabModularCatalogFolderConfigured()
        {
            if (extensionCatalogFolders == null || extensionCatalogFolders.Length == 0)
            {
                extensionCatalogFolders = new[] { PrefabModularCatalogFolder };
                return;
            }

            for (var i = 0; i < extensionCatalogFolders.Length; i++)
            {
                if (string.Equals(extensionCatalogFolders[i], PrefabModularCatalogFolder,
                        StringComparison.OrdinalIgnoreCase))
                    return;
            }

            var updated = new string[extensionCatalogFolders.Length + 1];
            updated[0] = PrefabModularCatalogFolder;
            Array.Copy(extensionCatalogFolders, 0, updated, 1, extensionCatalogFolders.Length);
            extensionCatalogFolders = updated;
        }

        private void Start()
        {
            if (!removeSampleBuildingGroupCubeOnPlay || !Application.isPlaying) return;

            StartCoroutine(RemoveSampleBuildingGroupArtifactsDeferred());
        }

        private void Update()
        {
            if (!IsOwnerInputControllerActive()) return;

            _blockBuildToggleThisFrame = false;

            if (EasyBuildBridgeInputFrameHelper.TryHandleForceCloseHotkey(Keyboard.current, ForceCloseBuildUi))
            {
                // Keep E inert for build mode so only B + HUD controls drive placement state.
                _blockBuildToggleThisFrame = true;
                PublishBuildUiStateIfChanged();
                return;
            }

            TryRefreshMousePlacementWhileMenuOpen();

            if (_manualBuildModeActive)
                EnsureRadialMenuClosed();

            if (IsBuildUiActive)
            {
                EnsureSelectedPart();
                HandleLiveBuildShortcuts();
            }

            if (TryHandleKeyboardToggleInput())
            {
                PublishBuildUiStateIfChanged();
                return;
            }

            _ = EasyBuildBridgeInputFrameHelper.TryHandleRightClickToggle(
                Mouse.current,
                rightClickTogglesRadialMenu,
                ignoreRightClickWhenPointerOverUi,
                CanProcessRightClickToggle,
                IsPointerOverUi,
                TryToggleBuildingRadialMenu);

            PublishBuildUiStateIfChanged();
        }

        private void PublishBuildUiStateIfChanged()
        {
            var currentBuildUiActive = IsBuildUiActive;
            if (currentBuildUiActive == _wasBuildUiActive) return;

            _wasBuildUiActive = currentBuildUiActive;
            Zombera.Core.CoreEventBus.PublishGlobal(new BuildModeChangedEvent(currentBuildUiActive, this));
        }

        private void HandleLiveBuildShortcuts()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            HandleHoldRotateShortcut(keyboard);
            HandleRealtimeHeightShortcut(keyboard);
        }

        private void HandleHoldRotateShortcut(Keyboard keyboard)
        {
            _nextRotateKeyRepeatAt = EasyBuildBridgeShortcutHelper.ResolveNextRotateRepeatAt(
                enableHoldRotatePreview,
                keyboard,
                holdRotatePreviewKey,
                holdRotateRepeatSeconds,
                _nextRotateKeyRepeatAt,
                Time.unscaledTime,
                TryRotatePreviewWithFallback);
        }

        private void HandleRealtimeHeightShortcut(Keyboard keyboard)
        {
            var keyCfg = new HeightKeyConfig(
                heightUpPrimaryKey,
                heightUpAlternateKey,
                heightUpNumpadKey,
                heightDownPrimaryKey,
                heightDownAlternateKey,
                heightDownNumpadKey,
                realtimeHeightStep,
                realtimeHeightRepeatSeconds);
            _nextHeightKeyRepeatAt = EasyBuildBridgeShortcutHelper.ResolveNextHeightRepeatAt(
                enableRealtimeHeightHotkeys,
                keyboard,
                keyCfg,
                _nextHeightKeyRepeatAt,
                Time.unscaledTime,
                TryAdjustPlacementHeightWithFallback);
        }

        private bool TryRotatePreviewWithFallback()
        {
            var rotated = TryRotatePreview(1);
            if (!rotated && TrySetPlacementMode())
            {
                EnsureSelectedPart();
                rotated = TryRotatePreview(1);
            }

            return rotated;
        }

        private bool TryAdjustPlacementHeightWithFallback(float delta)
        {
            var adjusted = TryAdjustPlacementHeight(delta);
            if (!adjusted)
            {
                EnsureSelectedPart();
                adjusted = TryAdjustPlacementHeight(delta);
            }

            return adjusted;
        }

        private bool TryRotatePreview(int direction)
        {
            if (direction == 0) return false;

            var controller = ResolveBuildingControllerInstance();
            if (controller == null) return false;

            EnsureSelectedPart();

            if (TryInvokeMethodExact(controller, "RotateAction", direction)
                || TryInvokeMethodExact(controller, "OnRotateButton", direction)
                || TryInvokeMethodExact(controller, "OnRotateAction", direction))
            {
                if (Time.unscaledTime >= _nextRotateShortcutLogAt)
                {
                    _nextRotateShortcutLogAt = Time.unscaledTime + 0.35f;
                    BridgeLog($"Live rotate step applied: direction={direction}");
                }

                return true;
            }

            return false;
        }

        private static bool TryInvokeMethodExact(object instance, string methodName, params object[] arguments)
        {
            return EasyBuildBridgeReflectionHelper.TryInvokeMethodExact(instance, methodName, arguments);
        }

        private void LateUpdate()
        {
            if (!IsOwnerInputControllerActive()) return;

            if (!_manualBuildModeActive && !_blockBuildToggleThisFrame) return;

            EnsureRadialMenuClosed();
        }

        private void TryRefreshMousePlacementWhileMenuOpen()
        {
            if (!TryResolveRadialMenu(out var radialMenu)) return;
            ResolveCursorPlacementBinder();
            EasyBuildBridgeCursorPlacementHelper.ForceMousePlacementRefreshIfMenuOpen(
                radialMenu,
                _cachedCursorPlacementBinder);
        }

        private void ResolveCursorPlacementBinder()
        {
            EasyBuildBridgeCursorPlacementHelper.ResolveCursorPlacementBinder(
                this,
                ref _cachedCursorPlacementBinder);
        }

        private bool IsOwnerInputControllerActive()
        {
            if (_cachedOwnerInputController == null)
            {
                _cachedOwnerInputController = GetComponent<PlayerInputController>();
                if (_cachedOwnerInputController == null)
                    _cachedOwnerInputController = GetComponentInParent<PlayerInputController>();
            }

            // Allow operation in scenes/tools that intentionally use the bridge without a player input controller.
            return _cachedOwnerInputController == null || _cachedOwnerInputController.isActiveAndEnabled;
        }

        private bool TryHandleKeyboardToggleInput()
        {
            var handled = EasyBuildBridgeInputFrameHelper.TryHandleKeyboardBuildToggle(
                Keyboard.current,
                toggleRadialMenuWithKeyboard,
                toggleRadialMenuKey,
                _blockBuildToggleThisFrame,
                _manualBuildModeActive,
                EnsureRadialMenuClosed,
                EnterManualBuildModeFromToggle,
                ExitManualBuildModeFromToggle,
                out var nextManualBuildModeActive);

            if (handled)
                _manualBuildModeActive = nextManualBuildModeActive;

            return handled;
        }

        private void EnterManualBuildModeFromToggle()
        {
            EnsurePlacementViewReady();
            EnsureSelectedPart();

            var hudIndex = _currentHudSelectionIndex >= 0 ? _currentHudSelectionIndex : 0;
            if (!TryActivateHudItem(hudIndex))
            {
                SetPlacementMode();
                ForceMousePlacementRefresh();
            }
        }

        private void ExitManualBuildModeFromToggle()
        {
            EnsureRadialMenuClosed();
            _ = TrySetControllerMode("None");
        }

        private void EnsurePlacementViewReady()
        {
            ResolveCursorPlacementBinder();
            _cachedCursorPlacementBinder?.EnsurePlacementViewReady();
        }

        private void ClearBuildPlacementPreview()
        {
            _ = TrySetControllerMode("None");
            ResolveCursorPlacementBinder();
            _cachedCursorPlacementBinder?.ForceClosePlacementPreview();
            ResolveBuildPlacementController();
            buildPlacementController?.ExitBuildMode();
        }

        private bool CanProcessRightClickToggle()
        {
            if (!requireLocalBuilderModeForRadialToggle) return true;

            ResolveBuildPlacementController();
            return buildPlacementController != null && buildPlacementController.IsBuildModeActive;
        }

        private void ResolveBuildPlacementController()
        {
            if (buildPlacementController != null) return;

            buildPlacementController = GetComponent<BuildPlacementController>();
            if (buildPlacementController != null) return;

            buildPlacementController = GetComponentInParent<BuildPlacementController>();
        }

        private static bool IsPointerOverUi()
        {
            return EasyBuildBridgeInputFrameHelper.IsPointerOverUi();
        }

        private IEnumerator RemoveSampleBuildingGroupArtifactsDeferred()
        {
            yield return WaitForSampleArtifactCleanupDelay();

            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var batchSize = Mathf.Clamp(sampleArtifactCleanupScanBatchSize, 32, 2048);
            for (var i = 0; i < allTransforms.Length; i++)
            {
                if (!TryResolveSampleArtifactRoot(allTransforms[i], out var rootObject)) continue;
                Destroy(rootObject);

                if ((i + 1) % batchSize == 0)
                    yield return null;
            }
        }

        private IEnumerator WaitForSampleArtifactCleanupDelay()
        {
            var cleanupDelay = Mathf.Max(0f, sampleArtifactCleanupDelaySeconds);
            if (cleanupDelay > 0f)
                yield return new WaitForSecondsRealtime(cleanupDelay);
            else
                // Keep this out of the first frame even when delay is zero.
                yield return null;
        }

        private static bool TryResolveSampleArtifactRoot(Transform candidate, out GameObject rootObject)
        {
            return EasyBuildBridgeSampleArtifactHelper.TryResolveSampleArtifactRoot(candidate, out rootObject);
        }

        private static bool IsSampleArtifactChildName(string childName)
        {
            return EasyBuildBridgeSampleArtifactHelper.IsSampleArtifactChildName(childName);
        }

        private bool TryToggleBuildingRadialMenu()
        {
            if (!TryResolveRadialMenu(out var radialMenu)) return false;
            return EasyBuildBridgeMenuStateHelper.TryToggleBuildingRadialMenu(
                radialMenu,
                EnsureSelectedPart,
                ApplyRadialMenuRuntimeOverrides,
                ResolveCursorPlacementBinder,
                ForceMousePlacementRefresh,
                SetPlacementMode,
                ref _manualBuildModeActive);
        }

        private void EnsureRadialMenuClosed()
        {
            if (!TryResolveRadialMenu(out var radialMenu)) return;
            EasyBuildBridgeMenuStateHelper.EnsureRadialMenuClosed(radialMenu);
        }

        private void ForceCloseBuildUi()
        {
            EasyBuildBridgeMenuStateHelper.ForceCloseBuildUi(
                EnsureRadialMenuClosed,
                ClearBuildPlacementPreview,
                ref _manualBuildModeActive,
                ref _currentHudSelectionIndex);
        }

    }
}
