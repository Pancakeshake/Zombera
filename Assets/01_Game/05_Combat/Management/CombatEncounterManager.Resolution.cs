using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

namespace Zombera.Combat
{
    public sealed partial class CombatEncounterManager
    {
        // ──────────────────────────────────────────────
        //  Encounter execution: validate → tick → resolve → end
        // ──────────────────────────────────────────────

        private bool ValidateEncounter(EncounterState state, out string reason)
        {
            reason = string.Empty;

            if (state == null || state.UnitA == null || state.UnitB == null)
            {
                reason = "invalid";
                return false;
            }

            if (!state.UnitA.IsAlive || !state.UnitB.IsAlive)
            {
                reason = "death";
                return false;
            }

            var maxDistanceSqr = DisengageRange * DisengageRange;
            var distanceSqr = (state.UnitA.transform.position - state.UnitB.transform.position).sqrMagnitude;
            if (distanceSqr <= maxDistanceSqr) return true;

            reason = "distance";
            return false;
        }

        private void ExecuteTick(EncounterState state)
        {
            var attacker = state.CurrentAttacker;
            var defender = attacker == state.UnitA ? state.UnitB : state.UnitA;

            if (attacker == null || defender == null)
            {
                EndEncounter(state.EncounterId, "invalid");
                return;
            }

            if (pauseNavigationDuringEncounter)
            {
                attacker.Controller?.Stop();
                defender.Controller?.Stop();
            }

            // If a survivor can be in multiple melee encounters, force their outgoing strike
            // to stay on the currently faced opponent instead of alternating by tick order.
            if (ShouldHoldSurvivorAttackOnCurrentDefender(attacker)
                && !IsPreferredFocusOpponent(attacker, defender))
            {
                state.CurrentAttacker = defender;
                return;
            }

            if (alignAttackerToDefenderBeforeAttack)
                AlignAttackerToDefender(attacker, defender, !smoothFacingDuringWindup);

            var result = ResolveAttack(attacker, defender);

            state.HasPendingAttack = true;
            state.PendingAttacker = attacker;
            state.PendingDefender = defender;
            state.PendingResult = result;
            var windupSeconds = Mathf.Max(0f, attackWindupSeconds);
            state.PendingResolveAt = Time.time + windupSeconds;

            CoreEventBus.PublishGlobal(new CombatAttackWindupEvent
            {
                EncounterId = state.EncounterId,
                Attacker = attacker,
                Defender = defender,
                WindupSeconds = windupSeconds,
                HitChance01 = result.HitChance01
            });

            if (state.PendingResolveAt <= Time.time) ResolvePendingAttack(state);
        }

        private void ResolvePendingAttack(EncounterState state)
        {
            if (state is not { HasPendingAttack: true }) return;

            var attacker = state.PendingAttacker;
            var defender = state.PendingDefender;
            var result = state.PendingResult;

            state.HasPendingAttack = false;
            state.PendingAttacker = null;
            state.PendingDefender = null;
            state.PendingResolveAt = 0f;

            if (attacker == null || defender == null)
            {
                EndEncounter(state.EncounterId, "invalid");
                return;
            }

            if (!attacker.IsAlive || !defender.IsAlive)
            {
                EndEncounter(state.EncounterId, "death");
                return;
            }

            if (alignAttackerToDefenderBeforeAttack)
                AlignAttackerToDefender(attacker, defender, !smoothFacingDuringWindup);

            var blockedByPositioning = (requireFacingToLandHit && !IsAttackerFacingDefender(attacker, defender))
                                       || (requireMeleeRangeToLandHit &&
                                           !IsAttackerWithinMeleeHitRange(attacker, defender));

            if (blockedByPositioning) result = CreateBlockedHitResult();

            var attackStyle = CombatAttackStyle.Unknown;
            var preferredReactionArea = CombatReactionArea.Chest;
            if (CombatAttackPresentationRegistry.TryConsumeSelection(
                    state.EncounterId,
                    attacker,
                    defender,
                    out var selectedAttackStyle,
                    out var selectedReactionArea))
            {
                attackStyle = selectedAttackStyle;
                preferredReactionArea = selectedReactionArea;
            }

            var attackerStats = attacker.Stats ?? attacker.GetComponent<UnitStats>();
            var attackerInventory = attacker.Inventory ?? attacker.GetComponent<UnitInventory>();
            var attackerStunChance01 = attackerStats != null
                ? Mathf.Clamp01(attackerStats.GetMeleeKnockbackChance() +
                                attackerStats.GetStrengthKnockbackChanceBonus())
                : 0f;

            if (result.DidHit && defender.Health != null)
            {
                CombatAttackPresentationRegistry.RegisterIncomingReactionHint(defender, preferredReactionArea);
                DamageSystem.ApplyDamage(defender.Health, result.Damage, DamageType.Melee, attacker.gameObject);

                if (attackerStats != null
                    && attackerInventory != null
                    && attackerStats.IsHeavyCarry(attackerInventory.CarryRatio))
                    attackerStats.RecordWeightedCombatHit(false);

                // Knockback: rolled from Melee + Strength chance.
                if (attackerStunChance01 > 0f && Random.value < attackerStunChance01)
                {
                    var defenderRb = defender.GetComponent<Rigidbody>();
                    if (defenderRb != null && !defenderRb.isKinematic)
                    {
                        var dir = defender.transform.position - attacker.transform.position;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.0001f)
                            defenderRb.AddForce(dir.normalized * knockbackImpulseForce, ForceMode.Impulse);
                    }
                }
            }

