using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

namespace Zombera.Combat
{
    /// <summary>
    /// Authoritative encounter gatekeeper and tactical exchange runner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatEncounterManager : MonoBehaviour
    {
        private sealed class EncounterState
        {
            public int EncounterId;
            public Unit UnitA;
            public Unit UnitB;
            public Unit CurrentAttacker;
            public bool HasPendingAttack;
            public Unit PendingAttacker;
            public Unit PendingDefender;
            public CombatResult PendingResult;
            public float PendingResolveAt;
        }

        public static CombatEncounterManager Instance { get; private set; }

        [Header("Encounter Rules")]
        [SerializeField] private float engageRange = 1.4f;
        [SerializeField] private float disengageRange = 1.75f;
        [SerializeField] private bool singleEncounterMode = false;
        [SerializeField] private bool pauseNavigationDuringEncounter = true;
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Combat Math")]
        [SerializeField] private float baseDamage = 8f;
        [SerializeField] private float hitBias01;
        [SerializeField] private float criticalChance01 = 0.05f;
        [SerializeField] private float criticalMultiplier = 1.5f;

        [Header("Animation Sync")]
        [SerializeField, Min(0f)] private float attackWindupSeconds = 0.32f;

        [Header("Facing Validation")]
        [SerializeField] private bool requireFacingToLandHit = true;
        [SerializeField] private bool alignAttackerToDefenderBeforeAttack = true;
        [SerializeField] private bool smoothFacingDuringWindup = true;
        [SerializeField, Min(0f)] private float facingTurnSpeedDegreesPerSecond = 720f;
        [SerializeField, Range(0f, 180f)] private float requiredFacingAngleDegrees = 65f;

        [Header("Survivor Attack Focus")]
        [Tooltip("When enabled, unarmed survivor counterattacks are locked to the encounter opponent they are currently facing most directly.")]
        [SerializeField] private bool lockUnarmedSurvivorCounterattacksToFocusTarget = true;
        [SerializeField, Range(0f, 180f)] private float survivorCounterattackFocusAngleDegrees = 80f;

        [Header("Hit Distance Validation")]
        [SerializeField] private bool requireMeleeRangeToLandHit = true;
        [SerializeField, Min(0.1f)] private float requiredMeleeHitRange = 1.0f;

        [Header("Knockback")]
        [SerializeField, Min(0f)] private float knockbackImpulseForce = 5f;

        [Header("Services")]
        [SerializeField] private CombatTickScheduler tickScheduler;

        private readonly Dictionary<int, EncounterState> encountersById = new Dictionary<int, EncounterState>();
        private readonly Dictionary<Unit, int> encounterIdByUnit = new Dictionary<Unit, int>();
        private readonly List<int> encounterIdsBuffer = new List<int>(32);

        private int nextEncounterId = 1;

        public float EngageRange => Mathf.Max(0.1f, engageRange);
        public float DisengageRange => Mathf.Max(EngageRange + 0.1f, disengageRange);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeManager()
        {
            if (Instance != null)
            {
                return;
            }

            CombatEncounterManager existing = FindFirstObjectByType<CombatEncounterManager>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            GameObject go = new GameObject("CombatEncounterManager");
            go.AddComponent<CombatTickScheduler>();
            go.AddComponent<CombatEncounterManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (tickScheduler == null)
            {
                tickScheduler = GetComponent<CombatTickScheduler>();
            }

            if (tickScheduler == null)
            {
                tickScheduler = gameObject.AddComponent<CombatTickScheduler>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool IsUnitInEncounter(Unit unit)
        {
            return unit != null && encounterIdByUnit.ContainsKey(unit);
        }

        public bool TryGetEncounterOpponent(Unit unit, out Unit opponent)
        {
            opponent = null;

            if (unit == null)
            {
                return false;
            }

            if (!encounterIdByUnit.TryGetValue(unit, out int encounterId))
            {
                return false;
            }

            if (!encountersById.TryGetValue(encounterId, out EncounterState state) || state == null)
            {
                return false;
            }

            if (state.UnitA == unit)
            {
                opponent = state.UnitB;
            }
            else if (state.UnitB == unit)
            {
                opponent = state.UnitA;
            }

            if (opponent == null || !opponent.IsAlive)
            {
                opponent = null;
                return false;
            }

            return true;
        }

        public bool TryDisengageUnit(Unit unit, string reason = "manual-disengage")
        {
            if (unit == null)
            {
                return false;
            }

            if (!encounterIdByUnit.TryGetValue(unit, out int encounterId))
            {
                return false;
            }

            string resolvedReason = string.IsNullOrWhiteSpace(reason) ? "manual-disengage" : reason;
            EndEncounter(encounterId, resolvedReason);
            return true;
        }

        public bool TryStartEncounter(Unit initiator, Unit target)
        {
            return TryStartEncounter(initiator, target, out _);
        }

        public bool TryStartEncounter(Unit initiator, Unit target, out int encounterId)
        {
            encounterId = 0;

            if (!CanStartEncounter(initiator, target))
            {
                return false;
            }

            bool initiatorInEncounter = encounterIdByUnit.TryGetValue(initiator, out int initiatorEncounterId);
            bool targetInEncounter = encounterIdByUnit.TryGetValue(target, out int targetEncounterId);

            // If initiator is already in an encounter, only allow it if both are already in the same one.
            if (initiatorInEncounter)
            {
                if (targetInEncounter && initiatorEncounterId == targetEncounterId)
                {
                    encounterId = initiatorEncounterId;
                    return true;
                }

                return false;
            }

            if (singleEncounterMode && encountersById.Count > 0)
            {
                return false;
            }

            int id = nextEncounterId++;
            Unit openingAttacker = ResolveOpeningAttacker(initiator, target);

            EncounterState state = new EncounterState
            {
                EncounterId = id,
                UnitA = initiator,
                UnitB = target,
                CurrentAttacker = openingAttacker
            };

            encountersById[id] = state;
            // Important: allow many attackers to engage the same defender by only reserving the
            // encounter slot for the initiator. Defenders may participate in multiple encounters.
            encounterIdByUnit[initiator] = id;

            tickScheduler?.RegisterEncounter(id);

            if (pauseNavigationDuringEncounter)
            {
                initiator.Controller?.Stop();
                target.Controller?.Stop();
            }

            EventSystem.PublishGlobal(new CombatEncounterStartedEvent
            {
                EncounterId = id,
                Initiator = initiator,
                Defender = target,
                Position = (initiator.transform.position + target.transform.position) * 0.5f
            });

            encounterId = id;
            return true;
        }

        private void Update()
        {
            if (encountersById.Count == 0 || tickScheduler == null)
            {
                return;
            }

            encounterIdsBuffer.Clear();
            foreach (KeyValuePair<int, EncounterState> pair in encountersById)
            {
                encounterIdsBuffer.Add(pair.Key);
            }

            for (int i = 0; i < encounterIdsBuffer.Count; i++)
            {
                int encounterId = encounterIdsBuffer[i];
                if (!encountersById.TryGetValue(encounterId, out EncounterState state))
                {
                    continue;
                }

                if (!ValidateEncounter(state, out string reason))
                {
                    EndEncounter(encounterId, reason);
                    continue;
                }

                if (state.HasPendingAttack)
                {
                    if (alignAttackerToDefenderBeforeAttack && smoothFacingDuringWindup)
                    {
                        AlignAttackerToDefender(state.PendingAttacker, state.PendingDefender, instant: false);
                    }

                    if (Time.time >= state.PendingResolveAt)
                    {
                        ResolvePendingAttack(state);
                    }

                    continue;
                }

                if (!tickScheduler.ShouldTick(encounterId, Time.deltaTime))
                {
                    continue;
                }

                ExecuteTick(state);
            }
        }

        private bool CanStartEncounter(Unit initiator, Unit target)
        {
            if (initiator == null || target == null || initiator == target)
            {
                return false;
            }

            if (!initiator.IsAlive || !target.IsAlive)
            {
                return false;
            }

            if (!UnitFactionUtility.AreHostile(initiator.Faction, target.Faction))
            {
                return false;
            }

            float engageRangeSqr = EngageRange * EngageRange;
            float distanceSqr = (initiator.transform.position - target.transform.position).sqrMagnitude;
            return distanceSqr <= engageRangeSqr;
        }

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

            float maxDistanceSqr = DisengageRange * DisengageRange;
            float distanceSqr = (state.UnitA.transform.position - state.UnitB.transform.position).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr)
            {
                reason = "distance";
                return false;
            }

            return true;
        }

        private void ExecuteTick(EncounterState state)
        {
            Unit attacker = state.CurrentAttacker;
            Unit defender = attacker == state.UnitA ? state.UnitB : state.UnitA;

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
            {
                AlignAttackerToDefender(attacker, defender, instant: !smoothFacingDuringWindup);
            }

            CombatResult result = ResolveAttack(attacker, defender);

            state.HasPendingAttack = true;
            state.PendingAttacker = attacker;
            state.PendingDefender = defender;
            state.PendingResult = result;
            float windupSeconds = Mathf.Max(0f, attackWindupSeconds);
            state.PendingResolveAt = Time.time + windupSeconds;

            EventSystem.PublishGlobal(new CombatAttackWindupEvent
            {
                EncounterId = state.EncounterId,
                Attacker = attacker,
                Defender = defender,
                WindupSeconds = windupSeconds,
                HitChance01 = result.HitChance01
            });

            if (state.PendingResolveAt <= Time.time)
            {
                ResolvePendingAttack(state);
            }
        }

        private void ResolvePendingAttack(EncounterState state)
        {
            if (state == null || !state.HasPendingAttack)
            {
                return;
            }

            Unit attacker = state.PendingAttacker;
            Unit defender = state.PendingDefender;
            CombatResult result = state.PendingResult;

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
            {
                AlignAttackerToDefender(attacker, defender, instant: !smoothFacingDuringWindup);
            }

            bool blockedByPositioning = false;

            if (requireFacingToLandHit && !IsAttackerFacingDefender(attacker, defender))
            {
                result = CreateBlockedHitResult();
                blockedByPositioning = true;
            }

            if (requireMeleeRangeToLandHit && !IsAttackerWithinMeleeHitRange(attacker, defender))
            {
                result = CreateBlockedHitResult();
                blockedByPositioning = true;
            }

            CombatAttackStyle attackStyle = CombatAttackStyle.Unknown;
            CombatReactionArea preferredReactionArea = CombatReactionArea.Chest;
            if (CombatAttackPresentationRegistry.TryConsumeSelection(
                state.EncounterId,
                attacker,
                defender,
                out CombatAttackStyle selectedAttackStyle,
                out CombatReactionArea selectedReactionArea))
            {
                attackStyle = selectedAttackStyle;
                preferredReactionArea = selectedReactionArea;
            }

            UnitStats attackerStats = attacker.Stats != null ? attacker.Stats : attacker.GetComponent<UnitStats>();
            UnitInventory attackerInventory = attacker.Inventory != null ? attacker.Inventory : attacker.GetComponent<UnitInventory>();
            float attackerStunChance01 = attackerStats != null
                ? Mathf.Clamp01(attackerStats.GetMeleeKnockbackChance() + attackerStats.GetStrengthKnockbackChanceBonus())
                : 0f;

            if (result.DidHit && defender.Health != null)
            {
                CombatAttackPresentationRegistry.RegisterIncomingReactionHint(defender, preferredReactionArea);
                DamageSystem.ApplyDamage(defender.Health, result.Damage, DamageType.Melee, attacker.gameObject);

                if (attackerStats != null && attackerInventory != null && attackerStats.IsHeavyCarry(attackerInventory.CarryRatio))
                {
                    attackerStats.RecordWeightedCombatHit(armed: false);
                }

                // Knockback: rolled from Melee + Strength chance.
                if (attackerStunChance01 > 0f)
                {
                    if (Random.value < attackerStunChance01)
                    {
                        Rigidbody defenderRb = defender.GetComponent<Rigidbody>();
                        if (defenderRb != null && !defenderRb.isKinematic)
                        {
                            Vector3 dir = (defender.transform.position - attacker.transform.position);
                            dir.y = 0f;
                            if (dir.sqrMagnitude > 0.0001f)
                            {
                                defenderRb.AddForce(dir.normalized * knockbackImpulseForce, ForceMode.Impulse);
                            }
                        }
                    }
                }
            }

            EventSystem.PublishGlobal(new CombatTickResolvedEvent
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
            WeaponSystem defenderWeapons = defender.GetComponent<WeaponSystem>();
            bool defenderIsUnarmedSurvivor = defender.Faction == UnitFaction.Survivor
                && (defenderWeapons == null || defenderWeapons.EquippedWeapon == null);
            if (defenderIsUnarmedSurvivor &&
                (IsSurvivorAlreadyAttackingElsewhere(defender, state.EncounterId)
                 || (ShouldHoldSurvivorAttackOnCurrentDefender(defender)
                     && !IsPreferredFocusOpponent(defender, attacker))))
            {
                state.CurrentAttacker = attacker;
            }
            else
            {
                state.CurrentAttacker = defender;
            }
        }

        private bool ShouldHoldSurvivorAttackOnCurrentDefender(Unit survivor)
        {
            if (!lockUnarmedSurvivorCounterattacksToFocusTarget || survivor == null)
            {
                return false;
            }

            return survivor.Role == UnitRole.Player && survivor.Faction == UnitFaction.Survivor;
        }

        private bool IsPreferredFocusOpponent(Unit survivor, Unit candidateOpponent)
        {
            if (survivor == null || candidateOpponent == null || !candidateOpponent.IsAlive)
            {
                return false;
            }

            Vector3 survivorForward = survivor.transform.forward;
            survivorForward.y = 0f;
            if (survivorForward.sqrMagnitude <= 0.0001f)
            {
                survivorForward = Vector3.forward;
            }
            else
            {
                survivorForward.Normalize();
            }

            float focusDotThreshold = Mathf.Cos(
                Mathf.Clamp(survivorCounterattackFocusAngleDegrees, 0f, 180f) * Mathf.Deg2Rad);

            Unit bestOpponent = null;
            float bestDot = -1f;
            float bestDistanceSqr = float.MaxValue;

            foreach (KeyValuePair<int, EncounterState> pair in encountersById)
            {
                EncounterState state = pair.Value;
                if (state == null || state.UnitA == null || state.UnitB == null)
                {
                    continue;
                }

                Unit opponent = null;
                if (state.UnitA == survivor)
                {
                    opponent = state.UnitB;
                }
                else if (state.UnitB == survivor)
                {
                    opponent = state.UnitA;
                }

                if (opponent == null || !opponent.IsAlive)
                {
                    continue;
                }

                Vector3 toOpponent = opponent.transform.position - survivor.transform.position;
                toOpponent.y = 0f;

                float dot = 1f;
                float distanceSqr = toOpponent.sqrMagnitude;
                if (distanceSqr > 0.0001f)
                {
                    dot = Vector3.Dot(survivorForward, toOpponent.normalized);
                }

                if (dot < focusDotThreshold)
                {
                    continue;
                }

                bool isBetter = dot > bestDot + 0.001f
                    || (Mathf.Abs(dot - bestDot) <= 0.001f && distanceSqr < bestDistanceSqr);

                if (!isBetter)
                {
                    continue;
                }

                bestDot = dot;
                bestDistanceSqr = distanceSqr;
                bestOpponent = opponent;
            }

            return bestOpponent != null && bestOpponent == candidateOpponent;
        }

        private bool IsSurvivorAlreadyAttackingElsewhere(Unit survivor, int exceptEncounterId)
        {
            foreach (KeyValuePair<int, EncounterState> pair in encountersById)
            {
                if (pair.Key == exceptEncounterId) continue;
                if (pair.Value.CurrentAttacker == survivor) return true;
            }
            return false;
        }

        private CombatResult ResolveAttack(Unit attacker, Unit defender)
        {
            UnitStats attackerStats = null;
            if (attacker != null)
            {
                attackerStats = attacker.Stats != null ? attacker.Stats : attacker.GetComponent<UnitStats>();
            }

            UnitStats defenderStats = null;
            if (defender != null)
            {
                defenderStats = defender.Stats != null ? defender.Stats : defender.GetComponent<UnitStats>();
            }

            int accuracy = attackerStats != null
                ? Mathf.RoundToInt(attackerStats.Melee * 0.4f
                    + attackerStats.Shooting * 0.3f
                    + attackerStats.Strength * 0.3f)
                : 50;

            int evasion = defenderStats != null
                ? Mathf.RoundToInt(defenderStats.Strength * 0.5f + defenderStats.Agility * 0.5f)
                : 35;

            float damage = baseDamage;
            if (attackerStats != null)
            {
                damage += attackerStats.Melee * 0.2f;
                damage = attackerStats.ApplyMeleeDamageScaling(damage);
            }

            CombatResult result = CombatResolver.ResolveAttack(
                accuracy,
                evasion,
                damage,
                hitBias01,
                criticalChance01,
                criticalMultiplier);

            // Agility dodge roll — applied after the base hit-chance calculation.
            if (result.DidHit && defenderStats != null)
            {
                float dodgeChance = defenderStats.GetAgilityDodgeChance();
                if (dodgeChance > 0f && UnityEngine.Random.value < dodgeChance)
                {
                    result = CreateBlockedHitResult();
                }
            }

            return result;
        }

        private void AlignAttackerToDefender(Unit attacker, Unit defender, bool instant)
        {
            if (attacker == null || defender == null)
            {
                return;
            }

            Vector3 toDefender = defender.transform.position - attacker.transform.position;
            toDefender.y = 0f;
            if (toDefender.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(toDefender.normalized, Vector3.up);
            if (instant)
            {
                attacker.transform.rotation = targetRotation;
                return;
            }

            float turnSpeed = Mathf.Max(0f, facingTurnSpeedDegreesPerSecond);
            if (turnSpeed <= 0f)
            {
                attacker.transform.rotation = targetRotation;
                return;
            }

            attacker.transform.rotation = Quaternion.RotateTowards(attacker.transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        private bool IsAttackerFacingDefender(Unit attacker, Unit defender)
        {
            if (attacker == null || defender == null)
            {
                return false;
            }

            Vector3 toDefender = defender.transform.position - attacker.transform.position;
            toDefender.y = 0f;

            if (toDefender.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            Vector3 forward = attacker.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            float requiredDot = Mathf.Cos(Mathf.Clamp(requiredFacingAngleDegrees, 0f, 180f) * Mathf.Deg2Rad);
            return Vector3.Dot(forward, toDefender.normalized) >= requiredDot;
        }

        private bool IsAttackerWithinMeleeHitRange(Unit attacker, Unit defender)
        {
            if (attacker == null || defender == null)
            {
                return false;
            }

            Vector3 toDefender = defender.transform.position - attacker.transform.position;
            toDefender.y = 0f;
            float hitRange = Mathf.Max(0.1f, requiredMeleeHitRange);
            return toDefender.sqrMagnitude <= hitRange * hitRange;
        }

        private static CombatResult CreateBlockedHitResult()
        {
            return new CombatResult(
                didHit: false,
                isCritical: false,
                damage: 0f,
                hitChance01: 0f,
                hitRoll01: 1f,
                criticalRoll01: 1f);
        }

        private Unit ResolveOpeningAttacker(Unit initiator, Unit target)
        {
            int initiatorInitiative = ResolveInitiative(initiator);
            int targetInitiative = ResolveInitiative(target);

            if (initiatorInitiative > targetInitiative)
            {
                return initiator;
            }

            if (targetInitiative > initiatorInitiative)
            {
                return target;
            }

            return Random.value >= 0.5f ? initiator : target;
        }

        private static int ResolveInitiative(Unit unit)
        {
            if (unit == null || unit.Stats == null)
            {
                return 50;
            }

            UnitStats stats = unit.Stats;
            return Mathf.RoundToInt(stats.Melee * 0.4f + stats.Shooting * 0.35f + stats.Strength * 0.25f);
        }

        private void EndEncounter(int encounterId, string reason, Unit winner = null, Unit loser = null)
        {
            if (!encountersById.TryGetValue(encounterId, out EncounterState state))
            {
                return;
            }

            encountersById.Remove(encounterId);

            if (state.UnitA != null && encounterIdByUnit.TryGetValue(state.UnitA, out int idA) && idA == encounterId)
            {
                encounterIdByUnit.Remove(state.UnitA);
            }

            if (state.UnitB != null && encounterIdByUnit.TryGetValue(state.UnitB, out int idB) && idB == encounterId)
            {
                encounterIdByUnit.Remove(state.UnitB);
            }

            tickScheduler?.UnregisterEncounter(encounterId);
            CombatAttackPresentationRegistry.ClearEncounter(encounterId);

            Unit resolvedWinner = winner;
            Unit resolvedLoser = loser;

            if (resolvedWinner == null || resolvedLoser == null)
            {
                bool aAlive = state.UnitA != null && state.UnitA.IsAlive;
                bool bAlive = state.UnitB != null && state.UnitB.IsAlive;

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

            EventSystem.PublishGlobal(new CombatEncounterEndedEvent
            {
                EncounterId = encounterId,
                Winner = resolvedWinner,
                Loser = resolvedLoser,
                Reason = reason
            });
        }
    }
}