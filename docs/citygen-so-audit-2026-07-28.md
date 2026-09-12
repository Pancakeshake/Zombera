# CityGen ScriptableObject Audit — CityPrefabRoadNetworkBuilder

**Date:** 2026-07-28
**Scope:** All ScriptableObject classes and `.asset` files related to CityGen, with focus on the CityPrefabRoadNetworkBuilder.

---

## 1. Inventory

### 1.1 ScriptableObject Classes (C#)

| # | Class | Namespace | File | Menu Path |
|---|-------|-----------|------|-----------|
| 1 | `CityBuildConfig` | `Zombera.World.Roads` | `Roads/Scripts/CityBuildConfig.cs` | `Zombera/City/Build Config` |
| 2 | `CityRoadMeshConfig` | `Zombera.World.Roads` | `Roads/Scripts/CityRoadMeshConfig.cs` | `Zombera/World/New SOs/City Road Mesh Config` |
| 3 | `CityTerrainFlattenConfig` | `Zombera.World.Roads` | `Roads/Scripts/CityTerrainFlattenConfig.cs` | `Zombera/World/New SOs/City Terrain Flatten Config` |
| 4 | `CityPrefabStreetscapeSettings` | `Zombera.World.Roads` | `Roads/Scripts/CityPrefabStreetscapeSettings.cs` | `Zombera/World/City Prefab Streetscape Settings` |
| 5 | `CityMathRoadLayoutAsset` | `Zombera.World.Roads` | `Roads/Scripts/CityMathRoadLayoutAsset.cs` | `Zombera/City/Layout` |
| 6 | `CityPrefabCompilationTest` | `Zombera.World.Roads` | `Roads/Scripts/CityPrefabCompilationTest.cs` | *(none)* |
| 7 | `CityBuildingPlacementConfig` | `Zombera.World.City` | `City/Scripts/CityBuildingPlacementConfig.cs` | `Zombera/World/New SOs/City Building Placement Config` |
| 8 | `CityPrefabCatalog` | `Zombera.World.City` | `City/Scripts/CityPrefabCatalog.cs` | `Zombera/World/City Prefab Catalog` |
| 9 | `CityParkConfig` | `Zombera.World.City` | `City/Scripts/CityParkConfig.cs` | `Zombera/World/New SOs/City Park Config` |
| 10 | `CityBiomeConfig` | `Zombera.World.City` | `City/Scripts/CityBiomeConfig.cs` | `Zombera/World/New SOs/City Biome Config` |
| 11 | `CityAreaRuntimeConfig` | `Zombera.World.City` | `City/Scripts/CityAreaRuntimeConfig.cs` | `Zombera/World/City Area Runtime Config` |

### 1.2 Existing `.asset` Files (in `Assets/02_Shared/ScriptableObjects/World/`)

All 10 non-test classes have a corresponding `.asset` file. One extra asset exists for the runtime path:

| Asset File | Class |
|------------|-------|
| `CityBuildConfig.asset` | `CityBuildConfig` |
| `CityRoadMeshConfig.asset` | `CityRoadMeshConfig` |
| `CityTerrainFlattenConfig.asset` | `CityTerrainFlattenConfig` |
| `CityPrefabStreetscapeSettings.asset` | `CityPrefabStreetscapeSettings` |
| `CityMathRoadLayoutAsset.asset` | `CityMathRoadLayoutAsset` |
| `CityBuildingPlacementConfig.asset` | `CityBuildingPlacementConfig` |
| `CityPrefabCatalog.asset` | `CityPrefabCatalog` |
| `CityParkConfig.asset` | `CityParkConfig` |
| `CityBiomeConfig.asset` | `CityBiomeConfig` |
| `CityAreaRuntimeConfig.asset` | `CityAreaRuntimeConfig` |

---

## 2. Dead Code

### 2.1 `CityPrefabCompilationTest` — **confirmed dead, safe to delete**

- File: `Assets/01_Game/02_World/Roads/Scripts/CityPrefabCompilationTest.cs`
- Self-documents: *"Minimal test file to verify compilation in Roads folder. Safe to delete once compilation is confirmed."*
- Zero references in any other `.cs` file (grep returns only its own definition).
- No `.asset` file exists for it.
- **Action:** Delete the `.cs` and `.cs.meta` files.

### 2.2 `CityParkConfig` — **needs verification**

