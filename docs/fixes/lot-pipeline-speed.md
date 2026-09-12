# District Lots / Buildings / Lot Terrain speed

**Date:** 2026-08-24

## Changes

### Lot Terrain (largest win)
- **Dirty-rect alphamap I/O** — `GetAlphamaps` / `SetAlphamaps` only the union of painted zones (city usually covers a fraction of each 1 km tile).
- **Dirty-rect MicroSplat control writes** — `SetPixels32` on the same sub-rect; skip `MicroSplatTerrain.Sync()` when MapMagic `MaterialPropertySerializer` is present (full Sync re-read was expensive).
- **Removed sharp-mask allocation** — soft then absolute-sharp stamps; no full-tile `bool[,]`.
- **Removed alphamap bump to 2048** for parking stripes (dominated flush time).

### District Lots
- **No per-lot Undo** on fill visuals / destroy-recreate (bulk regenerate path).
- **No terrain paint** in this step — industrial concrete / lot surfaces belong to
  **Lot Terrain** only (paint-during-lots was leaving concrete before Lot Terrain ran).

### Buildings
- Rejected candidates use `DestroyImmediate` (no Undo tax).
- Undo registered only on successful place.
- Reuse place-time footprint for the street-facing marker (no second renderer walk).
- Reuse a static door-socket buffer.

## How to verify

Re-run **District Lots → Buildings → Lot Terrain** and compare console:

- `[CityPrefabRoadNetworkBuilder] District lots=… in Xms`
- `[CityPrefabResidentialLotBuilder] Residential lots: … subdivide=… populate=…`
- `[CityPrefabRoadNetworkBuilder] Placed buildings=… in Xms`
- `[CityLotTerrainPainter] Flush N tile(s) in Xms — get=… stamp=… set=… sync=…`
- `[CityPrefabRoadNetworkBuilder] District lot terrain — … in Xms`

Flush `get`/`set`/`sync` should drop sharply vs full-tile 1024/2048 runs.

## Lot Terrain — named-area clip (2026-08-25)

**Intent:** Full concrete/asphalt fills **inside** named areas / district lots
are correct. Paint must **not** spill onto wilderness outside the city.

**Do not** skip whole-district fills for Commercial/Industrial/CityCore — those
presets intentionally use concrete layers 13–14 inside named areas.

**Clip:** `CollectNamedAreaPaintBounds` (union of hub-shifted named areas), not
an oversized flatten ring. Log: `Lot Terrain clip bounds=…`

**Sync:** Write dirty rects into existing MapMagic `_Control*` maps (scale when
resolutions differ). Never replace a mismatched control with a blank
alphamap-sized texture (that zeros wilderness outside the stamp).

**Verify:** Concrete fills districts/lots inside the city; grass/wilderness
outside named areas.


Paint can live in alphamaps, `_Control0–3` @ 513, and `_Control4` @ 2048
(mismatched sizes). Alphamap hard-erase was ~22s and never cleared `_Control4`.
`StartGenerate` without clearing TileData left products "ready" (~50ms no-op).

Reset now:
1. `CitySurfaceLayoutStore.Clear()`
2. **Hard wipe** every MicroSplat `_Control*` dirty-rect in city bounds
   (layer 0 full weight / zeros elsewhere — no alphamap stamp)
3. Overlapping tiles: `tile.Refresh(graph, clearAll:true)` so MapMagic rebuilds
   wilderness from the graph (not a cached City Surfaces product)
4. Wait for `IsGenerating`, then clear city meshes (no per-object Undo)

Console: `HardWipeControlsInBounds … wipedTiles=…`,
`MapMagic Refresh(clearAll) kicked on N tile(s)`,
`Reset step: paintClear=… mapMagicWait=…`

`paintClear` should be well under a second for the wipe; `mapMagicWait` covers
the real wilderness rebuild.
