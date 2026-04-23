using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Systems;

namespace Zombera.AI
{
    /// <summary>
    /// Per-zombie AI controller using tick-based updates for performance.
    /// </summary>
    public sealed class ZombieAI : MonoBehaviour
    {
        [SerializeField] private float aiTickInterval = 0.4f;
        [SerializeField] private ZombieStateMachine stateMachine;
        [SerializeField] private UnitHealth unitHealth;
        [SerializeField] private Unit unit;
        [SerializeField] private CombatEncounterManager encounterManager;
        [SerializeField] private float detectionRange = 15f;
        [SerializeField] private float encounterScanRadius = 5f;
        [SerializeField] private float encounterStartRange = 1.45f;
        [SerializeField] private bool useEncounterManagerEngageRange = true;

        [Header("Movement Safety")]
        [SerializeField] private bool enforceMoveSpeedBounds = true;
        [SerializeField] private Vector2 zombieMoveSpeedRange = new Vector2(1.2f, 2.0f);
        [SerializeField] private bool logMoveSpeedClamp;

        public float AITickInterval => aiTickInterval;
        public bool IsActive { get; private set; }

        private float tickTimer;
        private readonly List<Unit> nearbyEnemyBuffer = new List<Unit>(8);
        private UnitController unitController;
        private NavMeshAgent navMeshAgent;

        private void Awake()
        {
            if (stateMachine == null)
            {
                stateMachine = GetComponent<ZombieStateMachine>();
            }

            if (unitHealth == null)
            {
                unitHealth = GetComponent<UnitHealth>();
            }

            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            if (unitController == null)
            {
                unitController = GetComponent<UnitController>();
            }

            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (encounterManager == null)
            {
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();
            }
        }

        public void Initialize()
        {
            IsActive = true;
            tickTimer = 0f;
            unitHealth?.ResetHealthToMax();

            if (unit != null)
            {
                unit.SetRole(UnitRole.Zombie);
                unit.SetOptionalAI(this);
            }

            if (stateMachine != null)
            {
                stateMachine.SetState(ZombieState.Spawn);
            }

            EnforceMoveSpeedBounds();

            // Auto-resolve any sibling sensor/listener components that were added at design time.
            if (GetComponent<NoiseListener>() == null)
            {
                gameObject.AddComponent<NoiseListener>();
            }
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            if (!active)
                stateMachine?.ReleaseAttackSlot();
        }

        public void SetAITickInterval(float interval)
        {
            aiTickInterval = Mathf.Max(0.05f, interval);
        }

        public void SetDetectionRange(float range)
        {
            detectionRange = Mathf.Max(0.1f, range);
        }

        /// <summary>
        /// Called by UnitHealth when this zombie takes damage. Switches to Chase
        /// toward the damage source if not already chasing or attacking.
        /// </summary>
        public void OnDamagedBy(GameObject source)
        {
            if (source == null || stateMachine == null) return;
            ZombieState state = stateMachine.CurrentState;
            if (state == ZombieState.Chase || state == ZombieState.Attack) return;

            Unit sourceUnit = source.GetComponent<Unit>();
            if (sourceUnit == null) sourceUnit = source.GetComponentInParent<Unit>();
            if (sourceUnit != null && sourceUnit.IsAlive)
            {
                stateMachine.SetChaseTarget(sourceUnit);
                stateMachine.SetState(ZombieState.Chase);
            }
        }

        /// <summary>
        /// Directs this zombie to move toward a world position (used by HordeManager).
        /// </summary>
        public void DirectToPosition(Vector3 worldPosition)
        {
            if (stateMachine == null) return;
            stateMachine.SetInvestigateTarget(worldPosition);
            stateMachine.SetState(ZombieState.Investigate);
        }

        private void OnDisable()
        {
            stateMachine?.ReleaseAttackSlot();
        }

        private void Update()
        {
            if (!IsActive || unitHealth == null || unitHealth.IsDead)
            {
                return;
            }

            tickTimer += Time.deltaTime;

            if (tickTimer < aiTickInterval)
            {
                return;
            }

            tickTimer = 0f;
            TickAI();
        }

        private void TickAI()
        {
            EnforceMoveSpeedBounds();
            stateMachine?.TickStateMachine();
            ScanForTargets();
            TryRequestEncounter();
        }

