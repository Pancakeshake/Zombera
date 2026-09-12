using System.Collections.Generic;
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
        //  Save: squad + unit state capture
        // ──────────────────────────────────────────────

        private void CaptureSquadMembersFromRoster(GameSaveData saveData, Unit playerUnit)
        {
            if (SquadManager.Instance != null)
            {
                var roster = SquadManager.Instance.SquadMembers;
                for (var i = 0; i < roster.Count; i++)
                {
                    var member = roster[i];
                    if (member == null) continue;

                    var unit = member.Unit != null ? member.Unit : member.GetComponent<Unit>();
                    if (unit == null || unit == playerUnit) continue;

                    TryCaptureSquadMember(unit, saveData, true);
                }

                return;
            }

            var members = FindObjectsByType<SquadMember>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member == null) continue;

                var unit = member.Unit != null ? member.Unit : member.GetComponent<Unit>();
                if (unit == null || unit == playerUnit) continue;

                TryCaptureSquadMember(unit, saveData, true);
            }
        }

        private void TryCaptureSquadMember(Unit unit, GameSaveData saveData, bool fromSquadRoster)
        {
            if (unit == null || unit.Health == null) return;
            if (!fromSquadRoster && unit.Role == UnitRole.Player) return;

            if (fromSquadRoster && unit.Role == UnitRole.Player)
                unit.SetRole(UnitRole.SquadMember);

            var unitId = unit.UnitId;
            if (!string.IsNullOrWhiteSpace(unitId) && _savedSquadUnitIds.Contains(unitId))
            {
                unit.RegenerateUnitId();
                unitId = unit.UnitId;
                Debug.LogWarning(
                    "[PlayerSaveProvider] Duplicate squad unitId detected on '" + unit.name +
                    "'. Assigned a new runtime id before capture.",
                    unit);
            }

            if (!string.IsNullOrWhiteSpace(unitId) && !_savedSquadUnitIds.Add(unitId)) return;

            var squadData = new SquadMemberSaveData { unitId = unitId };
            CaptureUnitState(unit, squadData);
            saveData.squad.Add(squadData);
        }

        private void CaptureUnitState(Unit unit, PlayerSaveData data)
        {
            data.position = unit.transform.position;
            data.rotation = unit.transform.rotation;
            data.health = unit.Health.CurrentHealth;

            if (unit.Stats != null) data.stats = BuildUnitStatsSaveData(unit.Stats);

            AppearanceProfileService.TryCaptureProfile(unit.gameObject, out var profile);
            data.appearanceProfileJson = CharacterAppearanceProfile.Serialize(profile);

            // Equipment
            if (unit.TryGetComponent(out EquipmentSystem equipmentSystem))
            {
                data.equipment.Clear();
                foreach (var binding in equipmentSystem.EquippedItems)
                {
                    if (binding.item == null) continue;
                    data.equipment.Add(new EquipmentSaveData
                    {
                        slot = (int)binding.slot,
                        itemId = binding.item.itemId,
                        itemInstanceId = string.Empty
                    });
                }
            }

            // Weapon Runtime
            if (unit.TryGetComponent(out WeaponSystem weaponSystem))
            {
                data.weaponRuntime = new WeaponRuntimeSaveData
                {
                    equippedWeaponId = weaponSystem.EquippedWeapon != null ? weaponSystem.EquippedWeapon.weaponId : string.Empty,
                    currentAmmo = weaponSystem.CurrentAmmo
                };
            }
        }

        private void CaptureUnitState(Unit unit, SquadMemberSaveData data)
        {
            data.position = unit.transform.position;
            data.rotation = unit.transform.rotation;
            data.squadRole = (int)unit.Role;
            data.health = unit.Health.CurrentHealth;

            if (unit.Stats != null) data.stats = BuildUnitStatsSaveData(unit.Stats);

            AppearanceProfileService.TryCaptureProfile(unit.gameObject, out var profile);
            data.appearanceProfileJson = CharacterAppearanceProfile.Serialize(profile);

            // Equipment
            if (unit.TryGetComponent(out EquipmentSystem equipmentSystem))
            {
                data.equipment.Clear();
                foreach (var binding in equipmentSystem.EquippedItems)
                {
                    if (binding.item == null) continue;
                    data.equipment.Add(new EquipmentSaveData
                    {
                        slot = (int)binding.slot,
                        itemId = binding.item.itemId
                    });
                }
            }

            // Weapon Runtime
            if (unit.TryGetComponent(out WeaponSystem weaponSystem))
            {
                data.weaponRuntime = new WeaponRuntimeSaveData
                {
                    equippedWeaponId = weaponSystem.EquippedWeapon != null ? weaponSystem.EquippedWeapon.weaponId : string.Empty,
                    currentAmmo = weaponSystem.CurrentAmmo
                };
            }
        }

        private static void CaptureFormationState(GameSaveData saveData)
        {
            if (saveData == null) return;

            var formationController = ResolveFormationController();
            saveData.player.formation = formationController != null
                ? formationController.CaptureSaveData()
                : new FormationSaveData();
        }

        private static void RestoreFormationState(GameSaveData saveData)
        {
            if (saveData?.player?.formation is not { hasData: true }) return;

            var formationController = ResolveFormationController();
            formationController?.ApplySaveData(saveData.player.formation);
        }

        private static FormationController ResolveFormationController()
        {
            return SquadManager.ResolveRuntimeFormationController();
        }
    }
}
