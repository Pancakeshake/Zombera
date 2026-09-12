using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Crest cubic-ribbon validation for <see cref="InlandWaterFootprintPlan"/>: samples the ribbon
    /// at Crest's own interval and reconciles its edges with the carved waterline. Ribbon repair
    /// lives in the companion <c>InlandWaterFootprintPlan.RibbonRepair.cs</c>.
    /// </summary>
    public sealed partial class InlandWaterFootprintPlan
    {
        private const int EdgeValidationSamplesPerSide = 48;

        public InlandWaterFootprintReport ValidateCrestRibbon(
            LandformField field,
            InlandWaterFootprintOptions options,
            CrestRibbonValidationSettings crest,
            InlandWaterFootprintReport report = null)
        {
            report ??= new InlandWaterFootprintReport();
            report.Clear();
            Report = report;
            if (field == null)
                return report;

            var resolved = options.Normalized();
            var sampler = new CrestRibbonSampler();
            for (var f = 0; f < _features.Count; f++)
            {
                var feature = _features[f];
                if (feature?.Points == null || feature.Points.Count < 2)
                    continue;

                var radius = crest.SplineRadiusFor(feature.Kind);
                if (!sampler.TrySample(
                        BuildControlPositions(feature),
                        BuildControlMultipliers(feature, radius),
                        radius,
                        crest.SubdivisionsFor(feature.Kind),
                        feature.Closed))
                {
                    Report.Add(
                        feature.StableId, feature.Kind, 0,
                        feature.Points[0].CenterXZ,
                        InlandWaterFootprintFailure.DegenerateGeometry, 0f, 2f);
                    continue;
                }

                ValidateRibbonSamples(field, feature, sampler, resolved);
            }

            return report;
        }

        private void ValidateRibbonSamples(
            LandformField field,
            Feature feature,
            CrestRibbonSampler sampler,
            in InlandWaterFootprintOptions options)
        {
            for (var i = 0; i < sampler.Count; i++)
            {
                var center = sampler.Center(i);
                var originXZ = new Vector2(center.x, center.z);
                if (IsRibbonSampleSkipped(feature, sampler, i, originXZ, field))
                    continue;

                if (i > 0 && center.y > sampler.Center(i - 1).y + options.BankHeightToleranceMeters)
                {
                    Report.Add(
                        feature.StableId, feature.Kind, i, originXZ,
                        InlandWaterFootprintFailure.UphillOvershoot,
                        center.y - sampler.Center(i - 1).y,
                        options.BankHeightToleranceMeters);
                    continue;
                }

                ValidateRibbonSampleEdges(field, feature, sampler, i, options);
            }
        }

        private void ValidateRibbonSampleEdges(
            LandformField field,
            Feature feature,
            CrestRibbonSampler sampler,
            int index,
            in InlandWaterFootprintOptions options)
        {
            var center = sampler.Center(index);
            var originXZ = new Vector2(center.x, center.z);
            if (IsRibbonSampleSkipped(feature, sampler, index, originXZ, field))
                return;

            var normal = ResolveRibbonNormal(sampler, index);
            var clearance = ResolveRibbonClearance(feature, sampler, index, options);
            var surfaceY = ResolveControlSurfaceY(feature, sampler, index, center.y);
            var crossingTarget = surfaceY - clearance + options.BankHeightToleranceMeters;
            var halfWidth = sampler.HalfWidthMeters(index);
            var reach = Mathf.Max(halfWidth * 2f, halfWidth + options.MaximumBankExtensionMeters);
            var step = Mathf.Max(0.25f, reach / EdgeValidationSamplesPerSide);

            var left = FindBankCrossing(field, originXZ, normal, 1f, reach, step, crossingTarget);
            var right = FindBankCrossing(field, originXZ, normal, -1f, reach, step, crossingTarget);
            if (!IsOwnedRibbonCrossing(originXZ, normal, 1f, left))
                left = (false, 0f);
            if (!IsOwnedRibbonCrossing(originXZ, normal, -1f, right))
                right = (false, 0f);

            var centerHeight = LandformFieldSampling.SampleBilinear(field, originXZ.x, originXZ.y);
            var softLeft = (found: false, distance: 0f);
            var softRight = (found: false, distance: 0f);
            TryResolveSoftBanks(
                field, originXZ, normal, centerHeight, surfaceY, reach, step, options,
                ref softLeft, ref softRight);
            DiscardIncredibleBanks(ref softLeft, ref softRight);
            if (softLeft.found && !IsOwnedRibbonCrossing(originXZ, normal, 1f, softLeft))
                softLeft = (false, 0f);
            if (softRight.found && !IsOwnedRibbonCrossing(originXZ, normal, -1f, softRight))
                softRight = (false, 0f);
            PreferNearerSoftBanks(softLeft, softRight, ref left, ref right, options.LateralToleranceMeters);
            var softPreferred =
                (softLeft.found && left.found && Mathf.Abs(left.distance - softLeft.distance) < 0.01f) ||
                (softRight.found && right.found && Mathf.Abs(right.distance - softRight.distance) < 0.01f);
            if (!left.found)
                left = softLeft;
            if (!right.found)
                right = softRight;

            var leftOpen = !left.found;
            var rightOpen = !right.found;
            if (leftOpen && rightOpen)
                return;
            if (leftOpen)
                left = (true, right.distance);
            if (rightOpen)
                right = (true, left.distance);

            var measuredHalf = (left.distance + right.distance) * 0.5f;
            if (measuredHalf < MinCredibleBankMeters)
                return;

            // Soft trench = wet waterline; skip dig-shoulder offset (mid leftover trench artifact).
            var predictedHalf = halfWidth;
            if (!softPreferred)
            {
                predictedHalf += ResolveRibbonWaterlineOffset(
                    field, feature, sampler, index, originXZ, normal, surfaceY - clearance, options);
            }

            // Only a channel NARROWER than the rendered ribbon is a failure. Every repair path in
            // this loop is shrink-only (CapFeatureHalfWidth / AdoptControlHalfWidth /
            // InsertDriftedControls) and the carve only ever digs deeper, so terrain that is wider
            // than the waterline — a graded bank shoulder, or a leftover of an earlier, wider pass —
            // cannot be refilled by any bounded pass. Raising EdgeMismatch for it fails the stage on
            // a condition the loop can never change, while the ribbon still spans carved bed, not
            // dry land. The narrow direction is both the real defect and the repairable one.
            if (measuredHalf + options.LateralToleranceMeters >= predictedHalf)
                return;
            if (measuredHalf + options.LateralToleranceMeters < halfWidth)
            {
                CapFeatureHalfWidth(feature, measuredHalf);
                SyncSourceRiverWidths(feature);
            }

            Report.Add(
                feature.StableId, feature.Kind, index, originXZ,
                InlandWaterFootprintFailure.EdgeMismatch, measuredHalf, predictedHalf);
        }

        private static float ResolveControlSurfaceY(
            Feature feature,
            CrestRibbonSampler sampler,
            int sampleIndex,
            float fallbackY)
        {
            var control = ResolveControlIndex(sampleIndex, sampler);
            if (control < 0 || control >= feature.Points.Count)
                return fallbackY;
            return feature.Points[control].SurfaceWorldY;
        }

        private float ResolveRibbonWaterlineOffset(
            LandformField field,
            Feature feature,
            CrestRibbonSampler sampler,
            int sampleIndex,
            Vector2 originXZ,
            Vector2 normal,
            float bedLevel,
            in InlandWaterFootprintOptions options)
        {
            var control = ResolveControlIndex(sampleIndex, sampler);
            if (control < 0 || control >= feature.Points.Count)
                return 0f;

            var shoulder = Mathf.Max(0f, feature.Points[control].BankShoulderMeters);
            if (shoulder <= 0f)
                return 0f;

            var halfWidth = sampler.HalfWidthMeters(sampleIndex);
            var left = EstimateShoulderOffset(
                field, originXZ, normal, 1f, halfWidth, bedLevel, shoulder, options);
            var right = EstimateShoulderOffset(
                field, originXZ, normal, -1f, halfWidth, bedLevel, shoulder, options);
            return (left + right) * 0.5f;
        }

        private bool ShouldJudgeClearanceAt(Feature feature, Vector2 position) =>
            feature.Kind == FeatureKind.Lake ? IsLakeBedOwned(position) : IsCarveOwned(position);

        private bool IsOwnedRibbonCrossing(
            Vector2 originXZ,
            Vector2 normal,
            float direction,
            (bool found, float distance) crossing)
        {
            if (!crossing.found)
                return true;
            return IsCarveOwned(originXZ + normal * (direction * crossing.distance));
        }

        private bool IsRibbonSampleSkipped(
            Feature feature,
            CrestRibbonSampler sampler,
            int sampleIndex,
            Vector2 originXZ,
            LandformField field)
        {
            var control = ResolveControlIndex(sampleIndex, sampler);
            if (control >= 0 && control < feature.Points.Count && feature.Points[control].CarveSkipped)
                return true;
            if (!IsCarveOwned(originXZ))
                return true;
            return IsAboveCarveCeiling(originXZ, field);
        }

        private static float ResolveRibbonClearance(
            Feature feature,
            CrestRibbonSampler sampler,
            int sampleIndex,
            in InlandWaterFootprintOptions options)
        {
            var control = ResolveControlIndex(sampleIndex, sampler);
            var requested = control >= 0 && control < feature.Points.Count
                ? feature.Points[control].RequestedBedClearanceMeters
                : 0f;
            return Mathf.Max(Mathf.Max(options.MinimumBedClearanceMeters, requested), 0.05f);
        }

        private static Vector2 ResolveRibbonNormal(CrestRibbonSampler sampler, int index)
        {
            var before = sampler.Center(Mathf.Max(0, index - 1));
            var after = sampler.Center(Mathf.Min(sampler.Count - 1, index + 1));
            var tangent = new Vector2(after.x - before.x, after.z - before.z);
            if (tangent.sqrMagnitude < 1e-6f)
                return Vector2.up;
            tangent = tangent.normalized;
            return new Vector2(-tangent.y, tangent.x);
        }

        private static Vector3[] BuildControlPositions(Feature feature)
        {
            var positions = new Vector3[feature.Points.Count];
            for (var i = 0; i < positions.Length; i++)
            {
                var point = feature.Points[i];
                positions[i] = new Vector3(point.CenterXZ.x, point.SurfaceWorldY, point.CenterXZ.y);
            }

            return positions;
        }

        private static float[] BuildControlMultipliers(Feature feature, float splineRadius)
        {
            var multipliers = new float[feature.Points.Count];
            for (var i = 0; i < multipliers.Length; i++)
                multipliers[i] = ResolveRadiusMultiplier(feature.Points[i].TargetWetWidthMeters, splineRadius);
            return multipliers;
        }
    }
}
