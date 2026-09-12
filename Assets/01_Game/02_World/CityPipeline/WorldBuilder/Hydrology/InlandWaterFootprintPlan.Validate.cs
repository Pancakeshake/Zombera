using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Crest spline settings needed to evaluate the ribbon a feature will render.</summary>
    public readonly struct CrestRibbonValidationSettings
    {
        public readonly float RiverSplineRadius;
        public readonly int RiverSubdivisions;
        public readonly float LakeSplineRadius;
        public readonly int LakeSubdivisions;

        public CrestRibbonValidationSettings(
            float riverSplineRadius,
            int riverSubdivisions,
            float lakeSplineRadius,
            int lakeSubdivisions)
        {
            RiverSplineRadius = Mathf.Max(0.01f, riverSplineRadius);
            RiverSubdivisions = Mathf.Max(0, riverSubdivisions);
            LakeSplineRadius = Mathf.Max(0.01f, lakeSplineRadius);
            LakeSubdivisions = Mathf.Max(0, lakeSubdivisions);
        }

        /// <summary>Authored Crest reference values, matching the WorldWaterProfile defaults.</summary>
        public static CrestRibbonValidationSettings Default =>
            new(25f, 1, 20f, 1);

        public static CrestRibbonValidationSettings FromWater(WorldWaterProfile water) =>
            water == null
                ? Default
                : new CrestRibbonValidationSettings(
                    water.RiverSplineRadius,
                    water.RiverSplineSubdivisions,
                    water.LakeSplineRadius,
                    water.LakeSplineSubdivisions);

        public float SplineRadiusFor(InlandWaterFootprintPlan.FeatureKind kind) =>
            kind == InlandWaterFootprintPlan.FeatureKind.River ? RiverSplineRadius : LakeSplineRadius;

        public int SubdivisionsFor(InlandWaterFootprintPlan.FeatureKind kind) =>
            kind == InlandWaterFootprintPlan.FeatureKind.River ? RiverSubdivisions : LakeSubdivisions;
    }

    /// <summary>
    /// Carved-field validation for <see cref="InlandWaterFootprintPlan"/>: bed clearance across the
    /// wet footprint plus bank presence. Crest ribbon validation and repair live in the companion
    /// <c>InlandWaterFootprintPlan.Ribbon.cs</c> partial.
    /// </summary>
    public sealed partial class InlandWaterFootprintPlan
    {
        /// <summary>
        /// Bed clearance and bank presence across the wet footprint. These are the failures that
        /// would otherwise ship buried or floating water.
        /// </summary>
        public InlandWaterFootprintReport ValidateCarvedField(
            LandformField field,
            InlandWaterFootprintOptions options,
            InlandWaterFootprintReport report = null)
        {
            report ??= new InlandWaterFootprintReport();
            report.Clear();
            if (field == null)
            {
                Report = report;
                return report;
            }

            var resolved = options.Normalized();
            for (var f = 0; f < _features.Count; f++)
            {
                var feature = _features[f];
                if (feature?.Points == null)
                    continue;
                for (var p = 0; p < feature.Points.Count; p++)
                    ValidatePointClearance(field, feature, p, resolved);
            }

            Report = report;
            return report;
        }

        private void ValidatePointClearance(
            LandformField field,
            Feature feature,
            int index,
            in InlandWaterFootprintOptions options)
        {
            var point = feature.Points[index];
            // Ocean/lake cells are owned by another feature, a lake spine can sit a cell off its
            // stamped basin, and above the carve ceiling the carver never enforced a bed: none of
            // those has a bed this feature is answerable for.
            if (point.CarveSkipped)
                return;
            if (!ShouldJudgeClearanceAt(feature, point.CenterXZ))
                return;
            if (IsAboveCarveCeiling(point.CenterXZ, field))
                return;

            var clearance = Mathf.Max(point.RequestedBedClearanceMeters, options.MinimumBedClearanceMeters);
            var bedLevel = point.SurfaceWorldY - clearance;
            var normal = ResolveBankDirection(feature.Points, index);
            var halfWidth = Mathf.Max(0.5f, point.TargetWetHalfWidthMeters);
            var worst = float.NegativeInfinity;

            for (var s = -2; s <= 2; s++)
            {
                var offset = normal * (halfWidth * s * 0.25f);
                var position = point.CenterXZ + offset;
                if (!ShouldJudgeClearanceAt(feature, position))
                    continue;
                worst = Mathf.Max(worst, LandformFieldSampling.SampleBilinear(field, position.x, position.y));
            }

            if (float.IsNegativeInfinity(worst))
                return;

            if (worst > bedLevel + options.BankHeightToleranceMeters)
            {
                Report.Add(
                    feature.StableId, feature.Kind, index, point.CenterXZ,
                    InlandWaterFootprintFailure.BedClearance, worst - bedLevel, clearance);
            }

            if (!point.HasMissingBank)
                return;
            Report.Add(
                feature.StableId, feature.Kind, index, point.CenterXZ,
                InlandWaterFootprintFailure.MissingBank,
                Mathf.Max(point.MeasuredLeftMeters, point.MeasuredRightMeters),
                halfWidth);
        }
    }
}
