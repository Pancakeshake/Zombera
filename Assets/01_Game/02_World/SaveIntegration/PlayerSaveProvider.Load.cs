using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Core;
using Zombera.Inventory;
using Zombera.UI.Menus.CharacterCreation;

namespace Zombera.Systems
{
    public sealed partial class PlayerSaveProvider
    {
        // ──────────────────────────────────────────────
        //  Load: player + squad state restoration
        // ──────────────────────────────────────────────

        private void RestorePlayerState(GameSaveData saveData)
        {
            if (unitManager == null) return;

            var playerUnit = unitManager.FindFirstUnitByRole(UnitRole.Player);
            if (playerUnit == null) return;

            ApplyUnitState(playerUnit, saveData.player);

            if (playerUnit.Inventory != null && saveData.inventory != null)
            {
                playerUnit.Inventory.ClearInventory();

                if (saveData.inventory.itemInstances != null && saveData.inventory.itemInstances.Count > 0)
                {
                    foreach (var instance in saveData.inventory.itemInstances)
                    {
                        var item = GetItemDefinitionById(instance.itemId);
                        if (item != null) playerUnit.Inventory.AddItem(item, instance.quantity);
                    }
                }
                else
                {
                    for (int i = 0; i < saveData.inventory.itemIds.Count; i++)
                    {
                        var item = GetItemDefinitionById(saveData.inventory.itemIds[i]);
                        if (item != null) playerUnit.Inventory.AddItem(item, saveData.inventory.quantities[i]);
                    }
                }
            }
        }

        private void RestoreSquadState(GameSaveData saveData)
        {
            if (unitManager == null || saveData.squad == null) return;

            EnsureSquadRosterForLoad(saveData);
            PreassignSquadUnitIdsForRestore(saveData);

            var unmatchedSaveData = new List<SquadMemberSaveData>();
            var availableUnits = new List<Unit>();

            var allUnits = unitManager.GetAllActiveUnits(_unitBuffer);
            foreach (var unit in allUnits)
            {
                if (unit != null && (unit.Role == UnitRole.SquadMember || unit.Role == UnitRole.Survivor))
                {
                    availableUnits.Add(unit);
                }
            }

            int matchedCount = 0;
            int fallbackCount = 0;

            // First Pass: ID Matching
            foreach (var memberData in saveData.squad)
            {
                var unit = availableUnits.FirstOrDefault(u => u.UnitId == memberData.unitId);
                if (unit != null)
                {
                    ApplyUnitState(unit, memberData);
                    AssignStableRestoreIdentity(unit, memberData.unitId);
                    availableUnits.Remove(unit);
                    matchedCount++;
                }
                else
                {
                    unmatchedSaveData.Add(memberData);
                }
            }

            var stillUnmatched = new List<SquadMemberSaveData>(unmatchedSaveData);

            // Second Pass: Deterministic Fallback Matching (Proximity + Role)
            foreach (var memberData in unmatchedSaveData)
            {
                if (availableUnits.Count == 0) break;

                Unit nearestUnit = null;
                float minDistance = float.MaxValue;

                var preferredRole = (UnitRole)memberData.squadRole;
                var roleCandidates = new List<Unit>();
                for (var i = 0; i < availableUnits.Count; i++)
                {
                    var candidate = availableUnits[i];
                    if (candidate == null) continue;
                    if (candidate.Role == preferredRole)
                        roleCandidates.Add(candidate);
                }

                var candidates = roleCandidates.Count > 0 ? roleCandidates : availableUnits;

                foreach (var unit in candidates)
                {
                    float dist = Vector3.Distance(unit.transform.position, memberData.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearestUnit = unit;
                    }
                }

                if (nearestUnit != null)
                {
                    ApplyUnitState(nearestUnit, memberData);
                    AssignStableRestoreIdentity(nearestUnit, memberData.unitId);
                    availableUnits.Remove(nearestUnit);
                    stillUnmatched.Remove(memberData);
                    fallbackCount++;
                }
            }

            int finalUnmatchedCount = stillUnmatched.Count;
            Debug.Log($"[PlayerSaveProvider] Squad restore: {matchedCount} by ID, {fallbackCount} by role+proximity fallback. {finalUnmatchedCount} saved members were not restored.");

            if (finalUnmatchedCount > 0)
            {
                for (var i = 0; i < stillUnmatched.Count; i++)
                {
                    var data = stillUnmatched[i];
                    Debug.LogWarning($"[PlayerSaveProvider] Unmatched squad save entry: unitId='{data.unitId}', role={(UnitRole)data.squadRole}, position={data.position}.");
                }
            }
        }

        private void PreassignSquadUnitIdsForRestore(GameSaveData saveData)
        {
            if (unitManager == null || saveData?.squad == null || saveData.squad.Count == 0) return;

            var candidates = new List<Unit>();
            var allUnits = unitManager.GetAllActiveUnits(_unitBuffer);
            for (var i = 0; i < allUnits.Count; i++)
            {
                var unit = allUnits[i];
                if (unit == null) continue;
                if (unit.Role != UnitRole.SquadMember && unit.Role != UnitRole.Survivor) continue;
                candidates.Add(unit);
            }

            candidates.Sort(static (a, b) =>
                string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));

