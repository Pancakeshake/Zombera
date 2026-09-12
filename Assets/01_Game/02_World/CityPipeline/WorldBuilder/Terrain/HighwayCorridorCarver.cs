using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Coarse highway corridor shaping on the landform planning grid.</summary>
    public static class HighwayCorridorCarver
    {
        private const float SoftPinEntryRadiusMeters = 48f;
        private const float FillCapAllowanceMeters = 0.35f;

        private struct CorridorCarveContext
        {
            public LandformField Field;
            public RoadPolyline Road;
            public HydrologyPlan Hydrology;
            public IReadOnlyList<MountainTunnel> Tunnels;
            public PolylineHeightProfile Profile;
            public float HalfWidth;
            public float Blend;
            public float Radius;
            public float LateralSlop;
            public float MaxReclaimDepthMeters;
            public int X0;
            public int X1;
            public int Z0;
            public int Z1;
        }

        private struct CarveInputs
        {
            public LandformField Field;
            public RoadPolyline Road;
            public RoadNetworkSettings Settings;
            public HydrologyPlan Hydrology;
            public IReadOnlyList<CityFlattenPad> PadAnchors;
            public float MaxReclaimDepthMeters;
            public IReadOnlyList<MountainTunnel> Tunnels;
        }

        public struct HighwayCarveOptions
        {
            public HydrologyPlan Hydrology;
            public IReadOnlyList<CityFlattenPad> PadAnchors;
            public float MaxReclaimDepthMeters;
            public IReadOnlyList<MountainTunnel> Tunnels;
        }

        public static int Carve(
            LandformField field,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            HighwayCarveOptions options = default)
        {
            if (field == null || roads == null || roads.Count == 0 || settings == null)
                return 0;

            var carved = 0;
            for (var r = 0; r < roads.Count; r++)
            {
                var road = roads[r];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                var inputs = new CarveInputs
                {
                    Field = field,
                    Road = road,
                    Settings = settings,
                    Hydrology = options.Hydrology,
                    PadAnchors = options.PadAnchors,
                    MaxReclaimDepthMeters = options.MaxReclaimDepthMeters,
                    Tunnels = options.Tunnels
                };
                if (CarveHighway(inputs))
                    carved++;
            }

            return carved;
        }

        /// <summary>
        ///     Rasterize highway corridor cells (shoulder + blend) into a full-field bool mask.
        /// </summary>
        public static bool[] RasterizeCorridorMask(
            LandformField field,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings)
        {
            if (field?.WorldHeights == null)
                return null;

            var mask = new bool[field.WorldHeights.Length];
            if (roads == null || settings == null)
                return mask;

            for (var r = 0; r < roads.Count; r++)
            {
                var road = roads[r];
                if (road?.roadClass != RoadClass.Highway || road.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                var width = road.widthMeters > 0f
                    ? road.widthMeters
                    : settings.ResolveWidthMeters(RoadClass.Highway);
                var shoulder = Mathf.Max(settings.highwayTerrainShoulderMeters, settings.terrainShoulderMeters);
                var blend = Mathf.Max(
                    settings.highwayCoarseCorridorBlendMeters,
                    settings.highwayTerrainBlendMeters * 0.35f);
                var halfWidth = width * 0.5f + shoulder;
                var radius = halfWidth + blend;

                var profile = PolylineHeightProfile.Build(
                    road.pointsXZ,
                    xz => LandformFieldSampling.SampleBilinear(field, xz.x, xz.y),
                    resampleSpacingMeters: Mathf.Max(8f, field.CellSize),
                    smoothingIterations: 0,
                    maxSlopeDegrees: 0f);
                if (profile == null || !profile.IsValid)
                    continue;

                RasterizeProfile(field, profile, radius, mask);
            }

            return mask;
        }

        private static void RasterizeProfile(
            LandformField field,
            PolylineHeightProfile profile,
            float radius,
            bool[] mask)
        {
            var bounds = profile.BoundsXZ;
            var expanded = Rect.MinMaxRect(
                bounds.xMin - radius,
                bounds.yMin - radius,
                bounds.xMax + radius,
                bounds.yMax + radius);
            var x0 = Mathf.Max(0, Mathf.FloorToInt((expanded.xMin - field.OriginXZ.x) / field.CellSize));
            var x1 = Mathf.Min(field.Width - 1, Mathf.CeilToInt((expanded.xMax - field.OriginXZ.x) / field.CellSize));
            var z0 = Mathf.Max(0, Mathf.FloorToInt((expanded.yMin - field.OriginXZ.y) / field.CellSize));
            var z1 = Mathf.Min(field.Height - 1, Mathf.CeilToInt((expanded.yMax - field.OriginXZ.y) / field.CellSize));

            for (var z = z0; z <= z1; z++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var center = field.CellCenterXZ(x, z);
                    if (profile.DistanceToPath(center, out _) > radius)
                        continue;
                    mask[field.Index(x, z)] = true;
                }
            }
        }

        private static bool CarveHighway(in CarveInputs inputs)
        {
            if (!TryBuildCorridorContext(inputs, out var ctx))
                return false;

            ApplyCorridorCells(ctx);
            return true;
        }

        private static bool TryBuildCorridorContext(in CarveInputs inputs, out CorridorCarveContext ctx)
        {
            ctx = default;
            var field = inputs.Field;
            var road = inputs.Road;
            var settings = inputs.Settings;
            var padAnchors = inputs.PadAnchors;

            var width = road.widthMeters > 0f
                ? road.widthMeters
                : settings.ResolveWidthMeters(RoadClass.Highway);
            var shoulder = Mathf.Max(settings.highwayTerrainShoulderMeters, settings.terrainShoulderMeters);
            var blend = Mathf.Max(settings.highwayCoarseCorridorBlendMeters, settings.highwayTerrainBlendMeters * 0.35f);
            var halfWidth = width * 0.5f + shoulder;
            var radius = halfWidth + blend;
            var useSoftPin = padAnchors != null && padAnchors.Count > 0;

            System.Func<Vector2, float> sampleHeight = xz =>
            {
                if (TryResolvePadAnchorHeight(padAnchors, xz, out var padY))
                    return padY;
                return LandformFieldSampling.SampleBilinear(field, xz.x, xz.y);
            };

            System.Func<Vector2, bool> softPinAt = useSoftPin
                ? xz => IsSoftPinStation(padAnchors, xz)
                : null;

            // Terrain-follow only: slope-clamped design beds are reserved for tunnel detection.
            var profile = PolylineHeightProfile.Build(
                road.pointsXZ,
                sampleHeight,
                resampleSpacingMeters: Mathf.Max(8f, field.CellSize),
                smoothingIterations: 1,
                maxSlopeDegrees: 0f,
                pinEndpoints: false,
                softPinAt: softPinAt);
            if (profile == null || !profile.IsValid)
                return false;

            var bounds = profile.BoundsXZ;
            var expanded = Rect.MinMaxRect(
                bounds.xMin - radius,
                bounds.yMin - radius,
                bounds.xMax + radius,
                bounds.yMax + radius);

            ctx = new CorridorCarveContext
            {
                Field = field,
                Road = road,
                Hydrology = inputs.Hydrology,
                Tunnels = inputs.Tunnels,
                Profile = profile,
                HalfWidth = halfWidth,
                Blend = blend,
                Radius = radius,
                LateralSlop = halfWidth + 4f,
                MaxReclaimDepthMeters = inputs.MaxReclaimDepthMeters,
                X0 = Mathf.Max(0, Mathf.FloorToInt((expanded.xMin - field.OriginXZ.x) / field.CellSize)),
                X1 = Mathf.Min(field.Width - 1, Mathf.CeilToInt((expanded.xMax - field.OriginXZ.x) / field.CellSize)),
                Z0 = Mathf.Max(0, Mathf.FloorToInt((expanded.yMin - field.OriginXZ.y) / field.CellSize)),
                Z1 = Mathf.Min(field.Height - 1, Mathf.CeilToInt((expanded.yMax - field.OriginXZ.y) / field.CellSize))
            };
            return true;
        }

        private static void ApplyCorridorCells(in CorridorCarveContext ctx)
        {
            for (var z = ctx.Z0; z <= ctx.Z1; z++)
            {
                for (var x = ctx.X0; x <= ctx.X1; x++)
                    TryCarveCell(ctx, x, z);
            }
        }

        private static void TryCarveCell(in CorridorCarveContext ctx, int x, int z)
        {
            if (CityPadReclaimPolicy.ShouldSkipWaterForTerrainWrite(
                    ctx.Hydrology, x, z, ctx.MaxReclaimDepthMeters))
                return;

            var center = ctx.Field.CellCenterXZ(x, z);
            if (IsTunnelCoreSkip(ctx.Tunnels, ctx.Road.id, center, ctx.LateralSlop))
                return;

            var dist = ctx.Profile.DistanceToPath(center, out _);
            if (dist > ctx.Radius)
                return;

            var weight = CorridorWeight(dist, ctx.HalfWidth, ctx.Blend);
            if (weight <= 0f)
                return;

            var i = ctx.Field.Index(x, z);
            var current = ctx.Field.WorldHeights[i];
            var targetY = ctx.Profile.SampleHeightAlongPath(center);
            // Global fill-cap: never raise corridors into embankment walls/plateaus.
            if (targetY > current + FillCapAllowanceMeters)
                targetY = current + FillCapAllowanceMeters;

            ctx.Field.WorldHeights[i] = Mathf.Lerp(current, targetY, weight);
        }

        private static bool IsTunnelCoreSkip(
            IReadOnlyList<MountainTunnel> tunnels,
            int roadId,
            Vector2 center,
            float lateralSlop)
        {
            if (tunnels == null || tunnels.Count == 0)
                return false;

            for (var i = 0; i < tunnels.Count; i++)
            {
                var tunnel = tunnels[i];
                if (tunnel.RoadId != 0 && tunnel.RoadId != roadId)
                    continue;
                if (tunnel.IsInCoreCarveSkip(center, lateralSlop))
                    return true;
            }

            return false;
        }

        private static bool IsSoftPinStation(IReadOnlyList<CityFlattenPad> pads, Vector2 worldXZ)
        {
            if (TryResolvePadAnchorHeight(pads, worldXZ, out _))
                return true;

            if (pads == null)
                return false;

            for (var i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                if (pad == null || !pad.HasHighwayEntry)
                    continue;
                if (Vector2.Distance(worldXZ, pad.HighwayEntryXZ) <= SoftPinEntryRadiusMeters)
                    return true;
            }

            return false;
        }

        private static bool TryResolvePadAnchorHeight(
            IReadOnlyList<CityFlattenPad> pads,
            Vector2 worldXZ,
            out float padY)
        {
            padY = 0f;
            if (pads == null)
                return false;

            for (var i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                if (pad == null)
                    continue;
                if (worldXZ.x < pad.PlateauBoundsXZ.xMin || worldXZ.x > pad.PlateauBoundsXZ.xMax ||
                    worldXZ.y < pad.PlateauBoundsXZ.yMin || worldXZ.y > pad.PlateauBoundsXZ.yMax)
                    continue;

                padY = pad.TargetHeightWorldY;
                return true;
            }

            return false;
        }

        private static float CorridorWeight(float distance, float halfWidth, float blend)
        {
            if (distance <= halfWidth)
                return 1f;
            if (blend <= 0.01f)
                return 0f;

            var t = 1f - Mathf.Clamp01((distance - halfWidth) / blend);
            return t * t * (3f - 2f * t);
        }
    }
}
