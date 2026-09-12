#region

using UnityEngine;
using Zombera.BuildingSystem;

#endregion

namespace Zombera.AI
{
    public sealed partial class ZombieStateMachine
    {
        // ── Door Breaking ─────────────────────────────────────────────

        private void TickAttackDoor()
        {
            // If the door was destroyed, resume chasing.
            if (_targetDoor == null || _targetDoor.IsDestroyed)
            {
                _targetDoor = null;
                SetState(ZombieState.Chase);
                return;
            }

            var toDoor = _targetDoor.transform.position - transform.position;
            toDoor.y = 0f;
            var distToDoor = toDoor.magnitude;

            // Move toward the door until within melee range.
            if (distToDoor > attackRange)
            {
                _unitController?.MoveTo(_targetDoor.transform.position);
                return;
            }

            // In range — stop and face the door.
            _unitController?.Stop();
            if (toDoor.sqrMagnitude > 0.001f && _unitController != null)
                _unitController.Rotate(toDoor);

            // Swing on interval.
            if (Time.time >= _nextDoorSwingTime)
            {
                _nextDoorSwingTime = Time.time + doorSwingInterval;
                _zombieAnim?.TriggerAttackAnim();
                _targetDoor.TakeDamage(doorDamagePerSwing, gameObject);
                Debug.Log(
                    $"[DoorBreak] {name} hit door — {_targetDoor.CurrentHealth:F0}/{_targetDoor.MaxHealth:F0} HP");
            }
        }

        private DoorHealth FindBlockedDoor(Vector3 enemyPosition)
        {
            var origin = transform.position;
            var toEnemy = enemyPosition - origin;
            toEnemy.y = 0f;
            var enemyDist = toEnemy.magnitude;
            var toEnemyDir = enemyDist > 0.001f ? toEnemy / enemyDist : Vector3.forward;

            DoorHealth best = null;
            var bestDist = float.MaxValue;

            DoorHealth.FindNearbyDoors(origin, doorDetectRadius, _nearbyDoorsBuffer);

            foreach (var door in _nearbyDoorsBuffer)
            {
                // door filtering already done in FindNearbyDoors (destroyed, valid)
                var toDoor = door.transform.position - origin;
                toDoor.y = 0f;
                var dist = toDoor.magnitude;
                var dot = dist > 0.001f ? Vector3.Dot(toDoor / dist, toEnemyDir) : 1f;

                if (dot < 0.5f) continue; // must be roughly toward the enemy
                if (dist >= enemyDist) continue; // door must be closer than the enemy
                if (dist > doorDetectRadius) continue; // outside scan radius

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = door;
                }
            }

            return best;
        }
    }
}
