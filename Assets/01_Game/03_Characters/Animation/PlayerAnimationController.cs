#region

using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Zombera.Combat;
using Zombera.Core;
using Random = UnityEngine.Random;

#endregion

// ReSharper disable Unity.PerformanceCriticalCodeInvocation
// ReSharper disable Unity.PerformanceCriticalCodeNullComparison
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable InvertIf
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable DuplicatedSequentialIfBodies

namespace Zombera.Characters
{
    /// <summary>
    ///     Bridges combat events into player attack/hit animations.
    ///     Attach to the Player GameObject alongside UnitController.
    ///     Animator Controller parameters required:
    ///     AttackTrigger  (Trigger) — fires on each attack swing
    ///     AttackJab/Cross/Hook/Uppercut/Knee/Combo/LowKickTrigger (optional Trigger set)
    ///     — weighted attack system uses these when present
    ///     DodgeTrigger   (Trigger) — fires when the player dodges an attack
    ///     HitTrigger     (Trigger) — fires when the player takes a hit
    ///     DieTrigger     (Trigger) — fires on death
    ///     IsDead         (Bool)    — stays true after death
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class PlayerAnimationController : MonoBehaviour
    {
        [Header("References")] [SerializeField]
        private Unit unit;

        [SerializeField] private UnitHealth unitHealth;
        [SerializeField] private WeaponSystem weaponSystem;

        [Header("Animator Parameters")] [SerializeField]
        private string attackTriggerParameter = "AttackTrigger";

        [SerializeField] private string dodgeLeftTriggerParameter = "DodgeLeftTrigger";
        [SerializeField] private string dodgeRightTriggerParameter = "DodgeRightTrigger";
        [SerializeField] private string hitTriggerParameter = "HitTrigger";
        [SerializeField] private string dieTriggerParameter = "DieTrigger";
        [SerializeField] private string isDeadParameter = "IsDead";

        [Header("Behavior")] [SerializeField] private bool triggerDodgeFromCombatTicks = true;

        [SerializeField] [Min(0f)] private float maxAttackWindupAnimationDistance = 1.6f;
        [SerializeField] private bool suppressDefensiveReactionsWhileAttacking = true;
        [SerializeField] [Min(0f)] private float defensiveReactionSuppressAfterAttackTriggerSeconds = 0.18f;
        [SerializeField] [Min(0f)] private float combatStateHoldSeconds = 1.1f;

        [Header("Weighted Attacks")] [SerializeField]
        private WeightedAttackOption[] weightedAttackOptions =
        {
            new()
            {
                attackStyle = CombatAttackStyle.Jab,
                weight = 3f,
                triggerParameter = "AttackJabTrigger",
                fallbackStateName = "Attack_Jab",
                preferredReactionArea = CombatReactionArea.Chest,
                clipNameKeywords = new[] { "jab" }
            },
            new()
            {
                attackStyle = CombatAttackStyle.Cross,
                weight = 2.25f,
                triggerParameter = "AttackCrossTrigger",
                fallbackStateName = "Attack_Cross",
                preferredReactionArea = CombatReactionArea.Head,
                clipNameKeywords = new[] { "cross" }
            },
            new()
            {
                attackStyle = CombatAttackStyle.Hook,
                weight = 1.75f,
                triggerParameter = "AttackHookTrigger",
                fallbackStateName = "Attack_Hook",
                preferredReactionArea = CombatReactionArea.ShoulderRight,
                clipNameKeywords = new[] { "hook" }
            },
            new()
            {
                attackStyle = CombatAttackStyle.Uppercut,
                weight = 1.35f,
                triggerParameter = "AttackUppercutTrigger",
                fallbackStateName = "Attack_Uppercut",
                preferredReactionArea = CombatReactionArea.Head,
                clipNameKeywords = new[] { "uppercut" }
            },
            new()
            {
                attackStyle = CombatAttackStyle.Knee,
                weight = 1.1f,
                triggerParameter = "AttackKneeTrigger",
                fallbackStateName = "Attack_Knee",
                preferredReactionArea = CombatReactionArea.Stomach,
                clipNameKeywords = new[] { "knee" }
            },
            new()
            {
                attackStyle = CombatAttackStyle.Combo,
                weight = 0.95f,
                triggerParameter = "AttackComboTrigger",
                fallbackStateName = "Attack_Combo",
                preferredReactionArea = CombatReactionArea.Chest,
                clipNameKeywords = new[] { "combo" }
            },
            new()
            {
                attackStyle = CombatAttackStyle.LowKick,
                weight = 0.8f,
                triggerParameter = "AttackLowKickTrigger",
                fallbackStateName = "Attack_LowKick",
                preferredReactionArea = CombatReactionArea.Head,
                clipNameKeywords = new[] { "kick" }
            }
        };

        [Header("Attack Reaction Timing")]
        [Tooltip("When a hit lands, the latest cue at or before current attack time is used.")]
        [SerializeField]
        private AttackReactionTimeline[] attackReactionTimelines =
        {
            new()
            {
                attackStyle = CombatAttackStyle.Combo,
                fallbackReactionArea = CombatReactionArea.Chest,
                timedReactions = new TimedReactionArea[]
                {
                    new() { hitTimeSeconds = 0.14f, reactionArea = CombatReactionArea.Chest },
                    new() { hitTimeSeconds = 1.04f, reactionArea = CombatReactionArea.Chest },
                    new() { hitTimeSeconds = 1.17f, reactionArea = CombatReactionArea.Head }
                }
            },
            new()
            {
                attackStyle = CombatAttackStyle.Hook,
                fallbackReactionArea = CombatReactionArea.Chest,
                timedReactions = new TimedReactionArea[]
                {
                    new() { hitTimeSeconds = 0.08f, reactionArea = CombatReactionArea.Chest }
                }
            },
            new()
            {
                attackStyle = CombatAttackStyle.Uppercut,
                fallbackReactionArea = CombatReactionArea.Head,
                timedReactions = new TimedReactionArea[]
                {
                    new() { hitTimeSeconds = 0.10f, reactionArea = CombatReactionArea.Head }
                }
            },
            new()
            {
                attackStyle = CombatAttackStyle.LowKick,
                fallbackReactionArea = CombatReactionArea.Head,
                timedReactions = new TimedReactionArea[]
                {
                    new() { hitTimeSeconds = 0.11f, reactionArea = CombatReactionArea.Head }
                }
            }
        };

        [Header("Combat Entry")] [SerializeField]
        private string combatEntryTriggerParameter = "CombatEntryTrigger";

        [SerializeField] private string combatEntryStateName = "CombatEntry";
        [SerializeField] [Min(0f)] private float combatEntryTriggerCooldownSeconds = 0.4f;

        [Header("Facing Turn")] [SerializeField]
        private bool playRandomTurnOnFacing;

        [SerializeField] private string turnLeftStateName = "TurnLeft";
        [SerializeField] private string turnRightStateName = "TurnRight";
        [SerializeField] private bool biasTurnDirectionTowardTargetSide = true;
        [SerializeField] [Range(0f, 1f)] private float oppositeTurnDirectionChance = 0.05f;
        [SerializeField] [Min(0f)] private float minimumFacingTurnAngleDegrees = 8f;
        [SerializeField] [Range(0f, 180f)] private float maximumFacingTurnAnimationAngleDegrees = 85f;
        [SerializeField] [Min(0f)] private float turnAnimationCooldownSeconds = 0.1f;

        [Header("Movement Override")] [SerializeField]
        private string locomotionStateName = "Locomotion";

        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] [Range(0f, 180f)] private float turnAroundAngleDegrees = 100f;

