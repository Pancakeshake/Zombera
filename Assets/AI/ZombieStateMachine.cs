using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Systems;

namespace Zombera.AI
{
    /// <summary>
    /// Handles zombie state transitions: Idle, Wander, Investigate, Chase, Attack, CallHorde.
    /// </summary>
    public sealed class ZombieStateMachine : MonoBehaviour
    {
        [SerializeField] private float attackRange = 1f;
        [SerializeField] private float abandonChaseRange = 25f;
        [SerializeField, Min(1f)] private float attackExitRangeMultiplier = 1.1f;

        [Header("Duel Stance")]
        [Tooltip("Distance zombies without the attack token orbit around the target.")]
        [SerializeField] private float duelOrbitRadius = 1.6f;
        [Tooltip("How fast non-attacker zombies circle the target (degrees per second).")]
        [SerializeField] private float orbitSpeed = 45f;

        [Header("Investigate")]
        [SerializeField] private float investigateDuration = 4f;

        [Header("Call Horde")]
        [SerializeField, Min(1f)] private float callHordeRadius = 12f;

        [Header("Chase Timeout")]
        [SerializeField, Min(1f)] private float chaseTimeoutSeconds = 12f;

        [Header("Hit Stun")]
        [SerializeField, Min(0f)] private float maxCombatStunSeconds = 1.2f;

        [Header("Door Breaking")]
        [Tooltip("Radius to scan for a DoorHealth when the Nav path is blocked.")]
        [SerializeField] private float doorDetectRadius = 5f;
        [Tooltip("Damage applied to the door each swing.")]
        [SerializeField, Min(1f)] private float doorDamagePerSwing = 15f;
        [Tooltip("Seconds between door swings.")]
        [SerializeField, Min(0.1f)] private float doorSwingInterval = 0.8f;

        [Header("Spawn")]
        [Tooltip("Duration to stay in Spawn state before transitioning to Idle.")]
        [SerializeField, Min(0f)] private float spawnDurationSeconds = 1.2f;

        public ZombieState CurrentState { get; private set; } = ZombieState.Idle;

        private UnitController _unitController;
        private NavMeshAgent _navMeshAgent;
        private Unit _selfUnit;
        private ZombieAnimationController _zombieAnim;
        private CombatEncounterManager _encounterManager;
        private float _idleEndTime;
        private Vector3 _wanderTarget;
        private bool _hasWanderTarget;
        private Unit _chaseTarget;
        private float _orbitAngle;
        private bool _hasAttackSlot;
        private float _chaseStartTime;
        private float _combatStunExpiresAt;
        private Vector3 _investigatePoint;
        private float _investigateEndTime;
        private readonly List<Unit> _allyBuffer = new List<Unit>(8);
        // Door-breaking state
        private DoorHealth _targetDoor;
        private float _nextDoorSwingTime;
        private float _spawnEndTime;

        private void Awake()
        {
            _unitController = GetComponent<UnitController>();
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _selfUnit = GetComponent<Unit>();
            _zombieAnim = GetComponent<ZombieAnimationController>();
        }

        private CombatEncounterManager EncounterManager
        {
            get
            {
                if (_encounterManager == null)
                {
                    _encounterManager = CombatEncounterManager.Instance != null
                        ? CombatEncounterManager.Instance
                        : FindFirstObjectByType<CombatEncounterManager>();
                }

                return _encounterManager;
            }
        }

        public void SetChaseTarget(Unit target)
        {
            _chaseTarget = target;
        }

        public void SetInvestigateTarget(Vector3 worldPoint)
        {
            _investigatePoint = worldPoint;
        }

        public void SetState(ZombieState newState)
        {
            ExitState(CurrentState);
            CurrentState = newState;
            EnterState(CurrentState);
        }

        public void TickStateMachine()
        {
            if (Time.time < _combatStunExpiresAt)
            {
                _unitController?.Stop();
                return;
            }

            switch (CurrentState)
            {
                case ZombieState.Idle:
                    TickIdle();
                    break;
                case ZombieState.Spawn:
                    TickSpawn();
                    break;
                case ZombieState.Wander:
                    TickWander();
                    break;
                case ZombieState.Investigate:
                    TickInvestigate();
                    break;
                case ZombieState.Chase:
                    TickChase();
                    break;
                case ZombieState.Attack:
                    TickAttack();
                    break;
                case ZombieState.CallHorde:
                    TickCallHorde();
                    break;
                case ZombieState.AttackDoor:
                    TickAttackDoor();
                    break;
            }
        }

        private void EnterState(ZombieState state)
        {
            switch (state)
            {
                case ZombieState.Idle:
                    _idleEndTime = Time.time + Random.Range(1f, 3f);
                    _unitController?.Stop();
                    break;
                case ZombieState.Spawn:
                    _spawnEndTime = Time.time + spawnDurationSeconds;
                    _unitController?.Stop();
                    _zombieAnim?.TriggerSpawnAnim();
                    break;
                case ZombieState.Wander:
                    _hasWanderTarget = false;
                    break;
                case ZombieState.Investigate:
                    _investigateEndTime = Time.time + investigateDuration;
                    _unitController?.MoveTo(_investigatePoint);
                    break;
                case ZombieState.Chase:
                    _hasWanderTarget = false;
                    _chaseStartTime = Time.time;
                    break;
                case ZombieState.Attack:
                    _unitController?.Stop();
                    break;
                case ZombieState.AttackDoor:
                    _unitController?.Stop();
                    _nextDoorSwingTime = Time.time + 0.15f;
                    break;
            }
        }

