#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zombera.Characters;

#endregion

namespace Zombera.Systems
{
    public enum RtsCommandDispatchResult
    {
        None,
        SkippedMissingSquadManager,
        SkippedInactiveMode,
        SkippedPointerUnavailable,
        SkippedNoRightClick,
        SkippedPointerOverUi,
        SkippedNoSelection,
        IssuedAttack,
        IssuedMove,
        SkippedNoValidTarget,
        PreviewActive,
        PreviewCommittedMove,
        PreviewCommittedAttack,
        PreviewCancelled
    }

    /// <summary>
    ///     RTS command router for selected squad units.
    ///     Right-click on ground issues move; right-click on hostile targets issues attack.
    /// </summary>
    public sealed partial class CommandManager : MonoBehaviour
    {
        [Header("References")] [SerializeField] private Camera worldCamera;
        [SerializeField] private SquadManager squadManager;
        [SerializeField] private CommandSystem commandSystem;
        [SerializeField] private PlayerControlModeCoordinator controlModeCoordinator;
        [SerializeField] private RightClickMoveGhostPreviewController ghostPreviewController;

        [Header("Command Query")]
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private bool logMovementGroundingDiagnostics;
        [SerializeField] private LayerMask targetableMask = ~0;
        [SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Collide;
        [SerializeField] [Min(1f)] private float rayDistance = 1000f;

        [Header("Rules")] [SerializeField] private bool requireSelectionForOrders = true;
        [SerializeField] private bool requireHostileTargetsForAttack = true;

        [Header("Hold Preview Move")]
        [SerializeField] private bool enableRightClickHoldGhostMove = true;

        [Header("Input Actions (Optional)")] [SerializeField]
        private InputActionReference rightClickAction;

        [SerializeField] private InputActionReference pointerPositionAction;

        [Header("Hybrid Control")]
        [SerializeField] private bool hybridSingleCharacterAndRtsMode = true;
        [SerializeField] private bool autoSwitchToRtsWhenMultipleSelected = true;
        [SerializeField] [Min(2)] private int rtsCommandSelectionThreshold = 2;
        [SerializeField] private bool requireRtsModifierForCommandMouseInput = false;
        [SerializeField] private bool useAltAsRtsModifierFallback = true;
        [SerializeField] private InputActionReference rtsModifierAction;

        private static readonly List<RaycastResult> UiRaycastResults = new(16);
        private static PointerEventData s_uiPointerEventData;
        private static EventSystem s_uiPointerEventDataOwner;

        private readonly RaycastHit[] _targetHits = new RaycastHit[64];
        private bool _enabledRightClickAction;
        private bool _enabledPointerPositionAction;
        private bool _enabledRtsModifierAction;
        private readonly List<SquadMoveDeconfliction.GroupMoveAssignment> _previewCommitAssignmentCopy = new(32);
        private readonly List<SquadMember> _selectedOrderableMembersBuffer = new(32);

        private bool _ghostPreviewConfigured;

        private void Awake()
        {
            ResolveReferences();
            EnsureGhostPreviewController();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnableOptionalActions();
            ConfigureGhostPreviewIfNeeded();
        }

#pragma warning disable S2325 // Calls DisableOptionalActions() which accesses instance fields (partial: Input.cs)
        private void OnDisable()
        {
            DisableOptionalActions();
        }
#pragma warning restore S2325

        private void Update()
        {
            if (enableRightClickHoldGhostMove)
            {
                _ = DispatchRightClickHoldPreview();
                return;
            }

            _ = DispatchRightClickCommand();
        }

        private RtsCommandDispatchResult DispatchRightClickHoldPreview()
        {
            if (squadManager == null) return RtsCommandDispatchResult.SkippedMissingSquadManager;

            EnsureGhostPreviewController();
            ConfigureGhostPreviewIfNeeded();

            if (!ShouldProcessRtsCommandInput())
            {
                if (ghostPreviewController != null &&
                    ghostPreviewController.State != RightClickMoveGhostPreviewController.PreviewState.Idle)
                    ghostPreviewController.ForceCancel();

                return RtsCommandDispatchResult.SkippedInactiveMode;
            }

            if (!TryReadRightClickState(
                    out var pressedThisFrame,
                    out var releasedThisFrame,
                    out var held))
                return RtsCommandDispatchResult.SkippedNoRightClick;

            ghostPreviewController.Tick(pressedThisFrame, releasedThisFrame, held);

            if (ghostPreviewController.State == RightClickMoveGhostPreviewController.PreviewState.PreviewActive)
                return RtsCommandDispatchResult.PreviewActive;

            return RtsCommandDispatchResult.None;
        }

        private RtsCommandDispatchResult DispatchRightClickCommand()
        {
            if (squadManager == null) return RtsCommandDispatchResult.SkippedMissingSquadManager;
            if (!ShouldProcessRtsCommandInput()) return RtsCommandDispatchResult.SkippedInactiveMode;
            if (!TryReadPointerPosition(out var pointerScreenPosition)) return RtsCommandDispatchResult.SkippedPointerUnavailable;
            if (!TryReadRightClickPressed(out var wasPressed) || !wasPressed)
                return RtsCommandDispatchResult.SkippedNoRightClick;
            if (IsPointerOverUi(pointerScreenPosition)) return RtsCommandDispatchResult.SkippedPointerOverUi;
            if (requireSelectionForOrders && squadManager.SelectedMembers.Count == 0)
                return RtsCommandDispatchResult.SkippedNoSelection;

            if (TryGetTargetUnderPointer(pointerScreenPosition, out var targetHealth))
            {
                return IssueAttackCommand(targetHealth)
                    ? RtsCommandDispatchResult.IssuedAttack
                    : RtsCommandDispatchResult.SkippedNoValidTarget;
            }

            if (!TryGetGroundPoint(pointerScreenPosition, out var groundPoint))
                return RtsCommandDispatchResult.SkippedNoValidTarget;

            return IssueMoveCommand(groundPoint)
                ? RtsCommandDispatchResult.IssuedMove
                : RtsCommandDispatchResult.SkippedNoValidTarget;
        }

        private void EnsureGhostPreviewController()
        {
            if (ghostPreviewController == null)
                ghostPreviewController = GetComponent<RightClickMoveGhostPreviewController>();

            if (ghostPreviewController == null)
                ghostPreviewController = gameObject.AddComponent<RightClickMoveGhostPreviewController>();
        }

        private void ConfigureGhostPreviewIfNeeded()
        {
            if (_ghostPreviewConfigured || ghostPreviewController == null) return;

            ghostPreviewController.PreviewCommitMove += OnPreviewCommitMove;
            ghostPreviewController.PreviewCommitAttack += OnPreviewCommitAttack;
            ghostPreviewController.PreviewCancelled += OnPreviewCancel;

            ghostPreviewController.Configure(
                ShouldProcessRtsCommandInput,
                HasSelectionForOrders,
                ReadPointerPositionOrDefault,
                IsPointerOverUi,
                TryGetGroundPoint,
                TryGetHostileTargetUnderPointer,
                GetSelectedOrderableMembers,
                TryResolvePreviewAssignments);

            _ghostPreviewConfigured = true;
        }

        private void OnPreviewCommitMove(GroupMovePreviewCommit commit)
        {
            if (commit.HasAssignments)
            {
                _previewCommitAssignmentCopy.Clear();
                _previewCommitAssignmentCopy.AddRange(commit.Assignments);
                IssueMoveCommandWithAssignments(_previewCommitAssignmentCopy, commit.Destination);
                return;
            }

            IssueMoveCommand(commit.Destination);
        }

        private void IssueMoveCommandWithAssignments(
            IReadOnlyList<SquadMoveDeconfliction.GroupMoveAssignment> assignments,
            Vector3 destinationForDedup)
        {
            if (assignments == null || assignments.Count == 0) return;

            if (requireSelectionForOrders
                && squadManager.TryIssueMoveAssignmentsToSelection(assignments, destinationForDedup))
                return;

            var resolvedSystem = ResolveCommandSystem();
            if (resolvedSystem == null) return;

            resolvedSystem.ExecuteMoveAssignments(assignments);
        }

#pragma warning disable S2325 // Calls IssueAttackCommand() which accesses squadManager instance field (partial: Commands.cs)
        private void OnPreviewCommitAttack(UnitHealth targetHealth)
        {
            IssueAttackCommand(targetHealth);
        }
#pragma warning restore S2325

        private static void OnPreviewCancel()
        {
            // Preview dismissal is owned by GhostPreviewController; no command routing needed here.
        }

        private bool TryResolvePreviewAssignments(
            IReadOnlyList<SquadMember> members,
            Vector3 destination,
            List<SquadMoveDeconfliction.GroupMoveAssignment> assignmentsOut)
        {
            assignmentsOut?.Clear();
            if (assignmentsOut == null) return false;

            var resolvedCommandSystem = ResolveCommandSystem();
            if (resolvedCommandSystem == null) return false;

            return resolvedCommandSystem.TryResolveGroupMovePreview(members, destination, assignmentsOut);
        }

        private bool HasSelectionForOrders()
        {
            if (squadManager == null) return false;
            if (!requireSelectionForOrders) return true;
            return GetSelectedOrderableMembers().Count > 0;
        }

        private IReadOnlyList<SquadMember> GetSelectedOrderableMembers()
        {
            _selectedOrderableMembersBuffer.Clear();
            if (squadManager == null) return _selectedOrderableMembersBuffer;

            var selectedMembers = squadManager.SelectedMembers;
            for (var i = 0; i < selectedMembers.Count; i++)
            {
                var member = selectedMembers[i];
                if (member != null && member.IsAvailableForOrders())
                    _selectedOrderableMembersBuffer.Add(member);
            }

            return _selectedOrderableMembersBuffer;
        }

#pragma warning disable S2325 // Calls TryReadPointerPosition() which accesses pointerPositionAction instance field (partial: Input.cs)
        private Vector2 ReadPointerPositionOrDefault()
        {
            return TryReadPointerPosition(out var pointer) ? pointer : Vector2.zero;
        }
#pragma warning restore S2325

#pragma warning disable S2325 // Calls TryGetTargetUnderPointer() which accesses instance fields (partial: Commands.cs)
        private UnitHealth TryGetHostileTargetUnderPointer(Vector2 pointerScreenPosition)
        {
            return TryGetTargetUnderPointer(pointerScreenPosition, out var targetHealth) ? targetHealth : null;
        }
#pragma warning restore S2325

        private void ResolveReferences()
        {
            if (squadManager == null)
                squadManager = SquadManager.Instance != null
                    ? SquadManager.Instance
                    : FindFirstObjectByType<SquadManager>();

            if (commandSystem == null)
                commandSystem = FindFirstObjectByType<CommandSystem>(FindObjectsInactive.Include);

            if (worldCamera == null) worldCamera = Camera.main;
            if (controlModeCoordinator == null)
                controlModeCoordinator = PlayerControlModeCoordinator.Instance != null
                    ? PlayerControlModeCoordinator.Instance
                    : FindFirstObjectByType<PlayerControlModeCoordinator>();
        }

        private CommandSystem ResolveCommandSystem()
        {
            if (commandSystem != null) return commandSystem;

            if (squadManager != null)
                commandSystem = squadManager.GetOrEnsureCommandSystem();

            if (commandSystem == null && SquadManager.HasInstance)
                commandSystem = SquadManager.Instance.GetOrEnsureCommandSystem();

            if (commandSystem == null)
                commandSystem = SquadManager.ResolveRuntimeCommandSystem();

            return commandSystem;
        }
    }
}

