#region

using System;
using UnityEngine;
using UnityEngine.AI;

#endregion

namespace Zombera.Characters
{
    /// <summary>
    ///     Handles unit movement and input routing for player-controlled and AI-driven units.
    ///     Uses NavMeshAgent when present; falls back to direct transform movement.
    /// </summary>
    public sealed partial class UnitController : MonoBehaviour
    {
        private const int MaxNonPlayerNavMeshFallbackLogs = 8;
        private const float AnimatorResolveRetrySeconds = 0.5f;

        // Animator — fetched lazily because character visuals can initialize asynchronously after spawn.
        private static int _nonPlayerNavMeshFallbackLogCount;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        [SerializeField] private UnitRole role = UnitRole.Player;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] [Min(0.01f)] private float stoppingDistance = 0.15f;

        [Header("Group Move Arrival")]
        [SerializeField] [Min(0.15f)] private float groupMoveStoppingDistance = 0.75f;

        [SerializeField] [Min(1f)] private float groupMoveAcceleration = 12f;
        [SerializeField] [Min(0.1f)] private float groupMoveSettleStallSeconds = 0.5f;

        [Tooltip(
            "How quickly the Speed blend-tree parameter tracks actual speed. Lower = more responsive (try 0.05). Higher = smoother but laggier (0.2+).")]
        [SerializeField]
        [Min(0f)]
        private float animDampTime = 0.1f;

        [SerializeField] private bool useRigidbodyMovement;
        [SerializeField] private Rigidbody movementBody;
        [SerializeField] private UnitHealth unitHealth;
        [SerializeField] private UnitInventory unitInventory;
        [SerializeField] private UnitStats unitStats;

        [Header("Sprint")] [SerializeField] [Range(1f, 3f)]
        private float sprintSpeedMultiplier = 1.65f;

        [SerializeField] [Min(0.01f)] private float sprintBuildUpSeconds = 0.35f;

        [Header("NavMesh")]
        [SerializeField] private float navMeshRadius = 0.35f;
        [SerializeField] private float navMeshHeight = 2.0f;
        [SerializeField] private float navMeshBaseOffset = 0f;

        [Header("Debug")] [SerializeField] private bool logMoveToCalls;
        [SerializeField] private bool logGroundingDiagnostics;

        [SerializeField] private bool logMoveToCallsForPlayerOnly = true;
        [SerializeField] [Min(0f)] private float moveToLogCooldownSeconds = 0.1f;
        [SerializeField] private bool includeMoveToStackTrace;
        [SerializeField] [Min(0.1f)] private float nonPlayerAgentEnableRetrySeconds = 1.5f;
        [SerializeField] [Min(0.1f)] private float agentRebindRetrySeconds = 0.5f;
        [SerializeField] [Min(0.05f)] private float visualsResolveRetrySeconds = 0.25f;
        [SerializeField] [Range(0.001f, 0.2f)] private float inventoryEncumbranceRefreshThreshold = 0.01f;

        private NavMeshAgent _agent;
        private Animator _animator;
        private Animator _lastVisualsReadyAnimator;
        private float _arrivalStallTimer;
        private MoveArrivalProfile _activeMoveArrivalProfile = MoveArrivalProfile.Precise;
        private float _baselineAgentAcceleration = 20f;
        private float _baseAppliedMoveSpeed;
        private float _baseMovSpeed;
        private Vector3 _desiredMoveDirection;
        private float _lastEncumbranceSpeedMultiplier = float.NaN;
        private bool _loggedMoveToAgentUnavailableWarning;
        private bool _loggedNavMeshFallbackWarning;
        private float _moveStartRampDuration;
        private float _moveStartRampInitialMultiplier = 1f;
        private float _moveStartRampTimer;
        private float _nextMoveToLogAt;
        private float _nextAgentRebindAttemptAt;
        private float _nextNonPlayerAgentEnableAttemptAt;
        private float _nextAnimatorResolveAt;
        private float _lastMoveDestinationSampleAt = -1000f;
        private Vector3 _lastMoveDestinationSampleInput;
        private Vector3 _lastMoveDestinationSampleOutput;
        private float _nextVisualsResolveAt;
        private float _nextFallbackGroundClampAt;
        private bool _inventoryChangedSubscribed;
        private bool _knockbackAgentWasEnabled;
        private bool _knockbackResumeMoveTarget;
        private Vector3 _knockbackResumeTarget;

        private float _picHeartbeat;

        private float _postureSpeedMultiplier = 1f;
        private float _sprintBlend;
        private float _staminaRegenCooldownAt;

