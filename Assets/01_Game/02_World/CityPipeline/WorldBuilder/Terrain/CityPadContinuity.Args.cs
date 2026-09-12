using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Parameter bundles for <see cref="CityPadContinuity"/> (Sonar S107).</summary>
    public static partial class CityPadContinuity
    {
        public readonly struct QueryContinuityGateArgs
        {
            public readonly IWorldTerrainQuery TerrainQuery;
            public readonly Vector2 Center;
            public readonly float FootprintRadius;
            public readonly LandformProfile Landforms;
            public readonly float TargetHeightWorldY;
            public readonly bool IsCoastal;
            public readonly Vector2 SeawardNormalXZ;

            public QueryContinuityGateArgs(
                IWorldTerrainQuery terrainQuery,
                Vector2 center,
                float footprintRadius,
                LandformProfile landforms,
                float targetHeightWorldY,
                bool isCoastal = false,
                Vector2 seawardNormalXZ = default)
            {
                TerrainQuery = terrainQuery;
                Center = center;
                FootprintRadius = footprintRadius;
                Landforms = landforms;
                TargetHeightWorldY = targetHeightWorldY;
                IsCoastal = isCoastal;
                SeawardNormalXZ = seawardNormalXZ;
            }
        }

        public readonly struct FieldContinuityCore
        {
            public readonly LandformField Field;
            public readonly Rect Plateau;
            public readonly float TargetHeightWorldY;
            public readonly LandformProfile Landforms;
            public readonly HydrologyPlan Hydrology;
            public readonly float MaxReclaimDepth;

            public FieldContinuityCore(
                LandformField field,
                Rect plateau,
                float targetHeightWorldY,
                LandformProfile landforms,
                HydrologyPlan hydrology,
                float maxReclaimDepth)
            {
                Field = field;
                Plateau = plateau;
                TargetHeightWorldY = targetHeightWorldY;
                Landforms = landforms;
                Hydrology = hydrology;
                MaxReclaimDepth = maxReclaimDepth;
            }
        }

        public readonly struct FieldContinuityGateArgs
        {
            public readonly FieldContinuityCore Core;
            public readonly bool IsCoastal;
            public readonly Vector2 SeawardNormalXZ;

            public FieldContinuityGateArgs(
                in FieldContinuityCore core,
                bool isCoastal = false,
                Vector2 seawardNormalXZ = default)
            {
                Core = core;
                IsCoastal = isCoastal;
                SeawardNormalXZ = seawardNormalXZ;
            }
        }

        public readonly struct AnnulusGeometry
        {
            public readonly LandformField Field;
            public readonly Rect Plateau;
            public readonly float TargetHeightWorldY;
            public readonly float ProbeMeters;
            public readonly float CornerFrac;

            public AnnulusGeometry(
                LandformField field,
                Rect plateau,
                float targetHeightWorldY,
                float probeMeters,
                float cornerFrac)
            {
                Field = field;
                Plateau = plateau;
                TargetHeightWorldY = targetHeightWorldY;
                ProbeMeters = probeMeters;
                CornerFrac = cornerFrac;
            }
        }

        public readonly struct AnnulusWaterFilter
        {
            public readonly HydrologyPlan Hydrology;
            public readonly float MaxReclaimDepth;
            public readonly Vector2 SeawardNormalXZ;
            public readonly bool ApplySectorFilter;
            public readonly bool InlandOnly;
            public readonly int Stride;

            public AnnulusWaterFilter(
                HydrologyPlan hydrology,
                float maxReclaimDepth,
                Vector2 seawardNormalXZ = default,
                bool applySectorFilter = false,
                bool inlandOnly = true,
                int stride = 2)
            {
                Hydrology = hydrology;
                MaxReclaimDepth = maxReclaimDepth;
                SeawardNormalXZ = seawardNormalXZ;
                ApplySectorFilter = applySectorFilter;
                InlandOnly = inlandOnly;
                Stride = stride;
            }
        }

        public readonly struct AnnulusSampleArgs
        {
            public readonly AnnulusGeometry Geometry;
            public readonly AnnulusWaterFilter Filter;

            public AnnulusSampleArgs(in AnnulusGeometry geometry, in AnnulusWaterFilter filter)
            {
                Geometry = geometry;
                Filter = filter;
            }
        }

        private readonly struct QueryRingArgs
        {
            public readonly IWorldTerrainQuery TerrainQuery;
            public readonly Vector2 Center;
            public readonly float FootprintRadius;
            public readonly float OutwardMeters;
            public readonly float TargetY;
            public readonly bool IsCoastal;
            public readonly Vector2 SeawardNormalXZ;

            public QueryRingArgs(
                IWorldTerrainQuery terrainQuery,
                Vector2 center,
                float footprintRadius,
                float outwardMeters,
                float targetY,
                bool isCoastal,
                Vector2 seawardNormalXZ)
            {
                TerrainQuery = terrainQuery;
                Center = center;
                FootprintRadius = footprintRadius;
                OutwardMeters = outwardMeters;
                TargetY = targetY;
                IsCoastal = isCoastal;
                SeawardNormalXZ = seawardNormalXZ;
            }
        }
    }
}
