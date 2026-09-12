#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Systems;

#endregion

// ReSharper disable Unity.PerformanceCriticalCodeInvocation
// ReSharper disable Unity.PerformanceCriticalCodeNullComparison
// ReSharper disable InvertIf
// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable RedundantEmptySwitchSection

namespace Zombera.AI
{
    /// <summary>
    ///     Handles zombie state transitions: Idle, Wander, Investigate, Chase, Attack, CallHorde.
    /// </summary>
    public sealed partial class ZombieStateMachine : MonoBehaviour
    {
        [SerializeField] private float attackRange = 1f;
        [SerializeField] private float abandonChaseRange = 25f;
        [SerializeField] [Min(1f)] private float attackExitRangeMultiplier = 1.1f;

        [Header("Duel Stance")]
        [Tooltip("Distance zombies without the attack token orbit around the target.")]
        [SerializeField]
        private float duelOrbitRadius = 1.6f;

        [Tooltip("How fast non-attacker zombies circle the target (degrees per second).")] [SerializeField]
        private float orbitSpeed = 45f;

        [Header("Investigate")] [SerializeField]
        private float investigateDuration = 4f;

        [Header("Call Horde")] [SerializeField] [Min(1f)]
        private float callHordeRadius = 12f;

        [Header("Chase Timeout")] [SerializeField] [Min(1f)]
        private float chaseTimeoutSeconds = 12f;

        [Header("Hit Stun")] [SerializeField] [Min(0f)]
        private float maxCombatStunSeconds = 1.2f;

        [Header("Door Breaking")]
        [Tooltip("Radius to scan for a DoorHealth when the Nav path is blocked.")]
        [SerializeField]
        private float doorDetectRadius = 5f;

        [Tooltip("Damage applied to the door each swing.")] [SerializeField] [Min(1f)]
        private float doorDamagePerSwing = 15f;

        [Tooltip("Seconds between door swings.")] [SerializeField] [Min(0.1f)]
        private float doorSwingInterval = 0.8f;

        [Header("Wander")]
        [Tooltip("Abandon a wander target and return to Idle after this many seconds without reaching it (guards against stuck-on-rejected-destination loops).")]
        [SerializeField]
        [Min(1f)]
        private float wanderTargetTimeoutSeconds = 4f;

        [Header("Spawn")]
        [Tooltip("Duration to stay in Spawn state before transitioning to Idle.")]
        [SerializeField]
        [Min(0f)]
        private float spawnDurationSeconds = 1.2f;

        private readonly List<Unit> _allyBuffer = new(8);

        /// <summary>
        ///     Returns the nearest live, valid DoorHealth within <see cref="doorDetectRadius" />
        ///     that is in the general direction of the enemy.
        ///     Uses DoorHealth.All so no physics collider is required on the door object.
        /// </summary>
        private readonly List<DoorHealth> _nearbyDoorsBuffer = new();

        private float _chaseStartTime;
        private Unit _chaseTarget;
        private float _combatStunExpiresAt;
        private CombatEncounterManager _encounterManager;
        private bool _hasAttackSlot;
        private bool _hasWanderTarget;
        private float _idleEndTime;
        private float _investigateEndTime;
        private Vector3 _investigatePoint;
        private float _nextDoorSwingTime;
        private float _orbitAngle;
        private Unit _selfUnit;

        private float _spawnEndTime;
        private float _wanderTargetSetTime;
        private float _nextWanderMoveReissue;

        // Door-breaking state
        private DoorHealth _targetDoor;

        private UnitController _unitController;
        private Vector3 _wanderTarget;
        private ZombieAnimationController _zombieAnim;

        public ZombieState CurrentState { get; private set; } = ZombieState.Idle;

        private CombatEncounterManager EncounterManager
        {
            get
            {
                if (_encounterManager == null)
                    _encounterManager = CombatEncounterManager.Instance != null
                        ? CombatEncounterManager.Instance
                        : FindFirstObjectByType<CombatEncounterManager>();

                return _encounterManager;
            }
        }

        // ── Attack slot helpers ───────────────────────────────────────────────

        private static ZombieAttackSlotManager SlotManager
        {
            get
            {
                if (ZombieAttackSlotManager.Instance != null)
                    return ZombieAttackSlotManager.Instance;

                // Auto-create if missing — keeps setup friction low.
                var host = new GameObject("ZombieAttackSlotManager");
                return host.AddComponent<ZombieAttackSlotManager>();
            }
        }

        private void Awake()
        {
            _unitController = GetComponent<UnitController>();
            _selfUnit = GetComponent<Unit>();
            _zombieAnim = GetComponent<ZombieAnimationController>();
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
                default:
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
                case ZombieState.CallHorde:
                    _unitController?.Stop();
                    break;
                case ZombieState.AttackDoor:
                    _unitController?.Stop();
                    _nextDoorSwingTime = Time.time + 0.15f;
                    break;
                default:
                    break;
            }
        }

        private void ExitState(ZombieState state)
        {
            if (state == ZombieState.Attack) ReleaseAttackSlot();
            if (state == ZombieState.AttackDoor) _targetDoor = null;
        }

        private void TickIdle()
        {
            if (Time.time >= _idleEndTime) SetState(ZombieState.Wander);
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
                var toTarget = _wanderTarget - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 1f)
                {
                    SetState(ZombieState.Idle);
                    return;
                }

                // Abandon the target if we've been stuck too long (agent off NavMesh or destination rejected).
                if (Time.time > _wanderTargetSetTime + wanderTargetTimeoutSeconds)
                {
                    _hasWanderTarget = false;
                    SetState(ZombieState.Idle);
                    return;
                }

                // Re-issue periodically rather than every tick so the transform fallback stays active
                // without generating a rejected-destination warning every frame.
                if (Time.time >= _nextWanderMoveReissue)
                {
                    _unitController.MoveTo(_wanderTarget);
                    _nextWanderMoveReissue = Time.time + 0.5f;
                }

                return;
            }

            // Pick a new random wander point 3–8 m away.
            var randomOffset = Random.insideUnitCircle.normalized * Random.Range(3f, 8f);
            var candidate = transform.position + new Vector3(randomOffset.x, 0f, randomOffset.y);

            if (NavMesh.SamplePosition(candidate, out var hit, 5f, NavMesh.AllAreas)) candidate = hit.position;

            _wanderTarget = candidate;
            _wanderTargetSetTime = Time.time;
            _nextWanderMoveReissue = 0f;
            _hasWanderTarget = true;
            _unitController.MoveTo(_wanderTarget);
        }

        private void TickInvestigate()
        {
            if (_unitController == null) return;

            _unitController.MoveTo(_investigatePoint);

            var distSqr = (_investigatePoint - transform.position).sqrMagnitude;
            if (distSqr <= 1f || Time.time >= _investigateEndTime) SetState(ZombieState.Wander);
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