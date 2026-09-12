using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Converts routing depressions into a small set of river-connected lakes.</summary>
    internal static class ConnectedLakeSelector
    {
        /// <summary>Flow routing plus ocean mask/distance data shared by candidate filtering.</summary>
        private readonly struct RoutingContext
        {
            public readonly int[] FlowTo;
            public readonly int[] OceanDistance;
            public readonly bool[] OceanMask;

            public RoutingContext(int[] flowTo, int[] oceanDistance, bool[] oceanMask)
            {
                FlowTo = flowTo;
                OceanDistance = oceanDistance;
                OceanMask = oceanMask;
            }
        }

        /// <summary>Four-connected neighbour offsets, ordered to match the original scan order.</summary>
        private static readonly (int Dx, int Dz)[] NeighborOffsets =
        {
            (0, -1), (-1, 0), (1, 0), (0, 1)
        };

        public static LakeRecord[] Select(
            LandformField field,
            float[] filledHeights,
            bool[] oceanMask,
            int[] flowTo,
            HydrologyProfile profile,
            RiverPolyline[] retainedRivers)
        {
            if (field == null || filledHeights == null || profile == null)
                return Array.Empty<LakeRecord>();

            var candidates = LakeExtractor.Extract(field, filledHeights, profile, oceanMask, false);
            if (candidates.Length == 0)
                return candidates;
            var riverCells = BuildRiverCells(field, retainedRivers, out var riverSystems);
            var oceanDistance = BuildOceanDistance(field, oceanMask);
            var routing = new RoutingContext(flowTo, oceanDistance, oceanMask);
            var valid = new List<LakeRecord>(candidates.Length);
            for (var i = 0; i < candidates.Length; i++)
            {
                var lake = NormalizeCandidate(field, candidates[i], routing, riverCells, riverSystems, profile);
                if (lake != null)
                    valid.Add(lake);
            }

            valid.Sort((a, b) =>
            {
                var score = (b.EstimatedVolumeMetersCubed * b.MaxDepthMeters)
                    .CompareTo(a.EstimatedVolumeMetersCubed * a.MaxDepthMeters);
                return score != 0 ? score : a.StableId.CompareTo(b.StableId);
            });
            var totalLimit = CountPlayable(field, oceanMask) * field.CellSize * field.CellSize *
                             Mathf.Clamp01(profile.LakeMaximumTotalAreaFraction);
            var result = new List<LakeRecord>(profile.MaxVisibleLakes);
            var usedArea = 0f;
            for (var i = 0; i < valid.Count && result.Count < profile.MaxVisibleLakes; i++)
            {
                var lake = valid[i];
                if (usedArea + lake.AreaMetersSq > totalLimit + 0.01f)
                    continue;
                usedArea += lake.AreaMetersSq;
                result.Add(lake);
            }
            return result.ToArray();
        }

        private static LakeRecord NormalizeCandidate(
            LandformField field,
            LakeRecord source,
            RoutingContext routing,
            HashSet<int> riverCells,
            Dictionary<int, ulong> riverSystems,
            HydrologyProfile profile)
        {
            var sourceCells = ToCellSet(field, source.BasinCellCentersXZ);
            if (sourceCells.Count < 3)
                return null;
            FindSpill(field, field.WorldHeights, sourceCells, out var outlet);
            // Prefer extractor fill surface; neighbour spill alone can empty the wet set.
            var spill = source.SurfaceWorldY;
            var wet = new List<int>(sourceCells.Count);
            var sum = Vector2.zero;
            var maxDepth = 0f;
            var volume = 0f;
            foreach (var cell in sourceCells)
            {
                var depth = spill - field.WorldHeights[cell];
                if (depth <= 0.01f) continue;
                wet.Add(cell);
                sum += field.CellCenterXZ(cell % field.Width, cell / field.Width);
                maxDepth = Mathf.Max(maxDepth, depth);
                volume += depth * field.CellSize * field.CellSize;
            }
            if (wet.Count == 0) return null;
            var systemId = ResolveRiverSystem(outlet, routing, riverCells, riverSystems);
            if (profile.RequireLakeRiverConnection && systemId == 0)
                return null;
            if (MinDistance(routing.OceanDistance, wet) * field.CellSize < profile.LakeMinimumCoastDistanceMeters)
                return null;
            var area = wet.Count * field.CellSize * field.CellSize;
            if (area < profile.LakeMinAreaMetersSq ||
                maxDepth < profile.LakeMinimumDepthMeters || volume < profile.LakeMinimumVolumeMetersCubed)
                return null;
            // Individual max-area fraction is a soft budget hint only; total area cap below applies.
            var bounds = ComputeBounds(field, wet);
            var shortest = Mathf.Max(1f, Mathf.Min(bounds.width, bounds.height));
            if (Mathf.Max(bounds.width, bounds.height) / shortest > profile.LakeMaximumAspectRatio)
                return null;
            var outline = LakeOutlineBuilder.Build(field, wet);
            if (outline.Length < 4) return null;
            var center = sum / wet.Count;
            return new LakeRecord
            {
                StableId = source.StableId,
                CenterXZ = center,
                SurfaceWorldY = spill,
                AreaMetersSq = area,
                MaxDepthMeters = maxDepth,
                BoundsXZ = bounds,
                OutlineXZ = outline,
                EstimatedVolumeMetersCubed = volume,
                HasOutlet = outlet >= 0,
                OutletXZ = outlet >= 0 ? field.CellCenterXZ(outlet % field.Width, outlet / field.Width) : default,
                ConnectedRiverSystemStableId = systemId,
                BasinCellCentersXZ = ToCenters(field, wet)
            };
        }

        private static ulong ResolveRiverSystem(
            int outlet,
            RoutingContext routing,
            HashSet<int> riverCells,
            Dictionary<int, ulong> riverSystems)
        {
            var seen = new HashSet<int>();
            var cursor = outlet;
            var flowTo = routing.FlowTo;
            var oceanMask = routing.OceanMask;
            for (var guard = 0; cursor >= 0 && cursor < flowTo.Length && guard++ < flowTo.Length; guard++)
            {
                if (!seen.Add(cursor)) return 0;
                if (riverSystems.TryGetValue(cursor, out var systemId)) return systemId;
                if (riverCells.Contains(cursor) && riverSystems.TryGetValue(cursor, out systemId)) return systemId;
                if (oceanMask != null && cursor < oceanMask.Length && oceanMask[cursor]) return 0;
                cursor = flowTo[cursor];
            }
            return 0;
        }

        private static HashSet<int> BuildRiverCells(
            LandformField field,
            RiverPolyline[] rivers,
            out Dictionary<int, ulong> systems)
        {
            systems = new Dictionary<int, ulong>();
            var result = new HashSet<int>();
            if (rivers == null) return result;
            for (var r = 0; r < rivers.Length; r++)
            {
                var river = rivers[r];
                if (river?.PointsXZ == null) continue;
                for (var p = 0; p < river.PointsXZ.Length; p++)
                {
                    var x = Mathf.Clamp(Mathf.FloorToInt((river.PointsXZ[p].x - field.OriginXZ.x) / field.CellSize), 0, field.Width - 1);
                    var z = Mathf.Clamp(Mathf.FloorToInt((river.PointsXZ[p].y - field.OriginXZ.y) / field.CellSize), 0, field.Height - 1);
                    var cell = field.Index(x, z);
                    result.Add(cell);
                    systems[cell] = river.RiverSystemStableId;
                }
            }
            return result;
        }

        private static HashSet<int> ToCellSet(LandformField field, Vector2[] centers)
        {
            var result = new HashSet<int>();
            if (centers == null) return result;
            for (var i = 0; i < centers.Length; i++)
            {
                var x = Mathf.FloorToInt((centers[i].x - field.OriginXZ.x) / field.CellSize);
                var z = Mathf.FloorToInt((centers[i].y - field.OriginXZ.y) / field.CellSize);
                if (x >= 0 && z >= 0 && x < field.Width && z < field.Height)
                    result.Add(field.Index(x, z));
            }
            return result;
        }

        /// <summary>Finds the lowest neighbour cell of the basin; the spill height itself comes from the fill surface.</summary>
        private static void FindSpill(LandformField field, float[] terrain, HashSet<int> basin, out int outlet)
        {
            var best = float.PositiveInfinity;
            outlet = -1;
            foreach (var cell in basin)
            {
                var x = cell % field.Width;
                var z = cell / field.Width;
                for (var i = 0; i < NeighborOffsets.Length; i++)
                {
                    var nx = x + NeighborOffsets[i].Dx;
                    var nz = z + NeighborOffsets[i].Dz;
                    if (nx < 0 || nz < 0 || nx >= field.Width || nz >= field.Height) continue;
                    var neighbour = field.Index(nx, nz);
                    if (basin.Contains(neighbour) || terrain[neighbour] >= best) continue;
                    best = terrain[neighbour];
                    outlet = neighbour;
                }
            }
        }

        private static Rect ComputeBounds(LandformField field, IReadOnlyList<int> cells)
        {
            var first = field.CellCenterXZ(cells[0] % field.Width, cells[0] / field.Width);
            var minX = first.x; var maxX = first.x; var minZ = first.y; var maxZ = first.y;
            for (var i = 1; i < cells.Count; i++)
            {
                var p = field.CellCenterXZ(cells[i] % field.Width, cells[i] / field.Width);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y);
            }
            var half = field.CellSize * 0.5f;
            return Rect.MinMaxRect(minX - half, minZ - half, maxX + half, maxZ + half);
        }

        private static Vector2[] ToCenters(LandformField field, IReadOnlyList<int> cells)
        {
            var result = new Vector2[cells.Count];
            for (var i = 0; i < cells.Count; i++) result[i] = field.CellCenterXZ(cells[i] % field.Width, cells[i] / field.Width);
            return result;
        }

        private static int CountPlayable(LandformField field, bool[] oceanMask)
        {
            var count = 0;
            for (var i = 0; i < field.Width * field.Height; i++)
                if (oceanMask == null || i >= oceanMask.Length || !oceanMask[i]) count++;
            return count;
        }

        private static int MinDistance(int[] distances, IReadOnlyList<int> cells)
        {
            var result = int.MaxValue;
            for (var i = 0; i < cells.Count; i++) result = Mathf.Min(result, distances[cells[i]]);
            return result;
        }

        private static int[] BuildOceanDistance(LandformField field, bool[] oceanMask)
        {
            var result = new int[field.Width * field.Height];
            for (var i = 0; i < result.Length; i++) result[i] = int.MaxValue;
            var queue = new Queue<int>();
            SeedOceanCells(result, oceanMask, queue);
            while (queue.Count > 0)
                ExpandOceanFront(field, queue, result);
            return result;
        }

        private static void SeedOceanCells(int[] distances, bool[] oceanMask, Queue<int> queue)
        {
            for (var i = 0; i < distances.Length; i++)
            {
                if (oceanMask == null || i >= oceanMask.Length || !oceanMask[i]) continue;
                distances[i] = 0;
                queue.Enqueue(i);
            }
        }

        private static void ExpandOceanFront(LandformField field, Queue<int> queue, int[] distances)
        {
            var cell = queue.Dequeue();
            var x = cell % field.Width;
            var z = cell / field.Width;
            for (var i = 0; i < NeighborOffsets.Length; i++)
            {
                var nx = x + NeighborOffsets[i].Dx;
                var nz = z + NeighborOffsets[i].Dz;
                if (nx < 0 || nz < 0 || nx >= field.Width || nz >= field.Height) continue;
                var next = field.Index(nx, nz);
                if (distances[next] <= distances[cell] + 1) continue;
                distances[next] = distances[cell] + 1;
                queue.Enqueue(next);
            }
        }
    }
}
