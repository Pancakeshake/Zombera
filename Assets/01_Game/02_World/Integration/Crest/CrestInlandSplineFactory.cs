using System.Collections.Generic;
using Crest;
using Crest.Spline;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Builds the authored Crest inland root stack directly under a folder:
    /// an open, centred <see cref="Spline"/>, <see cref="RegisterHeightInput"/>,
    /// <see cref="RegisterFlowInput"/> and <see cref="ShapeFFT"/>, plus one
    /// <see cref="SplinePoint"/> child per footprint control point carrying
    /// <see cref="SplinePointData"/>, <see cref="SplinePointDataWaves"/> and
    /// <see cref="SplinePointDataFlow"/>.
    /// <para>
    /// Every setting comes from <see cref="WorldWaterProfile"/> so runtime generation never
    /// depends on the development scene, and the width comes from the shared footprint so the
    /// ribbon and the carved hole cannot disagree.
    /// </para>
    /// </summary>
    internal static class CrestInlandSplineFactory
    {
        internal const string SplinePointName = "SplinePoint";

        /// <summary>One named Crest spline preset, resolved from the water profile.</summary>
        internal readonly struct SplineStackSettings
        {
            public readonly float SplineRadius;
            public readonly int SplineSubdivisions;
            public readonly float HeightRadius;
            public readonly int HeightSubdivisions;
            public readonly int WaveResolution;
            public readonly float WaveTurbulence;
            public readonly float PointWaveWeight;
            public readonly float PointFlowVelocity;
            public readonly bool Closed;
            public readonly OceanWaveSpectrum Spectrum;

            public SplineStackSettings(
                WorldWaterProfile.CrestSplinePreset preset,
                float pointFlowVelocity,
                bool closed,
                OceanWaveSpectrum spectrum)
            {
                SplineRadius = Mathf.Max(0.01f, preset.SplineRadius);
                SplineSubdivisions = Mathf.Max(1, preset.SplineSubdivisions);
                HeightRadius = Mathf.Max(0f, preset.HeightRadius);
                HeightSubdivisions = Mathf.Max(1, preset.HeightSubdivisions);
                WaveResolution = Mathf.Max(1, preset.WaveResolution);
                WaveTurbulence = Mathf.Clamp01(preset.WaveTurbulence);
                PointWaveWeight = Mathf.Max(0f, preset.PointWaveWeight);
                PointFlowVelocity = Mathf.Max(0f, pointFlowVelocity);
                Closed = closed;
                Spectrum = spectrum;
            }
        }

        internal static GameObject Build(
            Transform parent,
            string name,
            IReadOnlyList<InlandWaterFootprintPlan.Point> points,
            in SplineStackSettings settings)
        {
            if (parent == null || points == null || points.Count < 2)
                return null;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var spline = root.AddComponent<Spline>();
            spline._offset = Spline.Offset.Center;
            spline._closed = settings.Closed;
            spline.Radius = settings.SplineRadius;
            spline.Subdivisions = settings.SplineSubdivisions;

            var heightInput = root.AddComponent<RegisterHeightInput>();
            heightInput.OverrideSplineSettings = true;
            heightInput.Radius = settings.HeightRadius;
            heightInput.Subdivisions = settings.HeightSubdivisions;
            var flowInput = root.AddComponent<RegisterFlowInput>();
            var waves = root.AddComponent<ShapeFFT>();
            waves._blendMode = ShapeWaves.ShapeBlendMode.Blend;
            waves._weight = 1f;
            waves._resolution = settings.WaveResolution;
            waves._windTurbulence = settings.WaveTurbulence;
            if (settings.Spectrum != null)
                waves._spectrum = settings.Spectrum;

            for (var i = 0; i < points.Count; i++)
                AddPoint(root.transform, points[i], settings);

            spline.UpdateSpline();
            CrestOceanConfigurator.EnableEditModeLodInput(heightInput);
            CrestOceanConfigurator.EnableEditModeLodInput(flowInput);
            CrestOceanConfigurator.EnableEditModeLodInput(waves);
            return root;
        }

        /// <summary>Adds one authored spline point; the radius multiplier is derived from the footprint width.</summary>
        private static void AddPoint(
            Transform parent,
            InlandWaterFootprintPlan.Point point,
            in SplineStackSettings settings)
        {
            var instance = new GameObject(SplinePointName);
            instance.transform.SetParent(parent, false);
            instance.transform.position = new Vector3(
                point.CenterXZ.x, point.SurfaceWorldY, point.CenterXZ.y);
            instance.AddComponent<SplinePoint>();
            instance.AddComponent<SplinePointData>().RadiusMultiplier =
                InlandWaterFootprintPlan.ResolveRadiusMultiplier(
                    point.TargetWetWidthMeters, settings.SplineRadius);
            instance.AddComponent<SplinePointDataFlow>().FlowVelocity = settings.PointFlowVelocity;
            instance.AddComponent<SplinePointDataWaves>().Weight = settings.PointWaveWeight;
        }

        /// <summary>Copies footprint centres into the flat XZ array scope helpers expect.</summary>
        internal static Vector2[] BuildScopePolyline(IReadOnlyList<InlandWaterFootprintPlan.Point> points)
        {
            var polyline = new Vector2[points.Count];
            for (var i = 0; i < points.Count; i++)
                polyline[i] = points[i].CenterXZ;
            return polyline;
        }

        internal static float ResolveMaxHalfWidth(IReadOnlyList<InlandWaterFootprintPlan.Point> points)
        {
            var max = 0.5f;
            for (var i = 0; i < points.Count; i++)
                max = Mathf.Max(max, points[i].TargetWetHalfWidthMeters);
            return max;
        }
    }
}
