using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Concrete reasons a water footprint can fail validation.</summary>
    public enum InlandWaterFootprintFailure : byte
    {
        None = 0,

        /// <summary>
        /// No waterline crossing within reach because the channel was never dug (see bed clearance).
        /// Open floodplain (terrain never rises within reach on a dug bed) is not a failure.
        /// </summary>
        MissingBank = 1,

        /// <summary>
        /// The carved channel is narrower than the rendered ribbon, so the ribbon edge would hang
        /// over dry bank. Only this direction is reported: repair can only shrink the plan and the
        /// carve only digs, so a channel <i>wider</i> than the ribbon (a graded bank shoulder, or a
        /// leftover from an earlier wider pass) is not repairable and must not fail the build.
        /// </summary>
        EdgeMismatch = 2,

        /// <summary>Terrain inside the wet footprint is not deep enough below the surface.</summary>
        BedClearance = 3,

        /// <summary>Cubic spline interpolation rises above the upstream sample (water runs uphill).</summary>
        UphillOvershoot = 4,

        /// <summary>A control point could not be placed or retained for a valid feature.</summary>
        DegenerateGeometry = 5
    }

    /// <summary>One feature-localised failure, carrying the id and world position for diagnostics.</summary>
    public readonly struct InlandWaterFootprintIssue
    {
        public readonly ulong StableId;
        public readonly InlandWaterFootprintPlan.FeatureKind Kind;
        public readonly int PointIndex;
        public readonly Vector2 WorldXZ;
        public readonly InlandWaterFootprintFailure Failure;
        public readonly float MeasuredMeters;
        public readonly float LimitMeters;

        public InlandWaterFootprintIssue(
            ulong stableId,
            InlandWaterFootprintPlan.FeatureKind kind,
            int pointIndex,
            Vector2 worldXZ,
            InlandWaterFootprintFailure failure,
            float measuredMeters,
            float limitMeters)
        {
            StableId = stableId;
            Kind = kind;
            PointIndex = pointIndex;
            WorldXZ = worldXZ;
            Failure = failure;
            MeasuredMeters = measuredMeters;
            LimitMeters = limitMeters;
        }

        public string Describe() =>
            $"{(Kind == InlandWaterFootprintPlan.FeatureKind.River ? "river" : "lake")} {StableId} " +
            $"point {PointIndex} {Failure} measured {MeasuredMeters:F2}m limit {LimitMeters:F2}m " +
            $"at ({WorldXZ.x:F1}, {WorldXZ.y:F1})";
    }

    /// <summary>Accumulated fit/validation diagnostics for one footprint plan.</summary>
    public sealed class InlandWaterFootprintReport
    {
        private readonly List<InlandWaterFootprintIssue> _issues = new();

        public IReadOnlyList<InlandWaterFootprintIssue> Issues => _issues;

        public bool IsClean => _issues.Count == 0;

        /// <summary>Repair pass that produced this report (1-based).</summary>
        public int Pass { get; internal set; }

        /// <summary>Control points inserted while repairing this pass.</summary>
        public int InsertedPoints { get; internal set; }

        /// <summary>Control points whose bank was extended to force a re-carve.</summary>
        public int ExtendedBanks { get; internal set; }

        /// <summary>Control heights flattened to remove an irreducible uphill interpolation run.</summary>
        public int FlattenedRuns { get; internal set; }

        public void Clear()
        {
            _issues.Clear();
            InsertedPoints = 0;
            ExtendedBanks = 0;
            FlattenedRuns = 0;
        }

        public void Add(in InlandWaterFootprintIssue issue) => _issues.Add(issue);

        public void Add(
            ulong stableId,
            InlandWaterFootprintPlan.FeatureKind kind,
            int pointIndex,
            Vector2 worldXZ,
            InlandWaterFootprintFailure failure,
            float measuredMeters,
            float limitMeters) =>
            _issues.Add(new InlandWaterFootprintIssue(
                stableId, kind, pointIndex, worldXZ, failure, measuredMeters, limitMeters));

        public int CountOf(InlandWaterFootprintFailure failure)
        {
            var count = 0;
            for (var i = 0; i < _issues.Count; i++)
            {
                if (_issues[i].Failure == failure)
                    count++;
            }

            return count;
        }

        public string Describe(int maxIssues = 4)
        {
            if (_issues.Count == 0)
                return "no footprint issues";

            var text = $"{_issues.Count} footprint issue(s)";
            var limit = Mathf.Min(maxIssues, _issues.Count);
            for (var i = 0; i < limit; i++)
                text += "; " + _issues[i].Describe();
            if (_issues.Count > limit)
                text += $"; (+{_issues.Count - limit} more)";
            return text;
        }
    }
}
