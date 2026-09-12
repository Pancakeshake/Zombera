# AI Plan: Quick Single-Tile Stress Scene

## 1) Goal
Create a dedicated test scene that skips the main menu flow and loading screen logic, boots directly into gameplay, uses a single MapMagic tile, and stresses the world with the maximum practical number of units while keeping the same player controls and HUD behavior as the real game.

Target outcome:
- no main menu hop
- no loading screen hop
- one small, deterministic world setup
- player movement/combat/interaction controls stay identical to the normal game
- startup squad and world systems fully active
- scene is good for stress-testing AI, navmesh, grounding, HUD, and spawn density

## 2) What Already Exists That You Can Reuse

### Bootstrap and test entry points
- `QuickWorldTestBootstrapper` already starts a procedural world session, initializes `GameManager`, and forces `GameState.Playing`.
- `TestGameplayBootstrap` already ensures `GameManager` and `WorldHUD` are present and active in a test scene.
- `TestScenePlayerBootstrap` already spawns a playable player, wires camera/input, and waits for world readiness.
- `PlayerSpawner` already owns the real gameplay spawn pipeline, including startup squad spawning and NavMesh-aware setup.

### World streaming / terrain stack
- `WorldManager` already coordinates chunk streaming, MapMagic tile streaming, region lookup, and world simulation.
- `ChunkLoader` already supports MapMagic tile streaming and chunk budget limits.
- `UnitGroundingManager` already fixes seam issues when MapMagic tiles apply.
- `RuntimeNavMeshBootstrapper` and `StreamingNavMeshTileService` already handle runtime navmesh readiness for the real game.

### Controls and HUD
- `PlayerInputController` and `PlayerBrain` already represent the actual player control path.
- `HUDManager` and `WorldHUDController` already drive the live HUD and squad-management overlay.
- Using these directly means the test scene exercises the same input and UI behavior as the main game.

## 3) What To Skip

Do not include these in the test scene flow:
- main menu scene
- loading screen scene
- save/load menu transitions
- character creator startup flow

The goal is to start directly in gameplay and spend the frame budget on world simulation and unit count, not UI transitions.

## 4) Scene Composition

Create a scene that contains only the systems needed to start gameplay immediately.

Recommended scene contents:
- `GameManager` or a prefab that instantiates it
- `WorldManager`
- `ChunkLoader`
- `ChunkGenerator`
- `RegionSystem`
- `MapMagicTileStreamBridge`
- a single MapMagic tile / minimal world tile layout
- `PlayerSpawner`
- `HUDManager` or `WorldHUDController`
- `UnitGroundingManager`
- runtime navmesh bootstrap / streaming navmesh service
- optional debug camera or world camera if not created by the player pipeline

## 5) Startup Flow

The scene should boot in this order:

1. Scene loads directly.
2. `QuickWorldTestBootstrapper` creates or resolves `GameManager`.
3. It begins a `ProceduralWorldSession` immediately.
4. `TestGameplayBootstrap` ensures the HUD is active.
5. `WorldManager` starts world generation/streaming.
6. `PlayerSpawner` spawns the player and startup squad.
7. Player camera, input, and HUD binding are restored through the normal runtime path.
8. `UnitGroundingManager` corrects any tile seam or elevation issues.

If you want the shortest possible time-to-control, use the existing deferred startup squad path so the player becomes playable first and the rest of the squad fills in over several frames.

## 6) Single MapMagic Tile Setup

Use one tile only, and keep the world constrained to that tile.

Recommended rules:
- place one MapMagic tile in the scene
- keep tile streaming margins at the minimum needed to keep that tile valid
- disable extra tile rings or world-expansion logic
- avoid far-distance streaming targets
- keep the procedural start seed deterministic so the scene is reproducible

This is a stress scene, not a traversal scene. The goal is to keep the world footprint small so the unit-count test is not diluted by streaming cost.

## 7) Max-Unit Stress Configuration

Use the existing startup squad and world spawner settings to push unit count without going through menu-based setup.

Recommended knobs:
- `spawnStartupSquadOnWorldStart = true`
- raise `startupSquadTotalCount` to your target test cap
- keep `minimumStartupSquadTotalCount` aligned with that cap
- keep `startupInitialCharacterCount` at the same value unless you want a staged ramp-up
- keep `deferStartupSquadSpawning = true` if you want smoother startup and fewer frame spikes
- turn off validation-only spawns unless you specifically want to test enemy load too

If you want to stress the full simulation, also raise any zombie/world event budgets you want to measure in that scene, but do that separately from the squad-count test so the bottleneck is obvious.

## 8) Control Fidelity Requirement

The scene must use the same player control path as the main game.

That means:
- keep `PlayerInputController` enabled after spawn
- keep `PlayerBrain`/player control binding intact
- keep the world camera binding path used by the real spawn pipeline
- keep squad and command hotkeys unchanged
- keep HUD toggles unchanged

In other words, this scene should feel like the live game, just with the menu/loading detour removed.

## 9) Runtime Systems To Keep Active

Keep these active so the test is meaningful:
- navmesh baking / streaming
- player movement and AI follow/move logic
- squad roster logic
- HUD and squad overlay
- grounding correction
- world chunk streaming
- region lookup
- combat targeting and hostility checks
- save-related runtime initialization only if needed for persistence validation

## 10) Runtime Systems To Disable Or Minimize

Disable or reduce these to keep the test focused and fast:
- loading screen UI
- main menu UI
- save/load slot browsing UI
- character-creator flow
- any scene-specific intro/audio transitions
- optional expensive visual polish that does not affect the control test

## 11) Suggested Implementation Path

### Phase 1: Build the new test scene shell
- duplicate or derive from an existing world test scene
- remove main menu / loading scene dependencies
- add the direct gameplay bootstrap components
- drop in one MapMagic tile

### Phase 2: Wire the runtime bootstrap
- add `QuickWorldTestBootstrapper`
- add `TestGameplayBootstrap`
- add `PlayerSpawner`
- confirm `GameState` becomes `Playing` immediately

### Phase 3: Make the scene reproducible
- set a fixed procedural seed
- keep one tile only
- lock the world start position
- keep chunk streaming budgets narrow

### Phase 4: Push unit count
- increase startup squad count
- optionally add enemy density in the same scene
- verify all units spawn, remain grounded, and respond to controls

### Phase 5: Measure and tune
- watch frame time during spawn
- watch navmesh bake time
- watch HUD and input responsiveness
- watch pathing on the single tile

## 12) Verification Checklist

Before calling the scene done, verify:
- the scene starts without entering MainMenu or Loading
- the player is controllable immediately
- squad members spawn correctly
- HUD is visible and interactive
- player movement and combat feel identical to the main game
- the single MapMagic tile loads and remains stable
- unit grounding is correct
- there are no startup-only missing reference errors
- the scene can hold the target unit count without breaking controls

## 13) Best First Slice

Build the smallest useful version first:
1. one scene
2. one MapMagic tile
3. `QuickWorldTestBootstrapper`
4. `TestGameplayBootstrap`
5. `PlayerSpawner` with a larger startup squad count
6. normal HUD and controls

That gets you a direct gameplay stress harness with the least amount of new code.