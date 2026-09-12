#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Core;
using Zombera.Systems;
using Random = UnityEngine.Random;

#endregion

// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable ConvertIfStatementToSwitchExpression
// ReSharper disable ConvertSwitchStatementToSwitchExpression
// ReSharper disable InvertIf
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable MergeIntoPattern
// ReSharper disable MergeIntoLogicalPattern
// ReSharper disable UseIndexFromEndExpression
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable UseRangeIndexer

namespace Zombera.AI
{
    /// <summary>
    ///     Bridges combat and health events into zombie animation triggers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ZombieAnimationController : MonoBehaviour
    {
        private const float SharedPlayerResolveIntervalSeconds = 0.25f;

        private static Transform s_cachedPlayerTransform;
        private static float s_nextSharedPlayerResolveAt;

        [Header("References")] [SerializeField]
        private Animator animator;

        [SerializeField] private Unit unit;
        [SerializeField] private UnitHealth unitHealth;

        [Header("Animator Parameters")] [SerializeField]
        private string attackTriggerParameter = "AttackTrigger";

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

        [Header("Behavior")] [SerializeField] private bool triggerAttackFromCombatTicks = true;

        [SerializeField] private bool triggerDodgeFromCombatTicks = true;
        [SerializeField] private bool triggerHitFromCombatTicks = true;
        [SerializeField] [Min(0f)] private float maxAttackWindupAnimationDistance = 1.6f;
        [SerializeField] private bool stunOnPlayerMeleeHits = true;
        [SerializeField] [Min(0f)] private float playerHitStunSeconds = 1f;
        [SerializeField] [Min(0f)] private float criticalPlayerHitStunBonusSeconds;
        [SerializeField] private string locomotionStateName = "Locomotion";
        [SerializeField] private string speedFloatParameter = "Speed";
        [SerializeField] [Min(0f)] private float locomotionLoopRestartThresholdNormalizedTime = 0.98f;
        [SerializeField] [Min(0f)] private float minimumSpeedForLocomotionLoopRestart = 0.1f;

        [Header("Locomotion Smoothing")]
        [Tooltip("Animator.SetFloat damping for Speed. 0 = instant. Crowds: 0.12–0.18 typical.")]
        [SerializeField] [Min(0f)]
        private float locomotionSpeedDampTime = 0.14f;

        [Tooltip("Animator.SetFloat damping for VelocityX / VelocityZ. 0 = instant.")]
        [SerializeField] [Min(0f)]
        private float locomotionVelocityDampTime = 0.11f;

        [Header("Animator Performance")]
        [SerializeField] private bool applyAnimatorCullDefaults = true;

        [SerializeField] private AnimatorCullingMode animatorCullingMode = AnimatorCullingMode.CullCompletely;

        [Header("Weighted Hit Reactions")] [SerializeField]
        private bool useWeightedHitReactions = true;

        [SerializeField] [Min(0.1f)] private float damageForMaxHitWeight = 24f;
        [SerializeField] [Min(0f)] private float unmatchedHitReactionBaseWeight = 1f;
        [SerializeField] [Min(0f)] private float cachedCombatHitContextTtlSeconds = 0.25f;

        [SerializeField] private WeightedHitReactionRule[] weightedHitReactionRules =
        {
            new()
            {
                clipNameKeywords = new[] { "knockback", "reaction" },
                baseWeight = 0.2f,
                damageInfluence = 1.3f,
                stunChanceInfluence = 2.4f,
                minDamage01 = 0.35f,
                minStunChance01 = 0.12f
            },
            new()
            {
                clipNameKeywords = new[] { "head", "shoulder" },
                baseWeight = 0.35f,
                damageInfluence = 0.75f,
                stunChanceInfluence = 0.6f,
                minDamage01 = 0.15f,
                minStunChance01 = 0f
            },
            new()
            {
                clipNameKeywords = new[] { "stomach", "chest", "torso" },
                baseWeight = 0.35f,
                damageInfluence = 0.55f,
                stunChanceInfluence = 0.3f,
                minDamage01 = 0f,
                minStunChance01 = 0f
            }
        };

        [Header("Death Body Presentation")] [SerializeField]
        private bool disableAiOnDeath = true;

        [SerializeField] private bool disableNavMeshAgentOnDeath = true;
        [SerializeField] private bool tryEnableRagdollOnDeath = true;
        [SerializeField] [Min(0f)] private float ragdollActivationDelaySeconds = 0.35f;
        [SerializeField] private bool disableAnimatorWhenRagdollEnabled = true;
        [SerializeField] private bool disableMainColliderWhenRagdolled = true;

        [Header("Legacy Animator Fallback")] [SerializeField]
        private bool forceStateFallbackWhenTriggerSet = true;

        [SerializeField] private string deadStateName = "Dead";
        [SerializeField] [Min(0f)] private float fallbackCrossFadeSeconds = 0.04f;

        [Header("Folder Clip Variants")] [SerializeField]
        private bool enableFolderClipVariants = true;

        [SerializeField] private AnimationClip[] zombieFolderClips = Array.Empty<AnimationClip>();

        [Header("Explicit Combat Idle / Reaction / Death Variants")] [SerializeField]
        private AnimationClip[] combatIdleOverrideClips = Array.Empty<AnimationClip>();

        [SerializeField] private AnimationClip[] reactionOverrideClips = Array.Empty<AnimationClip>();
        [SerializeField] private AnimationClip[] deathOverrideClips = Array.Empty<AnimationClip>();

        [Header("Override Base Clips")] [SerializeField]
        private AnimationClip baseIdleClip;

        [SerializeField] private AnimationClip baseCombatIdleClip;
        [SerializeField] private AnimationClip baseLocomotionClip;
        [SerializeField] private AnimationClip baseAttackClip;

        [Tooltip("Alternate attack clip (e.g. Scratch). Chosen at altAttackChance frequency.")] [SerializeField]
        private AnimationClip altAttackClip;

        [Tooltip("Probability (0–1) that the alternate attack clip plays instead of the base.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float altAttackChance = 0.8f;

        [SerializeField] private AnimationClip baseDodgeClip;
        [SerializeField] private AnimationClip baseHitClip;
        [SerializeField] private AnimationClip baseDeathClip;
        [SerializeField] private AnimationClip baseDeathClipSecondary;
        private readonly List<AnimationClip> _attackVariants = new();
        private readonly List<AnimationClip> _combatIdleVariants = new();
        private readonly List<AnimationClip> _deathVariants = new();
        private readonly List<AnimationClip> _dodgeVariants = new();
        private readonly List<AnimationClip> _filteredHitVariants = new();
        private readonly List<float> _filteredHitVariantWeights = new();
        private readonly List<AnimationClip> _hitVariants = new();

        private readonly List<AnimationClip> _idleVariants = new();
        private readonly List<AnimationClip> _locomotionVariants = new();
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _runtimeOverrides = new();
        private int _altAttackTriggerHash;

        private int _attackTriggerHash;
        private float _cachedIncomingHitContextExpiresAt;
        private float _cachedIncomingHitDamage;
        private CombatReactionArea _cachedIncomingHitReactionArea = CombatReactionArea.Default;
        private float _cachedIncomingHitStunChance01;
        private bool _combatTickSubscribed;
        private int _deathRollHash;
        private int _dieTriggerHash;
        private int _dodgeTriggerHash;
        private bool _folderVariantCategoriesBuilt;
        private bool _hasAltAttackTrigger;
        private bool _hasAppliedDeathPresentation;

        private bool _hasAttackTrigger;
        private bool _hasDeathRoll;
        private bool _hasDieTrigger;
        private bool _hasDodgeTrigger;
        private bool _hasHitTrigger;
        private bool _hasIsDeadBool;
        private bool _hasIsInCombatBool;
        private bool _hasRagdollRig;
        private bool _hasSpawnTrigger;
        private bool _hasSpeedFloat;
        private bool _hasVelocityX;
        private bool _hasVelocityZ;

        private bool _healthSubscribed;
        private int _hitTriggerHash;
        private int _isDeadHash;
        private int _isInCombatHash;
        private int _lastDodgeTriggerFrame = -1;
        private int _lastForcedLocomotionRestartFrame = -1;
        private int _lastHitTriggerFrame = -1;
        private Collider _mainCollider;
        private NavMeshAgent _navMeshAgent;
        private float _nextUpdateTime;
        private Coroutine _pendingRagdollCoroutine;
        private Collider[] _ragdollColliders = Array.Empty<Collider>();
        private bool _ragdollRigCached;
        private Rigidbody[] _ragdollRigidbodies = Array.Empty<Rigidbody>();
        private Rigidbody _rootRigidbody;

        private AnimatorOverrideController _runtimeOverrideController;
        private int _spawnTriggerHash;
        private int _speedFloatHash;
        private UnitController _unitController;

        private float _updateInterval;
        private int _velocityXHash;
        private int _velocityZHash;
        private Animator _initializedAnimator;
        private RuntimeAnimatorController _initializedAnimatorController;

        private ZombieController _zombieAi;
        private ZombieStateMachine _zombieStateMachine;

        /// <summary>Plays the attack animation. Call this for non-combat attack actions (e.g. breaking a door).</summary>
        public void TriggerAttackAnim()
        {
            TouchSplitMembersForAnalysis();
            TriggerAttack();
        }

        /// <summary>Plays the spawn animation.</summary>
        public void TriggerSpawnAnim()
        {
            TouchSplitMembersForAnalysis();
            if (animator == null || !_hasSpawnTrigger) return;
            animator.SetTrigger(_spawnTriggerHash);
        }

        private static void KeepMutableForSplitAnalysis<T>(ref T value)
        {
            // Intentionally empty. Passing by ref keeps split-host fields visible to analyzers.
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TouchSplitMembersForAnalysis()
        {
            KeepMutableForSplitAnalysis(ref _cachedIncomingHitReactionArea);
            KeepMutableForSplitAnalysis(ref _hasSpawnTrigger);
            KeepMutableForSplitAnalysis(ref _lastDodgeTriggerFrame);
            KeepMutableForSplitAnalysis(ref _lastForcedLocomotionRestartFrame);
            KeepMutableForSplitAnalysis(ref _lastHitTriggerFrame);
            KeepMutableForSplitAnalysis(ref _ragdollColliders);
            KeepMutableForSplitAnalysis(ref _ragdollRigidbodies);
            KeepMutableForSplitAnalysis(ref _spawnTriggerHash);
        }

        [Serializable]
        private sealed class WeightedHitReactionRule
        {
            [Tooltip("Clip-name keywords this weighting rule applies to.")]
            public string[] clipNameKeywords;

            [Min(0f)] public float baseWeight = 0.5f;
            [Range(-1f, 4f)] public float damageInfluence;
            [Range(-1f, 4f)] public float stunChanceInfluence;
            [Range(0f, 1f)] public float minDamage01;
            [Range(0f, 1f)] public float minStunChance01;
        }
    }
}