        [Header("Legacy Animator Fallback")] [SerializeField]
        private bool forceStateFallbackWhenTriggerSet;

        [SerializeField] private string attackStateName = "Attack";
        [SerializeField] private string dodgeLeftStateName = "DodgeLeft";
        [SerializeField] private string dodgeRightStateName = "DodgeRight";
        [SerializeField] [Min(0f)] private float dodgeStepDistance = 0.24f;
        [SerializeField] [Min(0.05f)] private float dodgeStepDurationSeconds = 0.18f;
        [SerializeField] [Min(0f)] private float dodgeFaceLockTurnSpeedDegreesPerSecond = 1080f;
        [SerializeField] [Min(0f)] private float dodgeFaceLockExtraSeconds = 0.08f;
        [SerializeField] private string hitStateName = "Hit";
        [SerializeField] private string deadStateName = "Dead";
        [SerializeField] [Min(0f)] private float fallbackCrossFadeSeconds = 0.04f;

        [Header("Combat Eye Focus")] [SerializeField]
        private bool enableCombatEyeFocus = true;

        [SerializeField] [Min(0f)] private float combatLookAtHeightOffset = 1.5f;
        [SerializeField] [Range(0f, 1f)] private float combatLookAtOverallWeight = 0.75f;
        [SerializeField] [Range(0f, 1f)] private float combatLookAtBodyWeight = 0.12f;
        [SerializeField] [Range(0f, 1f)] private float combatLookAtHeadWeight = 0.85f;
        [SerializeField] [Range(0f, 1f)] private float combatLookAtEyesWeight = 1f;
        [SerializeField] [Range(0f, 1f)] private float combatLookAtClampWeight = 0.55f;
        [SerializeField] [Min(0.01f)] private float combatLookAtWeightLerpSpeed = 7f;
        [SerializeField] [Min(0.01f)] private float combatLookAtPositionLerpSpeed = 9f;
        [SerializeField] [Min(0f)] private float combatLookTargetSwitchDelaySeconds = 0.15f;

