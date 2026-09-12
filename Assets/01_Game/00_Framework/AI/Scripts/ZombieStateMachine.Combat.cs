#region

using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Systems;

#endregion

namespace Zombera.AI
{
    public sealed partial class ZombieStateMachine
    {
        // ── Attack slot management ───────────────────────────────────

        private void TryAcquireAttackSlot()
        {
            _hasAttackSlot = ZombieAttackSlotManager.RequestSlot(this);
        }

        public void ReleaseAttackSlot()
        {
            if (_hasAttackSlot)
            {
                ZombieAttackSlotManager.ReleaseSlot(this);
                _hasAttackSlot = false;
            }
        }

        public void ApplyCombatStun(float durationSeconds)
        {
            var clampedDuration = Mathf.Clamp(durationSeconds, 0f, Mathf.Max(0f, maxCombatStunSeconds));
            if (clampedDuration <= 0f) return;

            _combatStunExpiresAt = Mathf.Max(_combatStunExpiresAt, Time.time + clampedDuration);
            _unitController?.Stop();
            ReleaseAttackSlot();
        }

        // ── Chase ─────────────────────────────────────────────────────

        private void TickChase()
        {
            if (_unitController == null) return;

            // Chase timeout: if we've been chasing too long without closing in, investigate the last known position.
            if (Time.time - _chaseStartTime > chaseTimeoutSeconds)
            {
                if (_chaseTarget != null)
                {
                    SetInvestigateTarget(_chaseTarget.transform.position);
                    _chaseTarget = null;
                    SetState(ZombieState.Investigate);
                }
                else
                {
                    SetState(ZombieState.Wander);
                }

                return;
            }

            if (_chaseTarget == null || !_chaseTarget.IsAlive)
            {
                _chaseTarget = null;
                SetState(ZombieState.Wander);
                return;
            }

            var distSqr = (transform.position - _chaseTarget.transform.position).sqrMagnitude;

            if (distSqr > abandonChaseRange * abandonChaseRange)
            {
                SetInvestigateTarget(_chaseTarget.transform.position);
                _chaseTarget = null;
                SetState(ZombieState.Investigate);
                return;
            }

            if (distSqr <= attackRange * attackRange)
            {
                SetState(ZombieState.Attack);
                return;
            }

            _unitController.MoveTo(_chaseTarget.transform.position);

            // After issuing move, check if path is blocked by a door.
            var door = FindBlockedDoor(_chaseTarget.transform.position);
            if (door != null)
            {
                _targetDoor = door;
                SetState(ZombieState.AttackDoor);
            }
        }

        // ── Attack ────────────────────────────────────────────────────

        private void TickAttack()
        {
            if (!HasLiveChaseTarget())
            {
                SetState(ZombieState.Wander);
                return;
            }

            if (IsBeyondAttackWindow())
            {
                SetState(ZombieState.Chase);
                return;
            }

            if (!EnsureEncounterForAttack())
            {
                SetState(ZombieState.Idle);
                return;
            }

            TickAttackSlotPositioning();
        }

        private bool HasLiveChaseTarget()
        {
            if (_chaseTarget != null && _chaseTarget.IsAlive) return true;

            _chaseTarget = null;
            return false;
        }

        private bool IsBeyondAttackWindow()
        {
            var distSqr = (transform.position - _chaseTarget.transform.position).sqrMagnitude;
            var attackExitRange = Mathf.Max(0.1f, attackRange) * Mathf.Max(1f, attackExitRangeMultiplier);
            var attackExitRangeSqr = attackExitRange * attackExitRange;
            var leashRangeSqr = abandonChaseRange * abandonChaseRange;

            return distSqr > leashRangeSqr || distSqr > attackExitRangeSqr;
        }

        private bool EnsureEncounterForAttack()
        {
            var self = ResolveSelfUnit();
            var encounterManager = EncounterManager;

            if (self == null || encounterManager == null) return false;

            if (encounterManager.IsUnitInEncounter(self)) return true;

            encounterManager.TryStartEncounter(self, _chaseTarget);
            return encounterManager.IsUnitInEncounter(self);
        }

        private Unit ResolveSelfUnit()
        {
            if (_selfUnit == null) _selfUnit = GetComponent<Unit>();

            return _selfUnit;
        }

        private void TickAttackSlotPositioning()
        {
            // Try to hold (or re-check) the attack token each tick.
            if (!_hasAttackSlot) TryAcquireAttackSlot();

            if (_hasAttackSlot)
            {
                MoveIntoAttackTokenPosition();
                return;
            }

            OrbitAroundTarget();
        }

        private void MoveIntoAttackTokenPosition()
        {
            // Token holder: close to melee range, then stop and face the target.
            var targetPosition = _chaseTarget.transform.position;
            var distToTarget = Vector3.Distance(transform.position, targetPosition);
            if (distToTarget > attackRange * 0.78f)
            {
                _unitController?.MoveTo(targetPosition);
                return;
            }

            _unitController?.Stop();
            if (_unitController != null) _unitController.Rotate(targetPosition - transform.position);
        }

        private void OrbitAroundTarget()
        {
            // No token: orbit at duel distance so the fight looks alive.
            _orbitAngle += orbitSpeed * Time.deltaTime;
            var rad = _orbitAngle * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * duelOrbitRadius;
            var orbitPos = _chaseTarget.transform.position + offset;
            _unitController?.MoveTo(orbitPos);
        }

        // ── Call Horde ────────────────────────────────────────────────

        private void TickCallHorde()
        {
            if (_chaseTarget != null && _selfUnit != null && UnitManager.Instance != null)
            {
                var allies = UnitManager.Instance.FindNearbyAllies(_selfUnit, callHordeRadius, _allyBuffer);
                foreach (var ally in allies)
                {
                    if (ally == null) continue;
                    var allyMachine = ally.GetComponent<ZombieStateMachine>();
                    if (allyMachine != null
                        && allyMachine.CurrentState != ZombieState.Chase
                        && allyMachine.CurrentState != ZombieState.Attack)
                    {
                        allyMachine.SetChaseTarget(_chaseTarget);
                        allyMachine.SetState(ZombieState.Chase);
                    }
                }
            }

            SetState(ZombieState.Chase);
        }
    }
}
