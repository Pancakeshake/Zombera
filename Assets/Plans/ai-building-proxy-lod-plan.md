# AI Plan: Building Proxy Bake in the Modular Generator Flow

Status: implemented (2026-08-23), rollout pending in-editor run.

## Goal

Every generated building prefab gets a game-ready **proxy** automatically:

- Proxy = all renderers collapsed into one mesh per material (sub-meshes), single fitted
  BoxCollider, shadows / light probes / reflection probes off.
- Output: `Assets/02_Shared/Proxies/Buildings_Complete/<District>/<WxD>/<District>_N_<guid8>_Proxy.prefab`
  plus a paired `<name>_ProxyMeshes.asset` mesh bundle (existing `CityBuildingPrefabNaming`
  parses `<District>_N_<8hex>_Proxy` → source name).
- Building prefabs themselves now save into footprint subfolders:
  `<District>/<Width>x<Depth>/<District>_N.prefab` (cells, e.g. `Residential/1x1`) —
  `Generation.cs` `TryComputeGenerationPlan` + `NextRandomBuildingNumber` handle it;
  proxies mirror the same subfolder tree. All 8 district folders already exist.
- No LODGroup in v1 (single-mesh proxy is the win; no decimator exists in the project).

## Why proxies

Runtime + editor city placement currently instantiates full modular buildings
(dozens–hundreds of GameObjects each). Placement code already prefers proxies
(`useProxyPrefabs` defaults `true` in `CityBuildConfig` / `CityBuildingPlacementConfig`;
`CityDistrictLotPlacement` / `CityNamedAreaBuildingSpawnUtility` fall back to the full
prefab when `entry.proxyPrefab == null`). Only the proxy assets were missing.

## Changes

- `Assets/Editor/BuildingProxyBaker.cs` (new): reusable prefab → proxy baker.
  Reuses `StreamedCityProxyGeometryHelper` for the combine; own visual-only strip
  (keeps `StairSocket*` markers so door-path generation works on proxies), optional
  GPU-instancing enable on source materials, `_ProxyMeshes.asset` bundle save.
- `Assets/Editor/ModularSingleLevelHouseGeneratorTool.Proxy.cs` (new): generator hook
  `TryBuildProxyForBuilding` called after skin in `GenerateNormalized` (gated by
  `GeneratorSettings.GenerateProxyPrefab`, default true); batch menu
  `Tools/Build/Mod Kits/Building Generator/Rebuild All Building Proxies`
  (`RebuildAllBuildingProxies`, public for MCP/tests) + orphan proxy cleanup.
- `ModularSingleLevelHouseGeneratorTool.cs`: `DefaultProxyOutputFolder` const +
  `GeneratorSettings.GenerateProxyPrefab` / `ProxyOutputFolder` fields.
- Wiring fixes (stale folders):
  - `CityStreetscapeConfig.proxyPrefabFolder` default + serialized asset →
    `Assets/02_Shared/Proxies/Buildings_Complete` (asset edited via YAML).
  - `CityPrefabRoadNetworkBuilder.ResolvedProxyFolder` fallback → same.
  - `StreamedCityProxySwapTool` defaults: source `Buildings_Modular_Complete`,
    proxies `Assets/02_Shared/Proxies/Buildings_Complete`, catalog
    `Assets/02_Shared/ScriptableObjects/StreamedCityCatalog.asset`; `BuildProxyAssetPath`
    preserves the source's district subfolder.
  - `BuildingSkinReskinTool.AssembledRoot`, `StreamedCityBuilderQuickTool.DefaultPrefabFolder`,
    `StreamedMapMagicCityBuilder.fallbackTownPrefabFolderPath` → `Buildings_Modular_Complete`.

## Rollout steps

1. In Unity, run `Tools/Build/Mod Kits/Building Generator/Rebuild All Building Proxies`
   (backfills the 26 existing prefabs, deletes the 8 orphan proxies at the
   `Proxies/Buildings_Complete` root whose sources no longer exist).
2. Regenerate the city (World Builder → Buildings step) — placement should now log
   proxy usage and instantiate far fewer objects.
3. Streamed runtime path (separate): run
   `Tools/World/City/Rebuild Streamed City Catalog (Buildings Modular Complete)` then
   `Tools/Build/Mod Kits/Building Generator/Proxy Swap/Build Proxies And Wire Catalog`
   to rewire `Resources/World/StreamedCityCatalog.asset` (currently missing; runtime
   `RuntimeCityAreaBuilder` loads it via `Resources.Load`).

## Follow-ups (not yet done)

- Migrate the existing 26 buildings from flat `<District>/` folders into footprint
  subfolders (measure cells via `CityBuildingPrefabFootprintUtility`, `AssetDatabase.MoveAsset`).
  After moving, re-run `Rebuild All Building Proxies` — path-based orphan cleanup removes
  the old-location proxies automatically.

- LOD1 box silhouette via LODGroup (needs none of the absent third-party decimators).
- `BuildingLODController` is dead code — wire into player-built structures or delete.
- Generator-side speedups if needed later: cache kit prefab dependencies across
  `Generate()` calls (window flow), avoid double skin save.
