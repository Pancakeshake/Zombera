using System.Diagnostics;
using JBooth.MicroSplat;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Debug = UnityEngine.Debug;

namespace Zombera.World.City
{
    /// <summary>
    ///     Direct MicroSplat control-texture urban wipe for MapMagic tiles.
    ///     Controls can differ in resolution (e.g. _Control0–3 at 513, _Control4
    ///     at 2048) — each map is wiped independently.
    /// </summary>
    public static partial class CityLotTerrainPainter
    {
        // City Surfaces → channels 14/15; lot concrete often lands on higher
        // controls too. Layer 13 noise (~13/255) is ignored via threshold.
        private const int DefaultFirstUrbanLayer = 14;
        private const string ControlTexPrefix = "_Control";
        private const int UrbanProbeStep = 8;
        private const byte UrbanChannelThreshold = 32;

        /// <summary>
        ///     Hub Reset path: force every MicroSplat control in bounds to wilderness
        ///     (layer 0 full weight on _Control0, zeros elsewhere). No alphamap I/O,
        ///     no urban-layer probe — works when paint lives on mismatched resolutions
        ///     (_Control4 @ 2048) or on non-urban concrete layers.
        /// </summary>
        public static void HardWipeControlsInBounds(Rect areaBoundsXZ)
        {
            _cachedPaintableTerrains = null;
            if (areaBoundsXZ.width <= 0f || areaBoundsXZ.height <= 0f)
            {
                Debug.LogWarning("[CityLotTerrainPainter] HardWipeControlsInBounds skipped — invalid bounds.");
                return;
            }

            var watch = Stopwatch.StartNew();
            var terrains = ResolveAllOverlappingTerrains(areaBoundsXZ);
            if (terrains.Count == 0)
            {
                Debug.LogWarning(
                    "[CityLotTerrainPainter] HardWipeControlsInBounds — no terrains overlap " +
                    areaBoundsXZ + ".");
                return;
            }

            var wipedTiles = 0;
            var wipedMaps = 0;

            for (var t = 0; t < terrains.Count; t++)
            {
                var terrain = terrains[t];
                if (!WorldTileInfoUtility.TryGetLiveTerrain(
                        new WorldTileInfo(default, default, terrain, WorldTileState.None),
                        out terrain))
                    continue;

                if (!TryHardWipeControlsOnTerrain(terrain, areaBoundsXZ, out var maps))
                    continue;

                wipedTiles++;
                wipedMaps += maps;
            }

            Debug.Log(
                "[CityLotTerrainPainter] HardWipeControlsInBounds terrains=" + terrains.Count +
                " wipedTiles=" + wipedTiles + " maps=" + wipedMaps +
                " wall=" + watch.ElapsedMilliseconds + "ms bounds=" + areaBoundsXZ + ".");
        }

        /// <summary>
        ///     Hub Reset path: wipe urban channels on every MicroSplat control
        ///     map overlapping bounds (per-texture resolution).
        /// </summary>
        public static void ClearUrbanPaintInBounds(Rect areaBoundsXZ, int firstUrbanLayer = DefaultFirstUrbanLayer)
        {
            if (areaBoundsXZ.width <= 0f || areaBoundsXZ.height <= 0f)
            {
                Debug.LogWarning("[CityLotTerrainPainter] ClearUrbanPaintInBounds skipped — invalid bounds.");
                return;
            }

            var watch = Stopwatch.StartNew();
            var terrains = ResolveAllOverlappingTerrains(areaBoundsXZ);
            if (terrains.Count == 0)
            {
                Debug.LogWarning(
                    "[CityLotTerrainPainter] ClearUrbanPaintInBounds — no terrains overlap " +
                    areaBoundsXZ + ".");
                return;
            }

            var wipedTiles = 0;
            var wipedBytes = 0L;
            var controlMs = 0L;

            for (var t = 0; t < terrains.Count; t++)
            {
                var terrain = terrains[t];
                if (!WorldTileInfoUtility.TryGetLiveTerrain(
                        new WorldTileInfo(default, default, terrain, WorldTileState.None),
                        out terrain))
                    continue;

                var cWatch = Stopwatch.StartNew();
                if (TryWipeAllControlsOnTerrain(terrain, areaBoundsXZ, firstUrbanLayer, out var bytes))
                {
                    wipedTiles++;
                    wipedBytes += bytes;
                }
                controlMs += cWatch.ElapsedMilliseconds;
            }

            Debug.Log(
                "[CityLotTerrainPainter] ClearUrbanPaintInBounds terrains=" + terrains.Count +
                " wipedTiles=" + wipedTiles + " urbanBytes=" + wipedBytes +
                " control=" + controlMs + "ms wall=" + watch.ElapsedMilliseconds +
                "ms bounds=" + areaBoundsXZ + ".");
        }

