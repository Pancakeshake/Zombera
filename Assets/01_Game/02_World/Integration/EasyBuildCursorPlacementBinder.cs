#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

#endregion

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Easy Build's <see cref="ThirdPersonBuildingView" /> and <see cref="FirstPersonBuildingView" /> cast placement rays
    ///     along the camera forward axis (character look direction), not the mouse cursor. That makes previews feel
    ///     "disconnected" from the cursor. This component registers <see cref="OrbitalBuildingView" /> (screen-point ray)
    ///     and switches the active view so radial-menu placement follows the mouse on the ground.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class EasyBuildCursorPlacementBinder : MonoBehaviour
    {
        private const string BuildingControllerTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Controllers.BuildingController";
        private const string BuildingManagerTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Managers.BuildingManager";
        private const string BuildingPartRegistryTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Parts.BuildingPartRegistry";
        private const string BuildingPartStateTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Parts.BuildingPart+BuildingState";
        private const string BuildingRadialMenuTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.UI.BuildingMenus.Implementations.BuildingRadialMenuUI";
        private const string BuildingSelectionActionTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.UI.BuildingMenus.Implementations.Actions.BuildingSelectionAction";

        private const string BuildingViewTypeEnumName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Controllers.Views.Abstracts.BuildingViewType";

        private const string BuildingViewTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Controllers.Views.Abstracts.BuildingView";
        private const string BuildingModeEnumTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Controllers.States.BuildingMode";

        private const string OrbitalViewTypeName =
            "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Controllers.Views.Implementations.OrbitalBuildingView";

        [SerializeField]
        [Tooltip("When false, leaves BuildingController's configured active view unchanged.")]
        private bool useCursorRayPlacement = true;
        [SerializeField]
        [Tooltip("Re-assert Orbital view while Easy Build mode is active (radial flow can switch it back).")]
        private bool enforceCursorViewDuringActiveBuildMode = true;
        [SerializeField] [Range(0.05f, 1f)]
        [Tooltip("How often to re-assert Orbital view while build mode stays active.")]
        private float enforceCursorViewIntervalSeconds = 0.2f;
        [SerializeField]
        [Tooltip("Repair radial menu slots that reference missing parts so all slot entries are valid/selectable.")]
        private bool ensureAllRadialSlotsHaveValidParts = true;
        [SerializeField]
        [Tooltip("If an alternate EasyBuild keybinding toggles build mode, force-close it. Keeps B/radial as the only entry path.")]
        private bool blockAlternateKeyboardBuildToggle = true;
        [SerializeField]
        private Key blockedKeyboardBuildToggleKey = Key.E;

        [SerializeField] private MonoBehaviour buildingController;
        [SerializeField]
        [Tooltip("Optional local builder mode source. If assigned (or auto-resolved), mouse-ray placement is only enforced while this controller is in build mode.")]
        private BuildPlacementController buildPlacementController;
        [SerializeField]
        [Tooltip("Require local legacy builder mode to be active before forcing EasyBuild Orbital mouse-ray placement.")]
        private bool requireLocalBuilderModeForMouseRayPlacement;

        [SerializeField]
        [Tooltip("Optional; defaults to Camera.main after spawn wiring.")]
        private Camera gameplayCamera;
        [SerializeField]
        [Tooltip("Keeps Easy Build preview meshes from sinking below terrain when using radial menu placement.")]
        private bool keepPreviewAboveGround = true;
        [SerializeField] [Range(0.05f, 0.5f)]
        [Tooltip("How often to run preview grounding repairs while build mode is active.")]
        private float previewFixIntervalSeconds = 0.15f;
        [SerializeField] [Min(0f)] private float previewGroundClearance = 0.01f;
        [SerializeField] private LayerMask previewGroundFallbackMask = ~0;
        [SerializeField]
        [Tooltip("When enabled, foundation previews auto-snap their vertical placement to terrain-driven step heights.")]
        private bool autoSnapFoundationHeights = true;
        [SerializeField] [Min(0.01f)]
        [Tooltip("Vertical step size used for terrain-based foundation height snapping.")]
        private float foundationHeightStepMeters = 1f;
        [SerializeField] [Min(0f)]
        [Tooltip("Probe radius used to inherit height from nearby placed foundations so connected bases stay level.")]
        private float foundationNeighborProbeRadius = 3.5f;
        [SerializeField]
        [Tooltip("Physics mask used to search nearby placed foundations for height inheritance.")]
        private LayerMask foundationNeighborProbeMask = ~0;
        [SerializeField] private bool forceGroundingOnPreviewSettings = true;
        [SerializeField] private bool clampNegativePreviewYOffset = true;
        [SerializeField] private bool expandGroundingMaskToCommonGroundLayers = true;
        [SerializeField]
        [Tooltip("Emit focused diagnostics for selected and preview parts matching the filter text.")]
        private bool enablePlacementDiagnosticsLogs;
        [SerializeField]
        [Tooltip("Disable placement diagnostics automatically in play mode to avoid startup hitching.")]
        private bool disablePlacementDiagnosticsInPlayMode = true;
        [SerializeField] private string placementDiagnosticsPartFilter = "barrel";
        [SerializeField] [Min(0.1f)]
        private float placementDiagnosticsRepeatIntervalSeconds = 0.4f;
        [SerializeField]
        [Tooltip("Emit low-frequency preview scan diagnostics so placement-stage flow can be traced even without Y adjustment events.")]
        private bool enablePreviewScanDiagnosticsLogs;
        [SerializeField] [Min(0.1f)]
        private float previewScanDiagnosticsIntervalSeconds = 0.75f;

        private bool _radialSlotsValidated;
        private Type _cachedBuildingManagerType;
        private object _cachedBuildingManagerInstance;
        private Type _cachedBuildingPartStateType;
        private Type _cachedBuildingControllerType;
        private Type _cachedBuildingModeEnumType;
        private Type _cachedOrbitalViewType;
        private Type _cachedBuildingViewType;
        private Type _cachedBuildingViewEnumType;
        private Type _cachedRadialMenuType;
        private Type _cachedSelectionActionType;
        private object _cachedPlacementStateEnum;
        private readonly Dictionary<string, int> _radialHudIndexByPartReference =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _nextPlacementDiagnosticAtByKey =
            new(StringComparer.OrdinalIgnoreCase);
        private const int MaxRadialHudSlotIndex = 63;
        private readonly Collider[] _foundationNeighborProbeHits = new Collider[64];
        private float _nextCursorViewEnforceAt;
        private float _nextPreviewFixAt;
        private float _nextPreviewScanDiagnosticAt;
        private bool _wasEasyBuildModeActive;

        public bool IsEasyBuildPlacementModeActive => IsBuildModeCurrentlyActive();

        public int GetEasyBuildSelectedHudBoxIndex()
        {
            if (!IsBuildModeCurrentlyActive()) return -1;

            EnsureRadialHudIndexMap();

            var selectedRadialPartReference = TryResolveSelectedRadialPartReference();
            if (!string.IsNullOrWhiteSpace(selectedRadialPartReference)
                && _radialHudIndexByPartReference.TryGetValue(selectedRadialPartReference, out var radialMappedIndex))
            {
                var clampedIndex = Mathf.Clamp(radialMappedIndex, 0, MaxRadialHudSlotIndex);
                LogPlacementDiagnostic("HudIndex.radialMap", selectedRadialPartReference, clampedIndex,
                    $"radialMappedIndex={radialMappedIndex}");
                return clampedIndex;
            }

            var selectedPartReference = TryResolveSelectedPartReferenceFromController();
            if (!string.IsNullOrWhiteSpace(selectedPartReference)
                && _radialHudIndexByPartReference.TryGetValue(selectedPartReference, out var mappedIndex))
            {
                var clampedIndex = Mathf.Clamp(mappedIndex, 0, MaxRadialHudSlotIndex);
                LogPlacementDiagnostic("HudIndex.controllerMap", selectedPartReference, clampedIndex,
                    $"mappedIndex={mappedIndex}");
                return clampedIndex;
            }

            var radialSlotIndex = TryResolveRadialSelectedSlotIndex();
            if (radialSlotIndex >= 0)
            {
                var clampedIndex = Mathf.Clamp(radialSlotIndex, 0, MaxRadialHudSlotIndex);
                LogPlacementDiagnostic("HudIndex.radialSlot", selectedRadialPartReference ?? selectedPartReference,
                    clampedIndex, $"radialSlotIndex={radialSlotIndex}");
                return clampedIndex;
            }

            var fallbackIndex = MapPartReferenceToHudBoxIndex(selectedPartReference);
            if (fallbackIndex >= 0)
                LogPlacementDiagnostic("HudIndex.legacyMap", selectedPartReference, fallbackIndex,
                    "using legacy keyword mapping");

            return fallbackIndex;
        }

        public void ForceMousePlacementRefresh()
        {
            EnsurePlacementViewReady();
        }


        public void SetGameplayCamera(Camera camera)
        {
            gameplayCamera = camera;
            if (!isActiveAndEnabled || !useCursorRayPlacement) return;

            EnsurePlacementViewReady();
        }

    }
}
