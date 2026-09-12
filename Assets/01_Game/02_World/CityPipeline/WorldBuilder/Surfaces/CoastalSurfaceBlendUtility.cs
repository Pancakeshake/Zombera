using UnityEngine;
namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Static coastal blend math for ocean shores — testable without MonoBehaviour.
    /// Suppress-then-add keeps pre-normalize alphamap sums within budget.
    /// </summary>
    public static class CoastalSurfaceBlendUtility
    {
        public const float BeachSoftMin = 0.22f;
        public const float BeachSoftFull = 0.72f;
        public const float MaxPreNormalizeSum = 2.5f;
        public const float CoastalOverlayBudget = 1.15f;
        public const float PerLayerCapScale = 0.55f;
        public struct CoastalLayerIndices
        {
            public int Sand;
            public int Sand01;
            public int WetSand;
            public int WetRock;
            public int BlackSand;
            public int CliffBright;
            public int CliffDark;
            public int CliffPink;
            public int CliffRed;
            public int Dirt;
            public int GrassGreen;
            public int GrassYellow;
            public int Grass;
            public int SparseGrass;
            public int MeadowGrass;
            public int DryForestFloor;
        }
        public struct CoastalBlendParams
        {
            public float CoastalT;
            public float Slope;
            public float WaterDist;
            public float EdgeDist;
            public float ElevAbove;
            public float Moisture;
            public float MacroNoise;
            public float DetailNoise;
            public bool PreferWet;
            public bool PreferBlackSand;
            public bool PreferRedCliff;
            public bool PreferDryDesert;
        }
        public struct BeachStrengthArgs
        {
            public float ShoreWeight;
            public float Elev;
            public float SeaLevel;
            public float WaterDist;
            public WorldWaterClass WaterClass;
            public string DominantId;
            public bool NearOceanCoast;
            public float OceanBeachMaxWaterDistanceMeters;
            public float BeachMaxElevationAboveSea;
            public float BeachInlandBlendMeters;
            public float EdgeDistMeters;
            public float OceanCoastStripWidthMeters;
        }
        public struct LegacyBeachAccumulateArgs
        {
            public float BeachStrength;
            public float WaterDist;
            public WorldWaterClass WaterClass;
            public bool PreferWetSand;
            public float Macro;
            public float Detail;
            public float InlandWetSandFadeMeters;
        }
        public static float ComputeBeachStrength(in BeachStrengthArgs args)
        {
            var strip = Mathf.Max(40f, args.OceanCoastStripWidthMeters);
            var onMapEdgeCoast = args.NearOceanCoast || args.EdgeDistMeters <= strip;
            if (!onMapEdgeCoast)
                return 0f;
            var elevAbove = args.Elev - args.SeaLevel;
            var beachElevCap = Mathf.Max(0.5f, args.BeachMaxElevationAboveSea);
            var elevFactor = 1f - Mathf.Clamp01(elevAbove / beachElevCap);
            // Cliffs in the barrier strip stay rocky; only low shelves get sand.
            if (elevFactor <= 0.02f && elevAbove > beachElevCap)
                return 0f;
            var maxWaterDist = Mathf.Max(6f, args.OceanBeachMaxWaterDistanceMeters);
            var waterFade = Mathf.Clamp01(1f - args.WaterDist / maxWaterDist);
            var edgeFade = Mathf.Clamp01(1f - args.EdgeDistMeters / strip);
            var distFade = Mathf.Max(waterFade, edgeFade);
            if (distFade <= 0.01f)
                return 0f;
            var inland = Mathf.Max(4f, args.BeachInlandBlendMeters);
            var strength = args.ShoreWeight * distFade;
            if (args.DominantId is "Shore" or "Ocean")
                strength = Mathf.Max(strength, 0.55f * distFade);
            // Map-edge dry shoulder: drive sand from boundary distance, not hydrology alone.
            if (elevAbove <= beachElevCap)
            {
                var edgeProx = Mathf.Exp(-args.EdgeDistMeters / inland) * edgeFade;
                strength = Mathf.Max(strength, edgeProx * Mathf.Max(elevFactor, 0.35f) * 0.95f);
                if (waterFade > 0.01f)
                {
                    var waterProx = Mathf.Exp(-args.WaterDist / inland) * waterFade;
                    strength = Mathf.Max(strength, waterProx * elevFactor * 0.75f);
                }
            }
            else if (args.WaterClass == WorldWaterClass.None && args.WaterDist < maxWaterDist)
            {
                var proximity = Mathf.Exp(-args.WaterDist / inland) * waterFade;
                strength = Mathf.Max(strength, proximity * elevFactor * 0.45f * distFade);
            }
            return Mathf.Clamp01(strength);
        }
        public static float CoastalBlendFactor(float beachStrength)
        {
            var t = Mathf.InverseLerp(BeachSoftMin, BeachSoftFull, beachStrength);
            return t * t * (3f - 2f * t);
        }
        public static void SuppressCompetingLayers(
            float[,,] map,
            int z,
            int x,
            in CoastalLayerIndices layers,
            float suppressStrength)
        {
            var keep = 1f - Mathf.Clamp01(suppressStrength);
            if (keep >= 0.999f)
                return;
            ScaleLayer(map, z, x, layers.GrassGreen, keep);
            ScaleLayer(map, z, x, layers.GrassYellow, keep);
            ScaleLayer(map, z, x, layers.Grass, keep);
            ScaleLayer(map, z, x, layers.SparseGrass, keep);
            ScaleLayer(map, z, x, layers.MeadowGrass, keep);
            ScaleLayer(map, z, x, layers.DryForestFloor, keep);
            ScaleLayer(map, z, x, layers.Sand, keep);
            ScaleLayer(map, z, x, layers.Sand01, keep);
            ScaleLayer(map, z, x, layers.Dirt, keep);
        }
        public static void AccumulateCoastalLayers(
            float[,,] map,
            int z,
            int x,
            int layerCount,
            in CoastalLayerIndices layers,
            in CoastalBlendParams p)
        {
            if (p.CoastalT <= 0.02f)
                return;
            var t = Mathf.Clamp01(p.CoastalT);
            if (t > 0.55f)
                t = Mathf.Min(t, CoastalOverlayBudget);
            else
                t = Mathf.Min(CoastalOverlayBudget, t * 0.92f);
            var flat = Mathf.InverseLerp(32f, 6f, p.Slope);
            var midSlope = Mathf.Clamp01(Mathf.InverseLerp(18f, 28f, p.Slope) *
                                         (1f - Mathf.InverseLerp(28f, 38f, p.Slope)));
            var steep = Mathf.InverseLerp(18f, 42f, p.Slope);
            var wet = p.PreferWet
                ? Mathf.Clamp01(0.55f + Mathf.Exp(-p.WaterDist / 8f) * 0.45f)
                : Mathf.Clamp01(Mathf.Exp(-p.WaterDist / 8f));
            var macro = p.MacroNoise;
            var detail = p.DetailNoise;
            var dryBias = Mathf.Lerp(0.72f, 1f, detail) * Mathf.Lerp(0.5f, 0.92f, macro);
            var wetBias = wet * Mathf.Lerp(0.22f, 0.58f, 1f - macro * 0.65f);
            var blackBias = p.PreferBlackSand ? Mathf.Lerp(0.25f, 0.55f, 1f - macro) : Mathf.Lerp(0.05f, 0.2f, detail);
            if (p.PreferDryDesert)
            {
                wetBias *= 0.35f;
                dryBias = Mathf.Min(1f, dryBias * 1.15f);
            }
            // PreferWet / waterline: bias toward WetSand over dry Sand.
            // Near sea level (elev → 0), force wet sand dominance for ocean biomes.
            var nearSeaElev = 1f - Mathf.Clamp01(Mathf.Max(0f, p.ElevAbove) / 2.5f);
            if (p.PreferWet || p.WaterDist < 3f)
            {
                wetBias = Mathf.Max(wetBias, 0.72f);
                dryBias *= 0.45f;
            }
            if (nearSeaElev > 0.05f)
            {
                wetBias = Mathf.Max(wetBias, Mathf.Lerp(0.35f, 0.95f, nearSeaElev));
                dryBias *= Mathf.Lerp(1f, 0.3f, nearSeaElev);
            }
            var cap = PerLayerCapScale * t;
            // Waterline / spray
            Add(map, z, x, layerCount, layers.WetSand, Mathf.Min(cap, t * flat * wetBias * 0.85f));
            Add(map, z, x, layerCount, layers.WetRock, Mathf.Min(cap, t * Mathf.Max(steep * 0.55f, wetBias * 0.35f) * 0.45f));
            // Flat beach — dry sand only on gentle shelves (no sand carpet on cliffs).
            var drySand = t * flat * dryBias * (1f - wetBias * 0.45f);
            Add(map, z, x, layerCount, layers.Sand, Mathf.Min(cap, drySand * 0.85f));
            Add(map, z, x, layerCount, layers.BlackSand, Mathf.Min(cap, drySand * blackBias));
            // Rocky shelf — yellow cliff accent
            var shelf = t * midSlope * Mathf.Lerp(0.45f, 0.9f, detail);
            Add(map, z, x, layerCount, layers.CliffBright, Mathf.Min(cap, shelf * 0.55f));
            Add(map, z, x, layerCount, layers.CliffPink, Mathf.Min(cap, shelf * 0.4f));
            Add(map, z, x, layerCount, layers.WetRock, Mathf.Min(cap, shelf * wetBias * 0.25f));
            // Steep cliff
            var cliff = t * steep * Mathf.Lerp(0.5f, 1f, macro);
            Add(map, z, x, layerCount, layers.CliffDark, Mathf.Min(cap, cliff * 0.55f));
            Add(map, z, x, layerCount, layers.CliffPink, Mathf.Min(cap, cliff * 0.25f));
            if (p.PreferRedCliff)
                Add(map, z, x, layerCount, layers.CliffRed, Mathf.Min(cap, cliff * 0.35f));
            Add(map, z, x, layerCount, layers.WetRock, Mathf.Min(cap, cliff * wetBias * 0.2f));
        }
        public static void AccumulateLegacyBeachLayers(
            float[,,] map,
            int z,
            int x,
            int layerCount,
            in CoastalLayerIndices layers,
            in LegacyBeachAccumulateArgs args)
        {
            if (args.BeachStrength <= 0.01f)
                return;
            var wetBias = args.PreferWetSand
                ? 0.75f
                : Mathf.Clamp01(Mathf.Exp(-args.WaterDist / 20f));
            _ = args.WaterClass;
            var drySand = args.BeachStrength * Mathf.Lerp(0.5f, 0.92f, args.Macro) *
                          Mathf.Lerp(0.72f, 1f, args.Detail);
            var wetSand = args.BeachStrength * wetBias *
                          Mathf.Lerp(0.22f, 0.58f, 1f - args.Macro * 0.65f);
            if (!args.PreferWetSand && args.WaterClass == WorldWaterClass.None)
            {
                var inlandFade = Mathf.Clamp01(
                    Mathf.Exp(-(args.WaterDist - 2f) / Mathf.Max(1f, args.InlandWetSandFadeMeters)));
                wetSand *= inlandFade;
            }
            var dirt = args.BeachStrength * (1f - args.Macro) *
                       Mathf.Lerp(0.1f, 0.3f, args.Detail) * (1f - wetBias * 0.45f);
            Add(map, z, x, layerCount, layers.Sand, drySand * (args.PreferWetSand ? 0.35f : 1f));
            // Legacy path used Sand01 as wet — keep for A/B rollback only.
            Add(map, z, x, layerCount, layers.Sand01, wetSand);
            Add(map, z, x, layerCount, layers.Dirt, dirt);
        }
        private static void ScaleLayer(float[,,] map, int z, int x, int layer, float keep)
        {
            if (layer < 0)
                return;
            map[z, x, layer] *= keep;
        }
        private static void Add(float[,,] map, int z, int x, int layerCount, int layer, float amount)
        {
            if (amount <= 0f || layer < 0 || layer >= layerCount)
                return;
            if (layer >= WorldSurfacePalette.InfrastructureMinIndex &&
                layer <= WorldSurfacePalette.InfrastructureMaxIndex)
                return;
            map[z, x, layer] += amount;
        }
    }
}