        private static bool TryHardWipeControlsOnTerrain(
            Terrain terrain, Rect areaBoundsXZ, out int wipedMaps)
        {
            wipedMaps = 0;
            if (!TryBuildTerrainClip(terrain, areaBoundsXZ, out var origin, out var size, out var clip))
                return false;

            var backend = WorldTileInfoUtility.FindSurfaceSyncBackend();
            var backendOwns = backend != null && backend.OwnsControlSync(terrain);

            for (var ci = 0; ci < 8; ci++)
            {
                if (!TryResolveControlTexture(terrain, backend, backendOwns, ci, out var tex))
                    continue;

                if (!TryHardWipeSingleControl(tex, clip, origin, size, isBaseControl: ci == 0))
                    continue;

                wipedMaps++;
                if (backendOwns)
                    backend.SetControlTexture(terrain, ci, tex);
            }

            if (wipedMaps <= 0)
                return false;

            if (backendOwns)
                backend.ApplyControls(terrain);
            ForceTerrainSplatRefresh(terrain);
            return true;
        }

        private static Color32[] _hardWipeBlock;

        private static bool TryHardWipeSingleControl(
            Texture2D tex, Rect clip, Vector3 origin, Vector3 size, bool isBaseControl)
        {
            if (!TryBuildSampleRegion(clip.xMin, clip.xMax, origin.x, size.x, tex.width, out var x0, out var width))
                return false;
            if (!TryBuildSampleRegion(clip.yMin, clip.yMax, origin.z, size.z, tex.height, out var z0, out var height))
                return false;

            var fill = isBaseControl
                ? new Color32(255, 0, 0, 0)
                : new Color32(0, 0, 0, 0);

            var count = width * height;
            var block = EnsureHardWipeBlock(count);
            for (var i = 0; i < count; i++)
                block[i] = fill;

            // Write-only dirty rect — no GetPixels / alphamap path.
            try
            {
                tex.SetPixels32(x0, z0, width, height, block);
            }
            catch (System.Exception)
            {
                return TryHardWipeRaw(tex, x0, z0, width, height, fill);
            }

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(false, false);
            return true;
        }

        private static Color32[] EnsureHardWipeBlock(int count)
        {
            // SetPixels32 requires colors.Length == width*height exactly.
            if (_hardWipeBlock == null || _hardWipeBlock.Length != count)
                _hardWipeBlock = new Color32[count];
            return _hardWipeBlock;
        }

        private static bool TryHardWipeRaw(
            Texture2D tex, int x0, int z0, int width, int height, Color32 fill)
        {
            Unity.Collections.NativeArray<Color32> raw;
            try
            {
                raw = tex.GetRawTextureData<Color32>();
            }
            catch (System.Exception)
            {
                return false;
            }

            if (!raw.IsCreated || raw.Length < tex.width * tex.height)
                return false;

            var tw = tex.width;
            for (var z = 0; z < height; z++)
            {
                var row = (z0 + z) * tw + x0;
                for (var x = 0; x < width; x++)
                    raw[row + x] = fill;
            }

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(false, false);
            return true;
        }

        private static bool TryWipeAllControlsOnTerrain(
            Terrain terrain, Rect areaBoundsXZ, int firstUrbanLayer, out long wipedBytes)
        {
            wipedBytes = 0;
            if (!TryBuildTerrainClip(terrain, areaBoundsXZ, out var origin, out var size, out var clip))
                return false;

            var backend = WorldTileInfoUtility.FindSurfaceSyncBackend();
            var backendOwns = backend != null && backend.OwnsControlSync(terrain);

            var any = false;
            // Wipe each control independently — resolutions often diverge
            // (_Control0–3 @ 513, _Control4 @ 2048 on City_Area tiles).
            // Continue past gaps so a missing mid control cannot skip _Control4.
            for (var ci = 0; ci < 8; ci++)
            {
                if (!TryResolveControlTexture(terrain, backend, backendOwns, ci, out var tex))
                    continue;

                if (!TryWipeSingleControl(
                        tex, clip, origin, size, ci * 4, firstUrbanLayer, out var bytes))
                    continue;

                wipedBytes += bytes;
                any = true;
                if (backendOwns)
                    backend.SetControlTexture(terrain, ci, tex);
                Debug.Log(
                    "[CityLotTerrainPainter] Wiped " + ControlTexPrefix + ci +
                    " res=" + tex.width + " bytes=" + bytes + " pos=" + origin);
            }

            if (!any)
                return false;

            if (backendOwns)
                backend.ApplyControls(terrain);
            ForceTerrainSplatRefresh(terrain);
            return true;
        }

