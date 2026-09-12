using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Per-district zone geometry definition for lot terrain subdivision.
    ///     Controls the size, placement, and surface type of each sub-zone
    ///     (front yard, backyard, driveway, building pad, etc.) within a lot.
    /// </summary>
    /// <summary>
    ///     Attribute that tells the custom property drawer to show a layer-name
    ///     dropdown sourced from the parent DistrictLotTerrainLayout's textureArrayConfig.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TextureLayerSelectorAttribute : PropertyAttribute { }

    [Serializable]
    public sealed class LotTerrainZoneLayout
    {
        [Header("Front Yard")]
        [Tooltip("Front yard depth as a fraction of lot depth (0–0.5). Always scales with the lot — no overflow possible.")]
        [Range(0f, 0.5f)]
        public float frontYardFractionOfLotDepth;

        [Tooltip("Terrain layer for the front yard. Dropdown lists layers from the TextureArrayConfig.")]
        [TextureLayerSelector]
        public int frontYardTextureIndex;

        [Header("Backyard")]
        [Tooltip("Terrain layer for the backyard.")]
        [TextureLayerSelector]
        public int backyardTextureIndex;

        [Header("Side Yards")]
        [Tooltip("Side yard width as a fraction of lot width (0–0.25). Always scales with the lot — no overflow possible.")]
        [Range(0f, 0.25f)]
        public float sideYardFractionOfLotWidth;

        [Tooltip("Terrain layer for both side-yard strips.")]
        [TextureLayerSelector]
        public int sideYardTextureIndex;

        [Header("Driveway")]
        [Tooltip("When enabled, generates a driveway strip from the street edge into the lot.")]
        public bool generateDriveway = true;

        [Tooltip("Width of the driveway strip in metres (3 m = single, 5 m = double).")]
        [Min(2f)]
        public float drivewayWidthMeters = 3f;

        [Tooltip("Driveway depth as a fraction of lot depth (0–0.8). Default 0.25 = one-quarter of lot depth.")]
        [Range(0f, 0.8f)]
        public float drivewayDepthFractionOfLotDepth = 0.25f;

        [Tooltip("Lateral offset of the driveway from the nearest side edge, in metres.")]
        public float drivewaySideOffsetMeters = 1.5f;

        [Tooltip("Terrain layer for the driveway.")]
        [TextureLayerSelector]
        public int drivewayTextureIndex;

        [Header("Building Pad")]
        [Tooltip("When enabled, a cleared/gravel zone is placed where the building will sit.")]
        public bool generateBuildingPad = true;

        [Tooltip("Terrain layer for the building pad.")]
        [TextureLayerSelector]
        public int buildingPadTextureIndex;

        [Header("Footpath")]
        [Tooltip("When enabled, generates a narrow footpath from the street to the building pad.")]
        public bool generateDoorFootpath = true;

        [Tooltip("Width of the footpath in metres.")]
        [Min(0.8f)]
        public float footpathWidthMeters = 1.2f;

        [Tooltip("Terrain layer for the door footpath.")]
        [TextureLayerSelector]
        public int footpathTextureIndex;

        [Header("Commercial Paint")]
        [Tooltip("White paint layer for parking stall stripes (Microsplat slot 16). -1 disables.")]
        [TextureLayerSelector]
        public int paintStripeTextureIndex = 16;

        [Header("Yard Variation")]
        [Tooltip("When enabled, grass yard zones pick one of several grass layers per lot, so neighbouring yards look distinct.")]
        public bool varyYardTexturePerLot;

        /// <summary>
        ///     Grass layers a yard can vary between (Microsplat_World indices:
        ///     GrassGreen, GrassYellow, sparse_grass). Only zones already painted
        ///     with a grass layer are remapped — concrete/asphalt zones untouched.
        /// </summary>
        private static readonly int[] YardGrassPool = { 0, 1, 4 };

        /// <summary>
        ///     Re-colours this lot's yard zones to one deterministic grass layer
        ///     picked from the lot position, making neighbouring lots' yards
        ///     visually distinct. No-op when <see cref="varyYardTexturePerLot"/>
        ///     is off or a yard zone isn't grass.
        /// </summary>
        public void ApplyYardTextureVariation(List<LotSubZone> zones, Vector3 lotPosition)
        {
            if (!varyYardTexturePerLot || zones == null || zones.Count == 0)
                return;

            var pick = YardGrassPool[Mathf.Abs(lotPosition.GetHashCode()) % YardGrassPool.Length];

            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (!IsYardZoneName(zone.DisplayName))
                    continue;
                if (!IsGrassLayer(zone.TextureLayerIndex))
                    continue;
                zone.TextureLayerIndex = pick;
                zones[i] = zone;
            }
        }

        /// <summary>
        ///     Inserts a full-lot grass underlay using this lot's yard pick so any
        ///     gaps behind / beside the house (uncapped rear, side-yard leftovers)
        ///     match the front yard instead of showing the district base green.
        ///     Call after <see cref="ApplyYardTextureVariation"/>.
        /// </summary>
        public void EnsureLotYardUnderlay(List<LotSubZone> zones, Rect lotRect)
        {
            if (zones == null || lotRect.width < 2f || lotRect.height < 2f)
                return;

            var yardLayer = -1;
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (!IsYardZoneName(zone.DisplayName))
                    continue;
                if (!IsGrassLayer(zone.TextureLayerIndex))
                    continue;
                yardLayer = zone.TextureLayerIndex;
                break;
            }

            if (yardLayer < 0)
            {
                if (!varyYardTexturePerLot || !IsGrassLayer(frontYardTextureIndex))
                    return;
                yardLayer = frontYardTextureIndex;
            }

            zones.Insert(0, new LotSubZone
            {
                TextureLayerIndex = yardLayer,
                Bounds = lotRect,
                HeightOffset = 0f,
                DisplayName = "LotYardFill",
                Sharp = false,
                ClippedOutline = null
            });
        }

        private static bool IsYardZoneName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return false;
            return displayName.IndexOf("Yard", StringComparison.Ordinal) >= 0
                || displayName == "LotYardFill";
        }

        private static bool IsGrassLayer(int layerIndex) =>
            layerIndex == 0 || layerIndex == 1 || layerIndex == 4;
    }

    /// <summary>
    ///     Per-district terrain subdivision layout catalog.
    ///     Plugged into <see cref="CityPrefabRoadNetworkBuilder"/> to define how each
    ///     district type subdivides its lots into sub-zones with different surface materials.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Zombera/World/District Lot Terrain Layout",
        fileName = "DistrictLotTerrainLayout",
        order = 316)]
    public sealed class DistrictLotTerrainLayout : ScriptableObject
    {
        [Header("Texture Source")]
        [Tooltip("The TextureArrayConfig that defines available layers. " +
                 "Used by the inspector dropdowns and the Populate … Config tool.")]
        public JBooth.MicroSplat.TextureArrayConfig textureArrayConfig;

                [Header("Residential")]
        public LotTerrainZoneLayout residential = new()
        {
            frontYardFractionOfLotDepth = 0.30f,
            sideYardFractionOfLotWidth = 0.15f,
            frontYardTextureIndex   = 0,   // GrassGreen
            backyardTextureIndex    = 0,   // GrassGreen
            sideYardTextureIndex    = 0,   // GrassGreen
            drivewayTextureIndex    = 13,   // Asphalt
            buildingPadTextureIndex = 14,   // RoughConcrete (slab)
            footpathTextureIndex    = 15,   // ConcreteTiles
            varyYardTexturePerLot   = true,
        };

        [Header("Commercial")]
        public LotTerrainZoneLayout commercial = new()
        {
            generateDriveway = false,
            generateDoorFootpath = false,
            generateBuildingPad = false,
            frontYardTextureIndex   = 14,   // RoughConcrete (whole-lot slab)
            backyardTextureIndex    = 14,   // RoughConcrete (whole-lot slab)
            sideYardTextureIndex    = 14,   // RoughConcrete (whole-lot slab)
            drivewayTextureIndex    = 13,   // Asphalt — commercial parking zones
            buildingPadTextureIndex = 14,   // RoughConcrete (whole-lot slab)
            footpathTextureIndex    = 15,   // ConcreteTiles — commercial walkway zones
            paintStripeTextureIndex = 16,   // White paint — parking stall stripes
        };

        [Header("Industrial")]
        public LotTerrainZoneLayout industrial = new()
        {
            frontYardFractionOfLotDepth = 0.06f,
            frontYardTextureIndex   = 12,   // dirt_albedo
            backyardTextureIndex    = 12,   // dirt_albedo
            drivewayWidthMeters     = 4f,
            sideYardTextureIndex    = 12,   // dirt_albedo
            drivewayTextureIndex    = 13,   // Asphalt
            buildingPadTextureIndex = 14,   // RoughConcrete (slab)
            footpathTextureIndex    = 15,   // ConcreteTiles
        };

                [Header("City Core")]
        public LotTerrainZoneLayout cityCore = new()
        {
            generateDriveway = false,
            generateDoorFootpath = false,
            generateBuildingPad = false,
            frontYardTextureIndex   = 14,   // Fresh concrete (whole-lot slab)
            backyardTextureIndex    = 14,   // Fresh concrete (whole-lot slab)
            sideYardTextureIndex    = 14,   // Fresh concrete (whole-lot slab)
            drivewayTextureIndex    = 14,   // Fresh concrete (whole-lot slab)
            buildingPadTextureIndex = 14,   // Fresh concrete (whole-lot slab)
            footpathTextureIndex    = 14,   // Fresh concrete (whole-lot slab)
        };

        [Header("Hospital")]
        public LotTerrainZoneLayout hospital = new()
        {
            generateDriveway = false,
            generateDoorFootpath = false,
            generateBuildingPad = false,
            frontYardTextureIndex   = 14,   // Fresh concrete (whole-lot slab)
            backyardTextureIndex    = 14,   // Fresh concrete (whole-lot slab)
            sideYardTextureIndex    = 14,   // Fresh concrete (whole-lot slab)
            drivewayTextureIndex    = 14,   // Fresh concrete (whole-lot slab)
            buildingPadTextureIndex = 14,   // Fresh concrete (whole-lot slab)
            footpathTextureIndex    = 14,   // Fresh concrete (whole-lot slab)
        };

        /// <summary>
        ///     Returns the zone layout for the given district type.
        ///     Unsupported types fall back to the residential layout.
        /// </summary>
        public LotTerrainZoneLayout GetLayout(CityDistrictType type)
        {
            return type switch
            {
                CityDistrictType.Residential => residential,
                CityDistrictType.Commercial  => commercial,
                CityDistrictType.Industrial  => industrial,
                CityDistrictType.Hospital    => hospital,
                CityDistrictType.CityCore    => cityCore,
                _ => residential
            };
        }
    }
}
