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

namespace Zombera.AI
{
    public sealed partial class ZombieAnimationController
    {

        private void EnsureInitializedForCurrentAnimator()
        {
            if (animator == null) return;

            var currentController = animator.runtimeAnimatorController;
            var needsRefresh = _initializedAnimator != animator
                               || _initializedAnimatorController != currentController;

            ApplyAnimatorRuntimeDefaults();

            if (!needsRefresh) return;

            BuildFolderVariantCategories();
            ResolveAttackClipsFromAnimator();
            CacheAnimatorParameters();
            EnsureRuntimeOverrideController();
            ApplyIdleAndLocomotionVariants();

            _initializedAnimator = animator;
            _initializedAnimatorController = animator.runtimeAnimatorController;
        }

        // If baseAttackClip / altAttackClip are not wired in the inspector,
        // find them by name from the animator controller's embedded clip list.
        private void ResolveAttackClipsFromAnimator()
        {
            if (animator == null) return;
            var ctrl = animator.runtimeAnimatorController;
            if (ctrl == null) return;

            foreach (var clip in ctrl.animationClips)
            {
                if (baseAttackClip == null && clip.name == "Zombie_Bite")
                    baseAttackClip = clip;
                if (altAttackClip == null && clip.name == "Zombie_Scratch")
                    altAttackClip = clip;

                if (baseCombatIdleClip == null)
                {
                    var normalizedName = clip.name.ToLowerInvariant();
                    if (normalizedName.Contains("combat_idle_zombie"))
                        baseCombatIdleClip = clip;
                }
            }
        }


        private void UpdateLODThrottling()
        {
            if (UnitManager.Instance == null)
            {
                _updateInterval = 0f;
                return;
            }

            ResolveSharedPlayerTransform();
            var playerTransform = s_cachedPlayerTransform;
            if (playerTransform == null)
            {
                _updateInterval = 0.1f;
                return;
            }

            var distSqr = (playerTransform.position - transform.position).sqrMagnitude;
            _updateInterval = ResolveLODInterval(distSqr);
        }

        private static float ResolveLODInterval(float distSqr)
        {
            if (distSqr < 15f * 15f) return 0f;      // Close — every frame
            if (distSqr < 40f * 40f) return 0.05f;    // Medium — ~20 FPS
            if (distSqr < 80f * 80f) return 0.1f;     // Far — 10 FPS
            return 0.5f;                                // Very far — 2 FPS
        }

        private static void ResolveSharedPlayerTransform()
        {
            if (s_cachedPlayerTransform != null && Time.unscaledTime < s_nextSharedPlayerResolveAt)
                return;

            var player = UnitManager.Instance.FindFirstUnitByRole(UnitRole.Player);
            s_cachedPlayerTransform = player != null ? player.transform : null;
            s_nextSharedPlayerResolveAt = Time.unscaledTime + SharedPlayerResolveIntervalSeconds;
        }


        private void DriveLocomotionParameters()
        {
            if (animator == null) return;
            if (!_hasSpeedFloat && !_hasVelocityX && !_hasVelocityZ) return;

            ComputeLocomotionValues(out var speed, out var localVel);
            var dt = Time.deltaTime;

            if (_hasSpeedFloat)
                SetAnimatorFloatDamped(_speedFloatHash, speed, locomotionSpeedDampTime, dt);

            if (_hasVelocityX)
                SetAnimatorFloatDamped(_velocityXHash, localVel.x, locomotionVelocityDampTime, dt);

            if (_hasVelocityZ)
                SetAnimatorFloatDamped(_velocityZHash, localVel.z, locomotionVelocityDampTime, dt);
        }

        private void ComputeLocomotionValues(out float speed, out Vector3 localVel)
        {
            speed = 0f;
            localVel = Vector3.zero;

            if (_navMeshAgent == null || !_navMeshAgent.enabled || !_navMeshAgent.isOnNavMesh)
                return;

            var worldVelocity = _navMeshAgent.velocity;
            speed = worldVelocity.magnitude;
            if (speed <= 0.01f) return;

            localVel = transform.InverseTransformDirection(worldVelocity / speed);
        }

        private void SetAnimatorFloatDamped(int hash, float value, float dampTime, float dt)
        {
            if (dampTime > 0f)
                animator.SetFloat(hash, value, dampTime, dt);
            else
                animator.SetFloat(hash, value);
        }


        private void ApplyAnimatorRuntimeDefaults()
        {
            if (animator == null) return;

            if (animator.applyRootMotion) animator.applyRootMotion = false;

            if (applyAnimatorCullDefaults) animator.cullingMode = animatorCullingMode;
        }


