using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;

namespace Zombera.Factions
{
    public sealed class FactionManager : MonoBehaviour
    {
        private static FactionManager _instance;

        private readonly Dictionary<string, FactionDefinition> _definitions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FactionStandingState> _standings = new(StringComparer.Ordinal);
        private readonly HashSet<FactionMember> _members = new();

        public static FactionManager Instance => _instance;
        public static bool HasInstance => _instance != null;

        public string PlayerFactionId => FactionIds.PlayerColony;

        public event Action FactionStateChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            EnsureDefinitionsLoaded();
            EnsureDefaultStandings();
            RefreshMembersFromScene();
        }

        private void RefreshMembersFromScene()
        {
            var members = FindObjectsByType<FactionMember>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < members.Length; i++)
                RegisterMember(members[i]);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public void RegisterMember(FactionMember member)
        {
            if (member == null) return;

            _members.Add(member);
            member.EnsureSeededFromRole(member.GetComponent<Unit>()?.Role ?? UnitRole.Survivor);

            if (!string.Equals(member.FactionId, PlayerFactionId, StringComparison.Ordinal))
                MarkDiscovered(member.FactionId, "Encountered in the world.");
        }

        public void UnregisterMember(FactionMember member)
        {
            if (member == null) return;
            _members.Remove(member);
        }

        public void NotifyMemberFactionChanged(FactionMember member)
        {
            if (member == null) return;
            MarkDiscovered(member.FactionId, "Allegiance changed.");
            NotifyChanged();
        }

