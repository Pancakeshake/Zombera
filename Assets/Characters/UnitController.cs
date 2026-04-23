using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Core;

namespace Zombera.Characters
{
    /// <summary>
    /// Handles unit movement and input routing for player-controlled and AI-driven units.
    /// Uses NavMeshAgent when present; falls back to direct transform movement.
    /// </summary>
    public sealed class UnitController : MonoBehaviour
    {
        [SerializeField] private UnitRole role = UnitRole.Player;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField, Min(0.01f)] private float stoppingDistance = 0.15f;
        [Tooltip("How quickly the Speed blend-tree parameter tracks actual speed. Lower = more responsive (try 0.05). Higher = smoother but laggier (0.2+).")]
        [SerializeField, Min(0f)] private float animDampTime = 0.1f;
        [SerializeField] private bool useRigidbodyMovement;
        [SerializeField] private Rigidbody movementBody;
        [SerializeField] private UnitHealth unitHealth;
        [SerializeField] private UnitInventory unitInventory;
        [SerializeField] private UnitStats unitStats;

        [Header("Sprint")]
        [SerializeField, Range(1f, 3f)] private float sprintSpeedMultiplier = 1.65f;
        [SerializeField, Min(0.01f)] private float sprintBuildUpSeconds = 0.35f;

        [Header("Debug")]
        [SerializeField] private bool logMoveToCalls;
        [SerializeField] private bool logMoveToCallsForPlayerOnly = true;
        [SerializeField, Min(0f)] private float moveToLogCooldownSeconds = 0.1f;
        [SerializeField] private bool includeMoveToStackTrace;

        public UnitRole Role => role;
        public float MoveSpeed => moveSpeed;
        public bool IsSprinting { get; private set; }
        public bool InputEnabled { get; private set; } = true;
        public Vector2 MoveInput { get; private set; }
        public Vector3 MoveTarget { get; private set; }
        public bool HasMoveTarget { get; private set; }
        public bool IsMoving { get; private set; }
        /// <summary>World-space velocity of the nav agent (or zero when stopped).</summary>
        public Vector3 WorldVelocity => agent != null ? agent.velocity : Vector3.zero;

        private NavMeshAgent agent;
        private float baseMovSpeed;
        private float _baseAppliedMoveSpeed;
        private float _staminaRegenCooldownAt;
        private Vector3 desiredMoveDirection;
        private float _arrivalStallTimer;
        private bool _loggedNavMeshFallbackWarning;
        private bool _loggedMoveToAgentUnavailableWarning;
        private float _nextMoveToLogAt;
        private float _sprintBlend;
        private float _moveStartRampTimer;
        private float _moveStartRampDuration;
        private float _moveStartRampInitialMultiplier = 1f;

        // Animator — fetched lazily because UMA builds it asynchronously after spawn.
        private static int _nonPlayerNavMeshFallbackLogCount;
        private const int MaxNonPlayerNavMeshFallbackLogs = 8;
        private Animator _animator;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Awake()
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            baseMovSpeed = moveSpeed;
            _baseAppliedMoveSpeed = moveSpeed;
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            stoppingDistance = Mathf.Max(0.01f, stoppingDistance);
            animDampTime = Mathf.Max(0f, animDampTime);
            sprintBuildUpSeconds = Mathf.Max(0.01f, sprintBuildUpSeconds);

            if (movementBody == null)
            {
                movementBody = GetComponent<Rigidbody>();
            }

            if (unitHealth == null)
            {
                unitHealth = GetComponent<UnitHealth>();
            }

            if (unitInventory == null)
            {
                unitInventory = GetComponent<UnitInventory>();
            }

            if (unitStats == null)
            {
                unitStats = GetComponent<UnitStats>();
            }

            agent = GetComponent<NavMeshAgent>();

