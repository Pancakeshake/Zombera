using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Core;

namespace Zombera.AI
{
    /// <summary>
    /// Bridges combat and health events into zombie animation triggers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZombieAnimationController : MonoBehaviour
    {
        [System.Serializable]
        private sealed class WeightedHitReactionRule
        {
            [Tooltip("Clip-name keywords this weighting rule applies to.")]
            public string[] clipNameKeywords = new string[0];
            [Min(0f)] public float baseWeight = 0.5f;
            [Range(-1f, 4f)] public float damageInfluence = 0f;
            [Range(-1f, 4f)] public float stunChanceInfluence = 0f;
            [Range(0f, 1f)] public float minDamage01;
            [Range(0f, 1f)] public float minStunChance01;
        }

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Unit unit;
        [SerializeField] private UnitHealth unitHealth;

        [Header("Animator Parameters")]
        [SerializeField] private string attackTriggerParameter    = "AttackTrigger";
        [SerializeField] private string altAttackTriggerParameter = "AltAttackTrigger";
        [SerializeField] private string dodgeTriggerParameter = "DodgeTrigger";
        [SerializeField] private string hitTriggerParameter = "HitTrigger";
        [SerializeField] private string dieTriggerParameter = "DieTrigger";
        [SerializeField] private string spawnTriggerParameter = "SpawnTrigger";
        [SerializeField] private string isDeadParameter = "IsDead";
        [SerializeField] private string isInCombatParameter = "IsInCombat";
        [SerializeField] private string velocityXParameter = "VelocityX";
        [SerializeField] private string velocityZParameter = "VelocityZ";
        [SerializeField] private string deathRollParameter = "DeathRoll";

        [Header("Behavior")]
        [SerializeField] private bool triggerAttackFromCombatTicks = true;
        [SerializeField] private bool triggerDodgeFromCombatTicks = true;
        [SerializeField] private bool triggerHitFromCombatTicks = true;
        [SerializeField, Min(0f)] private float maxAttackWindupAnimationDistance = 1.6f;
        [SerializeField] private bool stunOnPlayerMeleeHits = true;
        [SerializeField, Min(0f)] private float playerHitStunSeconds = 1f;
        [SerializeField, Min(0f)] private float criticalPlayerHitStunBonusSeconds = 0f;
        [SerializeField] private string locomotionStateName = "Locomotion";
        [SerializeField] private string speedFloatParameter = "Speed";
        [SerializeField, Min(0f)] private float locomotionLoopRestartThresholdNormalizedTime = 0.98f;
        [SerializeField, Min(0f)] private float minimumSpeedForLocomotionLoopRestart = 0.1f;

        [Header("Weighted Hit Reactions")]
        [SerializeField] private bool useWeightedHitReactions = true;
        [SerializeField, Min(0.1f)] private float damageForMaxHitWeight = 24f;
        [SerializeField, Min(0f)] private float unmatchedHitReactionBaseWeight = 1f;
        [SerializeField, Min(0f)] private float cachedCombatHitContextTtlSeconds = 0.25f;
        [SerializeField] private WeightedHitReactionRule[] weightedHitReactionRules = new WeightedHitReactionRule[]
        {
            new WeightedHitReactionRule
            {
                clipNameKeywords = new[] { "knockback", "reaction" },
                baseWeight = 0.2f,
                damageInfluence = 1.3f,
                stunChanceInfluence = 2.4f,
                minDamage01 = 0.35f,
                minStunChance01 = 0.12f
            },
            new WeightedHitReactionRule
            {
                clipNameKeywords = new[] { "head", "shoulder" },
                baseWeight = 0.35f,
                damageInfluence = 0.75f,
                stunChanceInfluence = 0.6f,
                minDamage01 = 0.15f,
                minStunChance01 = 0f
            },
            new WeightedHitReactionRule
            {
                clipNameKeywords = new[] { "stomach", "chest", "torso" },
                baseWeight = 0.35f,
                damageInfluence = 0.55f,
                stunChanceInfluence = 0.3f,
                minDamage01 = 0f,
                minStunChance01 = 0f
            }
        };

        [Header("Death Body Presentation")]
        [SerializeField] private bool disableAiOnDeath = true;
        [SerializeField] private bool disableNavMeshAgentOnDeath = true;
        [SerializeField] private bool tryEnableRagdollOnDeath = true;
        [SerializeField, Min(0f)] private float ragdollActivationDelaySeconds = 0.35f;
        [SerializeField] private bool disableAnimatorWhenRagdollEnabled = true;
        [SerializeField] private bool disableMainColliderWhenRagdolled = true;

        [Header("Legacy Animator Fallback")]
        [SerializeField] private bool forceStateFallbackWhenTriggerSet = true;
        [SerializeField] private string deadStateName = "Dead";
        [SerializeField, Min(0f)] private float fallbackCrossFadeSeconds = 0.04f;

        [Header("Folder Clip Variants")]
        [SerializeField] private bool enableFolderClipVariants = true;
        [SerializeField] private AnimationClip[] zombieFolderClips = new AnimationClip[0];

        [Header("Explicit Combat Idle / Reaction / Death Variants")]
        [SerializeField] private AnimationClip[] combatIdleOverrideClips = new AnimationClip[0];
        [SerializeField] private AnimationClip[] reactionOverrideClips = new AnimationClip[0];
        [SerializeField] private AnimationClip[] deathOverrideClips = new AnimationClip[0];

        [Header("Override Base Clips")]
        [SerializeField] private AnimationClip baseIdleClip;
        [SerializeField] private AnimationClip baseCombatIdleClip;
        [SerializeField] private AnimationClip baseLocomotionClip;
        [SerializeField] private AnimationClip baseAttackClip;
        [Tooltip("Alternate attack clip (e.g. Scratch). Chosen at altAttackChance frequency.")]
        [SerializeField] private AnimationClip altAttackClip;
        [Tooltip("Probability (0–1) that the alternate attack clip plays instead of the base.")]
        [SerializeField, Range(0f, 1f)] private float altAttackChance = 0.8f;
        [SerializeField] private AnimationClip baseDodgeClip;
        [SerializeField] private AnimationClip baseHitClip;
        [SerializeField] private AnimationClip baseDeathClip;
        [SerializeField] private AnimationClip baseDeathClipSecondary;

        private int attackTriggerHash;
        private int altAttackTriggerHash;
        private int dodgeTriggerHash;
        private int hitTriggerHash;
        private int dieTriggerHash;
        private int spawnTriggerHash;
        private int isDeadHash;
        private int isInCombatHash;
        private int velocityXHash;
        private int velocityZHash;
        private int deathRollHash;

        private bool hasAttackTrigger;
        private bool hasAltAttackTrigger;
        private bool hasDodgeTrigger;
        private bool hasHitTrigger;
        private bool hasDieTrigger;
        private bool hasSpawnTrigger;
        private bool hasIsDeadBool;
        private bool hasIsInCombatBool;
        private bool hasSpeedFloat;
        private bool hasVelocityX;
        private bool hasVelocityZ;
        private bool hasDeathRoll;
        private int speedFloatHash;
        private int lastForcedLocomotionRestartFrame = -1;

        private bool healthSubscribed;
        private bool combatTickSubscribed;
        private int lastHitTriggerFrame = -1;
        private int lastDodgeTriggerFrame = -1;
        private bool hasAppliedDeathPresentation;

        private ZombieAI zombieAi;
        private ZombieStateMachine zombieStateMachine;
        private UnitController unitController;
        private NavMeshAgent navMeshAgent;
        private Collider mainCollider;
        private Rigidbody rootRigidbody;
        private Rigidbody[] ragdollRigidbodies = System.Array.Empty<Rigidbody>();
        private Collider[] ragdollColliders = System.Array.Empty<Collider>();
        private bool ragdollRigCached;
        private bool hasRagdollRig;
        private Coroutine pendingRagdollCoroutine;

        private AnimatorOverrideController runtimeOverrideController;
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> runtimeOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

        private readonly List<AnimationClip> idleVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> locomotionVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> attackVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> dodgeVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> hitVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> deathVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> combatIdleVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> filteredHitVariants = new List<AnimationClip>();
        private readonly List<float> filteredHitVariantWeights = new List<float>();
        private bool folderVariantCategoriesBuilt;
        private float cachedIncomingHitDamage;
        private float cachedIncomingHitStunChance01;
        private CombatReactionArea cachedIncomingHitReactionArea = CombatReactionArea.Default;
        private float cachedIncomingHitContextExpiresAt;

        private void Awake()
        {
            AutoResolveReferences();
            CacheDeathPresentationReferences();
            EnsureRootMotionDisabled();
            BuildFolderVariantCategories();
            ResolveAttackClipsFromAnimator();
            CacheAnimatorParameters();
            EnsureRuntimeOverrideController();
            ApplyIdleAndLocomotionVariants();
        }

        // If baseAttackClip / altAttackClip are not wired in the inspector,
        // find them by name from the animator controller's embedded clip list.
        private void ResolveAttackClipsFromAnimator()
        {
            if (animator == null) return;
            RuntimeAnimatorController ctrl = animator.runtimeAnimatorController;
            if (ctrl == null) return;

            foreach (AnimationClip clip in ctrl.animationClips)
            {
                if (baseAttackClip == null && clip.name == "Zombie_Bite")
                    baseAttackClip = clip;
                if (altAttackClip == null && clip.name == "Zombie_Scratch")
                    altAttackClip = clip;

                if (baseCombatIdleClip == null)
                {
                    string normalizedName = clip.name.ToLowerInvariant();
                    if (normalizedName.Contains("combat_idle_zombie"))
                        baseCombatIdleClip = clip;
                }
            }
        }

        private void OnEnable()
        {
            AutoResolveReferences();
            CacheDeathPresentationReferences();
            RestoreAlivePresentationState();
            EnsureRootMotionDisabled();
            BuildFolderVariantCategories();
            CacheAnimatorParameters();
            EnsureRuntimeOverrideController();
            ApplyIdleAndLocomotionVariants();
            SubscribeHealthEvents();
            TrySubscribeCombatTickEvents();
            SyncDeadState();
            SyncCombatState();
        }

        private void OnDisable()
        {
            CancelPendingRagdoll();
            UnsubscribeHealthEvents();
            UnsubscribeCombatTickEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeHealthEvents();
            UnsubscribeCombatTickEvents();
        }

        private void Update()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();

                if (animator != null)
                {
                    EnsureRootMotionDisabled();
                    CacheAnimatorParameters();
                    EnsureRuntimeOverrideController();
                    ApplyIdleAndLocomotionVariants();
                    SyncDeadState();
                    SyncCombatState();
                }
            }
            else
            {
                TryRestartNonLoopingLocomotionState();
                DriveLocomotionParameters();
                SyncCombatState();
            }

