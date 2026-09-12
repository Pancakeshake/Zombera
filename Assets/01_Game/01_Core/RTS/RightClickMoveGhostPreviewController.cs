#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Systems
{
    public readonly struct GroupMovePreviewCommit
    {
        public Vector3 Destination { get; }
        public IReadOnlyList<SquadMoveDeconfliction.GroupMoveAssignment> Assignments { get; }

        public GroupMovePreviewCommit(
            Vector3 destination,
            IReadOnlyList<SquadMoveDeconfliction.GroupMoveAssignment> assignments)
        {
            Destination = destination;
            Assignments = assignments;
        }

        public bool HasAssignments => Assignments != null && Assignments.Count > 0;
    }

    /// <summary>
    ///     Hold-to-preview / release-to-commit right-click move input with pooled ghost markers.
    /// </summary>
    public sealed class RightClickMoveGhostPreviewController : MonoBehaviour
    {
        public delegate bool TryGetGroundPointDelegate(Vector2 screenPosition, out Vector3 groundPoint);

        public enum PreviewState
        {
            Idle,
            PressedPending,
            PreviewActive,
            CommitPending,
            Cancelled
        }

        [SerializeField] [Min(0f)] private float holdToPreviewThresholdSeconds = 0.1f;
        [SerializeField] [Min(0.02f)] private float previewUpdateIntervalSeconds = 0.04f;
        [SerializeField] [Min(1)] private int maxVisualizedGhosts = 64;
        [SerializeField] private bool debugSlots;

        private readonly List<SquadMoveDeconfliction.GroupMoveAssignment> _assignmentBuffer = new(32);

        private PreviewState _state = PreviewState.Idle;
        private float _pressStartedAt;
        private float _lastPreviewUpdateAt;
        private Vector3 _previewDestination;
        private Vector3 _previewForward = Vector3.forward;
        private bool _hasValidPreviewDestination;
        private UnitHealth _pressedHostileTarget;
        private bool _pressedOverHostileTarget;

        private RightClickMoveGhostPool _ghostPool;
        private Transform _poolRoot;

        private Func<bool> _isRtsInputActive;
        private Func<bool> _hasSelection;
        private Func<Vector2> _readPointerPosition;
        private Func<Vector2, bool> _isPointerOverUi;
        private TryGetGroundPointDelegate _tryGetGroundPoint;
        private Func<Vector2, UnitHealth> _tryGetHostileTarget;
        private Func<IReadOnlyList<SquadMember>> _getSelectedMembers;
        private Func<IReadOnlyList<SquadMember>, Vector3, List<SquadMoveDeconfliction.GroupMoveAssignment>, bool>
            _tryResolvePreviewAssignments;

        public PreviewState State => _state;
        public IReadOnlyList<SquadMoveDeconfliction.GroupMoveAssignment> LastPreviewAssignments => _assignmentBuffer;
        public event Action<GroupMovePreviewCommit> PreviewCommitMove;
        public event Action<UnitHealth> PreviewCommitAttack;
        public event Action PreviewCancelled;

        private void OnDisable()
        {
            CancelPreview(PreviewState.Cancelled);
        }

        private void OnDestroy()
        {
            _ghostPool?.Dispose();
            if (_poolRoot != null)
                Destroy(_poolRoot.gameObject);
        }

        public void Configure(
            Func<bool> isRtsInputActive,
            Func<bool> hasSelection,
            Func<Vector2> readPointerPosition,
            Func<Vector2, bool> isPointerOverUi,
            TryGetGroundPointDelegate tryGetGroundPoint,
            Func<Vector2, UnitHealth> tryGetHostileTarget,
            Func<IReadOnlyList<SquadMember>> getSelectedMembers,
            Func<IReadOnlyList<SquadMember>, Vector3, List<SquadMoveDeconfliction.GroupMoveAssignment>, bool>
                tryResolvePreviewAssignments)
        {
            _isRtsInputActive = isRtsInputActive;
            _hasSelection = hasSelection;
            _readPointerPosition = readPointerPosition;
            _isPointerOverUi = isPointerOverUi;
            _tryGetGroundPoint = tryGetGroundPoint;
            _tryGetHostileTarget = tryGetHostileTarget;
            _getSelectedMembers = getSelectedMembers;
            _tryResolvePreviewAssignments = tryResolvePreviewAssignments;
            EnsurePool();
        }

        public void ForceCancel()
        {
            CancelPreview(PreviewState.Cancelled);
        }

        public void Tick(bool rightPressedThisFrame, bool rightReleasedThisFrame, bool rightHeld)
        {
            if (_isRtsInputActive == null) return;

            if (_state != PreviewState.Idle && !_isRtsInputActive())
            {
                CancelPreview(PreviewState.Cancelled);
                return;
            }

            if (!rightHeld && !rightPressedThisFrame && !rightReleasedThisFrame &&
                _state is PreviewState.Idle or PreviewState.Cancelled)
                return;

            if (rightPressedThisFrame)
                BeginPress();

            if (_state is PreviewState.PressedPending or PreviewState.PreviewActive)
            {
                if (!CanContinuePreview())
                {
                    CancelPreview(PreviewState.Cancelled);
                    return;
                }

                if (_state == PreviewState.PressedPending &&
                    rightHeld &&
                    Time.unscaledTime - _pressStartedAt >= holdToPreviewThresholdSeconds)
                    EnterPreviewActive();

                if (_state == PreviewState.PreviewActive && rightHeld)
                    UpdatePreviewIfDue();
            }

            if (rightReleasedThisFrame)
                HandleRelease();
        }

        private void BeginPress()
        {
            if (!_isRtsInputActive() || !_hasSelection()) return;

            var pointer = _readPointerPosition();
            if (_isPointerOverUi(pointer)) return;

            _pressedHostileTarget = _tryGetHostileTarget(pointer);
            _pressedOverHostileTarget = _pressedHostileTarget != null;
            _pressStartedAt = Time.unscaledTime;
            _lastPreviewUpdateAt = 0f;
            _hasValidPreviewDestination = false;
            _state = PreviewState.PressedPending;
        }

        private bool CanContinuePreview()
        {
            return _isRtsInputActive() && _hasSelection();
        }

        private void EnterPreviewActive()
        {
            _state = PreviewState.PreviewActive;
            UpdatePreviewIfDue(force: true);
        }

        private void UpdatePreviewIfDue(bool force = false)
        {
            if (!force && Time.unscaledTime - _lastPreviewUpdateAt < previewUpdateIntervalSeconds) return;

            _lastPreviewUpdateAt = Time.unscaledTime;

            var pointer = _readPointerPosition();
            if (_isPointerOverUi(pointer))
            {
                _hasValidPreviewDestination = false;
                _ghostPool?.HideAll();
                return;
            }

            if (!_tryGetGroundPoint(pointer, out var groundPoint))
            {
                _hasValidPreviewDestination = false;
                RenderGhosts(Array.Empty<SquadMoveDeconfliction.GroupMoveAssignment>(), false);
                return;
            }

            _previewDestination = groundPoint;
            _hasValidPreviewDestination = true;

            var members = _getSelectedMembers();
            if (members == null || members.Count == 0 || _tryResolvePreviewAssignments == null)
            {
                _hasValidPreviewDestination = false;
                _ghostPool?.HideAll();
                return;
            }

            _assignmentBuffer.Clear();
            if (!_tryResolvePreviewAssignments(members, groundPoint, _assignmentBuffer))
            {
                _hasValidPreviewDestination = false;
                RenderGhosts(Array.Empty<SquadMoveDeconfliction.GroupMoveAssignment>(), false);
                return;
            }

            _previewForward = ResolveGroupForward(members, groundPoint);
            RenderGhosts(_assignmentBuffer, true);
        }

        private void HandleRelease()
        {
            if (_state == PreviewState.Idle) return;

            if (_state == PreviewState.PressedPending)
            {
                if (_pressedOverHostileTarget && _pressedHostileTarget != null && !_pressedHostileTarget.IsDead)
                {
                    PreviewCommitAttack?.Invoke(_pressedHostileTarget);
                    ResetToIdle();
                    return;
                }

                var pointer = _readPointerPosition();
                if (!_isPointerOverUi(pointer) && _tryGetGroundPoint(pointer, out var tapGround))
                {
                    PreviewCommitMove?.Invoke(new GroupMovePreviewCommit(tapGround, null));
                    ResetToIdle();
                    return;
                }

                CancelPreview(PreviewState.Cancelled);
                return;
            }

            if (_state == PreviewState.PreviewActive)
            {
                if (_hasValidPreviewDestination && CanContinuePreview() && _assignmentBuffer.Count > 0)
                {
                    PreviewCommitMove?.Invoke(
                        new GroupMovePreviewCommit(_previewDestination, _assignmentBuffer));
                }
                else
                    PreviewCancelled?.Invoke();

                ResetToIdle();
            }
        }

        private void CancelPreview(PreviewState terminalState)
        {
            if (_state == PreviewState.Idle) return;

            _ghostPool?.HideAll();
            if (terminalState == PreviewState.Cancelled)
                PreviewCancelled?.Invoke();

            ResetToIdle();
        }

        private void ResetToIdle()
        {
            _state = PreviewState.Idle;
            _pressedHostileTarget = null;
            _pressedOverHostileTarget = false;
            _hasValidPreviewDestination = false;
            _ghostPool?.HideAll();
        }

        private void RenderGhosts(
            IReadOnlyList<SquadMoveDeconfliction.GroupMoveAssignment> assignments,
            bool validDestination)
        {
            EnsurePool();
            if (_ghostPool == null) return;

            var count = assignments != null ? assignments.Count : 0;
            var visibleCount = Mathf.Min(count, maxVisualizedGhosts);
            _ghostPool.EnsureCapacity(visibleCount, maxVisualizedGhosts);

            for (var i = 0; i < visibleCount; i++)
            {
                var assignment = assignments[i];
                _ghostPool.SetGhost(i, assignment.Destination, _previewForward, validDestination);
            }

            if (debugSlots && validDestination)
                Debug.Log($"[RightClickMoveGhostPreview] slots={visibleCount} dest={_previewDestination}");
        }

        private void EnsurePool()
        {
            if (_ghostPool != null) return;

            var poolObject = new GameObject("RightClickMoveGhostPool");
            poolObject.transform.SetParent(transform, false);
            _poolRoot = poolObject.transform;
            _ghostPool = new RightClickMoveGhostPool(_poolRoot);
        }

        private static Vector3 ResolveGroupForward(IReadOnlyList<SquadMember> members, Vector3 destination)
        {
            if (members == null || members.Count == 0) return Vector3.forward;

            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member?.UnitController == null) continue;

                var toTarget = destination - member.UnitController.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f) return toTarget.normalized;
            }

            return Vector3.forward;
        }
    }
}
