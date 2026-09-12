#region

using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Systems;
using Debug = UnityEngine.Debug;

#endregion

namespace Zombera.Characters
{
    public sealed partial class UnitController
    {
        // Retained for compatibility with existing call sites; deprecation can be evaluated in a follow-up PR.
        // ReSharper disable once UnusedMember.Global
        public void SetInputEnabled(bool inputEnabled)
        {
            InputEnabled = inputEnabled;

            if (inputEnabled) return;

            MoveInput = Vector2.zero;

            // Halt any active move input on the NavMeshAgent while input is locked,
            // but leave HasMoveTarget so AI-issued paths remain active.
            if (_agent != null && _agent.isActiveAndEnabled) _agent.velocity = Vector3.zero;
        }

        public void SetRole(UnitRole unitRole)
        {
            role = unitRole;
        }

        // Retained for compatibility with existing call sites; deprecation can be evaluated in a follow-up PR.
        // ReSharper disable once UnusedMember.Global
        public void SetMoveInput(Vector2 input)
        {
            MoveInput = input;

            if (input.sqrMagnitude > 0f) HasMoveTarget = false;

            // Movement input is expected in world-space XZ. Callers (input handlers)
            // are responsible for rotating the vector relative to the active camera
            // before passing it here.
        }

        public void MoveTo(Vector3 worldPosition)
        {
            MoveTo(worldPosition, MoveArrivalProfile.Precise);
        }

        public void MoveTo(Vector3 worldPosition, MoveArrivalProfile arrivalProfile)
        {
            var requestedWorldPosition = worldPosition;
            var shouldLogMoveTo = ShouldLogMoveTo();
            var moveToCaller = shouldLogMoveTo ? ResolveMoveToCaller() : string.Empty;
            var canUseAgent = CanUseAgentForMove();

            if (canUseAgent)
                worldPosition = ResolveNavMeshMoveDestination(worldPosition);

            if (TryIgnoreDuplicateMoveToRequest(worldPosition, moveToCaller, requestedWorldPosition)) return;

            BeginMoveToRequest(worldPosition);
            ApplyMoveArrivalProfile(arrivalProfile);

            if (!canUseAgent)
            {
                TryLogAgentUnavailableMoveToWarning();
                LogMoveToCall(moveToCaller, requestedWorldPosition, worldPosition, "agent-unavailable");
                return;
            }

            TrySetAgentDestination(worldPosition, moveToCaller, requestedWorldPosition);
        }

        private void ApplyMoveArrivalProfile(MoveArrivalProfile arrivalProfile)
        {
            _activeMoveArrivalProfile = arrivalProfile;
            if (_agent == null || !_agent.enabled) return;

            switch (arrivalProfile)
            {
                case MoveArrivalProfile.GroupCommand:
                    _agent.stoppingDistance = Mathf.Max(stoppingDistance, groupMoveStoppingDistance);
                    _agent.acceleration = Mathf.Min(_baselineAgentAcceleration, groupMoveAcceleration);
                    _agent.autoBraking = true;
                    break;
                default:
                    _agent.stoppingDistance = stoppingDistance;
                    _agent.acceleration = _baselineAgentAcceleration;
                    _agent.autoBraking = true;
                    break;
            }
        }

        private float ResolveActiveArrivalTolerance()
        {
            return _activeMoveArrivalProfile == MoveArrivalProfile.GroupCommand
                ? Mathf.Max(groupMoveStoppingDistance, stoppingDistance, 0.35f)
                : Mathf.Max(_agent != null ? _agent.stoppingDistance : stoppingDistance, 0.35f);
        }

        private Vector3 ResolveNavMeshMoveDestination(Vector3 worldPosition)
        {
            var sampleInput = worldPosition;
            sampleInput.y = 0f;

            var previousInput = _lastMoveDestinationSampleInput;
            previousInput.y = 0f;

            var now = Time.unscaledTime;
            if (now - _lastMoveDestinationSampleAt <= 0.10f
                && (sampleInput - previousInput).sqrMagnitude <= 0.04f)
                return _lastMoveDestinationSampleOutput;

            var resolved = worldPosition;
            if (MovementDestinationResolver.TryValidateSlotPosition(worldPosition, worldPosition, out var grounded))
                resolved = grounded;

            _lastMoveDestinationSampleAt = now;
            _lastMoveDestinationSampleInput = worldPosition;
            _lastMoveDestinationSampleOutput = resolved;

            return resolved;
        }

        private bool TryIgnoreDuplicateMoveToRequest(Vector3 worldPosition, string moveToCaller,
            Vector3 requestedWorldPosition)
        {
            // Skip re-issuing the path if the agent is already heading to the same spot.
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || !HasMoveTarget) return false;

            var delta = MoveTarget - worldPosition;
            delta.y = 0f;
            var hasActiveRoute = _agent.hasPath || _agent.pathPending;

            if (delta.sqrMagnitude >= 0.25f || _agent.isStopped || !hasActiveRoute) return false;

