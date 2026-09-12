using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    [Serializable]
    public sealed class RegionState
    {
        public WorldEntityId id;
        public string sourceId = "world";
        public string displayName = "World";
        public int generationSeed;
        public Rect boundsXZ;
    }

    [Serializable]
    public sealed class SettlementState
    {
        public WorldEntityId id;
        public string sourceId = string.Empty;
        public WorldEntityId regionId;
        public string displayName = string.Empty;
        public Vector2 centerXZ;
        public Vector2 halfExtentsMeters;
        public float padHeightWorldY;
        public float buildabilityScore;
        public int layoutSeed;
    }

    [Serializable]
    public sealed class DistrictState
    {
        public WorldEntityId id;
        public string sourceId = string.Empty;
        public WorldEntityId settlementId;
        public int sourceAreaId;
        public string displayName = string.Empty;
        public string clusterName = string.Empty;
        public CityDistrictType districtType;
        public int gridX;
        public int gridZ;
        public Rect boundsXZ;
        public Vector2 centerXZ;
        public float groundWorldY;
        public float areaSquareMeters;
        public CityBlockCornerMask roundedCorners;
        public float arterialCornerRadiusMeters;
        public List<Vector2> outlineXZ = new();
    }

    [Serializable]
    public sealed class LotState
    {
        public WorldEntityId id;
        public string sourceId = string.Empty;
        public WorldEntityId districtId;
        public int sourceIndex;
        public Rect boundsXZ;
        public List<Vector2> outlineXZ = new();
        public float groundWorldY;
        public BlockFace streetFace;
        public CommercialLotKind commercialKind;
        public bool isCornerLot;
        public bool isCurvedLot;
    }

    [Serializable]
    public sealed class PoiState
    {
        public WorldEntityId id;
        public string sourceId = string.Empty;
        public WorldEntityId regionId;
        public string archetypeId = string.Empty;
        public string mapMarkerId = string.Empty;
        public Vector2 positionXZ;
        public float yawDegrees;
        public Vector2 footprintMeters;
        public bool discovered;
        public bool depleted;
    }
}