        private bool _isKnockedBack;
        private float _knockbackEndTime;

        private enum MovementTickState
        {
            Active,
            Dead,
            Knockback,
            PlayerInputLocked
        }

        // ReSharper disable once UnusedMember.Global
        public UnitRole Role => role;
        public float MoveSpeed => moveSpeed;
        public bool IsSprinting { get; private set; }

        // ReSharper disable once MemberCanBePrivate.Global
        public bool InputEnabled { get; private set; } = true;

        // ReSharper disable once MemberCanBePrivate.Global
        public Vector2 MoveInput { get; private set; }

        // ReSharper disable once MemberCanBePrivate.Global
        public Vector3 MoveTarget { get; private set; }
        public bool HasMoveTarget { get; private set; }
        public bool IsMoving { get; private set; }
        public bool HasVisualsReady => _animator != null;

        public event Action<UnitController, Animator> VisualsReady;

        /// <summary>World-space velocity of the nav agent (or zero when stopped).</summary>
        public Vector3 WorldVelocity => _agent != null ? _agent.velocity : Vector3.zero;

        private void Awake()
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            _baseMovSpeed = moveSpeed;
            _baseAppliedMoveSpeed = moveSpeed;
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            stoppingDistance = Mathf.Max(0.01f, stoppingDistance);
            animDampTime = Mathf.Max(0f, animDampTime);
            sprintBuildUpSeconds = Mathf.Max(0.01f, sprintBuildUpSeconds);

