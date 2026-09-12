#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;

#endregion

namespace Zombera.UI.SquadManagement
{
    public sealed partial class ZomberaSquadManagementUI
    {
        private SquadManager ResolveSquadManagerForLiveData()
        {
            if (_cachedSquadManager != null && Time.unscaledTime < _squadManagerCacheValidUntil)
                return _cachedSquadManager;

            _cachedSquadManager = SquadManager.Instance != null
                ? SquadManager.Instance
                : FindFirstObjectByType<SquadManager>();
            _squadManagerCacheValidUntil = Time.unscaledTime + LiveDataUnitScanCacheSeconds;
            return _cachedSquadManager;
        }

        private void EnsureAllUnitsCacheFresh()
        {
            if (Time.unscaledTime < _allUnitsCacheValidUntil) return;

            _cachedAllUnits.Clear();
            if (UnitManager.HasInstance)
                UnitManager.Instance.GetAllActiveUnits(_cachedAllUnits);
            else
                _cachedAllUnits.AddRange(FindObjectsByType<Unit>(FindObjectsSortMode.None));

            _allUnitsCacheValidUntil = Time.unscaledTime + LiveDataUnitScanCacheSeconds;
        }

        private bool TrySyncFromLiveData()
        {
            _liveSurvivorContexts.Clear();
            ResolveLiveContexts(_liveSurvivorContexts);

            if (_liveSurvivorContexts.Count == 0) return false;

            var previouslySelectedId = TryGetSelectedSurvivorId();

            var previousIds = new List<string>(_survivors.Count);
            for (var i = 0; i < _survivors.Count; i++) previousIds.Add(_survivors[i].id);

            _survivors.Clear();
            for (var i = 0; i < _liveSurvivorContexts.Count; i++)
                _survivors.Add(BuildSurvivorEntryFromContext(_liveSurvivorContexts[i]));

            var rosterChanged = previousIds.Count != _survivors.Count;
            if (!rosterChanged)
                for (var i = 0; i < _survivors.Count; i++)
                {
                    if (string.Equals(previousIds[i], _survivors[i].id, StringComparison.Ordinal)) continue;

                    rosterChanged = true;
                    break;
                }

            squadListPanel?.SetEntries(_survivors);

            var squadName = BuildSquadNameFromLiveData();
            if (squadNameText != null) squadNameText.text = squadName;

            var names = new List<string>(_survivors.Count);
            for (var i = 0; i < _survivors.Count; i++) names.Add(_survivors[i].displayName);

            squadCustomiserTab?.SetSquadName(squadName);
            if (rosterChanged) squadCustomiserTab?.SetMembers(names);

            if (_survivors.Count > 0)
            {
                var indexToSelect = 0;
                if (!string.IsNullOrWhiteSpace(previouslySelectedId))
                {
                    for (var i = 0; i < _survivors.Count; i++)
                        if (string.Equals(_survivors[i].id, previouslySelectedId, StringComparison.Ordinal))
                        {
                            indexToSelect = i;
                            break;
                        }
                }
                else if (_selectedSurvivorIndex >= 0 && _selectedSurvivorIndex < _survivors.Count)
                {
                    indexToSelect = _selectedSurvivorIndex;
                }

                _selectedSurvivorIndex = Mathf.Clamp(indexToSelect, 0, _survivors.Count - 1);
                squadListPanel?.SelectIndex(_selectedSurvivorIndex);
                HandleSurvivorSelection(_selectedSurvivorIndex, _survivors[_selectedSurvivorIndex]);
            }
            else
            {
                _selectedSurvivorIndex = -1;
            }

            RefreshTopStatus();
            return true;
        }

        private void ResolveLiveContexts(List<LiveSurvivorContext> target)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            if (includePlayerInRoster)
            {
                var player = ResolvePlayerUnit();
                TryAddLiveContext(target, seenIds, player, null);
            }

            var manager = ResolveSquadManagerForLiveData();

            if (manager != null)
            {
                manager.RefreshSquadRoster();

                var members = manager.SquadMembers;
                for (var i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    if (member == null) continue;

                    var memberUnit = member.Unit != null
                        ? member.Unit
                        : member.GetComponent<Unit>();

                    TryAddLiveContext(target, seenIds, memberUnit, member);
                }
            }

            if (target.Count > 0) return;

            EnsureAllUnitsCacheFresh();
            for (var i = 0; i < _cachedAllUnits.Count; i++)
            {
                var unit = _cachedAllUnits[i];
                if (unit == null) continue;

                if (unit.Role != UnitRole.SquadMember &&
                    (!includePlayerInRoster || unit.Role != UnitRole.Player)) continue;

                TryAddLiveContext(target, seenIds, unit, unit.GetComponent<SquadMember>());
            }
        }