            CoreEventBus.PublishGlobal(new CombatTickResolvedEvent
            {
                EncounterId = state.EncounterId,
                Attacker = attacker,
                Defender = defender,
                AttackStyle = attackStyle,
                PreferredReactionArea = preferredReactionArea,
                DidHit = result.DidHit,
                DidDefenderDodge = !result.DidHit && !blockedByPositioning,
                IsCritical = result.IsCritical,
                Damage = result.Damage,
                HitChance01 = result.HitChance01,
                AttackerStunChance01 = attackerStunChance01
            });

            if (!defender.IsAlive)
            {
                EndEncounter(state.EncounterId, "death", attacker, defender);
                return;
            }

            // Survivors (player/squad) may only be the active attacker in one encounter at
            // a time when unarmed — prevents them from auto-counter-attacking every zombie
            // simultaneously. If they're already swinging in another encounter, the zombie
            // keeps the initiative this round.
            var defenderWeapons = defender.GetComponent<WeaponSystem>();
            var defenderIsUnarmedSurvivor = defender.Faction == UnitFaction.Survivor
                                            && (defenderWeapons == null || defenderWeapons.EquippedWeapon == null);
            if (defenderIsUnarmedSurvivor &&
                (IsSurvivorAlreadyAttackingElsewhere(defender, state.EncounterId)
                 || (ShouldHoldSurvivorAttackOnCurrentDefender(defender)
                     && !IsPreferredFocusOpponent(defender, attacker))))
                state.CurrentAttacker = attacker;
            else
                state.CurrentAttacker = defender;
        }

        private void EndEncounter(int encounterId, string reason, Unit winner = null, Unit loser = null)
        {
            if (!_encountersById.Remove(encounterId, out var state)) return;

            if (state.UnitA != null && _encounterIdByUnit.TryGetValue(state.UnitA, out var idA) && idA == encounterId)
                _encounterIdByUnit.Remove(state.UnitA);

            if (state.UnitB != null && _encounterIdByUnit.TryGetValue(state.UnitB, out var idB) && idB == encounterId)
                _encounterIdByUnit.Remove(state.UnitB);

            tickScheduler?.UnregisterEncounter(encounterId);
            CombatAttackPresentationRegistry.ClearEncounter(encounterId);

            var resolvedWinner = winner;
            var resolvedLoser = loser;

            if (resolvedWinner == null || resolvedLoser == null)
            {
                var aAlive = state.UnitA != null && state.UnitA.IsAlive;
                var bAlive = state.UnitB != null && state.UnitB.IsAlive;

                if (aAlive && !bAlive)
                {
                    resolvedWinner = state.UnitA;
                    resolvedLoser = state.UnitB;
                }
                else if (bAlive && !aAlive)
                {
                    resolvedWinner = state.UnitB;
                    resolvedLoser = state.UnitA;
                }
            }

            CoreEventBus.PublishGlobal(new CombatEncounterEndedEvent
            {
                EncounterId = encounterId,
                Winner = resolvedWinner,
                Loser = resolvedLoser,
                Reason = reason
            });
        }
    }
}
