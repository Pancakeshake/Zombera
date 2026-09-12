using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Parameter bundles for <see cref="WorldSurfacePainter"/> hot paint paths (Sonar S107).</summary>
    public sealed partial class WorldSurfacePainter
    {
        private struct NaturalSurfacePaintArgs
        {
            public Terrain Terrain;
            public TerrainData Data;
            public float[,,] Map;
            public int Width;
            public int Height;
            public int Layers;
            public Vector3 Origin;
            public Vector3 Size;
            public LandformField Landforms;
            public HydrologyPlan Water;
            public BiomeField Biomes;
            public float SeaLevel;
            public List<WorldBiomeRecord> NaturalRecords;
        }

        private readonly struct AlphamapWriteTarget
        {
            public readonly float[,,] Map;
            public readonly int Z;
            public readonly int X;
            public readonly int Layers;

            public AlphamapWriteTarget(float[,,] map, int z, int x, int layers)
            {
                Map = map;
                Z = z;
                X = x;
                Layers = layers;
            }
        }

        private struct PaintTexelArgs
        {
            public AlphamapWriteTarget Target;
            public float WorldX;
            public float WorldZ;
            public LandformField Landforms;
            public HydrologyPlan Water;
            public BiomeField Biomes;
            public List<WorldBiomeRecord> NaturalRecords;
            public float SeaLevel;
        }

        private struct AccumulatePaintArgs
        {
            public AlphamapWriteTarget Target;
            public float WorldX;
            public float WorldZ;
            public LandformField Landforms;
            public BiomeField Biomes;
            public List<WorldBiomeRecord> NaturalRecords;
            public int Cell;
            public float Slope;
            public float Elev;
            public float SeaLevel;
            public PaintCellSnapshot Snapshot;
            public BiomeBilinearCoords BiomeCoords;
        }

        private struct CoastalBlendApplyArgs
        {
            public AlphamapWriteTarget Target;
            public float WorldX;
            public float WorldZ;
            public float CoastalT;
            public float Slope;
            public float ElevAbove;
            public float WaterDist;
            public float EdgeDist;
            public float Moisture;
            public WorldWaterClass WaterClass;
            public string DominantId;
            public bool PreferWetSand;
        }

        private struct BeachStrengthQueryArgs
        {
            public float ShoreWeight;
            public float Elev;
            public float SeaLevel;
            public float WaterDist;
            public WorldWaterClass WaterClass;
            public string DominantId;
            public bool NearOceanCoast;
            public float WarpedOceanEdgeDistMeters;
        }

        private struct BiomeBlendPaintArgs
        {
            public AlphamapWriteTarget Target;
            public BiomeField Biomes;
            public List<WorldBiomeRecord> NaturalRecords;
            public LandformField Landforms;
            public int Cell;
            public BiomeBilinearCoords Coords;
            public float ElevAbove;
            public float Slope;
            public float InteriorMask;
            public float WorldX;
            public float WorldZ;
        }

        private struct BiomeRecordElevationArgs
        {
            public AlphamapWriteTarget Target;
            public List<WorldBiomeRecord> NaturalRecords;
            public string StableId;
            public float ElevAbove;
            public float Slope;
            public float Weight;
            public float InteriorMask;
            public float WorldX;
            public float WorldZ;
        }

        private struct ValleyForestFloorArgs
        {
            public AlphamapWriteTarget Target;
            public float ElevAbove;
            public float Slope;
            public string DominantId;
            public float Moisture;
        }

        private struct ElevationRockBandArgs
        {
            public AlphamapWriteTarget Target;
            public float WorldX;
            public float WorldZ;
            public float Slope;
            public float ElevAbove;
            public string DominantId;
            public float SnowBiomeWeight;
            public float SnowMin;
            public float SnowFull;
        }

        private struct ElevationSnowBandArgs
        {
            public AlphamapWriteTarget Target;
            public float WorldX;
            public float WorldZ;
            public float Slope;
            public float ElevAbove;
            public float SnowBiomeWeight;
            public float RockStrength;
            public float SnowMin;
            public float SnowFull;
        }

        private struct HighlandGrassArgs
        {
            public AlphamapWriteTarget Target;
            public float WorldX;
            public float WorldZ;
            public float Slope;
            public float ElevAbove;
            public float SnowMin;
            public float SnowFull;
        }

        private struct MainGrassMixArgs
        {
            public AlphamapWriteTarget Target;
            public float WorldX;
            public float WorldZ;
            public float Scale;
            public string DominantId;
            public float Moisture;
        }

        private struct CliffTierArgs
        {
            public AlphamapWriteTarget Target;
            public float WorldX;
            public float WorldZ;
            public float Slope;
            public float ElevAbove;
            public float InteriorMask;
            public bool InOceanBarrierStrip;
            public bool NearOceanCoast;
            public string DominantId;
        }

        private struct PlanningFieldSampleArgs
        {
            public UnityEngine.Vector2 Origin;
            public float CellSize;
            public int Width;
            public int Height;
            public float[] Values;
            public float WorldX;
            public float WorldZ;
            public float Fallback;
        }

        private struct BeachDecision
        {
            public float CoastalT;
            public float BeachElevCap;
            public bool OnEdgeBeachBand;
            public bool ForceBeachSand;
            public float ElevAbove;
            public float WaterDist;
            public float EdgeDist;
            public float Moisture;
            public WorldWaterClass WaterClass;
            public string DominantId;
            public bool NearOceanCoast;
            public float InteriorMask;
            public bool InOceanBarrier;
        }
    }
}