            if (movementBody == null) movementBody = GetComponent<Rigidbody>();
            if (unitHealth == null) unitHealth = GetComponent<UnitHealth>();
            if (unitInventory == null) unitInventory = GetComponent<UnitInventory>();
            if (unitStats == null) unitStats = GetComponent<UnitStats>();

            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null && _agent.acceleration > 0f)
                _baselineAgentAcceleration = _agent.acceleration;
            TryResolveAnimator(true, true);
            SubscribeInventoryEvents();
            RefreshAppliedSpeed();
        }

        private void OnEnable()
        {
            SubscribeInventoryEvents();
            TryResolveAnimator(true, true);
        }

        private void OnTransformChildrenChanged()
        {
            // Character visuals are spawned/rewired asynchronously; force a fresh animator lookup.
            _animator = null;
            TryResolveAnimator(true, true);
        }

        private void Start()
        {
            // Agent is enabled via ForceEnableAgent() called by PlayerSpawner (which
            // knows when the NavMesh is ready). Fallback: try self-enable after one frame.
            if (_agent != null && !_agent.enabled && IsPlayerLikeRole()) StartCoroutine(FallbackEnableAgent());
        }

        private void Update()
        {
            switch (ResolveMovementTickState())
            {
                case MovementTickState.Dead:
                    ApplyDeadMovementState();
                    return;
                case MovementTickState.Knockback:
                    if (TryHandleKnockbackState()) return;
                    break;
                case MovementTickState.PlayerInputLocked:
                    ApplyPlayerInputLockedState();
                    return;
            }

            TickSprintBuildUp();
            TickMoveStartRamp();

            if (TryUpdateUsingNavMeshAgent()) return;

            if (ShouldAttemptAgentRecovery())
            {
                TryRecoverAgentBinding();
                if (TryUpdateUsingNavMeshAgent()) return;
            }

            UpdateUsingFallbackMovement();
        }

        private MovementTickState ResolveMovementTickState()
        {
            if (unitHealth != null && unitHealth.IsDead) return MovementTickState.Dead;

            if (_isKnockedBack) return MovementTickState.Knockback;

            return !InputEnabled && role == UnitRole.Player
                ? MovementTickState.PlayerInputLocked
                : MovementTickState.Active;
        }

        private void ApplyDeadMovementState()
        {
            if (HasMoveTarget || IsMoving) Stop();

            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;

            UpdateAnimator(0f);
        }

        private void ApplyPlayerInputLockedState()
        {
            IsMoving = false;

            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;

            UpdateAnimator(0f);
        }

        private bool ShouldAttemptAgentRecovery()
        {
            return _agent != null && (!_agent.enabled || !_agent.isOnNavMesh);
        }

        public void ApplyKnockback(Vector3 force, float duration = 0.5f)
        {
            if (movementBody == null || duration <= 0f) return;

            _isKnockedBack = true;
            _knockbackEndTime = Time.time + duration;
            _knockbackAgentWasEnabled = _agent != null && _agent.enabled;
            _knockbackResumeMoveTarget = HasMoveTarget;
            _knockbackResumeTarget = MoveTarget;
            MoveInput = Vector2.zero;
            IsMoving = false;
            HasMoveTarget = false;

            if (_agent != null && _agent.enabled)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
                _agent.velocity = Vector3.zero;
                _agent.enabled = false;
            }

            movementBody.isKinematic = false;
            movementBody.linearVelocity = Vector3.zero;
            movementBody.angularVelocity = Vector3.zero;
            movementBody.AddForce(force, ForceMode.Impulse);
        }

        private bool TryHandleKnockbackState()
        {
            if (!_isKnockedBack) return false;

            if (Time.time >= _knockbackEndTime)
            {
                _isKnockedBack = false;

                if (movementBody != null)
                {
                    movementBody.linearVelocity = Vector3.zero;
                    movementBody.angularVelocity = Vector3.zero;
                    movementBody.isKinematic = !useRigidbodyMovement;
                }

                TryRestoreAgentAfterKnockback();

                if (_knockbackResumeMoveTarget) MoveTo(_knockbackResumeTarget);

                _knockbackResumeMoveTarget = false;

                return false;
            }

            return true;
        }

        private void TryRestoreAgentAfterKnockback()
        {
            if (!_knockbackAgentWasEnabled) return;

            _knockbackAgentWasEnabled = false;

            if (_agent == null) return;

            if (!_agent.enabled || !_agent.isOnNavMesh) ForceEnableAgent();

            if (!_agent.enabled || !_agent.isOnNavMesh)
            {
                TryRecoverAgentBinding(true);
                return;
            }

            var syncPosition = transform.position;
            if (NavMesh.SamplePosition(transform.position + Vector3.up, out var navHit, 4f, NavMesh.AllAreas))
            {
                syncPosition = navHit.position;
                transform.position = syncPosition;
            }

            if (!_agent.Warp(syncPosition))
            {
                TryRecoverAgentBinding(true);
                return;
            }

            _agent.nextPosition = syncPosition;
            _agent.velocity = Vector3.zero;
            _agent.ResetPath();
            _agent.isStopped = false;
            _nextAgentRebindAttemptAt = 0f;
        }

        private void SubscribeInventoryEvents()
        {
            if (_inventoryChangedSubscribed || unitInventory == null) return;

            unitInventory.OnInventoryChanged += HandleInventoryChanged;
            _inventoryChangedSubscribed = true;
        }

        private void UnsubscribeInventoryEvents()
        {
            if (!_inventoryChangedSubscribed || unitInventory == null) return;

            unitInventory.OnInventoryChanged -= HandleInventoryChanged;
            _inventoryChangedSubscribed = false;
        }

        private void HandleInventoryChanged()
        {
            if (unitStats == null || unitInventory == null)
            {
                RefreshAppliedSpeed();
                return;
            }

            var encumbranceMultiplier = unitStats.GetEncumbranceSpeedMultiplier(unitInventory.CarryRatio);
            var threshold = Mathf.Max(0.0001f, inventoryEncumbranceRefreshThreshold);

            if (!float.IsNaN(_lastEncumbranceSpeedMultiplier)
                && Mathf.Abs(encumbranceMultiplier - _lastEncumbranceSpeedMultiplier) < threshold)
                return;

            RefreshAppliedSpeed();
        }

        private bool TryResolveAnimator(bool notifyWhenResolved = false, bool forceSearch = false)
        {
            if (!forceSearch && _animator != null)
            {
                if (notifyWhenResolved) NotifyVisualsReadyIfNeeded();
                return true;
            }

            var now = Time.unscaledTime;
            if (!forceSearch && now < _nextVisualsResolveAt) return false;

            _nextVisualsResolveAt = now + Mathf.Max(0.05f, visualsResolveRetrySeconds);

            _animator = GetComponentInChildren<Animator>(true);

            if (_animator == null) return false;

            if (notifyWhenResolved) NotifyVisualsReadyIfNeeded();
            return true;
        }

        private void NotifyVisualsReadyIfNeeded()
        {
            if (_animator == null || _animator == _lastVisualsReadyAnimator) return;

            _lastVisualsReadyAnimator = _animator;
            VisualsReady?.Invoke(this, _animator);
        }

        private void OnDisable()
        {
            UnsubscribeInventoryEvents();
        }
    }

    /// <summary>
    ///     Supported unit archetypes for shared character systems.
    /// </summary>
    public enum UnitRole
    {
        Player,
        SquadMember,
        Survivor,
        Enemy,
        Zombie,
        Bandit
    }
}
