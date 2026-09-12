using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Named-area-driven street lamp placement for the city hub. Walks each
    ///     district block's street-facing outline edges and places lamps at a fixed
    ///     offset outside the block boundary — on the footpath between the block and
    ///     the road — so placement never depends on road widths or centerlines.
    ///     Mirrors <see cref="CityPowerLinePlacer"/>: grid-aligned spacing slots along
    ///     the street axis plus a defensive road-surface clearance check.
    /// </summary>
    public static partial class CityStreetLampPlacer
    {
        private const float AxisAlignedEdgeEpsilonMeters = 0.05f;
        private const float RoadSurfaceClearanceMeters = 4f;
        private const float MinEdgeRunMeters = 2f;

        /// <summary>
        ///     Places street lamps around every named area in <paramref name="areasRoot"/>.
        ///     Each axis-aligned street-facing outline edge contributes one lamp line
        ///     (offset outward toward the road), so every street receives lamps on
        ///     both sides from its two neighbouring blocks.
        /// </summary>
        /// <returns>Total number of lamp instances created.</returns>
        public static int PlaceStreetLampsForAreas(
            Transform parent,
            Transform areasRoot,
            IReadOnlyList<RoadPolyline> roads,
            RoadNetworkSettings settings,
            GameObject lampPrefab,
            Func<Vector2, float> resolveHeight)
        {
            if (parent == null || areasRoot == null || settings == null || resolveHeight == null)
                return 0;
            if (!settings.spawnStreetLamps || lampPrefab == null)
                return 0;

            var spacing = Mathf.Max(1f, settings.streetLampSpacingMeters);
            var offsetMeters = Mathf.Max(0.1f, settings.streetLampNamedAreaOffsetMeters);
            var cornerSetback = Mathf.Max(0f, settings.markerSnapDistanceMeters);
            var lampIndex = 0;
            var placed = 0;

            for (var a = 0; a < areasRoot.childCount; a++)
            {
                var marker = areasRoot.GetChild(a).GetComponent<CityNamedAreaMarker>();
                if (marker == null)
                    continue;

                var outline = marker.GetHubShiftedOutlineXZ();
                if (outline == null || outline.Count < 4)
                    continue;

                var centroid = ComputeCentroid(outline);
                for (var e = 0; e < outline.Count; e++)
                {
                    var edgeStart = outline[e];
                    var edgeEnd = outline[(e + 1) % outline.Count];

                    // Only straight, axis-aligned street edges — fillet/arc segments
                    // at rounded corners are skipped (lamps stop at the corner setback).
                    if (!IsAxisAlignedEdge(edgeStart, edgeEnd))
                        continue;

                    placed += PlaceLampsAlongEdge(
                        parent, edgeStart, edgeEnd, centroid, offsetMeters,
                        cornerSetback, spacing, lampPrefab, resolveHeight,
                        marker.AreaId, e, ref lampIndex, roads);
                }
            }

            return placed;
        }

        private static int PlaceLampsAlongEdge(
            Transform parent,
            Vector2 edgeStart,
            Vector2 edgeEnd,
            Vector2 centroid,
            float offsetMeters,
            float cornerSetback,
            float spacing,
            GameObject prefab,
            Func<Vector2, float> resolveHeight,
            int areaId,
            int edgeIndex,
            ref int lampIndex,
            IReadOnlyList<RoadPolyline> roads)
        {
            var dir = edgeEnd - edgeStart;
            var edgeLen = dir.magnitude;
            if (edgeLen < MinEdgeRunMeters)
                return 0;

            dir /= edgeLen;
            var outward = -ResolveInwardNormal(edgeStart, edgeEnd, centroid);

            // Trim both ends so lamps stay clear of junction crossing surfaces.
            var setback = Mathf.Min(cornerSetback, edgeLen * 0.45f);
            var segMinXZ = edgeStart + dir * setback;
            var segMaxXZ = edgeEnd - dir * setback;

            var isHorizontal = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
            var deepCoord = isHorizontal
                ? segMinXZ.y + outward.y * offsetMeters
                : segMinXZ.x + outward.x * offsetMeters;
            var alongMin = isHorizontal ? segMinXZ.x : segMinXZ.y;
            var alongMax = isHorizontal ? segMaxXZ.x : segMaxXZ.y;
            if (alongMax < alongMin)
                (alongMin, alongMax) = (alongMax, alongMin);

            // Face along the street (same convention as the old road-based placement).
            var facing = Quaternion.LookRotation(new Vector3(-dir.x, 0f, -dir.y), Vector3.up);
            var placed = 0;

            // Grid-aligned slots (multiples of the spacing along the world axis) so
            // lamps on neighbouring blocks line up into one uniform street run.
            var first = Mathf.Ceil(alongMin / spacing) * spacing;
            for (var t = first; t <= alongMax + 0.01f; t += spacing)
            {
                var pos = isHorizontal
                    ? new Vector2(t, deepCoord)
                    : new Vector2(deepCoord, t);

                // Defensive: never let a lamp stand on the road surface itself.
                if (TooCloseToAnyRoad(pos, roads, RoadSurfaceClearanceMeters))
                    continue;

                var world = new Vector3(pos.x, resolveHeight(pos), pos.y);
                var instance = UnityEngine.Object.Instantiate(prefab, world, facing, parent);
                instance.name = $"Lamp_A{areaId}_e{edgeIndex}_{lampIndex++}";
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Place Street Lamp");
#endif
                placed++;
            }

            return placed;
        }

        private static bool IsAxisAlignedEdge(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < AxisAlignedEdgeEpsilonMeters
                   || Mathf.Abs(a.y - b.y) < AxisAlignedEdgeEpsilonMeters;
        }

        private static Vector2 ResolveInwardNormal(Vector2 edgeStart, Vector2 edgeEnd, Vector2 centroid)
        {
            var tangent = (edgeEnd - edgeStart).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
                return Vector2.up;

            var left = new Vector2(-tangent.y, tangent.x);
            var right = new Vector2(tangent.y, -tangent.x);
            var mid = (edgeStart + edgeEnd) * 0.5f;
            var toCentroid = centroid - mid;
            if (toCentroid.sqrMagnitude < 0.0001f)
                return left;

            return Vector2.Dot(left, toCentroid) >= Vector2.Dot(right, toCentroid) ? left : right;
        }

        private static Vector2 ComputeCentroid(IReadOnlyList<Vector2> outline)
        {
            var sum = Vector2.zero;
            for (var i = 0; i < outline.Count; i++)
                sum += outline[i];

            return sum / outline.Count;
        }

        private static bool TooCloseToAnyRoad(
            Vector2 point,
            IReadOnlyList<RoadPolyline> roads,
            float minDist)
        {
            if (roads == null || roads.Count == 0)
                return false;

            for (var r = 0; r < roads.Count; r++)
            {
                var road = roads[r];
                if (road?.pointsXZ == null || road.pointsXZ.Count < 2)
                    continue;

                for (var i = 1; i < road.pointsXZ.Count; i++)
                {
                    var a = road.pointsXZ[i - 1];
                    var b = road.pointsXZ[i];
                    var ab = b - a;
                    var t = Vector2.Dot(point - a, ab) / Mathf.Max(0.0001f, Vector2.Dot(ab, ab));
                    t = Mathf.Clamp01(t);
                    if (Vector2.Distance(point, a + t * ab) < minDist)
                        return true;
                }
            }

            return false;
        }
    }
}
