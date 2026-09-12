using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Applies river/lake/ocean carving into a landform height field and hydrology plan.</summary>
    public static class HydrologyCarver
    {
        public static void Carve(LandformField field, HydrologyPlan plan, HydrologyProfile profile) =>
            Carve(field, plan, profile, InlandWaterFootprintOptions.Default);

        public static void Carve(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            InlandWaterFootprintOptions options)
        {
            if (field == null || plan == null || profile == null) return;
            var resolved = options.Normalized();
            var footprint = plan.EnsureFootprintPlan(profile, resolved);
            var carveStrength = Mathf.Clamp(profile.RiverCarveStrength, 0f, 1f);

            CarveOcean(field, plan, profile.SeaLevelWorldY);
            CarveLakes(field, plan, profile);
            CarveRivers(field, plan, profile, carveStrength);
            ApplyInlandBathymetry(field, plan, profile, updatePlanDepths: true);
            footprint.FitToCarvedField(field, resolved);
            RefreshDistanceField(plan);
        }

        public static void Carve(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            bool[] exclusionMask,
            bool[] oceanMask = null) =>
            Carve(field, plan, profile, exclusionMask, oceanMask, InlandWaterFootprintOptions.Default);

        public static void Carve(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            bool[] exclusionMask,
            bool[] oceanMask,
            InlandWaterFootprintOptions options)
        {
            if (field == null || plan == null || profile == null) return;
            var resolved = options.Normalized();
            var footprint = plan.EnsureFootprintPlan(profile, resolved);
            var carveStrength = Mathf.Clamp(profile.RiverCarveStrength, 0f, 1f);

            CarveOcean(field, plan, profile.SeaLevelWorldY, oceanMask);
            CarveLakes(field, plan, profile, exclusionMask);
            CarveRivers(field, plan, profile, exclusionMask, carveStrength);
            ApplyInlandBathymetry(field, plan, profile, updatePlanDepths: true);
            footprint.FitToCarvedField(field, resolved);
            RefreshDistanceField(plan);
        }

        /// <summary>Height-only carve before a full <see cref="HydrologySolver.Solve"/> refresh.</summary>
        public static void CarveHeights(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            bool[] exclusionMask)
        {
            if (field == null || plan == null || profile == null) return;
            plan.EnsureFootprintPlan(profile, InlandWaterFootprintOptions.Default);

            CarveOceanHeights(field, profile.SeaLevelWorldY);
            CarveLakeHeights(field, plan, profile, exclusionMask);
            CarveRiverHeights(field, plan, profile, exclusionMask, profile.RiverCarveStrength);
            ApplyInlandBathymetry(field, plan, profile, updatePlanDepths: false);
        }

        /// <summary>
        /// Ensures inland water beds have minimum clearance below their preserved surfaces.
        /// Only applies to low-land water cells (no mountain slots).
        /// DepthMeters stays stamp/gameplay-scale.
        /// </summary>
        private static void ApplyInlandBathymetry(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            bool updatePlanDepths)
        {
            var sea = profile.SeaLevelWorldY;
            var maxLandY = sea + profile.MaxRiverLandElevationAboveSeaMeters;
            var riverClearance = Mathf.Max(0.25f, profile.MinRiverBedDepthBelowSea);
            var lakeClearance = Mathf.Max(0.5f, profile.MinLakeBedDepthBelowSea);
            var maxGameplayDepth = Mathf.Max(profile.MaxRiverDepthMeters, profile.LakeMinimumDepthMeters);

            var count = Mathf.Min(plan.WaterClass.Length, field.WorldHeights.Length);
            for (var i = 0; i < count; i++)
            {
                var waterClass = plan.WaterClass[i];
                if (waterClass != WorldWaterClass.River && waterClass != WorldWaterClass.Lake)
                    continue;
                if (field.WorldHeights[i] > maxLandY)
                    continue;

                var clearance = waterClass == WorldWaterClass.Lake ? lakeClearance : riverClearance;
                var bedFloor = plan.SurfaceWorldY[i] - clearance;
                field.WorldHeights[i] = Mathf.Min(field.WorldHeights[i], bedFloor);
                if (!updatePlanDepths)
                    continue;

                var stampDepth = plan.DepthMeters[i];
                if (stampDepth <= 0f)
                {
                    stampDepth = waterClass == WorldWaterClass.Lake
                        ? profile.LakeMinimumDepthMeters * 0.5f
                        : profile.MinRiverDepthMeters;
                }

                plan.DepthMeters[i] = Mathf.Min(stampDepth, maxGameplayDepth);
            }
        }

        private static void CarveOceanHeights(LandformField field, float seaLevel)
        {
            for (var z = 0; z < field.Height; z++)
            {
                for (var x = 0; x < field.Width; x++)
                {
                    var fi = field.Index(x, z);
                    if (field.WorldHeights[fi] > seaLevel) continue;
                    field.WorldHeights[fi] = Mathf.Min(field.WorldHeights[fi], seaLevel - 0.25f);
                }
            }
        }

        private static void CarveOcean(LandformField field, HydrologyPlan plan, float seaLevel) =>
            CarveOcean(field, plan, seaLevel, oceanMask: null);

        private static void CarveOcean(
            LandformField field,
            HydrologyPlan plan,
            float seaLevel,
            bool[] oceanMask)
        {
            if (oceanMask != null && oceanMask.Length == field.Width * field.Height)
            {
                for (var i = 0; i < oceanMask.Length; i++)
                {
                    if (!oceanMask[i]) continue;
                    if (i >= plan.WaterClass.Length) continue;

                    plan.WaterClass[i] = WorldWaterClass.Ocean;
                    plan.SurfaceWorldY[i] = seaLevel;
                    plan.DepthMeters[i] = seaLevel - field.WorldHeights[i];
                    field.WorldHeights[i] = Mathf.Min(field.WorldHeights[i], seaLevel - 0.25f);
                }

                return;
            }

            for (var z = 0; z < field.Height && z < plan.Height; z++)
            {
                for (var x = 0; x < field.Width && x < plan.Width; x++)
                {
                    var fi = field.Index(x, z);
                    var pi = plan.Index(x, z);
                    if (plan.WaterClass[pi] != WorldWaterClass.Ocean &&
                        field.WorldHeights[fi] > seaLevel)
                        continue;

                    plan.WaterClass[pi] = WorldWaterClass.Ocean;
                    plan.SurfaceWorldY[pi] = seaLevel;
                    plan.DepthMeters[pi] = seaLevel - field.WorldHeights[fi];
                    field.WorldHeights[fi] = Mathf.Min(field.WorldHeights[fi], seaLevel - 0.25f);
                }
            }
        }

        private static void CarveLakes(LandformField field, HydrologyPlan plan, HydrologyProfile profile) =>
            CarveLakes(field, plan, profile, exclusionMask: null);

        private static void CarveLakeHeights(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            bool[] exclusionMask)
        {
            if (plan.Lakes == null) return;
            for (var l = 0; l < plan.Lakes.Length; l++)
            {
                var lake = plan.Lakes[l];
                if (lake == null) continue;
                StampLakeCells(field, plan, lake, exclusionMask, markWater: false);
            }
        }

        private static void CarveLakes(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            bool[] exclusionMask)
        {
            if (plan.Lakes == null) return;
            for (var l = 0; l < plan.Lakes.Length; l++)
            {
                var lake = plan.Lakes[l];
                if (lake == null) continue;
                StampLakeCells(field, plan, lake, exclusionMask, markWater: true);
            }
        }

        private static void StampLakeCells(
            LandformField field,
            HydrologyPlan plan,
            LakeRecord lake,
            bool[] exclusionMask,
            bool markWater)
        {
            var cells = lake.BasinCellCentersXZ;
            if (cells == null) return;
            for (var c = 0; c < cells.Length; c++)
            {
                var x = Mathf.FloorToInt((cells[c].x - field.OriginXZ.x) / field.CellSize);
                var z = Mathf.FloorToInt((cells[c].y - field.OriginXZ.y) / field.CellSize);
                if (x < 0 || z < 0 || x >= plan.Width || z >= plan.Height)
                    continue;
                var fi = field.Index(x, z);
                var pi = plan.Index(x, z);
                if (plan.WaterClass[pi] == WorldWaterClass.Ocean || IsExcluded(field, x, z, exclusionMask))
                    continue;
                if (markWater)
                {
                    plan.WaterClass[pi] = WorldWaterClass.Lake;
                    plan.SurfaceWorldY[pi] = lake.SurfaceWorldY;
                    plan.DepthMeters[pi] = Mathf.Max(plan.DepthMeters[pi], lake.MaxDepthMeters * 0.5f);
                }
                field.WorldHeights[fi] = Mathf.Min(field.WorldHeights[fi], lake.SurfaceWorldY - 0.2f);
            }
        }

        private static void CarveRiverHeights(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            bool[] exclusionMask,
            float carveStrength)
        {
            if (plan.Rivers == null) return;
            var shoulder = Mathf.Max(0f, profile.CarveShoulderWidthMeters);
            var strength = Mathf.Clamp(carveStrength, 0f, 1f);

            for (var r = 0; r < plan.Rivers.Length; r++)
            {
                var river = plan.Rivers[r];
                if (river?.PointsXZ == null || river.PointsXZ.Length < 2) continue;

                for (var p = 0; p < river.PointsXZ.Length; p++)
                {
                    var width = ResolveRiverWidth(plan, river, p, profile.MinRiverWidthMeters);
                    var halfWidth = Mathf.Max(0.5f, width * 0.5f);
                    var clearance = ResolveRiverClearance(plan, river, p, profile.MinRiverBedDepthBelowSea);
                    var surfaceY = ResolveRiverSurfaceY(plan, river, p, profile.SeaLevelWorldY);
                    var depth = p < river.DepthMeters.Length ? river.DepthMeters[p] : profile.MinRiverDepthMeters;
                    StampDiscHeights(
                        field, plan, profile, river.PointsXZ[p], halfWidth, shoulder, depth, clearance,
                        surfaceY, exclusionMask, strength);
                }
            }
        }

        private static void CarveRivers(LandformField field, HydrologyPlan plan, HydrologyProfile profile) =>
            CarveRivers(field, plan, profile, exclusionMask: null, carveStrength: profile.RiverCarveStrength);

        private static void CarveRivers(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            bool[] exclusionMask,
            float carveStrength)
        {
            if (plan.Rivers == null) return;
            var shoulder = Mathf.Max(0f, profile.CarveShoulderWidthMeters);
            var strength = Mathf.Clamp(carveStrength, 0f, 1f);

            for (var r = 0; r < plan.Rivers.Length; r++)
            {
                var river = plan.Rivers[r];
                if (river?.PointsXZ == null || river.PointsXZ.Length < 2) continue;

                for (var p = 0; p < river.PointsXZ.Length; p++)
                {
                    var width = ResolveRiverWidth(plan, river, p, profile.MinRiverWidthMeters);
                    var halfWidth = Mathf.Max(0.5f, width * 0.5f);
                    var clearance = ResolveRiverClearance(plan, river, p, profile.MinRiverBedDepthBelowSea);
                    var surfaceY = ResolveRiverSurfaceY(plan, river, p, profile.SeaLevelWorldY);
                    var depth = p < river.DepthMeters.Length ? river.DepthMeters[p] : profile.MinRiverDepthMeters;
                    StampDisc(field, plan, profile, river.PointsXZ[p], halfWidth, shoulder, depth, clearance,
                        surfaceY, exclusionMask, strength);
                }
            }
        }

        private static void CarveRivers(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            float carveStrength) =>
            CarveRivers(field, plan, profile, exclusionMask: null, carveStrength);

        private static void StampDiscHeights(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            Vector2 centerXZ,
            float wetHalfWidth,
            float shoulder,
            float depth,
            float bedClearance,
            float riverSurfaceWorldY,
            bool[] exclusionMask,
            float carveStrength)
        {
            StampRiverDisc(field, plan, profile, centerXZ, wetHalfWidth, shoulder, depth, bedClearance,
                riverSurfaceWorldY, exclusionMask, carveStrength, markWater: false);
        }

        private static void StampDisc(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            Vector2 centerXZ,
            float wetHalfWidth,
            float shoulder,
            float depth,
            float bedClearance,
            float riverSurfaceWorldY,
            bool[] exclusionMask,
            float carveStrength)
        {
            StampRiverDisc(field, plan, profile, centerXZ, wetHalfWidth, shoulder, depth, bedClearance,
                riverSurfaceWorldY, exclusionMask, carveStrength, markWater: true);
        }

        /// <summary>Per-disc constants resolved once so the per-cell work stays flat.</summary>
        private readonly struct RiverDiscSettings
        {
            public readonly float HalfBed;
            public readonly float Radius;
            public readonly float RadiusSq;
            public readonly float Strength;
            public readonly float MaxLandY;
            public readonly float BedClearance;

            public RiverDiscSettings(
                float halfBed,
                float radius,
                float strength,
                float maxLandY,
                float bedClearance)
            {
                HalfBed = halfBed;
                Radius = radius;
                RadiusSq = radius * radius;
                Strength = strength;
                MaxLandY = maxLandY;
                BedClearance = bedClearance;
            }
        }

        /// <summary>
        /// Footprint-shaped bank profile: full requested depth from the centreline through the whole
        /// target wet half-width, the configurable shoulder starting outside it, then a smooth
        /// falloff to untouched terrain. Only the wet footprint is stamped as river water, so the
        /// carved waterline edge and the Crest ribbon edge describe the same width.
        /// </summary>
        private static void StampRiverDisc(
            LandformField field,
            HydrologyPlan plan,
            HydrologyProfile profile,
            Vector2 centerXZ,
            float wetHalfWidth,
            float shoulder,
            float depth,
            float bedClearance,
            float riverSurfaceWorldY,
            bool[] exclusionMask,
            float carveStrength,
            bool markWater)
        {
            var settings = new RiverDiscSettings(
                halfBed: Mathf.Max(0.5f, wetHalfWidth),
                radius: Mathf.Max(0.5f, wetHalfWidth) + Mathf.Max(0f, shoulder),
                strength: Mathf.Clamp(carveStrength, 0f, 1f),
                maxLandY: profile.SeaLevelWorldY + profile.MaxRiverLandElevationAboveSeaMeters,
                bedClearance: Mathf.Max(0.25f, bedClearance));

            var minX = Mathf.FloorToInt((centerXZ.x - settings.Radius - field.OriginXZ.x) / field.CellSize);
            var maxX = Mathf.CeilToInt((centerXZ.x + settings.Radius - field.OriginXZ.x) / field.CellSize);
            var minZ = Mathf.FloorToInt((centerXZ.y - settings.Radius - field.OriginXZ.y) / field.CellSize);
            var maxZ = Mathf.CeilToInt((centerXZ.y + settings.Radius - field.OriginXZ.y) / field.CellSize);

            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (!IsCellInRange(field, plan, x, z)) continue;

                    var pi = plan.Index(x, z);
                    if (plan.WaterClass[pi] == WorldWaterClass.Ocean) continue;
                    if (plan.WaterClass[pi] == WorldWaterClass.Lake) continue;
                    if (IsExcluded(field, x, z, exclusionMask)) continue;

                    var distSq = (field.CellCenterXZ(x, z) - centerXZ).sqrMagnitude;
                    if (distSq > settings.RadiusSq) continue;

                    var fi = field.Index(x, z);
                    var before = field.WorldHeights[fi];
                    if (before > settings.MaxLandY) continue;

                    var dist = Mathf.Sqrt(distSq);
                    var falloff = ResolveBankFalloff(dist, in settings);
                    var carve = depth * (1f - falloff);
                    // Reference the bed to the river's preserved free surface, not the local terrain:
                    // otherwise a fringe cell on a high bank is dug only relative to itself and stays
                    // above the waterline, leaving the channel's edge unsubmerged.
                    var surfaceWorldY = plan.WaterClass[pi] == WorldWaterClass.River
                        ? plan.SurfaceWorldY[pi]
                        : riverSurfaceWorldY;

                    var targetBed = Mathf.Min(before - carve * settings.Strength, surfaceWorldY - settings.BedClearance);
                    // A carve only ever digs: overlapping discs must never raise terrain back up.
                    field.WorldHeights[fi] = Mathf.Min(
                        field.WorldHeights[fi], Mathf.Lerp(targetBed, before, falloff));

                    if (!markWater || dist > settings.HalfBed)
                        continue;
                    plan.WaterClass[pi] = WorldWaterClass.River;
                    plan.DepthMeters[pi] = Mathf.Max(plan.DepthMeters[pi], carve);
                    plan.SurfaceWorldY[pi] = surfaceWorldY;
                }
            }
        }

        /// <summary>True when the cell exists in both the landform field and the hydrology plan.</summary>
        private static bool IsCellInRange(LandformField field, HydrologyPlan plan, int x, int z) =>
            x >= 0 && z >= 0 && x < field.Width && z < field.Height && x < plan.Width && z < plan.Height;

        /// <summary>Zero across the wet half-width, then a smooth rise to untouched over the shoulder.</summary>
        private static float ResolveBankFalloff(float dist, in RiverDiscSettings settings) =>
            dist <= settings.HalfBed
                ? 0f
                : SmoothStep(Mathf.InverseLerp(settings.HalfBed, settings.Radius, dist));

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static void RefreshDistanceField(HydrologyPlan plan) =>
            HydrologyDistanceField.Compute(plan);

        private static float ResolveRiverWidth(
            HydrologyPlan plan,
            RiverPolyline river,
            int pointIndex,
            float fallback)
        {
            if (plan?.FootprintPlan != null &&
                plan.FootprintPlan.TryGetRiverPointWidth(river.StableId, pointIndex, out var planned))
                return planned;
            return river?.WidthMeters != null && pointIndex >= 0 && pointIndex < river.WidthMeters.Length
                ? Mathf.Max(0.01f, river.WidthMeters[pointIndex])
                : Mathf.Max(0.01f, fallback);
        }

        /// <summary>
        /// The footprint's preserved free surface for a river control, so the carve and validation
        /// agree on what the water level is. Falls back to the supplied level when unavailable.
        /// </summary>
        private static float ResolveRiverSurfaceY(
            HydrologyPlan plan,
            RiverPolyline river,
            int pointIndex,
            float fallback)
        {
            if (plan?.FootprintPlan != null &&
                plan.FootprintPlan.TryGetFeature(river.StableId, out var feature) &&
                feature.TryGetPointBySourceIndex(pointIndex, out var point))
                return point.SurfaceWorldY;
            return fallback;
        }

        /// <summary>Per-point bed clearance from the footprint, so the carve honours the plan.</summary>
        private static float ResolveRiverClearance(
            HydrologyPlan plan,
            RiverPolyline river,
            int pointIndex,
            float fallback)
        {
            if (plan?.FootprintPlan != null &&
                plan.FootprintPlan.TryGetRiverPointClearance(river.StableId, pointIndex, out var planned))
                return planned;
            return Mathf.Max(0.25f, fallback);
        }

        private static bool IsExcluded(LandformField field, int x, int z, bool[] exclusionMask)
        {
            if (exclusionMask == null) return false;
            var i = field.Index(x, z);
            return i < exclusionMask.Length && exclusionMask[i];
        }
    }
}
