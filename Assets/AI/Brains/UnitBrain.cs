using UnityEngine;
using Zombera.AI.Actions;
using Zombera.AI.Decisions;
using Zombera.AI.Sensors;
using Zombera.AI.States;
using Zombera.Characters;
using Zombera.Debugging.DebugVisuals;
using Zombera.Systems;

namespace Zombera.AI.Brains
{
    /// <summary>
    /// Shared decision layer for all non-trivial unit behaviors.
    /// Responsibilities:
    /// - collect world context from sensors
    /// - evaluate decisions via UtilityEvaluator
    /// - execute reusable gameplay actions
    /// - track and report high-level brain state
    /// </summary>
    public abstract class UnitBrain : MonoBehaviour
    {
        [Header("Tick")]
        [SerializeField, Range(0.05f, 1f)] private float aiTickInterval = 0.3f;
        [SerializeField] private bool runOnEnable = true;

        [Header("Unit References")]
        [SerializeField] private Unit unit;
        [SerializeField] private UnitController unitController;
        [SerializeField] private UnitHealth unitHealth;
        [SerializeField] private UnitCombat unitCombat;
        [SerializeField] private UnitInventory unitInventory;
        [SerializeField] private UnitStats unitStats;

        [Header("Sensors")]
        [SerializeField] private EnemySensor enemySensor;
        [SerializeField] private AllySensor allySensor;
        [SerializeField] private NoiseSensor noiseSensor;

        [Header("Decision")]
        [SerializeField] private UtilityEvaluator utilityEvaluator;

        [Header("Actions")]
        [SerializeField] private MoveAction moveAction;
        [SerializeField] private AttackAction attackAction;
        [SerializeField] private ReloadAction reloadAction;
        [SerializeField] private FollowAction followAction;

        [Header("States")]
        [SerializeField] private IdleState idleState;
        [SerializeField] private ChaseState chaseState;
        [SerializeField] private AttackState attackState;

        [Header("Combat")]
        [SerializeField] private float defaultAttackRange = 2.25f;

        [Header("Debug")]
        [SerializeField] private bool showCurrentStateLabel = true;
        [SerializeField] private bool drawDetectionRadius = true;
        [SerializeField] private bool logStateTransitions = true;
        [SerializeField] private Color detectionRadiusColor = new Color(1f, 0.3f, 0.3f, 0.8f);
        [SerializeField] private AIDebugVisualizer debugVisualizer;

        private IUnitBrainState activeStateHandler;
        private float tickTimer;

        public bool IsBrainActive { get; private set; }
        public UnitBrainStateType CurrentState { get; private set; } = UnitBrainStateType.Idle;
        public UnitDecisionType CurrentDecision { get; private set; } = UnitDecisionType.Idle;
        public UnitSensorFrame LastSensorFrame { get; private set; }

        public Unit Unit => unit;
        public UnitController UnitController => unitController;
        public UnitHealth UnitHealth => unitHealth;
        public UnitCombat UnitCombat => unitCombat;
        public UnitInventory UnitInventory => unitInventory;
        public UnitStats UnitStats => unitStats;

        public EnemySensor EnemySensor => enemySensor;
        public AllySensor AllySensor => allySensor;
        public NoiseSensor NoiseSensor => noiseSensor;
        public UtilityEvaluator UtilityEvaluator => utilityEvaluator;

        public MoveAction MoveAction => moveAction;
        public AttackAction AttackAction => attackAction;
        public ReloadAction ReloadAction => reloadAction;
        public FollowAction FollowAction => followAction;

        public float AttackRange => defaultAttackRange;
        public float AITickInterval => aiTickInterval;

        protected virtual void Awake()
        {
            AutoWire();
            ConfigureDefaultRole();

            if (unit != null)
            {
                unit.SetOptionalAI(this);
            }

            PublishStateDebugLabel();
        }

        protected virtual void OnEnable()
        {
            if (runOnEnable)
            {
                StartBrain();
            }
        }

        protected virtual void OnDisable()
        {
            StopBrain();

            if (debugVisualizer != null)
            {
                debugVisualizer.RemoveTarget(transform);
            }
        }

