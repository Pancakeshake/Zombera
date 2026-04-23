using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
using Zombera.BuildingSystem;
using Zombera.Inventory;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Systems.Digging;
using Zombera.UI;

namespace Zombera.Systems
{
    /// <summary>
    /// Routes player input into unit movement/combat and squad-level command dispatch.
    /// </summary>
    public sealed class PlayerInputController : MonoBehaviour
    {
        [Header("Player Unit")]
        [SerializeField] private Unit playerUnit;
        [SerializeField] private UnitController unitController;
        [SerializeField] private UnitCombat unitCombat;
        [SerializeField] private PlayerAnimationController playerAnimationController;
        [SerializeField] private WeaponSystem weaponSystem;

        [Header("Gameplay Systems")]
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private CombatSystem combatSystem;
        [SerializeField] private SquadManager squadManager;
        [SerializeField] private CombatEncounterManager encounterManager;

        [Header("Input Settings")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private bool allowMoveWhileHoldingLeftMouse = false;

        [Header("Squad Selection")]
        [SerializeField] private bool enableDragBoxSquadSelection = true;
        [SerializeField, Min(4f)] private float dragSelectionThresholdPixels = 16f;
        [SerializeField, Min(1f)] private float dragSelectionBorderThickness = 2f;
        [SerializeField] private Color dragSelectionFillColor = new Color(0.16f, 0.55f, 0.82f, 0.16f);
        [SerializeField] private Color dragSelectionBorderColor = new Color(0.30f, 0.78f, 1f, 0.95f);
        [SerializeField, Min(0f)] private float dragSelectionScreenPaddingPixels = 8f;

        [Header("Combat Targeting")]
        [SerializeField] private bool requireHostileTargetsForCursorAndClick = true;
        [SerializeField, Min(0f)] private float zombieHoverAssistRadius = 0.35f;
        [SerializeField, Min(1f)] private float targetingRayDistance = 1000f;
        [SerializeField] private LayerMask targetableMask = ~0;
        [SerializeField] private QueryTriggerInteraction targetingQueryTriggerInteraction = QueryTriggerInteraction.Collide;
        [SerializeField, Min(0f)] private float postAttackMovementLockSeconds = 0.2f;
        [SerializeField, Min(0f)] private float minimumAttackMovementLockSeconds = 0.65f;
        [SerializeField] private bool allowMoveCommandToCancelAttack = true;
        [SerializeField, Min(0f)] private float combatFaceTurnSpeedDegreesPerSecond = 720f;
        [SerializeField, Min(0f)] private float combatFaceAssistDurationSeconds = 0.4f;
        [SerializeField, Range(0f, 180f)] private float encounterStartFacingToleranceDegrees = 12f;
        [SerializeField, Range(0f, 180f)] private float combatFaceAssistCompleteAngleDegrees = 4f;
        [SerializeField, Range(0.1f, 1f)] private float preferredEncounterStartDistanceFactor = 0.5f;
        [SerializeField] private bool playFacingTurnAnimationOnHostileClick = false;
        [SerializeField, Min(0f)] private float autoEngageEnemyDistanceMeters = 2f;
        [SerializeField, Min(0f)] private float autoEngageSuppressAfterPlayerMoveSeconds = 1f;

        [Header("Ranged Combat (Bow)")]
        [SerializeField] private bool enableBowRangedCombatState = true;
        [SerializeField, Min(0f)] private float bowPreferredMinDistanceMeters = 18f;
        [SerializeField, Min(0f)] private float bowPreferredMaxDistanceMeters = 36f;
        [SerializeField, Min(0f)] private float bowMeleeFallbackDistanceMeters = 2f;
        [SerializeField, Min(0f)] private float bowRepositionStepMeters = 1.6f;
        [SerializeField, Min(0f)] private float bowRepositionIntervalSeconds = 0.25f;
        [SerializeField, Min(0f)] private float bowDrawDurationSeconds = 0.16f;
        [SerializeField, Min(0f)] private float bowShotCadencePaddingSeconds = 0.03f;
        [SerializeField, Min(0.1f)] private float bowChargeDurationAtLevel1Seconds = 5f;
        [SerializeField, Min(0.1f)] private float bowChargeDurationAtLevel100Seconds = 1f;
        [SerializeField, Min(0f)] private float bowAimInitiationMaxSpeedMetersPerSecond = 0.08f;
        [SerializeField, Range(0f, 180f)] private float bowStartFacingToleranceDegrees = 12f;
        [SerializeField, Min(1f)] private float bowMaximumAttackRangeMeters = 45f;
        [SerializeField] private bool maintainBowPreferredStandoffDistance = false;

        [Header("Cursor")]
        [SerializeField] private CursorManager cursorManager;

        [Header("Building")]
        [SerializeField] private BuildPlacementController buildPlacementController;
        [SerializeField] private bool suspendGameplayInputWhileBuilding = true;

        [Header("Digging")]
        [SerializeField] private DiggingSystem diggingSystem;
        [SerializeField] private DigProgressWorldUI digProgressWorldUI;
        [SerializeField] private BowShotProgressWorldUI bowShotProgressWorldUI;
        [SerializeField] private bool rightClickDigEnabled = true;
        [SerializeField] private bool requireNoCombatTarget = true;

        [Header("Strength Training")]
        [SerializeField] private bool enableWeightTrainingHotkey = true;
        [SerializeField] private bool requireHeavyCarryForWeightTraining = true;
        [SerializeField, Min(0f)] private float weightTrainingRepCooldownSeconds = 0.6f;

        [Header("Sprinting")]
        [SerializeField] private bool enableSprinting = true;
        [SerializeField] private bool sprintToggleStartsEnabled;

        [Header("Combat Footwork")]
        [SerializeField] private PlayerCombatFootwork combatFootwork;

        [Header("Posture")]
        [SerializeField] private Key crouchKey = Key.C;
        [SerializeField] private Key crawlKey  = Key.Z;
        [SerializeField, Min(0f)] private float standUpDurationSeconds = 0.5f;
        [SerializeField] private bool lockMovementDuringPostureTransitions = true;
        [SerializeField, Min(0f)] private float crouchTransitionLockSeconds = 0.35f;
        [SerializeField, Min(0f)] private float crawlTransitionLockSeconds = 0.4f;
        [SerializeField, Min(0f)] private float standUpFromCrouchLockSeconds = 0.45f;
        [SerializeField, Min(0f)] private float standUpFromCrawlLockSeconds = 0.55f;
        [SerializeField, Min(0f)] private float postStandJogBuildUpSeconds = 0.3f;
        [SerializeField, Range(0.05f, 1f)] private float postStandJogStartSpeedMultiplier = 0.35f;

        [Header("Interaction")]
        [SerializeField] private ContainerInteractor containerInteractor;
        [SerializeField] private MonoBehaviour itemPickupInteractor;
        [SerializeField] private Zombera.BuildingSystem.DoorInteractor doorInteractor;
        [SerializeField] private Key interactKey = Key.E;

        public void SetWorldCamera(Camera camera)
        {
            worldCamera = camera;
            if (digProgressWorldUI != null)
            {
                digProgressWorldUI.SetWorldCamera(camera);
            }
            if (bowShotProgressWorldUI != null)
            {
                bowShotProgressWorldUI.SetWorldCamera(camera);
            }
            if (cursorManager != null)
            {
                cursorManager.SetCamera(camera);
            }

            if (buildPlacementController != null)
            {
                buildPlacementController.SetWorldCamera(camera);
            }
        }

        public void InjectSystems(CombatManager manager, CombatSystem system, SquadManager squad)
        {
            if (manager != null) combatManager = manager;
            if (system != null) combatSystem = system;
            if (squad != null) squadManager = squad;

            if (encounterManager == null)
            {
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : Object.FindFirstObjectByType<CombatEncounterManager>();
            }
        }
        [SerializeField] private float attackScanRadius = 20f;
        [SerializeField] private bool issueSquadMoveOnRightClick = true;

        private readonly List<UnitHealth> visibleTargets = new List<UnitHealth>();
        private static readonly List<UnityEngine.EventSystems.RaycastResult> UiRaycastResults = new List<UnityEngine.EventSystems.RaycastResult>(16);
        private readonly RaycastHit[] targetRaycastHits = new RaycastHit[128];
        private readonly RaycastHit[] targetSpherecastHits = new RaycastHit[128];
        private Unit pendingEncounterTarget;
        private Unit combatFacingAssistTarget;
        private bool suppressLeftClickMovementUntilRelease;
        private float movementLockExpiresAt;
        private float combatFacingAssistExpiresAt;
        private float nextWeightTrainingRepAt;
        private float suppressAutoEngageUntilAt;
        private bool suppressCombatFacingUntilMoveOrderCompletes;
        private float _standUpTimer;
        private float _postureTransitionTimer;
        private bool _pendingPostStandMoveRamp;
        private bool _sprintToggleActive;
        private Unit activeBowRangedTarget;
        private float nextBowShotAt;
        private float nextBowRepositionAt;
        private bool bowShotQueued;
        private float bowQueuedReleaseAt;
        private float bowQueuedDurationSeconds;
        private bool bowQueuedRapidFire;
        private bool isDragSelectCandidateActive;
        private bool isDragSelecting;
        private bool suppressNextLeftClickMoveRelease;
        private bool leftWorldClickBegan;
        private Vector2 dragSelectStartScreen;
        private Vector2 dragSelectCurrentScreen;
        private readonly List<SquadMember> dragSelectionBuffer = new List<SquadMember>(24);
        private const string ItemPickupInteractorTypeName = "Zombera.Inventory.ItemPickupInteractor";
        private static Type cachedItemPickupInteractorType;
        private static bool attemptedResolveItemPickupInteractorType;
        private static MethodInfo cachedItemPickupInteractMethod;

        public bool IsRangedCombatActive => activeBowRangedTarget != null && IsBowRangedModeActive();
        public bool IsBowShotCharging => bowShotQueued;
        public float BowShotChargeProgress01
        {
            get
            {
                if (!bowShotQueued)
                {
                    return 0f;
                }

                float duration = Mathf.Max(0.001f, bowQueuedDurationSeconds);
                float startAt = bowQueuedReleaseAt - duration;
                float elapsed = Mathf.Max(0f, Time.time - startAt);
                return Mathf.Clamp01(elapsed / duration);
            }
        }

        private void Awake()
        {
            if (playerUnit == null) playerUnit = GetComponent<Unit>();
            if (unitController == null) unitController = GetComponent<UnitController>();
            if (unitCombat == null) unitCombat = GetComponent<UnitCombat>();
            if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();
            if (playerAnimationController == null) playerAnimationController = GetComponent<PlayerAnimationController>();
            if (playerAnimationController == null) playerAnimationController = gameObject.AddComponent<PlayerAnimationController>();
            if (containerInteractor == null)
            {
                containerInteractor = GetComponent<ContainerInteractor>();
            }

            if (itemPickupInteractor == null)
            {
                EnsureItemPickupInteractorBound();
            }

            if (worldCamera == null) worldCamera = Camera.main;

            if (cursorManager == null) cursorManager = GetComponent<CursorManager>();
            if (cursorManager == null) cursorManager = gameObject.AddComponent<CursorManager>();

            if (diggingSystem == null) diggingSystem = GetComponent<DiggingSystem>();
            if (diggingSystem == null) diggingSystem = gameObject.AddComponent<DiggingSystem>();

            if (digProgressWorldUI == null) digProgressWorldUI = GetComponent<DigProgressWorldUI>();
            if (digProgressWorldUI == null) digProgressWorldUI = gameObject.AddComponent<DigProgressWorldUI>();

            if (bowShotProgressWorldUI == null) bowShotProgressWorldUI = GetComponent<BowShotProgressWorldUI>();
            if (bowShotProgressWorldUI == null) bowShotProgressWorldUI = gameObject.AddComponent<BowShotProgressWorldUI>();

            if (buildPlacementController == null)
            {
                buildPlacementController = GetComponent<BuildPlacementController>();
            }

            if (combatFootwork == null) combatFootwork = GetComponent<PlayerCombatFootwork>();
            if (combatFootwork == null) combatFootwork = gameObject.AddComponent<PlayerCombatFootwork>();

            if (digProgressWorldUI != null)
            {
                digProgressWorldUI.SetTarget(transform);
                digProgressWorldUI.SetDiggingSystem(diggingSystem);
                digProgressWorldUI.SetWorldCamera(worldCamera);
            }

            if (bowShotProgressWorldUI != null)
            {
                bowShotProgressWorldUI.SetTarget(transform);
                bowShotProgressWorldUI.SetInputController(this);
                bowShotProgressWorldUI.SetWorldCamera(worldCamera);
            }

            _sprintToggleActive = sprintToggleStartsEnabled;

        }

        private void OnEnable() { }

        private void OnDisable()
        {
            suppressLeftClickMovementUntilRelease = false;
            suppressNextLeftClickMoveRelease = false;
            leftWorldClickBegan = false;
            movementLockExpiresAt = 0f;
            combatFacingAssistTarget = null;
            combatFacingAssistExpiresAt = 0f;
            suppressAutoEngageUntilAt = 0f;
            suppressCombatFacingUntilMoveOrderCompletes = false;
            isDragSelectCandidateActive = false;
            isDragSelecting = false;
            _sprintToggleActive = false;
            _postureTransitionTimer = 0f;
            _pendingPostStandMoveRamp = false;
            dragSelectionBuffer.Clear();
            ClearBowRangedCombatState(clearAimTarget: true);
            unitController?.SetSprintActive(false);
        }

        private void Start() { }

        private void Update()
        {

            if (unitController == null)
            {
                return;
            }

            if (playerUnit != null && !playerUnit.IsAlive)
            {
                return;
            }

            if (IsMovementTemporarilyLocked())
            {
                pendingEncounterTarget = null;
                unitController?.Stop();
            }

            if (IsPostureTransitionMovementLocked())
            {
                unitController?.Stop();
            }

            if (suppressLeftClickMovementUntilRelease && Mouse.current != null && !Mouse.current.leftButton.isPressed)
            {
                suppressLeftClickMovementUntilRelease = false;
            }

            if (Mouse.current != null && !Mouse.current.leftButton.isPressed && !Mouse.current.leftButton.wasReleasedThisFrame)
            {
                leftWorldClickBegan = false;
            }

            if (suspendGameplayInputWhileBuilding && buildPlacementController != null && buildPlacementController.IsBuildModeActive)
            {
                if (diggingSystem != null && diggingSystem.IsDigging)
                {
                    diggingSystem.CancelDig();
                }

                pendingEncounterTarget = null;
                combatFacingAssistTarget = null;
                cursorManager?.ForceDefaultCursor();
                return;
            }

            TickMoveOrderFacingSuppression();
            TickCombatFacingAssist();
            TickPendingEncounterJoin();

            bool pointerOverUi = IsPointerOverUi();
            HandleSquadSelectionInput(pointerOverUi);
            bool hasNearbyEnemies = HasNearbyLiveEnemies();
            bool consumeRightClickCombat = HandleDigInput(hasNearbyEnemies, pointerOverUi);
            HandleMovementInput(pointerOverUi);
            TryAutoStartEncounterFromNearbyThreat();

            if (diggingSystem != null && diggingSystem.IsDigging && unitController.IsMoving)
            {
                diggingSystem.CancelDig();
            }

            if (cursorManager != null)
            {
                bool showAttackCursor = !pointerOverUi && TryGetUnitHealthUnderCursor(out _);
                cursorManager.SetAttackHover(showAttackCursor);
            }

            if (unitCombat != null && playerUnit != null)
            {
                HandleCombatInput(consumeRightClickCombat, pointerOverUi);
            }

            TickBowRangedCombatState();

            HandleWeightTrainingInput(pointerOverUi);

            HandleSprintInput();

            HandlePostureInput();

            TickPostureTransition();

            HandleInteractInput();

            HandleSquadCommandInput();
        }

        private void HandleWeightTrainingInput(bool pointerOverUi)
        {
            if (!enableWeightTrainingHotkey || pointerOverUi || playerUnit == null || playerUnit.Stats == null)
            {
                return;
            }

            if (Keyboard.current == null || !Keyboard.current.tKey.wasPressedThisFrame)
            {
                return;
            }

            if (Time.time < nextWeightTrainingRepAt)
            {
                return;
            }

            if (requireHeavyCarryForWeightTraining)
            {
                UnitInventory inventory = playerUnit.Inventory;
                if (inventory == null || !playerUnit.Stats.IsHeavyCarry(inventory.CarryRatio))
                {
                    return;
                }
            }

            playerUnit.Stats.RecordWeightTrainingRep();
            nextWeightTrainingRepAt = Time.time + Mathf.Max(0f, weightTrainingRepCooldownSeconds);
        }

        private void HandleSprintInput()
        {
            if (unitController == null) return;

            if (!enableSprinting)
            {
                _sprintToggleActive = false;
                unitController.SetSprintActive(false);
                return;
            }

            bool inCombatEncounter = IsPlayerInEncounter() || pendingEncounterTarget != null;
            if (inCombatEncounter)
            {
                _sprintToggleActive = false;
                unitController.SetSprintActive(false);
                return;
            }

            if (Keyboard.current != null)
            {
                bool sprintTogglePressed = Keyboard.current.leftShiftKey.wasPressedThisFrame
                    || Keyboard.current.rightShiftKey.wasPressedThisFrame;

                if (sprintTogglePressed)
                {
                    _sprintToggleActive = !_sprintToggleActive;
                }
            }

            // Block sprinting while in a posture or during stand-up animation.
            bool inPosture = playerUnit?.Stats != null && playerUnit.Stats.CurrentPosture != PostureState.Upright;
            if (inPosture || _standUpTimer > 0f)
            {
                unitController.SetSprintActive(false);
                return;
            }

            bool hasStamina = playerUnit?.Stats == null || playerUnit.Stats.Stamina > 0f;
            if (_sprintToggleActive && !hasStamina)
            {
                _sprintToggleActive = false;
            }

            bool hasMoveIntent = unitController.IsMoving || unitController.HasMoveTarget;
            bool wantsToSprint = _sprintToggleActive && hasMoveIntent && hasStamina;
            unitController.SetSprintActive(wantsToSprint);
        }

        private void HandlePostureInput()
        {
            if (Keyboard.current == null || playerUnit == null) return;

            if (Keyboard.current[crouchKey].wasPressedThisFrame)
            {
                bool nowCrouching = playerUnit.Stats?.CurrentPosture != PostureState.Crouching;
                ApplyPosture(nowCrouching ? PostureState.Crouching : PostureState.Upright);
            }
            else if (Keyboard.current[crawlKey].wasPressedThisFrame)
            {
                bool nowCrawling = playerUnit.Stats?.CurrentPosture != PostureState.Crawling;
                ApplyPosture(nowCrawling ? PostureState.Crawling : PostureState.Upright);
            }
        }

        private void ApplyPosture(PostureState state)
        {
            if (playerUnit?.Stats == null || unitController == null) return;

            PostureState currentState = playerUnit.Stats.CurrentPosture;
            if (currentState == state)
            {
                return;
            }

            if (state != PostureState.Upright)
            {
                // Entering crouch/crawl: cancel sprint immediately and snap to posture speed.
                unitController.SetSprintActive(false);
                float speedMult = playerUnit.Stats.SetPostureState(state);
                unitController.SetPostureSpeedMultiplier(speedMult);
                _standUpTimer = 0f;
                _pendingPostStandMoveRamp = false;

                float transitionLockSeconds = state == PostureState.Crawling
                    ? crawlTransitionLockSeconds
                    : crouchTransitionLockSeconds;
                BeginPostureTransitionMovementLock(transitionLockSeconds);
            }
            else
            {
                // Standing up: update state immediately (detection/XP reflect upright at once)
                // but keep current posture speed until stand-up animation completes.
                playerUnit.Stats.SetPostureState(PostureState.Upright);
                float standUpLockSeconds = currentState == PostureState.Crawling
                    ? standUpFromCrawlLockSeconds
                    : standUpFromCrouchLockSeconds;
                _standUpTimer = Mathf.Max(standUpDurationSeconds, standUpLockSeconds);
                _pendingPostStandMoveRamp = true;
            }

            playerAnimationController?.ApplyPostureState(state);
        }

        private void TickPostureTransition()
        {
            if (_postureTransitionTimer > 0f)
            {
                _postureTransitionTimer -= Time.deltaTime;
                if (_postureTransitionTimer <= 0f)
                {
                    _postureTransitionTimer = 0f;
                }
            }

            if (_standUpTimer <= 0f) return;
            _standUpTimer -= Time.deltaTime;
            if (_standUpTimer <= 0f)
            {
                _standUpTimer = 0f;
                unitController?.SetPostureSpeedMultiplier(1f);
            }
        }

        private void BeginPostureTransitionMovementLock(float durationSeconds)
        {
            if (!lockMovementDuringPostureTransitions)
            {
                _postureTransitionTimer = 0f;
                return;
            }

            float clampedDuration = Mathf.Max(0f, durationSeconds);
            _postureTransitionTimer = Mathf.Max(_postureTransitionTimer, clampedDuration);
            if (_postureTransitionTimer > 0f)
            {
                unitController?.Stop();
            }
        }

        private bool IsPostureTransitionMovementLocked()
        {
            return lockMovementDuringPostureTransitions
                && (_postureTransitionTimer > 0f || _standUpTimer > 0f);
        }

        private void TryBeginPostStandMoveRamp()
        {
            if (!_pendingPostStandMoveRamp || unitController == null)
            {
                return;
            }

            if (IsPostureTransitionMovementLocked())
            {
                return;
            }

            unitController.BeginMoveSpeedRamp(postStandJogBuildUpSeconds, postStandJogStartSpeedMultiplier);
            _pendingPostStandMoveRamp = false;
        }

        private void HandleInteractInput()
        {
            if (Keyboard.current == null) return;
            if (!Keyboard.current[interactKey].wasPressedThisFrame) return;

            // Door takes priority when nearby; fall through to container if no door found.
            if (doorInteractor != null && doorInteractor.Interact()) return;
            if (TryInteractNearestItemPickup()) return;
            containerInteractor?.Interact();
        }

        private bool TryInteractNearestItemPickup()
        {
            if (!EnsureItemPickupInteractorBound())
            {
                return false;
            }

            MethodInfo interactMethod = ResolveItemPickupInteractMethod(itemPickupInteractor.GetType());
            if (interactMethod == null)
            {
                return false;
            }

            object interactResult = interactMethod.Invoke(itemPickupInteractor, null);
            return interactResult is bool interacted && interacted;
        }

        private bool EnsureItemPickupInteractorBound()
        {
            if (itemPickupInteractor != null)
            {
                if (ResolveItemPickupInteractMethod(itemPickupInteractor.GetType()) != null)
                {
                    return true;
                }

                itemPickupInteractor = null;
            }

            Type interactorType = ResolveItemPickupInteractorType();
            if (interactorType == null)
            {
                return false;
            }

            if (itemPickupInteractor == null)
            {
                itemPickupInteractor = GetComponent(interactorType) as MonoBehaviour;
            }

            if (itemPickupInteractor == null)
            {
                itemPickupInteractor = gameObject.AddComponent(interactorType) as MonoBehaviour;
            }

            return itemPickupInteractor != null;
        }

        private static Type ResolveItemPickupInteractorType()
        {
            if (attemptedResolveItemPickupInteractorType)
            {
                return cachedItemPickupInteractorType;
            }

            attemptedResolveItemPickupInteractorType = true;
            cachedItemPickupInteractorType = Type.GetType(ItemPickupInteractorTypeName, throwOnError: false);
            if (cachedItemPickupInteractorType != null)
            {
                return cachedItemPickupInteractorType;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                cachedItemPickupInteractorType = assemblies[i].GetType(ItemPickupInteractorTypeName, throwOnError: false);
                if (cachedItemPickupInteractorType != null)
                {
                    return cachedItemPickupInteractorType;
                }
            }

            return null;
        }

        private static MethodInfo ResolveItemPickupInteractMethod(Type interactorType)
        {
            if (interactorType == null)
            {
                return null;
            }

            if (cachedItemPickupInteractMethod != null
                && cachedItemPickupInteractMethod.DeclaringType == interactorType)
            {
                return cachedItemPickupInteractMethod;
            }

            MethodInfo method = interactorType.GetMethod(
                "Interact",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (method == null || method.ReturnType != typeof(bool))
            {
                cachedItemPickupInteractMethod = null;
                return null;
            }

            cachedItemPickupInteractMethod = method;
            return cachedItemPickupInteractMethod;
        }

        private void HandleMovementInput(bool pointerOverUi)
        {
            if (Mouse.current == null) return;

            bool pressed = Mouse.current.leftButton.wasPressedThisFrame;
            bool released = Mouse.current.leftButton.wasReleasedThisFrame;
            bool held = Mouse.current.leftButton.isPressed;

            if (pressed)
            {
                leftWorldClickBegan = !pointerOverUi;

                if (leftWorldClickBegan && TryGetUnitHealthUnderCursor(out _))
                {
                    // Reserve left-click press/release pair for combat targeting.
                    suppressNextLeftClickMoveRelease = true;
                    leftWorldClickBegan = false;
                }
            }

            if (pointerOverUi)
            {
                if (released)
                {
                    leftWorldClickBegan = false;
                    suppressNextLeftClickMoveRelease = false;
                }

                return;
            }

            if (Keyboard.current != null
                && (Keyboard.current[crouchKey].wasPressedThisFrame
                    || Keyboard.current[crawlKey].wasPressedThisFrame))
            {
                return;
            }

            if (IsPostureTransitionMovementLocked())
            {
                return;
            }

            bool allowHoldMove = allowMoveWhileHoldingLeftMouse && !enableDragBoxSquadSelection;
            bool shouldMoveThisFrame = (released && leftWorldClickBegan) || (allowHoldMove && held && leftWorldClickBegan);

            if (released && suppressNextLeftClickMoveRelease && !leftWorldClickBegan)
            {
                suppressNextLeftClickMoveRelease = false;
                return;
            }

            if (suppressLeftClickMovementUntilRelease)
            {
                return;
            }

            if (!shouldMoveThisFrame)
            {
                return;
            }

            if (released)
            {
                leftWorldClickBegan = false;

                if (suppressNextLeftClickMoveRelease)
                {
                    suppressNextLeftClickMoveRelease = false;
                    return;
                }
            }

            bool discreteClickMove = released;

            bool movementLocked = IsMovementTemporarilyLocked();
            if (movementLocked)
            {
                bool canBreakEncounterWithMove = discreteClickMove && IsPlayerInEncounter();
                if (!discreteClickMove || (!allowMoveCommandToCancelAttack && !canBreakEncounterWithMove))
                {
                    return;
                }

                movementLockExpiresAt = 0f;
            }

            if (TryGetGroundPoint(out Vector3 groundPoint))
            {
                diggingSystem?.CancelDig();
                pendingEncounterTarget = null;
                combatFacingAssistTarget = null;

                if (discreteClickMove)
                {
                    BeginMoveOrderFacingSuppression();
                    ClearBowRangedCombatState(clearAimTarget: true);

                    suppressAutoEngageUntilAt = Mathf.Max(
                        suppressAutoEngageUntilAt,
                        Time.time + Mathf.Max(0f, autoEngageSuppressAfterPlayerMoveSeconds));

                    TryDisengagePlayerEncounterForMovement();
                    playerAnimationController?.CancelAttackForMovement(groundPoint);
                    playerAnimationController?.CancelCombatFacingForMovement();
                }

                TryBeginPostStandMoveRamp();
                unitController.MoveTo(groundPoint);
                combatFootwork?.NotifyPlayerIssuedMove();

                if (discreteClickMove && issueSquadMoveOnRightClick && squadManager != null)
                {
                    squadManager.IssueOrder(SquadCommandType.Move, groundPoint);
                }
            }
        }

        private void HandleSquadSelectionInput(bool pointerOverUi)
        {
            if (squadManager == null)
            {
                return;
            }

            if (Keyboard.current != null)
            {
                bool ctrlPressed = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
                if (ctrlPressed && Keyboard.current.aKey.wasPressedThisFrame)
                {
                    squadManager.SelectAllMembers();
                }
            }

            if (!enableDragBoxSquadSelection || Mouse.current == null)
            {
                return;
            }

            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            bool pressed = Mouse.current.leftButton.wasPressedThisFrame;
            bool held = Mouse.current.leftButton.isPressed;
            bool released = Mouse.current.leftButton.wasReleasedThisFrame;

            if (pressed)
            {
                if (pointerOverUi || TryGetUnitHealthUnderCursor(out _))
                {
                    isDragSelectCandidateActive = false;
                    isDragSelecting = false;
                    return;
                }

                isDragSelectCandidateActive = true;
                isDragSelecting = false;
                dragSelectStartScreen = mouseScreenPosition;
                dragSelectCurrentScreen = mouseScreenPosition;
            }

            if (!isDragSelectCandidateActive)
            {
                return;
            }

            if (held)
            {
                dragSelectCurrentScreen = mouseScreenPosition;

                if (!isDragSelecting)
                {
                    float threshold = Mathf.Max(4f, dragSelectionThresholdPixels);
                    if ((dragSelectCurrentScreen - dragSelectStartScreen).sqrMagnitude >= threshold * threshold)
                    {
                        isDragSelecting = true;
                    }
                }
            }

            if (!released)
            {
                return;
            }

            dragSelectCurrentScreen = mouseScreenPosition;

            if (isDragSelecting)
            {
                SelectSquadMembersInDragRectangle();
                suppressNextLeftClickMoveRelease = true;
            }

            isDragSelectCandidateActive = false;
            isDragSelecting = false;
        }

        private void SelectSquadMembersInDragRectangle()
        {
            if (squadManager == null)
            {
                return;
            }

            Camera selectionCamera = worldCamera != null ? worldCamera : Camera.main;
            if (selectionCamera == null)
            {
                return;
            }

            Rect selectionRect = GetNormalizedScreenRect(dragSelectStartScreen, dragSelectCurrentScreen);
            float padding = Mathf.Max(0f, dragSelectionScreenPaddingPixels);
            if (padding > 0f)
            {
                selectionRect.xMin -= padding;
                selectionRect.yMin -= padding;
                selectionRect.xMax += padding;
                selectionRect.yMax += padding;
            }

            if (playerUnit != null && playerUnit.IsAlive)
            {
                Vector3 playerScreenPosition = selectionCamera.WorldToScreenPoint(playerUnit.transform.position);
                if (playerScreenPosition.z > 0f
                    && selectionRect.Contains(new Vector2(playerScreenPosition.x, playerScreenPosition.y)))
                {
                    // Dragging over the controlled player is treated as a full-squad selection.
                    squadManager.SelectAllMembers();
                    return;
                }
            }

            dragSelectionBuffer.Clear();

            IReadOnlyList<SquadMember> members = squadManager.SquadMembers;
            for (int i = 0; i < members.Count; i++)
            {
                SquadMember member = members[i];
                if (member == null || !member.IsAvailableForOrders())
                {
                    continue;
                }

                Transform memberTransform = member.Unit != null
                    ? member.Unit.transform
                    : member.transform;

                if (memberTransform == null)
                {
                    continue;
                }

                if (TryGetMemberScreenRect(member, selectionCamera, out Rect memberScreenRect))
                {
                    if (selectionRect.Overlaps(memberScreenRect, true))
                    {
                        dragSelectionBuffer.Add(member);
                    }

                    continue;
                }

                Vector3 screenPosition = selectionCamera.WorldToScreenPoint(memberTransform.position);
                if (screenPosition.z <= 0f)
                {
                    continue;
                }

                if (selectionRect.Contains(new Vector2(screenPosition.x, screenPosition.y)))
                {
                    dragSelectionBuffer.Add(member);
                }
            }

            squadManager.SetSelectedMembers(dragSelectionBuffer);
        }

        private static bool TryGetMemberScreenRect(SquadMember member, Camera camera, out Rect screenRect)
        {
            screenRect = default;
            if (member == null || camera == null)
            {
                return false;
            }

            Transform root = member.Unit != null ? member.Unit.transform : member.transform;
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            bool hasAnyPoint = false;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Vector3 center = bounds.center;
                Vector3 extents = bounds.extents;

                for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                {
                    Vector3 corner = new Vector3(
                        center.x + (((cornerIndex & 1) == 0) ? -extents.x : extents.x),
                        center.y + (((cornerIndex & 2) == 0) ? -extents.y : extents.y),
                        center.z + (((cornerIndex & 4) == 0) ? -extents.z : extents.z));

                    Vector3 screenPoint = camera.WorldToScreenPoint(corner);
                    if (screenPoint.z <= 0f)
                    {
                        continue;
                    }

                    if (!hasAnyPoint)
                    {
                        minX = maxX = screenPoint.x;
                        minY = maxY = screenPoint.y;
                        hasAnyPoint = true;
                    }
                    else
                    {
                        if (screenPoint.x < minX) minX = screenPoint.x;
                        if (screenPoint.x > maxX) maxX = screenPoint.x;
                        if (screenPoint.y < minY) minY = screenPoint.y;
                        if (screenPoint.y > maxY) maxY = screenPoint.y;
                    }
                }
            }

            if (!hasAnyPoint)
            {
                Vector3 fallbackPoint = camera.WorldToScreenPoint(root.position);
                if (fallbackPoint.z <= 0f)
                {
                    return false;
                }

                screenRect = new Rect(fallbackPoint.x, fallbackPoint.y, 1f, 1f);
                return true;
            }

            screenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private void OnGUI()
        {
            if (!enableDragBoxSquadSelection || !isDragSelecting)
            {
                return;
            }

            Rect screenRect = GetNormalizedScreenRect(dragSelectStartScreen, dragSelectCurrentScreen);
            if (screenRect.width < 1f || screenRect.height < 1f)
            {
                return;
            }

            Rect guiRect = ConvertScreenRectToGuiRect(screenRect);
            DrawGuiRect(guiRect, dragSelectionFillColor);

            float borderThickness = Mathf.Max(1f, dragSelectionBorderThickness);
            DrawGuiRect(new Rect(guiRect.xMin, guiRect.yMin, guiRect.width, borderThickness), dragSelectionBorderColor);
            DrawGuiRect(new Rect(guiRect.xMin, guiRect.yMax - borderThickness, guiRect.width, borderThickness), dragSelectionBorderColor);
            DrawGuiRect(new Rect(guiRect.xMin, guiRect.yMin, borderThickness, guiRect.height), dragSelectionBorderColor);
            DrawGuiRect(new Rect(guiRect.xMax - borderThickness, guiRect.yMin, borderThickness, guiRect.height), dragSelectionBorderColor);
        }

        private static Rect GetNormalizedScreenRect(Vector2 start, Vector2 end)
        {
            float minX = Mathf.Min(start.x, end.x);
            float minY = Mathf.Min(start.y, end.y);
            float maxX = Mathf.Max(start.x, end.x);
            float maxY = Mathf.Max(start.y, end.y);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Rect ConvertScreenRectToGuiRect(Rect screenRect)
        {
            float guiY = Screen.height - screenRect.yMax;
            return new Rect(screenRect.xMin, guiY, screenRect.width, screenRect.height);
        }

        private static void DrawGuiRect(Rect rect, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void TryDisengagePlayerEncounterForMovement()
        {
            if (playerUnit == null)
            {
                return;
            }

            if (encounterManager == null)
            {
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : Object.FindFirstObjectByType<CombatEncounterManager>();
            }

            if (encounterManager == null)
            {
                return;
            }

            if (!encounterManager.TryDisengageUnit(playerUnit, "player-move-disengage"))
            {
                return;
            }

            ClearBowRangedCombatState(clearAimTarget: true);
            movementLockExpiresAt = 0f;
            pendingEncounterTarget = null;
            combatFacingAssistTarget = null;
            combatFacingAssistExpiresAt = 0f;
            suppressAutoEngageUntilAt = Mathf.Max(
                suppressAutoEngageUntilAt,
                Time.time + Mathf.Max(0f, autoEngageSuppressAfterPlayerMoveSeconds));
        }

        private bool IsPlayerInEncounter()
        {
            if (playerUnit == null)
            {
                return false;
            }

            if (encounterManager == null)
            {
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : Object.FindFirstObjectByType<CombatEncounterManager>();
            }

            return encounterManager != null && encounterManager.IsUnitInEncounter(playerUnit);
        }

        private bool HandleDigInput(bool hasNearbyEnemies = false, bool pointerOverUi = false)
        {
            if (!rightClickDigEnabled || diggingSystem == null || Mouse.current == null)
            {
                return false;
            }

            bool consumedRightClick = false;

            if (pointerOverUi)
            {
                if (Mouse.current.rightButton.wasReleasedThisFrame)
                {
                    diggingSystem.CancelDig();
                }

                return false;
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                // Combat takes priority over digging when enemies are nearby.
                bool hasCombatTarget = hasNearbyEnemies || (requireNoCombatTarget && TryGetUnitHealthUnderCursor(out _));

                if (!hasCombatTarget)
                {
                    consumedRightClick = true;
                    ClearBowRangedCombatState(clearAimTarget: true);
                    if (TryGetGroundPoint(out Vector3 digPoint))
                    {
                        _ = diggingSystem.StartDig(digPoint);
                    }
                }
            }

            if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                diggingSystem.CancelDig();
            }

            return consumedRightClick;
        }

        private void HandleCombatInput(bool consumeRightClickCombat, bool pointerOverUi)
        {
            bool rightClickThisFrame = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && !consumeRightClickCombat && !pointerOverUi;
            bool leftClickThisFrame = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !pointerOverUi;
            bool rKeyThisFrame = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;

            Unit preferredTarget = null;
            bool clickedHostileTarget = false;

            if ((rightClickThisFrame || leftClickThisFrame) && TryGetUnitHealthUnderCursor(out UnitHealth markedTarget))
            {
                unitCombat.SetMarkedTarget(markedTarget);
                preferredTarget = markedTarget != null ? markedTarget.GetComponentInParent<Unit>() : null;
                clickedHostileTarget = preferredTarget != null;

                if (clickedHostileTarget)
                {
                    bool requestTurnAnimation = playFacingTurnAnimationOnHostileClick
                        && playerAnimationController != null
                        && !unitController.IsMoving
                        && !IsPlayerInEncounter();
                    QueueCombatFacingAssist(preferredTarget, requestTurnAnimation);
                }

                if (leftClickThisFrame && clickedHostileTarget)
                {
                    // A hostile left-click is a combat intent; do not let movement consume this press.
                    suppressLeftClickMovementUntilRelease = true;
                    pendingEncounterTarget = null;
                    unitController?.Stop();
                }
            }

            if (clickedHostileTarget)
            {
                suppressCombatFacingUntilMoveOrderCompletes = false;

                if (IsBowRangedModeActive())
                {
                    StartBowRangedCombat(preferredTarget);
                    return;
                }

                ClearBowRangedCombatState(clearAimTarget: true);

                bool startedEncounter = TryStartPlayerEncounter(preferredTarget, allowApproachWhenOutOfRange: true);
                if (startedEncounter)
                {
                    BeginPostAttackMovementLock();
                    return;
                }

                // Keep hit timing animation-synced by avoiding immediate legacy attacks
                // whenever the encounter system is available.
                if (HasEncounterSystemAvailable())
                {
                    return;
                }

                // Fallback attack path if an encounter cannot start this frame.
                visibleTargets.Clear();

                if (UnitManager.Instance != null)
                {
                    List<Unit> nearbyEnemies = UnitManager.Instance.FindNearbyEnemies(playerUnit, attackScanRadius);

                    for (int i = 0; i < nearbyEnemies.Count; i++)
                    {
                        Unit enemy = nearbyEnemies[i];

                        if (enemy != null && enemy.Health != null && !enemy.Health.IsDead)
                        {
                            visibleTargets.Add(enemy.Health);
                        }
                    }
                }

                if (combatSystem != null)
                {
                    bool attacked = combatSystem.TryExecuteAttack(unitCombat, visibleTargets);
                    if (attacked && clickedHostileTarget)
                    {
                        BeginPostAttackMovementLock();
                        pendingEncounterTarget = null;
                        unitController?.Stop();
                    }
                }
                else if (combatManager != null)
                {
                    bool attacked = combatManager.RequestAttack(unitCombat, visibleTargets);
                    if (attacked && clickedHostileTarget)
                    {
                        BeginPostAttackMovementLock();
                        pendingEncounterTarget = null;
                        unitController?.Stop();
                    }
                }
                else
                {
                    bool attacked = unitCombat.ExecuteAttack(visibleTargets);
                    if (attacked && clickedHostileTarget)
                    {
                        BeginPostAttackMovementLock();
                        pendingEncounterTarget = null;
                        unitController?.Stop();
                    }
                }
            }

            if (rKeyThisFrame)
            {
                if (combatSystem != null)
                {
                    combatSystem.Reload(unitCombat);
                }
                else if (combatManager != null)
                {
                    combatManager.RequestReload(unitCombat);
                }
                else
                {
                    unitCombat.Reload();
                }
            }
        }

        private void TickBowRangedCombatState()
        {
            bool bowRangedModeActive = IsBowRangedModeActive();
            if (!bowRangedModeActive)
            {
                if (activeBowRangedTarget != null || bowShotQueued)
                {
                    ClearBowRangedCombatState(clearAimTarget: true);
                }

                return;
            }

            if (activeBowRangedTarget == null || !IsValidEncounterTarget(activeBowRangedTarget))
            {
                ClearBowRangedCombatState(clearAimTarget: true);
                return;
            }

            if (bowShotQueued)
            {
                // Never let suppression/posture gates trap an already-queued release.
                if (unitController != null && (unitController.HasMoveTarget || unitController.IsMoving))
                {
                    unitController.Stop();
                }

                TryReleaseQueuedBowShot();
                return;
            }

            if (IsMoveOrderFacingSuppressed())
            {
                return;
            }

            if (unitCombat != null && activeBowRangedTarget.Health != null)
            {
                unitCombat.SetMarkedTarget(activeBowRangedTarget.Health);
            }

            // Keep bow stance ownership stable while ranged combat remains active.
            playerAnimationController?.SetBowAimTarget(activeBowRangedTarget);

            if (IsPostureTransitionMovementLocked())
            {
                return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, activeBowRangedTarget.transform.position);
            float fallbackDistance = Mathf.Max(0f, bowMeleeFallbackDistanceMeters);

            if (!bowShotQueued && fallbackDistance > 0f && distanceToTarget <= fallbackDistance)
            {
                Unit fallbackTarget = activeBowRangedTarget;
                ClearBowRangedCombatState(clearAimTarget: true);
                _ = TryStartPlayerEncounter(fallbackTarget, allowApproachWhenOutOfRange: true);
                return;
            }

            MaintainBowStandoffDistance(activeBowRangedTarget, distanceToTarget);

            float effectiveRange = ResolveCurrentBowRange();
            if (distanceToTarget > effectiveRange)
            {
                return;
            }

            if (!IsReadyToInitiateBowAim(activeBowRangedTarget))
            {
                return;
            }

            if (Time.time < nextBowShotAt)
            {
                return;
            }

            QueueBowShot();
        }

        private void StartBowRangedCombat(Unit target)
        {
            if (!enableBowRangedCombatState || !IsBowRangedModeActive() || !IsValidEncounterTarget(target))
            {
                return;
            }

            if (encounterManager == null)
            {
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : Object.FindFirstObjectByType<CombatEncounterManager>();
            }

            if (encounterManager != null && playerUnit != null && encounterManager.IsUnitInEncounter(playerUnit))
            {
                encounterManager.TryDisengageUnit(playerUnit, "player-bow-ranged");
            }

            movementLockExpiresAt = 0f;
            pendingEncounterTarget = null;
            activeBowRangedTarget = target;
            nextBowRepositionAt = 0f;

            if (unitCombat != null && target.Health != null)
            {
                unitCombat.SetMarkedTarget(target.Health);
            }

            unitController?.Stop();
            playerAnimationController?.ClearBowAimTarget();
            QueueCombatFacingAssist(target, requestTurnAnimation: true);

            if (!bowShotQueued && Time.time >= nextBowShotAt)
            {
                QueueBowShot();
            }
        }

        private void QueueBowShot()
        {
            if (activeBowRangedTarget == null || !IsValidEncounterTarget(activeBowRangedTarget))
            {
                return;
            }

            if (!IsReadyToInitiateBowAim(activeBowRangedTarget))
            {
                return;
            }

            if (unitController != null && (unitController.HasMoveTarget || unitController.IsMoving))
            {
                unitController.Stop();
            }

            playerAnimationController?.SetBowAimTarget(activeBowRangedTarget);

            bowShotQueued = true;
            bowQueuedRapidFire = ResolveBowRapidFireMode();
            bowQueuedDurationSeconds = ResolveBowChargeDurationSeconds();
            bowQueuedReleaseAt = Time.time + bowQueuedDurationSeconds;
            playerAnimationController?.TriggerBowDraw(bowQueuedRapidFire);
        }

        private bool IsReadyToInitiateBowAim(Unit target)
        {
            if (target == null || !IsValidEncounterTarget(target))
            {
                return false;
            }

            if (unitController != null)
            {
                float maxSpeed = Mathf.Max(0f, bowAimInitiationMaxSpeedMetersPerSecond);
                bool isMoving = unitController.HasMoveTarget
                    || unitController.IsMoving
                    || unitController.WorldVelocity.magnitude > maxSpeed;

                if (isMoving)
                {
                    return false;
                }
            }

            if (!IsFacingTargetWithin(target, bowStartFacingToleranceDegrees))
            {
                QueueCombatFacingAssist(target, requestTurnAnimation: false);
                return false;
            }

            return true;
        }

        private void TryReleaseQueuedBowShot()
        {
            if (!bowShotQueued || !IsBowChargeComplete())
            {
                return;
            }

            if (activeBowRangedTarget == null || !IsValidEncounterTarget(activeBowRangedTarget))
            {
                bowShotQueued = false;
                bowQueuedRapidFire = false;
                bowQueuedReleaseAt = 0f;
                bowQueuedDurationSeconds = 0f;
                return;
            }

            bool rapidFire = bowQueuedRapidFire;
            bowShotQueued = false;
            bowQueuedRapidFire = false;
            bowQueuedReleaseAt = 0f;
            bowQueuedDurationSeconds = 0f;

            playerAnimationController?.TriggerBowRelease(rapidFire);

            bool attacked = TryExecuteBowAttack(activeBowRangedTarget);
            if (attacked)
            {
                nextBowShotAt = Time.time + ResolveBowShotCadenceSeconds();
                return;
            }

            nextBowShotAt = Time.time + 0.15f;
            TryReloadCurrentWeapon();
        }

        private bool TryExecuteBowAttack(Unit explicitTarget)
        {
            if (unitCombat == null || explicitTarget == null || explicitTarget.Health == null || explicitTarget.Health.IsDead)
            {
                return false;
            }

            WeaponSystem resolvedWeaponSystem = ResolveWeaponSystem();
            if (resolvedWeaponSystem != null && resolvedWeaponSystem.IsBowEquipped)
            {
                return resolvedWeaponSystem.TryAttackTarget(explicitTarget.Health);
            }

            visibleTargets.Clear();
            visibleTargets.Add(explicitTarget.Health);

            if (combatSystem != null)
            {
                return combatSystem.TryExecuteAttack(unitCombat, visibleTargets);
            }

            if (combatManager != null)
            {
                return combatManager.RequestAttack(unitCombat, visibleTargets);
            }

            return unitCombat.ExecuteAttack(visibleTargets);
        }

        private void TryReloadCurrentWeapon()
        {
            if (unitCombat == null)
            {
                return;
            }

            if (combatSystem != null)
            {
                combatSystem.Reload(unitCombat);
                return;
            }

            if (combatManager != null)
            {
                combatManager.RequestReload(unitCombat);
                return;
            }

            unitCombat.Reload();
        }

        private void MaintainBowStandoffDistance(Unit target, float distanceToTarget)
        {
            if (target == null || unitController == null)
            {
                return;
            }

            if (Time.time < nextBowRepositionAt)
            {
                return;
            }

            nextBowRepositionAt = Time.time + Mathf.Max(0.05f, bowRepositionIntervalSeconds);

            float effectiveRange = ResolveCurrentBowRange();

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 directionToTarget = toTarget.normalized;
            float stepDistance = Mathf.Max(0.1f, bowRepositionStepMeters);

            if (distanceToTarget > effectiveRange)
            {
                float desiredDistance = Mathf.Max(0.5f, effectiveRange - 0.5f);
                Vector3 approachPosition = target.transform.position - directionToTarget * Mathf.Max(desiredDistance, stepDistance);
                TryBeginPostStandMoveRamp();
                unitController.MoveTo(approachPosition);
                return;
            }

            if (!maintainBowPreferredStandoffDistance)
            {
                if (unitController.HasMoveTarget || unitController.IsMoving)
                {
                    unitController.Stop();
                }

                return;
            }

            float preferredMin = Mathf.Clamp(bowPreferredMinDistanceMeters, 0f, Mathf.Max(0.1f, effectiveRange - 0.2f));
            float preferredMax = Mathf.Clamp(
                bowPreferredMaxDistanceMeters,
                Mathf.Max(preferredMin + 0.1f, 0.2f),
                Mathf.Max(preferredMin + 0.1f, effectiveRange));

            if (distanceToTarget < preferredMin)
            {
                Vector3 retreatPosition = transform.position - directionToTarget * stepDistance;
                TryBeginPostStandMoveRamp();
                unitController.MoveTo(retreatPosition);
                return;
            }

            if (distanceToTarget > preferredMax)
            {
                Vector3 approachPosition = target.transform.position - directionToTarget * Mathf.Max(preferredMax, stepDistance);
                TryBeginPostStandMoveRamp();
                unitController.MoveTo(approachPosition);
                return;
            }

            if (unitController.HasMoveTarget)
            {
                unitController.Stop();
            }
        }

        private float ResolveCurrentBowRange()
        {
            return Mathf.Max(1f, bowMaximumAttackRangeMeters);
        }

        private bool IsBowChargeComplete()
        {
            if (!bowShotQueued)
            {
                return false;
            }

            if (Time.time >= bowQueuedReleaseAt)
            {
                return true;
            }

            return BowShotChargeProgress01 >= 0.999f;
        }

        private bool ResolveBowRapidFireMode()
        {
            WeaponSystem resolvedWeaponSystem = ResolveWeaponSystem();
            if (resolvedWeaponSystem?.EquippedWeapon == null)
            {
                return false;
            }

            return resolvedWeaponSystem.EquippedWeapon.fireRate >= 1.75f;
        }

        private float ResolveBowChargeDurationSeconds()
        {
            float fallbackDuration = Mathf.Max(0.1f, bowDrawDurationSeconds);

            UnitStats stats = playerUnit != null ? playerUnit.Stats : null;
            if (stats == null)
            {
                return fallbackDuration;
            }

            int shootingLevel = Mathf.Clamp(
                stats.GetSkillValue(UnitSkillType.Shooting),
                UnitStats.MinSkillLevel,
                UnitStats.MaxSkillLevel);

            float levelT = (shootingLevel - UnitStats.MinSkillLevel)
                / (float)(UnitStats.MaxSkillLevel - UnitStats.MinSkillLevel);

            float levelOneDuration = Mathf.Max(0.1f, bowChargeDurationAtLevel1Seconds);
            float levelHundredDuration = Mathf.Max(0.1f, bowChargeDurationAtLevel100Seconds);
            return Mathf.Lerp(levelOneDuration, levelHundredDuration, Mathf.Clamp01(levelT));
        }

        private float ResolveBowShotCadenceSeconds()
        {
            float byAttackCooldown = unitCombat != null ? unitCombat.EffectiveAttackCooldown : 0f;
            float byWeaponFireRate = 0.75f;

            WeaponSystem resolvedWeaponSystem = ResolveWeaponSystem();
            if (resolvedWeaponSystem?.EquippedWeapon != null && resolvedWeaponSystem.EquippedWeapon.fireRate > 0.01f)
            {
                byWeaponFireRate = 1f / resolvedWeaponSystem.EquippedWeapon.fireRate;
            }

            return Mathf.Max(byAttackCooldown, byWeaponFireRate) + Mathf.Max(0f, bowShotCadencePaddingSeconds);
        }

        private WeaponSystem ResolveWeaponSystem()
        {
            if (weaponSystem != null)
            {
                return weaponSystem;
            }

            if (unitCombat != null)
            {
                weaponSystem = unitCombat.GetComponent<WeaponSystem>();
            }

            if (weaponSystem == null && playerUnit != null)
            {
                weaponSystem = playerUnit.GetComponent<WeaponSystem>();
            }

            if (weaponSystem == null)
            {
                weaponSystem = GetComponent<WeaponSystem>();
            }

            return weaponSystem;
        }

        private bool IsBowRangedModeActive()
        {
            if (!enableBowRangedCombatState)
            {
                return false;
            }

            WeaponSystem resolvedWeaponSystem = ResolveWeaponSystem();
            return resolvedWeaponSystem != null && resolvedWeaponSystem.IsBowEquipped;
        }

        private void ClearBowRangedCombatState(bool clearAimTarget)
        {
            activeBowRangedTarget = null;
            bowShotQueued = false;
            bowQueuedReleaseAt = 0f;
            bowQueuedDurationSeconds = 0f;
            bowQueuedRapidFire = false;
            nextBowRepositionAt = 0f;

            if (clearAimTarget)
            {
                playerAnimationController?.ClearBowAimTarget();
            }
        }

        private void TryAutoStartEncounterFromNearbyThreat()
        {
            if (playerUnit == null || !playerUnit.IsAlive)
            {
                return;
            }

            if (IsBowRangedModeActive())
            {
                return;
            }

            if (IsMoveOrderFacingSuppressed())
            {
                return;
            }

            if (IsPostureTransitionMovementLocked())
            {
                return;
            }

            if (Time.time < suppressAutoEngageUntilAt)
            {
                return;
            }

            float autoEngageDistance = Mathf.Max(0f, autoEngageEnemyDistanceMeters);
            if (autoEngageDistance <= 0f)
            {
                return;
            }

            if (encounterManager == null)
            {
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : Object.FindFirstObjectByType<CombatEncounterManager>();
            }

            if (encounterManager == null || encounterManager.IsUnitInEncounter(playerUnit) || pendingEncounterTarget != null)
            {
                return;
            }

            Unit nearbyTarget = FindNearestEncounterTarget(autoEngageDistance);
            if (!IsValidEncounterTarget(nearbyTarget))
            {
                return;
            }

            if (TryStartPlayerEncounter(nearbyTarget, allowApproachWhenOutOfRange: false))
            {
                BeginPostAttackMovementLock();
            }
        }

        private void HandleSquadCommandInput()
        {
            if (squadManager == null || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame && TryGetGroundPoint(out Vector3 movePoint))
            {
                squadManager.IssueOrder(SquadCommandType.Move, movePoint);
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame && TryGetGroundPoint(out Vector3 attackPoint))
            {
                squadManager.IssueOrder(SquadCommandType.Attack, attackPoint);
            }

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                squadManager.IssueOrder(SquadCommandType.HoldPosition);
            }

            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                squadManager.IssueOrder(SquadCommandType.Follow);
            }

            if (Keyboard.current.digit5Key.wasPressedThisFrame && TryGetGroundPoint(out Vector3 defendPoint))
            {
                squadManager.IssueOrder(SquadCommandType.Defend, defendPoint);
            }
        }

        private bool TryStartPlayerEncounter(Unit preferredTarget = null, bool allowApproachWhenOutOfRange = true)
        {
            if (playerUnit == null)
            {
                pendingEncounterTarget = null;
                return false;
            }

            if (encounterManager == null)
            {
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : Object.FindFirstObjectByType<CombatEncounterManager>();
            }

            if (encounterManager == null)
            {
                pendingEncounterTarget = null;
                return false;
            }

            if (encounterManager.IsUnitInEncounter(playerUnit))
            {
                pendingEncounterTarget = null;
                return true;
            }

            Unit target = IsValidEncounterTarget(preferredTarget) ? preferredTarget : FindNearestEncounterTarget();
            if (!IsValidEncounterTarget(target))
            {
                pendingEncounterTarget = null;
                return false;
            }

            QueueCombatFacingAssist(target, requestTurnAnimation: true);
            bool withinPreferredDistance = IsWithinPreferredEncounterStartDistance(target);
            bool isFacingWithinTolerance = IsFacingTargetWithin(target, encounterStartFacingToleranceDegrees);

            if (withinPreferredDistance)
            {
                playerAnimationController?.TryTriggerCombatEntry();
            }

            if (!withinPreferredDistance || !isFacingWithinTolerance)
            {
                pendingEncounterTarget = target;
                if (allowApproachWhenOutOfRange)
                {
                    if (!withinPreferredDistance)
                    {
                        TryBeginPostStandMoveRamp();
                        unitController?.MoveTo(ResolveEncounterApproachPosition(target));
                    }
                }
                else
                {
                    unitController?.Stop();
                }

                return false;
            }

            if (encounterManager.TryStartEncounter(playerUnit, target))
            {
                pendingEncounterTarget = null;
                return true;
            }

            if (!allowApproachWhenOutOfRange)
            {
                pendingEncounterTarget = null;
                unitController?.Stop();
                return false;
            }

            pendingEncounterTarget = target;
            TryBeginPostStandMoveRamp();
            unitController?.MoveTo(ResolveEncounterApproachPosition(target));
            return false;
        }

        private void BeginPostAttackMovementLock()
        {
            float lockDuration = Mathf.Max(postAttackMovementLockSeconds, minimumAttackMovementLockSeconds);
            if (lockDuration <= 0f)
            {
                return;
            }

            movementLockExpiresAt = Mathf.Max(movementLockExpiresAt, Time.time + lockDuration);
        }

        private bool IsMovementTemporarilyLocked()
        {
            return movementLockExpiresAt > Time.time;
        }

        private bool HasEncounterSystemAvailable()
        {
            if (encounterManager != null)
            {
                return true;
            }

            encounterManager = CombatEncounterManager.Instance != null
                ? CombatEncounterManager.Instance
                : Object.FindFirstObjectByType<CombatEncounterManager>();

            return encounterManager != null;
        }

        private void TickPendingEncounterJoin()
        {
            if (IsMoveOrderFacingSuppressed())
            {
                pendingEncounterTarget = null;
                return;
            }

            if (IsPostureTransitionMovementLocked())
            {
                unitController?.Stop();
                return;
            }

            if (pendingEncounterTarget == null)
            {
                return;
            }

            if (!IsValidEncounterTarget(pendingEncounterTarget) || playerUnit == null || !playerUnit.IsAlive)
            {
                pendingEncounterTarget = null;
                return;
            }

            if (encounterManager == null)
            {
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : Object.FindFirstObjectByType<CombatEncounterManager>();
            }

            if (encounterManager == null)
            {
                pendingEncounterTarget = null;
                return;
            }

            if (encounterManager.IsUnitInEncounter(playerUnit))
            {
                pendingEncounterTarget = null;
                return;
            }

            QueueCombatFacingAssist(pendingEncounterTarget, requestTurnAnimation: false);
            bool withinPreferredDistance = IsWithinPreferredEncounterStartDistance(pendingEncounterTarget);

            if (withinPreferredDistance)
            {
                playerAnimationController?.TryTriggerCombatEntry();
            }

            if (!withinPreferredDistance)
            {
                TryBeginPostStandMoveRamp();
                unitController?.MoveTo(ResolveEncounterApproachPosition(pendingEncounterTarget));
            }

            if (!IsFacingTargetWithin(pendingEncounterTarget, encounterStartFacingToleranceDegrees) || !withinPreferredDistance)
            {
                return;
            }

            if (encounterManager.TryStartEncounter(playerUnit, pendingEncounterTarget))
            {
                pendingEncounterTarget = null;
            }
        }

        private Vector3 ResolveEncounterApproachPosition(Unit target)
        {
            if (target == null)
            {
                return transform.position;
            }

            float desiredDistance = GetPreferredEncounterStartDistance();

            Vector3 targetPosition = target.transform.position;
            Vector3 fromTargetToPlayer = transform.position - targetPosition;
            fromTargetToPlayer.y = 0f;

            if (fromTargetToPlayer.sqrMagnitude <= 0.0001f)
            {
                fromTargetToPlayer = -target.transform.forward;
                fromTargetToPlayer.y = 0f;

                if (fromTargetToPlayer.sqrMagnitude <= 0.0001f)
                {
                    fromTargetToPlayer = Vector3.back;
                }
            }

            fromTargetToPlayer.Normalize();

            Vector3 approachPosition = targetPosition + fromTargetToPlayer * desiredDistance;
            approachPosition.y = transform.position.y;
            return approachPosition;
        }

        private bool IsWithinPreferredEncounterStartDistance(Unit target)
        {
            if (target == null)
            {
                return false;
            }

            float preferredDistance = GetPreferredEncounterStartDistance();
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            return toTarget.sqrMagnitude <= preferredDistance * preferredDistance;
        }

        private float GetPreferredEncounterStartDistance()
        {
            float engageRange = encounterManager != null ? encounterManager.EngageRange : 1.9f;
            float distanceFactor = Mathf.Clamp(preferredEncounterStartDistanceFactor, 0.1f, 1f);
            return Mathf.Max(0.2f, engageRange * distanceFactor);
        }

        private void QueueCombatFacingAssist(Unit target, bool requestTurnAnimation)
        {
            if (target == null)
            {
                return;
            }

            if (IsMoveOrderFacingSuppressed())
            {
                return;
            }

            combatFacingAssistTarget = target;
            float duration = Mathf.Max(0f, combatFaceAssistDurationSeconds);
            combatFacingAssistExpiresAt = Mathf.Max(combatFacingAssistExpiresAt, Time.time + duration);

            FaceTargetForCombat(target);

            if (requestTurnAnimation)
            {
                playerAnimationController?.TryPlayRandomFacingTurn(target);
            }
        }

        private void TickCombatFacingAssist()
        {
            if (IsMoveOrderFacingSuppressed())
            {
                combatFacingAssistTarget = null;
                combatFacingAssistExpiresAt = 0f;
                return;
            }

            if (combatFacingAssistTarget == null)
            {
                return;
            }

            if (!IsValidEncounterTarget(combatFacingAssistTarget) || Time.time > combatFacingAssistExpiresAt)
            {
                combatFacingAssistTarget = null;
                combatFacingAssistExpiresAt = 0f;
                return;
            }

            FaceTargetForCombat(combatFacingAssistTarget);

            if (IsFacingTargetWithin(combatFacingAssistTarget, combatFaceAssistCompleteAngleDegrees))
            {
                combatFacingAssistTarget = null;
                combatFacingAssistExpiresAt = 0f;
            }
        }

        private void FaceTargetForCombat(Unit target)
        {
            if (target == null || unitController == null)
            {
                return;
            }

            float turnSpeed = Mathf.Max(0f, combatFaceTurnSpeedDegreesPerSecond);
            if (turnSpeed <= 0f)
            {
                unitController.FacePositionInstant(target.transform.position);
                return;
            }

            unitController.RotateTowardsPosition(target.transform.position, turnSpeed);
        }

        private void BeginMoveOrderFacingSuppression()
        {
            suppressCombatFacingUntilMoveOrderCompletes = true;
            pendingEncounterTarget = null;
            combatFacingAssistTarget = null;
            combatFacingAssistExpiresAt = 0f;
        }

        private void TickMoveOrderFacingSuppression()
        {
            if (!IsMoveOrderFacingSuppressed())
            {
                return;
            }

            pendingEncounterTarget = null;
            combatFacingAssistTarget = null;
            combatFacingAssistExpiresAt = 0f;
        }

        private bool IsMoveOrderFacingSuppressed()
        {
            if (!suppressCombatFacingUntilMoveOrderCompletes)
            {
                return false;
            }

            if (unitController == null)
            {
                suppressCombatFacingUntilMoveOrderCompletes = false;
                return false;
            }

            bool moveOrderActive = unitController.HasMoveTarget || unitController.IsMoving;
            if (moveOrderActive)
            {
                return true;
            }

            suppressCombatFacingUntilMoveOrderCompletes = false;
            return false;
        }

        private bool IsFacingTargetWithin(Unit target, float toleranceDegrees)
        {
            if (target == null)
            {
                return false;
            }

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            float angle = Vector3.Angle(forward, toTarget.normalized);
            return angle <= Mathf.Clamp(toleranceDegrees, 0f, 180f);
        }

        private Unit FindNearestEncounterTarget(float maxDistanceMeters = -1f)
        {
            if (playerUnit == null || UnitManager.Instance == null)
            {
                return null;
            }

            float maxDistanceSqr = float.PositiveInfinity;
            float searchRadius = Mathf.Max(0f, attackScanRadius);
            if (maxDistanceMeters > 0f)
            {
                maxDistanceSqr = maxDistanceMeters * maxDistanceMeters;
                searchRadius = Mathf.Min(searchRadius, maxDistanceMeters);
            }

            if (searchRadius <= 0f)
            {
                return null;
            }

            List<Unit> nearby = UnitManager.Instance.FindNearbyEnemies(playerUnit, searchRadius);
            Unit nearest = null;
            float nearestDistSqr = float.MaxValue;

            for (int i = 0; i < nearby.Count; i++)
            {
                Unit candidate = nearby[i];
                if (!IsValidEncounterTarget(candidate))
                {
                    continue;
                }

                float dSqr = (candidate.transform.position - transform.position).sqrMagnitude;
                if (dSqr > maxDistanceSqr)
                {
                    continue;
                }

                if (dSqr < nearestDistSqr)
                {
                    nearestDistSqr = dSqr;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private bool IsValidEncounterTarget(Unit target)
        {
            if (target == null || !target.IsAlive || playerUnit == null)
            {
                return false;
            }

            return UnitFactionUtility.AreHostile(playerUnit.Faction, target.Faction);
        }

        private bool TryGetGroundPoint(out Vector3 worldPoint)
        {
            worldPoint = default;

            if (worldCamera == null)
            {
                return false;
            }

            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Ray ray = worldCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundMask, QueryTriggerInteraction.Ignore))
            {
                // Preserve terrain/building height so NavMesh sampling works on non-zero elevation maps.
                worldPoint = hit.point;
                return true;
            }

            Plane fallbackPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));

            if (fallbackPlane.Raycast(ray, out float distance))
            {
                worldPoint = ray.GetPoint(distance);
                return true;
            }

            return false;
        }

