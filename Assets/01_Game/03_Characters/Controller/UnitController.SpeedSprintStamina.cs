#region

using UnityEngine;

#endregion

namespace Zombera.Characters
{
    public sealed partial class UnitController
    {
        public void SetMoveSpeed(float speed)
        {
            _baseMovSpeed = Mathf.Max(0.1f, speed);
            RefreshAppliedSpeed();
        }

        /// <summary>Apply a posture-driven speed multiplier (crouch/crawl). Pass 1 to clear.</summary>
        public void SetPostureSpeedMultiplier(float multiplier)
        {
            _postureSpeedMultiplier = Mathf.Max(0.05f, multiplier);
            RefreshAppliedSpeed();
        }

        /// <summary>
        ///     Activates or deactivates sprint. Speed and stamina drain are handled in Update.
        ///     Does nothing if there is no stamina left.
        /// </summary>
        public void SetSprintActive(bool sprint)
        {
            var wouldSprint = sprint && (unitStats == null || unitStats.Stamina > 0f);
            if (wouldSprint == IsSprinting) return;
            IsSprinting = wouldSprint;
            RefreshAppliedSpeed();
        }

        private void RefreshAppliedSpeed()
        {
            var agilityMult = unitStats != null ? unitStats.GetAgilityMoveSpeedMultiplier() : 1f;
            var encumbranceMult = unitStats != null && unitInventory != null
                ? unitStats.GetEncumbranceSpeedMultiplier(unitInventory.CarryRatio)
                : 1f;

            _lastEncumbranceSpeedMultiplier = encumbranceMult;

            _baseAppliedMoveSpeed =
                Mathf.Max(0.1f, _baseMovSpeed * agilityMult * encumbranceMult * _postureSpeedMultiplier);

            if (!IsSprinting) _sprintBlend = 0f;

            ApplyEffectiveMoveSpeed();
        }

        private void ApplyEffectiveMoveSpeed()
        {
            var sprintMult = Mathf.Lerp(1f, sprintSpeedMultiplier, _sprintBlend);
            var moveStartRampMult = EvaluateMoveStartRampMultiplier();
            moveSpeed = Mathf.Max(0.1f, _baseAppliedMoveSpeed * sprintMult * moveStartRampMult);
            if (_agent != null) _agent.speed = moveSpeed;
        }

        private float EvaluateMoveStartRampMultiplier()
        {
            if (_moveStartRampTimer <= 0f || _moveStartRampDuration <= 0f) return 1f;

            var elapsed01 = 1f - _moveStartRampTimer / _moveStartRampDuration;
            return Mathf.Lerp(_moveStartRampInitialMultiplier, 1f, Mathf.Clamp01(elapsed01));
        }

        private void TickMoveStartRamp()
        {
            if (_moveStartRampTimer <= 0f) return;

            var hasMoveIntent = MoveInput.sqrMagnitude > 0.0001f || HasMoveTarget;
            if (!hasMoveIntent) return;

            _moveStartRampTimer = Mathf.Max(0f, _moveStartRampTimer - Time.deltaTime);
            ApplyEffectiveMoveSpeed();
        }

        public void BeginMoveSpeedRamp(float durationSeconds, float startSpeedMultiplier = 0.35f)
        {
            var clampedDuration = Mathf.Max(0f, durationSeconds);
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
                if (_sprintBlend <= 0f) return;

                _sprintBlend = 0f;
                ApplyEffectiveMoveSpeed();
                return;
            }

            var hasMoveIntent = MoveInput.sqrMagnitude > 0.0001f || HasMoveTarget;
            if (!hasMoveIntent)
            {
                if (_sprintBlend <= 0f) return;

                _sprintBlend = 0f;
                ApplyEffectiveMoveSpeed();
                return;
            }

            var blendStep = Time.deltaTime / Mathf.Max(0.01f, sprintBuildUpSeconds);
            var nextBlend = Mathf.MoveTowards(_sprintBlend, 1f, blendStep);
            if (Mathf.Approximately(nextBlend, _sprintBlend)) return;

            _sprintBlend = nextBlend;
            ApplyEffectiveMoveSpeed();
        }

        private void TryRecordHeavyCarryWalkDistance(float distanceMeters)
        {
            if (distanceMeters <= 0f || unitStats == null || unitInventory == null) return;

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
                var regenRate = distanceMeters > 0f
                    ? unitStats.StaminaRegenPerSecondWalk
                    : unitStats.StaminaRegenPerSecondIdle;
                unitStats.RegenStamina(regenRate * Time.deltaTime);
            }
        }
    }
}
