using System.Collections.Generic;
using Crest;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Generates direct <c>WaterBodies/Lake_&lt;id&gt;</c> Crest roots from the footprint's lake
    /// medial spine. Every control point shares one constant surface height, while the per-point
    /// width follows the measured cross-section, so a lake reproduces varying widths without ever
    /// rendering a step.
    /// </summary>
    public static class CrestLakeSplineBuilder
    {
        internal static WorldWaterProfile.CrestSplinePreset ResolvePreset(WorldWaterProfile water) =>
            water != null ? water.LakePreset : WorldWaterProfile.CrestSplinePreset.LakeDefault;

        /// <summary>Spawns one Crest root per in-scope lake feature. Returns how many were built.</summary>
        public static int SpawnLakes(
            Transform parent,
            HydrologyPlan plan,
            InlandWaterFootprintPlan footprint,
            WorldBuildScope scope,
            WorldWaterProfile water,
            OceanWaveSpectrum spectrum,
            float seaLevelWorldY,
            List<GameObject> roots = null)
        {
            if (parent == null || plan?.Lakes == null || footprint == null)
                return 0;

            var preset = ResolvePreset(water);
            var tolerance = water != null
                ? water.CrestLakeSeaLevelToleranceMeters
                : LakeCrestPlacementUtility.DefaultSeaLevelToleranceMeters;
            var spawned = 0;
            for (var i = 0; i < plan.Lakes.Length; i++)
            {
                var lake = plan.Lakes[i];
                if (!CrestLakeWaterBodyPlacementUtility.TryBuildPlacement(
                        lake, seaLevelWorldY, tolerance, scope, out var placement))
                    continue;
                if (!footprint.TryGetFeature(lake.StableId, out var feature))
                    continue;

                var root = SpawnLakeSystem(parent, feature, placement, preset, spectrum);
                if (root == null)
                    continue;
                spawned++;
                roots?.Add(root);
            }

            return spawned;
        }

        /// <summary>Builds a single lake root: one constant surface height, varying widths.</summary>
        public static GameObject SpawnLakeSystem(
            Transform parent,
            InlandWaterFootprintPlan.Feature feature,
            CrestLakeWaterBodyPlacementUtility.LakePlacement placement,
            WorldWaterProfile.CrestSplinePreset preset,
            OceanWaveSpectrum spectrum)
        {
            if (feature?.Points == null || feature.Points.Count < 2)
                return null;

            var points = new List<InlandWaterFootprintPlan.Point>(feature.Points.Count);
            for (var i = 0; i < feature.Points.Count; i++)
            {
                var clone = feature.Points[i].Clone();
                clone.SurfaceWorldY = placement.SurfaceWorldY;
                points.Add(clone);
            }

            var stacks = new CrestInlandSplineFactory.SplineStackSettings(
                preset,
                preset.PointFlowVelocity,
                feature.Closed,
                spectrum);
            return CrestInlandSplineFactory.Build(
                parent, $"Lake_{placement.StableId}", points, stacks);
        }
    }
}