        private void EnforceMoveSpeedBounds()
        {
            if (!enforceMoveSpeedBounds)
            {
                return;
            }

            float minSpeed = Mathf.Max(0.1f, Mathf.Min(zombieMoveSpeedRange.x, zombieMoveSpeedRange.y));
            float maxSpeed = Mathf.Max(minSpeed, Mathf.Max(zombieMoveSpeedRange.x, zombieMoveSpeedRange.y));

            if (unitController != null)
            {
                float originalSpeed = unitController.MoveSpeed;
                float clamped = Mathf.Clamp(originalSpeed, minSpeed, maxSpeed);
                if (!Mathf.Approximately(clamped, originalSpeed))
                {
                    unitController.SetMoveSpeed(clamped);

                    if (logMoveSpeedClamp)
                    {
                        Debug.LogWarning($"[ZombieAI] Clamped zombie move speed from {originalSpeed:0.00} to {clamped:0.00} on {name}.", this);
                    }
                }
            }

            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (navMeshAgent == null)
            {
                return;
            }

            float originalAgentSpeed = navMeshAgent.speed;
            float clampedAgentSpeed = Mathf.Clamp(originalAgentSpeed, minSpeed, maxSpeed);
            if (!Mathf.Approximately(clampedAgentSpeed, originalAgentSpeed))
            {
                navMeshAgent.speed = clampedAgentSpeed;

                if (logMoveSpeedClamp)
                {
                    Debug.LogWarning($"[ZombieAI] Clamped NavMeshAgent speed from {originalAgentSpeed:0.00} to {clampedAgentSpeed:0.00} on {name}.", this);
                }
            }
        }

        private void ScanForTargets()
        {
            if (unit == null || UnitManager.Instance == null || stateMachine == null)
            {
                return;
            }

            // Already engaged — state machine drives it from here.
            ZombieState current = stateMachine.CurrentState;
            if (current == ZombieState.Chase || current == ZombieState.Attack || current == ZombieState.AttackDoor)
            {
                return;
            }

            List<Unit> nearby = UnitManager.Instance.FindNearbyEnemies(unit, detectionRange, nearbyEnemyBuffer);
            if (nearby == null || nearby.Count == 0)
            {
                return;
            }

            Unit nearest = null;
            float nearestDistSqr = float.MaxValue;

            for (int i = 0; i < nearby.Count; i++)
            {
                Unit candidate = nearby[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                float dSqr = (candidate.transform.position - transform.position).sqrMagnitude;
                if (dSqr < nearestDistSqr)
                {
                    nearestDistSqr = dSqr;
                    nearest = candidate;
                }
            }

            if (nearest == null)
            {
                return;
            }

            stateMachine.SetChaseTarget(nearest);
            stateMachine.SetState(ZombieState.Chase);
        }

        private void TryRequestEncounter()
        {
            if (unit == null || UnitManager.Instance == null)
            {
                return;
            }

            if (encounterManager == null)
            {
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();
            }

            if (encounterManager == null || encounterManager.IsUnitInEncounter(unit))
            {
                return;
            }

            List<Unit> nearbyEnemies = UnitManager.Instance.FindNearbyEnemies(unit, encounterScanRadius, nearbyEnemyBuffer);
            if (nearbyEnemies == null || nearbyEnemies.Count == 0)
            {
                return;
            }

            Unit nearestEnemy = null;
            float nearestDistanceSqr = float.MaxValue;

            for (int i = 0; i < nearbyEnemies.Count; i++)
            {
                Unit candidate = nearbyEnemies[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                float distanceSqr = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearestEnemy = candidate;
                }
            }

            if (nearestEnemy == null)
            {
                return;
            }

            float effectiveStartRange = ResolveEncounterStartRange();
            float startRangeSqr = effectiveStartRange * effectiveStartRange;
            if (nearestDistanceSqr > startRangeSqr)
            {
                return;
            }

            _ = encounterManager.TryStartEncounter(unit, nearestEnemy);
        }

        private float ResolveEncounterStartRange()
        {
            if (useEncounterManagerEngageRange && encounterManager != null)
            {
                return encounterManager.EngageRange;
            }

            return Mathf.Max(0.1f, encounterStartRange);
        }
    }
}