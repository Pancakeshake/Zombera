# Single-Tile Stress City Baseline (4_Single_Tile_Stress.unity)

Clean repro scene: `Assets/00_Scenes/SystemDevelopment/4_Single_Tile_Stress.unity`

Run Play once, then grep `%LOCALAPPDATA%\Unity\Editor\Editor.log` for these **first** markers.

## Authoritative layout spec (scene 6 screenshot)

| Field | Value |
|-------|-------|
| Center (authoring) | 500, 500 |
| Footprint | Square |
| Half extents (W/D min/max) | 400 / 400 / 400 / 400 |
| Radius (non-randomized fallback) | 280 |
| Street spacing | 80 |
| Block spacing | 55–95 |
| Block jitter | 0.22 |
| Arterial ring | On |
| Highway exits | Off |

Source of truth: `CityAreaRuntimeConfig.ApplyAuthoringLayoutDefaults`, mirrored in both `CityAreaRuntimeConfig.asset` files and `CityPrefabRoadNetworkBuilder.Reset()`.

## Baseline log markers (first occurrence per session)

| Issue | Search pattern | Notes |
|-------|----------------|-------|
| City build timeout | `[SingleTileStressSceneBootstrap] Timed out waiting for runtime City_Area build` | Should not appear after queue fix |
| EasyRoads invalid connection | `EasyRoads3D Error: connection index` | Junction port wiring failure |
| NaN mesh / collider | `non-finite value` or `-nan(ind)` | EasyRoads mesh build cascade |
| Missing script | `Missing (Mono Script)` or `missing script` | Run `Tools/0. Scenes/Diagnostics/Fix Missing Scripts + Duplicate EventSystems In Build Scenes` |
| Duplicate EventSystem | `There can be only one active Event System` | Often HUD + scene both spawn EventSystem |

## Expected healthy city pipeline (post-fix)

1. `[RuntimeCityAreaBuilder] Generated city layout after terrain` — layout + flatten + areas; unblocks Roads loading stage
2. `[RuntimeCityAreaBuilder] EasyRoads meshes built` — deferred mesh queue during LoadingWorld
3. `[RuntimeCityAreaBuilder] Initiated buildings after roads` — building queue after meshes

Bootstrap validation reports each phase separately via `ValidateCityBuildWhenReady`.

## Cleanup tools

- Missing scripts (selection): `Tools/Zombera/Remove Missing Scripts (Selection)`
- Build scenes audit/fix: `Tools/0. Scenes/Diagnostics/Fix Missing Scripts + Duplicate EventSystems In Build Scenes`
- Prefabs to check: `Assets/Shared/Prefabs/GameManager/[GameManager].prefab`, `Assets/Shared/Prefabs/UI/Menus/CoreRoot.prefab`