- File: `Assets/01_Game/02_World/City/Scripts/CityParkConfig.cs`
- Has an asset: `CityParkConfig.asset`
- Searched for `CityParkConfig` in all `.cs` files — only found in its own definition.
- The builder's park code (`CityPrefabRoadNetworkBuilder.Parks.cs`) resolves park settings from `buildConfig.parkScatterSettings` (a `CityParkScatterSettings` struct), **not** from `CityParkConfig`.
- `CityAreaRuntimeConfig` does not reference `CityParkConfig` either.
- **Suspected dead.** Could also be intended for future runtime path use. Mark for investigation.

---

## 3. Overlap / Duplication

### 3.1 `CityBuildConfig` vs `CityBuildingPlacementConfig`

These two SOs have **overlapping responsibility** for building placement:

| Field | `CityBuildConfig` | `CityBuildingPlacementConfig` |
|-------|-------------------|-------------------------------|
| `useProxyPrefabs` | ✅ | ✅ |
| `buildingLayoutSettings` | ✅ (`CityNamedAreaBuildingLayoutSettings`) | ✅ (`CityNamedAreaBuildingLayoutSettings`) |
| `buildingLayoutSeed` / `layoutSeed` | ✅ (`buildingLayoutSeed`) | ✅ (`layoutSeed`) |
| `useWorldSeedForLayout` | ✗ | ✅ |
| `buildingCatalog` | ✗ | ✅ (`StreamedCityCatalog`) |

- **CityBuildConfig** (175 lines, 16 fields): Used by `CityPrefabRoadNetworkBuilder` as its primary config (referenced as `buildConfig`). Covers roads, districts, buildings, parks, decorations, door paths, lot sizes, fence prefab, and test layout settings.
- **CityBuildingPlacementConfig** (47 lines, 5 fields): Referenced **only** by `CityAreaRuntimeConfig.buildingPlacementConfig`. The runtime config resolves building settings through it.

**The builder never reads `CityBuildingPlacementConfig`.** The runtime config (`CityAreaRuntimeConfig`) never reads `CityBuildConfig`. These are two parallel but non-overlapping code paths. Currently they cannot drift because the fields are independently set, but there is no single source of truth for building placement settings.

### 3.2 Config Resolution: Builder Inline vs SO Pattern

The builder uses a **dual-config pattern** on every field. For example:

```csharp
// Inline field (hidden in Inspector)
[SerializeField, HideInInspector] private bool useTIntersectionTestLayout;

// Resolved property: prefer SO, fall back to inline
private bool ResolvedUseTIntersectionTestLayout =>
    buildConfig != null ? buildConfig.useTIntersectionTestLayout : useTIntersectionTestLayout;
```

This pattern repeats for **18+ fields** across the partial files:
- `ResolvedUseProxyPrefabs`
- `ResolvedBuildingLayoutSeed`
- `ResolvedBuildingLayoutSettings`
- `ResolvedAutoGenerateDoorPaths`
- `ResolvedDoorPathWidthMeters`
- `ResolvedDoorPathHeightOffset`
- `ResolvedMaxDoorPathLengthMeters`
- `ResolvedGenerateResidentialLots`
- `ResolvedCreateEditorFloorVisuals`
- `ResolvedFloorVisualHeight`
- `ResolvedLotFrontYardMeters`
- `ResolvedResidentialLotSize` through `ResolvedCityCoreLotSize`
- `ResolvedParkScatterSettings`
- `ResolvedScatterSettings`
- ...and more in the main `.cs` file (terrain, mesh, build config fields)

The inline fields are marked `[HideInInspector]` — they exist only as fallback values when the SO is unassigned. This means every config change requires updating the SO, not the component. The inline fields are effectively dead data on the component.

### 3.3 `CityAreaRuntimeConfig` — Two-Layer Fallback Pattern

`CityAreaRuntimeConfig` wraps sub-SO references (`CityTerrainFlattenConfig`, `CityRoadMeshConfig`, `CityBiomeConfig`, `CityBuildingPlacementConfig`) and provides its own inline fallback fields. This is the same pattern as the builder, but structured differently — the builder uses one monolithic `CityBuildConfig`, while the runtime uses four specialized sub-SOs.

---

## 4. The Builder Partial File Structure

`CityPrefabRoadNetworkBuilder` is split across **11 partial files**:

