using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory;

namespace Zombera.Data
{
    /// <summary>
    /// Building blueprint data used by base construction systems.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Building Data", fileName = "BuildingData")]
    public sealed class BuildingData : ScriptableObject
    {
        public string buildingId;
        public string displayName;
        public float requiredWork = 100f;
        public List<MaterialRequirementData> requiredMaterials = new List<MaterialRequirementData>();
        public GameObject completedPrefab;

        // TODO: Add power, upkeep, and upgrade path definitions.
    }

    [Serializable]
    public sealed class MaterialRequirementData
    {
        public ItemDefinition item;
        public int amount = 1;
    }
}