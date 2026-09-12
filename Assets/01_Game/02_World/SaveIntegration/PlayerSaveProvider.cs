using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Inventory;

namespace Zombera.Systems
{
    public sealed partial class PlayerSaveProvider : MonoBehaviour, ISaveProvider
    {
        [SerializeField] private UnitManager unitManager;
        [SerializeField] private ItemSaveRegistry itemSaveRegistry;

        public int Priority => 100; // Load after world but before zombies/loot

        private readonly List<Unit> _unitBuffer = new();
        private readonly HashSet<string> _savedSquadUnitIds = new();

        public void OnSave(GameSaveData saveData)
        {
            if (unitManager == null)
                unitManager = FindFirstObjectByType<UnitManager>();

            if (unitManager == null) return;

            unitManager.RefreshRegistry();
            unitManager.EnsureUniqueUnitIdsForActiveUnits();
            SquadManager.Instance?.RefreshSquadRoster();

            saveData.squad ??= new List<SquadMemberSaveData>();
            saveData.squad.Clear();
            _savedSquadUnitIds.Clear();

            Unit playerUnit = null;
            var units = unitManager.GetAllActiveUnits(_unitBuffer);

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || unit.Health == null) continue;

                switch (unit.Role)
                {
                    case UnitRole.Player:
                        playerUnit = unit;
                        CaptureUnitState(unit, saveData.player);
                        if (unit.Inventory != null) saveData.inventory = BuildInventorySaveData(unit.Inventory);
                        break;
                    case UnitRole.SquadMember or UnitRole.Survivor:
                        TryCaptureSquadMember(unit, saveData, false);
                        break;
                }
            }

            CaptureSquadMembersFromRoster(saveData, playerUnit);
            CaptureFormationState(saveData);

            Debug.Log($"[PlayerSaveProvider] Saved player and {saveData.squad.Count} squad member(s).");
        }

        public void OnLoad(GameSaveData saveData)
        {
            if (unitManager == null)
                unitManager = FindFirstObjectByType<UnitManager>();

            if (itemSaveRegistry == null)
            {
                var registries = Resources.FindObjectsOfTypeAll<ItemSaveRegistry>();
                if (registries.Length > 0) itemSaveRegistry = registries[0];
            }

            RestorePlayerState(saveData);
            RestoreSquadState(saveData);
            RestoreFormationState(saveData);
        }

        private static bool IsRestoringFromSaveSession()
        {
            return GameManager.Instance != null && GameManager.Instance.IsLoadingSession;
        }

        private ItemDefinition GetItemDefinitionById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return null;
            if (itemSaveRegistry != null) return itemSaveRegistry.GetItem(itemId);

            var allItems = Resources.FindObjectsOfTypeAll<ItemDefinition>();
            return allItems.FirstOrDefault(i => i.itemId == itemId);
        }

    }
}