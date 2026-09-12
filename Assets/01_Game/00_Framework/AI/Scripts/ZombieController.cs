#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Core;
using Zombera.Systems;

#endregion

// ReSharper disable MergeIntoLogicalPattern
// ReSharper disable InvertIf
// ReSharper disable ForCanBeConvertedToForeach

namespace Zombera.AI
{
    /// <summary>
    ///     Per-zombie behavior controller using tick-based updates for performance.
    /// </summary>
    public class ZombieController : MonoBehaviour, IDamageNotifiable
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

        [Header("Tick Scheduling")] [SerializeField]
        private bool useAiTickManager = true;

        [SerializeField] [Min(0.1f)] private float aiTickManagerResolveRetrySeconds = 1f;

        [Header("Movement Safety")] [SerializeField]
        private bool enforceMoveSpeedBounds = true;

        [SerializeField] private Vector2 zombieMoveSpeedRange = new(1.2f, 2.0f);
        [SerializeField] private bool logMoveSpeedClamp;
        private readonly List<Unit> _nearbyEnemyBuffer = new(8);
        private NavMeshAgent _navMeshAgent;
        private AITickManager _aiTickManager;
        private bool _registeredWithAiTickManager;
        private float _nextAiTickManagerResolveAt;

        private float _tickTimer;
        private UnitController _unitController;

        // ReSharper disable once UnusedMember.Global
        public float AITickInterval => aiTickInterval;
        public bool IsActive { get; private set; }

        public ZombieStateMachine StateMachine => stateMachine;
        public UnitHealth Health => unitHealth;
        public Unit Unit => unit;
        public UnitController Controller => _unitController;

        protected virtual void Awake()
        {
            if (stateMachine == null) stateMachine = GetComponent<ZombieStateMachine>();

            if (unitHealth == null) unitHealth = GetComponent<UnitHealth>();

            if (unit == null) unit = GetComponent<Unit>();

            if (_unitController == null) _unitController = GetComponent<UnitController>();

            if (_navMeshAgent == null) _navMeshAgent = GetComponent<NavMeshAgent>();

            if (encounterManager == null)
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();
        }

        protected virtual void Update()
        {
            if (!IsActive || unitHealth == null || unitHealth.IsDead) return;

            if (ShouldUseExternalTickScheduler()) return;

            _tickTimer += Time.deltaTime;

            if (_tickTimer < aiTickInterval) return;

            _tickTimer = 0f;
            TickAI();
        }

        protected virtual void OnEnable()
        {
            Zombera.Core.RuntimeAiRegistry.RegisterZombie(this);
            _tickTimer = 0f;
            _nextAiTickManagerResolveAt = 0f;
            EnsureAiTickManagerRegistration();
        }

        protected virtual void OnDisable()
        {
            Zombera.Core.RuntimeAiRegistry.UnregisterZombie(this);
            UnregisterFromAiTickManager();
            stateMachine?.ReleaseAttackSlot();
        }

        public virtual void Initialize()
        {
            IsActive = true;
            _tickTimer = 0f;
            unitHealth?.ResetHealthToMax();

            if (unit != null)
            {
                unit.SetRole(UnitRole.Zombie);
                unit.SetOptionalAI(this);
            }

            if (stateMachine != null) stateMachine.SetState(ZombieState.Spawn);

            EnforceMoveSpeedBounds();

            if (GetComponent<NoiseListener>() == null) gameObject.AddComponent<NoiseListener>();
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            if (!active) stateMachine?.ReleaseAttackSlot();
        }

        public void SetAITickInterval(float interval)
        {
            aiTickInterval = Mathf.Max(0.05f, interval);
        }

        public void SetDetectionRange(float range)
        {
            detectionRange = Mathf.Max(0.1f, range);
        }

        public void OnDamagedBy(GameObject source)
        {
            if (source == null || stateMachine == null) return;
            var state = stateMachine.CurrentState;
            if (state == ZombieState.Chase || state == ZombieState.Attack) return;

            var sourceUnit = source.GetComponent<Unit>();
            if (sourceUnit == null) sourceUnit = source.GetComponentInParent<Unit>();
            if (sourceUnit != null && sourceUnit.IsAlive)
            {
                stateMachine.SetChaseTarget(sourceUnit);
                stateMachine.SetState(ZombieState.Chase);
            }
        }

        public void DirectToPosition(Vector3 worldPosition)
        {
            if (stateMachine == null) return;
            stateMachine.SetInvestigateTarget(worldPosition);
            stateMachine.SetState(ZombieState.Investigate);
        }

        protected virtual void TickAI()
        {
            EnforceMoveSpeedBounds();
            stateMachine?.TickStateMachine();

            // Single spatial query per tick feeds both target acquisition and encounter checks.
            var nearestEnemy = ScanNearestEnemy(out var nearestDistanceSqr);
            TryAcquireChaseTarget(nearestEnemy);
            TryRequestEncounter(nearestEnemy, nearestDistanceSqr);
        }

        /// <summary>
        ///     Runs one AI tick immediately. Used by <see cref="AITickManager" /> to stagger work across frames.
        /// </summary>
        public void ForceTickAI()
        {
            if (!IsActive || unitHealth == null || unitHealth.IsDead) return;

            TickAI();
        }

