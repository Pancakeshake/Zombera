#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Factions;

#endregion

namespace Zombera.Combat
{
    /// <summary>
    ///     Authoritative encounter gatekeeper and tactical exchange runner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class CombatEncounterManager : MonoBehaviour
    {
        [Header("Encounter Rules")] [SerializeField]
        private float engageRange = 1.4f;

        [SerializeField] private float disengageRange = 1.75f;
        [SerializeField] private bool singleEncounterMode;
        [SerializeField] private bool pauseNavigationDuringEncounter = true;
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Combat Math")] [SerializeField]
        private float baseDamage = 8f;

        [SerializeField] private float hitBias01;
        [SerializeField] private float criticalChance01 = 0.05f;
        [SerializeField] private float criticalMultiplier = 1.5f;

        [Header("Animation Sync")] [SerializeField] [Min(0f)]
        private float attackWindupSeconds = 0.32f;

        [Header("Facing Validation")] [SerializeField]
        private bool requireFacingToLandHit = true;

        [SerializeField] private bool alignAttackerToDefenderBeforeAttack = true;
        [SerializeField] private bool smoothFacingDuringWindup = true;
        [SerializeField] [Min(0f)] private float facingTurnSpeedDegreesPerSecond = 720f;
        [SerializeField] [Range(0f, 180f)] private float requiredFacingAngleDegrees = 65f;

        [Header("Survivor Attack Focus")]
        [Tooltip(
            "When enabled, unarmed survivor counterattacks are locked to the encounter opponent they are currently facing most directly.")]
        [SerializeField]
        private bool lockUnarmedSurvivorCounterattacksToFocusTarget = true;

        [SerializeField] [Range(0f, 180f)] private float survivorCounterattackFocusAngleDegrees = 80f;

        [Header("Hit Distance Validation")] [SerializeField]
        private bool requireMeleeRangeToLandHit = true;

        [SerializeField] [Min(0.1f)] private float requiredMeleeHitRange = 1.0f;

        [Header("Knockback")] [SerializeField] [Min(0f)]
        private float knockbackImpulseForce = 5f;

        [Header("Services")] [SerializeField] private CombatTickScheduler tickScheduler;

        private readonly Dictionary<Unit, int> _encounterIdByUnit = new();
        private readonly List<int> _encounterIdsBuffer = new(32);

        private readonly Dictionary<int, EncounterState> _encountersById = new();

        private int _nextEncounterId = 1;

        public static CombatEncounterManager Instance { get; private set; }

        public float EngageRange => CombatTuningConfig.EngageRangeOr(Mathf.Max(0.1f, engageRange));
        public float DisengageRange => CombatTuningConfig.DisengageRangeOr(Mathf.Max(EngageRange + 0.1f, disengageRange));

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

            if (tickScheduler == null) tickScheduler = GetComponent<CombatTickScheduler>();

            if (tickScheduler == null) tickScheduler = gameObject.AddComponent<CombatTickScheduler>();
        }

        private void Update()
        {
            if (!IsCombatAllowedForCurrentState()) return;

            if (_encountersById.Count == 0 || tickScheduler == null) return;

            _encounterIdsBuffer.Clear();
            foreach (var pair in _encountersById)
                _encounterIdsBuffer.Add(pair.Key);

            foreach (var encounterId in _encounterIdsBuffer)
                ProcessEncounterTick(encounterId);
        }

