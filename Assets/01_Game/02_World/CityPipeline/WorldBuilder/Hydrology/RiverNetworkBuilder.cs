using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Builds a sparse, connected river network from ocean outlets upstream.</summary>
    public static partial class RiverNetworkBuilder
    {
        public static RiverPolyline[] Build(
            LandformField field,
            float[] accumulation,
            int[] flowTo,
            bool[] exclusionMask,
            bool[] oceanMask,
            bool[] lakeMask,
            HydrologyProfile profile,
            LakeRecord[] lakes = null)
        {
            if (field == null || accumulation == null || flowTo == null || profile == null)
                return Array.Empty<RiverPolyline>();

            var upstream = BuildUpstream(field, flowTo);
            var lakeCells = BuildLakeCellMap(field, lakes);
            var outlets = FindOceanOutlets(field, accumulation, flowTo, oceanMask, profile);
            var systems = SelectOutlets(
                field, accumulation, upstream, oceanMask, lakeMask, outlets, profile);
            var result = new List<RiverPolyline>(profile.MaxVisibleRiverBranches);

            for (var i = 0; i < systems.Count && result.Count < profile.MaxVisibleRiverBranches; i++)
            {
                BuildSystem(
                    field,
                    accumulation,
                    flowTo,
                    oceanMask,
                    lakeMask,
                    lakes,
                    lakeCells,
                    upstream,
                    systems[i],
                    profile,
                    result);
            }

            return result.ToArray();
        }

        public static RiverPolyline[] Build(
            LandformField field,
            float[] accumulation,
            int[] flowTo,
            bool[] exclusionMask,
            bool[] lakeMask,
            HydrologyProfile profile)
        {
            return Build(field, accumulation, flowTo, exclusionMask, null, lakeMask, profile);
        }

        public static RiverPolyline[] Build(
            LandformField field,
            float[] accumulation,
            HydrologyProfile profile)
        {
            if (field == null || accumulation == null || profile == null)
                return Array.Empty<RiverPolyline>();

            var flowTo = new int[field.Width * field.Height];
            FlowAccumulationSolver.Compute(field.WorldHeights, field.Width, field.Height, accumulation, flowTo);
            return Build(field, accumulation, flowTo, null, null, null, profile);
        }

        private static void BuildSystem(
            LandformField field,
            float[] accumulation,
            int[] flowTo,
            bool[] oceanMask,
            bool[] lakeMask,
            LakeRecord[] lakes,
            Dictionary<int, ulong> lakeCells,
            List<int>[] upstream,
            int outlet,
            HydrologyProfile profile,
            List<RiverPolyline> result)
        {
            var maxLandY = profile.SeaLevelWorldY + profile.MaxRiverLandElevationAboveSeaMeters;
            var mainDownstreamFirst = TraceUpstream(
                outlet,
                upstream,
                accumulation,
                oceanMask,
                lakeMask,
                field,
                maxLandY,
                profile.ValleyFlowPreference,
                lakeCells,
                out var sourceLake);
            mainDownstreamFirst.Reverse();
            // Outlet selection already enforced length / longest-stem guarantee.
            if (mainDownstreamFirst.Count < 2)
                return;

            var systemId = HashSystem(outlet);
            var main = CreatePolyline(
                field,
                accumulation,
                mainDownstreamFirst,
                profile,
                systemId,
                0,
                RiverKind.MainStem,
                0,
                sourceLake,
                false,
                default,
                true,
                FindOceanPoint(field, outlet, flowTo[outlet], oceanMask));
            if (main == null)
                return;

            result.Add(main);
            var claimed = new HashSet<int>(mainDownstreamFirst);
            var branchCandidates = CollectTributaryCandidates(
                mainDownstreamFirst, upstream, accumulation, claimed, lakeMask, profile);
            var tributaryCount = 0;
            for (var i = 0; i < branchCandidates.Count &&
                            tributaryCount < profile.MaxTributariesPerSystem &&
                            result.Count < profile.MaxVisibleRiverBranches; i++)
            {
                var candidate = branchCandidates[i];
                var confluence = FindConfluence(mainDownstreamFirst, upstream, candidate);
                var branchDownstreamFirst = TraceUpstream(
                    candidate,
                    upstream,
                    accumulation,
                    oceanMask,
                    lakeMask,
                    field,
                    maxLandY,
                    profile.ValleyFlowPreference,
                    lakeCells,
                    out var branchLake);
                branchDownstreamFirst.Reverse();
                if (branchDownstreamFirst.Count < 2 ||
                    EstimateCellLength(field, branchDownstreamFirst) < profile.TributaryMinimumLengthMeters)
                    continue;

                for (var c = 0; c < branchDownstreamFirst.Count; c++)
                    claimed.Add(branchDownstreamFirst[c]);

                var branch = CreatePolyline(
                    field,
                    accumulation,
                    branchDownstreamFirst,
                    profile,
                    systemId,
                    tributaryCount + 1,
                    RiverKind.Tributary,
                    main.StableId,
                    branchLake,
                    true,
                    field.CellCenterXZ(confluence % field.Width, confluence / field.Width),
                    false,
                    default);
                if (branch == null)
                    continue;

                result.Add(branch);
                tributaryCount++;
            }
        }

        private static List<int> TraceUpstream(
            int start,
            List<int>[] upstream,
            float[] accumulation,
            bool[] oceanMask,
            bool[] lakeMask,
            LandformField field,
            float maxLandWorldY,
            float valleyFlowPreference,
            Dictionary<int, ulong> lakeCells,
            out ulong sourceLake)
        {
            sourceLake = 0;
            var path = new List<int>(64) { start };
            var cursor = start;
            var guard = 0;
            while (cursor >= 0 && cursor < upstream.Length && guard++ < upstream.Length)
            {
                var predecessor = SelectPredecessor(
                    upstream[cursor],
                    accumulation,
                    oceanMask,
                    lakeMask,
                    field,
                    maxLandWorldY,
                    valleyFlowPreference);
                if (predecessor < 0)
                {
                    sourceLake = lakeCells != null
                        ? FindAdjacentLake(cursor, upstream[cursor], lakeCells)
                        : 0UL;
                    break;
                }

                path.Add(predecessor);
                cursor = predecessor;
            }

            return path;
        }

        private static int SelectPredecessor(
            List<int> candidates,
            float[] accumulation,
            bool[] oceanMask,
            bool[] lakeMask,
            LandformField field,
            float maxLandWorldY,
            float valleyFlowPreference)
        {
            if (candidates == null || candidates.Count == 0)
                return -1;

            var maxFlow = float.NegativeInfinity;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!IsValidUpstreamCandidate(
                        candidate, accumulation, oceanMask, lakeMask, field, maxLandWorldY))
                    continue;
                if (accumulation[candidate] > maxFlow)
                    maxFlow = accumulation[candidate];
            }

            if (float.IsNegativeInfinity(maxFlow))
                return -1;

            var flowFloor = maxFlow * Mathf.Clamp(valleyFlowPreference, 0.05f, 1f);
            var best = -1;
            var bestHeight = float.PositiveInfinity;
            var bestFlow = float.NegativeInfinity;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!IsValidUpstreamCandidate(
                        candidate, accumulation, oceanMask, lakeMask, field, maxLandWorldY))
                    continue;
                var flow = accumulation[candidate];
                if (flow < flowFloor)
                    continue;
                var height = field != null && candidate < field.WorldHeights.Length
                    ? field.WorldHeights[candidate]
                    : 0f;
                if (height < bestHeight - 0.01f ||
                    (Mathf.Abs(height - bestHeight) <= 0.01f && flow > bestFlow) ||
                    (Mathf.Abs(height - bestHeight) <= 0.01f &&
                     Mathf.Abs(flow - bestFlow) <= 0.01f &&
                     (best < 0 || candidate < best)))
                {
                    best = candidate;
                    bestHeight = height;
                    bestFlow = flow;
                }
            }

            return best;
        }

        private static bool IsValidUpstreamCandidate(
            int candidate,
            float[] accumulation,
            bool[] oceanMask,
            bool[] lakeMask,
            LandformField field,
            float maxLandWorldY)
        {
            if (candidate < 0 || candidate >= accumulation.Length)
                return false;
            if (oceanMask != null && oceanMask[candidate])
                return false;
            if (lakeMask != null && lakeMask[candidate])
                return false;
            if (field != null &&
                candidate < field.WorldHeights.Length &&
                field.WorldHeights[candidate] > maxLandWorldY)
                return false;
            return true;
        }

        private static List<int> CollectTributaryCandidates(
            List<int> main,
            List<int>[] upstream,
            float[] accumulation,
            HashSet<int> claimed,
            bool[] lakeMask,
            HydrologyProfile profile)
        {
            var candidates = new List<int>(main.Count);
            for (var i = 0; i < main.Count; i++)
            {
                var cell = main[i];
                var parentFlow = accumulation[cell];
                for (var u = 0; u < upstream[cell].Count; u++)
                {
                    var candidate = upstream[cell][u];
                    if (claimed.Contains(candidate) || (lakeMask != null && lakeMask[candidate]))
                        continue;
                    if (accumulation[candidate] < parentFlow * profile.TributaryMinimumFlowFraction)
                        continue;
                    candidates.Add(candidate);
                }
            }

            candidates.Sort((a, b) =>
            {
                var compare = accumulation[b].CompareTo(accumulation[a]);
                return compare != 0 ? compare : a.CompareTo(b);
            });
            return candidates;
        }

        private static int FindConfluence(List<int> main, List<int>[] upstream, int candidate)
        {
            for (var i = 0; i < main.Count; i++)
            {
                if (upstream[main[i]].Contains(candidate))
                    return main[i];
            }

            return main[main.Count - 1];
        }

        private static RiverPolyline CreatePolyline(
            LandformField field,
            float[] accumulation,
            List<int> cells,
            HydrologyProfile profile,
            ulong systemId,
            int branchIndex,
            RiverKind kind,
            ulong parentId,
            ulong sourceLake,
            bool hasConfluence,
            Vector2 confluence,
            bool hasOceanMouth,
            Vector2 oceanMouth)
        {
            var raw = new List<Vector2>(cells.Count);
            for (var i = 0; i < cells.Count; i++)
                raw.Add(field.CellCenterXZ(cells[i] % field.Width, cells[i] / field.Width));

            var simplified = DouglasPeucker(raw, field.CellSize * 0.75f);
            var smoothed = Chaikin(simplified, 1);
            var resampled = Resample(smoothed, Mathf.Max(field.CellSize * 0.75f, 12f));
            if (hasConfluence)
                resampled.Add(confluence);
            if (resampled.Count < 2)
                return null;

            var stableId = HashRiver(systemId, branchIndex, kind);
            ApplyMeander(field, resampled, profile, stableId);
            var widths = new float[resampled.Count];
            var depths = new float[resampled.Count];
            var maxFlow = 0f;
            var outletFlow = Mathf.Max(
                profile.VisibleHeadwaterFlowThreshold + 1f,
                accumulation[cells[cells.Count - 1]]);
            for (var i = 0; i < resampled.Count; i++)
            {
                var flow = Mathf.Max(maxFlow, SampleAccumulation(field, accumulation, resampled[i]));
                maxFlow = flow;
                var norm = Mathf.Clamp01(Mathf.InverseLerp(
                    profile.VisibleHeadwaterFlowThreshold,
                    outletFlow,
                    flow));
                var widthCurve = profile.RiverWidthByAccumulation?.Evaluate(norm) ?? norm;
                var depthCurve = profile.RiverDepthByAccumulation?.Evaluate(norm) ?? norm;
                widths[i] = Mathf.Lerp(profile.MinRiverWidthMeters, profile.MaxRiverWidthMeters, widthCurve);
                depths[i] = Mathf.Lerp(profile.MinRiverDepthMeters, profile.MaxRiverDepthMeters, depthCurve);
                if (i > 0)
                {
                    widths[i] = Mathf.Max(widths[i], widths[i - 1]);
                    depths[i] = Mathf.Max(depths[i], depths[i - 1]);
                }
            }

            if (hasOceanMouth)
                ApplyMouthFlare(widths, profile);

            return new RiverPolyline
            {
                StableId = stableId,
                RiverSystemStableId = systemId,
                ParentRiverStableId = parentId,
                Kind = kind,
                PointsXZ = resampled.ToArray(),
                WidthMeters = widths,
                DepthMeters = depths,
                FlowAccumulation = maxFlow,
                HasOceanMouth = hasOceanMouth,
                OceanMouthXZ = oceanMouth,
                HasConfluence = hasConfluence,
                ConfluenceXZ = confluence,
                SourceLakeStableId = sourceLake
            };
        }

        private static List<int> FindOceanOutlets(
            LandformField field,
            float[] accumulation,
            int[] flowTo,
            bool[] oceanMask,
            HydrologyProfile profile)
        {
            var result = new List<int>();
            if (oceanMask == null)
                return result;
            for (var i = 0; i < flowTo.Length; i++)
            {
                if (oceanMask[i])
                    continue;
                var next = flowTo[i];
                if ((next >= 0 && next < oceanMask.Length && oceanMask[next]) ||
                    IsOceanAdjacent(field, i, oceanMask))
                    result.Add(i);
            }

            result.Sort((a, b) =>
            {
                var compare = accumulation[b].CompareTo(accumulation[a]);
                return compare != 0 ? compare : a.CompareTo(b);
            });
            return result;
        }

        private static List<int>[] BuildUpstream(LandformField field, int[] flowTo)
        {
            var result = new List<int>[field.Width * field.Height];
            for (var i = 0; i < result.Length; i++)
                result[i] = new List<int>(2);
            for (var i = 0; i < flowTo.Length; i++)
            {
                var next = flowTo[i];
                if (next >= 0 && next < result.Length && next != i)
                    result[next].Add(i);
            }

            return result;
        }

    }
}
