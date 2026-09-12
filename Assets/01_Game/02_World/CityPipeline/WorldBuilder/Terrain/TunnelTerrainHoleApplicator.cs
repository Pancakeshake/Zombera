using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Punch small Unity Terrain holes only at tunnel mouths (portal daylight).
    ///     Does not cut a full entry→exit slit through the mountain.
    /// </summary>
    public static class TunnelTerrainHoleApplicator
    {
        private const float FallbackClearHalfWidthMeters = 4.25f;
        private const float FallbackInnerLipMeters = 6f;

        public static void ApplyMouthHoles(
            IReadOnlyList<MountainTunnel> tunnels,
            RoadNetworkSettings settings)
        {
            if (tunnels == null || tunnels.Count == 0 || settings == null || !settings.tunnelEnterable)
                return;

            var snapped = new List<MountainTunnel>(tunnels.Count);
            for (var i = 0; i < tunnels.Count; i++)
                snapped.Add(tunnels[i]);
            TunnelMouthMarkerUtility.ApplySceneMarkersInPlace(snapped);

            for (var i = 0; i < snapped.Count; i++)
                ApplyMouthOnlyHoles(snapped[i], settings);
        }

        public static void ClearHolesOnScopedTerrains(IReadOnlyList<WorldTileCoord> tiles, WorldTileCatalog catalog)
        {
            if (tiles == null || catalog == null)
                return;

            for (var i = 0; i < tiles.Count; i++)
            {
                if (!catalog.TryGetTile(tiles[i], out var info) || info.Terrain?.terrainData == null)
                    continue;
                ClearAllHoles(info.Terrain);
            }
        }

        public static void ClearAllHoles(Terrain terrain)
        {
            if (terrain?.terrainData == null)
                return;

            var td = terrain.terrainData;
            var res = td.holesResolution;
            if (res <= 1)
                return;

            var holes = td.GetHoles(0, 0, res, res);
            var changed = false;
            for (var z = 0; z < res; z++)
            {
                for (var x = 0; x < res; x++)
                {
                    if (holes[z, x])
                        continue;
                    holes[z, x] = true;
                    changed = true;
                }
            }

            if (changed)
                td.SetHoles(0, 0, holes);
        }

        private static void ApplyMouthOnlyHoles(MountainTunnel tunnel, RoadNetworkSettings settings)
        {
            ResolvePortalCutFootprint(settings, out var halfWidth, out var inner);
            var approach = Mathf.Clamp(settings.tunnelHoleApproachMeters, 2f, 6f);
            var fwd = tunnel.ForwardXZ;

            // Entry mouth only — short pad outside + short lip into the bore.
            PaintSegmentHole(
                tunnel.EntryXZ - fwd * approach,
                tunnel.EntryXZ + fwd * inner,
                halfWidth);

            // Exit mouth only.
            PaintSegmentHole(
                tunnel.ExitXZ + fwd * approach,
                tunnel.ExitXZ - fwd * inner,
                halfWidth);
        }

        private static void PaintSegmentHole(Vector2 a, Vector2 b, float halfWidth)
        {
            var margin = halfWidth + 2f;
            var bounds = Rect.MinMaxRect(
                Mathf.Min(a.x, b.x) - margin,
                Mathf.Min(a.y, b.y) - margin,
                Mathf.Max(a.x, b.x) + margin,
                Mathf.Max(a.y, b.y) + margin);

            if (!WorldTileInfoUtility.TryResolveAllTerrainsOverlapping(bounds, out var terrains) ||
                terrains.Count == 0)
                return;

            for (var t = 0; t < terrains.Count; t++)
                PaintHoleOnTerrain(terrains[t], a, b, halfWidth);
        }

        private static void PaintHoleOnTerrain(Terrain terrain, Vector2 a, Vector2 b, float halfWidth)
        {
            if (terrain?.terrainData == null)
                return;

            var td = terrain.terrainData;
            var origin = terrain.transform.position;
            var size = td.size;
            var res = td.holesResolution;
            if (res <= 1 || size.x < 0.01f || size.z < 0.01f)
                return;

            var margin = halfWidth + 2f;
            var minX = Mathf.Min(a.x, b.x) - margin;
            var maxX = Mathf.Max(a.x, b.x) + margin;
            var minZ = Mathf.Min(a.y, b.y) - margin;
            var maxZ = Mathf.Max(a.y, b.y) + margin;

            if (!TryHoleRegion(minX, maxX, origin.x, size.x, res, out var startX, out var width))
                return;
            if (!TryHoleRegion(minZ, maxZ, origin.z, size.z, res, out var startZ, out var height))
                return;

            var holes = td.GetHoles(startX, startZ, width, height);
            var changed = false;

            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var wx = origin.x + ((startX + x) / (float)(res - 1)) * size.x;
                    var wz = origin.z + ((startZ + z) / (float)(res - 1)) * size.z;
                    if (!IsInsideOrientedRectangle(new Vector2(wx, wz), a, b, halfWidth))
                        continue;
                    if (!holes[z, x])
                        continue;
                    holes[z, x] = false;
                    changed = true;
                }
            }

            if (changed)
                td.SetHoles(startX, startZ, holes);
        }

        private static bool TryHoleRegion(
            float worldMin,
            float worldMax,
            float origin,
            float size,
            int res,
            out int start,
            out int count)
        {
            start = 0;
            count = 0;
            if (size <= 0.01f || res <= 1)
                return false;

            var u0 = (worldMin - origin) / size;
            var u1 = (worldMax - origin) / size;
            var i0 = Mathf.FloorToInt(Mathf.Clamp01(Mathf.Min(u0, u1)) * (res - 1));
            var i1 = Mathf.CeilToInt(Mathf.Clamp01(Mathf.Max(u0, u1)) * (res - 1));
            start = Mathf.Clamp(i0, 0, res - 1);
            var end = Mathf.Clamp(i1, 0, res - 1);
            count = end - start + 1;
            return count > 0;
        }

        private static void ResolvePortalCutFootprint(
            RoadNetworkSettings settings,
            out float halfWidth,
            out float innerDepth)
        {
            halfWidth = FallbackClearHalfWidthMeters;
            innerDepth = FallbackInnerLipMeters;
            var boreSource = settings.tunnelMidPrefab != null
                ? settings.tunnelMidPrefab
                : settings.tunnelPortalPrefab;
            if (InfrastructureKitSockets.TryGetBoreClearHalfWidth(boreSource, out var clearHalfWidth))
                halfWidth = clearHalfWidth;

            if (InfrastructureKitSockets.TryGetPortalCutFootprint(
                    settings.tunnelPortalPrefab,
                    out _,
                    out var socketDepth))
                innerDepth = socketDepth;
        }

        private static bool IsInsideOrientedRectangle(
            Vector2 point,
            Vector2 start,
            Vector2 end,
            float halfWidth)
        {
            var forward = end - start;
            var length = forward.magnitude;
            if (length < 0.0001f)
                return Vector2.Distance(point, start) <= halfWidth;

            forward /= length;
            var delta = point - start;
            var along = Vector2.Dot(delta, forward);
            if (along < 0f || along > length)
                return false;

            var lateral = Mathf.Abs(delta.x * forward.y - delta.y * forward.x);
            return lateral <= halfWidth;
        }

        internal static bool IsInsideMouthFootprint(
            Vector2 point,
            Vector2 start,
            Vector2 end,
            float halfWidth) =>
            IsInsideOrientedRectangle(point, start, end, halfWidth);
    }
}