        private void ProcessEncounterTick(int encounterId)
        {
            if (!_encountersById.TryGetValue(encounterId, out var state)) return;

            if (!ValidateEncounter(state, out var reason))
            {
                EndEncounter(encounterId, reason);
                return;
            }

            if (!state.HasPendingAttack)
            {
                if (tickScheduler.ShouldTick(encounterId, Time.deltaTime))
                    ExecuteTick(state);

                return;
            }

            if (alignAttackerToDefenderBeforeAttack && smoothFacingDuringWindup)
                AlignAttackerToDefender(state.PendingAttacker, state.PendingDefender, false);

            if (Time.time >= state.PendingResolveAt)
                ResolvePendingAttack(state);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeManager()
        {
            if (Instance != null) return;

            var existing = FindFirstObjectByType<CombatEncounterManager>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            var go = new GameObject("CombatEncounterManager");
            go.AddComponent<CombatTickScheduler>();
            go.AddComponent<CombatEncounterManager>();
        }

        public bool IsUnitInEncounter(Unit unit)
        {
            return unit != null && _encounterIdByUnit.ContainsKey(unit);
        }

        public bool TryGetEncounterOpponent(Unit unit, out Unit opponent)
        {
            opponent = null;

            if (unit == null) return false;

            if (!_encounterIdByUnit.TryGetValue(unit, out var encounterId)) return false;

            if (!_encountersById.TryGetValue(encounterId, out var state) || state == null) return false;

            if (state.UnitA != unit && state.UnitB != unit) return false;

            opponent = state.UnitA == unit ? state.UnitB : state.UnitA;
            if (opponent is { IsAlive: true }) return true;

            opponent = null;
            return false;
        }

        public bool TryDisengageUnit(Unit unit, string reason = "manual-disengage")
        {
            if (unit == null) return false;

            if (!_encounterIdByUnit.TryGetValue(unit, out var encounterId)) return false;

            var resolvedReason = string.IsNullOrWhiteSpace(reason) ? "manual-disengage" : reason;
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

            if (!IsCombatAllowedForCurrentState()) return false;

            if (!CanStartEncounter(initiator, target)) return false;

            var initiatorInEncounter = _encounterIdByUnit.TryGetValue(initiator, out var initiatorEncounterId);
            var targetInEncounter = _encounterIdByUnit.TryGetValue(target, out var targetEncounterId);

            // If initiator is already in an encounter, only allow it if both are already in the same one.
            if (initiatorInEncounter)
            {
                if (!targetInEncounter || initiatorEncounterId != targetEncounterId) return false;

                encounterId = initiatorEncounterId;
                return true;
            }

            if (singleEncounterMode && _encountersById.Count > 0) return false;

            var id = _nextEncounterId++;
            var openingAttacker = ResolveOpeningAttacker(initiator, target);

            var state = new EncounterState
            {
                EncounterId = id,
                UnitA = initiator,
                UnitB = target,
                CurrentAttacker = openingAttacker
            };

            _encountersById[id] = state;
            // Important: allow many attackers to engage the same defender by only reserving the
            // encounter slot for the initiator. Defenders may participate in multiple encounters.
            _encounterIdByUnit[initiator] = id;

            tickScheduler?.RegisterEncounter(id);

            if (pauseNavigationDuringEncounter)
            {
                initiator.Controller?.Stop();
                target.Controller?.Stop();
            }

            CoreEventBus.PublishGlobal(new CombatEncounterStartedEvent
            {
                EncounterId = id,
                Initiator = initiator,
                Defender = target,
                Position = (initiator.transform.position + target.transform.position) * 0.5f
            });

            encounterId = id;
            return true;
        }

        private bool CanStartEncounter(Unit initiator, Unit target)
        {
            if (initiator == null || target == null || initiator == target) return false;

            if (!initiator.IsAlive || !target.IsAlive) return false;

            if (!FactionManager.AreUnitsHostile(initiator, target)) return false;

            var engageRangeSqr = EngageRange * EngageRange;
            var distanceSqr = (initiator.transform.position - target.transform.position).sqrMagnitude;
            return distanceSqr <= engageRangeSqr;
        }

        private sealed class EncounterState
        {
            public Unit CurrentAttacker;
            public int EncounterId;
#pragma warning disable S3459 // Fields assigned in CombatEncounterManager.Resolution.cs
            public bool HasPendingAttack;
            public Unit PendingAttacker;
            public Unit PendingDefender;
            public float PendingResolveAt;
#pragma warning restore S3459
            public CombatResult PendingResult;
            public Unit UnitA;
            public Unit UnitB;
        }
    }
}
