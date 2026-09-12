using System.Collections.Generic;
using JBooth.MicroSplat;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.Roads;

namespace Zombera.World.City
{
    /// <summary>
    ///     Paints terrain layers under city lots using Unity's standard alphamap
    ///     API. Paints are batched per terrain and MicroSplat control textures are
    ///     rebuilt from each painted tile's alphamaps — shared textures are never written.
    /// </summary>
    /// <remarks>
    ///     Texture source of truth: layer indices come from the
    ///     <c>TextureArrayConfig</c> referenced by <c>DistrictLotTerrainConfig</c>
    ///     (currently <c>Microsplat_World</c>), but the PIXELS the terrain actually
    ///     samples come from the texture arrays referenced by the terrain template
    ///     material <c>Assets/01_Game/02_World/Terrain/Materials/Microsplat/MicroSplat.mat</c>
    ///     (<c>_Diffuse</c> / <c>_NormalSAO</c> / <c>_AntiTileArray</c>).
    ///     To change a terrain texture: edit the config, click MicroSplat's
    ///     "Update Splat Maps", and keep MicroSplat.mat pointed at the
    ///     <c>Microsplat_World_*_tarray</c> assets. Re-baking without keeping the
    ///     mat in sync silently renders stale pixels.
    ///     IMPORTANT: do NOT edit the baked texture-array assets or the config
    ///     asset fields directly — arrays are baked products. Texture changes
    ///     must be made from within the MicroSplat material/config UI
    ///     (MicroSplat editor → "Update Splat Maps").
    /// </remarks>
    public static partial class CityLotTerrainPainter
    {
        /// <summary>
        ///     Paints lot sub-zones into terrain alphamaps. Terrain targets are
        ///     resolved per sub-zone — resolving from the clip rect would break
        ///     region mode, where the clip rect spans every city and lands paint
        ///     on whichever tile overlaps it most instead of the tile under the lot.
        /// </summary>
        public static void PaintLotSubZones(
            List<LotSubZone> subZones,
            TextureArrayConfig texArrCfg = null,
            float blendMeters = 1.0f,
            Rect clipRect = default)
        {
            if (subZones == null || subZones.Count == 0) return;

            // Resolve the paintable terrain set ONCE for the whole call. The old
            // code re-scanned active/pinned terrains for every sub-zone (a
            // FindFirstObjectByType + pinned-tile walk each), which dominated
            // the Lot Terrain step's time.
            var terrainsToPaint = ResolvePaintableTerrains();
            if (terrainsToPaint.Count == 0) return;

#if UNITY_EDITOR
            RecordTerrainDataUndo(terrainsToPaint, "Paint Lot Sub-Zones");
#endif

            // MicroSplat Textures mode leaves terrainLayers empty — populate them.
            EnsureMissingTerrainLayers(terrainsToPaint, texArrCfg);

            for (var s = 0; s < subZones.Count; s++)
            {
                var zone = subZones[s];
                var surfaceLayer = zone.TextureLayerIndex >= 0
                    ? zone.TextureLayerIndex
                    : ResolveSurfaceLayer(LotSurfaceType.Grass);
                if (surfaceLayer < 0) continue;

                var zoneRect = ClipToBounds(zone.Bounds, clipRect);
                if (zoneRect.width <= 0f || zoneRect.height <= 0f) continue;

                // Driveways, building slabs and door paths get crisp binary edges
                // so the asphalt / concrete / paver ends exactly at the zone
                // bounds; yards and area fills keep the soft blended falloff.
                // Prefer LotSubZone.Sharp; DisplayName is a fallback for call sites
                // that still construct zones without the flag.
                var sharp = zone.Sharp
                    || zone.DisplayName is "Driveway" or "BuildingPad" or "Footpath"
                        or "CommercialWalkway" or "CommercialParking" or "ParkingStripe";
                var zoneBlend = sharp ? 0f : blendMeters;

                EnqueueZonePaint(zoneRect, surfaceLayer, zoneBlend, terrainsToPaint, sharp: sharp);
            }
        }