        private static bool TryBuildTerrainClip(
            Terrain terrain, Rect areaBoundsXZ,
            out Vector3 origin, out Vector3 size, out Rect clip)
        {
            origin = default;
            size = default;
            clip = default;
            if (terrain == null || terrain.terrainData == null)
                return false;

            origin = terrain.transform.position;
            size = terrain.terrainData.size;
            var tileRect = Rect.MinMaxRect(origin.x, origin.z, origin.x + size.x, origin.z + size.z);
            clip = Rect.MinMaxRect(
                Mathf.Max(areaBoundsXZ.xMin, tileRect.xMin),
                Mathf.Max(areaBoundsXZ.yMin, tileRect.yMin),
                Mathf.Min(areaBoundsXZ.xMax, tileRect.xMax),
                Mathf.Min(areaBoundsXZ.yMax, tileRect.yMax));
            return clip.width > 0f && clip.height > 0f;
        }

        /// <summary>
        ///     Prefer vendor sync backend when it owns controls; otherwise read
        ///     <see cref="MicroSplatTerrain"/> customControl0–7 (hub / City Surfaces path).
        /// </summary>
        private static bool TryResolveControlTexture(
            Terrain terrain, IWorldSurfaceSyncBackend backend, bool backendOwns,
            int controlIndex, out Texture2D tex)
        {
            tex = null;
            if (backendOwns &&
                backend.TryGetControlTexture(terrain, controlIndex, out tex) &&
                tex != null && tex.width >= 2 && tex.height >= 2)
                return true;

            return TryGetMicroSplatCustomControl(terrain, controlIndex, out tex);
        }

        private static bool TryGetMicroSplatCustomControl(
            Terrain terrain, int controlIndex, out Texture2D tex)
        {
            tex = null;
            var mso = terrain != null ? terrain.GetComponent<MicroSplatTerrain>() : null;
            if (mso == null)
                return false;

            tex = controlIndex switch
            {
                0 => mso.customControl0,
                1 => mso.customControl1,
                2 => mso.customControl2,
                3 => mso.customControl3,
                4 => mso.customControl4,
                5 => mso.customControl5,
                6 => mso.customControl6,
                7 => mso.customControl7,
                _ => null
            };
            return tex != null && tex.width >= 2 && tex.height >= 2;
        }

        private static bool TryWipeSingleControl(
            Texture2D tex, Rect clip, Vector3 origin, Vector3 size,
            int layerBase, int firstUrbanLayer, out long wipedBytes)
        {
            wipedBytes = 0;
            if (!TryBuildSampleRegion(clip.xMin, clip.xMax, origin.x, size.x, tex.width, out var x0, out var width))
                return false;
            if (!TryBuildSampleRegion(clip.yMin, clip.yMax, origin.z, size.z, tex.height, out var z0, out var height))
                return false;

            if (!TryFindUrbanDirtyOnTexture(
                    tex, layerBase, firstUrbanLayer, x0, z0, width, height,
                    out var dx, out var dz, out var dw, out var dh))
                return false;

            Unity.Collections.NativeArray<Color32> raw;
            try
            {
                raw = tex.GetRawTextureData<Color32>();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    "[CityLotTerrainPainter] Raw wipe failed on '" + tex.name + "': " + ex.Message);
                return false;
            }

            if (!raw.IsCreated || raw.Length < tex.width * tex.height)
                return false;

