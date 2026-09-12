# Project Overview
- Game Title: Zombera
- High-Level Concept: A survival RPG set in a zombie-infested world, combining third-person combat, base building, and resource management.
- Players: Single player (with squad/survivor AI).
- Inspiration / Reference Games: Project Zomboid, State of Decay, Kenshi.
- Tone / Art Direction: Gritty, realistic survival.
- Target Platform: PC (Windows).
- Screen Orientation / Resolution: Landscape 1920x1080.
- Render Pipeline: Universal Render Pipeline (URP).

# Game Mechanics
## Core Gameplay Loop
1. **Scavenge**: Explore the procedural world for resources.
2. **Build**: Construct and fortify bases.
3. **Survive**: Manage health, stamina, hunger, and thirst while avoiding or fighting zombies.
4. **Progress**: Improve skills (Strength, Shooting, Stealth, etc.) through action-based XP.

## Controls and Input Methods
- **Keyboard & Mouse**: WASD for movement, Mouse for aiming/looking.
- **New Input System**: Uses `PlayerInputController` for modular input handling.
- **Contextual Actions**: Interaction with containers, building placement, and combat stances.

# UI
- **HUD**: Health/Stamina bars, quick-slots, encumbrance indicators.
- **Inventory**: Weight-based grid or list system.
- **Building Menu**: Ghost previews for placement.
- **Main Menu**: Character creation and save slot management.

# Key Asset & Context
- **Core Managers**: `GameManager`, `UnitManager`, `ZombieManager`, `CombatManager`.
- **Systems**: `EquipmentSystem`, `WeaponSystem`, `SaveSystem`.
- **Data**: `ItemDefinition`, `WeaponData`, `UnitStats`.
- **Characters**: UMA-based dynamic characters for players and NPCs.

# Implementation Steps
## Phase 1: Stability & Bug Fixes
1. **Fix UMA Editor Errors in EquipmentSystem**:
   - Wrap `avatar.BuildCharacter()` in `EquipmentSystem.AppearanceUma.cs` with additional safety checks.
   - Investigate why `SetAnimatorController` fails in the editor and add guards against destroying assets.
2. **Resolve Cross-Scene References**:
   - Replace the direct reference from `Player_Preview` (MainMenu) to `UMA_GLIB` (Boot) with a dynamic lookup or a "Global Library" service initialized by `GameManager`.

## Phase 2: Performance Optimizations
1. **Integrate AITickManager**:
   - Modify `ZombieController.cs` to register with `AITickManager` on enable.
   - Remove or gate the `Update()` loop in `ZombieController` so it only ticks when requested by the manager.
2. **Optimize UnitManager Registry**:
   - Change `RefreshRegistry` to be more incremental.
   - Replace `_activeUnits.RemoveWhere` with a more efficient removal process to avoid O(n) overhead on every spawn/despawn.
3. **Save System Snapshotting**:
   - Optimize `SaveManager.BuildSaveSnapshot()` to build the data payload over multiple frames if necessary, or filter out non-essential zombie data.

## Phase 3: Architectural Refinement
1. **Decouple Weapon Logic**:
   - Move specialized bow logic out of `WeaponSystem.cs` into a `BowWeaponData` or dedicated `BowSubsystem`.
   - Refactor `WeaponSystem` to be more polymorphic.
2. **Data-Driven Skill Tuning**:
   - Create an `AttributeConfiguration` ScriptableObject to hold XP rates and growth curves currently hardcoded in `UnitStats.cs`.
   - Update `UnitStats` to reference this configuration.

# Verification & Testing
1. **Editor Stability Test**: Open the MainMenu and World scenes; verify that no `NullReferenceException` or "Destroying assets" errors appear in the console.
2. **AI Performance Test**: Spawn 100+ zombies and verify that the frame rate remains stable and that `AITickManager` is correctly staggering updates.
3. **Save/Load Integrity**: Perform multiple saves and loads to ensure all unit stats, inventory, and procedural world state are restored correctly without data loss.
4. **Input Validation**: Verify that movement and combat (including bow mechanics) feel responsive after the refactor.