        /// <summary>
        ///     The terrains this painter can write to: pinned MapMagic tiles when
        ///     available, otherwise every active terrain. Resolved once per paint
        ///     pass — per-zone resolution was the hot path in the Lot Terrain step.
        /// </summary>
        private static List<Terrain> ResolvePaintableTerrains()
        {
            if (_cachedPaintableTerrains != null)
            {
                PruneDestroyedTerrains(_cachedPaintableTerrains);
                if (_cachedPaintableTerrains.Count > 0)
                    return _cachedPaintableTerrains;
                _cachedPaintableTerrains = null;
            }

            if (WorldTileInfoUtility.TryGetPinnedTerrains(out var pinned) && pinned.Count > 0)
            {
                PruneDestroyedTerrains(pinned);
                if (pinned.Count > 0)
                {
                    _cachedPaintableTerrains = pinned;
                    return _cachedPaintableTerrains;
                }
            }

            var actives = Terrain.activeTerrains;
            var list = new List<Terrain>(actives.Length);
            for (var i = 0; i < actives.Length; i++)
            {
                if (TryGetLiveTerrain(actives[i], out var terrain))
                    list.Add(terrain);
            }

            _cachedPaintableTerrains = list;
            return _cachedPaintableTerrains;
        }

        private static void PruneDestroyedTerrains(List<Terrain> terrains)
        {
            for (var i = terrains.Count - 1; i >= 0; i--)
            {
                if (!TryGetLiveTerrain(terrains[i], out _))
                    terrains.RemoveAt(i);
            }
        }

