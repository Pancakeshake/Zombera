# Save System Fixes and Hardening

## 1. Building Piece Persistence
- Implement `BuildingSaveRegistry` to map prefab names and structure IDs to assets.
- Update `BuildingSaveProvider` to use the registry for spawning pieces.
- Create `BuildingCatalogBuildTool` to automatically index all `BuildingData` and `BuildPiece` prefabs.

## 2. World Pickup Robustness
- Update `PickupSaveProvider` to use a robust spawning template (with colliders and physics setup).
- Improve the "clear" logic to avoid wiping the world if the save data is empty or missing.

## 3. Save Pipeline Integrity
- Add a write lock or task tracking per slot in `SaveSystem` to prevent concurrent write races.
- Ensure metadata/index is only updated after a successful write (or use a consistent in-memory state).

## 4. Character State Consistency
- Update `PlayerSaveProvider` to clear existing equipment before loading.
- Ensure the `equippedWeaponId` is verified and synchronized during restoration.

## 5. Migration and Determinism
- Implement real transformation logic in `SaveMigrationService`.
- Ensure `SaveManager` uses priority ordering for both Save and Load phases.

# Implementation Steps

## 1. Create Building Registry and Tool
- **Description**: Implement `BuildingSaveRegistry` ScriptableObject and `BuildingCatalogBuildTool` Editor script.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Refactor Building and Pickup Providers
- **Description**: Update `BuildingSaveProvider` and `PickupSaveProvider` with robust spawn and clear logic.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## 3. Harden Save Pipeline
- **Description**: Add write serialization and improved error handling to `SaveSystem`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 4. Improve Player Restoration
- **Description**: Refactor `PlayerSaveProvider` for clean equipment/weapon state application.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 5. Finalize Migration and Ordering
- **Description**: Update `SaveMigrationService` and `SaveManager` ordering.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes
