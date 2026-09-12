using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Places street lamp prefabs on both sides of road polylines at regular intervals.
    ///     Reuses RoadMeshBuilder offset/resample logic to compute left/right placement lines
    ///     outward from the road edge, then instantiates lamps every N meters.
    ///     Supports both Terrain.SampleHeight (runtime) and Func&lt;Vector2, float&gt; resolveHeight
    ///     (editor/prefab builder). The city hub uses the named-area-driven placement in
    ///     <see cref="CityStreetLampPlacer.Areas"/> (partial) instead.
    /// </summary>
    public static partial class CityStreetLampPlacer
    {
        // ── Terrain-based overload (runtime tile processing) ──

        /// <summary>
        ///     Place street lamps on both sides of <paramref name="road" /> whose world-space
        ///     positions fall inside <paramref name="tileQueryRect" />. Lamps are parented to
        ///     <paramref name="parent" />. Uses Terrain.SampleHeight for Y.
        /// </summary>
        /// <returns>Total number of lamp instances created.</returns>
        public static int PlaceStreetLamps(
            Transform parent,
            Terrain terrain,
            RoadPolyline road,
            RoadNetworkSettings settings,
            Rect tileQueryRect)
        {
            if (parent == null || terrain == null || terrain.terrainData == null) return 0;
            if (road == null || road.pointsXZ == null || road.pointsXZ.Count < 2) return 0;
            if (settings == null || !settings.spawnStreetLamps || settings.streetLampPrefab == null) return 0;

            var terrainOriginY = terrain.transform.position.y;
            float ResolveY(Vector2 xz) => terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrainOriginY;

            return PlaceStreetLampsCore(parent, road, settings, tileQueryRect, ResolveY);
        }

        // ── resolveHeight overload (editor / prefab builder) ──

        /// <summary>
        ///     Place street lamps using a custom height resolver (e.g. raycast or flat ground).
        ///     Lamps whose XZ falls outside <paramref name="boundRect" /> are skipped.
        /// </summary>
        public static int PlaceStreetLamps(
            Transform parent,
            RoadPolyline road,
            RoadNetworkSettings settings,
            Rect boundRect,
            Func<Vector2, float> resolveHeight)
        {
            if (parent == null) return 0;
            if (road == null || road.pointsXZ == null || road.pointsXZ.Count < 2) return 0;
            if (settings == null || !settings.spawnStreetLamps || settings.streetLampPrefab == null) return 0;
            if (resolveHeight == null) return 0;

            return PlaceStreetLampsCore(parent, road, settings, boundRect, resolveHeight);
        }

        // ── Core shared implementation ──

        private static float ResolveRoadHalfWidthMeters(RoadPolyline road, RoadNetworkSettings settings)
        {
            if (road.widthMeters > 0.5f)
                return road.widthMeters * 0.5f;
            return settings.ResolveWidthMeters(road.roadClass) * 0.5f;
        }

        private static int PlaceStreetLampsCore(
            Transform parent,
            RoadPolyline road,
            RoadNetworkSettings settings,
            Rect boundRect,
            Func<Vector2, float> resolveHeight)
        {
            var spacing = Mathf.Max(1f, settings.streetLampSpacingMeters);
            var junctionExclusion = Mathf.Max(0f, settings.junctionRoadsideExclusionRadiusMeters);

            var jctStart = road.pointsXZ[0];
            var jctEnd = road.pointsXZ[road.pointsXZ.Count - 1];
            var lampIndex = 0;

            if (IsDualSidedPrefab(settings.streetLampPrefab))
            {
                lampIndex += PlaceLampsAlongCenterline(parent, road.pointsXZ, 0f, spacing,
                    settings.streetLampPrefab, $"Lamp_Road{road.id}", boundRect, resolveHeight, ref lampIndex, 0f, junctionExclusion, jctStart, jctEnd, allJunctions: null);
            }
            else
            {
                var halfRoad = ResolveRoadHalfWidthMeters(road, settings);
                // RoadPolyline.widthMeters is the script-side value set at generation time (e.g. 6 m).
                // EasyRoads builds the actual road mesh from the road type (view under Road Types
                // in the EasyRoads Road Network inspector). The current road type is 7.5 m surface
                // + 0.5 m sidewalks = 4.25 m half-width. Clamp to that minimum so the lamp always
                // clears the real mesh even when the script width is narrower.
                const float actualRoadSurfaceHalf = 3.75f;
                var sidewalkWidth = settings.spawnSidewalkMeshes ? settings.ResolveSidewalkWidthMeters() : 0f;
                var effectiveHalfRoad = Mathf.Max(halfRoad, actualRoadSurfaceHalf) + sidewalkWidth;
                var lampOffset = effectiveHalfRoad + Mathf.Max(0f, settings.streetLampOffsetMeters);

                // NOTE: Do NOT swap the 0f / 180f yaw values below (see earlier comment).
                lampIndex += PlaceLampsAlongCenterline(parent, road.pointsXZ, -lampOffset, spacing,
                    settings.streetLampPrefab, $"Lamp_L_Road{road.id}", boundRect, resolveHeight, ref lampIndex, 0f, junctionExclusion, jctStart, jctEnd, allJunctions: null);
                lampIndex += PlaceLampsAlongCenterline(parent, road.pointsXZ, lampOffset, spacing,
                    settings.streetLampPrefab, $"Lamp_R_Road{road.id}", boundRect, resolveHeight, ref lampIndex, 180f, junctionExclusion, jctStart, jctEnd, allJunctions: null);
            }

            return lampIndex;
        }

        /// <summary>
        ///     Walk the road centerline at regular intervals, offsetting each point laterally
        ///     by <paramref name="lateralOffset"/> (negative = left, positive = right).
        ///     No pre-computed offset polyline — each lamp position is computed directly
        ///     from the centerline, avoiding cumulative drift.
        /// </summary>
        private static int PlaceLampsAlongCenterline(
            Transform parent,
            IReadOnlyList<Vector2> roadCenterXZ,
            float lateralOffset,
            float spacing,
            GameObject prefab,
            string labelPrefix,
            Rect boundRect,
            Func<Vector2, float> resolveHeight,
            ref int startIndex,
            float sideSign,
            float junctionExclusion = 0f,
            Vector2 roadStartJunction = default,
            Vector2 roadEndJunction = default,
            IReadOnlyList<Vector2> allJunctions = null)
        {
            if (roadCenterXZ == null || roadCenterXZ.Count < 2) return 0;

            var samples = RoadMeshBuilder.ResamplePolyline(roadCenterXZ, Mathf.Clamp(spacing * 0.5f, 2f, 8f));
            if (samples.Count < 2) return 0;

            var jctA = roadStartJunction != default ? roadStartJunction : roadCenterXZ[0];
            var jctB = roadEndJunction != default ? roadEndJunction : roadCenterXZ[roadCenterXZ.Count - 1];
            var minJunctionDist = junctionExclusion * 0.85f;

            var rng = new System.Random(labelPrefix.GetHashCode());
            var jitterMagnitude = spacing * 0.20f;

            var nextDistance = 0f;
            var traveled = 0f;
            var placed = 0;

            for (var i = 1; i < samples.Count; i++)
            {
                var a = samples[i - 1];
                var b = samples[i];
                var seg = b - a;
                var segLen = seg.magnitude;
                if (segLen < 0.001f)
                {
                    traveled += segLen;
                    continue;
                }

                var tangent = seg / segLen;
                // Match the road mesh convention: Cross(up, tangent3D) → (tangent.y, -tangent.x) in 2D.
                var lateralDir = new Vector2(tangent.y, -tangent.x);

                while (traveled + segLen >= nextDistance)
                {
                    var localDistance = nextDistance - traveled;
                    var t = Mathf.Clamp01(localDistance / segLen);
                    var centerPoint = Vector2.Lerp(a, b, t);

                    // Offset laterally from the centerline (fresh each time — no drift).
                    var point = centerPoint + lateralDir * lateralOffset;

                    if (!boundRect.Contains(point))
                    {
                        nextDistance += spacing;
                        continue;
                    }

                    if (Vector2.Distance(centerPoint, jctA) < minJunctionDist ||
                        Vector2.Distance(centerPoint, jctB) < minJunctionDist ||
                        TooCloseToAnyJunction(centerPoint, allJunctions, minJunctionDist))
                    {
                        nextDistance += spacing;
                        continue;
                    }

                    var worldPos = new Vector3(point.x, 0f, point.y);
                    worldPos.y = resolveHeight(point);

                    var tangent3 = new Vector3(tangent.x, 0f, tangent.y);
                    var baseRotation = Quaternion.LookRotation(tangent3, Vector3.up);
                    var rotation = baseRotation * Quaternion.Euler(0f, sideSign, 0f);

                    var instance = UnityEngine.Object.Instantiate(prefab, worldPos, rotation, parent);
                    instance.name = $"{labelPrefix}_{startIndex + placed}";
                    placed++;

                    var jitter = (float)(rng.NextDouble() - 0.5) * jitterMagnitude;
                    nextDistance += spacing + jitter;
                }

                traveled += segLen;
            }

            return placed;
        }

        // ── Dual-sided prefab detection ──

        /// <summary>
        ///     Returns true when the prefab has children with significant local-X offsets,
        ///     indicating a dual-sided motorway / centre-median lamp that should be placed
        ///     at the road centerline rather than at each edge.
        /// </summary>
        private static bool IsDualSidedPrefab(GameObject prefab)
        {
            if (prefab == null) return false;
            var t = prefab.transform;
            for (var i = 0; i < t.childCount; i++)
            {
                if (Mathf.Abs(t.GetChild(i).localPosition.x) > 2f)
                    return true;
            }

            return false;
        }

        private static bool TooCloseToAnyJunction(
            Vector2 point,
            IReadOnlyList<Vector2> allJunctions,
            float minDist)
        {
            if (allJunctions == null || allJunctions.Count == 0)
                return false;

            for (var i = 0; i < allJunctions.Count; i++)
            {
                if (Vector2.Distance(point, allJunctions[i]) < minDist)
                    return true;
            }

            return false;
        }
    }
}