            LogMoveToCall(moveToCaller, requestedWorldPosition, worldPosition, "ignored-existing-path");
            return true;
        }

        private void BeginMoveToRequest(Vector3 worldPosition)
        {
            // Movement-state contract: MoveTo requests immediately declare intent and movement activity.
            MoveTarget = worldPosition;
            HasMoveTarget = true;
            IsMoving = true;
        }

        private void TrySetAgentDestination(Vector3 worldPosition, string moveToCaller, Vector3 requestedWorldPosition)
        {
            _loggedMoveToAgentUnavailableWarning = false;
            _agent.isStopped = false;

            bool destinationAccepted;
            try
            {
                destinationAccepted = _agent.SetDestination(worldPosition);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[UnitController] SetDestination failed: {exception.Message}", this);
                destinationAccepted = false;
            }

            if (destinationAccepted)
            {
                _nextAgentRebindAttemptAt = 0f;
                LogMoveToCall(moveToCaller, requestedWorldPosition, worldPosition, "set-destination");
                return;
            }

            if (role is UnitRole.Player or UnitRole.SquadMember or UnitRole.Survivor)
                Debug.LogWarning($"[UnitController] Destination rejected by NavMeshAgent at {worldPosition}.", this);
            LogMoveToCall(moveToCaller, requestedWorldPosition, worldPosition, "destination-rejected");
        }

        private void TryLogAgentUnavailableMoveToWarning()
        {
            if (_loggedMoveToAgentUnavailableWarning) return;

            if (role != UnitRole.Player && role != UnitRole.SquadMember && role != UnitRole.Survivor) return;

            _loggedMoveToAgentUnavailableWarning = true;
            Debug.LogWarning(
                $"[UnitController] MoveTo: agent not on NavMesh for {name}; using transform fallback movement (enabled={_agent?.enabled}, isOnNavMesh={_agent?.isOnNavMesh}).",
                this);
        }

        private bool ShouldLogMoveTo()
        {
            if (!logMoveToCalls) return false;

            return !logMoveToCallsForPlayerOnly || role == UnitRole.Player;
        }

        private void LogMoveToCall(string caller, Vector3 requestedPosition, Vector3 resolvedPosition, string state)
        {
            if (!ShouldLogMoveTo()) return;

            var cooldown = Mathf.Max(0f, moveToLogCooldownSeconds);
            if (cooldown > 0f && Time.unscaledTime < _nextMoveToLogAt) return;

            _nextMoveToLogAt = Time.unscaledTime + cooldown;

            var origin = string.IsNullOrWhiteSpace(caller) ? "unknown" : caller;
            var message =
                $"[UnitController] {name} MoveTo state={state} caller={origin} requested={requestedPosition} resolved={resolvedPosition}";

            if (includeMoveToStackTrace) message += $"\n{new StackTrace(2, false)}";

            Debug.Log(message, this);
        }

        private static string ResolveMoveToCaller()
        {
            var stackTrace = new StackTrace(2, false);
            var frames = stackTrace.GetFrames();
            if (frames == null) return "unknown";

            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                if (method == null) continue;

                var declaringType = method.DeclaringType;
                if (declaringType == null || declaringType == typeof(UnitController)) continue;

                return $"{declaringType.FullName}.{method.Name}";
            }

            return "unknown";
        }

        public void Stop()
        {
            // Movement-state contract: Stop is the authoritative immediate clear for intent and motion flags.
            MoveInput = Vector2.zero;
            HasMoveTarget = false;
            IsMoving = false;
            _desiredMoveDirection = Vector3.zero;
            _activeMoveArrivalProfile = MoveArrivalProfile.Precise;

            if (_agent == null || !_agent.isOnNavMesh) return;

            _agent.stoppingDistance = stoppingDistance;
            _agent.acceleration = _baselineAgentAcceleration;
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        /// <summary>
        ///     Smooth navmesh-constrained displacement over time (e.g. a dodge step).
        ///     Uses <see cref="NavMeshAgent.Move" /> each frame so the character stays on the navmesh
        ///     without interrupting any active path.
        /// </summary>
        public void BeginDodgeStep(Vector3 worldDir, float distance, float durationSeconds)
        {
            if (_agent == null || !_agent.isOnNavMesh || durationSeconds <= 0f) return;
            StopCoroutine(nameof(DodgeStepRoutine)); // cancel any in-flight dodge
            StartCoroutine(DodgeStepRoutine(worldDir.normalized, distance, durationSeconds));
        }

        private IEnumerator DodgeStepRoutine(Vector3 dir, float distance, float duration)
        {
            var elapsed = 0f;
            var speed = distance / duration;
            while (elapsed < duration)
            {
                var dt = Time.deltaTime;
                elapsed += dt;
                if (_agent != null && _agent.isOnNavMesh)
                    _agent.Move(dir * (speed * dt));
                yield return null;
            }
        }
    }
}