            var tw = tex.width;
            for (var z = 0; z < dh; z++)
            {
                var row = (dz + z) * tw + dx;
                for (var x = 0; x < dw; x++)
                {
                    var idx = row + x;
                    var c = raw[idx];
                    var urban = 0;
                    urban += TakeUrbanChannel(ref c.r, layerBase + 0, firstUrbanLayer);
                    urban += TakeUrbanChannel(ref c.g, layerBase + 1, firstUrbanLayer);
                    urban += TakeUrbanChannel(ref c.b, layerBase + 2, firstUrbanLayer);
                    urban += TakeUrbanChannel(ref c.a, layerBase + 3, firstUrbanLayer);
                    if (urban <= 0)
                        continue;

                    // Fold into this control's lowest non-urban channel when possible
                    // (same texture). Cross-control fold skipped (resolutions differ).
                    if (layerBase == 0)
                        c.r = (byte)Mathf.Min(255, c.r + urban);
                    else if (layerBase > 0 && layerBase < firstUrbanLayer)
                    {
                        // keep zeros; base grass lives on _Control0
                    }

                    raw[idx] = c;
                    wipedBytes += urban;
                }
            }

            if (wipedBytes <= 0)
                return false;

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(false, false);
            return true;
        }

        private static bool TryFindUrbanDirtyOnTexture(
            Texture2D tex, int layerBase, int firstUrbanLayer,
            int x0, int z0, int width, int height,
            out int dx, out int dz, out int dw, out int dh)
        {
            dx = dz = dw = dh = 0;
            Unity.Collections.NativeArray<Color32> raw;
            try
            {
                raw = tex.GetRawTextureData<Color32>();
            }
            catch (System.Exception)
            {
                return false;
            }

            if (!raw.IsCreated || raw.Length < tex.width * tex.height)
                return false;

            var minX = x0 + width;
            var minZ = z0 + height;
            var maxX = x0 - 1;
            var maxZ = z0 - 1;
            var found = false;
            var tw = tex.width;
            var step = UrbanProbeStep;

            for (var z = 0; z < height; z += step)
            {
                var row = (z0 + z) * tw;
                for (var x = 0; x < width; x += step)
                {
                    var c = raw[row + x0 + x];
                    if (!ControlPixelHasUrban(c, layerBase, firstUrbanLayer))
                        continue;
                    found = true;
                    var ax = x0 + x;
                    var az = z0 + z;
                    minX = Mathf.Min(minX, ax - step);
                    minZ = Mathf.Min(minZ, az - step);
                    maxX = Mathf.Max(maxX, ax + step);
                    maxZ = Mathf.Max(maxZ, az + step);
                }
            }

            if (!found)
                return false;

            minX = Mathf.Clamp(minX, x0, x0 + width - 1);
            minZ = Mathf.Clamp(minZ, z0, z0 + height - 1);
            maxX = Mathf.Clamp(maxX, x0, x0 + width - 1);
            maxZ = Mathf.Clamp(maxZ, z0, z0 + height - 1);
            dx = minX;
            dz = minZ;
            dw = maxX - minX + 1;
            dh = maxZ - minZ + 1;
            return dw > 0 && dh > 0;
        }

        private static void ForceTerrainSplatRefresh(Terrain terrain)
        {
            if (terrain == null)
                return;
            var wasEnabled = terrain.enabled;
            terrain.enabled = false;
#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
            terrain.enabled = wasEnabled;
        }

        private static bool ControlPixelHasUrban(Color32 c, int layerBase, int firstUrbanLayer)
        {
            // Entire control is urban when its base layer index is already urban
            // (_Control4 = layers 16+).
            if (layerBase >= firstUrbanLayer)
            {
                return c.r > UrbanChannelThreshold || c.g > UrbanChannelThreshold
                    || c.b > UrbanChannelThreshold || c.a > UrbanChannelThreshold;
            }

            if (layerBase + 0 >= firstUrbanLayer && c.r > UrbanChannelThreshold) return true;
            if (layerBase + 1 >= firstUrbanLayer && c.g > UrbanChannelThreshold) return true;
            if (layerBase + 2 >= firstUrbanLayer && c.b > UrbanChannelThreshold) return true;
            if (layerBase + 3 >= firstUrbanLayer && c.a > UrbanChannelThreshold) return true;
            return false;
        }

        private static int TakeUrbanChannel(ref byte channel, int layer, int firstUrbanLayer)
        {
            if (layer < firstUrbanLayer || channel == 0)
                return 0;
            var v = channel;
            channel = 0;
            return v;
        }
    }
}
