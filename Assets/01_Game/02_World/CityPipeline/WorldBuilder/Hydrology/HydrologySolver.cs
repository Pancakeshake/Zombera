using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Ocean flood-fill, priority-flood, D8, rivers, lakes → <see cref="HydrologyPlan"/>.</summary>
    public static partial class HydrologySolver
    {
        public static HydrologyPlan Solve(
            LandformField field,
            HydrologyProfile profile,
            LandformProfile landformProfile,
            WorldMapSession session,
            IReadOnlyList<CityFlattenPad> cityPads = null)
        {
            if (field == null || profile == null)
                return new HydrologyPlan();

            var count = field.Width * field.Height;
            var layout = WorldMapBoundaryLayout.Resolve(session, landformProfile);
            var oceanMask = BuildOceanMask(field, profile.SeaLevelWorldY, layout, landformProfile);
            var exclusionMask = HydrologyExclusionMask.Build(
                field,
                profile.SeaLevelWorldY,
                session,
                landformProfile,
                oceanMask);
            var placementMask = HydrologyExclusionMask.BuildRiverPlacementMask(
                field,
                profile.SeaLevelWorldY,
                session,
                landformProfile,
                profile,
                oceanMask);
            var filled = PriorityFloodSolver.FillDepressions(field, oceanMask, layout);

            var accumulation = new float[count];
            var flowTo = new int[count];
            FlowAccumulationSolver.Compute(filled, field.Width, field.Height, accumulation, flowTo, oceanMask);

            // Oceans are not inland lakes. Mountains remain part of the flow domain so
            // headwaters can descend from the visible ranges instead of being cut off.
            var preliminaryRivers = RiverNetworkBuilder.Build(
                field, accumulation, flowTo, null, oceanMask, null, profile, null);
            var lakes = ConnectedLakeSelector.Select(
                field, filled, oceanMask, flowTo, profile, preliminaryRivers);
            var lakeMask = BuildLakeMask(field, lakes, profile, out var nearestLakeIndex);

            var rivers = RiverNetworkBuilder.Build(
                field, accumulation, flowTo, null, oceanMask, lakeMask, profile, lakes);
            LinkLakesToRiverSystems(rivers, lakes);
            if (!HydrologyTopologyValidator.Validate(rivers, lakes, out var topologyError))
                throw new InvalidOperationException("Hydrology topology validation failed: " + topologyError);

            var plan = new HydrologyPlan(
                field.Width,
                field.Height,
                field.CellSize,
                field.OriginXZ,
                rivers,
                lakes);

            Array.Copy(accumulation, plan.FlowAccumulation, count);
            RasterizeOcean(field, plan, oceanMask, profile.SeaLevelWorldY);
            RasterizeLakes(plan, lakes, lakeMask, nearestLakeIndex);
            RasterizeRivers(field, plan, rivers, profile, null);
            HydrologyDistanceField.Compute(plan);
            Debug.Log(
                $"[HydrologySolver] rivers={plan.Rivers.Length} lakes={plan.Lakes.Length} " +
                $"excluded={CountBlocked(exclusionMask)} placement={CountBlocked(placementMask)} " +
                $"allOceanEdges=true seed={session.Seed}");
            return plan;
        }

        private static int CountBlocked(bool[] mask)
        {
            if (mask == null) return 0;
            var count = 0;
            for (var i = 0; i < mask.Length; i++)
            {
                if (mask[i]) count++;
            }

            return count;
        }

        private static void LinkLakesToRiverSystems(RiverPolyline[] rivers, LakeRecord[] lakes)
        {
            if (rivers == null || lakes == null)
                return;
            for (var r = 0; r < rivers.Length; r++)
            {
                var river = rivers[r];
                if (river == null) continue;
                LinkLake(lakes, river.SourceLakeStableId, river, false);
                LinkLake(lakes, river.JoinLakeStableId, river, true);
            }
        }

        private static void LinkLake(LakeRecord[] lakes, ulong lakeId, RiverPolyline river, bool inlet)
        {
            if (lakeId == 0) return;
            for (var l = 0; l < lakes.Length; l++)
            {
                var lake = lakes[l];
                if (lake == null || lake.StableId != lakeId) continue;
                lake.ConnectedRiverSystemStableId = river.RiverSystemStableId;
                if (inlet)
                {
                    var ids = new List<ulong>(lake.InletRiverStableIds ?? Array.Empty<ulong>());
                    if (!ids.Contains(river.StableId)) ids.Add(river.StableId);
                    lake.InletRiverStableIds = ids.ToArray();
                }
                else
                {
                    lake.OutletRiverStableId = river.StableId;
                }
                return;
            }
        }

    }
}