            var assignCount = Mathf.Min(candidates.Count, saveData.squad.Count);
            for (var i = 0; i < assignCount; i++)
            {
                var savedId = saveData.squad[i].unitId;
                if (string.IsNullOrWhiteSpace(savedId)) continue;
                AssignStableRestoreIdentity(candidates[i], savedId);
            }
        }

        private void EnsureSquadRosterForLoad(GameSaveData saveData)
        {
            var requiredMembers = saveData?.squad != null ? saveData.squad.Count : 0;
            if (requiredMembers <= 0) return;

            var spawner = FindFirstObjectByType<PlayerSpawner>();
            if (spawner != null)
            {
                var ensured = spawner.EnsureLoadedSaveSquadMembers(requiredMembers);
                Debug.Log($"[PlayerSaveProvider] Load restore requested squad roster bootstrap. SavedMembers={requiredMembers}, EnsuredMembers={ensured}.");
            }

            unitManager.RefreshRegistry();
            SquadManager.Instance?.RefreshSquadRoster();
        }

        private void ApplyUnitState(Unit unit, PlayerSaveData data)
        {
            ApplyTransformState(unit, data.position, data.rotation);
            if (unit.Stats != null) ApplyUnitStatsSaveData(unit.Stats, data.stats);
            if (unit.Health != null) unit.Health.SetHealth(data.health);

            var appearanceJson = data.appearanceProfileJson;
            var allowMenuAppearanceFallback = unit.Role == UnitRole.Player
                                              && CharacterSelectionState.HasSelection
                                              && !IsRestoringFromSaveSession();
            if (string.IsNullOrWhiteSpace(appearanceJson) && allowMenuAppearanceFallback)
                appearanceJson = CharacterSelectionState.SelectedAppearanceProfileJson;

            if (!string.IsNullOrWhiteSpace(appearanceJson))
            {
                var profile = CharacterAppearanceProfile.Deserialize(appearanceJson);
                AppearanceProfileService.TryApplyProfile(unit.gameObject, profile);
            }

            // Equipment
            if (unit.TryGetComponent(out EquipmentSystem equipmentSystem))
            {
                equipmentSystem.ClearAll();

                foreach (var eq in data.equipment)
                {
                    var item = GetItemDefinitionById(eq.itemId);
                    if (item != null) equipmentSystem.Equip((EquipmentSlot)eq.slot, item);
                }
            }

            ApplyWeaponRuntimeState(unit, data.weaponRuntime);
        }

        private void ApplyUnitState(Unit unit, SquadMemberSaveData data)
        {
            var resolvedRotation = HasValidRotation(data.rotation) ? data.rotation : unit.transform.rotation;
            ApplyTransformState(unit, data.position, resolvedRotation);
            if (unit.Stats != null) ApplyUnitStatsSaveData(unit.Stats, data.stats);
            if (unit.Health != null) unit.Health.SetHealth(data.health);

            if (!string.IsNullOrWhiteSpace(data.appearanceProfileJson))
            {
                var profile = CharacterAppearanceProfile.Deserialize(data.appearanceProfileJson);
                AppearanceProfileService.TryApplyProfile(unit.gameObject, profile);
            }

            // Equipment
            if (unit.TryGetComponent(out EquipmentSystem equipmentSystem))
            {
                equipmentSystem.ClearAll();

                foreach (var eq in data.equipment)
                {
                    var item = GetItemDefinitionById(eq.itemId);
                    if (item != null) equipmentSystem.Equip((EquipmentSlot)eq.slot, item);
                }
            }

            ApplyWeaponRuntimeState(unit, data.weaponRuntime);
        }

        private static void ApplyWeaponRuntimeState(Unit unit, WeaponRuntimeSaveData weaponRuntime)
        {
            if (unit == null || weaponRuntime == null) return;
            if (!unit.TryGetComponent(out WeaponSystem weaponSystem)) return;

            if (!string.IsNullOrEmpty(weaponRuntime.equippedWeaponId) &&
                (weaponSystem.EquippedWeapon == null ||
                 weaponSystem.EquippedWeapon.weaponId != weaponRuntime.equippedWeaponId))
            {
                if (unit.TryGetComponent(out EquipmentSystem eqSys))
                {
                    var matchingWeapon = eqSys.EquippedItems
                        .Select(b => b.item?.equippedWeaponData)
                        .FirstOrDefault(w => w != null && w.weaponId == weaponRuntime.equippedWeaponId);

                    if (matchingWeapon != null) weaponSystem.EquipWeapon(matchingWeapon);
                }
            }

            weaponSystem.RestoreAmmo(weaponRuntime.currentAmmo);
        }

        private static void ApplyTransformState(Unit unit, Vector3 position, Quaternion rotation)
        {
            if (unit == null) return;

            unit.transform.rotation = rotation;
            UnitNavUtils.PlaceUnitOnNavMesh(unit.gameObject, position, 4f, false);
        }

        private static void AssignStableRestoreIdentity(Unit unit, string savedUnitId)
        {
            if (unit == null || string.IsNullOrWhiteSpace(savedUnitId)) return;

            unit.AssignUnitIdForRestore(savedUnitId);

            if (unit.TryGetComponent(out SquadMember squadMember))
                squadMember.AssignMemberIdForRestore(savedUnitId);
        }

        private static bool HasValidRotation(Quaternion rotation)
        {
            var magnitude = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            return magnitude > 0.5f;
        }
    }
}