        private static bool IsPointerOverUi()
        {
            UnityEngine.EventSystems.EventSystem uiEventSystem = ResolveUiEventSystem();

            if (uiEventSystem == null)
            {
                return false;
            }

            if (Mouse.current != null)
            {
                if (uiEventSystem.IsPointerOverGameObject())
                {
                    return true;
                }

                if (IsScreenPositionOverUi(uiEventSystem, Mouse.current.position.ReadValue()))
                {
                    return true;
                }
            }

            if (Touchscreen.current == null)
            {
                return false;
            }

            for (int i = 0; i < Touchscreen.current.touches.Count; i++)
            {
                var touch = Touchscreen.current.touches[i];

                if (!touch.press.isPressed)
                {
                    continue;
                }

                if (IsScreenPositionOverUi(uiEventSystem, touch.position.ReadValue()))
                {
                    return true;
                }
            }

            return false;
        }

        private static UnityEngine.EventSystems.EventSystem ResolveUiEventSystem()
        {
            UnityEngine.EventSystems.EventSystem current = UnityEngine.EventSystems.EventSystem.current;
            if (IsUsableUiEventSystem(current))
            {
                return current;
            }

            UnityEngine.EventSystems.EventSystem[] systems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < systems.Length; i++)
            {
                if (IsUsableUiEventSystem(systems[i]))
                {
                    return systems[i];
                }
            }

