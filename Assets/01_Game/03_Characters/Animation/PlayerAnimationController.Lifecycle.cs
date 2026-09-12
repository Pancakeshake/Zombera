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

namespace Zombera.Characters
{
    public sealed partial class PlayerAnimationController
    {

        private void Awake()
        {
            if (unit == null) unit = GetComponent<Unit>();
            if (unitHealth == null) unitHealth = GetComponent<UnitHealth>();
            if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();
            BuildFolderVariantCategories();
        }


        private void Update()
        {
            // Animator can be created asynchronously at spawn time; keep trying until it arrives.
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator != null)
                {
                    ApplyAnimatorRuntimeDefaults();
                    CacheParameters();
                    EnsureRuntimeOverrideController();
                    ApplyIdleAndLocomotionVariants();
                }
            }
            else if (!_paramsCached)
            {
                ApplyAnimatorRuntimeDefaults();
                CacheParameters();
                EnsureRuntimeOverrideController();
                ApplyIdleAndLocomotionVariants();
            }

            if (unit == null) unit = GetComponent<Unit>();

            if (unitHealth == null) unitHealth = GetComponent<UnitHealth>();

            if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();

            if (!_healthSubscribed || !_eventSubscribed) SubscribeEvents();

            if (_unitController == null)
                _unitController = GetComponent<UnitController>();

            HandlePostureInput();

            if (_animator != null && _paramsCached)
            {
                ApplyAnimatorRuntimeDefaults();
                SyncDeadBool();

                if (unitHealth != null && unitHealth.IsDead)
                {
                    SetCombatAnimatorFlag(false);
                    SetBowAnimatorDefaults();
                    ResetBowVisualPlayback(false);
                }
                else
                {
                    DriveLocomotionParameters();
                    SyncCombatAnimatorState();
                    DriveBowParameters();
                    TickBowVisualPlayback();
                    TickDodgeFaceLock();
                }
            }
        }


        private void OnEnable()
        {
            SubscribeEvents();
        }


        private void OnDisable()
        {
            ResetBowVisualPlayback(true);
            ResetCombatEyeFocus(true);
            _bowAimTarget = null;
            _dodgeFaceTarget = null;
            _dodgeFaceLockExpiresAt = 0f;
            _combatStateHoldUntilTime = 0f;
            _defensiveReactionSuppressUntilTime = 0f;
            UnsubscribeEvents();
        }


        private void OnAnimatorIK(int layerIndex)
        {
            _ = layerIndex;
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


        private void CacheParameters()
        {
            _attackHash = Animator.StringToHash(attackTriggerParameter);
            _dodgeHash = Animator.StringToHash(dodgeLeftTriggerParameter);
            _dodgeRightHash = Animator.StringToHash(dodgeRightTriggerParameter);
            _hitHash = Animator.StringToHash(hitTriggerParameter);
            _dieHash = Animator.StringToHash(dieTriggerParameter);
            _isDeadHash = Animator.StringToHash(isDeadParameter);
            _combatEntryHash = Animator.StringToHash(combatEntryTriggerParameter);

            _hasAttack = HasParam(attackTriggerParameter, AnimatorControllerParameterType.Trigger);
            _hasDodgeLeft = HasParam(dodgeLeftTriggerParameter, AnimatorControllerParameterType.Trigger);
            _hasDodgeRight = HasParam(dodgeRightTriggerParameter, AnimatorControllerParameterType.Trigger);
            _hasHit = HasParam(hitTriggerParameter, AnimatorControllerParameterType.Trigger);
            _hasDie = HasParam(dieTriggerParameter, AnimatorControllerParameterType.Trigger);
            _hasIsDead = HasParam(isDeadParameter, AnimatorControllerParameterType.Bool);
            _hasCombatEntry = HasParam(combatEntryTriggerParameter, AnimatorControllerParameterType.Trigger);

            _speedHash = Animator.StringToHash(speedParameter);
            _velocityXHash = Animator.StringToHash(velocityXParameter);
            _velocityZHash = Animator.StringToHash(velocityZParameter);
            _isSprintingHash = Animator.StringToHash(isSprintingParameter);
            _isCrouchingHash = Animator.StringToHash(isCrouchingParameter);
            _isCrawlingHash = Animator.StringToHash(isCrawlingParameter);
            _isSittingHash = Animator.StringToHash(isSittingParameter);
            _isInCombatHash = Animator.StringToHash(isInCombatParameter);
            _bowEquippedHash = Animator.StringToHash(bowEquippedParameter);
            _bowAimYHash = Animator.StringToHash(bowAimYParameter);
            _bowNotchHash = Animator.StringToHash(bowNotchParameter);
            _bowShootHash = Animator.StringToHash(bowShootParameter);
            _bowRapidFireHash = Animator.StringToHash(bowRapidFireParameter);

            _hasSpeed = HasParam(speedParameter, AnimatorControllerParameterType.Float);
            _hasVelocityX = HasParam(velocityXParameter, AnimatorControllerParameterType.Float);
            _hasVelocityZ = HasParam(velocityZParameter, AnimatorControllerParameterType.Float);
            _hasIsSprinting = HasParam(isSprintingParameter, AnimatorControllerParameterType.Bool);
            _hasIsCrouching = HasParam(isCrouchingParameter, AnimatorControllerParameterType.Bool);
            _hasIsCrawling = HasParam(isCrawlingParameter, AnimatorControllerParameterType.Bool);
            _hasIsSitting = HasParam(isSittingParameter, AnimatorControllerParameterType.Bool);
            _hasIsInCombat = HasParam(isInCombatParameter, AnimatorControllerParameterType.Bool);
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
            foreach (var p in _animator.parameters)
                if (p.type == type && p.name == paramName)
                    return true;
            return false;
        }


        private void SubscribeEvents()
        {
            if (!_healthSubscribed && unitHealth != null)
            {
                unitHealth.Damaged += OnDamaged;
                unitHealth.Died += OnDied;
                _healthSubscribed = true;
            }

            if (!_eventSubscribed && CoreEventBus.HasInstance)
            {
                CoreEventBus.Instance.Subscribe<CombatAttackWindupEvent>(OnCombatAttackWindup);
                CoreEventBus.Instance.Subscribe<CombatTickResolvedEvent>(OnCombatTickResolved);
                _eventSubscribed = true;
            }
        }


        private void UnsubscribeEvents()
        {
            if (_healthSubscribed && unitHealth != null)
            {
                unitHealth.Damaged -= OnDamaged;
                unitHealth.Died -= OnDied;
                _healthSubscribed = false;
            }

            if (_eventSubscribed)
            {
                CoreEventBus.Instance?.Unsubscribe<CombatAttackWindupEvent>(OnCombatAttackWindup);
                CoreEventBus.Instance?.Unsubscribe<CombatTickResolvedEvent>(OnCombatTickResolved);
                _eventSubscribed = false;
            }
        }


        private void ApplyAnimatorRuntimeDefaults()
        {
            if (_animator == null) return;

            if (_animator.applyRootMotion) _animator.applyRootMotion = false;

            if (applyAnimatorCullDefaults) _animator.cullingMode = animatorCullingMode;
        }
    }
}
