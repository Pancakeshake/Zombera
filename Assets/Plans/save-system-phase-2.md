# Save System Overhaul - Phase 2: Provider Architecture

## Goal
Decouple the monolithic `SaveManager` into domain-specific providers. This improves maintainability, allows for isolated testing of save logic, and makes it easier to extend the save schema for new systems (e.g., Quests, Equipment).

## Architecture
- **`ISaveProvider`**: Interface for all systems that contribute to or restore from the save data.
- **`SaveManager`**: Coordinates the save/load flow by calling providers in a deterministic order.

## Implementation Steps

### 1. Define Core Interface
- Create `Assets/01_Game/09_SaveSystem/Core/ISaveProvider.cs`.
- Method `OnSave(GameSaveData saveData)`
- Method `OnLoad(GameSaveData saveData)`
- Method `Priority` (to handle restore ordering)

### 2. Implement Domain Providers
- **`PlayerSaveProvider`**: Handles player transform, health, stats, and inventory.
- **`ZombieSaveProvider`**: Handles zombie snapshots and AI restoration.
- **`WorldSaveProvider`**: Handles chunk deltas, session seed, and environmental state.
- **`BuildingSaveProvider`**: Handles base completion and storage.
- **`LootSaveProvider`**: Handles container states.

### 3. Refactor SaveManager
- Maintain a list of `ISaveProvider` references.
- Update `BuildSaveSnapshot` to iterate providers for saving.
- Update `RestoreRuntimeState` to iterate providers for loading (sorted by priority).

## "Save" Naming Constraint
All new files must have "Save" in their name:
- `ISaveProvider.cs`
- `PlayerSaveProvider.cs`
- etc.

## Verification
- Verify that saving and loading still works for all existing data.
- Verify that restore order is respected (e.g., world before player).