        private Unit ResolvePlayerUnit()
        {
            if (_cachedPlayerUnit != null
                && (!_cachedPlayerUnit.IsAlive || !_cachedPlayerUnit.gameObject.activeInHierarchy))
                _cachedPlayerUnit = null;

            if (_cachedPlayerUnit != null && Time.unscaledTime < _playerUnitCacheValidUntil) return _cachedPlayerUnit;

            var unitManager = UnitManager.Instance;
            if (unitManager != null)
            {
                var managed = unitManager.FindFirstUnitByRole(UnitRole.Player);
                if (managed != null)
                {
                    _cachedPlayerUnit = managed;
                    _playerUnitCacheValidUntil = Time.unscaledTime + LiveDataUnitScanCacheSeconds;
                    return managed;
                }
            }

            var spawner = FindFirstObjectByType<PlayerSpawner>();
            if (spawner != null && spawner.SpawnedPlayer != null)
            {
                _cachedPlayerUnit = spawner.SpawnedPlayer;
                _playerUnitCacheValidUntil = Time.unscaledTime + LiveDataUnitScanCacheSeconds;
                return spawner.SpawnedPlayer;
            }

            EnsureAllUnitsCacheFresh();
            for (var i = 0; i < _cachedAllUnits.Count; i++)
                if (_cachedAllUnits[i] != null && _cachedAllUnits[i].Role == UnitRole.Player)
                {
                    _cachedPlayerUnit = _cachedAllUnits[i];
                    _playerUnitCacheValidUntil = Time.unscaledTime + LiveDataUnitScanCacheSeconds;
                    return _cachedPlayerUnit;
                }

            _cachedPlayerUnit = null;
            _playerUnitCacheValidUntil = Time.unscaledTime + LiveDataUnitScanCacheSeconds;
            return null;
        }

        private static void TryAddLiveContext(
            List<LiveSurvivorContext> target,
            HashSet<string> seenIds,
            Unit unit,
            SquadMember member)
        {
            if (unit == null) return;

            var memberId = member != null ? member.MemberId : null;

            string id;
            if (!string.IsNullOrWhiteSpace(unit.UnitId))
                id = unit.UnitId;
            else if (!string.IsNullOrWhiteSpace(memberId))
                id = memberId;
            else
                id = unit.gameObject.GetInstanceID().ToString();

            if (!seenIds.Add(id)) return;

            target.Add(new LiveSurvivorContext
            {
                Id = id,
                Unit = unit,
                SurvivorController = unit.GetComponent<SurvivorController>()
            });
        }

        private static SquadListPanelController.SurvivorEntryData BuildSurvivorEntryFromContext(
            LiveSurvivorContext context)
        {
            var unit = context != null ? context.Unit : null;
            var health = unit != null ? unit.Health : null;
            var stats = unit != null ? unit.Stats : null;

            var health01 = 1f;
            if (health != null && health.MaxHealth > 0f)
                health01 = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);

            var stamina01 = stats != null
                ? Mathf.Clamp01(stats.Stamina / 100f)
                : 1f;

            return new SquadListPanelController.SurvivorEntryData(
                context != null ? context.Id : string.Empty,
                ResolveDisplayName(unit),
                ResolvePortrait(unit),
                health01,
                stamina01,
                ResolveCondition(health01));
        }

        private static string ResolveDisplayName(Unit unit)
        {
            if (unit == null || unit.gameObject == null) return "Unknown";

            if (unit.Role == UnitRole.Player && CharacterSelectionState.HasSelection &&
                !string.IsNullOrWhiteSpace(CharacterSelectionState.SelectedCharacterName))
                return CharacterSelectionState.SelectedCharacterName.Trim();

            var name = unit.gameObject.name;
            return string.IsNullOrWhiteSpace(name) ? "Survivor" : name.Trim();
        }

        private static Sprite ResolvePortrait(Unit unit)
        {
            if (unit == null) return null;

            if (unit.Role == UnitRole.Player && CharacterSelectionState.SelectedPortraitSprite != null)
                return CharacterSelectionState.SelectedPortraitSprite;

            var spriteRenderer = unit.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) return spriteRenderer.sprite;

            return null;
        }

        private static SquadListPanelController.SurvivorCondition ResolveCondition(float health01)
        {
            return health01 switch
            {
                > 0.72f => SquadListPanelController.SurvivorCondition.Stable,
                > 0.45f => SquadListPanelController.SurvivorCondition.Wounded,
                > 0.25f => SquadListPanelController.SurvivorCondition.Exhausted,
                _ => SquadListPanelController.SurvivorCondition.Critical
            };
        }

        private string BuildSquadNameFromLiveData()
        {
            if (CharacterSelectionState.HasSelection &&
                !string.IsNullOrWhiteSpace(CharacterSelectionState.SelectedCharacterName))
                return CharacterSelectionState.SelectedCharacterName + "'s Squad";

            return "Squad " + Mathf.Max(1, _liveSurvivorContexts.Count);
        }

        private string TryGetSelectedSurvivorId()
        {
            if (_selectedSurvivorIndex >= 0 && _selectedSurvivorIndex < _liveSurvivorContexts.Count)
                return _liveSurvivorContexts[_selectedSurvivorIndex].Id;

            if (_selectedSurvivorIndex >= 0 && _selectedSurvivorIndex < _survivors.Count)
                return _survivors[_selectedSurvivorIndex].id;

            return string.Empty;
        }
    }
}