        [Header("Folder Clip Variants")] [SerializeField]
        private bool enableFolderClipVariants = true;

        [SerializeField] private AnimationClip[] playerFolderClips = Array.Empty<AnimationClip>();
        public AnimationClip[] FolderClips => playerFolderClips;

        [Header("Locomotion Parameters")] [SerializeField]
        private string speedParameter = "Speed";

        [SerializeField] private string velocityXParameter = "VelocityX";
        [SerializeField] private string velocityZParameter = "VelocityZ";
        [SerializeField] private string isSprintingParameter = "IsSprinting";
        [SerializeField] private string isCrouchingParameter = "IsCrouching";
        [SerializeField] private string isCrawlingParameter = "IsCrawling";
        [SerializeField] private string isSittingParameter = "IsSitting";
        [SerializeField] private string isInCombatParameter = "IsInCombat";

        [Header("Locomotion Smoothing")]
        [Tooltip("Animator.SetFloat damping for Speed. 0 = no smoothing. Typical 0.08–0.18.")]
        [SerializeField] [Min(0f)]
        private float locomotionSpeedDampTime = 0.10f;

        [Tooltip("Animator.SetFloat damping for VelocityX / VelocityZ (blend tree axes). 0 = instant. Often slightly lower than Speed for responsive turns.")]
        [SerializeField] [Min(0f)]
        private float locomotionVelocityDampTime = 0.08f;

        [Tooltip(
            "Planar speed below this (m/s) is treated as zero for locomotion parameters. Suppresses NavMeshAgent avoidance jitter so idle/combat idle does not crawl the blend tree.")]
        [SerializeField] [Min(0f)]
        private float locomotionIdleVelocityDeadZone = 0.10f;

        [Header("Animator Performance")]
        [SerializeField] private bool applyAnimatorCullDefaults = true;

        [SerializeField] private AnimatorCullingMode animatorCullingMode = AnimatorCullingMode.CullCompletely;

        [Header("Bow Parameters")] [SerializeField]
        private string bowEquippedParameter = "BowEquipped";

        [SerializeField] private string bowAimYParameter = "BowAimY";
        [SerializeField] private string bowNotchParameter = "BowNotch";
        [SerializeField] private string bowShootParameter = "BowShoot";
        [SerializeField] private string bowRapidFireParameter = "BowRapidFire";
        [SerializeField] [Min(0f)] private float bowAimHeightOffset = 1.45f;
        [SerializeField] [Min(0f)] private float bowAimActivationMaxSpeedMetersPerSecond = 0.08f;
        [SerializeField] [Range(0f, 180f)] private float bowAimFacingToleranceDegrees = 12f;
        [SerializeField] [Range(0f, 1f)] private float bowAnimationMaxNormalizedMoveSpeed = 0.18f;
        [SerializeField] private string bowNotchStateName = "BowNotch";
        [SerializeField] private string bowShootStateName = "BowShoot";
        [SerializeField] [Min(0f)] private float bowShootFallbackCrossFadeSeconds = 0.05f;

