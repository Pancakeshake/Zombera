#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory;

#endregion

namespace Zombera.Data
{
    /// <summary>
    ///     Building blueprint data used by base construction systems.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Building Data", fileName = "BuildingData")]
    public sealed class BuildingData : ScriptableObject
    {
        public string buildingId;
        public string displayName;
        public float requiredWork = 100f;
        public List<MaterialRequirementData> requiredMaterials = new();
        public GameObject completedPrefab;

        [Header("Upkeep & Power")] [Tooltip("Power draw in watts while the building is active. 0 = passive.")] [Min(0f)]
        public float powerConsumptionWatts;

        [Tooltip("Resource units consumed per in-game day to maintain this structure.")] [Min(0f)]
        public float upkeepCostPerDay;

        [Header("Upgrade Paths")] [Tooltip("Buildings this structure can be upgraded into.")]
        public List<BuildingData> upgradePaths = new();
    }

    [Serializable]
    public sealed class MaterialRequirementData
    {
        public ItemDefinition item;
        public int amount = 1;
    }
}