        private void CacheAnimatorParameters()
        {
            _attackTriggerHash = Animator.StringToHash(attackTriggerParameter);
            _altAttackTriggerHash = Animator.StringToHash(altAttackTriggerParameter);
            _dodgeTriggerHash = Animator.StringToHash(dodgeTriggerParameter);
            _hitTriggerHash = Animator.StringToHash(hitTriggerParameter);
            _dieTriggerHash = Animator.StringToHash(dieTriggerParameter);
            _spawnTriggerHash = Animator.StringToHash(spawnTriggerParameter);
            _isDeadHash = Animator.StringToHash(isDeadParameter);
            _isInCombatHash = Animator.StringToHash(isInCombatParameter);
            _speedFloatHash = Animator.StringToHash(speedFloatParameter);
            _velocityXHash = Animator.StringToHash(velocityXParameter);
            _velocityZHash = Animator.StringToHash(velocityZParameter);
            _deathRollHash = Animator.StringToHash(deathRollParameter);

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                ResetAnimatorParameterAvailability();
                return;
            }

            var parameters = animator.parameters;

            _hasAttackTrigger = HasParameter(parameters, attackTriggerParameter, AnimatorControllerParameterType.Trigger);
            _hasAltAttackTrigger = HasParameter(parameters, altAttackTriggerParameter, AnimatorControllerParameterType.Trigger);
            _hasDodgeTrigger = HasParameter(parameters, dodgeTriggerParameter, AnimatorControllerParameterType.Trigger);
            _hasHitTrigger = HasParameter(parameters, hitTriggerParameter, AnimatorControllerParameterType.Trigger);
            _hasDieTrigger = HasParameter(parameters, dieTriggerParameter, AnimatorControllerParameterType.Trigger);
            _hasSpawnTrigger = HasParameter(parameters, spawnTriggerParameter, AnimatorControllerParameterType.Trigger);
            _hasIsDeadBool = HasParameter(parameters, isDeadParameter, AnimatorControllerParameterType.Bool);
            _hasIsInCombatBool = HasParameter(parameters, isInCombatParameter, AnimatorControllerParameterType.Bool);
            _hasSpeedFloat = HasParameter(parameters, speedFloatParameter, AnimatorControllerParameterType.Float);
            _hasVelocityX = HasParameter(parameters, velocityXParameter, AnimatorControllerParameterType.Float);
            _hasVelocityZ = HasParameter(parameters, velocityZParameter, AnimatorControllerParameterType.Float);
            _hasDeathRoll = HasParameter(parameters, deathRollParameter, AnimatorControllerParameterType.Float);

            BuildFolderVariantCategories();
        }


        private void ResetAnimatorParameterAvailability()
        {
            _hasAttackTrigger = false;
            _hasAltAttackTrigger = false;
            _hasDodgeTrigger = false;
            _hasHitTrigger = false;
            _hasDieTrigger = false;
            _hasSpawnTrigger = false;
            _hasIsDeadBool = false;
            _hasIsInCombatBool = false;
            _hasSpeedFloat = false;
            _hasVelocityX = false;
            _hasVelocityZ = false;
            _hasDeathRoll = false;
        }


        private void TryRestartNonLoopingLocomotionState()
        {
            if (!CanQueryAnimatorState() || string.IsNullOrWhiteSpace(locomotionStateName)) return;

            if (animator.IsInTransition(0)) return;

            if (Time.frameCount == _lastForcedLocomotionRestartFrame) return;

            if (_hasSpeedFloat)
            {
                var speedValue = animator.GetFloat(_speedFloatHash);
                if (speedValue < Mathf.Max(0f, minimumSpeedForLocomotionLoopRestart)) return;
            }

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (!StateMatchesName(stateInfo, locomotionStateName) || stateInfo.loop) return;

            var restartThreshold = Mathf.Clamp01(locomotionLoopRestartThresholdNormalizedTime);
            if (stateInfo.normalizedTime < restartThreshold) return;

            _lastForcedLocomotionRestartFrame = Time.frameCount;

            var stateHash = Animator.StringToHash(locomotionStateName);
            if (animator.HasState(0, stateHash))
            {
                animator.Play(stateHash, 0, 0f);
                return;
            }

            var qualifiedStateName = "Base Layer." + locomotionStateName;
            var qualifiedHash = Animator.StringToHash(qualifiedStateName);
            if (animator.HasState(0, qualifiedHash)) animator.Play(qualifiedHash, 0, 0f);
        }


        private bool CanQueryAnimatorState()
        {
            if (animator == null) return false;
            if (!animator.isActiveAndEnabled) return false;
            if (animator.runtimeAnimatorController == null) return false;
            if (!animator.isInitialized) return false;
            return true;
        }


        private static bool StateMatchesName(AnimatorStateInfo stateInfo, string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName)) return false;

            var shortHash = Animator.StringToHash(stateName);
            if (stateInfo.shortNameHash == shortHash) return true;

            var fullPathHash = Animator.StringToHash("Base Layer." + stateName);
            return stateInfo.fullPathHash == fullPathHash;
        }


        private static bool HasParameter(AnimatorControllerParameter[] parameters, string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            if (parameters == null || string.IsNullOrWhiteSpace(parameterName)) return false;

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                if (parameter.type == parameterType &&
                    string.Equals(parameter.name, parameterName, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