        private void ExitState(ZombieState state)
        {
            if (state == ZombieState.Attack)
            {
                ReleaseAttackSlot();
            }
            if (state == ZombieState.AttackDoor)
            {
                _targetDoor = null;
            }
        }

        // ── Attack slot helpers ───────────────────────────────────────────────

        private ZombieAttackSlotManager SlotManager
        {
            get
            {
                if (ZombieAttackSlotManager.Instance != null)
                    return ZombieAttackSlotManager.Instance;

                // Auto-create if missing — keeps setup friction low.
                GameObject host = new GameObject("ZombieAttackSlotManager");
                return host.AddComponent<ZombieAttackSlotManager>();
            }
        }

        private bool TryAcquireAttackSlot()
        {
            _hasAttackSlot = SlotManager.RequestSlot(this);
            return _hasAttackSlot;
        }

        public void ReleaseAttackSlot()
        {
            if (_hasAttackSlot)
            {
                SlotManager.ReleaseSlot(this);
                _hasAttackSlot = false;
            }
        }

        public void ApplyCombatStun(float durationSeconds)
        {
            float clampedDuration = Mathf.Clamp(durationSeconds, 0f, Mathf.Max(0f, maxCombatStunSeconds));
            if (clampedDuration <= 0f)
            {
                return;
            }

            _combatStunExpiresAt = Mathf.Max(_combatStunExpiresAt, Time.time + clampedDuration);
            _unitController?.Stop();
            ReleaseAttackSlot();
        }

        private void TickIdle()
        {
            if (Time.time >= _idleEndTime)
            {
                SetState(ZombieState.Wander);
            }
        }

        private void TickSpawn()
        {
            if (Time.time >= _spawnEndTime)
                SetState(ZombieState.Idle);
        }

        private void TickWander()
        {
            if (_unitController == null) return;

            if (_hasWanderTarget)
            {
                Vector3 toTarget = _wanderTarget - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 1f)
                {
                    SetState(ZombieState.Idle);
                    return;
                }
                // Re-issue each tick so the agent/transform fallback stays active.
                _unitController.MoveTo(_wanderTarget);
                return;
            }

            // Pick a new random wander point 3–8 m away.
            Vector2 randomOffset = Random.insideUnitCircle.normalized * Random.Range(3f, 8f);
            Vector3 candidate = transform.position + new Vector3(randomOffset.x, 0f, randomOffset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                candidate = hit.position;
            }

            _wanderTarget = candidate;
            _hasWanderTarget = true;
            _unitController.MoveTo(_wanderTarget);
        }

        private void TickInvestigate()
        {
            if (_unitController == null) return;

            _unitController.MoveTo(_investigatePoint);

            float distSqr = (_investigatePoint - transform.position).sqrMagnitude;
            if (distSqr <= 1f || Time.time >= _investigateEndTime)
            {
                SetState(ZombieState.Wander);
            }
        }

        private void TickChase()
        {
            if (_unitController == null) return;

            // Chase timeout: if we've been chasing too long without closing in, investigate the last known position.
            if (Time.time - _chaseStartTime > chaseTimeoutSeconds)
            {
                if (_chaseTarget != null)
                {
                    SetInvestigateTarget(_chaseTarget.transform.position);
                    _chaseTarget = null;
                    SetState(ZombieState.Investigate);
                }
                else
                {
                    SetState(ZombieState.Wander);
                }
                return;
            }

            if (_chaseTarget == null || !_chaseTarget.IsAlive)
            {
                _chaseTarget = null;
                SetState(ZombieState.Wander);
                return;
            }

            float distSqr = (transform.position - _chaseTarget.transform.position).sqrMagnitude;

            if (distSqr > abandonChaseRange * abandonChaseRange)
            {
                SetInvestigateTarget(_chaseTarget.transform.position);
                _chaseTarget = null;
                SetState(ZombieState.Investigate);
                return;
            }

            if (distSqr <= attackRange * attackRange)
            {
                SetState(ZombieState.Attack);
                return;
            }

            _unitController.MoveTo(_chaseTarget.transform.position);

            // After issuing move, check if path is blocked by a door.
            DoorHealth door = FindBlockedDoor(_chaseTarget.transform.position);
            if (door != null)
            {
                _targetDoor = door;
                SetState(ZombieState.AttackDoor);
            }
        }

