using System.Collections.Generic;
using Crest;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Generates direct <c>WaterBodies/River_&lt;id&gt;</c> Crest roots from the shared inland water
    /// footprint: no <c>RiverSystem</c>, <c>Centerlines</c>, clip-mesh or inland <c>WaterBody</c>
    /// wrapper. Controls are already ordered upstream-to-mouth and height-enforced by the
    /// footprint, so a positive flow velocity always pushes water downstream.
    /// </summary>
    public static class CrestRiverSplineBuilder
    {
        // Authored Crest river values live in WorldWaterProfile.CrestSplinePreset.RiverDefault; only
        // the flow window needs a fallback here because it is not part of the spline preset.
        internal const float DefaultMinimumFlowVelocity = 0.75f;
        internal const float DefaultMaximumFlowVelocity = 4f;

        /// <summary>Optional Crest presentation tuning for spawned river splines.</summary>
        public readonly struct RiverSplineSettings
        {
            /// <summary>Overrides the inland river wave spectrum when set.</summary>
            public readonly OceanWaveSpectrum Spectrum;

            /// <summary>Flow window the authored per-point velocity is clamped into.</summary>
            public readonly float MinimumFlowVelocity;
            public readonly float MaximumFlowVelocity;

            public RiverSplineSettings(
                OceanWaveSpectrum spectrum = null,
                float minimumFlowVelocity = DefaultMinimumFlowVelocity,
                float maximumFlowVelocity = DefaultMaximumFlowVelocity)
            {
                Spectrum = spectrum;
                MinimumFlowVelocity = minimumFlowVelocity;
                MaximumFlowVelocity = maximumFlowVelocity;
            }
        }

        /// <summary>Resolves the river preset from the profile, falling back to the authored defaults.</summary>
        internal static WorldWaterProfile.CrestSplinePreset ResolvePreset(WorldWaterProfile water) =>
            water != null ? water.RiverPreset : WorldWaterProfile.CrestSplinePreset.RiverDefault;

        internal static RiverSplineSettings ResolveSettings(WorldWaterProfile water, OceanWaveSpectrum spectrum) =>
            new(
                spectrum,
                water != null ? water.RiverMinimumFlowSpeedMetersPerSecond : DefaultMinimumFlowVelocity,
                water != null ? water.RiverMaximumFlowSpeedMetersPerSecond : DefaultMaximumFlowVelocity);

        /// <summary>Spawns one Crest root per in-scope river feature. Returns how many were built.</summary>
        public static int SpawnRivers(
            Transform parent,
            HydrologyPlan plan,
            InlandWaterFootprintPlan footprint,
            WorldBuildScope scope,
            WorldWaterProfile water,
            OceanWaveSpectrum spectrum,
            float seaLevelWorldY,
            List<GameObject> roots = null)
        {
            if (parent == null || plan?.Rivers == null || footprint == null)
                return 0;

            var settings = ResolveSettings(water, spectrum);
            var preset = ResolvePreset(water);
            var spawned = 0;
            for (var i = 0; i < plan.Rivers.Length; i++)
            {
                var river = plan.Rivers[i];
                if (river == null)
                    continue;
                if (!footprint.TryGetFeature(river.StableId, out var feature))
                    continue;
                if (!IsFeatureInScope(feature, scope))
                    continue;

                var root = SpawnRiverSystem(parent, feature, river, preset, settings, seaLevelWorldY);
                if (root == null)
                    continue;
                spawned++;
                roots?.Add(root);
            }

            return spawned;
        }

        /// <summary>Builds a single river root from a footprint feature.</summary>
        public static GameObject SpawnRiverSystem(
            Transform parent,
            InlandWaterFootprintPlan.Feature feature,
            RiverPolyline river,
            WorldWaterProfile.CrestSplinePreset preset,
            RiverSplineSettings settings,
            float seaLevelWorldY)
        {
            if (feature?.Points == null || feature.Points.Count < 2)
                return null;

            var positions = BuildOrderedPositions(feature, river, seaLevelWorldY);
            var stacks = new CrestInlandSplineFactory.SplineStackSettings(
                preset,
                Mathf.Clamp(
                    preset.PointFlowVelocity,
                    Mathf.Min(settings.MinimumFlowVelocity, settings.MaximumFlowVelocity),
                    Mathf.Max(settings.MinimumFlowVelocity, settings.MaximumFlowVelocity)),
                feature.Closed,
                settings.Spectrum);

            var root = CrestInlandSplineFactory.Build(
                parent, $"River_{feature.StableId}", positions, stacks);
            return root;
        }

        /// <summary>
        /// Applies the ocean mouth to the actual rendered sea level and keeps the sampled free
        /// surface non-increasing downstream. Falls back to the previous point (never literal zero)
        /// when the preserved surface is unavailable.
        /// </summary>
        internal static List<InlandWaterFootprintPlan.Point> BuildOrderedPositions(
            InlandWaterFootprintPlan.Feature feature,
            RiverPolyline river,
            float seaLevelWorldY)
        {
            var points = new List<InlandWaterFootprintPlan.Point>(feature.Points.Count);
            for (var i = 0; i < feature.Points.Count; i++)
            {
                var clone = feature.Points[i].Clone();
                if (float.IsNaN(clone.SurfaceWorldY) || float.IsInfinity(clone.SurfaceWorldY))
                    clone.SurfaceWorldY = points.Count > 0 ? points[points.Count - 1].SurfaceWorldY : seaLevelWorldY;
                points.Add(clone);
            }

            if (river != null && river.HasOceanMouth)
                points[points.Count - 1].SurfaceWorldY = seaLevelWorldY;
            for (var i = points.Count - 2; i >= 0; i--)
                points[i].SurfaceWorldY = Mathf.Max(points[i].SurfaceWorldY, points[i + 1].SurfaceWorldY);

            return points;
        }

        internal static bool IsFeatureInScope(
            InlandWaterFootprintPlan.Feature feature,
            WorldBuildScope scope)
        {
            if (feature?.Points == null || feature.Points.Count == 0)
                return false;
            return HydrologySurfaceScopeUtility.IntersectsPolyline(
                scope,
                CrestInlandSplineFactory.BuildScopePolyline(feature.Points),
                CrestInlandSplineFactory.ResolveMaxHalfWidth(feature.Points));
        }

        /// <summary>True when a river polyline reaches the build scope (used before the plan exists).</summary>
        public static bool IsRiverInScope(RiverPolyline river, WorldBuildScope scope)
        {
            if (river?.PointsXZ == null || river.PointsXZ.Length == 0)
                return false;
            return HydrologySurfaceScopeUtility.IntersectsPolyline(
                scope,
                river.PointsXZ,
                HydrologyPolylineSampler.ResolveMaxWidthMeters(river) * 0.5f);
        }
    }
}
