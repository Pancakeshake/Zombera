using UnityEngine;
using Zombera.Characters;

namespace Zombera.AI.Actions
{
    /// <summary>
    /// Reusable movement action adapter.
    /// Converts high-level brain intents into UnitController commands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MoveAction : MonoBehaviour
    {
        [SerializeField] private UnitController unitController;
        [SerializeField] private float retreatDistance = 5f;

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

        // TODO: Route movement through NavMesh/pathfinding service when available.
        // TODO: Add movement request priority and cancellation handling.
    }
}