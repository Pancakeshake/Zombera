using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Ordered inland water geometry shared by terrain carving, Crest spline generation and road
    /// protection. One instance is built per hydrology plan so the rendered ribbon and the carved
    /// hole can never disagree about a channel's centre, surface or width.
    /// <para>
    /// Width contract: <see cref="Point.TargetWetWidthMeters"/> is the <b>full</b> wet span in
    /// metres. Crest renders <c>Spline.Radius * RadiusMultiplier</c>, therefore
    /// <see cref="ResolveRadiusMultiplier"/> is the only correct conversion.
    /// </para>
    /// </summary>
    public sealed partial class InlandWaterFootprintPlan
    {
        public enum FeatureKind : byte
        {
            River = 0,
            Lake = 1
        }

        /// <summary>Why a control point exists. Pinned roles survive simplification.</summary>
        [Flags]
        public enum PointRole : byte
        {
            None = 0,
            Source = 1 << 0,
            Mouth = 1 << 1,
            Junction = 1 << 2,
            LakeConnection = 1 << 3,
            MajorBend = 1 << 4,
            WidthExtremum = 1 << 5,
            ElevationTransition = 1 << 6,
            /// <summary>Added while fitting/repairing; free to be simplified away later.</summary>
            Inserted = 1 << 7
        }

        /// <summary>Roles that must not be removed by control-point simplification.</summary>
        public const PointRole PinnedRoleMask =
            PointRole.Source | PointRole.Mouth | PointRole.Junction |
            PointRole.LakeConnection | PointRole.MajorBend |
            PointRole.WidthExtremum | PointRole.ElevationTransition;

        /// <summary>Turn angle above which a river vertex counts as a major bend.</summary>
        public const float MajorBendDegrees = 25f;

        /// <summary>Minimum vertical step over a segment that counts as an elevation transition.</summary>
        public const float ElevationTransitionMeters = 2f;

        [Serializable]
        public sealed class Point
        {
            public Vector2 CenterXZ;
            public float SurfaceWorldY;

            /// <summary>Half of the desired wet waterline span in metres.</summary>
            public float TargetWetHalfWidthMeters;

            public float BankShoulderMeters;
            public float RequestedBedClearanceMeters;
            public bool IsJunction;
            public PointRole Role;

            /// <summary>
            /// Index of this point in the source <see cref="RiverPolyline.PointsXZ"/>. River features
            /// are reordered upstream-to-mouth, so the carver must map back through this instead of
            /// assuming list position matches the river's own arrays.
            /// </summary>
            public int SourceIndex;

            /// <summary>
            /// True when the river carver deliberately declines to dig this point's cell because the
            /// ocean or a lake already owns it (an ocean mouth, or a river reaching a lake). The bed
            /// there is provided by that other feature, so fit/validation must skip it instead of
            /// demanding a river bed that will never be carved.
            /// </summary>
            public bool CarveSkipped;

            /// <summary>Measured wet half-spans from the last <see cref="FitToCarvedField"/> pass.</summary>
            public float MeasuredLeftMeters;
            public float MeasuredRightMeters;

            public bool LeftBankMissing;
            public bool RightBankMissing;

            public float TargetWetWidthMeters => Mathf.Max(0f, TargetWetHalfWidthMeters * 2f);

            public bool IsPinned => (Role & PinnedRoleMask) != 0;

            public bool HasMissingBank => LeftBankMissing || RightBankMissing;

            public Point Clone() => new()
            {
                CenterXZ = CenterXZ,
                SurfaceWorldY = SurfaceWorldY,
                TargetWetHalfWidthMeters = TargetWetHalfWidthMeters,
                BankShoulderMeters = BankShoulderMeters,
                RequestedBedClearanceMeters = RequestedBedClearanceMeters,
                IsJunction = IsJunction,
                Role = Role,
                SourceIndex = SourceIndex,
                CarveSkipped = CarveSkipped,
                MeasuredLeftMeters = MeasuredLeftMeters,
                MeasuredRightMeters = MeasuredRightMeters,
                LeftBankMissing = LeftBankMissing,
                RightBankMissing = RightBankMissing
            };

            public void ClearMeasurements()
            {
                MeasuredLeftMeters = 0f;
                MeasuredRightMeters = 0f;
                LeftBankMissing = false;
                RightBankMissing = false;
            }
        }

        [Serializable]
        public sealed class Feature
        {
            public ulong StableId;
            public FeatureKind Kind;
            public bool Closed;
            public readonly List<Point> Points = new();
            public float SurfaceWorldY;

            public bool IsRiver => Kind == FeatureKind.River;

            /// <summary>
            /// Resolves a source <see cref="RiverPolyline"/> index to its control point. River points
            /// are reordered upstream-to-mouth and the list is then simplified, spaced and repaired, so
            /// this scans rather than caching a map that any edit would invalidate (build-time only).
            /// </summary>
            public bool TryGetPointBySourceIndex(int sourceIndex, out Point point)
            {
                for (var i = 0; i < Points.Count; i++)
                {
                    if (Points[i].SourceIndex != sourceIndex)
                        continue;
                    point = Points[i];
                    return true;
                }

                point = null;
                return false;
            }
        }

        private readonly List<Feature> _features = new();
        private readonly Dictionary<ulong, Feature> _byId = new();

        public IReadOnlyList<Feature> Features => _features;

        /// <summary>Diagnostics from the most recent fit/validation pass.</summary>
        public InlandWaterFootprintReport Report { get; private set; } = new();

        /// <summary>
        /// Above this terrain height the river carver deliberately declines to dig (mountain
        /// runoff protection), so river points there are skipped instead of reported as failures.
        /// </summary>
        public float CarveCeilingWorldY { get; set; } = float.PositiveInfinity;

        /// <summary>
        /// Source hydrology, used to tell river-owned cells from ocean/lake cells. The ocean mask
        /// reaches a wide coastal strip, so a river's last points can overlap the ocean: the bed
        /// there is the ocean's, not the river's, and must not be validated as a river bank.
        /// </summary>
        public HydrologyPlan SourcePlan { get; private set; }

        /// <summary>
        /// True when the river carve owns this cell, i.e. it is neither ocean nor lake. Out-of-range
        /// points count as owned so map-edge and tile-seam cases are still validated.
        /// </summary>
        public bool IsCarveOwned(Vector2 worldXZ)
        {
            if (SourcePlan?.WaterClass == null)
                return true;
            var x = Mathf.FloorToInt((worldXZ.x - SourcePlan.OriginXZ.x) / SourcePlan.CellSizeMeters);
            var z = Mathf.FloorToInt((worldXZ.y - SourcePlan.OriginXZ.y) / SourcePlan.CellSizeMeters);
            if (x < 0 || z < 0 || x >= SourcePlan.Width || z >= SourcePlan.Height)
                return true;
            var waterClass = SourcePlan.WaterClass[SourcePlan.Index(x, z)];
            return waterClass != WorldWaterClass.Ocean && waterClass != WorldWaterClass.Lake;
        }

        /// <summary>
        /// True when this cell really is part of the lake basin. A lake's medial spine reaches the
        /// traced outline while the stamped basin is rasterised about a cell smaller, so clearance
        /// must only be judged on cells the lake flood actually owns.
        /// </summary>
        public bool IsLakeBedOwned(Vector2 worldXZ)
        {
            if (SourcePlan?.WaterClass == null)
                return true;
            var x = Mathf.FloorToInt((worldXZ.x - SourcePlan.OriginXZ.x) / SourcePlan.CellSizeMeters);
            var z = Mathf.FloorToInt((worldXZ.y - SourcePlan.OriginXZ.y) / SourcePlan.CellSizeMeters);
            if (x < 0 || z < 0 || x >= SourcePlan.Width || z >= SourcePlan.Height)
                return false;
            return SourcePlan.WaterClass[SourcePlan.Index(x, z)] == WorldWaterClass.Lake;
        }

        /// <summary>Feature lookup by stable id, for the Crest generators.</summary>
        public bool TryGetFeature(ulong stableId, out Feature feature) => _byId.TryGetValue(stableId, out feature);

        public static InlandWaterFootprintPlan Build(HydrologyPlan plan, HydrologyProfile profile) =>
            Build(plan, profile, InlandWaterFootprintOptions.Default);

        public static InlandWaterFootprintPlan Build(
            HydrologyPlan plan,
            HydrologyProfile profile,
            InlandWaterFootprintOptions options)
        {
            var result = new InlandWaterFootprintPlan();
            if (plan == null || profile == null)
                return result;

            result.SourcePlan = plan;
            AddRivers(result, plan, profile);
            AddLakes(result, plan, profile);
            result.ApplyAdaptiveControlPoints(options);
            return result;
        }

        public bool TryGet(ulong stableId, out Feature feature) => _byId.TryGetValue(stableId, out feature);

        public bool TryGetRiverPointWidth(ulong riverId, int pointIndex, out float widthMeters)
        {
            widthMeters = 0f;
            if (!_byId.TryGetValue(riverId, out var feature) || feature.Kind != FeatureKind.River)
                return false;
            if (!feature.TryGetPointBySourceIndex(pointIndex, out var point))
                return false;
            widthMeters = point.TargetWetWidthMeters;
            return widthMeters > 0f;
        }

        /// <summary>Per-point bed clearance for a source river index, so the carve honours the plan.</summary>
        public bool TryGetRiverPointClearance(ulong riverId, int pointIndex, out float clearanceMeters)
        {
            clearanceMeters = 0f;
            if (!_byId.TryGetValue(riverId, out var feature) || feature.Kind != FeatureKind.River)
                return false;
            if (!feature.TryGetPointBySourceIndex(pointIndex, out var point))
                return false;
            clearanceMeters = Mathf.Max(0.25f, point.RequestedBedClearanceMeters);
            return true;
        }

        /// <summary>Converts a measured full wet span to Crest's authored multiplier.</summary>
        public static float ResolveRadiusMultiplier(float targetWetWidthMeters, float splineRadius)
        {
            return Mathf.Max(0.01f, targetWetWidthMeters / Mathf.Max(0.01f, splineRadius));
        }

        private static void AddRivers(
            InlandWaterFootprintPlan result,
            HydrologyPlan plan,
            HydrologyProfile profile)
        {
            if (plan.Rivers == null)
                return;
            for (var r = 0; r < plan.Rivers.Length; r++)
            {
                var river = plan.Rivers[r];
                if (river?.PointsXZ == null || river.PointsXZ.Length == 0)
                    continue;
                AddFeature(result, BuildRiverFeature(plan, river, profile));
            }
        }

        private static Feature BuildRiverFeature(
            HydrologyPlan plan,
            RiverPolyline river,
            HydrologyProfile profile)
        {
            var feature = new Feature
            {
                StableId = river.StableId,
                Kind = FeatureKind.River,
                Closed = false
            };

            var count = river.PointsXZ.Length;
            var fallback = ResolveInitialSurface(plan, river, profile.SeaLevelWorldY);
            for (var i = 0; i < count; i++)
            {
                var width = HydrologyPolylineSampler.ResolveWidthMeters(river.WidthMeters, i);
                var depth = HydrologyPolylineSampler.ResolveWidthMeters(river.DepthMeters, i);
                feature.Points.Add(new Point
                {
                    CenterXZ = river.PointsXZ[i],
                    SurfaceWorldY = ResolveRiverSurface(plan, river.PointsXZ[i], fallback),
                    TargetWetHalfWidthMeters = Mathf.Max(0.5f, width * 0.5f),
                    BankShoulderMeters = Mathf.Max(0f, profile.CarveShoulderWidthMeters),
                    RequestedBedClearanceMeters = Mathf.Max(profile.MinRiverBedDepthBelowSea, depth * 0.1f),
                    SourceIndex = i,
                    CarveSkipped = IsCarveSkippedAt(plan, river.PointsXZ[i])
                });
            }

            OrderUpstreamToMouth(river, feature.Points);
            MarkRiverRoles(river, feature.Points);
            EnforceRiverHeights(river, feature.Points, profile.SeaLevelWorldY);
            feature.SurfaceWorldY = feature.Points[0].SurfaceWorldY;
            return feature;
        }

        /// <summary>
        /// Orders river controls upstream-to-mouth so a positive flow velocity always pushes
        /// water downstream, matching the authored Crest reference.
        /// </summary>
        private static void OrderUpstreamToMouth(RiverPolyline river, List<Point> points)
        {
            if (points.Count < 2)
                return;

            bool reverse;
            if (river.HasOceanMouth)
            {
                var first = (points[0].CenterXZ - river.OceanMouthXZ).sqrMagnitude;
                var last = (points[points.Count - 1].CenterXZ - river.OceanMouthXZ).sqrMagnitude;
                reverse = first < last;
            }
            else
            {
                reverse = points[0].SurfaceWorldY < points[points.Count - 1].SurfaceWorldY;
            }

            if (!reverse)
                return;

            points.Reverse();
        }

        private static void MarkRiverRoles(RiverPolyline river, List<Point> points)
        {
            var last = points.Count - 1;
            points[0].Role |= PointRole.Source;
            points[last].Role |= PointRole.Mouth;

            if (river.SourceLakeStableId != 0)
                points[0].Role |= PointRole.LakeConnection;
            if (river.JoinLakeStableId != 0)
                points[last].Role |= PointRole.LakeConnection;

            MarkConfluence(river, points);
            MarkBends(points);
            MarkWidthExtrema(points);
            MarkElevationTransitions(points);
        }

        private static void MarkConfluence(RiverPolyline river, List<Point> points)
        {
            if (!river.HasConfluence)
                return;

            var bestIndex = -1;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < points.Count; i++)
            {
                var distance = (points[i].CenterXZ - river.ConfluenceXZ).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return;
            points[bestIndex].Role |= PointRole.Junction;
            points[bestIndex].IsJunction = true;
        }

        private static void MarkBends(List<Point> points)
        {
            for (var i = 1; i < points.Count - 1; i++)
            {
                var turn = TurnDegrees(points, i);
                if (turn < MajorBendDegrees)
                    continue;
                if (turn < TurnDegrees(points, i - 1) || turn < TurnDegrees(points, i + 1))
                    continue;
                points[i].Role |= PointRole.MajorBend;
            }
        }

        private static float TurnDegrees(List<Point> points, int index)
        {
            if (index <= 0 || index >= points.Count - 1)
                return 0f;
            var incoming = points[index].CenterXZ - points[index - 1].CenterXZ;
            var outgoing = points[index + 1].CenterXZ - points[index].CenterXZ;
            return incoming.sqrMagnitude < 0.01f || outgoing.sqrMagnitude < 0.01f
                ? 0f
                : Vector2.Angle(incoming, outgoing);
        }

        private static void MarkWidthExtrema(List<Point> points)
        {
            for (var i = 1; i < points.Count - 1; i++)
            {
                var width = points[i].TargetWetHalfWidthMeters;
                var before = points[i - 1].TargetWetHalfWidthMeters;
                var after = points[i + 1].TargetWetHalfWidthMeters;
                var isMin = width <= before && width <= after && (width < before || width < after);
                var isMax = width >= before && width >= after && (width > before || width > after);
                if (isMin || isMax)
                    points[i].Role |= PointRole.WidthExtremum;
            }
        }

        private static void MarkElevationTransitions(List<Point> points)
        {
            for (var i = 1; i < points.Count; i++)
            {
                var drop = Mathf.Abs(points[i].SurfaceWorldY - points[i - 1].SurfaceWorldY);
                if (drop < ElevationTransitionMeters)
                    continue;
                points[i].Role |= PointRole.ElevationTransition;
                points[i - 1].Role |= PointRole.ElevationTransition;
            }
        }

        /// <summary>
        /// Forces non-increasing downstream heights and pins an ocean mouth to sea level so a
        /// spline can never render a step up into the ocean.
        /// </summary>
        private static void EnforceRiverHeights(
            RiverPolyline river,
            List<Point> points,
            float seaLevelWorldY)
        {
            if (river.HasOceanMouth && points.Count > 0)
                points[points.Count - 1].SurfaceWorldY = seaLevelWorldY;

            for (var i = points.Count - 2; i >= 0; i--)
                points[i].SurfaceWorldY = Mathf.Max(points[i].SurfaceWorldY, points[i + 1].SurfaceWorldY);
        }

        /// <summary>
        /// True when a river disc would skip this cell because the ocean or a lake already owns it.
        /// </summary>
        private static bool IsCarveSkippedAt(HydrologyPlan plan, Vector2 worldXZ)
        {
            if (plan?.WaterClass == null)
                return false;
            var x = Mathf.FloorToInt((worldXZ.x - plan.OriginXZ.x) / plan.CellSizeMeters);
            var z = Mathf.FloorToInt((worldXZ.y - plan.OriginXZ.y) / plan.CellSizeMeters);
            if (x < 0 || z < 0 || x >= plan.Width || z >= plan.Height)
                return false;
            var waterClass = plan.WaterClass[plan.Index(x, z)];
            return waterClass == WorldWaterClass.Ocean || waterClass == WorldWaterClass.Lake;
        }

        private static float ResolveInitialSurface(
            HydrologyPlan plan,
            RiverPolyline river,
            float seaLevelWorldY)
        {
            if (HydrologyPolylineSampler.TrySampleSurfaceY(plan, river.PointsXZ[0], out var sampled))
                return sampled;
            return seaLevelWorldY;
        }

        /// <summary>
        /// Preserved free surface at a river control. Never returns a literal zero: a point with
        /// no wet cell falls back to the nearest wet cell, then to the feature/sea level surface.
        /// </summary>
        private static float ResolveRiverSurface(HydrologyPlan plan, Vector2 point, float fallback)
        {
            if (HydrologyPolylineSampler.TrySampleSurfaceY(plan, point, out var sampled))
                return sampled;
            return fallback;
        }

        private static void AddFeature(InlandWaterFootprintPlan result, Feature feature)
        {
            if (feature == null || feature.Points.Count == 0)
                return;
            if (result._byId.ContainsKey(feature.StableId))
                feature.StableId ^= (ulong)result._features.Count + 1UL;
            result._features.Add(feature);
            result._byId[feature.StableId] = feature;
        }
    }
}
