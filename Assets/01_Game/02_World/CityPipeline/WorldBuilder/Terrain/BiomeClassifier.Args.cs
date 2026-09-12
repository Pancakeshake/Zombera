using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Parameter bundles for <see cref="BiomeClassifier"/> (Sonar S107).</summary>
    public static partial class BiomeClassifier
    {
        public readonly struct ClassifyProfiles
        {
            public readonly WorldBiomePalette Palette;
            public readonly HydrologyProfile HydrologyProfile;
            public readonly LandformProfile LandformProfile;
            public readonly WorldMapSizeSettings MapSize;

            public ClassifyProfiles(
                WorldBiomePalette palette,
                HydrologyProfile hydrologyProfile,
                LandformProfile landformProfile,
                WorldMapSizeSettings mapSize)
            {
                Palette = palette;
                HydrologyProfile = hydrologyProfile;
                LandformProfile = landformProfile;
                MapSize = mapSize;
            }
        }

        public readonly struct ClassifyArgs
        {
            public readonly LandformField Landforms;
            public readonly HydrologyPlan Hydrology;
            public readonly ClassifyProfiles Profiles;
            public readonly WorldMapSession Session;
            public readonly int BiomeSeed;
            public readonly bool FastClassify;

            public ClassifyArgs(
                LandformField landforms,
                HydrologyPlan hydrology,
                in ClassifyProfiles profiles,
                WorldMapSession session,
                int biomeSeed,
                bool fastClassify = false)
            {
                Landforms = landforms;
                Hydrology = hydrology;
                Profiles = profiles;
                Session = session;
                BiomeSeed = biomeSeed;
                FastClassify = fastClassify;
            }
        }

        private sealed class ClassifyLoopContext
        {
            public BiomeField Field;
            public LandformField Landforms;
            public HydrologyPlan Hydrology;
            public List<WorldBiomeRecord> Records;
            public Dictionary<string, int> BiomeIndexById;
            public DeterministicNoise2D TempNoise;
            public DeterministicNoise2D MoistNoise;
            public DeterministicNoise2D CoastNoise;
            public float SeaLevel;
            public float ShoreBand;
            public float EdgeClearance;
            public float EdgeDepth;
            public Rect Bounds;
            public WorldMapBoundaryLayout BoundaryLayout;
            public LandformProfile LandformProfile;
            public float InvTempScale;
            public float InvMoistScale;
            public int NoiseOctaves;
        }

        private readonly struct ForcedBiomeBoostArgs
        {
            public readonly BiomeField Field;
            public readonly Dictionary<string, int> BiomeIndexById;
            public readonly int Cell;
            public readonly ForcedBiomeScalars Scalars;

            public ForcedBiomeBoostArgs(
                BiomeField field,
                Dictionary<string, int> biomeIndexById,
                int cell,
                in ForcedBiomeScalars scalars)
            {
                Field = field;
                BiomeIndexById = biomeIndexById;
                Cell = cell;
                Scalars = scalars;
            }
        }

        private readonly struct ForcedBiomeScalars
        {
            public readonly float WaterDist;
            public readonly float ShoreBand;
            public readonly float Elev;
            public readonly float SeaLevel;
            public readonly float ElevNorm;
            public readonly float Slope;
            public readonly float BoostScale;

            public ForcedBiomeScalars(
                float waterDist,
                float shoreBand,
                float elev,
                float seaLevel,
                float elevNorm,
                float slope,
                float boostScale)
            {
                WaterDist = waterDist;
                ShoreBand = shoreBand;
                Elev = elev;
                SeaLevel = seaLevel;
                ElevNorm = elevNorm;
                Slope = slope;
                BoostScale = boostScale;
            }
        }
    }
}
