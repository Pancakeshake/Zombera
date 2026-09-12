# AI Plan: Finite Map Size Selection (Game Start)

## 1) Goal
Replace the unbounded MapMagic world with a **finite, selectable map size** chosen at game start, so sessions are predictable, budgetable and replayable.

Target outcome:
- New Game flow offers a map size choice (Small / Medium / Large) plus a world seed.
- The world is generated inside a fixed square of MapMagic tiles instead of streaming forever.
- Map edges are a natural boundary (ocean / impassable mountains), not invisible walls.
- City density, POI scatter and zombie spawn zones scale with the chosen size.
- Existing streaming systems (tile pinning, per-tile NavMesh) keep working inside the bounded rect.

## 2) Proposed Size Tiers

| Option | Tiles | Area | Suggested city sites |
|--------|-------|------|----------------------|
| Small  | 4 x 4  | 16 km²  | 2-4   |
| Medium | 8 x 8  | 64 km²  | 8-14  |
| Large  | 10 x 10 | 100 km² | 12-24 |

- Tile size is fixed at **1000 m x 1000 m** (`MapMagicObject.tileSize`).
- Default tier: **Medium (8 x 8)**. 10 x 10 is the Large upper bound, not the default.
- Context for scale: 100 km² sits between Project Zomboid (~92 km²) and DayZ (225 km²). Walking across a 10 km edge takes ~40 minutes. Density, not area, drives how the map feels.

## 3) What Already Exists That We Can Reuse

### World / streaming
- `WorldManager` coordinates MapMagic tile streaming, chunk budget and world-session lifecycle.
- MapMagic tiles are generated on demand by the `MapMagicObject` (`tileSize` 1000 x 1000) — a finite map just pins a bounded rect once and stops expanding.
- `StreamingNavMeshTileService` already builds/removes NavMesh per tile — unchanged inside a bounded rect.

### Region / cities
- `CityRegionAsset` now has a seeded **Site Scatter** (`RandomizeSites()`): `scatterCenterXZ`, `scatterHalfExtentMeters`, `minSiteClearanceMeters`, `scatterSeed`.
  - Map size can map directly onto scatter extent + site count (e.g. Medium = 2000 m half-extent / 10 sites).
- `CityPrefabRoadNetworkBuilder` region mode generates every city + MST highways in one deterministic pass.

### Game start / menu
- `GameManager.Instance.CurrentState` already gates world spawning (`LoadingWorld` / `Playing` / `Paused`; `MainMenu` blocked).
- MainMenu flow is the natural place for the New Game options screen.

## 4) Open Design Decisions

1. **Boundary style** — ocean via MapMagic coastline node, or steep mountain ring via height node at the edge? (Prefer ocean: clearer player expectation, reuse existing water stack.)
2. **City count per tier** — derive from area (e.g. one city per ~8 km²) or expose as an "city density" option? (Recommend derived, with an Advanced toggle later.)
3. **Edge clearance** — scatter must keep city centers at least one footprint radius inside the boundary so rings/highways never cross the map edge.
4. **Seed flow** — size + seed feed: region seed, MapMagic seed, scatter seed, and (later) POI/zombie scatters.
5. **Save compatibility** — saves must record map size + seed and reject/repair saves from other-sized worlds.

## 5) Suggested Implementation Order

1. `WorldMapSizeSettings` ScriptableObject / runtime config: tier enum, tiles-per-side, city count target, seed.
2. New Game UI in MainMenu: tier cards + seed field; persist into `GameManager` before `LoadingWorld`.
3. Bound the streaming rect: pass tile count to MapMagic pinning; stop pinning beyond the rect.
4. Feed size into `CityRegionAsset` scatter (extent + site count + edge clearance).
5. Boundary terrain (ocean ring) + safe-edge clamps in scatter/highway generation.
6. Save/load stores map size + seed; mismatch handling.
7. Perf budgets per tier: tile count, NavMesh coverage, zombie cap, POI counts.

## 6) Related Plans / Links

- Region pipeline (cities + highways): `CityPrefabRoadNetworkBuilder.Region.cs`, `CityMathRoadLayoutGenerator.RegionHighways.cs`
- Scatter system: `CityRegionAsset.RandomizeSites()`
- Streaming conventions: `AGENTS.md` section 4 (MapMagic events, NavMesh streaming, spawn safety)
