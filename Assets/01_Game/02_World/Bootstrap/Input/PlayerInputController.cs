#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Inventory;
using Zombera.Systems.Digging;
using Zombera.UI;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Routes player input into unit movement/combat and squad-level command dispatch.
    /// </summary>
    public sealed partial class PlayerInputController : MonoBehaviour, Zombera.Core.IPlayerInputController
    {
        bool Zombera.Core.IPlayerInputController.InputEnabled
        {
            get => enabled;
            set => enabled = value;
        }

        private static readonly List<RaycastResult> UiRaycastResults = new(16);
        private static readonly List<PlayerInputController> ActiveControllers = new(4);

        private const float UiEventSystemHealthyRecheckSeconds = 2f;
        private const float UiEventSystemRecoveryRetrySeconds = 0.5f;

        private static PointerEventData _cachedUiPointerEventData;
        private static EventSystem _cachedUiPointerEventDataOwner;
        private static EventSystem _cachedResolvedUiEventSystem;
        private static float _nextUiEventSystemResolveAt;
        private static bool _cachedIsPointerOverUi;
        private static int _lastUiCheckFrame = -1;

        public static IReadOnlyList<PlayerInputController> ActiveInstances => ActiveControllers;

        private ItemPickupInteractor _typedItemPickupInteractor;
        private readonly List<SquadMember> _dragSelectionBuffer = new(24);
        private readonly List<SquadMember> _selectionMergeBuffer = new(24);
        private readonly RaycastHit[] _targetRaycastHits = new RaycastHit[128];
        private readonly RaycastHit[] _targetSpherecastHits = new RaycastHit[128];

        private readonly List<UnitHealth> _visibleTargets = new();
        private readonly List<Unit> _nearbyEnemyBuffer = new();
        private float _combatFacingAssistExpiresAt;
        private Unit _combatFacingAssistTarget;
        private bool _isDragSelectCandidateActive;
        private bool _isDragSelecting;
        private LeftClickMovementPhase _leftClickMovementPhase;
        private float _movementLockExpiresAt;
        private float _nextWeightTrainingRepAt;
        private Unit _pendingEncounterTarget;
        private bool _pendingPostStandMoveRamp;
        private float _postureTransitionTimer;
        private bool _sprintToggleActive;
        private float _standUpTimer;
        private float _suppressAutoEngageUntilAt;
        private bool _enabledCrawlToggleAction;
        private bool _enabledCrouchToggleAction;
        private bool _enabledInteractAction;
        private bool _enabledSquadAttackCommandAction;
        private bool _enabledSquadDefendCommandAction;
        private bool _enabledSquadFollowCommandAction;
        private bool _enabledSquadHoldCommandAction;
        private bool _enabledSquadMoveCommandAction;
        private bool _suppressCombatFacingUntilMoveOrderCompletes;
        private bool _suppressSprintToggleThisFrame;
        private bool _suppressLeftClickMovementUntilRelease;
        [SerializeField] private bool allowMoveCommandToCancelAttack = true;
        [SerializeField] private bool allowMoveWhileHoldingLeftMouse;

        [SerializeField] private float attackScanRadius = 20f;

        /// <summary>Central combat tuning wins when a CombatTuningConfig asset exists.</summary>
        private float EffectiveAttackScanRadius => Zombera.Combat.CombatTuningConfig.AttackScanRadiusOr(attackScanRadius);

        [Tooltip("How often per second the controller scans UnitManager for nearby enemies (auto-engage, dig gating). Results are cached between scans.")]
        [SerializeField] [Range(1f, 30f)] private float nearbyEnemyScansPerSecond = 8f;
        private float _nextNearbyEnemyScanAt;
        private bool _cachedHasNearbyLiveEnemies;
        private float _nextAutoEngageScanAt;
        [SerializeField] [Min(0f)] private float autoEngageEnemyDistanceMeters = 2f;
        [SerializeField] [Min(0f)] private float autoEngageSuppressAfterPlayerMoveSeconds = 1f;
        [SerializeField] [Min(0f)] private float bowAimInitiationMaxSpeedMetersPerSecond = 0.08f;
        [SerializeField] [Min(0.1f)] private float bowChargeDurationAtLevel100Seconds = 1f;
        [SerializeField] [Min(0.1f)] private float bowChargeDurationAtLevel1Seconds = 5f;
        [SerializeField] [Min(0f)] private float bowDrawDurationSeconds = 0.16f;
        [SerializeField] [Min(1f)] private float bowMaximumAttackRangeMeters = 45f;
        [SerializeField] [Min(0f)] private float bowMeleeFallbackDistanceMeters = 2f;
        [SerializeField] [Min(0f)] private float bowPreferredMaxDistanceMeters = 36f;

        [SerializeField] [Min(0f)] private float bowPreferredMinDistanceMeters = 18f;
        [SerializeField] [Min(0f)] private float bowRepositionIntervalSeconds = 0.25f;
        [SerializeField] [Min(0f)] private float bowRepositionStepMeters = 1.6f;
        [SerializeField] [Min(0f)] private float bowShotCadencePaddingSeconds = 0.03f;
        [SerializeField] private BowShotProgressWorldUI bowShotProgressWorldUI;
        [SerializeField] [Range(0f, 180f)] private float bowStartFacingToleranceDegrees = 12f;

        [Header("Building")] [SerializeField] private EasyBuildCursorPlacementBinder easyBuildCursorPlacementBinder;
        [SerializeField] private bool autoAttachEasyBuildCursorPlacementBinder = true;
        [SerializeField] private EasyBuildRadialMenuInputBridge easyBuildRadialMenuInputBridge;
        [SerializeField] private bool autoAttachEasyBuildRadialMenuInputBridge = true;
        [SerializeField] private BuildPlacementController legacyBuildPlacementController;
        [SerializeField] private bool autoAttachBuildPlacementControllerForHudBuilds = true;
        [SerializeField] [Range(0f, 180f)] private float combatFaceAssistCompleteAngleDegrees = 4f;
        [SerializeField] [Min(0f)] private float combatFaceAssistDurationSeconds = 0.4f;
        [SerializeField] [Min(0f)] private float combatFaceTurnSpeedDegreesPerSecond = 720f;

        [Header("Combat Footwork")] [SerializeField]
        private PlayerCombatFootwork combatFootwork;

        [Header("Gameplay Systems")] [SerializeField]
        private CombatManager combatManager;

        [SerializeField] private CombatSystem combatSystem;

        [Header("Interaction")] [SerializeField]
        private ContainerInteractor containerInteractor;

        [SerializeField] private Key crawlKey = Key.Z;
        [SerializeField] [Min(0f)] private float crawlTransitionLockSeconds = 0.4f;

        [Header("Posture")] [SerializeField] private Key crouchKey = Key.C;
        [SerializeField] [Min(0f)] private float crouchTransitionLockSeconds = 0.35f;

        [Header("Cursor")] [SerializeField] private CursorManager cursorManager;

        [Header("Digging")] [SerializeField] private DiggingSystem diggingSystem;

        [SerializeField] private DigProgressWorldUI digProgressWorldUI;
        [SerializeField] private DoorInteractor doorInteractor;
        [SerializeField] private Color dragSelectionBorderColor = new(0.30f, 0.78f, 1f, 0.95f);
        [SerializeField] [Min(1f)] private float dragSelectionBorderThickness = 2f;
        [SerializeField] private Color dragSelectionFillColor = new(0.16f, 0.55f, 0.82f, 0.16f);
        [SerializeField] [Min(0f)] private float dragSelectionScreenPaddingPixels = 8f;

        [SerializeField] [Min(4f)] private float dragSelectionThresholdPixels = 16f;

        [Header("Ranged Combat (Bow)")] [SerializeField]
        private bool enableBowRangedCombatState = true;

        [Header("Squad Selection")] [SerializeField]
        private bool enableDragBoxSquadSelection = true;

        [SerializeField] private bool enableClickSquadSelection = true;
        [SerializeField] private bool allowAdditiveSelectionModifier = true;

        [Header("Sprinting")] [SerializeField] private bool enableSprinting = true;

        [Header("Strength Training")] [SerializeField]
        private bool enableWeightTrainingHotkey = true;

        [SerializeField] private CombatEncounterManager encounterManager;
        [SerializeField] [Range(0f, 180f)] private float encounterStartFacingToleranceDegrees = 12f;

        [SerializeField] private LayerMask groundMask;
        [SerializeField] private bool logMovementGroundingDiagnostics;
        [SerializeField] private Key interactKey = Key.None;
        [SerializeField] private bool issueSquadMoveOnLeftClick = true;

        [Header("Input Actions (Optional)")] [SerializeField]
        private InputActionReference crouchToggleAction;

        [SerializeField] private InputActionReference crawlToggleAction;
        [SerializeField] private InputActionReference interactAction;
        [SerializeField] private InputActionReference squadMoveCommandAction;
        [SerializeField] private InputActionReference squadAttackCommandAction;
        [SerializeField] private InputActionReference squadHoldCommandAction;
        [SerializeField] private InputActionReference squadFollowCommandAction;
        [SerializeField] private InputActionReference squadDefendCommandAction;

        [SerializeField] private MonoBehaviour itemPickupInteractor;
        [SerializeField] private bool lockMovementDuringPostureTransitions = true;
        [SerializeField] private bool maintainBowPreferredStandoffDistance;
        [SerializeField] [Min(0f)] private float minimumAttackMovementLockSeconds = 0.65f;
        [SerializeField] private PlayerAnimationController playerAnimationController;

        [Header("Player Unit")] [SerializeField]
        private Unit playerUnit;

        [SerializeField] private bool playFacingTurnAnimationOnHostileClick;

        [SerializeField] [Min(0f)] private float postAttackMovementLockSeconds = 0.2f;
        [SerializeField] [Min(0f)] private float postStandJogBuildUpSeconds = 0.3f;
        [SerializeField] [Range(0.05f, 1f)] private float postStandJogStartSpeedMultiplier = 0.35f;
        [SerializeField] [Range(0.1f, 1f)] private float preferredEncounterStartDistanceFactor = 0.5f;

        [SerializeField] private bool requireHeavyCarryForWeightTraining = true;

        [Header("Combat Targeting")] [SerializeField]
        private bool requireHostileTargetsForCursorAndClick = true;

        [SerializeField] private bool requireNoCombatTarget = true;
        [SerializeField] private bool rightClickDigEnabled = true;

        [SerializeField] private bool sprintToggleStartsEnabled;
        [SerializeField] private SquadManager squadManager;
        [SerializeField] private SelectionBoxUI selectionBoxUi;
        [SerializeField] private PlayerControlModeCoordinator controlModeCoordinator;
        [SerializeField] [Min(0f)] private float standUpDurationSeconds = 0.5f;
        [SerializeField] [Min(0f)] private float standUpFromCrawlLockSeconds = 0.55f;
        [SerializeField] [Min(0f)] private float standUpFromCrouchLockSeconds = 0.45f;

        [SerializeField] private bool suspendGameplayInputWhileBuilding = true;
        [SerializeField] private LayerMask targetableMask = ~0;

        [SerializeField]
        private QueryTriggerInteraction targetingQueryTriggerInteraction = QueryTriggerInteraction.Collide;

        [SerializeField] [Min(1f)] private float targetingRayDistance = 1000f;
        [SerializeField] private UnitCombat unitCombat;

        [SerializeField] private UnitController unitController;
        [SerializeField] private WeaponSystem weaponSystem;
        [SerializeField] [Min(0f)] private float weightTrainingRepCooldownSeconds = 0.6f;


        [Header("Input Settings")] [SerializeField]
        private Camera worldCamera;

        [SerializeField] [Min(0f)] private float zombieHoverAssistRadius = 0.35f;

        [Header("RTS Input Interop")] [SerializeField]
        private bool externalMouseInputOverride;

        private void Awake()
{
            ResolveCoreReferences();
            ResolveInteractionReferences();
            EnsureRuntimeSupportComponents();
            ConfigureWorldUiBindings();
            _sprintToggleActive = sprintToggleStartsEnabled;
        }

        private void Update()
        {
            if (!CanProcessGameplayUpdate()) return;

            var pointerOverUi = IsPointerOverUi();
            PublishCurrentControlMode(pointerOverUi);
            var legacyMouseInputActive = !externalMouseInputOverride;
            var consumeRightClickCombat = legacyMouseInputActive
                && ProcessLegacyInputBeforeCombat(pointerOverUi);

            CancelDigIfMoving();

            if (legacyMouseInputActive) ProcessLegacyCombatInput(pointerOverUi, consumeRightClickCombat);

            TickBowRangedCombatState();
            HandleWeightTrainingInput(pointerOverUi);
            HandleSprintInput();
            HandlePostureInput();
            TickPostureTransition();
            HandleInteractInput();

            if (legacyMouseInputActive) HandleSquadCommandInput();
        }

        private bool ProcessLegacyInputBeforeCombat(bool pointerOverUi)
        {
            HandleSquadSelectionInput(pointerOverUi);
            var hasNearbyEnemies = HasNearbyLiveEnemies();
            var consumeRightClickCombat = HandleDigInput(hasNearbyEnemies, pointerOverUi);
            HandleMovementInput(pointerOverUi);
            TryAutoStartEncounterFromNearbyThreat();
            return consumeRightClickCombat;
        }

        private void ProcessLegacyCombatInput(bool pointerOverUi, bool consumeRightClickCombat)
        {
            UpdateAttackHoverCursor(pointerOverUi);
            HandleCombatInputIfReady(consumeRightClickCombat, pointerOverUi);
        }

        private void PublishCurrentControlMode(bool pointerOverUi)
        {
            if (controlModeCoordinator == null) return;

            // SelectionManager owns SquadRts mode publication when it is capturing RTS mouse input.
            if (externalMouseInputOverride) return;

            var isBuildMode = suspendGameplayInputWhileBuilding
                              && easyBuildCursorPlacementBinder != null
                              && easyBuildCursorPlacementBinder.IsEasyBuildPlacementModeActive;

            if (isBuildMode)
            {
                controlModeCoordinator.RequestMode(PlayerControlMode.BuildMode, PlayerControlModeSource.PlayerInput);
                return;
            }

            controlModeCoordinator.RequestMode(
                pointerOverUi ? PlayerControlMode.UiBlocked : PlayerControlMode.SingleUnit,
                PlayerControlModeSource.PlayerInput);
        }

        private void OnEnable()
        {
            if (!ActiveControllers.Contains(this)) ActiveControllers.Add(this);

            EnableRebindableInputActions();
            EnableOptionalInputActions();

            if (easyBuildRadialMenuInputBridge != null && !easyBuildRadialMenuInputBridge.enabled)
                easyBuildRadialMenuInputBridge.enabled = true;

            if (easyBuildCursorPlacementBinder != null && !easyBuildCursorPlacementBinder.enabled)
                easyBuildCursorPlacementBinder.enabled = true;
        }

        private void OnDisable()
        {
            _ = ActiveControllers.Remove(this);

            DisableOptionalInputActions();
            DisableRebindableInputActions();

            if (easyBuildRadialMenuInputBridge != null)
            {
                easyBuildRadialMenuInputBridge.TryCancelBuildUi();
                easyBuildRadialMenuInputBridge.enabled = false;
            }

            if (easyBuildCursorPlacementBinder != null)
                easyBuildCursorPlacementBinder.enabled = false;

            if (selectionBoxUi != null) selectionBoxUi.Hide();

            _suppressLeftClickMovementUntilRelease = false;
            _leftClickMovementPhase = LeftClickMovementPhase.Idle;
            _movementLockExpiresAt = 0f;
            _combatFacingAssistTarget = null;
            _combatFacingAssistExpiresAt = 0f;
            _suppressAutoEngageUntilAt = 0f;
            _suppressCombatFacingUntilMoveOrderCompletes = false;
            _isDragSelectCandidateActive = false;
            _isDragSelecting = false;
            _sprintToggleActive = false;
            _postureTransitionTimer = 0f;
            _pendingPostStandMoveRamp = false;
            _dragSelectionBuffer.Clear();
            _selectionMergeBuffer.Clear();
            ClearBowRangedCombatState(true);
            unitController?.SetSprintActive(false);
        }



        private bool CanProcessGameplayUpdate()
        {
            if (unitController == null) return false;

            if (playerUnit != null && !playerUnit.IsAlive) return false;

            ApplyMovementLockStops();
            UpdateLeftClickStateTracking();

            if (ShouldSuspendForBuildMode()) return false;

            TickMoveOrderFacingSuppression();
            TickCombatFacingAssist();
            TickPendingEncounterJoin();
            return true;
        }

        private void ApplyMovementLockStops()
        {
            if (IsMovementTemporarilyLocked())
            {
                _pendingEncounterTarget = null;
                unitController?.Stop();
            }

            if (IsPostureTransitionMovementLocked()) unitController?.Stop();
        }

        private void UpdateLeftClickStateTracking()
        {
            if (Mouse.current == null) return;

            if (_suppressLeftClickMovementUntilRelease && !Mouse.current.leftButton.isPressed)
                _suppressLeftClickMovementUntilRelease = false;

            if (_leftClickMovementPhase == LeftClickMovementPhase.WorldPressActive
                && !Mouse.current.leftButton.isPressed
                && !Mouse.current.leftButton.wasReleasedThisFrame)
                _leftClickMovementPhase = LeftClickMovementPhase.Idle;
        }

        private bool ShouldSuspendForBuildMode()
        {
            if (!suspendGameplayInputWhileBuilding
                || easyBuildCursorPlacementBinder == null
                || !easyBuildCursorPlacementBinder.IsEasyBuildPlacementModeActive)
                return false;

            if (diggingSystem != null && diggingSystem.IsDigging) diggingSystem.CancelDig();

            _pendingEncounterTarget = null;
            _combatFacingAssistTarget = null;
            cursorManager?.ForceDefaultCursor();
            return true;
        }




    }
}