        public FactionDefinition GetFaction(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId)) return null;
            return _definitions.TryGetValue(factionId, out var definition) ? definition : null;
        }

        public IReadOnlyCollection<FactionDefinition> GetAllDefinitions() => _definitions.Values;

        public bool TryGetStanding(string factionId, out FactionStandingState standing)
        {
            standing = null;
            if (string.IsNullOrWhiteSpace(factionId)) return false;
            if (string.Equals(factionId, PlayerFactionId, StringComparison.Ordinal)) return false;

            return _standings.TryGetValue(factionId, out standing);
        }

        public FactionStandingState GetOrCreateStanding(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId)
                || string.Equals(factionId, PlayerFactionId, StringComparison.Ordinal))
                return null;

            if (_standings.TryGetValue(factionId, out var existing)) return existing;

            var definition = GetFaction(factionId);
            var state = new FactionStandingState
            {
                factionId = factionId,
                standingValue = definition?.DefaultStandingValue ?? 0,
                diplomacyState = definition?.DefaultAttitudeProfile ?? FactionDiplomacyState.Unknown,
                discovered = definition?.StartsDiscovered ?? false
            };

            _standings[factionId] = state;
            return state;
        }

        public void SetStanding(string factionId, int standingValue, string interactionSummary = null)
        {
            if (string.IsNullOrWhiteSpace(factionId)
                || string.Equals(factionId, PlayerFactionId, StringComparison.Ordinal))
                return;

            var state = GetOrCreateStanding(factionId);
            if (state == null) return;

            var previousStanding = state.standingValue;
            var previousDiplomacy = state.diplomacyState;

            state.standingValue = Mathf.Clamp(standingValue, -100, 100);
            state.diplomacyState = ResolveDiplomacyFromStanding(state.standingValue, state.diplomacyState);

            if (!string.IsNullOrWhiteSpace(interactionSummary))
                state.lastInteractionSummary = interactionSummary;

            MarkDiscovered(factionId, interactionSummary);

            CoreEventBus.Instance?.Publish(new FactionStandingChangedEvent(
                factionId, previousStanding, state.standingValue, previousDiplomacy, state.diplomacyState));

            NotifyChanged();
        }

        public void AdjustStanding(string factionId, int delta, string interactionSummary = null)
        {
            if (!TryGetStanding(factionId, out var state))
                state = GetOrCreateStanding(factionId);

            if (state == null) return;
            SetStanding(factionId, state.standingValue + delta, interactionSummary);
        }

        public void MarkDiscovered(string factionId, string interactionSummary = null)
        {
            if (string.IsNullOrWhiteSpace(factionId)
                || string.Equals(factionId, PlayerFactionId, StringComparison.Ordinal))
                return;

            var state = GetOrCreateStanding(factionId);
            if (state == null || state.discovered) return;

            state.discovered = true;
            if (!string.IsNullOrWhiteSpace(interactionSummary))
                state.lastInteractionSummary = interactionSummary;

            CoreEventBus.Instance?.Publish(new FactionDiscoveredEvent(factionId));
            NotifyChanged();
        }

        public void UpdateLastKnownRegion(string factionId, string regionId)
        {
            if (string.IsNullOrWhiteSpace(factionId) || string.IsNullOrWhiteSpace(regionId)) return;

            var state = GetOrCreateStanding(factionId);
            if (state == null) return;

            if (string.Equals(state.lastKnownRegionId, regionId, StringComparison.Ordinal)) return;

            state.lastKnownRegionId = regionId;
            NotifyChanged();
        }

        public string ResolveFactionId(Unit unit)
        {
            if (unit == null) return FactionIds.PlayerColony;

            var member = unit.GetComponent<FactionMember>();
            if (member != null && !string.IsNullOrWhiteSpace(member.FactionId))
                return member.FactionId;

            return UnitFactionUtility.DefaultFactionIdFromRole(unit.Role);
        }

        public bool IsHostile(Unit source, Unit target)
        {
            if (source == null || target == null || source == target) return false;
            return IsHostile(ResolveFactionId(source), ResolveFactionId(target));
        }

        public bool IsHostile(string sourceFactionId, string targetFactionId)
        {
            if (string.IsNullOrWhiteSpace(sourceFactionId) || string.IsNullOrWhiteSpace(targetFactionId))
                return false;

            if (string.Equals(sourceFactionId, targetFactionId, StringComparison.Ordinal))
                return false;

            if (string.Equals(sourceFactionId, PlayerFactionId, StringComparison.Ordinal))
                return IsHostileToPlayer(targetFactionId);

            if (string.Equals(targetFactionId, PlayerFactionId, StringComparison.Ordinal))
                return IsHostileToPlayer(sourceFactionId);

            var sourceCoarse = UnitFactionUtility.CoarseFactionFromFactionId(sourceFactionId);
            var targetCoarse = UnitFactionUtility.CoarseFactionFromFactionId(targetFactionId);
            return UnitFactionUtility.AreHostile(sourceCoarse, targetCoarse);
        }

        public bool IsHostileToPlayer(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId)
                || string.Equals(factionId, PlayerFactionId, StringComparison.Ordinal))
                return false;

            if (TryGetStanding(factionId, out var standing))
            {
                return standing.diplomacyState is FactionDiplomacyState.Hostile or FactionDiplomacyState.AtWar;
            }

            var definition = GetFaction(factionId);
            if (definition != null)
            {
                return definition.DefaultAttitudeProfile is FactionDiplomacyState.Hostile
                    or FactionDiplomacyState.AtWar;
            }

            return UnitFactionUtility.AreHostile(
                UnitFaction.Survivor,
                UnitFactionUtility.CoarseFactionFromFactionId(factionId));
        }

        public static bool AreUnitsHostile(Unit source, Unit target)
        {
            if (source == null || target == null || source == target) return false;

            if (HasInstance)
                return Instance.IsHostile(source, target);

            return UnitFactionUtility.AreHostile(source.Faction, target.Faction);
        }

        public FactionPanelSummary BuildPanelSummary()
        {
            var summary = new FactionPanelSummary();
            foreach (var pair in _standings)
            {
                var state = pair.Value;
                if (state == null || !state.discovered) continue;

                summary.DiscoveredCount++;
                if (state.diplomacyState is FactionDiplomacyState.Hostile or FactionDiplomacyState.AtWar)
                    summary.HostileCount++;
                if (state.diplomacyState is FactionDiplomacyState.Friendly or FactionDiplomacyState.Allied)
                    summary.AlliedCount++;
            }

            summary.UnknownCount = Mathf.Max(0, _definitions.Count - 1 - summary.DiscoveredCount);
            return summary;
        }

        public List<FactionListEntry> BuildDiscoveredFactionList()
        {
            var entries = new List<FactionListEntry>(_standings.Count);
            foreach (var pair in _standings)
            {
                var state = pair.Value;
                if (state == null || !state.discovered) continue;

                var definition = GetFaction(state.factionId);
                entries.Add(new FactionListEntry
                {
                    FactionId = state.factionId,
                    DisplayName = definition?.DisplayName ?? state.factionId,
                    Category = definition?.Category ?? FactionCategory.Survivor,
                    StandingValue = state.standingValue,
                    DiplomacyState = state.diplomacyState,
                    LastKnownRegionId = state.lastKnownRegionId,
                    LastInteractionSummary = state.lastInteractionSummary,
                    PrimaryColor = definition?.PrimaryColor ?? Color.gray,
                    Description = definition?.Description ?? string.Empty
                });
            }

            entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            return entries;
        }

        public List<FactionStandingState> CaptureStandings()
        {
            var captured = new List<FactionStandingState>(_standings.Count);
            foreach (var pair in _standings)
            {
                if (pair.Value == null) continue;
                captured.Add(pair.Value.Clone());
            }

            return captured;
        }

        public void RestoreStandings(IReadOnlyList<FactionStandingState> standings)
        {
            _standings.Clear();
            EnsureDefaultStandings();

            if (standings == null) return;

            for (var i = 0; i < standings.Count; i++)
            {
                var state = standings[i];
                if (state == null || string.IsNullOrWhiteSpace(state.factionId)) continue;
                _standings[state.factionId] = state.Clone();
            }

            NotifyChanged();
        }

        private void EnsureDefinitionsLoaded()
        {
            _definitions.Clear();

            var registry = FactionDefinitionRegistry.Instance;
            if (registry != null)
            {
                var registryFactions = registry.Factions;
                for (var i = 0; i < registryFactions.Count; i++)
                {
                    var definition = registryFactions[i];
                    if (definition == null || string.IsNullOrWhiteSpace(definition.FactionId)) continue;
                    _definitions[definition.FactionId] = definition;
                }
            }

            if (_definitions.Count > 0) return;

            SeedBuiltInDefinitions();
        }

        private void SeedBuiltInDefinitions()
        {
            RegisterBuiltIn(FactionDefinition.CreateRuntime(
                FactionIds.PlayerColony, "Survivor Colony",
                "Your settlement and squad.",
                FactionCategory.Survivor, FactionDiplomacyState.Allied, 100, true,
                new Color(0.36f, 0.58f, 0.30f)));

            RegisterBuiltIn(FactionDefinition.CreateRuntime(
                FactionIds.ZombieHorde, "Infected Horde",
                "Mindless infected drawn to noise and movement.",
                FactionCategory.Infected, FactionDiplomacyState.AtWar, -100, true,
                new Color(0.45f, 0.20f, 0.18f)));

            RegisterBuiltIn(FactionDefinition.CreateRuntime(
                FactionIds.BanditRaiders, "Bandit Raiders",
                "Hostile scavengers who prey on weak settlements.",
                FactionCategory.Raider, FactionDiplomacyState.Hostile, -60, true,
                new Color(0.55f, 0.28f, 0.16f)));

            RegisterBuiltIn(FactionDefinition.CreateRuntime(
                FactionIds.SurvivorNeutral, "Neutral Survivors",
                "Independent survivors with no fixed allegiance.",
                FactionCategory.Survivor, FactionDiplomacyState.Neutral, 0, false,
                new Color(0.55f, 0.55f, 0.48f)));

            RegisterBuiltIn(FactionDefinition.CreateRuntime(
                FactionIds.SettlementIronhaven, "Ironhaven Traders",
                "A fortified settlement known for cautious trade.",
                FactionCategory.Trader, FactionDiplomacyState.Friendly, 40, true,
                new Color(0.28f, 0.42f, 0.58f)));
        }

        private void RegisterBuiltIn(FactionDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.FactionId)) return;
            _definitions[definition.FactionId] = definition;
        }

        private void EnsureDefaultStandings()
        {
            foreach (var pair in _definitions)
            {
                if (string.Equals(pair.Key, PlayerFactionId, StringComparison.Ordinal)) continue;
                GetOrCreateStanding(pair.Key);
            }
        }

        private static FactionDiplomacyState ResolveDiplomacyFromStanding(
            int standingValue,
            FactionDiplomacyState current)
        {
            if (standingValue >= 75) return FactionDiplomacyState.Allied;
            if (standingValue >= 35) return FactionDiplomacyState.Friendly;
            if (standingValue >= 10) return FactionDiplomacyState.Neutral;
            if (standingValue >= -10) return current == FactionDiplomacyState.Unknown
                ? FactionDiplomacyState.Neutral
                : FactionDiplomacyState.Suspicious;
            if (standingValue >= -50) return FactionDiplomacyState.Hostile;
            return FactionDiplomacyState.AtWar;
        }

        private void NotifyChanged() => FactionStateChanged?.Invoke();
    }

    public struct FactionPanelSummary
    {
        public int DiscoveredCount;
        public int HostileCount;
        public int AlliedCount;
        public int UnknownCount;
    }

    public struct FactionListEntry
    {
        public string FactionId;
        public string DisplayName;
        public FactionCategory Category;
        public int StandingValue;
        public FactionDiplomacyState DiplomacyState;
        public string LastKnownRegionId;
        public string LastInteractionSummary;
        public Color PrimaryColor;
        public string Description;
    }
}