        private void Update()
        {
            if (!IsBrainActive || unitHealth == null || unitHealth.IsDead)
            {
                return;
            }

            tickTimer += Time.deltaTime;

            if (tickTimer < aiTickInterval)
            {
                return;
            }

            tickTimer = 0f;
            TickBrain();
        }

        public void StartBrain()
        {
            IsBrainActive = true;
            tickTimer = 0f;
            PublishStateDebugLabel();
        }

        public void StopBrain()
        {
            IsBrainActive = false;
            moveAction?.StopMovement();
        }

        public void SetBrainActive(bool active)
        {
            if (active)
            {
                StartBrain();
            }
            else
            {
                StopBrain();
            }
        }

        public void RequestImmediateTick()
        {
            tickTimer = aiTickInterval;
        }

        public void SetTickIntervalExternal(float interval)
        {
            SetTickInterval(interval);
        }

        protected virtual void TickBrain()
        {
            UnitSensorFrame sensorFrame = CollectSensorFrame();
            LastSensorFrame = sensorFrame;

            if (!ShouldUseDecisionSystem(sensorFrame))
            {
                RunManualControl(sensorFrame);
                return;
            }

            UnitDecision decision = EvaluateDecision(sensorFrame);
            ApplyDecision(decision, sensorFrame);
        }

        protected virtual UnitSensorFrame CollectSensorFrame()
        {
            if (enemySensor != null)
            {
                enemySensor.Sense(unit);
            }

            if (allySensor != null)
            {
                allySensor.Sense(unit);
            }

            if (noiseSensor != null)
            {
                noiseSensor.Sense(unit);
            }

            UnitSensorFrame sensorFrame = new UnitSensorFrame
            {
                Self = unit,
                SelfPosition = transform.position,
                NearestEnemy = enemySensor != null ? enemySensor.NearestEnemy : null,
                NearestEnemyDistance = enemySensor != null ? enemySensor.NearestEnemyDistance : float.PositiveInfinity,
                NearbyEnemyCount = enemySensor != null ? enemySensor.EnemyCount : 0,
                NearbyAllyCount = allySensor != null ? allySensor.NearbyAlliesCount : 0,
                HasHeardNoise = noiseSensor != null && noiseSensor.HasRecentNoise,
                LastNoisePosition = noiseSensor != null ? noiseSensor.LastNoisePosition : Vector3.zero,
                LastNoiseAge = noiseSensor != null ? noiseSensor.LastNoiseAge : float.PositiveInfinity
            };

            return sensorFrame;
        }

        protected virtual bool ShouldUseDecisionSystem(UnitSensorFrame sensorFrame)
        {
            _ = sensorFrame;
            return true;
        }

        protected virtual UnitDecision EvaluateDecision(UnitSensorFrame sensorFrame)
        {
            if (utilityEvaluator != null)
            {
                return utilityEvaluator.Evaluate(this, sensorFrame);
            }

            if (sensorFrame.NearestEnemy != null)
            {
                return new UnitDecision
                {
                    DecisionType = sensorFrame.NearestEnemyDistance <= defaultAttackRange
                        ? UnitDecisionType.Attack
                        : UnitDecisionType.Chase,
                    Score = 1f,
                    TargetUnit = sensorFrame.NearestEnemy,
                    TargetPosition = sensorFrame.NearestEnemy.transform.position,
                    Reason = "Fallback decision without UtilityEvaluator"
                };
            }

            return new UnitDecision
            {
                DecisionType = UnitDecisionType.Wander,
                Score = 0.15f,
                TargetPosition = GetFallbackWanderPosition(),
                Reason = "Fallback wandering without UtilityEvaluator"
            };
        }

        protected virtual void RunManualControl(UnitSensorFrame sensorFrame)
        {
            TransitionState(UnitBrainStateType.Idle, "Decision bypassed", sensorFrame, default);
            activeStateHandler?.Tick(this, sensorFrame, default);
        }

