using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;

namespace Zombera.AI.Actions
{
    /// <summary>
    /// Reusable movement action adapter.
    /// Converts high-level brain intents into UnitController commands.
    /// Includes a NavMesh-aware fallback for destinations that aren't fully reachable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MoveAction : MonoBehaviour
    {
        [SerializeField] private UnitController unitController;
        [SerializeField] private float retreatDistance = 5f;
        [Tooltip("Search radius used when looking for a reachable fallback position near a blocked destination.")]
        [SerializeField, Min(0.5f)] private float navFallbackSampleRadius = 8f;

        public void Initialize(UnitController controller)
        {
            if (controller != null)
            {
                unitController = controller;
            }
            else if (unitController == null)
            {
                unitController = GetComponent<UnitController>();
            }
        }

        public bool ExecuteMove(Vector3 worldPosition)
        {
            if (unitController == null)
            {
                return false;
            }

            unitController.MoveTo(worldPosition);
            return true;
        }

        /// <summary>
        /// Moves toward <paramref name="worldPosition"/>. If the direct NavMesh path is
        /// incomplete (blocked geometry), navigates to the closest reachable edge point instead.
        /// </summary>
        public bool ExecuteMoveWithNavFallback(Vector3 worldPosition)
        {
            if (unitController == null)
            {
                return false;
            }

            Vector3 destination = worldPosition;

            var path = new NavMeshPath();
            bool calculated = NavMesh.CalculatePath(transform.position, worldPosition, NavMesh.AllAreas, path);

            if (!calculated || path.status != NavMeshPathStatus.PathComplete)
            {
                // Path fully or partially blocked — use closest reachable NavMesh point.
                if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, navFallbackSampleRadius, NavMesh.AllAreas))
                {
                    destination = hit.position;
                }
            }

            unitController.MoveTo(destination);
            return true;
        }

        public bool ExecuteWander(Vector3 origin, float radius)
        {
            Vector2 random = Random.insideUnitCircle;

            if (random.sqrMagnitude <= 0.0001f)
            {
                random = Vector2.right;
            }

            random.Normalize();
            Vector3 destination = origin + new Vector3(random.x, 0f, random.y) * Mathf.Max(0.1f, radius);
            return ExecuteMove(destination);
        }

        public Vector3 CalculateRetreatPoint(Vector3 selfPosition, Vector3 threatPosition)
        {
            Vector3 away = selfPosition - threatPosition;
            away.y = 0f;

            if (away.sqrMagnitude <= 0.0001f)
            {
                away = Vector3.back;
            }

            away.Normalize();
            return selfPosition + away * retreatDistance;
        }

        public void StopMovement()
        {
            unitController?.Stop();
        }

        /// <summary>
        /// Cancels any pending move destination.
        /// Higher-priority commands (retreat, flee) call this before issuing a new move.
        /// </summary>
        public void CancelMove()
        {
            unitController?.Stop();
        }

        /// <summary>
        /// Issues a move only if no move is already active or if the new request has
        /// a higher priority.  Priority 0 = lowest (patrol), 10 = highest (flee/retreat).
        /// </summary>
        public bool ExecuteMoveWithPriority(Vector3 worldPosition, int priority)
        {
            if (unitController == null)
            {
                return false;
            }

            // If we are not currently executing a high-priority command, accept the request.
            if (priority >= currentMovePriority || !unitController.HasMoveTarget)
            {
                currentMovePriority = priority;
                return ExecuteMove(worldPosition);
            }

            return false;
        }

        /// <summary>Resets the tracked move priority so lower-priority moves can resume.</summary>
        public void ResetMovePriority()
        {
            currentMovePriority = 0;
        }

        private int currentMovePriority;
    }
}