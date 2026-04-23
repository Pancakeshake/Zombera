using UnityEngine;
using UnityEngine.AI;
using Zombera.AI.Brains;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Data;

namespace Zombera.AI.Brains
{
    /// <summary>
    /// Zombie-specific brain profile.
    /// Prioritizes enemy pursuit and melee pressure.
    /// Chase distance and aggression are boosted by the spawned ZombieType archetype.
    /// </summary>
    public sealed class ZombieBrain : UnitBrain
    {
        [Header("Zombie Priorities")]
        [SerializeField] private float chaseStartDistance = 16f;
        [SerializeField] private float noiseInvestigateWeight = 0.6f;

        [Header("Door Attack")]
        [Tooltip("Radius to scan for closed doors when path is blocked.")]
        [SerializeField] private float doorDetectRadius = 2.0f;

        // Multiplier applied to chase/attack scores — set at spawn from ZombieType.
        private float _aggressionMultiplier = 1f;

        private void Reset()
        {
            SetTickInterval(0.4f);
        }

        /// <summary>Applied by ZombieSpawner via ZombieType.aggressionMultiplier.</summary>
        public void ApplyArchetypeProfile(ZombieType zombieType)
        {
            if (zombieType == null) return;
            _aggressionMultiplier = Mathf.Max(0.1f, zombieType.aggressionMultiplier);
            if (zombieType.perceptionRadius > 0f)
                chaseStartDistance = zombieType.perceptionRadius;
        }

        protected override void ConfigureDefaultRole()
        {
            if (Unit != null)
            {
                Unit.SetRole(UnitRole.Zombie);
            }
        }

        protected override UnitDecision EvaluateDecision(UnitSensorFrame sensorFrame)
        {
            if (sensorFrame.NearestEnemy != null)
            {
                if (sensorFrame.NearestEnemyDistance <= AttackRange)
                {
                    return new UnitDecision
                    {
                        DecisionType = UnitDecisionType.Attack,
                        Score = 1f * _aggressionMultiplier,
                        TargetUnit = sensorFrame.NearestEnemy,
                        TargetPosition = sensorFrame.NearestEnemy.transform.position,
                        Reason = "Zombie enemy within attack range"
                    };
                }

                // If path to enemy is blocked, check for a closed door to attack.
                DoorHealth blockedDoor = FindBlockedDoor(sensorFrame.NearestEnemy.transform.position);
                if (blockedDoor != null)
                {
                    return new UnitDecision
                    {
                        DecisionType = UnitDecisionType.AttackDoor,
                        Score = 0.95f * _aggressionMultiplier,
                        TargetDoor = blockedDoor,
                        TargetPosition = blockedDoor.transform.position,
                        Reason = "Zombie attacking door blocking path to enemy"
                    };
                }

                if (sensorFrame.NearestEnemyDistance <= chaseStartDistance)
                {
                    return new UnitDecision
                    {
                        DecisionType = UnitDecisionType.Chase,
                        Score = 0.9f * _aggressionMultiplier,
                        TargetUnit = sensorFrame.NearestEnemy,
                        TargetPosition = sensorFrame.NearestEnemy.transform.position,
                        Reason = "Zombie enemy detected in chase radius"
                    };
                }
            }

            if (sensorFrame.HasHeardNoise)
            {
                return new UnitDecision
                {
                    DecisionType = UnitDecisionType.Chase,
                    Score = noiseInvestigateWeight * _aggressionMultiplier,
                    TargetPosition = sensorFrame.LastNoisePosition,
                    Reason = "Zombie investigating nearby noise"
                };
            }

            UnitDecision fallback = base.EvaluateDecision(sensorFrame);

            if (fallback.DecisionType == UnitDecisionType.Follow)
            {
                fallback.DecisionType = UnitDecisionType.Wander;
                fallback.Reason = "Zombie fallback converted follow -> wander";
            }

            return fallback;
        }

        /// <summary>
        /// Returns a DoorHealth in melee range whose NavMeshObstacle is active (door closed)
        /// AND whose NavMesh path from this zombie to the enemy target is partial/invalid.
        /// </summary>
        private DoorHealth FindBlockedDoor(Vector3 enemyPosition)
        {
            // Quick check: is the NavMesh path to the enemy actually blocked?
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                NavMeshPath path = new NavMeshPath();
                agent.CalculatePath(enemyPosition, path);
                if (path.status == NavMeshPathStatus.PathComplete)
                    return null; // Path is clear, no need to attack a door.
            }

            // Scan nearby for a DoorHealth whose obstacle is still active.
            Collider[] hits = Physics.OverlapSphere(transform.position, doorDetectRadius);
            foreach (Collider hit in hits)
            {
                DoorHealth door = hit.GetComponent<DoorHealth>();
                if (door == null)
                    door = hit.GetComponentInParent<DoorHealth>();
                if (door != null && !door.IsDestroyed)
                    return door;
            }

            return null;
        }
    }
}