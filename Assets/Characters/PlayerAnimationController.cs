using System.Collections.Generic;
using UnityEngine.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using Zombera.Combat;
using Zombera.Core;

namespace Zombera.Characters
{
    /// <summary>
    /// Bridges combat events into player attack/hit animations.
    /// Attach to the Player GameObject alongside UnitController.
    ///
    /// Animator Controller parameters required:
    ///   AttackTrigger  (Trigger) — fires on each attack swing
    ///   AttackJab/Cross/Hook/Uppercut/Knee/Combo/LowKickTrigger (optional Trigger set)
    ///                  — weighted attack system uses these when present
    ///   DodgeTrigger   (Trigger) — fires when the player dodges an attack
    ///   HitTrigger     (Trigger) — fires when the player takes a hit
    ///   DieTrigger     (Trigger) — fires on death
    ///   IsDead         (Bool)    — stays true after death
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        [System.Serializable]
        private sealed class WeightedAttackOption
        {
            public CombatAttackStyle attackStyle = CombatAttackStyle.Jab;
            [Min(0f)] public float weight = 1f;
            public string triggerParameter = "AttackTrigger";
            public string fallbackStateName = "Attack";
            public CombatReactionArea preferredReactionArea = CombatReactionArea.Chest;
            public string[] clipNameKeywords = new string[0];
        }

        [System.Serializable]
        private sealed class TimedReactionArea
        {
            [Min(0f)] public float hitTimeSeconds;
            public CombatReactionArea reactionArea = CombatReactionArea.Chest;
        }

        [System.Serializable]
        private sealed class AttackReactionTimeline
        {
            public CombatAttackStyle attackStyle = CombatAttackStyle.Unknown;
            public CombatReactionArea fallbackReactionArea = CombatReactionArea.Default;
            public TimedReactionArea[] timedReactions = new TimedReactionArea[0];
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

        [Header("References")]
        [SerializeField] private Unit unit;
        [SerializeField] private UnitHealth unitHealth;
        [SerializeField] private WeaponSystem weaponSystem;

        [Header("Animator Parameters")]
        [SerializeField] private string attackTriggerParameter = "AttackTrigger";
        [SerializeField] private string dodgeLeftTriggerParameter  = "DodgeLeftTrigger";
        [SerializeField] private string dodgeRightTriggerParameter = "DodgeRightTrigger";
        [SerializeField] private string hitTriggerParameter   = "HitTrigger";
        [SerializeField] private string dieTriggerParameter   = "DieTrigger";
        [SerializeField] private string isDeadParameter       = "IsDead";

        [Header("Behavior")]
        [SerializeField] private bool triggerDodgeFromCombatTicks = true;
        [SerializeField, Min(0f)] private float maxAttackWindupAnimationDistance = 1.6f;
        [SerializeField] private bool suppressDefensiveReactionsWhileAttacking = true;
        [SerializeField, Min(0f)] private float defensiveReactionSuppressAfterAttackTriggerSeconds = 0.18f;
        [SerializeField, Min(0f)] private float combatStateHoldSeconds = 1.1f;

        [Header("Weighted Attacks")]
        [SerializeField] private WeightedAttackOption[] weightedAttackOptions = new WeightedAttackOption[]
        {
            new WeightedAttackOption
            {
                attackStyle = CombatAttackStyle.Jab,
                weight = 3f,
                triggerParameter = "AttackJabTrigger",
                fallbackStateName = "Attack_Jab",
                preferredReactionArea = CombatReactionArea.Chest,
                clipNameKeywords = new[] { "jab" }
            },
            new WeightedAttackOption
            {
                attackStyle = CombatAttackStyle.Cross,
                weight = 2.25f,
                triggerParameter = "AttackCrossTrigger",
                fallbackStateName = "Attack_Cross",
                preferredReactionArea = CombatReactionArea.Head,
                clipNameKeywords = new[] { "cross" }
            },
            new WeightedAttackOption
            {
                attackStyle = CombatAttackStyle.Hook,
                weight = 1.75f,
                triggerParameter = "AttackHookTrigger",
                fallbackStateName = "Attack_Hook",
                preferredReactionArea = CombatReactionArea.ShoulderRight,
                clipNameKeywords = new[] { "hook" }
            },
            new WeightedAttackOption
            {
                attackStyle = CombatAttackStyle.Uppercut,
                weight = 1.35f,
                triggerParameter = "AttackUppercutTrigger",
                fallbackStateName = "Attack_Uppercut",
                preferredReactionArea = CombatReactionArea.Head,
                clipNameKeywords = new[] { "uppercut" }
            },
            new WeightedAttackOption
            {
                attackStyle = CombatAttackStyle.Knee,
                weight = 1.1f,
                triggerParameter = "AttackKneeTrigger",
                fallbackStateName = "Attack_Knee",
                preferredReactionArea = CombatReactionArea.Stomach,
                clipNameKeywords = new[] { "knee" }
            },
            new WeightedAttackOption
            {
                attackStyle = CombatAttackStyle.Combo,
                weight = 0.95f,
                triggerParameter = "AttackComboTrigger",
                fallbackStateName = "Attack_Combo",
                preferredReactionArea = CombatReactionArea.Chest,
                clipNameKeywords = new[] { "combo" }
            },
            new WeightedAttackOption
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
        [SerializeField] private AttackReactionTimeline[] attackReactionTimelines = new AttackReactionTimeline[]
        {
            new AttackReactionTimeline
            {
                attackStyle = CombatAttackStyle.Combo,
                fallbackReactionArea = CombatReactionArea.Chest,
                timedReactions = new[]
                {
                    new TimedReactionArea { hitTimeSeconds = 0.14f, reactionArea = CombatReactionArea.Chest },
                    new TimedReactionArea { hitTimeSeconds = 1.04f, reactionArea = CombatReactionArea.Chest },
                    new TimedReactionArea { hitTimeSeconds = 1.17f, reactionArea = CombatReactionArea.Head }
                }
            },
            new AttackReactionTimeline
            {
                attackStyle = CombatAttackStyle.Hook,
                fallbackReactionArea = CombatReactionArea.Chest,
                timedReactions = new[]
                {
                    new TimedReactionArea { hitTimeSeconds = 0.08f, reactionArea = CombatReactionArea.Chest }
                }
            },
            new AttackReactionTimeline
            {
                attackStyle = CombatAttackStyle.Uppercut,
                fallbackReactionArea = CombatReactionArea.Head,
                timedReactions = new[]
                {
                    new TimedReactionArea { hitTimeSeconds = 0.10f, reactionArea = CombatReactionArea.Head }
                }
            },
            new AttackReactionTimeline
            {
                attackStyle = CombatAttackStyle.LowKick,
                fallbackReactionArea = CombatReactionArea.Head,
                timedReactions = new[]
                {
                    new TimedReactionArea { hitTimeSeconds = 0.11f, reactionArea = CombatReactionArea.Head }
                }
            }
        };

        [Header("Combat Entry")]
        [SerializeField] private string combatEntryTriggerParameter = "CombatEntryTrigger";
        [SerializeField] private string combatEntryStateName = "CombatEntry";
        [SerializeField, Min(0f)] private float combatEntryTriggerCooldownSeconds = 0.4f;

        [Header("Facing Turn")]
        [SerializeField] private bool playRandomTurnOnFacing = false;
        [SerializeField] private string turnLeftStateName = "TurnLeft";
        [SerializeField] private string turnRightStateName = "TurnRight";
        [SerializeField] private bool biasTurnDirectionTowardTargetSide = true;
        [SerializeField, Range(0f, 1f)] private float oppositeTurnDirectionChance = 0.05f;
        [SerializeField, Min(0f)] private float minimumFacingTurnAngleDegrees = 8f;
        [SerializeField, Range(0f, 180f)] private float maximumFacingTurnAnimationAngleDegrees = 85f;
        [SerializeField, Min(0f)] private float turnAnimationCooldownSeconds = 0.1f;

        [Header("Movement Override")]
        [SerializeField] private string locomotionStateName = "Locomotion";
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField, Range(0f, 180f)] private float turnAroundAngleDegrees = 100f;

        [Header("Legacy Animator Fallback")]
        [SerializeField] private bool forceStateFallbackWhenTriggerSet = false;
        [SerializeField] private string attackStateName = "Attack";
        [SerializeField] private string dodgeLeftStateName  = "DodgeLeft";
        [SerializeField] private string dodgeRightStateName = "DodgeRight";
        [SerializeField, Min(0f)] private float dodgeStepDistance = 0.24f;
        [SerializeField, Min(0.05f)] private float dodgeStepDurationSeconds = 0.18f;
        [SerializeField, Min(0f)] private float dodgeFaceLockTurnSpeedDegreesPerSecond = 1080f;
        [SerializeField, Min(0f)] private float dodgeFaceLockExtraSeconds = 0.08f;
        [SerializeField] private string hitStateName = "Hit";
        [SerializeField] private string deadStateName = "Dead";
        [SerializeField, Min(0f)] private float fallbackCrossFadeSeconds = 0.04f;

        [Header("Combat Eye Focus")]
        [SerializeField] private bool enableCombatEyeFocus = true;
        [SerializeField, Min(0f)] private float combatLookAtHeightOffset = 1.5f;
        [SerializeField, Range(0f, 1f)] private float combatLookAtOverallWeight = 0.75f;
        [SerializeField, Range(0f, 1f)] private float combatLookAtBodyWeight = 0.12f;
        [SerializeField, Range(0f, 1f)] private float combatLookAtHeadWeight = 0.85f;
        [SerializeField, Range(0f, 1f)] private float combatLookAtEyesWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float combatLookAtClampWeight = 0.55f;
        [SerializeField, Min(0.01f)] private float combatLookAtWeightLerpSpeed = 7f;
        [SerializeField, Min(0.01f)] private float combatLookAtPositionLerpSpeed = 9f;
        [SerializeField, Min(0f)] private float combatLookTargetSwitchDelaySeconds = 0.15f;

