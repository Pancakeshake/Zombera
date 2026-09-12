# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG set in a zombie-infested world with base building and tactical combat.
- Players: Single player (with squad management).
- Inspiration / Reference Games: Kenshi, Project Zomboid, State of Decay.
- Tone / Art Direction: Realistic, gritty survival.
- Target Platform: PC (Windows).
- Screen Orientation / Resolution: Landscape 1920x1080.
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
Scavenge resources, fight zombies/bandits, build/fortify bases, and manage a squad of survivors.
## Controls and Input Methods
Keyboard and Mouse (New Input System). Features character movement, tactical squad commands, and building placement.

# UI
- HUD: Health, stamina, quick-slots, squad status.
- Menus: Inventory, crafting, building, squad management.
- World UI: Damage popups, interaction prompts, progress bars.

# Key Asset & Context
- `GameManager`: Central coordinator.
- `CoreEventBus`: Decoupled communication.
- `PlayerInputController`: Routes inputs.
- `UnitController`: Character movement/AI.
- `ZombieManager`: Zombie lifecycle/spawning.
- `InventoryManager`: Item tracking.
- `BuildManager`: Base building.

# Implementation Steps

## Code Quality & Architecture
1. **Refactor Hardcoded Key Bindings in PlayerInputController**: Move `crawlKey`, `crouchKey`, `interactKey`, and squad command keys (1-5) to an `InputActionAsset`. Update `PlayerInputController` to use events from `PlayerInput` or polling via `InputAction`.
2. **Standardize System Initialization**: Ensure all classes implementing `IGameSystem` have their `Initialize` called consistently by `GameManager` and avoid logic in `Awake` that depends on other systems.
3. **Consolidate Partial Classes**: Evaluate if some partial classes (like `GameManager` or `PlayerInputController`) can be broken down into smaller, focused components (e.g., `PlayerCombatHandler`, `PlayerMovementHandler`) instead of one giant partial class.
4. **Improve Singleton Pattern**: Implement a more robust singleton pattern that warns if an instance is accessed before being set or if duplicates are found in the scene.
5. **Optimize CoreEventBus Dispatch**: Reduce GC pressure in `CoreEventBus` by avoiding closures in `Publish` (when in queued mode) and investigating the use of a more efficient delegate storage if needed.

## Performance & Optimization
6. **Object Pooling for Projectiles and UI**: Verify if projectiles (bullets, arrows) and UI elements (damage popups) are pooled. Implement/Refactor to use `UnityEngine.Pool`.
7. **Optimize UnitController Updates**: `UnitController` performs multiple `Try...` checks in `Update`. Refactor to a state machine or use more efficient conditional checks to reduce overhead for large numbers of units.
8. **ZombieManager Budgeting**: Ensure `ZombieManager`'s dynamic budget scaling is efficient and doesn't cause spikes when calculating spawn positions or checking player proximity.
9. **NavMesh Query Optimization**: `UnitController` and `ZombieManager` frequently query the NavMesh. Implement caching or limit the frequency of these queries where possible.
10. **Renderer Scratch Buffer Usage**: `GameManager` has a `_rendererScratch` list. Ensure it's cleared properly and used efficiently across different systems to avoid multiple allocations.

## Bug Fixes & Robustness
11. **Fix UnitController Animator Fetching**: The `_animator` is fetched in `Awake`, but comments suggest visuals (and thus the animator) might load later. Implement a robust "VisualsReady" check or lazy-loading with event notification.
12. **Knockback and NavMesh Sync**: Improve the `ApplyKnockback` logic in `UnitController` to ensure the `NavMeshAgent` is correctly re-enabled and synchronized with the transform after the knockback ends.
13. **Handle Missing EventSystem**: Ensure UI-related logic in `PlayerInputController` and `CoreEventBus` fails gracefully if the `EventSystem` is missing or being reloaded.
14. **Inventory Weight Recalculation**: Ensure `RefreshAppliedSpeed` in `UnitController` is called only when significant inventory changes occur, avoiding unnecessary recalculations.
15. **SaveManager Data Integrity**: Review `SaveManager.LoadGame` to ensure it handles missing or corrupted save files gracefully without breaking the `GameManager` lifecycle.

## Features & Polish
16. **Implement Input Rebinding Support**: Using the New Input System, add a system to allow users to rebind keys, utilizing the refactored `InputActionAsset`.
17. **Enhanced Debug Logging**: Standardize debug logging across systems using a custom logger that can be toggled via `GameManager.enablePerfTraceLogs`.
18. **Zombie Spawn Placement Validation**: Improve the logic for finding valid zombie spawn points to avoid spawning inside buildings or inaccessible areas.
19. **Squad Command Visuals**: Add visual feedback (e.g., 3D icons or move-target markers) for all squad commands (Move, Attack, Defend).
20. **CoreEventBus Diagnostic Tools**: Create an Editor window to visualize event traffic on the `CoreEventBus` for easier debugging of decoupled systems.

# Verification & Testing
1. **Input Test**: Verify all actions (movement, combat, building) work correctly after migrating to the Action Map.
2. **Stress Test**: Spawn 200+ zombies and monitor performance/frame time.
3. **Save/Load Test**: Verify that all system states are correctly restored after loading a save.
4. **Unit Tests**: Write unit tests for `CoreEventBus` to ensure correct subscription/unsubscription and dispatch order.
5. **NavMesh Test**: Verify units don't get stuck or move through walls during knockbacks or fallback movement.
