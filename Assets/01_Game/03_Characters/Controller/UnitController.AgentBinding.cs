#region

using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Core;
using Zombera.Systems;

#endregion

namespace Zombera.Characters
{
    public sealed partial class UnitController
    {
        /// <summary>
        ///     Called by PlayerSpawner immediately after spawn when the NavMesh is confirmed
        ///     baked and ready. Configures the agent and force-places it on the surface.
        /// </summary>
        public bool AgentIsOnNavMesh => _agent != null && _agent.enabled && _agent.isOnNavMesh;

        public void ForceEnableAgent()
        {
            TryEnableAgentOnNavMesh();
        }

        /// <summary>
        /// Enables and warps the NavMeshAgent when local NavMesh is ready. Returns true when on-mesh.
        /// </summary>
        public bool TryEnableAgentOnNavMesh()
        {
            LogGroundingState("ForceEnableAgent.Start");
            if (!CanEnableAgentInCurrentGameState()) return false;

            if (_agent == null)
            {
                Debug.LogWarning("[UnitController] ForceEnableAgent: no NavMeshAgent component.", this);
                return false;
            }

            if (!CanAttemptAgentEnableNow()) return false;

            ConfigureAgentForRuntimeMovement();

            if (MovementGroundingSettings.Active.PreferTerrainHeightOverNavMeshAt(transform.position)
                && UnitNavUtils.TryResolveGroundReferenceY(transform.position, out var groundY)
                && transform.position.y - groundY > MovementGroundingSettings.Active.SeamVerticalDeltaThreshold)
            {
                var grounded = transform.position;
                grounded.y = groundY;
                transform.position = grounded;
            }

            if (!UnitNavUtils.PlaceUnitOnNavMesh(gameObject, transform.position, 12f))
            {
                HandleAgentEnableFailure(
                    $"[UnitController] ForceEnableAgent: no nearby NavMesh for {name}. Falling back to transform movement.");
                LogGroundingState("ForceEnableAgent.NoNavMeshFound");
                return false;
            }

            _nextNonPlayerAgentEnableAttemptAt = 0f;
            _loggedNavMeshFallbackWarning = false;
            LogGroundingState("ForceEnableAgent.Success");
            return AgentIsOnNavMesh;
        }

        private static bool CanEnableAgentInCurrentGameState()
        {
            // World scene objects stay loaded while MainMenu / Booting run; NavMesh is not ready yet.
            // Skip agent placement until a real world session state (same gate as PlayerSpawner).
            var gm = GameManagerGateway.Instance;
            if (gm == null) return true;

            var state = gm.CurrentState;
            return state is GameState.LoadingWorld or GameState.Playing or GameState.Paused;
        }

        private void ConfigureAgentForRuntimeMovement()
        {
            _agent.speed = moveSpeed;
            _agent.stoppingDistance = stoppingDistance;
            _agent.angularSpeed = rotationSpeed * 10f;
            _baselineAgentAcceleration = 20f;
            _agent.acceleration = _baselineAgentAcceleration;
            _agent.autoBraking = true;

            SyncNavMeshAgentDimensionsFromCapsule();
            _agent.updatePosition = true;
            _agent.updateRotation = true;
        }

        private void SyncNavMeshAgentDimensionsFromCapsule()
        {
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                _agent.radius = navMeshRadius;
                _agent.height = navMeshHeight;
                _agent.baseOffset = navMeshBaseOffset;
                return;
            }

            _agent.radius = navMeshRadius > 0f ? navMeshRadius : capsule.radius;
            _agent.height = navMeshHeight > 0f ? navMeshHeight : capsule.height;

            if (Mathf.Abs(navMeshBaseOffset) > 0.0001f)
            {
                _agent.baseOffset = navMeshBaseOffset;
                return;
            }

            // Align agent cylinder base with capsule bottom at the character pivot.
            _agent.baseOffset = capsule.center.y - capsule.height * 0.5f;
        }