        [Header("Folder Clip Variants")]
        [SerializeField] private bool enableFolderClipVariants = true;
        [SerializeField] private AnimationClip[] playerFolderClips = new AnimationClip[0];

        [Header("Locomotion Parameters")]
        [SerializeField] private string speedParameter      = "Speed";
        [SerializeField] private string velocityXParameter  = "VelocityX";
        [SerializeField] private string velocityZParameter  = "VelocityZ";
        [SerializeField] private string isSprintingParameter = "IsSprinting";
        [SerializeField] private string isCrouchingParameter = "IsCrouching";
        [SerializeField] private string isCrawlingParameter  = "IsCrawling";
        [SerializeField] private string isSittingParameter   = "IsSitting";
        [SerializeField] private string isInCombatParameter  = "IsInCombat";

        [Header("Bow Parameters")]
        [SerializeField] private string bowEquippedParameter  = "BowEquipped";
        [SerializeField] private string bowAimYParameter      = "BowAimY";
        [SerializeField] private string bowNotchParameter     = "BowNotch";
        [SerializeField] private string bowShootParameter     = "BowShoot";
        [SerializeField] private string bowRapidFireParameter = "BowRapidFire";
        [SerializeField, Min(0f)] private float bowAimHeightOffset = 1.45f;
        [SerializeField, Min(0f)] private float bowAimActivationMaxSpeedMetersPerSecond = 0.08f;
        [SerializeField, Range(0f, 180f)] private float bowAimFacingToleranceDegrees = 12f;
        [SerializeField, Range(0f, 1f)] private float bowAnimationMaxNormalizedMoveSpeed = 0.18f;
        [SerializeField] private string bowNotchStateName = "BowNotch";
        [SerializeField] private string bowShootStateName = "BowShoot";
        [SerializeField, Min(0f)] private float bowShootFallbackCrossFadeSeconds = 0.05f;

        [Header("Bow Visual (Wooden Bow)")]
        [SerializeField] private bool driveBowVisualRig = true;
        [SerializeField] private string bowVisualNameContains = "Wooden Bow";
        [SerializeField] private string bowVisualClipName = "Armature.001Action.003";
        [SerializeField] private string bowVisualClipAssetPath = "Assets/ThirdParty/Free medieval weapons/Models/Wooden Bow.fbx";
        [SerializeField, Range(0f, 1f)] private float bowVisualHoldNormalizedTime = 0.62f;
        [SerializeField, Min(0.01f)] private float bowVisualDrawPlaybackSpeed = 1f;
        [SerializeField, Min(0.01f)] private float bowVisualReleasePlaybackSpeed = 2.2f;

        [Header("Posture Input")]

        [Header("Override Base Clips")]
        [SerializeField] private AnimationClip baseIdleClip;
        [SerializeField] private AnimationClip baseLocomotionClip;
        [SerializeField] private AnimationClip baseAttackClip;
        [SerializeField] private AnimationClip baseDodgeClip;
        [SerializeField] private AnimationClip baseHitClip;
        [SerializeField] private AnimationClip baseDeadClip;
        [SerializeField] private AnimationClip baseTurnLeftClip;
        [SerializeField] private AnimationClip baseTurnRightClip;

        // Cached hashes
        private int _attackHash;
        private int _dodgeHash;       // DodgeLeft trigger hash
        private int _dodgeRightHash;  // DodgeRight trigger hash
        private int _hitHash;
        private int _dieHash;
        private int _isDeadHash;
        private int _combatEntryHash;
        private int _speedHash;
        private int _velocityXHash;
        private int _velocityZHash;
        private int _isSprintingHash;
        private int _isCrouchingHash;
        private int _isCrawlingHash;
        private int _isSittingHash;
        private int _isInCombatHash;
        private int _bowEquippedHash;
        private int _bowAimYHash;
        private int _bowNotchHash;
        private int _bowShootHash;
        private int _bowRapidFireHash;

        // Flag set once the animator is available (UMA builds it async)
        private bool _paramsCached;
        private bool _hasAttack;
        private bool _hasDodgeLeft;
        private bool _hasDodgeRight;
        private bool _hasHit;
        private bool _hasDie;
        private bool _hasIsDead;
        private bool _hasCombatEntry;
        private bool _hasSpeed;
        private bool _hasVelocityX;
        private bool _hasVelocityZ;
        private bool _hasIsSprinting;
        private bool _hasIsCrouching;
        private bool _hasIsCrawling;
        private bool _hasIsSitting;
        private bool _hasIsInCombat;
        private bool _hasBowEquipped;
        private bool _hasBowAimY;
        private bool _hasBowNotch;
        private bool _hasBowShoot;
        private bool _hasBowRapidFire;

        private UnitController _unitController;
        private CombatEncounterManager _encounterManager;
        private bool _isCrouching;
        private bool _isCrawling;
        private bool _isSitting;

        private Animator _animator;
        private bool _healthSubscribed;
        private bool _eventSubscribed;
        private int _lastHitFrame = -1;
        private float _nextTurnAnimationTime;
        private float _nextCombatEntryTriggerTime;
        private float _combatStateHoldUntilTime;
        private float _defensiveReactionSuppressUntilTime;
        private Unit _currentCombatLookTarget;
        private Unit _bowAimTarget;
        private float _nextCombatLookTargetSwitchAt;
        private float _smoothedCombatLookAtWeight;
        private Vector3 _smoothedCombatLookAtPosition;
        private bool _hasSmoothedCombatLookAtPosition;
        private bool _isApplyingAnimatorIk;
        private Unit _dodgeFaceTarget;
        private float _dodgeFaceLockExpiresAt;

        private enum BowVisualPlaybackPhase
        {
            None,
            Draw,
            Hold,
            Release,
        }

        private Animator _bowVisualAnimator;
        private AnimationClip _bowVisualClip;
        private PlayableGraph _bowVisualGraph;
        private AnimationClipPlayable _bowVisualPlayable;
        private BowVisualPlaybackPhase _bowVisualPlaybackPhase;
        private float _bowVisualSampleTime;

        private AnimatorOverrideController _runtimeOverrideController;
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _runtimeOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

        private readonly List<AnimationClip> _idleVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> _locomotionVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> _attackVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> _dodgeVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> _hitVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> _deadVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> _turnLeftVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> _turnRightVariants = new List<AnimationClip>();
        private readonly List<AnimationClip> _attackVariantFilterBuffer = new List<AnimationClip>();
        private readonly List<CachedWeightedAttackOption> _cachedWeightedAttackOptions = new List<CachedWeightedAttackOption>();
        private readonly List<int> _weightedAttackTriggerHashes = new List<int>();
        private bool _folderVariantCategoriesBuilt;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (unit == null)       unit       = GetComponent<Unit>();
            if (unitHealth == null) unitHealth = GetComponent<UnitHealth>();
            if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();
            BuildFolderVariantCategories();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            SetCombatAnimatorFlag(false);
            SetBowAnimatorDefaults();
            ResetBowVisualPlayback(clearRigState: true);
            ResetCombatEyeFocus(immediate: true);
            _bowAimTarget = null;
            _dodgeFaceTarget = null;
            _dodgeFaceLockExpiresAt = 0f;
            _combatStateHoldUntilTime = 0f;
            _defensiveReactionSuppressUntilTime = 0f;
            UnsubscribeEvents();
        }