        private static bool TryGetLiveTerrain(Terrain terrain, out Terrain live)
        {
            live = terrain;
            if (live == null)
                return false;

            try
            {
                if (live.terrainData == null)
                {
                    live = null;
                    return false;
                }
            }
            catch (MissingReferenceException)
            {
                live = null;
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Resolved once per paint batch. Invalidated on flush so the next
        ///     pipeline step (and any MapMagic re-pin) sees fresh terrain state.
        /// </summary>
        private static List<Terrain> _cachedPaintableTerrains;

        private static void EnsureMissingTerrainLayers(
            List<Terrain> terrains, TextureArrayConfig texArrCfg)
        {
            if (texArrCfg == null) return;
            for (var t = 0; t < terrains.Count; t++)
            {
                // Always run EnsureTerrainLayers — it no-ops when the terrain
                // already has a complete layer set matching the config count.
                // Previously only empty terrains were repaired, so slot 16 (white
                // paint) never appeared on MapMagic tiles that shipped with 16 layers.
                if (terrains[t]?.terrainData != null)
                    EnsureTerrainLayers(terrains[t], texArrCfg);
            }
        }

#if UNITY_EDITOR
        private static void RecordTerrainDataUndo(List<Terrain> terrains, string undoName)
        {
            for (var t = 0; t < terrains.Count; t++)
            {
                if (terrains[t]?.terrainData != null)
                    UnityEditor.Undo.RecordObject(terrains[t].terrainData, undoName);
            }
        }
#endif

        /// <summary>
        ///     Paints the ground terrain under a district fill area.
        ///     Called from <c>GenerateDistrictLotTerrain</c> (step 4).
        ///     Ensures terrain layers exist from the MicroSplat config before painting.
        /// </summary>
        public static void PaintDistrictArea(
            Rect areaBoundsXZ,
            TextureArrayConfig texArrCfg,
            int textureLayerIndex,
            float blendMeters = 1.5f,
            Rect clipRect = default,
            bool sharp = false)
        {
            if (texArrCfg == null || textureLayerIndex < 0) return;
            if (textureLayerIndex >= texArrCfg.sourceTextures.Count) return;

            areaBoundsXZ = ClipToBounds(areaBoundsXZ, clipRect);
            if (areaBoundsXZ.width <= 0f || areaBoundsXZ.height <= 0f)
                return;

            var terrains = ResolveAllOverlappingTerrains(areaBoundsXZ);
            if (terrains.Count == 0)
            {
                Debug.LogWarning("[CityLotTerrainPainter] No active terrains — terrain painting skipped.");
                return;
            }

            for (var t = 0; t < terrains.Count; t++)
            {
                var terrain = terrains[t];
                if (terrain == null || terrain.terrainData == null) continue;

                // MicroSplat Textures mode leaves terrainLayers empty.
                // Populate them from the TextureArrayConfig so SetAlphamaps works.
                var layersBefore = terrain.terrainData.alphamapLayers;
                if (layersBefore <= 0)
                {
                    EnsureTerrainLayers(terrain, texArrCfg);
                    Debug.Log($"[CityLotTerrainPainter] Restored {terrain.terrainData.alphamapLayers} terrain layers on '{terrain.name}'.");
                }
            }

            var blend = sharp ? 0f : blendMeters;
            EnqueueZonePaint(areaBoundsXZ, textureLayerIndex, blend, terrains, sharp: sharp);
        }

        /// <summary>
        ///     Resets the terrain alphamaps under a district area back to grass.
        ///     Called when clearing city generation so old paint doesn't linger
        ///     between MapMagic regeneration cycles.
        /// </summary>
        /// <param name="eraseResidualUrban">
        ///     When true, also sweeps active terrains for leftover urban layers
        ///     outside <paramref name="areaBoundsXZ"/>. Expensive on large worlds;
        ///     hub Reset should pass false (bounds hard-erase is enough).
        /// </param>
        public static void ClearDistrictAreaPaint(Rect areaBoundsXZ,
            TextureArrayConfig texArrCfg = null, float blendMeters = 2.0f,
            bool eraseResidualUrban = true)
        {
            if (areaBoundsXZ.width <= 0f || areaBoundsXZ.height <= 0f)
            {
                Debug.LogWarning("[CityLotTerrainPainter] ClearDistrictAreaPaint skipped — invalid bounds.");
                return;
            }

            var totalWatch = System.Diagnostics.Stopwatch.StartNew();

            // Region resets span many MapMagic tiles — clear every overlapping
            // terrain, not just the one containing the bounds centre.
            var terrains = ResolveAllOverlappingTerrains(areaBoundsXZ);
            if (terrains.Count == 0)
            {
                Debug.LogWarning($"[CityLotTerrainPainter] ClearDistrictAreaPaint — no terrains overlap {areaBoundsXZ}.");
                return;
            }

            // MicroSplat Textures mode leaves terrainLayers empty — populate
            // them so SetAlphamaps works (same as PaintDistrictArea).
            if (texArrCfg != null)
            {
                for (var t = 0; t < terrains.Count; t++)
                {
                    var td = terrains[t]?.terrainData;
                    if (td != null && td.alphamapLayers <= 0)
                        EnsureTerrainLayers(terrains[t], texArrCfg);
                }
            }

            // Layer 0 is always the base/dominant terrain texture in any
            // MicroSplat TextureArrayConfig — no name lookup needed.
            var grassLayer = 0;

            EnqueueZonePaint(areaBoundsXZ, grassLayer, blendMeters, terrains, hardErase: true);
            FlushPendingZonePaints();
            var clearMs = totalWatch.ElapsedMilliseconds;

            if (!eraseResidualUrban)
            {
                Debug.Log(
                    "[CityLotTerrainPainter] ClearDistrictAreaPaint bounds clear=" + clearMs +
                    "ms (residual skipped) terrains=" + terrains.Count + ".");
                return;
            }

            // MapMagic runs in MicroSplat Textures mode and never touches
            // alphamaps, so paint from older city layouts outside this reset
            // footprint survives forever and gets resurrected whenever a tile's
            // control textures are repacked from its alphamaps. Sweep active
            // terrains for leftover urban paint — dirty-rect only.
            totalWatch.Restart();
            var residualMs = EraseResidualUrbanPaint();
            Debug.Log(
                "[CityLotTerrainPainter] ClearDistrictAreaPaint bounds clear=" + clearMs +
                "ms residual=" + residualMs + "ms (erase work " + totalWatch.ElapsedMilliseconds +
                "ms) terrains=" + terrains.Count + ".");
        }

        /// <summary>
        ///     Paints an industrial lot's full bounds with the concrete layer.
        ///     Enqueues only — callers flush once after batching all lots so the
        ///     step doesn't pay a full alphamap read/write per lot.
        /// </summary>
        public static void PaintIndustrialLot(Rect lotBoundsXZ, float blendMeters = 1.5f)
        {
            var terrains = ResolveAllOverlappingTerrains(lotBoundsXZ);
            if (terrains.Count == 0) return;

            var layer = ResolveConcreteLayerIndex(terrains[0].terrainData);
            if (layer < 0) return;

            EnqueueZonePaint(lotBoundsXZ, layer, blendMeters, terrains);
        }

        // ── Paint the ground (batched: one alphamap read/write per terrain) ──

        private sealed class PendingZonePaint
        {
            public Rect Rect;
            public int Layer;
            public float Blend;

            /// <summary>
            ///     True for clear operations: non-target layers are erased
            ///     completely instead of the usual 0.85 damped fade, so a reset
            ///     leaves no residual ghost of the old paint.
            /// </summary>
            public bool HardErase;

            /// <summary>
            ///     True for crisp-edge paints (driveways, slabs): full replace
            ///     inside the zone and a binary boundary instead of a soft falloff.
            /// </summary>
            public bool Sharp;
        }

        private static readonly Dictionary<Terrain, List<PendingZonePaint>> PendingZonePaints = new();

        private static void EnqueueZonePaint(
            Rect zoneRect, int layer, float blendMeters,
            IReadOnlyList<Terrain> terrains, bool hardErase = false, bool sharp = false)
        {
            if (zoneRect.width <= 0f || zoneRect.height <= 0f || layer < 0 || terrains == null) return;

            // Every overlapping terrain receives the zone — paint used to stop at
            // tile borders and leave hard-edged lines beside lots. The terrain set
            // is resolved once per paint pass; only this rect check is per zone.
            for (var t = 0; t < terrains.Count; t++)
            {
                var terrain = terrains[t];
                if (terrain == null || terrain.terrainData == null) continue;
                if (!GetTerrainRect(terrain).Overlaps(zoneRect)) continue;

                if (!PendingZonePaints.TryGetValue(terrain, out var list))
                {
                    list = new List<PendingZonePaint>(16);
                    PendingZonePaints[terrain] = list;
#if UNITY_EDITOR
                    UnityEditor.Undo.RecordObject(terrain.terrainData, "Paint Lot Terrain");
#endif
                }

                list.Add(new PendingZonePaint
                {
                    Rect = zoneRect,
                    Layer = layer,
                    Blend = blendMeters,
                    HardErase = hardErase,
                    Sharp = sharp
                });
            }
        }

        /// <summary>
        ///     Applies every queued zone per terrain with a single GetAlphamaps /
        ///     SetAlphamaps pair, then rebuilds MicroSplat control textures once.
        ///     The old per-zone read/write did hundreds of native calls and
        ///     repainted overlapping pixels repeatedly (~27 s per step).
        /// </summary>
        public static void FlushPendingZonePaints()
        {
            if (PendingZonePaints.Count == 0)
            {
                _cachedPaintableTerrains = null;
                return;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var getMs = 0L;
            var stampMs = 0L;
            var setMs = 0L;
            var syncMs = 0L;
            var tileCount = 0;

            foreach (var kvp in PendingZonePaints)
            {
                if (kvp.Key == null || kvp.Key.terrainData == null) continue;
                tileCount++;
                FlushTerrainZonePaints(kvp.Key, kvp.Value, ref getMs, ref stampMs, ref setMs, ref syncMs);
            }

            PendingZonePaints.Clear();

            // Invalidate cached terrains — MapMagic may re-pin between pipeline steps.
            _cachedPaintableTerrains = null;

            Debug.Log(
                "[CityLotTerrainPainter] Flush " + tileCount + " tile(s) in " + watch.ElapsedMilliseconds +
                "ms — get=" + getMs + "ms stamp=" + stampMs + "ms set=" + setMs + "ms sync=" + syncMs + "ms.");
        }

        // ── Shared helpers ──────────────────────────────────────────

        /// <summary>
        ///     Clamps a paint zone to the given city bounds. An invalid (default)
        ///     clip rect leaves the zone untouched.
        /// </summary>
        private static Rect ClipToBounds(Rect zone, Rect clip)
        {
            if (clip.width <= 0f || clip.height <= 0f)
                return zone;

            return Rect.MinMaxRect(
                Mathf.Max(zone.xMin, clip.xMin),
                Mathf.Max(zone.yMin, clip.yMin),
                Mathf.Min(zone.xMax, clip.xMax),
                Mathf.Min(zone.yMax, clip.yMax));
        }

        /// <summary>
        ///     Returns every terrain whose rect overlaps the given area. Paint must
        ///     cover all overlapping tiles so zones spanning tile borders don't get
        ///     cut off.
        /// </summary>
        private static List<Terrain> ResolveAllOverlappingTerrains(Rect areaRect)
        {
            var result = new List<Terrain>();
            var terrains = ResolvePaintableTerrains();
            for (var t = 0; t < terrains.Count; t++)
            {
                var terrain = terrains[t];
                if (areaRect.width <= 0f || areaRect.height <= 0f || GetTerrainRect(terrain).Overlaps(areaRect))
                    result.Add(terrain);
            }

            return result;
        }

        private static Rect GetTerrainRect(Terrain terrain)
        {
            if (!TryGetLiveTerrain(terrain, out terrain))
                return default;

            var origin = terrain.transform.position;
            var size = terrain.terrainData.size;
            return Rect.MinMaxRect(origin.x, origin.z, origin.x + size.x, origin.z + size.z);
        }
    }
}
