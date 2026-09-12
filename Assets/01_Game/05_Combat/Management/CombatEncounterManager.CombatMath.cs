using UnityEngine;
using Zombera.Characters;

namespace Zombera.Combat
{
    public sealed partial class CombatEncounterManager
    {
        // ──────────────────────────────────────────────
        //  Attack math + facing + initiative
        // ──────────────────────────────────────────────

        private CombatResult ResolveAttack(Unit attacker, Unit defender)
        {
            UnitStats attackerStats = null;
            if (attacker != null)
                attackerStats = attacker.Stats != null ? attacker.Stats : attacker.GetComponent<UnitStats>();

            UnitStats defenderStats = null;
            if (defender != null)
                defenderStats = defender.Stats != null ? defender.Stats : defender.GetComponent<UnitStats>();

            var accuracy = attackerStats != null
                ? Mathf.RoundToInt(attackerStats.Melee * 0.4f
                                   + attackerStats.Shooting * 0.3f
                                   + attackerStats.Strength * 0.3f)
                : 50;

            var evasion = defenderStats != null
                ? Mathf.RoundToInt(defenderStats.Strength * 0.5f + defenderStats.Agility * 0.5f)
                : 35;

            var damage = baseDamage;
            if (attackerStats != null)
            {
                damage += attackerStats.Melee * 0.2f;
                damage = attackerStats.ApplyMeleeDamageScaling(damage);
            }

            var result = CombatResolver.ResolveAttack(
                accuracy,
                evasion,
                damage,
                hitBias01,
                criticalChance01,
                criticalMultiplier);

            // Agility dodge roll — applied after the base hit-chance calculation.
            if (!result.DidHit || defenderStats == null) return result;

            var dodgeChance = defenderStats.GetAgilityDodgeChance();
            if (dodgeChance > 0f && Random.value < dodgeChance) result = CreateBlockedHitResult();
            return result;
        }

        private void AlignAttackerToDefender(Unit attacker, Unit defender, bool instant)
        {
            if (attacker == null || defender == null) return;

            var toDefender = defender.transform.position - attacker.transform.position;
            toDefender.y = 0f;
            if (toDefender.sqrMagnitude <= 0.0001f) return;

            var targetRotation = Quaternion.LookRotation(toDefender.normalized, Vector3.up);
            if (instant)
            {
                attacker.transform.rotation = targetRotation;
                return;
            }

            var turnSpeed = CombatTuningConfig.FacingTurnSpeedOr(Mathf.Max(0f, facingTurnSpeedDegreesPerSecond));
            if (turnSpeed <= 0f)
            {
                attacker.transform.rotation = targetRotation;
                return;
            }

            attacker.transform.rotation = Quaternion.RotateTowards(attacker.transform.rotation, targetRotation,
                turnSpeed * Time.deltaTime);
        }

        private bool IsAttackerFacingDefender(Unit attacker, Unit defender)
        {
            if (attacker == null || defender == null) return false;

            var toDefender = defender.transform.position - attacker.transform.position;
            toDefender.y = 0f;

            if (toDefender.sqrMagnitude <= 0.0001f) return true;

            var forward = attacker.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            var requiredDot = Mathf.Cos(Mathf.Clamp(requiredFacingAngleDegrees, 0f, 180f) * Mathf.Deg2Rad);
            return Vector3.Dot(forward, toDefender.normalized) >= requiredDot;
        }

        private bool IsAttackerWithinMeleeHitRange(Unit attacker, Unit defender)
        {
            if (attacker == null || defender == null) return false;

            var toDefender = defender.transform.position - attacker.transform.position;
            toDefender.y = 0f;
            var hitRange = Mathf.Max(0.1f, requiredMeleeHitRange);
            return toDefender.sqrMagnitude <= hitRange * hitRange;
        }

        private static CombatResult CreateBlockedHitResult()
        {
            return new CombatResult(
                false,
                false,
                0f,
                0f,
                1f,
                1f);
        }

        private static Unit ResolveOpeningAttacker(Unit initiator, Unit target)
        {
            var initiatorInitiative = ResolveInitiative(initiator);
            var targetInitiative = ResolveInitiative(target);

            if (initiatorInitiative > targetInitiative) return initiator;

            if (targetInitiative > initiatorInitiative) return target;

            return Random.value >= 0.5f ? initiator : target;
        }

        private static int ResolveInitiative(Unit unit)
        {
            if (unit == null || unit.Stats == null) return 50;

            var stats = unit.Stats;
            return Mathf.RoundToInt(stats.Melee * 0.4f + stats.Shooting * 0.35f + stats.Strength * 0.25f);
        }
    }
}