        private void Update()
        {
            // Animator is built async by UMA — keep trying until it arrives.
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator != null)
                {
                    EnsureRootMotionDisabled();
                    CacheParameters();
                    EnsureRuntimeOverrideController();
                    ApplyIdleAndLocomotionVariants();
                }
            }
            else if (!_paramsCached)
            {
                EnsureRootMotionDisabled();
                CacheParameters();
                EnsureRuntimeOverrideController();
                ApplyIdleAndLocomotionVariants();
            }

            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            if (unitHealth == null)
            {
                unitHealth = GetComponent<UnitHealth>();
            }

            if (weaponSystem == null)
            {
                weaponSystem = GetComponent<WeaponSystem>();
            }

            if (!_healthSubscribed || !_eventSubscribed)
            {
                SubscribeEvents();
            }

            if (_unitController == null)
                _unitController = GetComponent<UnitController>();

            HandlePostureInput();

            if (_animator != null && _paramsCached)
            {
                EnsureRootMotionDisabled();
                DriveLocomotionParameters();
                SyncCombatAnimatorState();
                DriveBowParameters();
                TickBowVisualPlayback();
                TickDodgeFaceLock();
            }
        }

        // ── Locomotion & posture driving ─────────────────────────────────────

        private void DriveLocomotionParameters()
        {
            if (_unitController == null) return;

            Vector3 worldVel = _unitController.WorldVelocity;
            Vector3 localVel = transform.InverseTransformDirection(worldVel);
            float maxSpeed   = Mathf.Max(0.01f, _unitController.MoveSpeed);
            float normX      = Mathf.Clamp(localVel.x / maxSpeed, -1f, 1f);
            float normZ      = Mathf.Clamp(localVel.z / maxSpeed, -1f, 1f);
            float speed      = Mathf.Clamp01(worldVel.magnitude / maxSpeed);

            if (_hasSpeed)       _animator.SetFloat(_speedHash,       speed);
            if (_hasVelocityX)   _animator.SetFloat(_velocityXHash,   normX);
            if (_hasVelocityZ)   _animator.SetFloat(_velocityZHash,   normZ);
            if (_hasIsSprinting) _animator.SetBool(_isSprintingHash,  _unitController.IsSprinting);
        }

        /// <summary>Called by PlayerInputController when the player's posture changes.</summary>
        public void ApplyPostureState(PostureState state)
        {
            _isCrouching = state == PostureState.Crouching;
            _isCrawling  = state == PostureState.Crawling;
            _isSitting   = false;

            if (_animator == null || !_paramsCached) return;
            if (_hasIsCrouching) _animator.SetBool(_isCrouchingHash, _isCrouching);
            if (_hasIsCrawling)  _animator.SetBool(_isCrawlingHash,  _isCrawling);
            if (_hasIsSitting)   _animator.SetBool(_isSittingHash,   _isSitting);
        }

        private void HandlePostureInput()
        {
            // Per-frame stealth XP while crouching/crawling.
            if ((_isCrouching || _isCrawling) && unit?.Stats != null)
            {
                unit.Stats.RecordUndetectedTime(Time.deltaTime);
            }

            // Keep animator bools in sync in case animator was rebuilt this frame.
            if (_animator != null && _paramsCached)
            {
                if (_hasIsCrouching) _animator.SetBool(_isCrouchingHash, _isCrouching);
                if (_hasIsCrawling)  _animator.SetBool(_isCrawlingHash,  _isCrawling);
                if (_hasIsSitting)   _animator.SetBool(_isSittingHash,   _isSitting);
            }
        }

        // ── Parameter caching ────────────────────────────────────────────────

        private void CacheParameters()
        {
            _attackHash     = Animator.StringToHash(attackTriggerParameter);
            _dodgeHash      = Animator.StringToHash(dodgeLeftTriggerParameter);
            _dodgeRightHash = Animator.StringToHash(dodgeRightTriggerParameter);
            _hitHash        = Animator.StringToHash(hitTriggerParameter);
            _dieHash     = Animator.StringToHash(dieTriggerParameter);
            _isDeadHash  = Animator.StringToHash(isDeadParameter);
            _combatEntryHash = Animator.StringToHash(combatEntryTriggerParameter);

            _hasAttack     = HasParam(attackTriggerParameter,          AnimatorControllerParameterType.Trigger);
            _hasDodgeLeft  = HasParam(dodgeLeftTriggerParameter,        AnimatorControllerParameterType.Trigger);
            _hasDodgeRight = HasParam(dodgeRightTriggerParameter,       AnimatorControllerParameterType.Trigger);
            _hasHit        = HasParam(hitTriggerParameter,              AnimatorControllerParameterType.Trigger);
            _hasDie     = HasParam(dieTriggerParameter,   AnimatorControllerParameterType.Trigger);
            _hasIsDead  = HasParam(isDeadParameter,       AnimatorControllerParameterType.Bool);
            _hasCombatEntry = HasParam(combatEntryTriggerParameter, AnimatorControllerParameterType.Trigger);

            _speedHash       = Animator.StringToHash(speedParameter);
            _velocityXHash   = Animator.StringToHash(velocityXParameter);
            _velocityZHash   = Animator.StringToHash(velocityZParameter);
            _isSprintingHash = Animator.StringToHash(isSprintingParameter);
            _isCrouchingHash = Animator.StringToHash(isCrouchingParameter);
            _isCrawlingHash  = Animator.StringToHash(isCrawlingParameter);
            _isSittingHash   = Animator.StringToHash(isSittingParameter);
            _isInCombatHash  = Animator.StringToHash(isInCombatParameter);
            _bowEquippedHash = Animator.StringToHash(bowEquippedParameter);
            _bowAimYHash = Animator.StringToHash(bowAimYParameter);
            _bowNotchHash = Animator.StringToHash(bowNotchParameter);
            _bowShootHash = Animator.StringToHash(bowShootParameter);
            _bowRapidFireHash = Animator.StringToHash(bowRapidFireParameter);

            _hasSpeed       = HasParam(speedParameter,      AnimatorControllerParameterType.Float);
            _hasVelocityX   = HasParam(velocityXParameter,  AnimatorControllerParameterType.Float);
            _hasVelocityZ   = HasParam(velocityZParameter,  AnimatorControllerParameterType.Float);
            _hasIsSprinting = HasParam(isSprintingParameter, AnimatorControllerParameterType.Bool);
            _hasIsCrouching = HasParam(isCrouchingParameter, AnimatorControllerParameterType.Bool);
            _hasIsCrawling  = HasParam(isCrawlingParameter,  AnimatorControllerParameterType.Bool);
            _hasIsSitting   = HasParam(isSittingParameter,   AnimatorControllerParameterType.Bool);
            _hasIsInCombat  = HasParam(isInCombatParameter,  AnimatorControllerParameterType.Bool);
            _hasBowEquipped = HasParam(bowEquippedParameter, AnimatorControllerParameterType.Bool);
            _hasBowAimY = HasParam(bowAimYParameter, AnimatorControllerParameterType.Float);
            _hasBowNotch = HasParam(bowNotchParameter, AnimatorControllerParameterType.Trigger);
            _hasBowShoot = HasParam(bowShootParameter, AnimatorControllerParameterType.Trigger);
            _hasBowRapidFire = HasParam(bowRapidFireParameter, AnimatorControllerParameterType.Bool);

            _paramsCached = true;

            // Sync dead state in case this component enabled after death.
            SyncDeadBool();
            SyncCombatAnimatorState();
            DriveBowParameters();

            BuildFolderVariantCategories();
            CacheWeightedAttackOptions();
        }

        private bool HasParam(string paramName, AnimatorControllerParameterType type)
        {
            if (_animator == null || string.IsNullOrEmpty(paramName)) return false;
            foreach (AnimatorControllerParameter p in _animator.parameters)
                if (p.type == type && p.name == paramName) return true;
            return false;
        }

        // ── Event subscriptions ───────────────────────────────────────────────

        private void SubscribeEvents()
        {
            if (!_healthSubscribed && unitHealth != null)
            {
                unitHealth.Damaged += OnDamaged;
                unitHealth.Died    += OnDied;
                _healthSubscribed = true;
            }

            if (!_eventSubscribed && EventSystem.Instance != null)
            {
                EventSystem.Instance.Subscribe<CombatAttackWindupEvent>(OnCombatAttackWindup);
                EventSystem.Instance.Subscribe<CombatTickResolvedEvent>(OnCombatTickResolved);
                _eventSubscribed = true;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_healthSubscribed && unitHealth != null)
            {
                unitHealth.Damaged -= OnDamaged;
                unitHealth.Died    -= OnDied;
                _healthSubscribed = false;
            }

            if (_eventSubscribed)
            {
                EventSystem.Instance?.Unsubscribe<CombatAttackWindupEvent>(OnCombatAttackWindup);
                EventSystem.Instance?.Unsubscribe<CombatTickResolvedEvent>(OnCombatTickResolved);
                _eventSubscribed = false;
            }
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnCombatAttackWindup(CombatAttackWindupEvent e)
        {
            if (unit == null) return;

            if (e.Attacker == unit || e.Defender == unit)
            {
                RefreshCombatStateHold();
            }

            if (e.Attacker == unit && IsWithinAttackAnimationDistance(e.Defender))
            {
                CachedWeightedAttackOption selectedAttack = TriggerAttack();
                CombatReactionArea timedReactionArea = ResolveReactionAreaForHitTiming(selectedAttack, e.WindupSeconds);
                CombatAttackPresentationRegistry.RegisterSelection(
                    e.EncounterId,
                    e.Attacker,
                    e.Defender,
                    selectedAttack.AttackStyle,
                    timedReactionArea,
                    e.WindupSeconds + 0.5f);
            }
        }

        private void OnCombatTickResolved(CombatTickResolvedEvent e)
        {
            if (unit == null) return;

            if (e.Attacker == unit || e.Defender == unit)
            {
                RefreshCombatStateHold();
            }

            if (triggerDodgeFromCombatTicks
                && e.Defender == unit
                && e.DidDefenderDodge
                && IsWithinAttackAnimationDistance(e.Attacker))
            {
                TriggerDodge();
                return;
            }

            // Fallback only: when health events are unavailable, keep hit feedback.
            if (unitHealth == null && e.Defender == unit && e.DidHit)
                TriggerHit();
        }

        public void TryPlayRandomFacingTurn(Unit targetUnit)
        {
            if (!playRandomTurnOnFacing || targetUnit == null)
            {
                return;
            }

            if (Time.time < _nextTurnAnimationTime)
            {
                return;
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator != null && !_paramsCached)
                {
                    CacheParameters();
                }
            }

            if (_animator == null)
            {
                return;
            }

            if (IsAttackStateActive())
            {
                return;
            }

            Vector3 toTarget = targetUnit.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            float turnAngle = Vector3.Angle(forward, toTarget.normalized);
            if (turnAngle < Mathf.Max(0f, minimumFacingTurnAngleDegrees))
            {
                return;
            }

            float maxTurnAngle = Mathf.Clamp(maximumFacingTurnAnimationAngleDegrees, 0f, 180f);
            if (maxTurnAngle > 0f && turnAngle > maxTurnAngle)
            {
                return;
            }

            string preferredState = ResolvePreferredTurnState(forward, toTarget.normalized);
            string alternateState = preferredState == turnLeftStateName ? turnRightStateName : turnLeftStateName;
            bool chooseAlternate = Random.value < Mathf.Clamp01(oppositeTurnDirectionChance);
            string primaryState = chooseAlternate ? alternateState : preferredState;
            string secondaryState = chooseAlternate ? preferredState : alternateState;

            if (TryCrossFadeTurnState(primaryState) || TryCrossFadeTurnState(secondaryState))
            {
                _nextTurnAnimationTime = Time.time + Mathf.Max(0f, turnAnimationCooldownSeconds);
            }
        }

        public void CancelAttackForMovement(Vector3 destinationWorldPosition)
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator != null && !_paramsCached)
                {
                    CacheParameters();
                }
            }

            if (_animator == null)
            {
                return;
            }

            bool wasAttacking = IsAttackStateActive();
            ResetAllCombatTriggers();

            if (!wasAttacking)
            {
                return;
            }

            Vector3 desiredDirection = destinationWorldPosition - transform.position;
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                TryExitAttackToLocomotion();
                return;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            Vector3 desiredDirectionNormalized = desiredDirection.normalized;
            float turnAngle = Vector3.Angle(forward, desiredDirectionNormalized);
            bool shouldPlayTurn = turnAngle >= Mathf.Clamp(turnAroundAngleDegrees, 0f, 180f);
            float maxTurnAngle = Mathf.Clamp(maximumFacingTurnAnimationAngleDegrees, 0f, 180f);
            bool withinTurnAnimationCap = maxTurnAngle <= 0f || turnAngle <= maxTurnAngle;

            if (shouldPlayTurn && withinTurnAnimationCap && TryPlayTurnTowardDirection(forward, desiredDirectionNormalized))
            {
                return;
            }

            TryExitAttackToLocomotion();
        }

        public void CancelCombatFacingForMovement()
        {
            _dodgeFaceTarget = null;
            _dodgeFaceLockExpiresAt = 0f;
        }

        public void TryTriggerCombatEntry()
        {
            if (Time.time < _nextCombatEntryTriggerTime)
            {
                return;
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator != null && !_paramsCached)
                {
                    CacheParameters();
                }
            }

            if (_animator == null)
            {
                return;
            }

            if (unitHealth != null && unitHealth.IsDead)
            {
                return;
            }

            bool triggered = false;
            if (_hasCombatEntry)
            {
                ResetCombatTriggersExcept(_combatEntryHash);
                _animator.SetTrigger(_combatEntryHash);
                triggered = true;
            }

            TryPlayFallbackState(combatEntryStateName, triggered);
            _nextCombatEntryTriggerTime = Time.time + Mathf.Max(0f, combatEntryTriggerCooldownSeconds);
        }

        private string ResolvePreferredTurnState(Vector3 forward, Vector3 toTargetNormalized)
        {
            if (!biasTurnDirectionTowardTargetSide)
            {
                return Random.value < 0.5f ? turnLeftStateName : turnRightStateName;
            }

            float side = Vector3.Cross(forward, toTargetNormalized).y;
            if (Mathf.Abs(side) <= 0.0001f)
            {
                return Random.value < 0.5f ? turnLeftStateName : turnRightStateName;
            }

            // Positive means target is to the right of current forward.
            return side > 0f ? turnRightStateName : turnLeftStateName;
        }

        private bool TryPlayTurnTowardDirection(Vector3 forward, Vector3 directionNormalized)
        {
            string preferredState = ResolvePreferredTurnState(forward, directionNormalized);
            string alternateState = preferredState == turnLeftStateName ? turnRightStateName : turnLeftStateName;

            if (TryCrossFadeTurnState(preferredState) || TryCrossFadeTurnState(alternateState))
            {
                _nextTurnAnimationTime = Time.time + Mathf.Max(0f, turnAnimationCooldownSeconds);
                return true;
            }

            return false;
        }

        private void TryExitAttackToLocomotion()
        {
            TryApplyRandomVariant(baseLocomotionClip, _locomotionVariants);
            if (TryCrossFadeState(locomotionStateName))
            {
                return;
            }

            TryApplyRandomVariant(baseIdleClip, _idleVariants);
            _ = TryCrossFadeState(idleStateName);
        }

        private bool TryCrossFadeTurnState(string stateName)
        {
            if (string.Equals(stateName, turnLeftStateName, System.StringComparison.Ordinal))
            {
                TryApplyRandomVariant(baseTurnLeftClip, _turnLeftVariants);
            }
            else if (string.Equals(stateName, turnRightStateName, System.StringComparison.Ordinal))
            {
                TryApplyRandomVariant(baseTurnRightClip, _turnRightVariants);
            }

            return TryCrossFadeState(stateName);
        }

        private bool IsAttackStateActive()
        {
            if (_animator == null || string.IsNullOrWhiteSpace(attackStateName))
            {
                return false;
            }

            AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);
            if (StateMatchesName(currentState, attackStateName))
            {
                return true;
            }

            if (_animator.IsInTransition(0))
            {
                AnimatorStateInfo nextState = _animator.GetNextAnimatorStateInfo(0);
                if (StateMatchesName(nextState, attackStateName))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldSuppressDefensiveReaction()
        {
            if (!suppressDefensiveReactionsWhileAttacking)
            {
                return false;
            }

            if (Time.time <= _defensiveReactionSuppressUntilTime)
            {
                return true;
            }

            return IsAttackStateActive();
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

        private void OnDamaged(float amount)
        {
            if (amount > 0f) TriggerHit();
        }

        private void OnDied()
        {
            _combatStateHoldUntilTime = 0f;
            SyncDeadBool();
            SetCombatAnimatorFlag(false);
            ResetCombatEyeFocus(immediate: true);
            TryApplyRandomVariant(baseDeadClip, _deadVariants);

            bool triggered = false;
            if (_animator != null && _hasDie)
            {
                _animator.SetTrigger(_dieHash);
                triggered = true;
            }

            TryPlayFallbackState(deadStateName, triggered);
        }

        // ── Trigger helpers ───────────────────────────────────────────────────

        private CachedWeightedAttackOption TriggerAttack()
        {
            RefreshCombatStateHold();
            CachedWeightedAttackOption selectedAttack = ResolveSelectedAttackOption();

            if (_animator == null)
            {
                return selectedAttack;
            }

            if (!TryApplyFilteredAttackVariant(selectedAttack.ClipNameKeywords))
            {
                TryApplyRandomVariant(baseAttackClip, _attackVariants);
            }

            bool triggered = false;
            int triggerHash = _attackHash;

            if (selectedAttack.HasTrigger)
            {
                triggerHash = selectedAttack.TriggerHash;
                ResetCombatTriggersExcept(triggerHash);
                _animator.SetTrigger(triggerHash);
                triggered = true;
            }
            else if (_hasAttack)
            {
                ResetCombatTriggersExcept(_attackHash);
                _animator.SetTrigger(_attackHash);
                triggered = true;
            }

            string fallbackStateName = !string.IsNullOrWhiteSpace(selectedAttack.FallbackStateName)
                ? selectedAttack.FallbackStateName
                : attackStateName;

            TryPlayFallbackState(fallbackStateName, triggered);

            if (!string.Equals(fallbackStateName, attackStateName, System.StringComparison.Ordinal))
            {
                TryPlayFallbackState(attackStateName, triggered);
            }

            if (suppressDefensiveReactionsWhileAttacking)
            {
                _defensiveReactionSuppressUntilTime = Mathf.Max(
                    _defensiveReactionSuppressUntilTime,
                    Time.time + Mathf.Max(0f, defensiveReactionSuppressAfterAttackTriggerSeconds));
            }

            return selectedAttack;
        }

        private void TriggerDodge()
        {
            if (ShouldSuppressDefensiveReaction())
            {
                return;
            }

            if (_animator == null)
            {
                return;
            }

            AnimationClip dodgeBaseClip = baseDodgeClip != null ? baseDodgeClip : baseLocomotionClip;
            List<AnimationClip> dodgeVariants = _dodgeVariants.Count > 0 ? _dodgeVariants : _locomotionVariants;
            TryApplyRandomVariant(dodgeBaseClip, dodgeVariants);

            // Randomly pick left or right dodge, falling back to whichever exists.
            bool useLeft  = _hasDodgeLeft  && HasAnimatorState(dodgeLeftStateName);
            bool useRight = _hasDodgeRight && HasAnimatorState(dodgeRightStateName);

            if (useLeft || useRight)
            {
                bool pickLeft = useLeft && (!useRight || UnityEngine.Random.value < 0.5f);
                int  hash      = pickLeft ? _dodgeHash : _dodgeRightHash;
                string state   = pickLeft ? dodgeLeftStateName : dodgeRightStateName;
                Unit dodgeOpponent = ResolveEncounterOpponent();
                Vector3 dodgeDir = ResolveBackstepDirection(dodgeOpponent);
                float dodgeDistance = Mathf.Clamp(dodgeStepDistance, 0f, 0.5f);
                float dodgeDuration = Mathf.Max(0.05f, dodgeStepDurationSeconds);
                StartDodgeFaceLock(dodgeOpponent, dodgeDuration);
                _unitController?.BeginDodgeStep(dodgeDir, dodgeDistance, dodgeDuration);
                ResetCombatTriggersExcept(hash);
                _animator.SetTrigger(hash);
                TryPlayFallbackState(state, triggerWasSet: true);
                return;
            }

            // Fallback to hit so dodge still has visual feedback on legacy controllers.
            if (_hasHit)
            {
                _animator.SetTrigger(_hitHash);
                TryPlayFallbackState(hitStateName, triggerWasSet: true);
                return;
            }

            TryPlayFallbackState(dodgeLeftStateName, triggerWasSet: false);
        }

        private Unit ResolveEncounterOpponent()
        {
            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            if (unit == null)
            {
                return null;
            }

            if (_encounterManager == null)
            {
                _encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : Object.FindFirstObjectByType<CombatEncounterManager>();
            }

            if (_encounterManager == null || !_encounterManager.IsUnitInEncounter(unit))
            {
                return null;
            }

            if (!_encounterManager.TryGetEncounterOpponent(unit, out Unit encounterOpponent))
            {
                return null;
            }

            if (encounterOpponent == null || !encounterOpponent.IsAlive)
            {
                return null;
            }

            return encounterOpponent;
        }

        private Vector3 ResolveBackstepDirection(Unit dodgeOpponent)
        {
            if (dodgeOpponent != null)
            {
                Vector3 awayFromOpponent = transform.position - dodgeOpponent.transform.position;
                awayFromOpponent.y = 0f;

                if (awayFromOpponent.sqrMagnitude > 0.0001f)
                {
                    return awayFromOpponent.normalized;
                }
            }

            Vector3 fallback = -transform.forward;
            fallback.y = 0f;

            if (fallback.sqrMagnitude <= 0.0001f)
            {
                return Vector3.back;
            }

            return fallback.normalized;
        }

        private void StartDodgeFaceLock(Unit dodgeOpponent, float dodgeDuration)
        {
            if (_unitController == null)
            {
                _unitController = GetComponent<UnitController>();
            }

            if (_unitController == null || dodgeOpponent == null)
            {
                _dodgeFaceTarget = null;
                _dodgeFaceLockExpiresAt = 0f;
                return;
            }

            _dodgeFaceTarget = dodgeOpponent;
            _dodgeFaceLockExpiresAt = Time.time + Mathf.Max(0f, dodgeDuration + dodgeFaceLockExtraSeconds);
            _unitController.FacePositionInstant(dodgeOpponent.transform.position);
        }

        private void TickDodgeFaceLock()
        {
            if (_dodgeFaceTarget == null)
            {
                return;
            }

            if (_unitController == null)
            {
                _unitController = GetComponent<UnitController>();
            }

            if (_unitController == null
                || !_dodgeFaceTarget.IsAlive
                || Time.time > _dodgeFaceLockExpiresAt)
            {
                _dodgeFaceTarget = null;
                _dodgeFaceLockExpiresAt = 0f;
                return;
            }

            float turnSpeed = Mathf.Max(0f, dodgeFaceLockTurnSpeedDegreesPerSecond);
            if (turnSpeed <= 0f)
            {
                _unitController.FacePositionInstant(_dodgeFaceTarget.transform.position);
                return;
            }

            _unitController.RotateTowardsPosition(_dodgeFaceTarget.transform.position, turnSpeed);
        }

        private void TriggerHit()
        {
            if (ShouldSuppressDefensiveReaction())
            {
                return;
            }

            // Guard against multiple hits resolving in the same frame.
            if (Time.frameCount == _lastHitFrame) return;
            _lastHitFrame = Time.frameCount;

            if (_animator == null)
            {
                return;
            }

            TryApplyRandomVariant(baseHitClip, _hitVariants);

            bool triggered = false;
            if (_hasHit)
            {
                ResetCombatTriggersExcept(_hitHash);
                _animator.SetTrigger(_hitHash);
                triggered = true;
            }

            TryPlayFallbackState(hitStateName, triggered);
        }

        private CachedWeightedAttackOption ResolveSelectedAttackOption()
        {
            if (TrySelectWeightedAttackOption(out CachedWeightedAttackOption selectedAttack))
            {
                return selectedAttack;
            }

            return new CachedWeightedAttackOption(
                attackStyle: CombatAttackStyle.Unknown,
                preferredReactionArea: CombatReactionArea.Chest,
                weight: 1f,
                triggerHash: _attackHash,
                hasTrigger: _hasAttack,
                fallbackStateName: attackStateName,
                clipNameKeywords: null);
        }

        private bool TrySelectWeightedAttackOption(out CachedWeightedAttackOption selectedAttack)
        {
            selectedAttack = default;

            if (_cachedWeightedAttackOptions.Count == 0)
            {
                return false;
            }

            float totalWeight = 0f;
            for (int i = 0; i < _cachedWeightedAttackOptions.Count; i++)
            {
                totalWeight += Mathf.Max(0f, _cachedWeightedAttackOptions[i].Weight);
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            float roll = Random.value * totalWeight;
            float runningWeight = 0f;

            for (int i = 0; i < _cachedWeightedAttackOptions.Count; i++)
            {
                CachedWeightedAttackOption option = _cachedWeightedAttackOptions[i];
                runningWeight += Mathf.Max(0f, option.Weight);
                if (roll <= runningWeight)
                {
                    selectedAttack = option;
                    return true;
                }
            }

            selectedAttack = _cachedWeightedAttackOptions[_cachedWeightedAttackOptions.Count - 1];
            return true;
        }

        private CombatReactionArea ResolveReactionAreaForHitTiming(CachedWeightedAttackOption selectedAttack, float projectedHitTimeSeconds)
        {
            CombatReactionArea resolvedReactionArea = selectedAttack.PreferredReactionArea == CombatReactionArea.Default
                ? CombatReactionArea.Chest
                : selectedAttack.PreferredReactionArea;

            if (attackReactionTimelines == null || attackReactionTimelines.Length == 0)
            {
                return resolvedReactionArea;
            }

            AttackReactionTimeline matchedTimeline = null;
            for (int i = 0; i < attackReactionTimelines.Length; i++)
            {
                AttackReactionTimeline timeline = attackReactionTimelines[i];
                if (timeline == null || timeline.attackStyle != selectedAttack.AttackStyle)
                {
                    continue;
                }

                matchedTimeline = timeline;
                break;
            }

            if (matchedTimeline == null)
            {
                return resolvedReactionArea;
            }

            if (matchedTimeline.fallbackReactionArea != CombatReactionArea.Default)
            {
                resolvedReactionArea = matchedTimeline.fallbackReactionArea;
            }

            TimedReactionArea[] timedReactions = matchedTimeline.timedReactions;
            if (timedReactions == null || timedReactions.Length == 0)
            {
                return resolvedReactionArea;
            }

            float sampleTime = Mathf.Max(0f, projectedHitTimeSeconds);
            float bestReactionTime = float.NegativeInfinity;

            for (int i = 0; i < timedReactions.Length; i++)
            {
                TimedReactionArea reactionCue = timedReactions[i];
                if (reactionCue == null)
                {
                    continue;
                }

                float cueTime = Mathf.Max(0f, reactionCue.hitTimeSeconds);
                if (cueTime > sampleTime + 0.0001f || cueTime < bestReactionTime)
                {
                    continue;
                }

                bestReactionTime = cueTime;
                if (reactionCue.reactionArea != CombatReactionArea.Default)
                {
                    resolvedReactionArea = reactionCue.reactionArea;
                }
            }

            return resolvedReactionArea;
        }

        private void CacheWeightedAttackOptions()
        {
            _cachedWeightedAttackOptions.Clear();
            _weightedAttackTriggerHashes.Clear();

            if (weightedAttackOptions == null || weightedAttackOptions.Length == 0)
            {
                return;
            }

            for (int i = 0; i < weightedAttackOptions.Length; i++)
            {
                WeightedAttackOption option = weightedAttackOptions[i];
                if (option == null)
                {
                    continue;
                }

                float weight = Mathf.Max(0f, option.weight);
                if (weight <= 0f)
                {
                    continue;
                }

                string triggerParameter = string.IsNullOrWhiteSpace(option.triggerParameter)
                    ? attackTriggerParameter
                    : option.triggerParameter;

                int triggerHash = Animator.StringToHash(triggerParameter);
                bool hasTrigger = HasParam(triggerParameter, AnimatorControllerParameterType.Trigger);

                _cachedWeightedAttackOptions.Add(new CachedWeightedAttackOption(
                    attackStyle: option.attackStyle,
                    preferredReactionArea: option.preferredReactionArea == CombatReactionArea.Default
                        ? CombatReactionArea.Chest
                        : option.preferredReactionArea,
                    weight: weight,
                    triggerHash: triggerHash,
                    hasTrigger: hasTrigger,
                    fallbackStateName: option.fallbackStateName,
                    clipNameKeywords: option.clipNameKeywords));

                if (hasTrigger && triggerHash != _attackHash && !_weightedAttackTriggerHashes.Contains(triggerHash))
                {
                    _weightedAttackTriggerHashes.Add(triggerHash);
                }
            }
        }

        private bool TryApplyFilteredAttackVariant(string[] clipKeywords)
        {
            if (!enableFolderClipVariants
                || baseAttackClip == null
                || _attackVariants.Count == 0
                || clipKeywords == null
                || clipKeywords.Length == 0)
            {
                return false;
            }

            _attackVariantFilterBuffer.Clear();

            for (int i = 0; i < _attackVariants.Count; i++)
            {
                AnimationClip candidate = _attackVariants[i];
                if (candidate == null)
                {
                    continue;
                }

                string candidateName = candidate.name.ToLowerInvariant();
                if (ContainsAny(candidateName, clipKeywords))
                {
                    _attackVariantFilterBuffer.Add(candidate);
                }
            }

            if (_attackVariantFilterBuffer.Count == 0)
            {
                return false;
            }

            AnimationClip selectedVariant = _attackVariantFilterBuffer[Random.Range(0, _attackVariantFilterBuffer.Count)];
            TryApplySpecificVariant(baseAttackClip, selectedVariant);
            return true;
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

        private void ResetCombatTriggersExcept(int keepHash)
        {
            if (_animator == null)
            {
                return;
            }

            if (_hasAttack && _attackHash != keepHash)
            {
                _animator.ResetTrigger(_attackHash);
            }

            for (int i = 0; i < _weightedAttackTriggerHashes.Count; i++)
            {
                int weightedHash = _weightedAttackTriggerHashes[i];
                if (weightedHash == keepHash)
                {
                    continue;
                }

                _animator.ResetTrigger(weightedHash);
            }

            if (_hasDodgeLeft && _dodgeHash != keepHash)
            {
                _animator.ResetTrigger(_dodgeHash);
            }

            if (_hasDodgeRight && _dodgeRightHash != keepHash)
            {
                _animator.ResetTrigger(_dodgeRightHash);
            }

            if (_hasHit && _hitHash != keepHash)
            {
                _animator.ResetTrigger(_hitHash);
            }

            if (_hasCombatEntry && _combatEntryHash != keepHash)
            {
                _animator.ResetTrigger(_combatEntryHash);
            }
        }

        private bool HasAnimatorState(string stateName)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName)) return false;
            int hash = Animator.StringToHash(stateName);
            // Check all layers for a state with this name hash.
            for (int layer = 0; layer < _animator.layerCount; layer++)
            {
                if (_animator.HasState(layer, hash)) return true;
            }
            return false;
        }

        private void ResetAllCombatTriggers()
        {
            if (_animator == null)
            {
                return;
            }

            if (_hasAttack)
            {
                _animator.ResetTrigger(_attackHash);
            }

            for (int i = 0; i < _weightedAttackTriggerHashes.Count; i++)
            {
                _animator.ResetTrigger(_weightedAttackTriggerHashes[i]);
            }

            if (_hasDodgeLeft)
            {
                _animator.ResetTrigger(_dodgeHash);
            }

            if (_hasDodgeRight)
            {
                _animator.ResetTrigger(_dodgeRightHash);
            }

            if (_hasHit)
            {
                _animator.ResetTrigger(_hitHash);
            }

            if (_hasCombatEntry)
            {
                _animator.ResetTrigger(_combatEntryHash);
            }
        }

        private void TryPlayFallbackState(string stateName, bool triggerWasSet)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            if (!forceStateFallbackWhenTriggerSet && triggerWasSet)
            {
                return;
            }

            _ = TryCrossFadeState(stateName);
        }

        private void BuildFolderVariantCategories()
        {
            if (_folderVariantCategoriesBuilt)
            {
                return;
            }

            _folderVariantCategoriesBuilt = true;

            _idleVariants.Clear();
            _locomotionVariants.Clear();
            _attackVariants.Clear();
            _dodgeVariants.Clear();
            _hitVariants.Clear();
            _deadVariants.Clear();
            _turnLeftVariants.Clear();
            _turnRightVariants.Clear();

            if (playerFolderClips == null || playerFolderClips.Length == 0)
            {
                return;
            }

            for (int i = 0; i < playerFolderClips.Length; i++)
            {
                AnimationClip clip = playerFolderClips[i];
                if (clip == null)
                {
                    continue;
                }

                string name = clip.name.ToLowerInvariant();

                if (ContainsAny(name, "idle"))
                {
                    AddUnique(_idleVariants, clip);
                }

                if (ContainsAny(name, "walk", "run", "strafe"))
                {
                    AddUnique(_locomotionVariants, clip);
                }

                if (ContainsAny(name, "attack", "punch", "kick"))
                {
                    AddUnique(_attackVariants, clip);
                }

                if (ContainsAny(name, "dodge", "strafe", "jump"))
                {
                    AddUnique(_dodgeVariants, clip);
                }

                if (ContainsAny(name, "reaction", "hit", "react"))
                {
                    AddUnique(_hitVariants, clip);
                }

                if (ContainsAny(name, "death", "dying", "die"))
                {
                    AddUnique(_deadVariants, clip);
                }

                bool hasTurn = ContainsAny(name, "turn");
                if (hasTurn && ContainsAny(name, "left"))
                {
                    AddUnique(_turnLeftVariants, clip);
                }

                if (hasTurn && ContainsAny(name, "right"))
                {
                    AddUnique(_turnRightVariants, clip);
                }
            }

            if (_dodgeVariants.Count == 0)
            {
                for (int i = 0; i < _locomotionVariants.Count; i++)
                {
                    AddUnique(_dodgeVariants, _locomotionVariants[i]);
                }
            }

            if (_turnLeftVariants.Count == 0)
            {
                for (int i = 0; i < _locomotionVariants.Count; i++)
                {
                    AnimationClip clip = _locomotionVariants[i];
                    if (clip != null && clip.name.ToLowerInvariant().Contains("left"))
                    {
                        AddUnique(_turnLeftVariants, clip);
                    }
                }
            }

            if (_turnRightVariants.Count == 0)
            {
                for (int i = 0; i < _locomotionVariants.Count; i++)
                {
                    AnimationClip clip = _locomotionVariants[i];
                    if (clip != null && clip.name.ToLowerInvariant().Contains("right"))
                    {
                        AddUnique(_turnRightVariants, clip);
                    }
                }
            }
        }

        private void EnsureRuntimeOverrideController()
        {
            if (!enableFolderClipVariants || _animator == null)
            {
                return;
            }

            RuntimeAnimatorController currentController = _animator.runtimeAnimatorController;
            if (currentController == null)
            {
                return;
            }

            RuntimeAnimatorController baseController = currentController;
            if (currentController is AnimatorOverrideController existingOverride && existingOverride.runtimeAnimatorController != null)
            {
                baseController = existingOverride.runtimeAnimatorController;
            }

            bool requiresOverride = _runtimeOverrideController == null
                || _runtimeOverrideController.runtimeAnimatorController != baseController
                || _animator.runtimeAnimatorController != _runtimeOverrideController;

            if (!requiresOverride)
            {
                return;
            }

            _runtimeOverrideController = new AnimatorOverrideController(baseController);
            _animator.runtimeAnimatorController = _runtimeOverrideController;
            _runtimeOverrides.Clear();
            _runtimeOverrideController.GetOverrides(_runtimeOverrides);
        }

        private void ApplyIdleAndLocomotionVariants()
        {
            TryApplyRandomVariant(baseIdleClip, _idleVariants);
            TryApplyRandomVariant(baseLocomotionClip, _locomotionVariants);
        }

        private void TryApplySpecificVariant(AnimationClip baseClip, AnimationClip candidate)
        {
            if (!enableFolderClipVariants || baseClip == null || candidate == null)
            {
                return;
            }

            EnsureRuntimeOverrideController();
            if (_runtimeOverrideController == null)
            {
                return;
            }

            bool found = false;
            bool changed = false;

            for (int i = 0; i < _runtimeOverrides.Count; i++)
            {
                KeyValuePair<AnimationClip, AnimationClip> overridePair = _runtimeOverrides[i];
                if (overridePair.Key != baseClip)
                {
                    continue;
                }

                found = true;
                if (overridePair.Value == candidate)
                {
                    continue;
                }

                _runtimeOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overridePair.Key, candidate);
                changed = true;
            }

            if (changed)
            {
                _runtimeOverrideController.ApplyOverrides(_runtimeOverrides);
            }
            else if (!found)
            {
                _runtimeOverrideController[baseClip.name] = candidate;
            }
        }

        private void TryApplyRandomVariant(AnimationClip baseClip, List<AnimationClip> variants)
        {
            if (!enableFolderClipVariants || baseClip == null || variants == null || variants.Count == 0)
            {
                return;
            }

            EnsureRuntimeOverrideController();
            if (_runtimeOverrideController == null)
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

            for (int i = 0; i < _runtimeOverrides.Count; i++)
            {
                KeyValuePair<AnimationClip, AnimationClip> overridePair = _runtimeOverrides[i];
                if (overridePair.Key != baseClip)
                {
                    continue;
                }

                found = true;
                if (overridePair.Value == candidate)
                {
                    continue;
                }

                _runtimeOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overridePair.Key, candidate);
                changed = true;
            }

            if (changed)
            {
                _runtimeOverrideController.ApplyOverrides(_runtimeOverrides);
            }
            else if (!found)
            {
                _runtimeOverrideController[baseClip.name] = candidate;
            }
        }

        private static void AddUnique(List<AnimationClip> destination, AnimationClip clip)
        {
            if (destination == null || clip == null || destination.Contains(clip))
            {
                return;
            }

            destination.Add(clip);
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

        private void EnsureRootMotionDisabled()
        {
            if (_animator == null)
            {
                return;
            }

            if (_animator.applyRootMotion)
            {
                _animator.applyRootMotion = false;
            }
        }

        private bool TryCrossFadeState(string stateName)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            float fadeSeconds = Mathf.Max(0f, fallbackCrossFadeSeconds);
            const int baseLayer = 0;

            int stateHash = Animator.StringToHash(stateName);
            if (_animator.HasState(baseLayer, stateHash))
            {
                _animator.CrossFadeInFixedTime(stateHash, fadeSeconds, baseLayer);
                return true;
            }

            string qualifiedStateName = "Base Layer." + stateName;
            int qualifiedHash = Animator.StringToHash(qualifiedStateName);
            if (_animator.HasState(baseLayer, qualifiedHash))
            {
                _animator.CrossFadeInFixedTime(qualifiedHash, fadeSeconds, baseLayer);
                return true;
            }

            return false;
        }

        private void SyncDeadBool()
        {
            if (_animator == null || !_hasIsDead) return;
            _animator.SetBool(_isDeadHash, unitHealth != null && unitHealth.IsDead);
        }

        private void SyncCombatAnimatorState()
        {
            if (_animator == null || !_hasIsInCombat)
            {
                return;
            }

            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            if (unitHealth != null && unitHealth.IsDead)
            {
                SetCombatAnimatorFlag(false);
                return;
            }

            if (_encounterManager == null)
            {
                _encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : Object.FindFirstObjectByType<CombatEncounterManager>();
            }

            bool inCombatEncounter = unit != null
                && _encounterManager != null
                && _encounterManager.IsUnitInEncounter(unit);

            bool inCombatHoldWindow = Time.time <= _combatStateHoldUntilTime;
            SetCombatAnimatorFlag(inCombatEncounter || inCombatHoldWindow);
        }

        private void RefreshCombatStateHold()
        {
            _combatStateHoldUntilTime = Mathf.Max(
                _combatStateHoldUntilTime,
                Time.time + Mathf.Max(0f, combatStateHoldSeconds));
        }

        private void OnAnimatorIK(int layerIndex)
        {
            _isApplyingAnimatorIk = true;
            try
            {
                ApplyCombatEyeFocus();
            }
            finally
            {
                _isApplyingAnimatorIk = false;
            }
        }

        private void ApplyCombatEyeFocus()
        {
            if (_animator == null || !enableCombatEyeFocus)
            {
                return;
            }

            if (unitHealth != null && unitHealth.IsDead)
            {
                ResetCombatEyeFocus(immediate: false);
                return;
            }

            Unit focusTarget = ResolveCombatLookTarget();
            float desiredWeight = focusTarget != null ? 1f : 0f;
            float weightStep = Mathf.Max(0.01f, combatLookAtWeightLerpSpeed) * Time.deltaTime;
            _smoothedCombatLookAtWeight = Mathf.MoveTowards(_smoothedCombatLookAtWeight, desiredWeight, weightStep);

            if (focusTarget != null)
            {
                Vector3 desiredLookPosition = focusTarget.transform.position + Vector3.up * Mathf.Max(0f, combatLookAtHeightOffset);

                if (!_hasSmoothedCombatLookAtPosition)
                {
                    _smoothedCombatLookAtPosition = desiredLookPosition;
                    _hasSmoothedCombatLookAtPosition = true;
                }
                else
                {
                    float lerpT = 1f - Mathf.Exp(-Mathf.Max(0.01f, combatLookAtPositionLerpSpeed) * Time.deltaTime);
                    _smoothedCombatLookAtPosition = Vector3.Lerp(_smoothedCombatLookAtPosition, desiredLookPosition, lerpT);
                }
            }

            if (_smoothedCombatLookAtWeight <= 0.0001f || !_hasSmoothedCombatLookAtPosition)
            {
                _animator.SetLookAtWeight(0f);
                return;
            }

            float overallWeight = Mathf.Clamp01(combatLookAtOverallWeight) * _smoothedCombatLookAtWeight;
            _animator.SetLookAtWeight(
                overallWeight,
                Mathf.Clamp01(combatLookAtBodyWeight),
                Mathf.Clamp01(combatLookAtHeadWeight),
                Mathf.Clamp01(combatLookAtEyesWeight),
                Mathf.Clamp01(combatLookAtClampWeight));
            _animator.SetLookAtPosition(_smoothedCombatLookAtPosition);
        }

        private Unit ResolveCombatLookTarget()
        {
            if (!enableCombatEyeFocus)
            {
                _currentCombatLookTarget = null;
                return null;
            }

            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            if (unit == null)
            {
                _currentCombatLookTarget = null;
                return null;
            }

            if (_encounterManager == null)
            {
                _encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : Object.FindFirstObjectByType<CombatEncounterManager>();
            }

            if (_encounterManager == null || !_encounterManager.IsUnitInEncounter(unit))
            {
                _currentCombatLookTarget = null;
                return null;
            }

            if (!_encounterManager.TryGetEncounterOpponent(unit, out Unit encounterOpponent))
            {
                _currentCombatLookTarget = null;
                return null;
            }

            if (_currentCombatLookTarget == null || !_currentCombatLookTarget.IsAlive)
            {
                _currentCombatLookTarget = encounterOpponent;
                _nextCombatLookTargetSwitchAt = Time.time + Mathf.Max(0f, combatLookTargetSwitchDelaySeconds);
                return _currentCombatLookTarget;
            }

            if (_currentCombatLookTarget == encounterOpponent)
            {
                _nextCombatLookTargetSwitchAt = Time.time + Mathf.Max(0f, combatLookTargetSwitchDelaySeconds);
                return _currentCombatLookTarget;
            }

            if (Time.time < _nextCombatLookTargetSwitchAt)
            {
                return _currentCombatLookTarget;
            }

            _currentCombatLookTarget = encounterOpponent;
            _nextCombatLookTargetSwitchAt = Time.time + Mathf.Max(0f, combatLookTargetSwitchDelaySeconds);
            return _currentCombatLookTarget;
        }

        private void ResetCombatEyeFocus(bool immediate)
        {
            _currentCombatLookTarget = null;
            _nextCombatLookTargetSwitchAt = 0f;

            if (immediate)
            {
                _smoothedCombatLookAtWeight = 0f;
                _hasSmoothedCombatLookAtPosition = false;
            }

            if (_animator != null && _isApplyingAnimatorIk)
            {
                _animator.SetLookAtWeight(0f);
            }
        }

        public void SetBowAimTarget(Unit targetUnit)
        {
            _bowAimTarget = targetUnit != null && targetUnit.IsAlive ? targetUnit : null;

            if (_bowAimTarget != null)
            {
                RefreshCombatStateHold();
            }
        }

        public void ClearBowAimTarget()
        {
            _bowAimTarget = null;
            ResetBowVisualPlayback(clearRigState: false);

            if (_animator != null && _paramsCached && _hasBowRapidFire)
            {
                _animator.SetBool(_bowRapidFireHash, false);
            }
        }

        public void TriggerBowDraw(bool rapidFire)
        {
            RefreshCombatStateHold();

            if (_animator == null || !_paramsCached || !IsBowEquippedRuntime())
            {
                return;
            }

            if (_bowAimTarget != null && !_bowAimTarget.IsAlive)
            {
                _bowAimTarget = null;
            }

            if (_bowAimTarget == null)
            {
                _bowAimTarget = ResolveBowAimTargetFallback();
            }

            if (_bowAimTarget == null || !_bowAimTarget.IsAlive)
            {
                return;
            }

            if (_hasBowEquipped)
            {
                _animator.SetBool(_bowEquippedHash, true);
            }

            _ = rapidFire;

            if (_hasBowRapidFire)
            {
                _animator.SetBool(_bowRapidFireHash, false);
            }

            if (_hasBowNotch)
            {
                _animator.SetTrigger(_bowNotchHash);
            }

            StartBowVisualDraw();
        }

        public void TriggerBowRelease(bool rapidFire)
        {
            RefreshCombatStateHold();

            if (_animator == null || !_paramsCached || !IsBowEquippedRuntime())
            {
                return;
            }

            _ = rapidFire;

            if (_hasBowRapidFire)
            {
                _animator.SetBool(_bowRapidFireHash, false);
            }

            if (_hasBowShoot)
            {
                _animator.SetTrigger(_bowShootHash);
            }

            if (IsAnimatorInStateOrTransitionTo(bowNotchStateName))
            {
                float previousCrossFadeSeconds = fallbackCrossFadeSeconds;
                fallbackCrossFadeSeconds = Mathf.Max(0f, bowShootFallbackCrossFadeSeconds);
                _ = TryCrossFadeState(bowShootStateName);
                fallbackCrossFadeSeconds = previousCrossFadeSeconds;
            }

            StartBowVisualRelease();
        }

        private void DriveBowParameters()
        {
            if (_animator == null || !_paramsCached)
            {
                return;
            }

            bool bowEquippedRuntime = IsBowEquippedRuntime();

            if (_bowAimTarget != null && !_bowAimTarget.IsAlive)
            {
                _bowAimTarget = null;
            }

            if (_bowAimTarget == null && bowEquippedRuntime)
            {
                _bowAimTarget = ResolveBowAimTargetFallback();
            }

            bool hasBowAimTarget = _bowAimTarget != null && _bowAimTarget.IsAlive;

            if (_hasBowEquipped)
            {
                _animator.SetBool(_bowEquippedHash, bowEquippedRuntime && hasBowAimTarget);
            }

            if (!bowEquippedRuntime || !hasBowAimTarget)
            {
                SetBowAnimatorDefaults();
                return;
            }

            bool bowAnimationAllowed = IsBowAnimationAllowed();
            if (!bowAnimationAllowed)
            {
                if (_hasBowAimY)
                {
                    _animator.SetFloat(_bowAimYHash, 0f);
                }

                if (_hasBowRapidFire)
                {
                    _animator.SetBool(_bowRapidFireHash, false);
                }

                return;
            }

            float normalizedAimY = 0f;

            if (_bowAimTarget != null)
            {
                RefreshCombatStateHold();
            }

            if (_hasBowAimY)
            {
                _animator.SetFloat(_bowAimYHash, normalizedAimY);
            }

            if (_hasBowRapidFire && _bowAimTarget == null)
            {
                _animator.SetBool(_bowRapidFireHash, false);
            }
        }

        private bool IsAnimatorInStateOrTransitionTo(string stateName)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);
            if (StateMatchesName(currentState, stateName))
            {
                return true;
            }

            if (_animator.IsInTransition(0))
            {
                AnimatorStateInfo nextState = _animator.GetNextAnimatorStateInfo(0);
                return StateMatchesName(nextState, stateName);
            }

            return false;
        }

        private bool IsBowAnimationAllowed()
        {
            if (!IsBowEquippedRuntime())
            {
                return false;
            }

            if (_bowAimTarget == null || !_bowAimTarget.IsAlive)
            {
                return false;
            }

            if (_unitController == null)
            {
                _unitController = GetComponent<UnitController>();
            }

            if (_unitController == null)
            {
                return true;
            }

            if (_unitController.IsSprinting)
            {
                return false;
            }

            if (_unitController.HasMoveTarget || _unitController.IsMoving)
            {
                return false;
            }

            float speed = _unitController.WorldVelocity.magnitude;
            if (speed > Mathf.Max(0f, bowAimActivationMaxSpeedMetersPerSecond))
            {
                return false;
            }

            float maxSpeed = Mathf.Max(0.01f, _unitController.MoveSpeed);
            float normalizedSpeed = Mathf.Clamp01(speed / maxSpeed);
            if (normalizedSpeed > Mathf.Clamp01(bowAnimationMaxNormalizedMoveSpeed))
            {
                return false;
            }

            return IsFacingBowAimTarget(_bowAimTarget);
        }

        private bool IsFacingBowAimTarget(Unit targetUnit)
        {
            if (targetUnit == null)
            {
                return false;
            }

            Vector3 toTarget = targetUnit.transform.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            float angle = Vector3.Angle(forward, toTarget.normalized);
            return angle <= Mathf.Clamp(bowAimFacingToleranceDegrees, 0f, 180f);
        }

        private Unit ResolveBowAimTargetFallback()
        {
            if (unit == null)
            {
                unit = GetComponent<Unit>();
            }

            if (unit == null)
            {
                return null;
            }

            if (_encounterManager == null)
            {
                _encounterManager = CombatEncounterManager.Instance != null
                    ? CombatEncounterManager.Instance
                    : Object.FindFirstObjectByType<CombatEncounterManager>();
            }

            if (_encounterManager != null
                && _encounterManager.TryGetEncounterOpponent(unit, out Unit encounterOpponent)
                && encounterOpponent != null
                && encounterOpponent.IsAlive)
            {
                return encounterOpponent;
            }

            return null;
        }

        private bool IsBowEquippedRuntime()
        {
            if (weaponSystem == null)
            {
                weaponSystem = GetComponent<WeaponSystem>();
            }

            return weaponSystem != null && weaponSystem.IsBowEquipped;
        }

        private void SetBowAnimatorDefaults()
        {
            if (!CanWriteAnimatorParameters() || !_paramsCached)
            {
                return;
            }

            if (_hasBowEquipped)
            {
                _animator.SetBool(_bowEquippedHash, false);
            }

            if (_hasBowAimY)
            {
                _animator.SetFloat(_bowAimYHash, 0f);
            }

            if (_hasBowRapidFire)
            {
                _animator.SetBool(_bowRapidFireHash, false);
            }
        }

        private void TickBowVisualPlayback()
        {
            if (!driveBowVisualRig || _bowVisualPlaybackPhase == BowVisualPlaybackPhase.None)
            {
                return;
            }

            if (!EnsureBowVisualPlayable())
            {
                return;
            }

            float clipLength = Mathf.Max(0.01f, _bowVisualClip.length);
            float holdTime = clipLength * Mathf.Clamp01(bowVisualHoldNormalizedTime);

            switch (_bowVisualPlaybackPhase)
            {
                case BowVisualPlaybackPhase.Draw:
                    _bowVisualSampleTime += Time.deltaTime * Mathf.Max(0.01f, bowVisualDrawPlaybackSpeed);
                    if (_bowVisualSampleTime >= holdTime)
                    {
                        _bowVisualSampleTime = holdTime;
                        _bowVisualPlaybackPhase = BowVisualPlaybackPhase.Hold;
                    }
                    break;

                case BowVisualPlaybackPhase.Hold:
                    _bowVisualSampleTime = holdTime;
                    break;

                case BowVisualPlaybackPhase.Release:
                    _bowVisualSampleTime += Time.deltaTime * Mathf.Max(0.01f, bowVisualReleasePlaybackSpeed);
                    if (_bowVisualSampleTime >= clipLength)
                    {
                        _bowVisualSampleTime = clipLength;
                        _bowVisualPlaybackPhase = BowVisualPlaybackPhase.None;
                    }
                    break;
            }

            _bowVisualPlayable.SetTime(_bowVisualSampleTime);
            _bowVisualPlayable.SetSpeed(0f);
            _bowVisualGraph.Evaluate(0f);
        }

        private void StartBowVisualDraw()
        {
            if (!driveBowVisualRig || !EnsureBowVisualPlayable())
            {
                return;
            }

            _bowVisualSampleTime = 0f;
            _bowVisualPlaybackPhase = BowVisualPlaybackPhase.Draw;
            _bowVisualPlayable.SetTime(_bowVisualSampleTime);
            _bowVisualPlayable.SetSpeed(0f);
            _bowVisualGraph.Evaluate(0f);
        }

        private void StartBowVisualRelease()
        {
            if (!driveBowVisualRig || !EnsureBowVisualPlayable())
            {
                return;
            }

            if (_bowVisualPlaybackPhase == BowVisualPlaybackPhase.None)
            {
                float clipLength = Mathf.Max(0.01f, _bowVisualClip.length);
                _bowVisualSampleTime = clipLength * Mathf.Clamp01(bowVisualHoldNormalizedTime);
            }

            _bowVisualPlaybackPhase = BowVisualPlaybackPhase.Release;
        }

        private bool EnsureBowVisualPlayable()
        {
            if (!driveBowVisualRig)
            {
                return false;
            }

            if (_bowVisualAnimator == null)
            {
                _bowVisualAnimator = ResolveBowVisualAnimator();
            }

            if (_bowVisualAnimator == null)
            {
                return false;
            }

            if (_bowVisualClip == null)
            {
                _bowVisualClip = ResolveBowVisualClip();
            }

            if (_bowVisualClip == null)
            {
                return false;
            }

            if (_bowVisualGraph.IsValid())
            {
                return true;
            }

            _bowVisualGraph = PlayableGraph.Create($"{name}_BowVisualRig");
            _bowVisualPlayable = AnimationClipPlayable.Create(_bowVisualGraph, _bowVisualClip);
            _bowVisualPlayable.SetApplyFootIK(false);
            _bowVisualPlayable.SetApplyPlayableIK(false);
            _bowVisualPlayable.SetTime(0d);
            _bowVisualPlayable.SetSpeed(0d);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_bowVisualGraph, "BowVisual", _bowVisualAnimator);
            output.SetSourcePlayable(_bowVisualPlayable);

            _bowVisualGraph.Play();
            _bowVisualGraph.Evaluate(0f);
            return true;
        }

        private Animator ResolveBowVisualAnimator()
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            if (animators == null || animators.Length == 0)
            {
                return null;
            }

            string nameToken = string.IsNullOrWhiteSpace(bowVisualNameContains)
                ? string.Empty
                : bowVisualNameContains.ToLowerInvariant();

            for (int i = 0; i < animators.Length; i++)
            {
                Animator candidate = animators[i];
                if (candidate == null || candidate == _animator)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(nameToken) || candidate.name.ToLowerInvariant().Contains(nameToken))
                {
                    return candidate;
                }
            }

            for (int i = 0; i < animators.Length; i++)
            {
                Animator candidate = animators[i];
                if (candidate != null && candidate != _animator)
                {
                    return candidate;
                }
            }

            return null;
        }

        private AnimationClip ResolveBowVisualClip()
        {
            if (string.IsNullOrWhiteSpace(bowVisualClipName))
            {
                AnimationClip animatorFallback = ResolveBowVisualClipFromAnimatorController();
                if (animatorFallback != null)
                {
                    return animatorFallback;
                }

#if UNITY_EDITOR
                return ResolveBowVisualClipFromAssetPath();
#else
                return null;
#endif
            }

            AnimationClip[] allClips = Resources.FindObjectsOfTypeAll<AnimationClip>();
            if (allClips == null || allClips.Length == 0)
            {
                AnimationClip animatorFallback = ResolveBowVisualClipFromAnimatorController();
                if (animatorFallback != null)
                {
                    return animatorFallback;
                }

#if UNITY_EDITOR
                return ResolveBowVisualClipFromAssetPath();
#else
                return null;
#endif
            }

            for (int i = 0; i < allClips.Length; i++)
            {
                AnimationClip clip = allClips[i];
                if (clip == null)
                {
                    continue;
                }

                if (string.Equals(clip.name, bowVisualClipName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            for (int i = 0; i < allClips.Length; i++)
            {
                AnimationClip clip = allClips[i];
                if (clip == null)
                {
                    continue;
                }

                if (clip.name.IndexOf(bowVisualClipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return clip;
                }
            }

            AnimationClip fallbackClip = ResolveBowVisualClipFromAnimatorController();
            if (fallbackClip != null)
            {
                return fallbackClip;
            }

#if UNITY_EDITOR
            return ResolveBowVisualClipFromAssetPath();
#else
            return null;
#endif
        }

        private AnimationClip ResolveBowVisualClipFromAnimatorController()
        {
            if (_bowVisualAnimator == null || _bowVisualAnimator.runtimeAnimatorController == null)
            {
                return null;
            }

            AnimationClip[] controllerClips = _bowVisualAnimator.runtimeAnimatorController.animationClips;
            if (controllerClips == null || controllerClips.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < controllerClips.Length; i++)
            {
                AnimationClip clip = controllerClips[i];
                if (clip == null)
                {
                    continue;
                }

                if (string.Equals(clip.name, bowVisualClipName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            for (int i = 0; i < controllerClips.Length; i++)
            {
                AnimationClip clip = controllerClips[i];
                if (clip == null)
                {
                    continue;
                }

                if (clip.name.IndexOf(bowVisualClipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return clip;
                }
            }

            for (int i = 0; i < controllerClips.Length; i++)
            {
                AnimationClip clip = controllerClips[i];
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        private AnimationClip ResolveBowVisualClipFromAssetPath()
        {
            if (string.IsNullOrWhiteSpace(bowVisualClipAssetPath))
            {
                return null;
            }

            Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(bowVisualClipAssetPath);
            if (assets == null || assets.Length == 0)
            {
                return null;
            }

            AnimationClip fallback = null;
            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip == null)
                {
                    continue;
                }

                if (clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = clip;
                }

                if (string.Equals(clip.name, bowVisualClipName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }

                if (clip.name.IndexOf(bowVisualClipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return clip;
                }
            }

            return fallback;
        }
#endif

        private void ResetBowVisualPlayback(bool clearRigState)
        {
            _bowVisualPlaybackPhase = BowVisualPlaybackPhase.None;
            _bowVisualSampleTime = 0f;

            if (!_bowVisualGraph.IsValid())
            {
                if (clearRigState)
                {
                    _bowVisualAnimator = null;
                    _bowVisualClip = null;
                }

                return;
            }

            _bowVisualPlayable.SetTime(0d);
            _bowVisualPlayable.SetSpeed(0d);
            _bowVisualGraph.Evaluate(0f);

            if (clearRigState)
            {
                _bowVisualGraph.Destroy();
                _bowVisualAnimator = null;
                _bowVisualClip = null;
            }
        }

        private void SetCombatAnimatorFlag(bool value)
        {
            if (!CanWriteAnimatorParameters() || !_hasIsInCombat)
            {
                return;
            }

            _animator.SetBool(_isInCombatHash, value);
        }

        private bool CanWriteAnimatorParameters()
        {
            return _animator != null
                && _animator.isActiveAndEnabled
                && _animator.runtimeAnimatorController != null;
        }
    }
}