        [Header("Bow Visual (Wooden Bow)")] [SerializeField]
        private bool driveBowVisualRig = true;

        [SerializeField] private string bowVisualNameContains = "Wooden Bow";
        [SerializeField] private string bowVisualClipName = "Armature.001Action.003";

        [SerializeField] private string bowVisualClipAssetPath =
            "Assets/03_ThirdParty/Free medieval weapons/Models/Wooden Bow.fbx";

        [SerializeField] [Range(0f, 1f)] private float bowVisualHoldNormalizedTime = 0.62f;
        [SerializeField] [Min(0.01f)] private float bowVisualDrawPlaybackSpeed = 1f;
        [SerializeField] [Min(0.01f)] private float bowVisualReleasePlaybackSpeed = 2.2f;

        [Header("Posture Input")] [Header("Override Base Clips")] [SerializeField]
        private AnimationClip baseIdleClip;

        [SerializeField] private AnimationClip baseLocomotionClip;
        [SerializeField] private AnimationClip baseAttackClip;
        [SerializeField] private AnimationClip baseDodgeClip;
        [SerializeField] private AnimationClip baseHitClip;
        [SerializeField] private AnimationClip baseDeadClip;
        [SerializeField] private AnimationClip baseTurnLeftClip;
        [SerializeField] private AnimationClip baseTurnRightClip;
        private readonly List<AnimationClip> _attackVariantFilterBuffer = new();
        private readonly List<AnimationClip> _attackVariants = new();
        private readonly List<CachedWeightedAttackOption> _cachedWeightedAttackOptions = new();
        private readonly List<AnimationClip> _deadVariants = new();
        private readonly List<AnimationClip> _dodgeVariants = new();
        private readonly List<AnimationClip> _hitVariants = new();

        private readonly List<AnimationClip> _idleVariants = new();
        private readonly List<AnimationClip> _locomotionVariants = new();
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _runtimeOverrides = new();
        private readonly List<AnimationClip> _turnLeftVariants = new();
        private readonly List<AnimationClip> _turnRightVariants = new();
        private readonly List<int> _weightedAttackTriggerHashes = new();

        private Animator _animator;

        // Cached hashes
        private int _attackHash;
        private Unit _bowAimTarget;
        private int _bowAimYHash;
        private int _bowEquippedHash;
        private int _bowNotchHash;
        private int _bowRapidFireHash;
        private int _bowShootHash;

        private Animator _bowVisualAnimator;
        private AnimationClip _bowVisualClip;
        private PlayableGraph _bowVisualGraph;
        private AnimationClipPlayable _bowVisualPlayable;
        private BowVisualPlaybackPhase _bowVisualPlaybackPhase;
        private float _bowVisualSampleTime;
        private int _combatEntryHash;
        private float _combatStateHoldUntilTime;
        private Unit _currentCombatLookTarget;
        private float _defensiveReactionSuppressUntilTime;
        private int _dieHash;
        private float _dodgeFaceLockExpiresAt;
        private Unit _dodgeFaceTarget;
        private int _dodgeHash; // DodgeLeft trigger hash
        private int _dodgeRightHash; // DodgeRight trigger hash
        private CombatEncounterManager _encounterManager;
        private bool _eventSubscribed;
        private bool _folderVariantCategoriesBuilt;
        private bool _hasAttack;
        private bool _hasBowAimY;
        private bool _hasBowEquipped;
        private bool _hasBowNotch;
        private bool _hasBowRapidFire;
        private bool _hasBowShoot;
        private bool _hasCombatEntry;
        private bool _hasDie;
        private bool _hasDodgeLeft;
        private bool _hasDodgeRight;
        private bool _hasHit;
        private bool _hasIsCrawling;
        private bool _hasIsCrouching;
        private bool _hasIsDead;
        private bool _hasIsInCombat;
        private bool _hasIsSitting;
        private bool _hasIsSprinting;
        private bool _hasSmoothedCombatLookAtPosition;
        private bool _hasSpeed;
        private bool _hasVelocityX;
        private bool _hasVelocityZ;
        private bool _healthSubscribed;
        private int _hitHash;
        private bool _isApplyingAnimatorIk;
        private bool _isCrawling;
        private int _isCrawlingHash;
        private bool _isCrouching;
        private int _isCrouchingHash;
        private int _isDeadHash;
        private int _isInCombatHash;
        private bool _isSitting;
        private int _isSittingHash;
        private int _isSprintingHash;
        private int _lastHitFrame = -1;
        private float _nextCombatEntryTriggerTime;
        private float _nextCombatLookTargetSwitchAt;
        private float _nextTurnAnimationTime;

