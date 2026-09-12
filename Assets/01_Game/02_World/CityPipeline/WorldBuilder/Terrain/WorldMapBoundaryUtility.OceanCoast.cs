using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Ocean trench, coast height/blend/falloff and ocean edge distance helpers.</summary>
    public static partial class WorldMapBoundaryUtility
    {
        public static float EvaluateOceanTrenchHeight(
            float landHeight,
            float seaLevel,
            float edgeDistanceMeters,
            float stripDepthMeters,
            float steepness,
            float trenchDepthMeters,
            float noiseAmplitudeMeters,
            float alongEdgeMeters,
            DeterministicNoise2D noise)
        {
            if (stripDepthMeters <= 1f)
                return landHeight;

            var inwardT = Mathf.Clamp01(edgeDistanceMeters / stripDepthMeters);
            var wall = Mathf.Pow(1f - inwardT, Mathf.Max(1.1f, steepness));
            var floor = seaLevel - Mathf.Max(8f, trenchDepthMeters);
            var alongNoise = noise.Fbm(alongEdgeMeters / 900f, edgeDistanceMeters / 420f, 3);
            floor += (alongNoise - 0.5f) * 2f * noiseAmplitudeMeters;

            var trenchHeight = Mathf.Lerp(landHeight, floor, wall);
            if (inwardT < 0.5f)
                trenchHeight = Mathf.Min(trenchHeight, seaLevel - 6f - inwardT * 18f);

            return trenchHeight;
        }

        /// <summary>
        ///     Ocean-side coast profile: offshore trench → shallow shelf → beach declining into the sea.
        ///     Never creates a dry berm (height stays ≤ sea − ε on wet bands). Underwater shelf relief
        ///     may be non-monotonic seaward when reef deepen noise is enabled.
        /// </summary>
        public static float EvaluateOceanCoastHeight(in OceanCoastHeightParams p)
        {
            if (p.StripDepthMeters <= 1f)
                return p.LandHeight;

            var offshoreEnd = Mathf.Clamp(p.OffshoreWidthMeters, 8f, p.StripDepthMeters);
            var shoreEnd = Mathf.Clamp(offshoreEnd + p.ShoreShelfWidthMeters, offshoreEnd, p.StripDepthMeters);
            var beachEnd = Mathf.Clamp(shoreEnd + p.BeachWidthMeters, shoreEnd, p.StripDepthMeters);

            var outerDepth = Mathf.Max(0.5f, p.ShelfOuterDepthMeters);
            var innerDepth = Mathf.Clamp(p.ShelfInnerDepthMeters, CoastalShelfReefUtility.MinWaterCoverMeters, outerDepth);
            var shelfOuterHeight = p.SeaLevel - outerDepth;
            var shelfInnerHeight = p.SeaLevel - innerDepth;

            // Subtle along-shore grade noise (along-edge only so beach face stays orderly).
            var alongNoise = p.Noise != null
                ? p.Noise.Fbm(p.AlongEdgeMeters / 900f, 0.37f, 3)
                : 0.5f;
            var gradeNoise = (alongNoise - 0.5f) * 2f * p.TrenchNoiseAmplitudeMeters * 0.12f;

            if (p.EdgeDistanceMeters <= offshoreEnd)
            {
                var deep = EvaluateOceanTrenchHeight(
                    p.LandHeight,
                    p.SeaLevel,
                    p.EdgeDistanceMeters,
                    offshoreEnd,
                    p.TrenchSteepness,
                    p.TrenchDepthMeters,
                    p.TrenchNoiseAmplitudeMeters,
                    p.AlongEdgeMeters,
                    p.Noise);
                var trenchBlend = SmoothStep01(p.EdgeDistanceMeters / Mathf.Max(1f, offshoreEnd));
                // Join trench to the same outer shelf height the shelf band starts at.
                return Mathf.Min(Mathf.Lerp(deep, shelfOuterHeight, trenchBlend), shelfOuterHeight);
            }

            if (p.EdgeDistanceMeters <= shoreEnd)
            {
                var shelfT = SmoothStep01(Mathf.InverseLerp(offshoreEnd, shoreEnd, p.EdgeDistanceMeters));
                var height = Mathf.Lerp(shelfOuterHeight, shelfInnerHeight, shelfT);
                return CoastalShelfReefUtility.ApplyDeepen(
                    height,
                    p.SeaLevel,
                    p.WorldX,
                    p.WorldZ,
                    p.ShelfReefNoiseScaleMeters,
                    p.ShelfReefNoiseAmplitudeMeters,
                    p.ShelfReefCoverage,
                    p.Noise);
            }

            // Beach declines from a dry shoulder (never above land) down to sea level — no berm above inland.
            var beachInlandHeight = ResolveBeachInlandHeight(
                p.LandHeight, p.SeaLevel, p.BeachMaxElevationMeters);

            if (p.EdgeDistanceMeters <= beachEnd)
            {
                var beachT = SmoothStep01(Mathf.InverseLerp(shoreEnd, beachEnd, p.EdgeDistanceMeters));
                var height = Mathf.Lerp(p.SeaLevel, beachInlandHeight, beachT);
                height += gradeNoise * beachT * beachT;
                // Keep the dry shoulder as a ceiling so grade noise cannot form a berm above the soft falloff join.
                return Mathf.Min(height, beachInlandHeight);
            }

            // Inland of the beach within the strip: natural land (soft falloff / blend fade in generator).
            return p.LandHeight;
        }

        /// <summary>Legacy positional overload for callers/tests; reef deepen disabled (amp 0).</summary>
        public static float EvaluateOceanCoastHeight(
            float landHeight,
            float seaLevel,
            float edgeDistanceMeters,
            float stripDepthMeters,
            float offshoreWidthMeters,
            float shoreShelfWidthMeters,
            float beachWidthMeters,
            float beachMaxElevationMeters,
            float trenchDepthMeters,
            float trenchSteepness,
            float noiseAmplitudeMeters,
            float alongEdgeMeters,
            DeterministicNoise2D noise,
            float worldX = 0f,
            float worldZ = 0f,
            float shelfOuterDepthMeters = 8f,
            float shelfInnerDepthMeters = 1f,
            float shelfReefNoiseScaleMeters = 180f,
            float shelfReefNoiseAmplitudeMeters = 0f,
            float shelfReefCoverage = 0.32f)
        {
            var p = new OceanCoastHeightParams
            {
                LandHeight = landHeight,
                SeaLevel = seaLevel,
                EdgeDistanceMeters = edgeDistanceMeters,
                StripDepthMeters = stripDepthMeters,
                OffshoreWidthMeters = offshoreWidthMeters,
                ShoreShelfWidthMeters = shoreShelfWidthMeters,
                BeachWidthMeters = beachWidthMeters,
                BeachMaxElevationMeters = beachMaxElevationMeters,
                TrenchDepthMeters = trenchDepthMeters,
                TrenchSteepness = trenchSteepness,
                TrenchNoiseAmplitudeMeters = noiseAmplitudeMeters,
                AlongEdgeMeters = alongEdgeMeters,
                WorldX = worldX,
                WorldZ = worldZ,
                ShelfOuterDepthMeters = shelfOuterDepthMeters,
                ShelfInnerDepthMeters = shelfInnerDepthMeters,
                ShelfReefNoiseScaleMeters = shelfReefNoiseScaleMeters,
                ShelfReefNoiseAmplitudeMeters = shelfReefNoiseAmplitudeMeters,
                ShelfReefCoverage = shelfReefCoverage,
                Noise = noise
            };
            return EvaluateOceanCoastHeight(in p);
        }

        /// <summary>
        ///     Full coast authority through trench/shelf/beach; fade only inland of the beach core.
        ///     Prevents pre-coast land height from leaking into the beach and forming a mound.
        /// </summary>
        public static float EvaluateOceanCoastBlend(
            float edgeDistanceMeters,
            float stripDepthMeters,
            float coastCoreDepthMeters)
        {
            if (stripDepthMeters <= 1f || edgeDistanceMeters >= stripDepthMeters)
                return 0f;

            var coreEnd = Mathf.Clamp(coastCoreDepthMeters, 1f, stripDepthMeters);
            if (edgeDistanceMeters <= coreEnd)
                return 1f;

            return 1f - Mathf.Clamp01(
                (edgeDistanceMeters - coreEnd) / Mathf.Max(1f, stripDepthMeters - coreEnd));
        }

        /// <summary>
        ///     Min distance to any ocean-classified map edge (not limited to the barrier strip).
        /// </summary>
        public static bool TryGetOceanEdgeDistance(
            float x,
            float z,
            Rect bounds,
            WorldMapBoundaryLayout layout,
            out float oceanEdgeDistanceMeters)
        {
            oceanEdgeDistanceMeters = float.MaxValue;
            var found = false;

            if (layout.West == WorldMapBoundaryKind.Ocean)
            {
                found = true;
                oceanEdgeDistanceMeters = Mathf.Min(oceanEdgeDistanceMeters, x - bounds.xMin);
            }

            if (layout.East == WorldMapBoundaryKind.Ocean)
            {
                found = true;
                oceanEdgeDistanceMeters = Mathf.Min(oceanEdgeDistanceMeters, bounds.xMax - x);
            }

            if (layout.South == WorldMapBoundaryKind.Ocean)
            {
                found = true;
                oceanEdgeDistanceMeters = Mathf.Min(oceanEdgeDistanceMeters, z - bounds.yMin);
            }

            if (layout.North == WorldMapBoundaryKind.Ocean)
            {
                found = true;
                oceanEdgeDistanceMeters = Mathf.Min(oceanEdgeDistanceMeters, bounds.yMax - z);
            }

            return found;
        }

        /// <summary>
        /// Ocean edge distance only when the nearest map edge is ocean-classified.
        /// Prevents mixed-layout corner bleed (e.g. north mountains with west/east ocean).
        /// </summary>
        public static bool TryGetSideAwareOceanEdgeDistance(
            float x,
            float z,
            Rect bounds,
            WorldMapBoundaryLayout layout,
            out float oceanEdgeDistanceMeters)
        {
            oceanEdgeDistanceMeters = 0f;
            TryGetNearestEdgeSide(x, z, bounds, out var nearestSide, out _);
            if (layout.Get(nearestSide) != WorldMapBoundaryKind.Ocean)
                return false;

            return TryGetOceanEdgeDistance(x, z, bounds, layout, out oceanEdgeDistanceMeters);
        }

        /// <summary>
        ///     Pushes the shoreline inland with FBM so coasts are eroded/irregular.
        ///     Never increases distance — geometric map edges always stay wet.
        /// </summary>
        public static float WarpOceanEdgeDistance(
            float straightOceanEdgeDistanceMeters,
            float worldX,
            float worldZ,
            DeterministicNoise2D noise,
            float erosionAmplitudeMeters,
            float erosionScaleMeters)
        {
            if (straightOceanEdgeDistanceMeters <= 0f || erosionAmplitudeMeters <= 0f || noise == null)
                return Mathf.Max(0f, straightOceanEdgeDistanceMeters);

            var scale = Mathf.Max(1f, erosionScaleMeters);
            var macro = noise.Fbm(worldX / scale, worldZ / scale, 4);
            var detail = noise.Fbm(
                worldX / (scale * 0.42f) + 19.1f,
                worldZ / (scale * 0.42f) - 7.3f,
                3);
            var erosion = (macro * 0.62f + detail * 0.38f) * erosionAmplitudeMeters;
            return Mathf.Max(0f, straightOceanEdgeDistanceMeters - erosion);
        }

        /// <summary>
        ///     Highs-only soft approach inland of the beach core. Never lifts lows toward sea.
        ///     Falloff width is clamped to the edge barrier depth so it cannot outrun hydrology/playable insets.
        /// </summary>
        public static float EvaluateOceanCoastFalloff(
            float rawHeight,
            float seaLevel,
            float oceanEdgeDistanceMeters,
            float coastCoreDepthMeters,
            float falloffWidthMeters,
            float barrierDepthMeters,
            float dryShoulderElevationMeters)
        {
            var effectiveFalloff = Mathf.Min(
                Mathf.Max(0f, falloffWidthMeters),
                Mathf.Max(1f, barrierDepthMeters));
            if (effectiveFalloff <= 1f || oceanEdgeDistanceMeters >= effectiveFalloff)
                return rawHeight;

            var coastCore = Mathf.Clamp(coastCoreDepthMeters, 0f, effectiveFalloff);
            if (oceanEdgeDistanceMeters <= coastCore)
                return rawHeight;

            var dryShoulder = seaLevel + Mathf.Max(0f, dryShoulderElevationMeters);
            if (rawHeight <= dryShoulder)
                return rawHeight;

            var inlandT = SmoothStep01(Mathf.InverseLerp(
                coastCore, effectiveFalloff, oceanEdgeDistanceMeters));
            return Mathf.Lerp(dryShoulder, rawHeight, inlandT);
        }

        /// <summary>
        ///     Lifts inland lows to sea + dryFloor without changing ocean trench/beach core heights.
        ///     Soft-blends across <paramref name="blendMeters"/> inland of the coast strip.
        /// </summary>
        public static float ApplyInlandDryFloor(
            float height,
            float seaLevel,
            float oceanEdgeDistanceMeters,
            float coastCoreDepthMeters,
            float dryFloorMetersAboveSea,
            float blendMeters)
        {
            if (dryFloorMetersAboveSea <= 0f)
                return height;

            var floor = seaLevel + dryFloorMetersAboveSea;
            if (height >= floor)
                return height;

            var coastCore = Mathf.Max(0f, coastCoreDepthMeters);
            if (oceanEdgeDistanceMeters <= coastCore)
                return height;

            var blend = Mathf.Max(1f, blendMeters);
            var t = SmoothStep01(Mathf.InverseLerp(
                coastCore, coastCore + blend, oceanEdgeDistanceMeters));
            if (t <= 0.001f)
                return height;

            return Mathf.Lerp(height, floor, t);
        }

        /// <summary>True when <paramref name="x"/>/<paramref name="z"/> lies inside an ocean edge strip.</summary>
        public static bool TryGetOceanCoastDistance(
            float x,
            float z,
            Rect bounds,
            float stripDepthMeters,
            WorldMapBoundaryLayout layout,
            out float edgeDistanceMeters)
        {
            edgeDistanceMeters = 0f;
            if (stripDepthMeters <= 1f)
                return false;

            if (!TryGetSideAwareOceanEdgeDistance(x, z, bounds, layout, out edgeDistanceMeters))
                return false;

            return edgeDistanceMeters < stripDepthMeters;
        }

        private static float ResolveBeachInlandHeight(
            float landHeight,
            float seaLevel,
            float beachMaxElevationMeters)
        {
            var dryCap = seaLevel + Mathf.Max(0f, beachMaxElevationMeters);
            return landHeight <= dryCap ? landHeight : dryCap;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);
    }
}