            if (!combatTickSubscribed)
            {
                TrySubscribeCombatTickEvents();
            }
        }

        private void DriveLocomotionParameters()
        {
            if (animator == null) return;
            if (!hasSpeedFloat && !hasVelocityX && !hasVelocityZ) return;

            float speed = 0f;
            Vector3 localVel = Vector3.zero;

            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                Vector3 worldVelocity = navMeshAgent.velocity;
                speed = worldVelocity.magnitude;
                if (speed > 0.01f)
                    localVel = transform.InverseTransformDirection(worldVelocity / speed);
            }

            if (hasSpeedFloat)
                animator.SetFloat(speedFloatHash, speed);
            if (hasVelocityX)
                animator.SetFloat(velocityXHash, localVel.x);
            if (hasVelocityZ)
                animator.SetFloat(velocityZHash, localVel.z);
        }

        private void AutoResolveReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (unit == null || unit.gameObject.scene != gameObject.scene)
            {
                unit = GetComponent<Unit>();
            }

            if (unitHealth == null || unitHealth.gameObject.scene != gameObject.scene)
            {
                unitHealth = GetComponent<UnitHealth>();
            }
        }

        private void CacheDeathPresentationReferences()
        {
            if (zombieAi == null || zombieAi.gameObject.scene != gameObject.scene)
            {
                zombieAi = GetComponent<ZombieAI>();
            }

            if (zombieStateMachine == null || zombieStateMachine.gameObject.scene != gameObject.scene)
            {
                zombieStateMachine = GetComponent<ZombieStateMachine>();
            }

            if (unitController == null || unitController.gameObject.scene != gameObject.scene)
            {
                unitController = GetComponent<UnitController>();
            }

            if (navMeshAgent == null || navMeshAgent.gameObject.scene != gameObject.scene)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (mainCollider == null || mainCollider.gameObject.scene != gameObject.scene)
            {
                mainCollider = GetComponent<Collider>();
            }

            if (rootRigidbody == null || rootRigidbody.gameObject.scene != gameObject.scene)
            {
                rootRigidbody = GetComponent<Rigidbody>();
            }

            CacheRagdollRig();
        }

        private void CacheRagdollRig()
        {
            if (ragdollRigCached)
            {
                return;
            }

            ragdollRigCached = true;

            Rigidbody[] allRigidbodies = GetComponentsInChildren<Rigidbody>(true);
            List<Rigidbody> ragdollBodies = new List<Rigidbody>();

            for (int i = 0; i < allRigidbodies.Length; i++)
            {
                Rigidbody body = allRigidbodies[i];

                if (body == null || body == rootRigidbody)
                {
                    continue;
                }

                ragdollBodies.Add(body);
            }

            ragdollRigidbodies = ragdollBodies.ToArray();

            Collider[] allColliders = GetComponentsInChildren<Collider>(true);
            List<Collider> ragdollBodyColliders = new List<Collider>();

            for (int i = 0; i < allColliders.Length; i++)
            {
                Collider collider = allColliders[i];

                if (collider == null || collider == mainCollider)
                {
                    continue;
                }

                Rigidbody attachedBody = collider.attachedRigidbody;
                if (attachedBody == null || attachedBody == rootRigidbody)
                {
                    continue;
                }

                ragdollBodyColliders.Add(collider);
            }

            ragdollColliders = ragdollBodyColliders.ToArray();
            hasRagdollRig = ragdollRigidbodies.Length > 0;

            SetRagdollEnabled(false);
        }

        private void SetRagdollEnabled(bool enabled)
        {
            if (!hasRagdollRig)
            {
                return;
            }

            for (int i = 0; i < ragdollRigidbodies.Length; i++)
            {
                Rigidbody body = ragdollRigidbodies[i];
                if (body == null)
                {
                    continue;
                }

                body.isKinematic = !enabled;
                body.useGravity = enabled;
                body.detectCollisions = enabled;

                if (enabled)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            for (int i = 0; i < ragdollColliders.Length; i++)
            {
                Collider collider = ragdollColliders[i];
                if (collider == null)
                {
                    continue;
                }

                collider.enabled = enabled;
            }
        }

        private void RestoreAlivePresentationState()
        {
            hasAppliedDeathPresentation = false;
            CancelPendingRagdoll();

            if (animator != null && !animator.enabled)
            {
                animator.enabled = true;
            }

            if (mainCollider != null && !mainCollider.enabled)
            {
                mainCollider.enabled = true;
            }

            SetRagdollEnabled(false);
        }

        private void CancelPendingRagdoll()
        {
            if (pendingRagdollCoroutine == null)
            {
                return;
            }

            StopCoroutine(pendingRagdollCoroutine);
            pendingRagdollCoroutine = null;
        }

        private void EnsureRootMotionDisabled()
        {
            if (animator == null)
            {
                return;
            }

            if (animator.applyRootMotion)
            {
                animator.applyRootMotion = false;
            }
        }

        private void CacheAnimatorParameters()
        {
            attackTriggerHash    = Animator.StringToHash(attackTriggerParameter);
            altAttackTriggerHash = Animator.StringToHash(altAttackTriggerParameter);
            dodgeTriggerHash     = Animator.StringToHash(dodgeTriggerParameter);
            hitTriggerHash    = Animator.StringToHash(hitTriggerParameter);
            dieTriggerHash    = Animator.StringToHash(dieTriggerParameter);
            spawnTriggerHash  = Animator.StringToHash(spawnTriggerParameter);
            isDeadHash        = Animator.StringToHash(isDeadParameter);
            isInCombatHash    = Animator.StringToHash(isInCombatParameter);
            speedFloatHash    = Animator.StringToHash(speedFloatParameter);
            velocityXHash     = Animator.StringToHash(velocityXParameter);
            velocityZHash     = Animator.StringToHash(velocityZParameter);
            deathRollHash     = Animator.StringToHash(deathRollParameter);

            if (animator == null)
            {
                hasAttackTrigger    = false;
                hasAltAttackTrigger = false;
                hasDodgeTrigger     = false;
                hasHitTrigger    = false;
                hasDieTrigger    = false;
                hasSpawnTrigger  = false;
                hasIsDeadBool    = false;
                hasIsInCombatBool = false;
                hasSpeedFloat    = false;
                hasVelocityX     = false;
                hasVelocityZ     = false;
                hasDeathRoll     = false;
                return;
            }

            hasAttackTrigger    = HasParameter(attackTriggerParameter,    AnimatorControllerParameterType.Trigger);
            hasAltAttackTrigger = HasParameter(altAttackTriggerParameter, AnimatorControllerParameterType.Trigger);
            hasDodgeTrigger     = HasParameter(dodgeTriggerParameter,     AnimatorControllerParameterType.Trigger);
            hasHitTrigger    = HasParameter(hitTriggerParameter,    AnimatorControllerParameterType.Trigger);
            hasDieTrigger    = HasParameter(dieTriggerParameter,    AnimatorControllerParameterType.Trigger);
            hasSpawnTrigger  = HasParameter(spawnTriggerParameter,  AnimatorControllerParameterType.Trigger);
            hasIsDeadBool    = HasParameter(isDeadParameter,        AnimatorControllerParameterType.Bool);
            hasIsInCombatBool = HasParameter(isInCombatParameter,    AnimatorControllerParameterType.Bool);
            hasSpeedFloat    = HasParameter(speedFloatParameter,    AnimatorControllerParameterType.Float);
            hasVelocityX     = HasParameter(velocityXParameter,     AnimatorControllerParameterType.Float);
            hasVelocityZ     = HasParameter(velocityZParameter,     AnimatorControllerParameterType.Float);
            hasDeathRoll     = HasParameter(deathRollParameter,     AnimatorControllerParameterType.Float);

            BuildFolderVariantCategories();
        }

        private void TryRestartNonLoopingLocomotionState()
        {
            if (animator == null || string.IsNullOrWhiteSpace(locomotionStateName))
            {
                return;
            }

            if (animator.IsInTransition(0))
            {
                return;
            }

            if (Time.frameCount == lastForcedLocomotionRestartFrame)
            {
                return;
            }

            if (hasSpeedFloat)
            {
                float speedValue = animator.GetFloat(speedFloatHash);
                if (speedValue < Mathf.Max(0f, minimumSpeedForLocomotionLoopRestart))
                {
                    return;
                }
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (!StateMatchesName(stateInfo, locomotionStateName) || stateInfo.loop)
            {
                return;
            }

            float restartThreshold = Mathf.Clamp01(locomotionLoopRestartThresholdNormalizedTime);
            if (stateInfo.normalizedTime < restartThreshold)
            {
                return;
            }

            lastForcedLocomotionRestartFrame = Time.frameCount;

            int stateHash = Animator.StringToHash(locomotionStateName);
            if (animator.HasState(0, stateHash))
            {
                animator.Play(stateHash, 0, 0f);
                return;
            }

            string qualifiedStateName = "Base Layer." + locomotionStateName;
            int qualifiedHash = Animator.StringToHash(qualifiedStateName);
            if (animator.HasState(0, qualifiedHash))
            {
                animator.Play(qualifiedHash, 0, 0f);
            }
        }

        private static bool StateMatchesName(AnimatorStateInfo stateInfo, string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            int shortHash = Animator.StringToHash(stateName);
            if (stateInfo.shortNameHash == shortHash)
            {
                return true;
            }

            int fullPathHash = Animator.StringToHash("Base Layer." + stateName);
            return stateInfo.fullPathHash == fullPathHash;
        }

        private bool HasParameter(string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];

                if (parameter.type == parameterType && string.Equals(parameter.name, parameterName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void SubscribeHealthEvents()
        {
            if (healthSubscribed || unitHealth == null)
            {
                return;
            }

            unitHealth.Damaged += HandleDamaged;
            unitHealth.Died += HandleDied;
            healthSubscribed = true;
        }

        private void UnsubscribeHealthEvents()
        {
            if (!healthSubscribed || unitHealth == null)
            {
                healthSubscribed = false;
                return;
            }

            unitHealth.Damaged -= HandleDamaged;
            unitHealth.Died -= HandleDied;
            healthSubscribed = false;
        }

        private void TrySubscribeCombatTickEvents()
        {
            if (combatTickSubscribed || EventSystem.Instance == null)
            {
                return;
            }

            EventSystem.Instance.Subscribe<CombatAttackWindupEvent>(HandleCombatAttackWindup);
            EventSystem.Instance.Subscribe<CombatTickResolvedEvent>(HandleCombatTickResolved);
            combatTickSubscribed = true;
        }

        private void UnsubscribeCombatTickEvents()
        {
            if (!combatTickSubscribed)
            {
                return;
            }

            if (EventSystem.Instance != null)
            {
                EventSystem.Instance.Unsubscribe<CombatAttackWindupEvent>(HandleCombatAttackWindup);
                EventSystem.Instance.Unsubscribe<CombatTickResolvedEvent>(HandleCombatTickResolved);
            }

            combatTickSubscribed = false;
        }

        private void HandleCombatAttackWindup(CombatAttackWindupEvent gameEvent)
        {
            if (unit == null)
            {
                return;
            }

            if (triggerAttackFromCombatTicks && gameEvent.Attacker == unit && IsWithinAttackAnimationDistance(gameEvent.Defender))
            {
                TriggerAttack();
            }
        }

        private void HandleCombatTickResolved(CombatTickResolvedEvent gameEvent)
        {
            if (unit == null)
            {
                return;
            }

            if (gameEvent.Defender == unit && gameEvent.DidHit)
            {
                CacheIncomingHitContext(gameEvent);
            }

            TryApplyPlayerHitStun(gameEvent);

            if (triggerDodgeFromCombatTicks
                && gameEvent.Defender == unit
                && gameEvent.DidDefenderDodge
                && IsWithinAttackAnimationDistance(gameEvent.Attacker))
            {
                TriggerDodge();
                return;
            }

            // Fallback only: if health callbacks are missing, still react to resolved hits.
            if (triggerHitFromCombatTicks && unitHealth == null && gameEvent.Defender == unit && gameEvent.DidHit)
            {
                TriggerHit(gameEvent.PreferredReactionArea, gameEvent.Damage, gameEvent.AttackerStunChance01);
            }
        }

        private void TryApplyPlayerHitStun(CombatTickResolvedEvent gameEvent)
        {
            if (!stunOnPlayerMeleeHits
                || !gameEvent.DidHit
                || gameEvent.Defender != unit
                || gameEvent.Attacker == null
                || gameEvent.Attacker.Role != UnitRole.Player)
            {
                return;
            }

            float stunSeconds = Mathf.Max(0f, playerHitStunSeconds);
            if (gameEvent.IsCritical)
            {
                stunSeconds += Mathf.Max(0f, criticalPlayerHitStunBonusSeconds);
            }

            if (stunSeconds <= 0f)
            {
                return;
            }

            if (zombieStateMachine == null || zombieStateMachine.gameObject.scene != gameObject.scene)
            {
                zombieStateMachine = GetComponent<ZombieStateMachine>();
            }

            zombieStateMachine?.ApplyCombatStun(stunSeconds);
        }

        private void HandleDamaged(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            CombatReactionArea preferredReactionArea = CombatReactionArea.Default;
            float damage = Mathf.Max(0f, amount);
            float stunChance01 = 0f;

            if (TryGetCachedIncomingHitContext(out float cachedDamage, out float cachedStunChance01, out CombatReactionArea cachedReactionArea))
            {
                if (cachedDamage > 0f)
                {
                    damage = cachedDamage;
                }

                stunChance01 = cachedStunChance01;
                if (cachedReactionArea != CombatReactionArea.Default)
                {
                    preferredReactionArea = cachedReactionArea;
                }
            }

            if (unit != null)
            {
                if (CombatAttackPresentationRegistry.TryConsumeIncomingReactionHint(unit, out CombatReactionArea hintArea)
                    && hintArea != CombatReactionArea.Default)
                {
                    preferredReactionArea = hintArea;
                }
            }

            TriggerHit(preferredReactionArea, damage, stunChance01);
        }

        private void HandleDied()
        {
            SyncDeadState();

            // Randomise which death state plays (Dead1/Dead2 in the controller).
            if (animator != null && hasDeathRoll)
                animator.SetFloat(deathRollHash, Random.value);

            TryApplyDeathVariants();

            bool triggered = false;

            if (animator != null && hasDieTrigger)
            {
                animator.SetTrigger(dieTriggerHash);
                triggered = true;
            }

            TryPlayFallbackState(deadStateName, triggered);
            ApplyDeathPresentation();
        }

        private void ApplyDeathPresentation()
        {
            if (hasAppliedDeathPresentation)
            {
                return;
            }

            hasAppliedDeathPresentation = true;

            unitController?.Stop();

            if (disableAiOnDeath && zombieAi != null)
            {
                zombieAi.SetActive(false);
            }

            if (disableNavMeshAgentOnDeath && navMeshAgent != null && navMeshAgent.enabled)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.ResetPath();
                navMeshAgent.enabled = false;
            }

            if (!tryEnableRagdollOnDeath || !hasRagdollRig)
            {
                return;
            }

            float delay = Mathf.Max(0f, ragdollActivationDelaySeconds);

            if (delay <= 0f)
            {
                EnableRagdollNow();
                return;
            }

            CancelPendingRagdoll();
            pendingRagdollCoroutine = StartCoroutine(EnableRagdollAfterDelay(delay));
        }

        private IEnumerator EnableRagdollAfterDelay(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            pendingRagdollCoroutine = null;

            if (!isActiveAndEnabled || unitHealth == null || !unitHealth.IsDead)
            {
                yield break;
            }

            EnableRagdollNow();
        }

        private void EnableRagdollNow()
        {
            if (!hasRagdollRig)
            {
                return;
            }

            if (disableAnimatorWhenRagdollEnabled && animator != null)
            {
                animator.enabled = false;
            }

            if (disableMainColliderWhenRagdolled && mainCollider != null)
            {
                mainCollider.enabled = false;
            }

            SetRagdollEnabled(true);
        }

        /// <summary>Plays the attack animation. Call this for non-combat attack actions (e.g. breaking a door).</summary>
        public void TriggerAttackAnim() => TriggerAttack();

        /// <summary>Plays the spawn animation.</summary>
        public void TriggerSpawnAnim()
        {
            if (animator == null || !hasSpawnTrigger) return;
            animator.SetTrigger(spawnTriggerHash);
        }

        private void TriggerAttack()
        {
            if (animator == null) return;

            // 80% chance: fire AltAttackTrigger (Scratch state)
            // 20% chance: fire AttackTrigger (Bite state)
            bool useAlt = hasAltAttackTrigger && Random.value < altAttackChance;
            if (useAlt)
            {
                animator.SetTrigger(altAttackTriggerHash);
            }
            else if (hasAttackTrigger)
            {
                animator.SetTrigger(attackTriggerHash);
            }
        }

        private void TriggerDodge()
        {
            if (Time.frameCount == lastDodgeTriggerFrame)
            {
                return;
            }

            lastDodgeTriggerFrame = Time.frameCount;

            if (animator == null)
            {
                return;
            }

            AnimationClip dodgeBaseClip = baseDodgeClip != null ? baseDodgeClip : baseLocomotionClip;
            List<AnimationClip> preferredDodgeVariants = dodgeVariants.Count > 0 ? dodgeVariants : locomotionVariants;
            TryApplyRandomVariant(dodgeBaseClip, preferredDodgeVariants);

            if (hasDodgeTrigger)
            {
                animator.SetTrigger(dodgeTriggerHash);
                return;
            }

            // Fallback to hit so dodge still has visual feedback on legacy controllers.
            if (hasHitTrigger)
            {
                animator.SetTrigger(hitTriggerHash);
            }
        }

        private void TriggerHit(
            CombatReactionArea preferredReactionArea = CombatReactionArea.Default,
            float damage = 0f,
            float stunChance01 = 0f)
        {
            if (Time.frameCount == lastHitTriggerFrame)
            {
                return;
            }

            lastHitTriggerFrame = Time.frameCount;

            if (animator == null || !hasHitTrigger)
            {
                return;
            }

            if (!TryApplyHitReactionVariant(preferredReactionArea, damage, stunChance01))
            {
                TryApplyRandomVariant(baseHitClip, hitVariants, ignoreVariantToggle: true);
            }

            animator.SetTrigger(hitTriggerHash);
        }

        private bool TryApplyHitReactionVariant(CombatReactionArea preferredReactionArea, float damage, float stunChance01)
        {
            if (baseHitClip == null
                || hitVariants.Count == 0)
            {
                return false;
            }

            float normalizedDamage = Mathf.Clamp01(damage / Mathf.Max(0.1f, damageForMaxHitWeight));
            float normalizedStunChance01 = Mathf.Clamp01(stunChance01);
            string[] areaKeywords = ReactionAreaKeywords(preferredReactionArea);

            filteredHitVariants.Clear();
            filteredHitVariantWeights.Clear();

            for (int i = 0; i < hitVariants.Count; i++)
            {
                AnimationClip variant = hitVariants[i];
                if (variant == null)
                {
                    continue;
                }

                string variantName = variant.name.ToLowerInvariant();

                if (areaKeywords != null
                    && areaKeywords.Length > 0
                    && !ContainsAny(variantName, areaKeywords))
                {
                    continue;
                }

                float weight = EvaluateHitReactionWeight(variantName, normalizedDamage, normalizedStunChance01);
                if (weight > 0f)
                {
                    filteredHitVariants.Add(variant);
                    filteredHitVariantWeights.Add(weight);
                }
            }

            if (filteredHitVariants.Count == 0 && areaKeywords != null && areaKeywords.Length > 0)
            {
                for (int i = 0; i < hitVariants.Count; i++)
                {
                    AnimationClip variant = hitVariants[i];
                    if (variant == null)
                    {
                        continue;
                    }

                    string variantName = variant.name.ToLowerInvariant();
                    float weight = EvaluateHitReactionWeight(variantName, normalizedDamage, normalizedStunChance01);
                    if (weight > 0f)
                    {
                        filteredHitVariants.Add(variant);
                        filteredHitVariantWeights.Add(weight);
                    }
                }
            }

            if (filteredHitVariants.Count == 0)
            {
                return false;
            }

            AnimationClip selected = SelectWeightedHitReactionVariant(filteredHitVariants, filteredHitVariantWeights);
            if (selected == null)
            {
                return false;
            }

            TryApplySpecificVariant(baseHitClip, selected, ignoreVariantToggle: true);
            return true;
        }

        private void TryApplyDeathVariants()
        {
            if (deathVariants.Count == 0)
            {
                return;
            }

            AnimationClip primaryVariant = deathVariants[Random.Range(0, deathVariants.Count)];
            if (primaryVariant != null)
            {
                TryApplySpecificVariant(baseDeathClip, primaryVariant, ignoreVariantToggle: true);
            }

            AnimationClip secondaryBase = baseDeathClipSecondary;
            if (secondaryBase == null || deathVariants.Count == 0)
            {
                return;
            }

            AnimationClip secondaryVariant = primaryVariant;

            if (deathVariants.Count > 1)
            {
                for (int attempts = 0; attempts < 6; attempts++)
                {
                    AnimationClip candidate = deathVariants[Random.Range(0, deathVariants.Count)];
                    if (candidate != null && candidate != primaryVariant)
                    {
                        secondaryVariant = candidate;
                        break;
                    }
                }

                if (secondaryVariant == null || secondaryVariant == primaryVariant)
                {
                    for (int i = 0; i < deathVariants.Count; i++)
                    {
                        AnimationClip candidate = deathVariants[i];
                        if (candidate != null && candidate != primaryVariant)
                        {
                            secondaryVariant = candidate;
                            break;
                        }
                    }
                }
            }

            if (secondaryVariant != null)
            {
                TryApplySpecificVariant(secondaryBase, secondaryVariant, ignoreVariantToggle: true);
            }
        }

        private void CacheIncomingHitContext(CombatTickResolvedEvent gameEvent)
        {
            cachedIncomingHitDamage = Mathf.Max(0f, gameEvent.Damage);
            cachedIncomingHitStunChance01 = Mathf.Clamp01(gameEvent.AttackerStunChance01);
            cachedIncomingHitReactionArea = gameEvent.PreferredReactionArea;
            cachedIncomingHitContextExpiresAt = Time.time + Mathf.Max(0f, cachedCombatHitContextTtlSeconds);
        }

        private bool TryGetCachedIncomingHitContext(out float damage, out float stunChance01, out CombatReactionArea reactionArea)
        {
            damage = 0f;
            stunChance01 = 0f;
            reactionArea = CombatReactionArea.Default;

            if (cachedIncomingHitContextExpiresAt <= 0f || Time.time > cachedIncomingHitContextExpiresAt)
            {
                return false;
            }

            damage = Mathf.Max(0f, cachedIncomingHitDamage);
            stunChance01 = Mathf.Clamp01(cachedIncomingHitStunChance01);
            reactionArea = cachedIncomingHitReactionArea;
            return true;
        }

        private float EvaluateHitReactionWeight(string variantNameLower, float damage01, float stunChance01)
        {
            float weight = Mathf.Max(0f, unmatchedHitReactionBaseWeight);

            if (!useWeightedHitReactions || weightedHitReactionRules == null || weightedHitReactionRules.Length == 0)
            {
                return Mathf.Max(0.0001f, weight);
            }

            for (int i = 0; i < weightedHitReactionRules.Length; i++)
            {
                WeightedHitReactionRule rule = weightedHitReactionRules[i];
                if (rule == null || !RuleMatchesVariant(rule, variantNameLower, damage01, stunChance01))
                {
                    continue;
                }

                float weightedContribution = Mathf.Max(0f, rule.baseWeight);
                weightedContribution *= Mathf.Max(0f, 1f + damage01 * rule.damageInfluence);
                weightedContribution *= Mathf.Max(0f, 1f + stunChance01 * rule.stunChanceInfluence);
                weight += weightedContribution;
            }

            return Mathf.Max(0.0001f, weight);
        }

        private static bool RuleMatchesVariant(WeightedHitReactionRule rule, string variantNameLower, float damage01, float stunChance01)
        {
            if (rule == null)
            {
                return false;
            }

            if (damage01 < Mathf.Clamp01(rule.minDamage01) || stunChance01 < Mathf.Clamp01(rule.minStunChance01))
            {
                return false;
            }

            if (rule.clipNameKeywords == null || rule.clipNameKeywords.Length == 0)
            {
                return true;
            }

            return ContainsAny(variantNameLower, rule.clipNameKeywords);
        }

        private static AnimationClip SelectWeightedHitReactionVariant(List<AnimationClip> variants, List<float> weights)
        {
            if (variants == null || variants.Count == 0)
            {
                return null;
            }

            if (weights == null || weights.Count != variants.Count)
            {
                return variants[Random.Range(0, variants.Count)];
            }

            float totalWeight = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                totalWeight += Mathf.Max(0f, weights[i]);
            }

            if (totalWeight <= 0.0001f)
            {
                return variants[Random.Range(0, variants.Count)];
            }

            float roll = Random.value * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < variants.Count; i++)
            {
                cumulative += Mathf.Max(0f, weights[i]);
                if (roll <= cumulative)
                {
                    return variants[i];
                }
            }

            return variants[variants.Count - 1];
        }

        private static string[] ReactionAreaKeywords(CombatReactionArea preferredReactionArea)
        {
            switch (preferredReactionArea)
            {
                case CombatReactionArea.Head:
                    return new[] { "head" };
                case CombatReactionArea.ShoulderLeft:
                    return new[] { "shoulder_l", "shoulderl", "left_shoulder", "shoulderleft" };
                case CombatReactionArea.ShoulderRight:
                    return new[] { "shoulder_r", "shoulderr", "right_shoulder", "shoulderright" };
                case CombatReactionArea.Stomach:
                    return new[] { "stomach", "gut", "abdomen", "torso" };
                case CombatReactionArea.Legs:
                    return new[] { "leg", "knee", "shin" };
                case CombatReactionArea.Chest:
                    return new[] { "chest", "torso" };
                default:
                    return null;
            }
        }

        private bool IsWithinAttackAnimationDistance(Unit defender)
        {
            float maxDistance = Mathf.Max(0f, maxAttackWindupAnimationDistance);
            if (maxDistance <= 0f || defender == null)
            {
                return true;
            }

            Vector3 delta = defender.transform.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= maxDistance * maxDistance;
        }

        private void SyncDeadState()
        {
            if (animator == null || !hasIsDeadBool)
            {
                return;
            }

            bool isDead = unitHealth != null && unitHealth.IsDead;
            animator.SetBool(isDeadHash, isDead);
        }

        private void SyncCombatState()
        {
            if (animator == null || !hasIsInCombatBool)
            {
                return;
            }

            bool isDead = unitHealth != null && unitHealth.IsDead;
            bool isInCombat = false;

            if (!isDead && zombieStateMachine != null)
            {
                ZombieState currentState = zombieStateMachine.CurrentState;
                isInCombat = currentState == ZombieState.Chase
                    || currentState == ZombieState.Attack
                    || currentState == ZombieState.AttackDoor;
            }

            animator.SetBool(isInCombatHash, isInCombat);
        }

        private void BuildFolderVariantCategories()
        {
            if (folderVariantCategoriesBuilt)
            {
                return;
            }

            folderVariantCategoriesBuilt = true;

            idleVariants.Clear();
            locomotionVariants.Clear();
            attackVariants.Clear();
            dodgeVariants.Clear();
            hitVariants.Clear();
            deathVariants.Clear();
            combatIdleVariants.Clear();

            AddRangeUnique(combatIdleVariants, combatIdleOverrideClips);

            AddRangeUnique(hitVariants, reactionOverrideClips);
            AddRangeUnique(deathVariants, deathOverrideClips);

            if (zombieFolderClips == null || zombieFolderClips.Length == 0)
            {
                AddUnique(hitVariants, baseHitClip);
                AddUnique(deathVariants, baseDeathClip);
                AddUnique(deathVariants, baseDeathClipSecondary);
                return;
            }

            for (int i = 0; i < zombieFolderClips.Length; i++)
            {
                AnimationClip clip = zombieFolderClips[i];
                if (clip == null)
                {
                    continue;
                }

                string name = clip.name.ToLowerInvariant();
                if (ContainsAny(name, "idle", "scream"))
                {
                    if (ContainsAny(name, "combat"))
                    {
                        AddUnique(combatIdleVariants, clip);
                    }
                    else
                    {
                        AddUnique(idleVariants, clip);
                    }
                }

                if (ContainsAny(name, "walk", "run", "crawl"))
                {
                    AddUnique(locomotionVariants, clip);
                }

                if (ContainsAny(name, "attack", "bite", "neck"))
                {
                    AddUnique(attackVariants, clip);
                }

                if (ContainsAny(name, "scratch", "swipe", "claw"))
                {
                    AddUnique(attackVariants, clip);
                }

                if (ContainsAny(name, "reaction", "hit"))
                {
                    AddUnique(hitVariants, clip);
                }

                if (ContainsAny(name, "death", "dying", "die"))
                {
                    AddUnique(deathVariants, clip);
                }

                if (ContainsAny(name, "dodge", "crawl"))
                {
                    AddUnique(dodgeVariants, clip);
                }
            }

            if (dodgeVariants.Count == 0)
            {
                for (int i = 0; i < locomotionVariants.Count; i++)
                {
                    AddUnique(dodgeVariants, locomotionVariants[i]);
                }
            }

            AddUnique(hitVariants, baseHitClip);
            AddUnique(deathVariants, baseDeathClip);
            AddUnique(deathVariants, baseDeathClipSecondary);
        }

        private void EnsureRuntimeOverrideController(bool ignoreVariantToggle = false)
        {
            if ((!enableFolderClipVariants && !ignoreVariantToggle) || animator == null)
            {
                return;
            }

            RuntimeAnimatorController currentController = animator.runtimeAnimatorController;
            if (currentController == null)
            {
                return;
            }

            RuntimeAnimatorController baseController = currentController;
            if (currentController is AnimatorOverrideController existingOverride && existingOverride.runtimeAnimatorController != null)
            {
                baseController = existingOverride.runtimeAnimatorController;
            }

            bool requiresOverride = runtimeOverrideController == null
                || runtimeOverrideController.runtimeAnimatorController != baseController
                || animator.runtimeAnimatorController != runtimeOverrideController;

            if (!requiresOverride)
            {
                return;
            }

            runtimeOverrideController = new AnimatorOverrideController(baseController);
            animator.runtimeAnimatorController = runtimeOverrideController;
            runtimeOverrides.Clear();
            runtimeOverrideController.GetOverrides(runtimeOverrides);
        }

        private void ApplyIdleAndLocomotionVariants()
        {
            TryApplyRandomVariant(baseIdleClip, idleVariants);

            AnimationClip combatIdleBaseClip = baseCombatIdleClip;
            if (combatIdleBaseClip == null && combatIdleOverrideClips != null && combatIdleOverrideClips.Length > 0)
            {
                combatIdleBaseClip = combatIdleOverrideClips[0];
            }

            if (combatIdleBaseClip != null)
            {
                bool forceCombatIdleOverride = combatIdleVariants.Count > 0;
                TryApplyRandomVariant(combatIdleBaseClip, combatIdleVariants, ignoreVariantToggle: forceCombatIdleOverride);
            }

            // Keep locomotion clips fixed: randomizing the forward base clip can break
            // directional blend-tree mapping (e.g. forward movement using backward clips).
        }

        private void TryApplySpecificVariant(AnimationClip baseClip, AnimationClip candidate, bool ignoreVariantToggle = false)
        {
            if ((!enableFolderClipVariants && !ignoreVariantToggle) || baseClip == null || candidate == null)
                return;

            EnsureRuntimeOverrideController(ignoreVariantToggle);
            if (runtimeOverrideController == null)
                return;

            bool found = false;
            bool changed = false;

            for (int i = 0; i < runtimeOverrides.Count; i++)
            {
                KeyValuePair<AnimationClip, AnimationClip> overridePair = runtimeOverrides[i];
                if (!IsEquivalentBaseClip(overridePair.Key, baseClip)) continue;
                found = true;
                if (overridePair.Value == candidate) continue;
                runtimeOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overridePair.Key, candidate);
                changed = true;
            }

            if (changed)
                runtimeOverrideController.ApplyOverrides(runtimeOverrides);
            else if (!found)
            {
                AnimationClip fallbackBaseClip = FindCompatibleOverrideBaseClip(baseClip);
                if (fallbackBaseClip != null)
                    runtimeOverrideController[fallbackBaseClip.name] = candidate;
                else
                    runtimeOverrideController[baseClip.name] = candidate;
            }
        }

        private void TryApplyRandomVariant(AnimationClip baseClip, List<AnimationClip> variants, bool ignoreVariantToggle = false)
        {
            if ((!enableFolderClipVariants && !ignoreVariantToggle) || baseClip == null || variants == null || variants.Count == 0)
            {
                return;
            }

            EnsureRuntimeOverrideController(ignoreVariantToggle);
            if (runtimeOverrideController == null)
            {
                return;
            }

            AnimationClip candidate = variants[Random.Range(0, variants.Count)];
            if (candidate == null || candidate == baseClip)
            {
                return;
            }

            bool found = false;
            bool changed = false;

            for (int i = 0; i < runtimeOverrides.Count; i++)
            {
                KeyValuePair<AnimationClip, AnimationClip> overridePair = runtimeOverrides[i];
                if (!IsEquivalentBaseClip(overridePair.Key, baseClip))
                {
                    continue;
                }

                found = true;
                if (overridePair.Value == candidate)
                {
                    continue;
                }

                runtimeOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overridePair.Key, candidate);
                changed = true;
            }

            if (changed)
            {
                runtimeOverrideController.ApplyOverrides(runtimeOverrides);
            }
            else if (!found)
            {
                AnimationClip fallbackBaseClip = FindCompatibleOverrideBaseClip(baseClip);
                if (fallbackBaseClip != null)
                {
                    runtimeOverrideController[fallbackBaseClip.name] = candidate;
                }
                else
                {
                    runtimeOverrideController[baseClip.name] = candidate;
                }
            }
        }

        private AnimationClip FindCompatibleOverrideBaseClip(AnimationClip baseClip)
        {
            if (baseClip == null)
            {
                return null;
            }

            for (int i = 0; i < runtimeOverrides.Count; i++)
            {
                AnimationClip key = runtimeOverrides[i].Key;
                if (IsEquivalentBaseClip(key, baseClip))
                {
                    return key;
                }
            }

            return null;
        }

        private static bool IsEquivalentBaseClip(AnimationClip controllerBaseClip, AnimationClip requestedBaseClip)
        {
            if (controllerBaseClip == null || requestedBaseClip == null)
            {
                return false;
            }

            if (controllerBaseClip == requestedBaseClip)
            {
                return true;
            }

            string controllerName = NormalizeClipLookupName(controllerBaseClip.name);
            string requestedName = NormalizeClipLookupName(requestedBaseClip.name);

            if (string.IsNullOrEmpty(controllerName) || string.IsNullOrEmpty(requestedName))
            {
                return false;
            }

            if (controllerName == requestedName)
            {
                return true;
            }

            return controllerName == requestedName + "loop"
                || requestedName == controllerName + "loop";
        }

        private static string NormalizeClipLookupName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            string trimmed = rawName.Trim();
            int pipeIndex = trimmed.LastIndexOf('|');
            if (pipeIndex >= 0 && pipeIndex < trimmed.Length - 1)
            {
                trimmed = trimmed.Substring(pipeIndex + 1);
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(trimmed.Length);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char character = char.ToLowerInvariant(trimmed[i]);
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            string normalized = builder.ToString();
            const string armaturePrefix = "armature";
            if (normalized.StartsWith(armaturePrefix, System.StringComparison.Ordinal))
            {
                normalized = normalized.Substring(armaturePrefix.Length);
            }

            return normalized;
        }

        private static void AddUnique(List<AnimationClip> destination, AnimationClip clip)
        {
            if (destination == null || clip == null || destination.Contains(clip))
            {
                return;
            }

            destination.Add(clip);
        }

        private static void AddRangeUnique(List<AnimationClip> destination, AnimationClip[] clips)
        {
            if (destination == null || clips == null)
            {
                return;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                AddUnique(destination, clips[i]);
            }
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            string normalizedValue = value.ToLowerInvariant();

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && normalizedValue.Contains(token.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private void TryPlayFallbackState(string stateName, bool triggerWasSet)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            if (!forceStateFallbackWhenTriggerSet && triggerWasSet)
            {
                return;
            }

            float fadeSeconds = Mathf.Max(0f, fallbackCrossFadeSeconds);
            const int baseLayer = 0;

            int stateHash = Animator.StringToHash(stateName);
            if (animator.HasState(baseLayer, stateHash))
            {
                animator.CrossFadeInFixedTime(stateHash, fadeSeconds, baseLayer);
                return;
            }

            string qualifiedStateName = "Base Layer." + stateName;
            int qualifiedHash = Animator.StringToHash(qualifiedStateName);
            if (animator.HasState(baseLayer, qualifiedHash))
            {
                animator.CrossFadeInFixedTime(qualifiedHash, fadeSeconds, baseLayer);
            }
        }
    }
}
