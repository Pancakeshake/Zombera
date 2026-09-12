# Project Overview
- **Game Title**: Zombera
- **Objective**: Implement Phase 1 (Foundation Hardening) of the comprehensive Save System overhaul.
- **Phase 1 Goals**:
  - Implement a central `ItemDefinitionRegistry` for fast and reliable item lookup.
  - Introduce `SaveEnvelope` for data versioning and metadata management.
  - Implement atomic file operations in `SaveSystem` to prevent data corruption during crashes.

# Save System Design (Phase 1)
## 1. Item Definition Registry
Currently, the `SaveManager` uses `Resources.FindObjectsOfTypeAll<ItemDefinition>()` which is inefficient and unreliable if items are not in a `Resources` folder.
- **Solution**: A `ItemDefinitionRegistry` ScriptableObject that stores references to all items in the project.
- **Automation**: The existing `ItemsCatalogBuildTool` will be updated to automatically populate this registry whenever items are built or synced.

## 2. Save Envelope & Versioning
To support future schema migrations, all save data will be wrapped in a `SaveEnvelope`.
- **Fields**: `saveVersion`, `gameVersion`, `timestamp`, `contentHash`, `payload` (JSON string of `GameSaveData`).
- **Migration**: The system will check `saveVersion` on load to determine if a migration is needed (Phase 2).

## 3. Atomic Save Pipeline
To ensure data integrity, the write process will follow a temporary-file-swap pattern.
- **Process**:
  1. Write payload to `{slotId}.sav.tmp`.
  2. If `{slotId}.sav` exists, rename it to `{slotId}.sav.bak`.
  3. Rename `{slotId}.sav.tmp` to `{slotId}.sav`.
- **Result**: Even if the game/PC crashes during the write, either the old save or the new save will be intact.

# Key Assets & Context
- **Scripts**:
  - `Assets/01_Game/06_Inventory/Items/ItemSaveRegistry.cs` (New)
  - `Assets/01_Game/01_Core/Systems/SaveSystem.cs` (Modified)
  - `Assets/01_Game/09_SaveSystem/Core/SaveManager.cs` (Modified)
  - `Assets/Editor/ItemsCatalogBuildTool.cs` (Modified)
- **Data Models**:
  - `SaveEnvelope`
  - `ItemDefinition`

# Implementation Steps

## 1. Create Item Save Registry
- **Description**: Implement `ItemSaveRegistry` ScriptableObject and its lookup logic.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Update Items Catalog Tool
- **Description**: Modify `ItemsCatalogBuildTool.cs` to find (or create) the `ItemSaveRegistry` asset and sync all discovered `ItemDefinition` assets into it.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## 3. Implement Save Envelope and Atomic Write
- **Description**: Update `SaveSystem.cs` with `SaveEnvelope` class and refactor `SaveGameData` for atomic disk operations.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 4. Refactor SaveManager for Registry Lookup
- **Description**: Add `itemSaveRegistry` field to `SaveManager`. Replace existing item lookup logic with registry-based calls.
- **Assigned role**: developer
- **Dependencies**: Step 1, Step 2
- **Parallelizable**: No

# Verification & Testing
1. **Registry Sync**: Run "Tools/4. Items/Build Items..." and verify the registry asset is populated.
2. **Save/Load Integrity**:
   - Save a game, verify `.sav` and `.sav.bak` exist in `persistentDataPath`.
   - Verify `SaveEnvelope` fields (version) are present in the JSON file.
   - Load the game and verify all items are restored correctly.
3. **Legacy Fallback**: Verify that save files created before this change (without the envelope) still load correctly via a fallback check.