            if (unitInventory != null)
            {
                unitInventory.OnInventoryChanged += RefreshAppliedSpeed;
            }
        }

        private void OnEnable() { }

        private void OnDisable()
        {
            if (unitInventory != null)
            {
                unitInventory.OnInventoryChanged -= RefreshAppliedSpeed;
            }
        }

        private void Start()
        {
            // Agent is enabled via ForceEnableAgent() called by PlayerSpawner (which
            // knows when the NavMesh is ready). Fallback: try self-enable after one frame.
            if (agent != null && !agent.enabled)
            {
                StartCoroutine(FallbackEnableAgent());
            }
        }

        /// <summary>
        /// Called by PlayerSpawner immediately after spawn when the NavMesh is confirmed
        /// baked and ready. Configures the agent and force-places it on the surface.
        /// </summary>
        public void ForceEnableAgent()
        {
            // MainMenu/CharacterCreator may keep preview Units alive without any NavMesh.
            // Avoid spamming warnings and unnecessary sampling when not in an active world session.
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.CurrentState == GameState.MainMenu)
            {
                return;
            }

            if (agent == null)
            {
                Debug.LogWarning("[UnitController] ForceEnableAgent: no NavMeshAgent component.", this);
                return;
            }

            agent.speed = moveSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.angularSpeed = rotationSpeed * 10f;
            agent.acceleration = 20f;
            agent.autoBraking = true;

            const int walkableAreaMask = 1 << 0;
            Vector3 sampleOrigin = transform.position + Vector3.up * 1.5f;
            float[] probeDistances = { 2f, 6f, 12f, 24f };

            bool hasSample = false;
            NavMeshHit navHit = default;

            for (int index = 0; index < probeDistances.Length; index++)
            {
                if (NavMesh.SamplePosition(sampleOrigin, out navHit, probeDistances[index], walkableAreaMask))
                {
                    hasSample = true;
                    break;
                }

                if (NavMesh.SamplePosition(sampleOrigin, out navHit, probeDistances[index], NavMesh.AllAreas))
                {
                    hasSample = true;
                    break;
                }
            }

            if (!hasSample)
            {
                // Recovery pass for large vertical gaps (for example spawn Y below generated terrain).
                Vector3 elevatedSampleOrigin = new Vector3(
                    transform.position.x,
                    transform.position.y + 128f,
                    transform.position.z);
                float[] elevatedProbeDistances = { 48f, 96f, 160f, 256f };

                for (int index = 0; index < elevatedProbeDistances.Length; index++)
                {
                    if (NavMesh.SamplePosition(elevatedSampleOrigin, out navHit, elevatedProbeDistances[index], walkableAreaMask))
                    {
                        hasSample = true;
                        break;
                    }

                    if (NavMesh.SamplePosition(elevatedSampleOrigin, out navHit, elevatedProbeDistances[index], NavMesh.AllAreas))
                    {
                        hasSample = true;
                        break;
                    }
                }
            }

            if (!hasSample)
            {
                if (agent.enabled)
                {
                    agent.enabled = false;
                }

                if (!_loggedNavMeshFallbackWarning)
                {
                    _loggedNavMeshFallbackWarning = true;
                    string message = $"[UnitController] ForceEnableAgent: no nearby NavMesh for {name}. Falling back to transform movement.";
                    LogNavMeshFallback(message);
                }

                return;
            }

            // Move to sampled point before enabling to avoid 'not close enough to NavMesh'.
            transform.position = navHit.position;

            if (!agent.enabled)
            {
                agent.enabled = true;

                if (!agent.enabled)
                {
                    if (!_loggedNavMeshFallbackWarning)
                    {
                        _loggedNavMeshFallbackWarning = true;
                        string message = $"[UnitController] ForceEnableAgent: failed to enable NavMeshAgent near {navHit.position}. Falling back to transform movement.";
                        LogNavMeshFallback(message);
                    }

                    return;
                }
            }

            bool warpSucceeded = agent.Warp(navHit.position);

            if (!warpSucceeded || !agent.isOnNavMesh)
            {
                if (agent.enabled)
                {
                    agent.enabled = false;
                }

                if (!_loggedNavMeshFallbackWarning)
                {
                    _loggedNavMeshFallbackWarning = true;
                    string message = $"[UnitController] ForceEnableAgent: failed to place agent on NavMesh at {navHit.position}. Falling back to transform movement.";
                    LogNavMeshFallback(message);
                }

                return;
            }

            _loggedNavMeshFallbackWarning = false;
        }

        private void LogNavMeshFallback(string message)
        {
            if (role == UnitRole.Player || role == UnitRole.SquadMember || role == UnitRole.Survivor)
            {
                Debug.LogWarning(message, this);
                return;
            }

            if (_nonPlayerNavMeshFallbackLogCount >= MaxNonPlayerNavMeshFallbackLogs)
            {
                return;
            }

            _nonPlayerNavMeshFallbackLogCount++;
            Debug.Log(message, this);
        }

        private IEnumerator FallbackEnableAgent()
        {
            // Give the NavMesh a couple of frames to settle before the first attempt.
            yield return null;
            yield return null;

            float elapsed = 0f;
            const float retryInterval = 0.5f;
            const float timeout = 10f;

            while (elapsed < timeout && agent != null && !agent.isOnNavMesh)
            {
                ForceEnableAgent();
                if (agent != null && agent.isOnNavMesh) yield break;
                yield return new WaitForSeconds(retryInterval);
                elapsed += retryInterval;
            }
        }

        public void SetInputEnabled(bool enabled)
        {
            InputEnabled = enabled;

            if (!enabled)
            {
                MoveInput = Vector2.zero;

                // Halt any active move input on the NavMeshAgent while input is locked,
                // but leave HasMoveTarget so AI-issued paths remain active.
                if (agent != null && agent.isActiveAndEnabled)
                {
                    agent.velocity = Vector3.zero;
                }
            }
        }

        public void SetRole(UnitRole unitRole)
        {
            role = unitRole;
        }

        public void SetMoveSpeed(float speed)
        {
            baseMovSpeed = Mathf.Max(0.1f, speed);
            RefreshAppliedSpeed();
        }

        private float _postureSpeedMultiplier = 1f;

        /// <summary>Apply a posture-driven speed multiplier (crouch/crawl). Pass 1 to clear.</summary>
        public void SetPostureSpeedMultiplier(float multiplier)
        {
            _postureSpeedMultiplier = Mathf.Max(0.05f, multiplier);
            RefreshAppliedSpeed();
        }

        /// <summary>
        /// Activates or deactivates sprint. Speed and stamina drain are handled in Update.
        /// Does nothing if there is no stamina left.
        /// </summary>
        public void SetSprintActive(bool sprint)
        {
            bool wouldSprint = sprint && (unitStats == null || unitStats.Stamina > 0f);
            if (wouldSprint == IsSprinting) return;
            IsSprinting = wouldSprint;
            RefreshAppliedSpeed();
        }

        private void RefreshAppliedSpeed()
        {
            float agilityMult = (unitStats != null) ? unitStats.GetAgilityMoveSpeedMultiplier() : 1f;
            float encumbranceMult = (unitStats != null && unitInventory != null)
                ? unitStats.GetEncumbranceSpeedMultiplier(unitInventory.CarryRatio)
                : 1f;

            _baseAppliedMoveSpeed = Mathf.Max(0.1f, baseMovSpeed * agilityMult * encumbranceMult * _postureSpeedMultiplier);

            if (!IsSprinting)
            {
                _sprintBlend = 0f;
            }

            ApplyEffectiveMoveSpeed();
        }

        private void ApplyEffectiveMoveSpeed()
        {
            float sprintMult = Mathf.Lerp(1f, sprintSpeedMultiplier, _sprintBlend);
            float moveStartRampMult = EvaluateMoveStartRampMultiplier();
            moveSpeed = Mathf.Max(0.1f, _baseAppliedMoveSpeed * sprintMult * moveStartRampMult);
            if (agent != null)
            {
                agent.speed = moveSpeed;
            }
        }

        private float EvaluateMoveStartRampMultiplier()
        {
            if (_moveStartRampTimer <= 0f || _moveStartRampDuration <= 0f)
            {
                return 1f;
            }

            float elapsed01 = 1f - (_moveStartRampTimer / _moveStartRampDuration);
            return Mathf.Lerp(_moveStartRampInitialMultiplier, 1f, Mathf.Clamp01(elapsed01));
        }

        private void TickMoveStartRamp()
        {
            if (_moveStartRampTimer <= 0f)
            {
                return;
            }

            bool hasMoveIntent = MoveInput.sqrMagnitude > 0.0001f || HasMoveTarget;
            if (!hasMoveIntent)
            {
                return;
            }

            _moveStartRampTimer = Mathf.Max(0f, _moveStartRampTimer - Time.deltaTime);
            ApplyEffectiveMoveSpeed();
        }

        public void BeginMoveSpeedRamp(float durationSeconds, float startSpeedMultiplier = 0.35f)
        {
            float clampedDuration = Mathf.Max(0f, durationSeconds);
            if (clampedDuration <= 0f)
            {
                _moveStartRampTimer = 0f;
                _moveStartRampDuration = 0f;
                _moveStartRampInitialMultiplier = 1f;
                ApplyEffectiveMoveSpeed();
                return;
            }

            _moveStartRampDuration = clampedDuration;
            _moveStartRampTimer = clampedDuration;
            _moveStartRampInitialMultiplier = Mathf.Clamp(startSpeedMultiplier, 0.05f, 1f);
            ApplyEffectiveMoveSpeed();
        }

        private void TickSprintBuildUp()
        {
            if (!IsSprinting)
            {
                if (_sprintBlend > 0f)
                {
                    _sprintBlend = 0f;
                    ApplyEffectiveMoveSpeed();
                }

                return;
            }

            bool hasMoveIntent = MoveInput.sqrMagnitude > 0.0001f || HasMoveTarget;
            if (!hasMoveIntent)
            {
                if (_sprintBlend > 0f)
                {
                    _sprintBlend = 0f;
                    ApplyEffectiveMoveSpeed();
                }

                return;
            }

            float blendStep = Time.deltaTime / Mathf.Max(0.01f, sprintBuildUpSeconds);
            float nextBlend = Mathf.MoveTowards(_sprintBlend, 1f, blendStep);
            if (!Mathf.Approximately(nextBlend, _sprintBlend))
            {
                _sprintBlend = nextBlend;
                ApplyEffectiveMoveSpeed();
            }
        }

        public void SetMoveInput(Vector2 input)
        {
            MoveInput = input;

            if (input.sqrMagnitude > 0f)
            {
                HasMoveTarget = false;
            }

            // Movement input is expected in world-space XZ. Callers (PlayerBrain,
            // InputHandler) are responsible for rotating the vector relative to the
            // active camera before passing it here.
        }

        public void MoveTo(Vector3 worldPosition)
        {
            Vector3 requestedWorldPosition = worldPosition;
            bool shouldLogMoveTo = ShouldLogMoveTo();
            string moveToCaller = shouldLogMoveTo ? ResolveMoveToCaller() : string.Empty;

            // Snap the destination to the nearest walkable NavMesh point so that
            // clicks landing on building walls / rooftops still result in valid movement.
            Vector3 sampleOrigin = worldPosition + Vector3.up * 2f;
            if (NavMesh.SamplePosition(sampleOrigin, out NavMeshHit navHit, 25f, 1 << 0) ||
                NavMesh.SamplePosition(sampleOrigin, out navHit, 25f, NavMesh.AllAreas))
            {
                worldPosition = navHit.position;
            }

            // Skip re-issuing the path if the agent is already heading to the same spot.
            if (agent != null && agent.enabled && agent.isOnNavMesh && HasMoveTarget)
            {
                Vector3 delta = MoveTarget - worldPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.25f && !agent.isStopped && (agent.hasPath || agent.pathPending))
                {
                    LogMoveToCall(moveToCaller, requestedWorldPosition, worldPosition, "ignored-existing-path");
                    return;
                }
            }

            MoveTarget = worldPosition;
            HasMoveTarget = true;
            IsMoving = true;

            if (agent != null && (!agent.enabled || !agent.isOnNavMesh))
            {
                ForceEnableAgent();
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh && agent.gameObject.activeInHierarchy)
            {
                _loggedMoveToAgentUnavailableWarning = false;
                agent.isStopped = false;

                bool destinationAccepted = false;
                try
                {
                    destinationAccepted = agent.SetDestination(worldPosition);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"[UnitController] SetDestination failed: {exception.Message}", this);
                    destinationAccepted = false;
                }

                if (destinationAccepted)
                {
                    LogMoveToCall(moveToCaller, requestedWorldPosition, worldPosition, "set-destination");
                }
                else
                {
                    Debug.LogWarning($"[UnitController] Destination rejected by NavMeshAgent at {worldPosition}.", this);
                    LogMoveToCall(moveToCaller, requestedWorldPosition, worldPosition, "destination-rejected");
                }
            }
            else
            {
                TryLogAgentUnavailableMoveToWarning();
                LogMoveToCall(moveToCaller, requestedWorldPosition, worldPosition, "agent-unavailable");
            }
        }

        private void TryLogAgentUnavailableMoveToWarning()
        {
            if (_loggedMoveToAgentUnavailableWarning)
            {
                return;
            }

            if (role != UnitRole.Player && role != UnitRole.SquadMember && role != UnitRole.Survivor)
            {
                return;
            }

            _loggedMoveToAgentUnavailableWarning = true;
            Debug.LogWarning(
                $"[UnitController] MoveTo: agent not on NavMesh for {name}; using transform fallback movement (enabled={agent?.enabled}, isOnNavMesh={agent?.isOnNavMesh}).",
                this);
        }

        private bool ShouldLogMoveTo()
        {
            if (!logMoveToCalls)
            {
                return false;
            }

            if (logMoveToCallsForPlayerOnly && role != UnitRole.Player)
            {
                return false;
            }

            return true;
        }

        private void LogMoveToCall(string caller, Vector3 requestedPosition, Vector3 resolvedPosition, string state)
        {
            if (!ShouldLogMoveTo())
            {
                return;
            }

            float cooldown = Mathf.Max(0f, moveToLogCooldownSeconds);
            if (cooldown > 0f && Time.unscaledTime < _nextMoveToLogAt)
            {
                return;
            }

            _nextMoveToLogAt = Time.unscaledTime + cooldown;

            string origin = string.IsNullOrWhiteSpace(caller) ? "unknown" : caller;
            string message = $"[UnitController] {name} MoveTo state={state} caller={origin} requested={requestedPosition} resolved={resolvedPosition}";

            if (includeMoveToStackTrace)
            {
                message += $"\n{new System.Diagnostics.StackTrace(2, false)}";
            }

            Debug.Log(message, this);
        }

        private string ResolveMoveToCaller()
        {
            System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace(2, false);
            System.Diagnostics.StackFrame[] frames = stackTrace.GetFrames();
            if (frames == null)
            {
                return "unknown";
            }

            for (int i = 0; i < frames.Length; i++)
            {
                System.Reflection.MethodBase method = frames[i].GetMethod();
                if (method == null)
                {
                    continue;
                }

                System.Type declaringType = method.DeclaringType;
                if (declaringType == null || declaringType == typeof(UnitController))
                {
                    continue;
                }

                return $"{declaringType.FullName}.{method.Name}";
            }

            return "unknown";
        }

        public void Stop()
        {
            MoveInput = Vector2.zero;
            HasMoveTarget = false;
            IsMoving = false;
            desiredMoveDirection = Vector3.zero;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        /// <summary>
        /// Smooth navmesh-constrained displacement over time (e.g. a dodge step).
        /// Uses <see cref="NavMeshAgent.Move"/> each frame so the character stays on the navmesh
        /// without interrupting any active path.
        /// </summary>
        public void BeginDodgeStep(Vector3 worldDir, float distance, float durationSeconds)
        {
            if (agent == null || !agent.isOnNavMesh || durationSeconds <= 0f) return;
            StopCoroutine(nameof(DodgeStepRoutine)); // cancel any in-flight dodge
            StartCoroutine(DodgeStepRoutine(worldDir.normalized, distance, durationSeconds));
        }

        private IEnumerator DodgeStepRoutine(Vector3 dir, float distance, float duration)
        {
            float elapsed = 0f;
            float speed = distance / duration;
            while (elapsed < duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                if (agent != null && agent.isOnNavMesh)
                    agent.Move(dir * speed * dt);
                yield return null;
            }
        }


        public void Rotate(Vector3 direction)
        {
            Vector3 planarDirection = new Vector3(direction.x, 0f, direction.z);

            if (planarDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        public void FacePositionInstant(Vector3 worldPosition)
        {
            Vector3 planarDirection = worldPosition - transform.position;
            planarDirection.y = 0f;

            if (planarDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
        }

        public void RotateTowardsPosition(Vector3 worldPosition, float maxDegreesPerSecond)
        {
            Vector3 planarDirection = worldPosition - transform.position;
            planarDirection.y = 0f;

            if (planarDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
            float turnSpeed = Mathf.Max(0f, maxDegreesPerSecond);

            if (turnSpeed <= 0f)
            {
                transform.rotation = targetRotation;
                return;
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        private float _agentCheckTimer;
        private float _picHeartbeat;

        private void Update()
        {
            _agentCheckTimer += Time.deltaTime;

            if (unitHealth != null && unitHealth.IsDead)
            {
                if (HasMoveTarget || IsMoving)
                {
                    Stop();
                }

                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }

                UpdateAnimator(0f);
                return;
            }

            if (!InputEnabled && role == UnitRole.Player)
            {
                IsMoving = false;

                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }

                UpdateAnimator(0f);
                return;
            }

            TickSprintBuildUp();
            TickMoveStartRamp();

            // NavMeshAgent handles its own movement; just track state and rotation.
            if (agent != null && agent.isOnNavMesh)
            {
                float arrivalTolerance = Mathf.Max(agent.stoppingDistance, 0.35f);
                bool closeEnoughByPath = !agent.pathPending && agent.remainingDistance <= arrivalTolerance;

                bool closeEnoughByTarget = false;
                if (HasMoveTarget)
                {
                    Vector3 toTarget = MoveTarget - transform.position;
                    toTarget.y = 0f;
                    closeEnoughByTarget = toTarget.sqrMagnitude <= arrivalTolerance * arrivalTolerance;
                }

                if (closeEnoughByPath || closeEnoughByTarget)
                {
                    Stop();
                    _arrivalStallTimer = 0f;
                    UpdateAnimator(0f);
                    return;
                }

                bool hasRoute = agent.pathPending || agent.hasPath;
                float speed = agent.velocity.magnitude;
                bool velocityMoving = speed > 0.05f;

                // If we have a path but velocity stays near zero, treat as stalled and stop.
                if (!agent.pathPending && hasRoute && !velocityMoving)
                {
                    _arrivalStallTimer += Time.deltaTime;
                    if (_arrivalStallTimer >= 0.35f)
                    {
                        Stop();
                        _arrivalStallTimer = 0f;
                        UpdateAnimator(0f);
                        return;
                    }
                }
                else
                {
                    _arrivalStallTimer = 0f;
                }

                bool agentMoving = !agent.isStopped && hasRoute && velocityMoving;
                IsMoving = agentMoving;

                if (agentMoving && agent.velocity.sqrMagnitude > 0.01f)
                {
                    Rotate(agent.velocity);
                    float distThisFrame = speed * Time.deltaTime;
                    TryRecordHeavyCarryWalkDistance(distThisFrame);
                    TickStamina(distThisFrame, sprinting: IsSprinting);
                }
                else
                {
                    TickStamina(0f, sprinting: false);
                }

                if (!agentMoving && HasMoveTarget)
                {
                    HasMoveTarget = false;
                }

                UpdateAnimator(agentMoving ? speed : 0f);
                return;
            }

            // Fallback: direct movement for units without a NavMeshAgent.
            desiredMoveDirection = ResolveDesiredDirection();

            if (desiredMoveDirection.sqrMagnitude <= 0.0001f)
            {
                IsMoving = false;
                return;
            }

            IsMoving = true;
            Vector3 movementDelta = desiredMoveDirection.normalized * moveSpeed * Time.deltaTime;
            ApplyMovement(movementDelta);
            Rotate(desiredMoveDirection);
            float fallbackDist = movementDelta.magnitude;
            TryRecordHeavyCarryWalkDistance(fallbackDist);
            TickStamina(fallbackDist, sprinting: IsSprinting);
            UpdateAnimator(moveSpeed);
        }

        /// <summary>
        /// Sets the animator Speed parameter. Fetches the Animator lazily because
        /// UMA builds the character (and its Animator) asynchronously after spawn.
        /// </summary>
        private void UpdateAnimator(float speed)
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator == null) return;
            }

            if (speed <= 0.01f || !IsMoving)
            {
                _animator.SetFloat(SpeedHash, 0f);
                return;
            }

            // Smooth damp so blend tree transitions feel natural.
            _animator.SetFloat(SpeedHash, speed, animDampTime, Time.deltaTime);
        }

        private Vector3 ResolveDesiredDirection()
        {
            if (role == UnitRole.Player && MoveInput.sqrMagnitude > 0.0001f)
            {
                return new Vector3(MoveInput.x, 0f, MoveInput.y);
            }

            if (!HasMoveTarget)
            {
                return Vector3.zero;
            }

            Vector3 toTarget = MoveTarget - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= stoppingDistance * stoppingDistance)
            {
                Stop();
                return Vector3.zero;
            }

            return toTarget;
        }

        private void ApplyMovement(Vector3 movementDelta)
        {
            if (useRigidbodyMovement && movementBody != null)
            {
                movementBody.MovePosition(movementBody.position + movementDelta);
                return;
            }

            transform.position += movementDelta;
        }

        private void TryRecordHeavyCarryWalkDistance(float distanceMeters)
        {
            if (distanceMeters <= 0f || unitStats == null || unitInventory == null)
            {
                return;
            }

            unitStats.RecordHeavyCarryWalkDistance(distanceMeters, unitInventory.CarryRatio);
        }

        private void TickStamina(float distanceMeters, bool sprinting)
        {
            if (unitStats == null) return;

            if (sprinting && distanceMeters > 0f)
            {
                if (unitStats.Stamina > 0f)
                {
                    unitStats.DrainStamina(unitStats.StaminaDrainPerSecondSprint * Time.deltaTime);
                    unitStats.RecordSprintDistance(distanceMeters);
                    unitStats.RecordExertionTime(Time.deltaTime);
                    _staminaRegenCooldownAt = Time.time + unitStats.StaminaRegenDelaySeconds;
                }
                else
                {
                    // Out of stamina — stop sprinting.
                    IsSprinting = false;
                    RefreshAppliedSpeed();
                }
            }
            else if (Time.time >= _staminaRegenCooldownAt)
            {
                float regenRate = (distanceMeters > 0f)
                    ? unitStats.StaminaRegenPerSecondWalk
                    : unitStats.StaminaRegenPerSecondIdle;
                unitStats.RegenStamina(regenRate * Time.deltaTime);
            }
        }
    }

    /// <summary>
    /// Supported unit archetypes for shared character systems.
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