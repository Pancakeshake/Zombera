using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Crest ribbon repair for <see cref="InlandWaterFootprintPlan"/>: width-drift control insertion
    /// plus a bounded uphill-overshoot fixpoint.
    /// <para>
    /// The fixpoint re-samples the Crest ribbon after every height-repair sweep. A one-shot sweep
    /// decided from the sample it had just invalidated, so small overshoots (0.27–0.46 m against the
    /// 0.25 m bank tolerance) survived every stage pass and failed the build closed. Each sweep now
    /// lowers the controls that shape an uphill run, and falls back to inserting a locally clamped
    /// control when the rise is pure spline curvature with nothing left to lower.
    /// </para>
    /// </summary>
    public sealed partial class InlandWaterFootprintPlan
    {
        /// <summary>Safety limit on the control points one repair call may insert.</summary>
        private const int MaxRibbonRepairInserts = 256;

        /// <summary>
        /// Sweep budget for the uphill-overshoot fixpoint. The loop also stops as soon as a sweep
        /// finds nothing left to repair or the insert budget is exhausted.
        /// </summary>
        private const int MaxRibbonRepairSweeps = 16;

        /// <summary>One queued control insertion, applied after the sweep that collected it.</summary>
        private readonly struct RibbonInsert
        {
            public readonly int Index;
            public readonly Vector2 CenterXZ;
            public readonly float HalfWidthMeters;
            public readonly float SurfaceWorldY;

            public RibbonInsert(int index, Vector2 centerXZ, float halfWidthMeters, float surfaceWorldY)
            {
                Index = index;
                CenterXZ = centerXZ;
                HalfWidthMeters = halfWidthMeters;
                SurfaceWorldY = surfaceWorldY;
            }
        }

        /// <summary>
        /// Repairs the ribbon the carve loop validates: adopts prior edge mismatches, inserts controls
        /// where the carved waterline has drifted, then runs the bounded uphill-overshoot fixpoint.
        /// The returned report is measured from the repaired plan, never from the sample the sweeps
        /// started out with.
        /// </summary>
        public InlandWaterFootprintReport RepairCrestRibbon(
            LandformField field,
            InlandWaterFootprintOptions options,
            CrestRibbonValidationSettings crest,
            InlandWaterFootprintReport priorReport = null)
        {
            var resolved = options.Normalized();
            AdoptEdgeMismatchWidths(priorReport);

            // Width drift first: it must run while soft dig-depth still matches the prior validate.
            var inserted = RepairDriftedWidths(field, crest, resolved);

            var flattened = 0;
            var overshootInserts = 0;
            for (var sweep = 0; sweep < MaxRibbonRepairSweeps; sweep++)
            {
                var budget = MaxRibbonRepairInserts - overshootInserts;
                if (budget <= 0)
                    break;

                var sweepResult = RepairUphillOvershoots(field, resolved, crest, budget);
                flattened += sweepResult.Lowered;
                overshootInserts += sweepResult.Inserted;
                if (sweepResult.Lowered == 0 && sweepResult.Inserted == 0)
                    break;
            }

            var report = ValidateCrestRibbon(field, resolved, crest);
            report.InsertedPoints = inserted + overshootInserts;
            report.FlattenedRuns = flattened;
            return report;
        }

        /// <summary>Inserts controls where the carved waterline drifted from the rendered ribbon.</summary>
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

                var radius = crest.SplineRadiusFor(feature.Kind);
                if (!sampler.TrySample(
                        BuildControlPositions(feature),
                        BuildControlMultipliers(feature, radius),
                        radius,
                        crest.SubdivisionsFor(feature.Kind),
                        feature.Closed))
                    continue;

                inserted += InsertDriftedControls(field, feature, sampler, options);
            }

            return inserted;
        }

        /// <summary>
        /// One fixpoint sweep: re-samples every feature and repairs its uphill runs. Returns how many
        /// controls were lowered and how many were inserted.
        /// </summary>
        private (int Lowered, int Inserted) RepairUphillOvershoots(
            LandformField field,
            in InlandWaterFootprintOptions options,
            CrestRibbonValidationSettings crest,
            int insertBudget)
        {
            var lowered = 0;
            var inserted = 0;
            var sampler = new CrestRibbonSampler();
            var inserts = new List<RibbonInsert>(4);
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
                    continue;

                var budget = insertBudget - inserted;
                lowered += CollectUphillRunRepairs(field, feature, sampler, options, inserts, budget);
                inserted += ApplyRepairInserts(feature, inserts, budget);
                inserts.Clear();
            }

            return (lowered, inserted);
        }

        /// <summary>
        /// Walks the sampled ribbon and repairs every maximal uphill run. Runs are maximal so one
        /// crest is repaired once instead of once per flagged sample.
        /// </summary>
        private int CollectUphillRunRepairs(
            LandformField field,
            Feature feature,
            CrestRibbonSampler sampler,
            in InlandWaterFootprintOptions options,
            List<RibbonInsert> inserts,
            int insertBudget)
        {
            var lowered = 0;
            for (var i = 1; i < sampler.Count; i++)
            {
                if (!IsUphillSample(feature, sampler, i, field, options))
                    continue;

                var end = i;
                while (end + 1 < sampler.Count &&
                       IsUphillSample(feature, sampler, end + 1, field, options))
                    end++;

                lowered += RepairUphillRun(feature, sampler, i, end, inserts, insertBudget);
                i = end;
            }

            return lowered;
        }

        /// <summary>Judges a sample exactly as <c>ValidateCrestRibbon</c> does.</summary>
        private bool IsUphillSample(
            Feature feature,
            CrestRibbonSampler sampler,
            int index,
            LandformField field,
            in InlandWaterFootprintOptions options)
        {
            if (index <= 0 || index >= sampler.Count)
                return false;
            var center = sampler.Center(index);
            if (center.y <= sampler.Center(index - 1).y + options.BankHeightToleranceMeters)
                return false;
            return !IsRibbonSampleSkipped(feature, sampler, index, new Vector2(center.x, center.z), field);
        }

        /// <summary>
        /// Removes the convexity that lets the cubic ribbon bulge into an uphill run. A control above
        /// the midpoint of its neighbours is the shape that overshoots, so it is lowered to that
        /// midpoint: the local drop is redistributed over the neighbouring spans instead of being
        /// moved upstream, the water surface never rises, and the downstream gradient is preserved.
        /// When every mapped control already sits at or below the midpoint the rise is pure curvature,
        /// so a locally clamped control is queued instead.
        /// </summary>
        private int RepairUphillRun(
            Feature feature,
            CrestRibbonSampler sampler,
            int startSample,
            int endSample,
            List<RibbonInsert> inserts,
            int insertBudget)
        {
            var baseWorldY = sampler.Center(startSample - 1).y;
            var from = Mathf.Max(0, ResolveControlIndex(startSample - 1, sampler) - 1);
            var to = Mathf.Min(feature.Points.Count - 1, ResolveControlIndex(endSample, sampler) + 1);
            var lowered = LowerControlsAboveNeighbourMidpoint(feature, from, to);
            if (lowered > 0 || inserts.Count >= insertBudget)
                return lowered;

            QueueClampedControl(feature, sampler, startSample, endSample, baseWorldY, inserts);
            return lowered;
        }

        /// <summary>
        /// Lowers every control in <c>[from, to]</c> that sits above the midpoint of its neighbours.
        /// This can never invert the downstream gradient: the midpoint of a non-increasing triple is
        /// always at or above its downstream value.
        /// </summary>
        private static int LowerControlsAboveNeighbourMidpoint(Feature feature, int from, int to)
        {
            var lowered = 0;
            for (var c = from; c <= to; c++)
            {
                if (c < 0 || c >= feature.Points.Count)
                    continue;

                var point = feature.Points[c];
                var before = feature.Points[Mathf.Max(0, c - 1)].SurfaceWorldY;
                var after = feature.Points[Mathf.Min(feature.Points.Count - 1, c + 1)].SurfaceWorldY;
                var midpoint = (before + after) * 0.5f;
                if (point.SurfaceWorldY <= midpoint)
                    continue;

                point.SurfaceWorldY = midpoint;
                lowered++;
            }

            return lowered;
        }

        /// <summary>
        /// Queues a control at the run's crest. Its height is clamped between the control the water
        /// continues into and the run's base level, so the insert can only pull the bulge down.
        /// </summary>
        private static void QueueClampedControl(
            Feature feature,
            CrestRibbonSampler sampler,
            int startSample,
            int endSample,
            float baseWorldY,
            List<RibbonInsert> inserts)
        {
            var crest = startSample;
            for (var i = startSample; i <= endSample; i++)
            {
                if (sampler.Center(i).y > sampler.Center(crest).y)
                    crest = i;
            }

            var center = sampler.Center(crest);
            var control = ResolveControlIndex(crest, sampler);
            var downstream = feature.Points[Mathf.Clamp(control + 1, 0, feature.Points.Count - 1)].SurfaceWorldY;
            inserts.Add(new RibbonInsert(
                control + 1,
                new Vector2(center.x, center.z),
                Mathf.Max(MinCredibleBankMeters, sampler.HalfWidthMeters(crest)),
                Mathf.Max(downstream, Mathf.Min(baseWorldY, center.y))));
        }

        private static int ResolveControlIndex(int sampleIndex, CrestRibbonSampler sampler)
        {
            if (sampler.Count <= 1 || sampler.ControlCount <= 1)
                return 0;
            var t = sampleIndex / (float)(sampler.Count - 1);
            return Mathf.Clamp(Mathf.RoundToInt(t * (sampler.ControlCount - 1)), 0, sampler.ControlCount - 1);
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

        /// <summary>Applies queued inserts back-to-front so every queued index stays valid.</summary>
        private int ApplyRepairInserts(Feature feature, List<RibbonInsert> inserts, int maxInserts)
        {
            if (inserts.Count == 0 || maxInserts <= 0)
                return 0;

            inserts.Sort((a, b) => b.Index.CompareTo(a.Index));
            var applied = 0;
            for (var i = 0; i < inserts.Count && applied < maxInserts; i++)
            {
                var entry = inserts[i];
                if (!InsertPoint(
                        feature.StableId,
                        entry.Index,
                        entry.CenterXZ,
                        entry.HalfWidthMeters,
                        entry.SurfaceWorldY))
                    continue;
                applied++;
            }

            return applied;
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
