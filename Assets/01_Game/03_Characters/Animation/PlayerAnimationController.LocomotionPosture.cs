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

        private Vector3 LocomotionWorldVelocityAfterDeadZone()
        {
            if (_unitController == null) return Vector3.zero;

            var v = _unitController.WorldVelocity;
            var dz = locomotionIdleVelocityDeadZone;
            if (dz <= 0f) return v;

            var planar = new Vector3(v.x, 0f, v.z);
            return planar.sqrMagnitude < dz * dz ? Vector3.zero : v;
        }


        private void DriveLocomotionParameters()
        {
            if (_unitController == null) return;

            var worldVel = LocomotionWorldVelocityAfterDeadZone();
            var localVel = transform.InverseTransformDirection(worldVel);
            var maxSpeed = Mathf.Max(0.01f, _unitController.MoveSpeed);
            var normX = Mathf.Clamp(localVel.x / maxSpeed, -1f, 1f);
            var normZ = Mathf.Clamp(localVel.z / maxSpeed, -1f, 1f);
            var speed = Mathf.Clamp01(worldVel.magnitude / maxSpeed);

            var dt = Time.deltaTime;

            if (_hasSpeed)
            {
                if (locomotionSpeedDampTime > 0f)
                    _animator.SetFloat(_speedHash, speed, locomotionSpeedDampTime, dt);
                else
                    _animator.SetFloat(_speedHash, speed);
            }

            if (_hasVelocityX)
            {
                if (locomotionVelocityDampTime > 0f)
                    _animator.SetFloat(_velocityXHash, normX, locomotionVelocityDampTime, dt);
                else
                    _animator.SetFloat(_velocityXHash, normX);
            }

            if (_hasVelocityZ)
            {
                if (locomotionVelocityDampTime > 0f)
                    _animator.SetFloat(_velocityZHash, normZ, locomotionVelocityDampTime, dt);
                else
                    _animator.SetFloat(_velocityZHash, normZ);
            }

            if (_hasIsSprinting) _animator.SetBool(_isSprintingHash, _unitController.IsSprinting);
        }

        public void ApplyPostureState(PostureState state)
        {
            _isCrouching = state == PostureState.Crouching;
            _isCrawling = state == PostureState.Crawling;
            _isSitting = false;

            if (_animator == null || !_paramsCached) return;
            if (_hasIsCrouching) _animator.SetBool(_isCrouchingHash, _isCrouching);
            if (_hasIsCrawling) _animator.SetBool(_isCrawlingHash, _isCrawling);
            if (_hasIsSitting) _animator.SetBool(_isSittingHash, _isSitting);
        }


        private void HandlePostureInput()
        {
            if (unitHealth != null && unitHealth.IsDead) return;

            // Per-frame stealth XP while crouching/crawling.
            if ((_isCrouching || _isCrawling) && unit?.Stats != null) unit.Stats.RecordUndetectedTime(Time.deltaTime);

            // Keep animator bools in sync in case animator was rebuilt this frame.
            if (_animator != null && _paramsCached)
            {
                if (_hasIsCrouching) _animator.SetBool(_isCrouchingHash, _isCrouching);
                if (_hasIsCrawling) _animator.SetBool(_isCrawlingHash, _isCrawling);
                if (_hasIsSitting) _animator.SetBool(_isSittingHash, _isSitting);
            }
        }
    }
}
