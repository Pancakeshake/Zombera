using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    [Serializable]
    public sealed class BuildingState
    {
        public WorldEntityId id;
        public string sourceId = string.Empty;
        public WorldEntityId settlementId;
        public WorldEntityId districtId;
        public WorldEntityId lotId;
        public string archetypeId = string.Empty;
        public string typeId = string.Empty;
        public CityDistrictType districtType;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
        public Rect footprintXZ;
        public BlockFace streetFace;
        public bool hasDoorAnchor;
        public Vector3 doorAnchorWorld;
        public float condition01 = 1f;
        public bool abandoned;
        public long abandonedAtHour = -1;
        public BuildingOccupancyState occupancy = new();
        public BuildingOwnershipState ownership = new();
        public BuildingUtilityState utilities = new();
        public BuildingDamageState damage = new();
        public BuildingFireState fire = new();
        public BuildingLootState loot = new();
        public List<BuildingModificationState> modifications = new();
        public List<BuildingModuleState> modules = new();
    }

    [Serializable]
    public sealed class BuildingOccupancyState
    {
        public bool occupied;
        public int capacity;
        public int currentCount;
    }

    [Serializable]
    public sealed class BuildingOwnershipState
    {
        public string ownerTypeId = string.Empty;
        public string ownerId = string.Empty;
    }

    [Serializable]
    public sealed class BuildingUtilityState
    {
        public WorldUtilityStatus power;
        public WorldUtilityStatus water;
    }

    [Serializable]
    public sealed class BuildingDamageState
    {
        public float structuralDamage01;
        public float fireDamage01;
        public bool destroyed;
    }

    [Serializable]
    public sealed class BuildingFireState
    {
        public bool active;
        public long startedAtHour = -1;
        public float intensity01;
    }

    [Serializable]
    public sealed class BuildingLootState
    {
        public bool generated;
        public int generationSeed;
        public bool looted;
        public List<WorldItemStackState> items = new();
    }

    [Serializable]
    public sealed class WorldItemStackState
    {
        public string itemId = string.Empty;
        public int quantity;
    }

    [Serializable]
    public sealed class BuildingModificationState
    {
        public string modificationId = string.Empty;
        public string typeId = string.Empty;
        public string slotId = string.Empty;
        public bool enabled = true;
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
    }

    [Serializable]
    public sealed class BuildingModuleState
    {
        public string moduleId = string.Empty;
        public string typeId = string.Empty;
        public bool enabled = true;
        public float condition01 = 1f;
    }
}