        // Flag set once the animator is available (built asynchronously after spawn).
        private bool _paramsCached;

        private AnimatorOverrideController _runtimeOverrideController;
        private Vector3 _smoothedCombatLookAtPosition;
        private float _smoothedCombatLookAtWeight;
        private int _speedHash;

        private UnitController _unitController;
        private int _velocityXHash;
        private int _velocityZHash;

        // ── Lifecycle ────────────────────────────────────────────────────────

        // ── Locomotion & posture driving ─────────────────────────────────────

        /// <summary>Called by PlayerInputController when the player's posture changes.</summary>

        // ── Parameter caching ────────────────────────────────────────────────

        // ── Event subscriptions ───────────────────────────────────────────────

        // ── Event handlers ────────────────────────────────────────────────────

        // ── Trigger helpers ───────────────────────────────────────────────────

#if UNITY_EDITOR
#endif

        private void SetCombatAnimatorFlag(bool value)
        {
            if (!CanWriteAnimatorParameters() || !_hasIsInCombat) return;

            _animator.SetBool(_isInCombatHash, value);
        }

        private bool CanWriteAnimatorParameters()
        {
            return _animator != null
                   && _animator.isActiveAndEnabled
                   && _animator.isInitialized
                   && _animator.runtimeAnimatorController != null
                   && !_animator.hasBoundPlayables;
        }

        [Serializable]
        private sealed class WeightedAttackOption
        {
            public CombatAttackStyle attackStyle = CombatAttackStyle.Jab;
            [Min(0f)] public float weight = 1f;
            public string triggerParameter = "AttackTrigger";
            public string fallbackStateName = "Attack";
            public CombatReactionArea preferredReactionArea = CombatReactionArea.Chest;
            public string[] clipNameKeywords = Array.Empty<string>();
        }

        [Serializable]
        private sealed class TimedReactionArea
        {
            [Min(0f)] public float hitTimeSeconds;
            public CombatReactionArea reactionArea = CombatReactionArea.Chest;
        }

        [Serializable]
        private sealed class AttackReactionTimeline
        {
            public CombatAttackStyle attackStyle = CombatAttackStyle.Unknown;
            public CombatReactionArea fallbackReactionArea = CombatReactionArea.Default;
            public TimedReactionArea[] timedReactions = Array.Empty<TimedReactionArea>();
        }

        private readonly struct CachedWeightedAttackOption
        {
            public readonly CombatAttackStyle AttackStyle;
            public readonly CombatReactionArea PreferredReactionArea;
            public readonly float Weight;
            public readonly int TriggerHash;
            public readonly bool HasTrigger;
            public readonly string FallbackStateName;
            public readonly string[] ClipNameKeywords;

            public CachedWeightedAttackOption(
                CombatAttackStyle attackStyle,
                CombatReactionArea preferredReactionArea,
                float weight,
                int triggerHash,
                bool hasTrigger,
                string fallbackStateName,
                string[] clipNameKeywords)
            {
                AttackStyle = attackStyle;
                PreferredReactionArea = preferredReactionArea;
                Weight = weight;
                TriggerHash = triggerHash;
                HasTrigger = hasTrigger;
                FallbackStateName = fallbackStateName;
                ClipNameKeywords = clipNameKeywords;
            }
        }

        private enum BowVisualPlaybackPhase
        {
            None,
            Draw,
            Hold,
            Release
        }
    }
}