        private void TickAttackDoor()
        {
            // If the door was destroyed, resume chasing.
            if (_targetDoor == null || _targetDoor.IsDestroyed)
            {
                _targetDoor = null;
                SetState(ZombieState.Chase);
                return;
            }

            Vector3 toDoor = _targetDoor.transform.position - transform.position;
            toDoor.y = 0f;
            float distToDoor = toDoor.magnitude;

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
                Debug.Log($"[DoorBreak] {name} hit door — {_targetDoor.CurrentHealth:F0}/{_targetDoor.MaxHealth:F0} HP");
            }
        }

        /// <summary>
        /// Returns the nearest live, valid DoorHealth within <see cref="doorDetectRadius"/>
        /// that is in the general direction of the enemy.
        /// Uses DoorHealth.All so no physics collider is required on the door object.
        /// </summary>
        private DoorHealth FindBlockedDoor(Vector3 enemyPosition)
        {
            Vector3 origin = transform.position;
            Vector3 toEnemy = enemyPosition - origin;
            toEnemy.y = 0f;
            float enemyDist = toEnemy.magnitude;
            Vector3 toEnemyDir = enemyDist > 0.001f ? toEnemy / enemyDist : Vector3.forward;

            DoorHealth best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < DoorHealth.All.Count; i++)
            {
                DoorHealth door = DoorHealth.All[i];
                if (door == null || door.IsDestroyed || !door.IsValid) continue;

                Vector3 toDoor = door.transform.position - origin;
                toDoor.y = 0f;
                float dist = toDoor.magnitude;
                float dot = dist > 0.001f ? Vector3.Dot(toDoor / dist, toEnemyDir) : 1f;

                if (dot < 0.5f) continue;           // must be roughly toward the enemy
                if (dist >= enemyDist) continue;    // door must be closer than the enemy
                if (dist > doorDetectRadius) continue; // outside scan radius

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = door;
                }
            }

            return best;
        }

        private void TickAttack()
        {
            if (_chaseTarget == null || !_chaseTarget.IsAlive)
            {
                _chaseTarget = null;
                SetState(ZombieState.Wander);
                return;
            }

            float distSqr = (transform.position - _chaseTarget.transform.position).sqrMagnitude;
            float attackExitRange = Mathf.Max(0.1f, attackRange) * Mathf.Max(1f, attackExitRangeMultiplier);
            float attackExitRangeSqr = attackExitRange * attackExitRange;
            float leashRangeSqr = abandonChaseRange * abandonChaseRange;

            if (distSqr > leashRangeSqr)
            {
                SetState(ZombieState.Chase);
                return;
            }

            if (distSqr > attackExitRangeSqr)
            {
                SetState(ZombieState.Chase);
                return;
            }

            CombatEncounterManager encounterManager = EncounterManager;
            if (_selfUnit == null)
            {
                _selfUnit = GetComponent<Unit>();
            }

            bool inEncounter = _selfUnit != null && encounterManager != null && encounterManager.IsUnitInEncounter(_selfUnit);

            if (!inEncounter)
            {
                if (_selfUnit != null && encounterManager != null)
                {
                    encounterManager.TryStartEncounter(_selfUnit, _chaseTarget);
                    inEncounter = encounterManager.IsUnitInEncounter(_selfUnit);
                }

                if (!inEncounter)
                {
                    SetState(ZombieState.Idle);
                    return;
                }
            }

            // Try to hold (or re-check) the attack token each tick.
            if (!_hasAttackSlot)
                TryAcquireAttackSlot();

            if (_hasAttackSlot)
            {
                // Token holder: close to melee range, then stop and face the target.
                float distToTarget = Vector3.Distance(transform.position, _chaseTarget.transform.position);
                if (distToTarget > attackRange * 0.78f)
                {
                    _unitController?.MoveTo(_chaseTarget.transform.position);
                }
                else
                {
                    _unitController?.Stop();
                    if (_unitController != null)
                        _unitController.Rotate(_chaseTarget.transform.position - transform.position);
                }
            }
            else
            {
                // No token: orbit at duel distance so the fight looks alive.
                _orbitAngle += orbitSpeed * Time.deltaTime;
                float rad = _orbitAngle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * duelOrbitRadius;
                Vector3 orbitPos = _chaseTarget.transform.position + offset;
                _unitController?.MoveTo(orbitPos);
            }
        }

        private void TickCallHorde()
        {
            if (_chaseTarget != null && _selfUnit != null && UnitManager.Instance != null)
            {
                List<Unit> allies = UnitManager.Instance.FindNearbyAllies(_selfUnit, callHordeRadius, _allyBuffer);
                for (int i = 0; i < allies.Count; i++)
                {
                    Unit ally = allies[i];
                    if (ally == null) continue;
                    ZombieStateMachine allyMachine = ally.GetComponent<ZombieStateMachine>();
                    if (allyMachine != null
                        && allyMachine.CurrentState != ZombieState.Chase
                        && allyMachine.CurrentState != ZombieState.Attack)
                    {
                        allyMachine.SetChaseTarget(_chaseTarget);
                        allyMachine.SetState(ZombieState.Chase);
                    }
                }
            }

            SetState(ZombieState.Chase);
        }
    }

    public enum ZombieState
    {
        Idle,
        Spawn,
        Wander,
        Investigate,
        Chase,
        Attack,
        CallHorde,
        AttackDoor
    }
}