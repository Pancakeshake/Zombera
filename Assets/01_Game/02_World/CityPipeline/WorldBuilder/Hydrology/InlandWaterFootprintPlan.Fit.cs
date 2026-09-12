using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Fits the sparse control points to the terrain that was actually applied. Each candidate
    /// point samples the local perpendicular bank direction and walks outward until the terrain
    /// leaves the carved bed, then recentres on the midpoint of the two crossings and takes the
    /// measured span as its target width. Soft banks (rise from dug centre) are preferred when
    /// nearer than the free-surface dig shoulder so an oversized MaxRiverWidth plan shrinks to the
    /// Crest trench instead of surviving as EdgeMismatch. Fit only shrinks; widening uses
    /// <c>ExtendForRecarve</c>.
    /// </summary>
    public sealed partial class InlandWaterFootprintPlan
    {
        private const int BankRefinementSteps = 5;

        /// <summary>
        /// Soft-bank / EdgeMismatch hits closer than this are treated as open floodplain noise
        /// (bilinear wobble at the dug centre), not a real waterline.
        /// </summary>
        private const float MinCredibleBankMeters = 2f;

        public InlandWaterFootprintReport FitToCarvedField(
            LandformField field,
            InlandWaterFootprintOptions options,
            int samplesPerSide = 64) =>
            FitToCarvedField(field, options, new InlandWaterFootprintReport(), samplesPerSide);

        public InlandWaterFootprintReport FitToCarvedField(
            LandformField field,
            InlandWaterFootprintOptions options,
            InlandWaterFootprintReport report,
            int samplesPerSide = 64)
        {
            Report = report ?? new InlandWaterFootprintReport();
            Report.Clear();
            if (field == null)
                return Report;

            var resolved = options.Normalized();
            for (var f = 0; f < _features.Count; f++)
            {
                var feature = _features[f];
                if (feature?.Points == null || feature.Points.Count == 0)
                    continue;
                for (var p = 0; p < feature.Points.Count; p++)
                    FitPoint(field, feature, p, resolved, samplesPerSide);
            }

            return Report;
        }

        private void FitPoint(
            LandformField field,
            Feature feature,
            int index,
            in InlandWaterFootprintOptions options,
            int samplesPerSide)
        {
            var points = feature.Points;
            var point = points[index];
            point.ClearMeasurements();

            // Mountain runoff above the carve ceiling is never dug, and an ocean/lake cell is owned by
            // another feature: neither has a river bed or banks to measure, so keep the planned
            // geometry untouched and report nothing.
            if (point.CarveSkipped)
                return;
            if (IsAboveCarveCeiling(point.CenterXZ, field))
                return;

            var normal = ResolveBankDirection(points, index);
            var plannedHalf = Mathf.Max(0.5f, point.TargetWetHalfWidthMeters);
            var clearance = Mathf.Max(point.RequestedBedClearanceMeters, options.MinimumBedClearanceMeters);
            var bedLevel = point.SurfaceWorldY - clearance;
            var crossingTarget = bedLevel + options.BankHeightToleranceMeters;
            var reach = Mathf.Max(
                plannedHalf * 2f,
                plannedHalf + Mathf.Max(0f, point.BankShoulderMeters) + options.MaximumBankExtensionMeters);
            var step = Mathf.Max(0.25f, reach / Mathf.Max(4, samplesPerSide));

            var centerHeight = LandformFieldSampling.SampleBilinear(field, point.CenterXZ.x, point.CenterXZ.y);
            if (centerHeight > crossingTarget)
            {
                ReportUndugBed(feature, index, point, plannedHalf, centerHeight - bedLevel, clearance, options);
                return;
            }

            var left = FindBankCrossing(field, point.CenterXZ, normal, 1f, reach, step, crossingTarget);
            var right = FindBankCrossing(field, point.CenterXZ, normal, -1f, reach, step, crossingTarget);

            // Unowned surface banks (dig shoulder in ocean/lake) must not abort Fit — that left
            // MaxRiverWidth locked while ribbon soft-banks still reported EdgeMismatch.
            if (!IsOwnedCrossing(point, normal, 1f, left))
                left = (false, 0f);
            if (!IsOwnedCrossing(point, normal, -1f, right))
                right = (false, 0f);

            // Free-surface banks often land on the dig shoulder of an oversized plan (~MaxRiverWidth).
            // Soft banks (rise from dug centre) find the real Crest trench edge; prefer that when
            // nearer so Fit shrinks instead of locking the shoulder and leaving ribbon EdgeMismatch.
            var softLeft = (found: false, distance: 0f);
            var softRight = (found: false, distance: 0f);
            TryResolveSoftBanks(
                field, point.CenterXZ, normal, centerHeight, point.SurfaceWorldY,
                reach, step, options, ref softLeft, ref softRight);
            DiscardIncredibleBanks(ref softLeft, ref softRight);
            if (softLeft.found && !IsOwnedCrossing(point, normal, 1f, softLeft))
                softLeft = (false, 0f);
            if (softRight.found && !IsOwnedCrossing(point, normal, -1f, softRight))
                softRight = (false, 0f);
            PreferNearerSoftBanks(softLeft, softRight, ref left, ref right, options.LateralToleranceMeters);

            // Surface miss still falls back to soft when soft alone found a bank.
            if (!left.found)
                left = softLeft;
            if (!right.found)
                right = softRight;

            var leftOpen = !left.found;
            var rightOpen = !right.found;
            point.LeftBankMissing = false;
            point.RightBankMissing = false;

            if (leftOpen && rightOpen)
            {
                point.MeasuredLeftMeters = plannedHalf;
                point.MeasuredRightMeters = plannedHalf;
                return;
            }

            // One-sided open: mirror the found bank so we still narrow an oversized plan.
            var leftDist = leftOpen ? right.distance : left.distance;
            var rightDist = rightOpen ? left.distance : right.distance;
            point.MeasuredLeftMeters = leftDist;
            point.MeasuredRightMeters = rightDist;
            ApplyMeasuredSpan(
                field, feature, index, point, normal, leftDist, rightDist, plannedHalf, bedLevel, options);
        }

        /// <summary>
        /// Finds the dug trench edge as a rise above the carved centre. Used when the free-surface
        /// crossing target never fires because surrounding land stays below it, and to detect a
        /// nearer wet edge than the dig shoulder. Requires real dig depth so centre noise is ignored.
        /// Always fills both sides (callers merge with surface banks via <see cref="PreferNearerSoftBanks"/>).
        /// </summary>
        private static void TryResolveSoftBanks(
            LandformField field,
            Vector2 center,
            Vector2 normal,
            float centerHeight,
            float surfaceWorldY,
            float reach,
            float step,
            in InlandWaterFootprintOptions options,
            ref (bool found, float distance) left,
            ref (bool found, float distance) right)
        {
            var digDepth = Mathf.Max(0f, surfaceWorldY - centerHeight);
            if (digDepth < options.BankHeightToleranceMeters)
                return;

            var rise = Mathf.Max(
                1f,
                Mathf.Max(options.BankHeightToleranceMeters * 4f, digDepth * 0.1f));
            var softTarget = centerHeight + rise;
            left = FindBankCrossing(field, center, normal, 1f, reach, step, softTarget);
            right = FindBankCrossing(field, center, normal, -1f, reach, step, softTarget);
        }

        private static void DiscardIncredibleBanks(
            ref (bool found, float distance) left,
            ref (bool found, float distance) right)
        {
            if (left.found && left.distance < MinCredibleBankMeters)
                left = (false, 0f);
            if (right.found && right.distance < MinCredibleBankMeters)
                right = (false, 0f);
        }

        /// <summary>
        /// When soft banks find a credible nearer trench edge than the free-surface dig shoulder,
        /// adopt soft — otherwise Crest ribbon validation reports EdgeMismatch against MaxRiverWidth.
        /// </summary>
        private static void PreferNearerSoftBanks(
            (bool found, float distance) softLeft,
            (bool found, float distance) softRight,
            ref (bool found, float distance) left,
            ref (bool found, float distance) right,
            float lateralTolerance)
        {
            if (softLeft.found &&
                (!left.found || softLeft.distance + lateralTolerance < left.distance))
                left = softLeft;
            if (softRight.found &&
                (!right.found || softRight.distance + lateralTolerance < right.distance))
                right = softRight;
        }

        /// <summary>
        /// The channel was never dug to the requested depth here: keeping the planned edges and
        /// widening forces the next bounded pass to re-carve rather than bury the ribbon.
        /// </summary>
        private void ReportUndugBed(
            Feature feature,
            int index,
            Point point,
            float plannedHalf,
            float shortfall,
            float clearance,
            in InlandWaterFootprintOptions options)
        {
            point.LeftBankMissing = false;
            point.RightBankMissing = false;
            point.MeasuredLeftMeters = plannedHalf;
            point.MeasuredRightMeters = plannedHalf;
            ExtendForRecarve(point, options);
            Report.Add(
                feature.StableId, feature.Kind, index, point.CenterXZ,
                InlandWaterFootprintFailure.BedClearance, shortfall, clearance);
        }

        /// <summary>
        /// Recentres on the midpoint of the two measured banks and reconciles the planned (bed) width
        /// with the measured waterline once the carve profile's own shoulder offset is removed.
        /// </summary>
        private void ApplyMeasuredSpan(
            LandformField field,
            Feature feature,
            int index,
            Point point,
            Vector2 normal,
            float leftDistance,
            float rightDistance,
            float plannedHalf,
            float bedLevel,
            in InlandWaterFootprintOptions options)
        {
            var recentreLimit = ResolveRecentreLimit(feature.Points, index);
            point.CenterXZ += normal * Mathf.Clamp(
                (leftDistance - rightDistance) * 0.5f, -recentreLimit, recentreLimit);
            var measuredHalf = (leftDistance + rightDistance) * 0.5f;
            if (measuredHalf < MinCredibleBankMeters)
                return;

            var waterlineOffset = ResolveWaterlineOffset(field, point, normal, bedLevel, options);
            var predictedHalf = plannedHalf + waterlineOffset;

            // Dead-band against the PREDICTED waterline, so the shoulder artifact is not mistaken
            // for a width error. Fit may only SHRINK: widening from a leftover wide trench after a
            // narrowed repair would fight EdgeMismatch forever. Growth uses ExtendForRecarve.
            if (Mathf.Abs(measuredHalf - predictedHalf) <= options.LateralToleranceMeters)
                return;

            // Clear shrink path: when the soft/surface edge is well inside the plan, adopt it
            // directly. Shoulder offset is only meaningful near the planned dig edge.
            float adopted;
            if (measuredHalf + options.LateralToleranceMeters < plannedHalf)
                adopted = Mathf.Max(MinCredibleBankMeters, measuredHalf);
            else
                adopted = Mathf.Max(MinCredibleBankMeters, measuredHalf - waterlineOffset);

            if (adopted >= plannedHalf - options.LateralToleranceMeters)
                return;
            point.TargetWetHalfWidthMeters = adopted;
            CapNeighbourhoodHalfWidth(feature, index, adopted);
        }

        /// <summary>
        /// Keeps Crest radius-multiplier interpolation from reintroducing MaxRiverWidth between a
        /// narrowed control and its still-wide neighbours. Cap is exact (no 1.25 slack): slack kept
        /// predicted half above soft measured edges and fought <c>LateralToleranceMeters</c> forever.
        /// </summary>
        private static void CapFeatureHalfWidth(Feature feature, float bedHalfWidth)
        {
            if (feature?.Points == null || feature.Points.Count == 0)
                return;
            var cap = Mathf.Max(MinCredibleBankMeters, bedHalfWidth);
            for (var i = 0; i < feature.Points.Count; i++)
            {
                var point = feature.Points[i];
                if (point.TargetWetHalfWidthMeters > cap)
                    point.TargetWetHalfWidthMeters = cap;
            }
        }

        /// <summary>
        /// Narrows one control and its immediate neighbours only. <c>CrestRibbonSampler</c>
        /// interpolates the ribbon radius multiplier between neighbouring controls, so capping the
        /// neighbourhood is enough to keep a narrowed control from being interpolated back up —
        /// whereas capping the whole feature let a single local measurement collapse an entire river
        /// to that width, which the shrink-only repair could then never widen back.
        /// </summary>
        private static void CapNeighbourhoodHalfWidth(Feature feature, int controlIndex, float bedHalfWidth)
        {
            if (feature?.Points == null || feature.Points.Count == 0)
                return;
            var cap = Mathf.Max(MinCredibleBankMeters, bedHalfWidth);
            var from = Mathf.Max(0, controlIndex - 1);
            var to = Mathf.Min(feature.Points.Count - 1, controlIndex + 1);
            for (var i = from; i <= to; i++)
            {
                var point = feature.Points[i];
                if (point.TargetWetHalfWidthMeters > cap)
                    point.TargetWetHalfWidthMeters = cap;
            }
        }

        /// <summary>
        /// True when a control sits above <see cref="CarveCeilingWorldY"/>, where
        /// <c>HydrologyCarver.ApplyInlandBathymetry</c> deliberately declines to enforce bed
        /// clearance (low-land-only policy, so rivers stop cutting mountains and elevated lake
        /// basins are left natural). Applies to rivers and lakes alike, because that pass gates on
        /// terrain height rather than water class. An unset ceiling (its infinity default) is false.
        /// </summary>
        private bool IsAboveCarveCeiling(Vector2 centerXZ, LandformField field)
        {
            if (float.IsPositiveInfinity(CarveCeilingWorldY))
                return false;
            return LandformFieldSampling.SampleBilinear(field, centerXZ.x, centerXZ.y) > CarveCeilingWorldY;
        }

        /// <summary>
        /// Keeps the planned edge for a missing side and widens the plan so the next carve pass
        /// digs further out; growth is bounded by the repair-pass limit.
        /// </summary>
        private void ExtendForRecarve(Point point, in InlandWaterFootprintOptions options)
        {
            if (options.MaximumBankExtensionMeters <= 0f)
                return;
            point.TargetWetHalfWidthMeters += options.MaximumBankExtensionMeters;
            Report.ExtendedBanks++;
        }

        /// <summary>True when a measured bank crossing sits on a cell the river carve still owns.</summary>
        private bool IsOwnedCrossing(
            Point point,
            Vector2 normal,
            float direction,
            (bool found, float distance) crossing)
        {
            if (!crossing.found)
                return true;
            var position = point.CenterXZ + normal * (direction * crossing.distance);
            return IsCarveOwned(position);
        }

        /// <summary>Perpendicular (bank) direction of the control polyline at a point.</summary>
        public static Vector2 ResolveBankDirection(System.Collections.Generic.List<Point> points, int index)
        {
            var before = Mathf.Max(0, index - 1);
            var after = Mathf.Min(points.Count - 1, index + 1);
            var tangent = points[after].CenterXZ - points[before].CenterXZ;
            if (tangent.sqrMagnitude < 1e-4f)
                tangent = points[points.Count - 1].CenterXZ - points[0].CenterXZ;
            if (tangent.sqrMagnitude < 1e-4f)
                return Vector2.up;
            tangent = tangent.normalized;
            return new Vector2(-tangent.y, tangent.x);
        }

        /// <summary>
        /// Distance the smooth shoulder pushes the waterline past the planned wet edge on one side.
        /// <para>
        /// The carve keeps the full bed from the centreline through the whole target wet half-width
        /// and only then rises to untouched terrain over the bank shoulder. Terrain therefore
        /// reaches <c>BankHeightToleranceMeters</c> above the bed some way <i>into</i> the shoulder,
        /// not at the wet edge. That offset is a property of the carve profile, not a fitting error:
        /// treated as error it consumes the entire lateral tolerance budget and makes the repair
        /// loop chase its own tail, so fit and validation predict it and compare against the
        /// predicted waterline instead.
        /// </para>
        /// </summary>
        private static float EstimateShoulderOffset(
            LandformField field,
            Vector2 center,
            Vector2 normal,
            float direction,
            float halfWidth,
            float bedLevel,
            float shoulderMeters,
            in InlandWaterFootprintOptions options)
        {
            if (shoulderMeters <= 0f)
                return 0f;

            var bankRise = SampleAlong(field, center, normal, direction, halfWidth) - bedLevel;
            if (bankRise <= options.BankHeightToleranceMeters)
            {
                // Still at bed at the planned wet edge. If the rise never appears across the
                // shoulder either, we are mid leftover trench / open floodplain — inventing a
                // full-shoulder waterline keeps predictedHalf ≈ MaxRiverWidth and EdgeMismatch loops.
                var riseAtShoulderEnd =
                    SampleAlong(field, center, normal, direction, halfWidth + shoulderMeters) - bedLevel;
                if (riseAtShoulderEnd <= options.BankHeightToleranceMeters)
                    return 0f;
                return shoulderMeters;
            }

            var target = Mathf.Clamp01(options.BankHeightToleranceMeters / bankRise);
            return InverseSmoothStep(target) * shoulderMeters;
        }

        /// <summary>Inverts <c>t*t*(3-2t)</c> by bisection (the curve is monotone on [0, 1]).</summary>
        private static float InverseSmoothStep(float value)
        {
            var low = 0f;
            var high = 1f;
            for (var i = 0; i < 24; i++)
            {
                var mid = (low + high) * 0.5f;
                if (mid * mid * (3f - 2f * mid) < value)
                    low = mid;
                else
                    high = mid;
            }

            return high;
        }

        /// <summary>Mean predicted waterline offset for one control point, averaged over both banks.</summary>
        private static float ResolveWaterlineOffset(
            LandformField field,
            Point point,
            Vector2 normal,
            float bedLevel,
            in InlandWaterFootprintOptions options)
        {
            var halfWidth = Mathf.Max(0.5f, point.TargetWetHalfWidthMeters);
            var shoulder = Mathf.Max(0f, point.BankShoulderMeters);
            var left = EstimateShoulderOffset(
                field, point.CenterXZ, normal, 1f, halfWidth, bedLevel, shoulder, options);
            var right = EstimateShoulderOffset(
                field, point.CenterXZ, normal, -1f, halfWidth, bedLevel, shoulder, options);
            return (left + right) * 0.5f;
        }

        /// <summary>
        /// First distance along <paramref name="normal"/> where the terrain climbs back out of the
        /// carved bed. Bisection refines the last step to sub-cell accuracy. Returns
        /// <c>found=false</c> only when every sample stayed at or below the crossing target
        /// (open floodplain within reach) — callers should accept the planned edge, not fail.
        /// </summary>
        private static (bool found, float distance) FindBankCrossing(
            LandformField field,
            Vector2 center,
            Vector2 normal,
            float direction,
            float reach,
            float step,
            float crossingTarget)
        {
            var low = 0f;
            for (var distance = step; distance <= reach; distance += step)
            {
                var height = SampleAlong(field, center, normal, direction, distance);
                if (height <= crossingTarget)
                {
                    low = distance;
                    continue;
                }

                var high = distance;
                for (var i = 0; i < BankRefinementSteps; i++)
                {
                    var mid = (low + high) * 0.5f;
                    if (SampleAlong(field, center, normal, direction, mid) <= crossingTarget)
                        low = mid;
                    else
                        high = mid;
                }

                return (true, high);
            }

            return (false, 0f);
        }

        private static float SampleAlong(
            LandformField field,
            Vector2 center,
            Vector2 normal,
            float direction,
            float distance)
        {
            var position = center + normal * (direction * distance);
            return LandformFieldSampling.SampleBilinear(field, position.x, position.y);
        }
    }
}