| File | Lines | Responsibility |
|------|-------|----------------|
| `CityPrefabRoadNetworkBuilder.cs` | 402 | Core: fields, footpaths, road markings, street lamps, main generate entry |
| `CityPrefabRoadNetworkBuilder.Generation.cs` | 251 | Road generation pipeline, seed, validation, cache |
| `CityPrefabRoadNetworkBuilder.Buildings.cs` | 226 | Building placement, door paths, grid/outline validation |
| `CityPrefabRoadNetworkBuilder.Districts.cs` | 396 | Named areas, district lots, fences, floor visuals |
| `CityPrefabRoadNetworkBuilder.RoadStack.cs` | 91 | Road stack setup, RefreshReferences, ground height |
| `CityPrefabRoadNetworkBuilder.Parks.cs` | 147 | Park tree/bench scatter |
| `CityPrefabRoadNetworkBuilder.Decorations.cs` | 131 | Lot trees, foliage, props scatter |
| `CityPrefabRoadNetworkBuilder.Streetscape.cs` | 245 | Traffic lights, street signs, power lines, parked cars, furniture |
| `CityPrefabRoadNetworkBuilder.TileSnap.cs` | 120 | MapMagic pinned tile snap, world terrain bounds |
| `CityPrefabRoadNetworkBuilder.Publishing.cs` | 106 | Terrain flatten, road mesh build, summary |
| `CityPrefabRoadNetworkBuilder.Gizmos.cs` | 70 | Scene gizmo drawing |
| **Total** | **2,185** | |

All files use `public sealed partial class CityPrefabRoadNetworkBuilder : MonoBehaviour` correctly. The split follows the AGENTS.md 500-line rule.

---

## 5. Findings Summary

### 5.1 Dead Code — Immediate Actions
1. **Delete `CityPrefabCompilationTest.cs`** — confirmed dead, zero references, self-documents as deletable.
2. **Investigate `CityParkConfig`** — zero code references found. If unused, delete both `.cs` and `.asset`.

### 5.2 Duplication / Tech Debt
3. **`CityBuildConfig` vs `CityBuildingPlacementConfig` overlap**: Both define `useProxyPrefabs` and `buildingLayoutSettings`. They serve different code paths (builder vs. runtime) but could be unified or at least documented.
4. **Inline-fallback pattern**: 18+ `HideInInspector` fields on the builder exist only as SO fallbacks. If the builder is always used with a `CityBuildConfig` SO assigned, these fields are dead weight. Consider removing them and making the SO mandatory.
5. **Two config architectures**: The builder uses one monolithic `CityBuildConfig`. The runtime uses four modular sub-SOs wrapped by `CityAreaRuntimeConfig`. These are different philosophies serving the same domain. If both paths are alive, document the split. If one path is unused, consolidate.

### 5.3 Possible Big Refactor
If a refactor is desired, the candidate scope would be:
- **Merge `CityBuildingPlacementConfig` into `CityBuildConfig`** (or vice versa) so there's one SO for building placement.
- **Remove the inline-fallback fields** from the builder, making the SO reference required.
- **Audit `CityAreaRuntimeConfig` path** — if the runtime city builder is still active, ensure it uses the same SOs as the prefab builder. If not, retire it.
- **Audit `CityParkConfig`** — if dead, remove. If alive, wire it into the builder instead of the inline `CityParkScatterSettings` struct.

---

## 6. File Reference

| What | Path |
|------|------|
| Builder partials (11 files) | `Assets/01_Game/02_World/Roads/Scripts/CityPrefabRoadNetworkBuilder*.cs` |
| Builder editor | `Assets/Editor/CityPrefabRoadNetworkBuilderEditor.cs` |
| Config SO classes (Roads) | `Assets/01_Game/02_World/Roads/Scripts/CityBuildConfig.cs`, `CityRoadMeshConfig.cs`, `CityTerrainFlattenConfig.cs`, `CityPrefabStreetscapeSettings.cs`, `CityMathRoadLayoutAsset.cs` |
| Config SO classes (City) | `Assets/01_Game/02_World/City/Scripts/CityAreaRuntimeConfig.cs`, `CityBuildingPlacementConfig.cs`, `CityPrefabCatalog.cs`, `CityParkConfig.cs`, `CityBiomeConfig.cs` |
| SO assets | `Assets/02_Shared/ScriptableObjects/World/City*.asset` |
| Dead test file | `Assets/01_Game/02_World/Roads/Scripts/CityPrefabCompilationTest.cs` |
