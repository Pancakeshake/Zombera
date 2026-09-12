# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival game with survivors, squad management, and zombies.
- Players: Single player
- Target Platform: Standalone Windows 64
- Render Pipeline: Universal Render Pipeline (URP)

# Cleanup & Refactoring (No-UMA Policy)
Since UMA is no longer used, all scripts and assets referencing UMA must be renamed and logic sanitized to use a generic "Runtime Appearance Profile" system.

## Renaming Tasks
- **Scripts & Files**:
    - `Assets/Scripts/UI/Menus/CharacterCreation/UmaAppearanceService.cs` -> `AppearanceProfileService.cs`
    - `Assets/Scripts/AI/ZombieUmaAppearance.cs` -> `ZombieAppearance.cs`
    - `Assets/Scripts/Data/ZombieUmaVisualProfile.cs` -> `ZombieVisualProfile.cs`
    - `Assets/Scripts/Characters/NpcUmaVisualSpawner.cs` -> `NpcAppearanceVariantSpawner.cs` (Already has this name in class, but file name should match or be simplified)
    - `Assets/Scripts/Characters/UmaSpawnStylingService.cs` -> `SpawnAppearanceStylingService.cs`
- **Classes**:
    - `ZombieUmaAppearance` -> `ZombieAppearance`
    - `ZombieUmaVisualProfile` -> `ZombieVisualProfile`
    - Update all references in `ZombieSpawner.cs`, `SurvivorSpawner.cs`, `StartupSquadSpawner.cs`, `PlayerSpawner.cs`, etc.

# Game Mechanics
## Core Gameplay Loop
The player survives, manages a squad, and customization persists through a save system. Variety in NPC appearance is generated using a unified profile contract (`CharacterAppearanceProfile`).

# Implementation Steps

## 1. Implement Appearance Service Apply/Capture
- **File**: `Assets/Scripts/UI/Menus/CharacterCreation/AppearanceProfileService.cs` (Renamed from `UmaAppearanceService.cs`)
- **Work**:
    - Implement `TryApplyProfile`:
        - Set `avatarRoot.transform.localScale` using "height" DNA value (if present) or default to 1.
        - Apply `skinColor`, `hairColor`, `eyeColor` to `Renderer` materials. Use name/property heuristics (e.g., finding material names containing "Skin", "Hair", "Eyes").
        - Toggle child GameObjects (Wardrobe Proxies) based on `wardrobeSelection.hairRecipeName` and `beardRecipeName`. It will look for child GameObjects with these names and enable them, while disabling others in the same "slot" (e.g. "Hair" container).
    - Implement `TryCaptureProfile`:
        - Read transform scale into "height" DNA.
        - Sample colors from materials.
        - Identify active wardrobe children to populate `hairRecipeName` and `beardRecipeName`.
- **Verification**: Profile round-trip changes visible state, then captures same values.

## 2. Wire Player Spawn Path
- **Files**:
    - `Assets/Scripts/Characters/PlayerSpawner.cs`
    - `Assets/Scripts/Characters/SpawnAppearanceStylingService.cs`
- **Work**:
    - `PlayerSpawner.cs`: In `SpawnPlayer`, pass the selected profile JSON from `CharacterSelectionState` to `SpawnAppearanceStylingService`.
    - `SpawnAppearanceStylingService.cs`: 
        - Update `ApplySelectedAppearanceProfile` to deserialize the profile and call `AppearanceProfileService.TryApplyProfile`.
        - Feed `CharacterSelectionState.SelectedAppearanceProfileJson` into the application path.
- **Verification**: Selected creator appearance is visible on spawned player.

## 3. Persist Appearance in Save Data
- **Files**:
    - `Assets/Scripts/Core/SaveSystem.cs`
    - `Assets/Scripts/Systems/SaveManager.cs`
- **Work**:
    - `SaveSystem.cs`: Add `appearanceProfileJson` string field to `PlayerSaveData`, `SquadMemberSaveData`, and `ZombieSaveData`.
    - `SaveManager.cs`:
        - Update `PopulateUnitData` and `PopulateZombieData` to capture the profile using `AppearanceProfileService.TryCaptureProfile`.
        - Update `RestorePlayerState` and `RestoreSquadState` to apply the saved profile JSON.
- **Verification**: Save/load restores visuals for player and squad.

## 4. Add Zombie Restore Path
- **File**: `Assets/Scripts/Systems/SaveManager.cs`
- **Work**:
    - In `RestoreRuntimeState`, add a call to a new `RestoreZombieState` method.
    - `RestoreZombieState` will iterate through `saveData.zombies`, find or spawn the corresponding zombie, and apply its saved `appearanceProfileJson`.
- **Verification**: Saved zombies respawn with restored visuals.

## 5. Unify Variant Handling & Randomization
- **Files**:
    - `AppearanceProfileService.cs`
    - `NpcAppearanceVariantSpawner.cs`
    - `ZombieAppearance.cs`
- **Work**:
    - Centralize randomization logic in `AppearanceProfileService.GenerateRandomProfile()`.
    - Refactor `NpcAppearanceVariantSpawner` and `ZombieAppearance` to generate a profile first, then use the standard `TryApplyProfile` method.
- **Verification**: All actor types use the same profile format for visuals and can be randomized or selected.

## 6. Legacy Migration
- **File**: `Assets/Scripts/Core/CharacterSelectionState.cs`
- **Work**:
    - In `GetProfileDefaults` or a similar startup check, if `SelectedAppearanceRecipe` (old) has data but `SelectedAppearanceProfileJson` (new) is empty, generate a default `CharacterAppearanceProfile` and persist it.
- **Verification**: Old loadouts still produce stable visuals.

# Verification & Testing
- **Round-Trip Test**: In Character Creator, modify appearance, verify it applies.
- **Spawn Test**: Start game, verify player look matches creator selection.
- **Save/Load Test**: Save game, restart, load, verify player, squad, and zombies maintain their unique looks.
- **Hierarchy Check**: Verify no UMA-specific components are required; the system relies on standard Unity transforms, renderers, and materials.
