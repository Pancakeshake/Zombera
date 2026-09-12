using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Rock-authoritative alphamap strip on seaward pad faces (batched Get/SetAlphamaps).
    /// </summary>
    public static class CityPadSeawardSurfaceStamp
    {
        private const float StripWidthMeters = 28f;

        public static int Apply(
            IReadOnlyList<CityFlattenPad> pads,
            WorldTileCatalog catalog,
            IReadOnlyList<WorldTileCoord> tiles,
            WorldSurfacePainter painter)
        {
            if (pads == null || catalog == null || tiles == null || painter == null)
                return 0;

            var wetRock = painter.ResolveSemanticLayerIndex("WetRock");
            var cliffDark = painter.ResolveSemanticLayerIndex("CliffDark");
            if (wetRock < 0 && cliffDark < 0)
                return 0;

            var stamped = 0;
            for (var p = 0; p < pads.Count; p++)
            {
                var pad = pads[p];
                if (pad == null || !pad.IsCoastal || pad.SeawardNormalXZ.sqrMagnitude < 0.0001f)
                    continue;

                var strip = Mathf.Max(8f, StripWidthMeters);
                var stripBounds = BuildSeawardStripBounds(pad, strip);

                for (var t = 0; t < tiles.Count; t++)
                {
                    if (!catalog.TryGetTile(tiles[t], out var info) ||
                        !WorldTileInfoUtility.TryGetLiveTerrain(info, out var terrain))
                        continue;

                    if (!info.WorldRectXZ.Overlaps(stripBounds))
                        continue;

                    if (StampTerrain(terrain, pad, wetRock, cliffDark, strip, stripBounds))
                    {
                        painter.MarkPaintedDirty(terrain);
                        stamped++;
                    }
                }
            }

            return stamped;
        }

        private static Rect BuildSeawardStripBounds(CityFlattenPad pad, float strip)
        {
            var plateau = pad.PlateauBoundsXZ;
            var padExpand = strip + 2f;
            var baseBounds = Rect.MinMaxRect(
                plateau.xMin - padExpand,
                plateau.yMin - padExpand,
                plateau.xMax + padExpand,
                plateau.yMax + padExpand);

            var seaward = pad.SeawardNormalXZ.normalized;
            // Bias AABB toward the seaward face so inland tiles cull cleanly.
            var shift = strip * 0.5f;
            return Rect.MinMaxRect(
                baseBounds.xMin + Mathf.Min(0f, seaward.x) * shift * 2f,
                baseBounds.yMin + Mathf.Min(0f, seaward.y) * shift * 2f,
                baseBounds.xMax + Mathf.Max(0f, seaward.x) * shift * 2f,
                baseBounds.yMax + Mathf.Max(0f, seaward.y) * shift * 2f);
        }

        private static bool StampTerrain(
            Terrain terrain,
            CityFlattenPad pad,
            int wetRock,
            int cliffDark,
            float strip,
            Rect stripBounds)
        {
            var data = terrain.terrainData;
            if (data == null)
                return false;

            var alphamapW = data.alphamapWidth;
            var alphamapH = data.alphamapHeight;
            var layerCount = data.alphamapLayers;
            if (alphamapW < 2 || alphamapH < 2 || layerCount < 1)
                return false;

            var pos = terrain.transform.position;
            var size = data.size;
            if (!TryBuildAlphamapWindow(
                    stripBounds, pos, size, alphamapW, alphamapH,
                    out var startX, out var startZ, out var width, out var height))
                return false;

            var seaward = pad.SeawardNormalXZ.normalized;
            var map = data.GetAlphamaps(startX, startZ, width, height);
            var changed = PaintAlphamapWindow(
                map, pad, wetRock, cliffDark, strip, seaward,
                pos, size, startX, startZ, alphamapW, alphamapH, width, height, layerCount);
            if (!changed)
                return false;

            data.SetAlphamaps(startX, startZ, map);
            return true;
        }

        private static bool PaintAlphamapWindow(
            float[,,] map,
            CityFlattenPad pad,
            int wetRock,
            int cliffDark,
            float strip,
            Vector2 seaward,
            Vector3 pos,
            Vector3 size,
            int startX,
            int startZ,
            int alphamapW,
            int alphamapH,
            int width,
            int height,
            int layerCount)
        {
            var changed = false;
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (!TryPaintQuayCell(
                            map, pad, wetRock, cliffDark, strip, seaward,
                            pos, size, startX, startZ, alphamapW, alphamapH, x, z, layerCount))
                        continue;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool TryPaintQuayCell(
            float[,,] map,
            CityFlattenPad pad,
            int wetRock,
            int cliffDark,
            float strip,
            Vector2 seaward,
            Vector3 pos,
            Vector3 size,
            int startX,
            int startZ,
            int alphamapW,
            int alphamapH,
            int x,
            int z,
            int layerCount)
        {
            var wx = pos.x + (startX + x + 0.5f) / alphamapW * size.x;
            var wz = pos.z + (startZ + z + 0.5f) / alphamapH * size.z;
            var world = new Vector2(wx, wz);
            var d = CityPadLandformFlattener.RoundedRectDistanceOutside(
                world, pad.PlateauBoundsXZ, 0.2f);
            if (d < -2f || d > strip)
                return false;

            var fromCenter = world - pad.CenterXZ;
            if (!CityCoastalPadUtility.IsSeawardBearing(fromCenter, seaward, 0.05f))
                return false;

            var rockT = d <= 0f
                ? 0.35f
                : Mathf.Clamp01(1f - d / strip);
            if (rockT <= 0.05f)
                return false;

            SuppressLayers(map, z, x, layerCount, rockT * 0.92f);
            if (cliffDark >= 0 && cliffDark < layerCount)
                map[z, x, cliffDark] += rockT * 0.55f;
            if (wetRock >= 0 && wetRock < layerCount)
                map[z, x, wetRock] += rockT * 0.45f;
            NormalizeCell(map, z, x, layerCount);
            return true;
        }

        private static bool TryBuildAlphamapWindow(
            Rect worldBounds,
            Vector3 terrainPos,
            Vector3 terrainSize,
            int alphamapW,
            int alphamapH,
            out int startX,
            out int startZ,
            out int width,
            out int height)
        {
            startX = startZ = width = height = 0;
            if (terrainSize.x <= 0.01f || terrainSize.z <= 0.01f)
                return false;

            var minX = Mathf.Clamp01((worldBounds.xMin - terrainPos.x) / terrainSize.x);
            var maxX = Mathf.Clamp01((worldBounds.xMax - terrainPos.x) / terrainSize.x);
            var minZ = Mathf.Clamp01((worldBounds.yMin - terrainPos.z) / terrainSize.z);
            var maxZ = Mathf.Clamp01((worldBounds.yMax - terrainPos.z) / terrainSize.z);

            var x0 = Mathf.Clamp(Mathf.FloorToInt(minX * alphamapW), 0, alphamapW - 1);
            var x1 = Mathf.Clamp(Mathf.CeilToInt(maxX * alphamapW), 0, alphamapW);
            var z0 = Mathf.Clamp(Mathf.FloorToInt(minZ * alphamapH), 0, alphamapH - 1);
            var z1 = Mathf.Clamp(Mathf.CeilToInt(maxZ * alphamapH), 0, alphamapH);
            if (x1 <= x0 || z1 <= z0)
                return false;

            startX = x0;
            startZ = z0;
            width = x1 - x0;
            height = z1 - z0;
            return width > 0 && height > 0;
        }

        private static void SuppressLayers(float[,,] map, int z, int x, int layerCount, float strength)
        {
            strength = Mathf.Clamp01(strength);
            for (var l = 0; l < layerCount; l++)
                map[z, x, l] *= 1f - strength;
        }

        private static void NormalizeCell(float[,,] map, int z, int x, int layerCount)
        {
            var sum = 0f;
            for (var l = 0; l < layerCount; l++)
                sum += map[z, x, l];
            if (sum <= 0.0001f)
            {
                map[z, x, 0] = 1f;
                return;
            }

            var inv = 1f / sum;
            for (var l = 0; l < layerCount; l++)
                map[z, x, l] *= inv;
        }
    }
}