        protected virtual void ApplyDecision(UnitDecision decision, UnitSensorFrame sensorFrame)
        {
            CurrentDecision = decision.DecisionType;

            switch (decision.DecisionType)
            {
                case UnitDecisionType.Attack:
                    TransitionState(UnitBrainStateType.Attack, decision.Reason, sensorFrame, decision);
                    activeStateHandler?.Tick(this, sensorFrame, decision);
                    break;

                case UnitDecisionType.Chase:
                    TransitionState(UnitBrainStateType.Chase, decision.Reason, sensorFrame, decision);
                    activeStateHandler?.Tick(this, sensorFrame, decision);
                    break;

                case UnitDecisionType.Reload:
                    TransitionState(UnitBrainStateType.Idle, decision.Reason, sensorFrame, decision);
                    reloadAction?.ExecuteReload();
                    activeStateHandler?.Tick(this, sensorFrame, decision);
                    break;

                case UnitDecisionType.Follow:
                    TransitionState(UnitBrainStateType.Idle, decision.Reason, sensorFrame, decision);

                    if (decision.TargetUnit != null)
                    {
                        followAction?.ExecuteFollow(decision.TargetUnit.transform);
                    }
                    else
                    {
                        followAction?.ExecuteMoveTo(decision.TargetPosition);
                    }

                    activeStateHandler?.Tick(this, sensorFrame, decision);
                    break;

                case UnitDecisionType.Retreat:
                    TransitionState(UnitBrainStateType.Idle, decision.Reason, sensorFrame, decision);

                    if (moveAction != null)
                    {
                        Vector3 threat = sensorFrame.NearestEnemy != null
                            ? sensorFrame.NearestEnemy.transform.position
                            : transform.position + transform.forward;

                        Vector3 retreatPoint = moveAction.CalculateRetreatPoint(transform.position, threat);
                        moveAction.ExecuteMove(retreatPoint);
                    }

                    activeStateHandler?.Tick(this, sensorFrame, decision);
                    break;

                case UnitDecisionType.Wander:
                    TransitionState(UnitBrainStateType.Idle, decision.Reason, sensorFrame, decision);

                    if (moveAction != null)
                    {
                        Vector3 wanderPosition = decision.TargetPosition != Vector3.zero
                            ? decision.TargetPosition
                            : GetFallbackWanderPosition();

                        moveAction.ExecuteMove(wanderPosition);
                    }

                    activeStateHandler?.Tick(this, sensorFrame, decision);
                    break;

                case UnitDecisionType.Idle:
                default:
                    TransitionState(UnitBrainStateType.Idle, decision.Reason, sensorFrame, decision);
                    activeStateHandler?.Tick(this, sensorFrame, decision);
                    break;
            }

            PublishStateDebugLabel();
        }

        protected void TransitionState(UnitBrainStateType nextState, string reason, UnitSensorFrame sensorFrame, UnitDecision decision)
        {
            if (CurrentState == nextState)
            {
                return;
            }

            UnitBrainStateType previousState = CurrentState;
            activeStateHandler?.Exit(this);

            CurrentState = nextState;
            activeStateHandler = ResolveStateHandler(nextState);
            activeStateHandler?.Enter(this, sensorFrame, decision);

            if (logStateTransitions)
            {
                Debug.Log($"[{name}] Brain state {previousState} -> {nextState} ({reason})", this);
            }
        }

        protected Vector3 GetFallbackWanderPosition(float distance = 3f)
        {
            Vector2 random = Random.insideUnitCircle;

            if (random.sqrMagnitude <= 0.0001f)
            {
                random = Vector2.right;
            }

            random.Normalize();
            return transform.position + new Vector3(random.x, 0f, random.y) * distance;
        }

        protected void SetTickInterval(float interval)
        {
            aiTickInterval = Mathf.Max(0.01f, interval);
        }

        protected virtual void ConfigureDefaultRole()
        {
            // TODO: Derived brains can force role defaults (Zombie, SquadMember, Player).
        }

        private IUnitBrainState ResolveStateHandler(UnitBrainStateType stateType)
        {
            switch (stateType)
            {
                case UnitBrainStateType.Chase:
                    return chaseState;
                case UnitBrainStateType.Attack:
                    return attackState;
                case UnitBrainStateType.Idle:
                default:
                    return idleState;
            }
        }