        private bool ShouldUseExternalTickScheduler()
        {
            if (!useAiTickManager) return false;

            if (_registeredWithAiTickManager)
            {
                if (_aiTickManager != null && _aiTickManager.isActiveAndEnabled)
                    return true;

                _registeredWithAiTickManager = false;
                _aiTickManager = null;
            }

            EnsureAiTickManagerRegistration();
            return _registeredWithAiTickManager;
        }

        private void EnsureAiTickManagerRegistration()
        {
            if (!useAiTickManager || _registeredWithAiTickManager) return;

            var now = Time.unscaledTime;
            if (now < _nextAiTickManagerResolveAt) return;

            _aiTickManager = AITickManager.Instance;
            if (_aiTickManager == null)
                _aiTickManager = FindFirstObjectByType<AITickManager>();

            if (_aiTickManager == null)
            {
                _nextAiTickManagerResolveAt = now + Mathf.Max(0.1f, aiTickManagerResolveRetrySeconds);
                return;
            }

            _aiTickManager.Register(this);
            _registeredWithAiTickManager = true;
            _nextAiTickManagerResolveAt = 0f;
        }

        private void UnregisterFromAiTickManager()
        {
            if (!_registeredWithAiTickManager)
            {
                _aiTickManager = null;
                return;
            }

            _aiTickManager?.Unregister(this);
            _aiTickManager = null;
            _registeredWithAiTickManager = false;
        }

        private void EnforceMoveSpeedBounds()
        {
            if (!enforceMoveSpeedBounds) return;

            var minSpeed = Mathf.Max(0.1f, Mathf.Min(zombieMoveSpeedRange.x, zombieMoveSpeedRange.y));
            var maxSpeed = Mathf.Max(minSpeed, Mathf.Max(zombieMoveSpeedRange.x, zombieMoveSpeedRange.y));

            if (_unitController != null)
            {
                var originalSpeed = _unitController.MoveSpeed;
                var clamped = Mathf.Clamp(originalSpeed, minSpeed, maxSpeed);
                if (!Mathf.Approximately(clamped, originalSpeed))
                {
                    _unitController.SetMoveSpeed(clamped);

                    if (logMoveSpeedClamp)
                        Debug.LogWarning(
                            $"[ZombieController] Clamped zombie move speed from {originalSpeed:0.00} to {clamped:0.00} on {name}.",
                            this);
                }
            }

            if (_navMeshAgent == null) return;

            var originalAgentSpeed = _navMeshAgent.speed;
            var clampedAgentSpeed = Mathf.Clamp(originalAgentSpeed, minSpeed, maxSpeed);
            if (!Mathf.Approximately(clampedAgentSpeed, originalAgentSpeed))
            {
                _navMeshAgent.speed = clampedAgentSpeed;

                if (logMoveSpeedClamp)
                    Debug.LogWarning(
                        $"[ZombieController] Clamped NavMeshAgent speed from {originalAgentSpeed:0.00} to {clampedAgentSpeed:0.00} on {name}.",
                        this);
            }
        }

        private Unit ScanNearestEnemy(out float nearestDistanceSqr)
        {
            nearestDistanceSqr = float.MaxValue;

            if (unit == null || UnitManager.Instance == null) return null;

            var scanRadius = Mathf.Max(detectionRange, encounterScanRadius);
            var nearby = UnitManager.Instance.FindNearbyEnemies(unit, scanRadius, _nearbyEnemyBuffer);
            return FindNearestAliveUnit(nearby, out nearestDistanceSqr);
        }

        private void TryAcquireChaseTarget(Unit nearestEnemy)
        {
            if (stateMachine == null || nearestEnemy == null) return;

            var current = stateMachine.CurrentState;
            if (current == ZombieState.Chase || current == ZombieState.Attack ||
                current == ZombieState.AttackDoor) return;

            var detectionRangeSqr = detectionRange * detectionRange;
            if ((nearestEnemy.transform.position - transform.position).sqrMagnitude > detectionRangeSqr) return;

            stateMachine.SetChaseTarget(nearestEnemy);
            stateMachine.SetState(ZombieState.Chase);
        }

        private void TryRequestEncounter(Unit nearestEnemy, float nearestDistanceSqr)
        {
            if (unit == null || nearestEnemy == null) return;

            var scanRadiusSqr = encounterScanRadius * encounterScanRadius;
            if (nearestDistanceSqr > scanRadiusSqr) return;

            var resolvedEncounterManager = ResolveEncounterManager();
            if (resolvedEncounterManager == null || resolvedEncounterManager.IsUnitInEncounter(unit)) return;

            var effectiveStartRange = ResolveEncounterStartRange();
            var startRangeSqr = effectiveStartRange * effectiveStartRange;
            if (nearestDistanceSqr > startRangeSqr) return;

            _ = resolvedEncounterManager.TryStartEncounter(unit, nearestEnemy);
        }

        private CombatEncounterManager ResolveEncounterManager()
        {
            if (encounterManager == null)
                encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : FindFirstObjectByType<CombatEncounterManager>();

            return encounterManager;
        }

        private Unit FindNearestAliveUnit(List<Unit> candidates, out float nearestDistanceSqr)
        {
            nearestDistanceSqr = float.MaxValue;

            if (candidates == null || candidates.Count == 0) return null;

            Unit nearest = null;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || !candidate.IsAlive) continue;

                var distanceSqr = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private float ResolveEncounterStartRange()
        {
            if (useEncounterManagerEngageRange && encounterManager != null) return encounterManager.EngageRange;

            return Mathf.Max(0.1f, encounterStartRange);
        }
    }
}