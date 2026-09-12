# City pipeline Reset — terrain paint clear

**Date:** 2026-08-25  
**Status:** Working (hard wipe + MapMagic `Refresh(clearAll)`)

## Goal

**Reset (Clear All + Terrain)** must:

1. Remove all city splat paint (lot concrete, City Surfaces frame, etc.)
2. Leave wilderness-looking MapMagic terrain
3. Stay fast (no multi‑tens‑of‑seconds alphamap stamping)

## Symptoms when broken

| Symptom | Typical cause |
|--------|----------------|
| Concrete / gray rectangular frame remains after Reset | High-res `_Control4` (or similar) not wiped; MapMagic products still “ready” |
| Reset ~20–25s with `Flush … stamp=… sync=…` | Alphamap `ClearDistrictAreaPaint` path running |
| `ClearUrbanPaintInBounds … wipedTiles=0` | Urban probe / `break` on missing control / wrong layer gate |
| `MapMagic StartGenerate kicked` + `mapMagicWait≈50ms` | Generate without clearing TileData — no-op |

## Why older approaches failed

### Alphamap hard-erase (`ClearDistrictAreaPaint`)

- Reads/stamps/writes alphamaps on every overlapping tile, then syncs to MicroSplat.
- Cost: ~22s for ~12 tiles (`get` + `stamp` + `set` + `sync`).
- MapMagic City Area tiles often keep **mismatched control resolutions**:
  - `_Control0–3` @ **513** (alphamap sync path)
  - `_Control4` @ **2048** (City Surfaces / real visible paint)
- Alphamap sync only rewrites controls matching alphamap size → **leaves `_Control4` concrete**.

### Selective “urban layer” wipe (`ClearUrbanPaintInBounds`)

- Only clears layers ≥ 14 (City Surfaces channels).
- Lot concrete can sit on **lower** MicroSplat layers → missed.
- Loop used `break` on a null mid-control → could skip `_Control4`.
- `GetRawTextureData` often fails on MapMagic maps → silent `wipedTiles=0`.

### `StartGenerate(tile)` alone

```csharp
mapMagic.StartGenerate(tile, true, false);
```

- Does **not** call `ClearChanged` / clear TileData.
- Graph products stay **ready** → generate finishes in ~50ms without rewriting splat outputs.
- Manual inspector **Generate Changed** looked different because it goes through `Refresh`, which clears change then regenerates.

`MapMagicObject.StartGenerate(TerrainTile, …)` also only runs when `instantGenerate` is true; the reliable per-tile API is:

```csharp
tile.Refresh(mapMagic.graph, clearAll: true);
```

## Working Reset path

**Owner files:**

- `Assets/01_Game/02_World/CityPipeline/Roads/Scripts/CityPrefabRoadNetworkBuilder.Terrain.cs`
- `Assets/01_Game/02_World/CityPipeline/City/Scripts/CityLotTerrainPainter.ControlWipe.cs`
- `Assets/Editor/CityPipeline/CityPipelineRunnerWindow.Steps.cs` (`ResetRoutine`)

**Sequence:**

1. `CitySurfaceLayoutStore.Clear()`  
   So the next City Surfaces generate does not invent concrete from a stale layout.
2. Resolve / expand city paint bounds (same as before).
3. `CityLotTerrainPainter.HardWipeControlsInBounds(bounds)`  
   - Dirty-rect write-only on every `_Control0…7` present (`continue` past gaps, never `break`).  
   - `_Control0` ← `(255,0,0,0)` (full layer 0).  
   - Other controls ← `(0,0,0,0)`.  
   - Prefer `SetPixels32(x,y,w,h,…)` + `Apply`; raw path as fallback.  
   - **Do not** run alphamap `ClearDistrictAreaPaint` on Reset.
4. For each overlapping pinned tile:  
   `tile.Refresh(mapMagic.graph, clearAll: true)`  
   - Clears TileData products, then regenerates wilderness from the graph.  
   - Fallback: `mapMagic.Refresh(clearAll: true)` if no tile overlaps.
5. Hub waits until `IsGenerating` finishes (with a short grace if enqueue is delayed).
6. `ClearAllGeneratedCityContent()` (meshes/roads/lots — no per-object Undo).

## Console checklist (healthy)

```
[CityLotTerrainPainter] HardWipeControlsInBounds terrains=N wipedTiles=N maps=… wall=…ms
[CityPrefabRoadNetworkBuilder] MapMagic Refresh(clearAll) kicked on N tile(s).
[CityPipelineRunnerWindow] Reset step: paintClear=…ms mapMagicWait=…ms clearContent=…ms
```

- `paintClear` should be **well under a few seconds** (wipe only).
- `mapMagicWait` covers the real wilderness rebuild (can be longer; that is expected).
- **Do not** expect `Flush … stamp=…` from Reset — that means the slow alphamap path is back.
- **Do not** expect `StartGenerate kicked` + ~50ms wait — that means clearAll refresh was lost.

## Related pipeline rules (don’t reintroduce)

- **District Lots / Buildings** must not paint industrial concrete; **Lot Terrain** owns lot splat paint.
- City Surfaces with an empty `CitySurfaceLayoutStore` must emit an **empty** layout (no invented concrete frame).
- Prefer scene-visible ownership; Reset paint logic stays on `CityPrefabRoadNetworkBuilder` + `CityLotTerrainPainter`.

## Lot Terrain clip (related)

Painting **inside** named areas / district lots with full concrete is correct.
Spill **outside the city** is not. Lot Terrain clips to the union of hub-shifted
named-area bounds and syncs dirty rects into existing MicroSplat controls
without blanking wilderness. See `docs/fixes/lot-pipeline-speed.md`.