        private void AutoWire()
        {
            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            if (unitController == null)
            {
                unitController = GetComponent<UnitController>();
            }

            if (unitHealth == null)
            {
                unitHealth = GetComponent<UnitHealth>();
            }

            if (unitCombat == null)
            {
                unitCombat = GetComponent<UnitCombat>();
            }

            if (unitInventory == null)
            {
                unitInventory = GetComponent<UnitInventory>();
            }

            if (unitStats == null)
            {
                unitStats = GetComponent<UnitStats>();
            }

            if (enemySensor == null)
            {
                enemySensor = GetComponent<EnemySensor>();
            }

            if (allySensor == null)
            {
                allySensor = GetComponent<AllySensor>();
            }

            if (noiseSensor == null)
            {
                noiseSensor = GetComponent<NoiseSensor>();
            }

            if (utilityEvaluator == null)
            {
                utilityEvaluator = GetComponent<UtilityEvaluator>();
            }

            if (moveAction == null)
            {
                moveAction = GetComponent<MoveAction>();
            }

            if (attackAction == null)
            {
                attackAction = GetComponent<AttackAction>();
            }

            if (reloadAction == null)
            {
                reloadAction = GetComponent<ReloadAction>();
            }

            if (followAction == null)
            {
                followAction = GetComponent<FollowAction>();
            }

            if (idleState == null)
            {
                idleState = GetComponent<IdleState>();
            }

            if (chaseState == null)
            {
                chaseState = GetComponent<ChaseState>();
            }

            if (attackState == null)
            {
                attackState = GetComponent<AttackState>();
            }

            if (debugVisualizer == null)
            {
                debugVisualizer = FindObjectOfType<AIDebugVisualizer>();
            }

            moveAction?.Initialize(unitController);
            attackAction?.Initialize(unit, unitCombat);
            reloadAction?.Initialize(unitCombat);
            followAction?.Initialize(unitController, GetComponent<FollowController>());
        }

        private void PublishStateDebugLabel()
        {
            if (!showCurrentStateLabel)
            {
                return;
            }

            if (debugVisualizer == null)
            {
                debugVisualizer = FindObjectOfType<AIDebugVisualizer>();
            }

            debugVisualizer?.SetAIState(transform, $"{GetType().Name}: {CurrentState}");
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDetectionRadius || enemySensor == null)
            {
                return;
            }

            Gizmos.color = detectionRadiusColor;
            Gizmos.DrawWireSphere(transform.position, enemySensor.DetectionRadius);
        }
    }

    /// <summary>
    /// Minimal shared AI state contract.
    /// </summary>
    public interface IUnitBrainState
    {
        UnitBrainStateType StateType { get; }
        void Enter(UnitBrain brain, UnitSensorFrame sensorFrame, UnitDecision decision);
        void Tick(UnitBrain brain, UnitSensorFrame sensorFrame, UnitDecision decision);
        void Exit(UnitBrain brain);
    }

    /// <summary>
    /// High-level brain states.
    /// </summary>
    public enum UnitBrainStateType
    {
        Idle,
        Chase,
        Attack
    }

    /// <summary>
    /// Utility-decision outcomes that map into reusable actions/states.
    /// </summary>
    public enum UnitDecisionType
    {
        Idle,
        Attack,
        Retreat,
        Reload,
        Follow,
        Wander,
        Chase
    }

    /// <summary>
    /// Sensor snapshot consumed by utility scoring.
    /// </summary>
    public struct UnitSensorFrame
    {
        public Unit Self;
        public Vector3 SelfPosition;

        public Unit NearestEnemy;
        public float NearestEnemyDistance;
        public int NearbyEnemyCount;
        public int NearbyAllyCount;

        public bool HasHeardNoise;
        public Vector3 LastNoisePosition;
        public float LastNoiseAge;
    }

    /// <summary>
    /// Result from the decision layer, routed into state/action execution.
    /// </summary>
    public struct UnitDecision
    {
        public UnitDecisionType DecisionType;
        public float Score;
        public Unit TargetUnit;
        public Vector3 TargetPosition;
        public string Reason;
    }
}