        private void HandleAgentEnableFailure(string message)
        {
            if (_agent != null && _agent.enabled) _agent.enabled = false;

            if (_loggedNavMeshFallbackWarning) return;

            _loggedNavMeshFallbackWarning = true;
            LogNavMeshFallback(message);
        }

        private void LogNavMeshFallback(string message)
        {
            if (IsPlayerLikeRole())
            {
                Debug.LogWarning(message, this);
                return;
            }

            if (_nonPlayerNavMeshFallbackLogCount >= MaxNonPlayerNavMeshFallbackLogs) return;

            _nonPlayerNavMeshFallbackLogCount++;
            Debug.Log(message, this);
        }

        private bool CanAttemptAgentEnableNow()
        {
            if (!IsLocalNavMeshReadyForAgentBinding()) return false;

            if (IsPlayerLikeRole()) return true;

            var now = Time.unscaledTime;
            if (now < _nextNonPlayerAgentEnableAttemptAt) return false;

            _nextNonPlayerAgentEnableAttemptAt = now + Mathf.Max(0.1f, nonPlayerAgentEnableRetrySeconds);
            return true;
        }

        private static INavMeshTileReadiness s_cachedNavMeshService;
        private static float s_nextNavMeshServiceLookupAt;

        private bool IsLocalNavMeshReadyForAgentBinding()
        {
            var pos = transform.position;
            var navMeshService = ResolveNavMeshService();
            if (navMeshService is MonoBehaviour behaviour && behaviour.isActiveAndEnabled)
                return navMeshService.IsNavMeshReadyNear(pos);

            return UnitNavUtils.IsNavMeshReadyAt(pos);
        }

        private static INavMeshTileReadiness ResolveNavMeshService()
        {
            var now = Time.unscaledTime;
            if (s_cachedNavMeshService != null && now < s_nextNavMeshServiceLookupAt)
                return s_cachedNavMeshService;

            s_nextNavMeshServiceLookupAt = now + 2f;
            s_cachedNavMeshService = null;

            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is not INavMeshTileReadiness readiness) continue;

                s_cachedNavMeshService = readiness;
                break;
            }

            return s_cachedNavMeshService;
        }

        private bool IsPlayerLikeRole()
        {
            return role is UnitRole.Player or UnitRole.SquadMember or UnitRole.Survivor;
        }

        private IEnumerator FallbackEnableAgent()
        {
            // Give the NavMesh a couple of frames to settle before the first attempt.
            yield return null;
            yield return null;

            var elapsed = 0f;
            const float retryInterval = 0.5f;
            const float timeout = 10f;

            while (elapsed < timeout && _agent != null && !_agent.isOnNavMesh)
            {
                ForceEnableAgent();
                if (_agent != null && _agent.isOnNavMesh) yield break;
                yield return new WaitForSeconds(retryInterval);
                elapsed += retryInterval;
            }
        }

        private bool CanUseAgentForMove()
        {
            if (_agent != null && (!_agent.enabled || !_agent.isOnNavMesh))
            {
                if (IsLocalNavMeshReadyForAgentBinding())
                    TryRecoverAgentBinding(false);
            }

            return _agent != null && _agent.enabled && _agent.isOnNavMesh && _agent.gameObject.activeInHierarchy;
        }

        private void TryRecoverAgentBinding(bool forceImmediate = false)
        {
            if (_agent == null || (_agent.enabled && _agent.isOnNavMesh)) return;
            if (!CanEnableAgentInCurrentGameState()) return;
            if (!IsLocalNavMeshReadyForAgentBinding()) return;

            var now = Time.unscaledTime;
            if (!forceImmediate && now < _nextAgentRebindAttemptAt) return;

            _nextAgentRebindAttemptAt = now + Mathf.Max(0.1f, agentRebindRetrySeconds);
            TryEnableAgentOnNavMesh();
        }
    }
}