            return null;
        }

        private static bool IsUsableUiEventSystem(UnityEngine.EventSystems.EventSystem uiEventSystem)
        {
            if (uiEventSystem == null || !uiEventSystem.isActiveAndEnabled)
            {
                return false;
            }

            UnityEngine.EventSystems.BaseInputModule inputModule = uiEventSystem.currentInputModule;
            return inputModule != null && inputModule.isActiveAndEnabled;
        }

        private static bool IsScreenPositionOverUi(UnityEngine.EventSystems.EventSystem uiEventSystem, Vector2 screenPosition)
        {
            var pointerData = new UnityEngine.EventSystems.PointerEventData(uiEventSystem)
            {
                position = screenPosition
            };

            UiRaycastResults.Clear();
            uiEventSystem.RaycastAll(pointerData, UiRaycastResults);
            return UiRaycastResults.Count > 0;
        }

        private bool HasNearbyLiveEnemies()
        {
            if (playerUnit == null || UnitManager.Instance == null) return false;
            List<Unit> nearby = UnitManager.Instance.FindNearbyEnemies(playerUnit, attackScanRadius);
            for (int i = 0; i < nearby.Count; i++)
            {
                if (nearby[i] != null && nearby[i].IsAlive) return true;
            }
            return false;
        }

        private bool TryGetUnitHealthUnderCursor(out UnitHealth unitHealth)
        {
            unitHealth = null;

            if (worldCamera == null || Mouse.current == null)
            {
                return false;
            }

            Vector2 mousePos2 = Mouse.current.position.ReadValue();
            Ray ray = worldCamera.ScreenPointToRay(mousePos2);

            int rayHitCount = Physics.RaycastNonAlloc(
                ray,
                targetRaycastHits,
                targetingRayDistance,
                targetableMask,
                targetingQueryTriggerInteraction);

            if (TryResolveNearestTargetFromHits(targetRaycastHits, rayHitCount, out unitHealth))
            {
                return true;
            }

            float assistRadius = Mathf.Max(0f, zombieHoverAssistRadius);
            if (assistRadius <= 0.0001f)
            {
                return false;
            }

            int sphereHitCount = Physics.SphereCastNonAlloc(
                ray,
                assistRadius,
                targetSpherecastHits,
                targetingRayDistance,
                targetableMask,
                targetingQueryTriggerInteraction);

            return TryResolveNearestTargetFromHits(targetSpherecastHits, sphereHitCount, out unitHealth);
        }

        private bool TryResolveNearestTargetFromHits(RaycastHit[] hitBuffer, int hitCount, out UnitHealth nearestTarget)
        {
            nearestTarget = null;

            if (hitBuffer == null || hitCount <= 0)
            {
                return false;
            }

            int clampedHitCount = Mathf.Min(hitCount, hitBuffer.Length);
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < clampedHitCount; i++)
            {
                RaycastHit hit = hitBuffer[i];
                hitBuffer[i] = default;

                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                {
                    continue;
                }

                UnitHealth candidateHealth = hitCollider.GetComponentInParent<UnitHealth>();
                if (!IsValidCursorTarget(candidateHealth))
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestTarget = candidateHealth;
                }
            }

            return nearestTarget != null;
        }

        private bool IsValidCursorTarget(UnitHealth candidateHealth)
        {
            if (candidateHealth == null || candidateHealth.IsDead)
            {
                return false;
            }

            if (!requireHostileTargetsForCursorAndClick)
            {
                return true;
            }

            Unit candidateUnit = candidateHealth.GetComponentInParent<Unit>();
            return IsValidEncounterTarget(candidateUnit);
        }
    }
}