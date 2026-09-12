using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Adopts EdgeMismatch ribbon measurements into the footprint plan so repair can shrink an
    /// oversized MaxRiverWidth channel when Flatten would otherwise kill soft dig-depth.
    /// </summary>
    public sealed partial class InlandWaterFootprintPlan
    {
        /// <summary>
        /// Shrinks features from prior EdgeMismatch measurements and syncs source river WidthMeters.
        /// </summary>
        public int AdoptEdgeMismatchWidths(InlandWaterFootprintReport report)
        {
            if (report?.Issues == null || report.Issues.Count == 0)
                return 0;

            var adopted = 0;
            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                if (issue.Failure != InlandWaterFootprintFailure.EdgeMismatch)
                    continue;
                if (issue.MeasuredMeters < MinCredibleBankMeters)
                    continue;
                if (!_byId.TryGetValue(issue.StableId, out var feature))
                    continue;

                var before = MaxFeatureHalfWidth(feature);
                CapFeatureHalfWidth(feature, issue.MeasuredMeters);
                SyncSourceRiverWidths(feature);
                if (MaxFeatureHalfWidth(feature) < before - 0.01f)
                    adopted++;
            }

            if (adopted > 0)
                Debug.Log(
                    $"[InlandWaterFootprint] AdoptEdgeMismatchWidths updates={adopted} " +
                    $"issues={report.CountOf(InlandWaterFootprintFailure.EdgeMismatch)}");
            return adopted;
        }

        private static float MaxFeatureHalfWidth(Feature feature)
        {
            var max = 0f;
            for (var i = 0; i < feature.Points.Count; i++)
                max = Mathf.Max(max, feature.Points[i].TargetWetHalfWidthMeters);
            return max;
        }

        /// <summary>Largest planned river half-width — used by carve repair progress logs.</summary>
        public float MaxRiverHalfWidthMeters()
        {
            var max = 0f;
            for (var f = 0; f < _features.Count; f++)
            {
                var feature = _features[f];
                if (feature?.Points == null || feature.Kind != FeatureKind.River)
                    continue;
                max = Mathf.Max(max, MaxFeatureHalfWidth(feature));
            }

            return max;
        }

        private void SyncSourceRiverWidths(Feature feature)
        {
            if (feature.Kind != FeatureKind.River || SourcePlan?.Rivers == null)
                return;
            for (var r = 0; r < SourcePlan.Rivers.Length; r++)
            {
                var river = SourcePlan.Rivers[r];
                if (river?.WidthMeters == null || river.StableId != feature.StableId)
                    continue;
                for (var i = 0; i < feature.Points.Count; i++)
                {
                    var point = feature.Points[i];
                    if (point.SourceIndex < 0 || point.SourceIndex >= river.WidthMeters.Length)
                        continue;
                    river.WidthMeters[point.SourceIndex] = point.TargetWetWidthMeters;
                }

                return;
            }
        }
    }
}
