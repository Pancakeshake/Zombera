using UnityEngine;
using Zombera.AI.Brains;
using Zombera.BuildingSystem;

namespace Zombera.AI.States
{
    /// <summary>
    /// Brain state that drives a zombie to melee a closed door until it breaks.
    ///
    /// Entered when ZombieBrain detects a DoorHealth in melee range while the
    /// NavMeshObstacle is blocking the path to the target enemy.
    ///
    /// Each brain tick the zombie stops moving, faces the door, and applies damage
    /// at the configured interval. Exits automatically when:
    ///   - The door is destroyed (DoorHealth.IsDestroyed)
    ///   - The door target reference is lost
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackDoorState : MonoBehaviour, IUnitBrainState
    {
        [Tooltip("Damage per swing.")]
        [SerializeField, Min(1f)] private float damagePerSwing = 15f;

        [Tooltip("Seconds between swings.")]
        [SerializeField, Min(0.1f)] private float swingInterval = 0.8f;

        public UnitBrainStateType StateType => UnitBrainStateType.AttackDoor;

        private float _nextSwingTime;

        public void Enter(UnitBrain brain, UnitSensorFrame sensorFrame, UnitDecision decision)
        {
            brain.MoveAction?.StopMovement();
            _nextSwingTime = Time.time + 0.1f; // small delay before first swing
        }

        public void Tick(UnitBrain brain, UnitSensorFrame sensorFrame, UnitDecision decision)
        {
            if (brain == null)
                return;

            DoorHealth door = decision.TargetDoor;

            if (door == null || door.IsDestroyed)
                return;

            // Face the door.
            Vector3 toTarget = door.transform.position - brain.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
                brain.transform.rotation = Quaternion.LookRotation(toTarget);

            // Keep still.
            brain.MoveAction?.StopMovement();

            // Swing.
            if (Time.time >= _nextSwingTime)
            {
                _nextSwingTime = Time.time + swingInterval;
                door.TakeDamage(damagePerSwing, brain.gameObject);
            }
        }

        public void Exit(UnitBrain brain)
        {
            // Nothing to clean up.
        }
    }
}
