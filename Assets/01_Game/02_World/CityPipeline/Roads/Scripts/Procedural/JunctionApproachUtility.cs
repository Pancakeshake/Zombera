using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    public readonly struct JunctionApproach
    {
        public Vector2 JunctionCenterXZ { get; }
        public Vector2 ApproachDir { get; }
        public float RoadHalfWidth { get; }
        public float StopLineDistance { get; }

        public JunctionApproach(
            Vector2 junctionCenterXZ,
            Vector2 approachDir,
            float roadHalfWidth,
            float stopLineDistance)
        {
            JunctionCenterXZ = junctionCenterXZ;
            ApproachDir = approachDir;
            RoadHalfWidth = roadHalfWidth;
            StopLineDistance = stopLineDistance;
        }
    }

    /// <summary>
    ///     Polyline-derived junction approaches for procedural streetscape placement.
    /// </summary>
    public static class JunctionApproachUtility
    {
        public static bool TryGetApproaches(
            JunctionRecord junction,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            out JunctionApproach[] approaches)
        {
            approaches = null;
            if (junction.OutwardDirs == null || junction.OutwardDirs.Length < 2)
                return false;

            var stopOffset = settings != null
                ? Mathf.Max(0.5f, settings.junctionStopLineOffsetMeters)
                : 1.8f;
            var halfWidth = junction.RoadWidthMeters > 0f
                ? junction.RoadWidthMeters * 0.5f
                : ResolveDefaultHalfWidth(roads, junction.PositionXZ, settings);

            var buffer = new List<JunctionApproach>(junction.OutwardDirs.Length);
            for (var i = 0; i < junction.OutwardDirs.Length; i++)
            {
                var outward = junction.OutwardDirs[i];
                if (outward.sqrMagnitude < 0.0001f)
                    continue;

                var approachDir = -outward.normalized;
                buffer.Add(new JunctionApproach(
                    junction.PositionXZ,
                    approachDir,
                    halfWidth,
                    stopOffset));
            }

            if (buffer.Count == 0)
                return false;

            approaches = buffer.ToArray();
            return true;
        }

        public static void CollectApproaches(
            IReadOnlyList<JunctionRecord> junctions,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            List<JunctionApproach> buffer)
        {
            buffer?.Clear();
            if (buffer == null || junctions == null || junctions.Count == 0)
                return;

            for (var i = 0; i < junctions.Count; i++)
            {
                if (!TryGetApproaches(junctions[i], roads, settings, out var approaches))
                    continue;

                for (var a = 0; a < approaches.Length; a++)
                    buffer.Add(approaches[a]);
            }
        }

        private static float ResolveDefaultHalfWidth(
            IReadOnlyList<RoadPolyline> roads,
            Vector2 junctionPos,
            RoadNetworkSettings settings)
        {
            var bestDistSq = float.MaxValue;
            var width = settings != null ? settings.ResolveWidthMeters(RoadClass.Local) : 6f;

            if (roads == null)
                return width * 0.5f;

            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                for (var p = 0; p < road.pointsXZ.Count; p++)
                {
                    var distSq = (road.pointsXZ[p] - junctionPos).sqrMagnitude;
                    if (distSq >= bestDistSq)
                        continue;

                    bestDistSq = distSq;
                    width = road.widthMeters > 0f
                        ? road.widthMeters
                        : settings != null
                            ? settings.ResolveWidthMeters(road.roadClass)
                            : 6f;
                }
            }

            return Mathf.Max(0.5f, width) * 0.5f;
        }
    }
}
