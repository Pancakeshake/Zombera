using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Carved-waterline width-drift repair for <see cref="InlandWaterFootprintPlan"/>: inserts
    /// controls where the carved bed drifted away from the rendered Crest ribbon and adopts the
    /// measured bed width on the owning control. The uphill-overshoot fixpoint lives in the companion
    /// <c>InlandWaterFootprintPlan.RibbonRepair.cs</c>.
    /// </summary>
    public sealed partial class InlandWaterFootprintPlan
    {
        /// <summary>
        /// Inserts controls where the carved waterline drifted from the rendered ribbon. Runs before
        /// the uphill fixpoint so the width repair still sees the soft dig depth the prior validate
        /// measured.
        /// </summary>
        private int RepairDriftedWidths(
            LandformField field,
            CrestRibbonValidationSettings crest,
            in InlandWaterFootprintOptions options)
        {
            var inserted = 0;
            var sampler = new CrestRibbonSampler();
            for (var f = 0; f < _features.Count; f++)
            {
                var feature = _features[f];
                if (feature?.Points == null || feature.Points.Count < 2)
                    continue;

                if (!TrySampleFeature(feature, crest, sampler))
                    continue;

                inserted += InsertDriftedControls(field, feature, sampler, options);
            }

            return inserted;
        }

        private int InsertDriftedControls(
            LandformField field,
            Feature feature,
            CrestRibbonSampler sampler,
            in InlandWaterFootprintOptions options)
        {
            var inserts = new List<RibbonInsert>();
            CollectDriftedControls(field, feature, sampler, options, inserts);
            return ApplyRepairInserts(feature, inserts, MaxRibbonRepairInserts);
        }

        private void CollectDriftedControls(
            LandformField field,
            Feature feature,
            CrestRibbonSampler sampler,
            in InlandWaterFootprintOptions options,
            List<RibbonInsert> inserts)
        {
            for (var i = 0; i < sampler.Count; i++)
            {
                var center = sampler.Center(i);
                var origin = new Vector2(center.x, center.z);
                // Inserting controls for undug runoff or an ocean/lake-owned cell would chase an
                // edge that was never carved.
                if (IsRibbonSampleSkipped(feature, sampler, i, origin, field))
                    continue;

                var normal = ResolveRibbonNormal(sampler, i);
                var clearance = ResolveRibbonClearance(feature, sampler, i, options);
                var surfaceY = ResolveControlSurfaceY(feature, sampler, i, center.y);
                var crossingTarget = surfaceY - clearance + options.BankHeightToleranceMeters;
                var halfWidth = sampler.HalfWidthMeters(i);
                var reach = Mathf.Max(halfWidth * 2f, halfWidth + options.MaximumBankExtensionMeters);
                var step = Mathf.Max(0.25f, reach / EdgeValidationSamplesPerSide);

                var left = FindBankCrossing(field, origin, normal, 1f, reach, step, crossingTarget);
                var right = FindBankCrossing(field, origin, normal, -1f, reach, step, crossingTarget);
                if (!IsOwnedRibbonCrossing(origin, normal, 1f, left))
                    left = (false, 0f);
                if (!IsOwnedRibbonCrossing(origin, normal, -1f, right))
                    right = (false, 0f);

                var centerHeight = LandformFieldSampling.SampleBilinear(field, origin.x, origin.y);
                var softLeft = (found: false, distance: 0f);
                var softRight = (found: false, distance: 0f);
                TryResolveSoftBanks(
                    field, origin, normal, centerHeight, surfaceY, reach, step, options,
                    ref softLeft, ref softRight);
                DiscardIncredibleBanks(ref softLeft, ref softRight);
                if (softLeft.found && !IsOwnedRibbonCrossing(origin, normal, 1f, softLeft))
                    softLeft = (false, 0f);
                if (softRight.found && !IsOwnedRibbonCrossing(origin, normal, -1f, softRight))
                    softRight = (false, 0f);
                PreferNearerSoftBanks(softLeft, softRight, ref left, ref right, options.LateralToleranceMeters);
                if (!left.found)
                    left = softLeft;
                if (!right.found)
                    right = softRight;

                if (!left.found && !right.found)
                    continue;
                if (!left.found)
                    left = (true, right.distance);
                if (!right.found)
                    right = (true, left.distance);

                var measuredHalf = (left.distance + right.distance) * 0.5f;
                if (measuredHalf < MinCredibleBankMeters)
                    continue;

                var bedLevel = surfaceY - clearance;
                var waterlineOffset = ResolveRibbonWaterlineOffset(
                    field, feature, sampler, i, origin, normal, bedLevel, options);
                var predictedHalf = halfWidth + waterlineOffset;
                if (Mathf.Abs(measuredHalf - predictedHalf) <= options.LateralToleranceMeters)
                    continue;

                // Adopt the carved bed half-width on the owning control so the next carve digs the
                // same span Crest will render — inserts alone cannot shrink SourceIndex widths.
                var bedHalf = Mathf.Max(MinCredibleBankMeters, measuredHalf - waterlineOffset);
                if (bedHalf >= halfWidth - options.LateralToleranceMeters)
                    continue;
                var control = ResolveControlIndex(i, sampler);
                AdoptControlHalfWidth(feature, control, bedHalf);

                inserts.Add(new RibbonInsert(
                    control + 1,
                    origin + normal * ((left.distance - right.distance) * 0.5f),
                    bedHalf,
                    surfaceY));
            }
        }

        private static void AdoptControlHalfWidth(Feature feature, int controlIndex, float bedHalfWidth)
        {
            if (feature?.Points == null || feature.Points.Count == 0)
                return;
            if (bedHalfWidth < MinCredibleBankMeters)
                return;
            if (controlIndex < 0 || controlIndex >= feature.Points.Count)
                return;

            CapNeighbourhoodHalfWidth(feature, controlIndex, bedHalfWidth);
            feature.Points[controlIndex].TargetWetHalfWidthMeters = bedHalfWidth;
        }
    }
}
