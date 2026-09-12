#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Zombera.AI;
using Zombera.Core;
using Zombera.Data;
using Zombera.Debugging;
using Zombera.Inventory;
using Zombera.Systems;
using Zombera.UI;
using Zombera.World;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.Characters
{
    public sealed partial class PlayerSpawner : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnValidate()
        {
            startupSquadTotalCount = Mathf.Max(1, startupSquadTotalCount);
            startupInitialCharacterCount = Mathf.Max(1, startupInitialCharacterCount);
            minimumStartupSquadTotalCount = Mathf.Max(1, minimumStartupSquadTotalCount);
            startupSquadRingRadius = Mathf.Max(0.25f, startupSquadRingRadius);
            ClampStartupSquadSkillTierValues();

            if (!autoPopulateDevModeSpawnItemsInEditor) return;

            var discoveredItems = new List<ItemDefinition>();
            var itemGuids = AssetDatabase.FindAssets("t:ItemDefinition");

            foreach (var itemGuid in itemGuids)
            {
                var itemPath = AssetDatabase.GUIDToAssetPath(itemGuid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);

                if (item == null || discoveredItems.Contains(item)) continue;

                discoveredItems.Add(item);
            }

            if (IsSameItemSet(devModeSpawnInventoryItems, discoveredItems)) return;

            devModeSpawnInventoryItems = discoveredItems.ToArray();
            EditorUtility.SetDirty(this);
        }

        private void ClampStartupSquadSkillTierValues()
        {
            if (startupSquadSkillTiers == null || startupSquadSkillTiers.Length == 0)
            {
                startupSquadSkillTiers = (int[])DefaultStartupSquadSkillTiers.Clone();
                return;
            }

            for (var i = 0; i < startupSquadSkillTiers.Length; i++)
                startupSquadSkillTiers[i] = Mathf.Clamp(startupSquadSkillTiers[i], UnitStats.MinSkillLevel,
                    UnitStats.MaxSkillLevel);
        }

        private static bool IsSameItemSet(ItemDefinition[] existingItems, List<ItemDefinition> discoveredItems)
        {
            if (existingItems == null) return discoveredItems == null || discoveredItems.Count == 0;

            if (discoveredItems == null) return existingItems.Length == 0;

            if (existingItems.Length != discoveredItems.Count) return false;

            return existingItems.All(discoveredItems.Contains);
        }
#endif
    }
}
