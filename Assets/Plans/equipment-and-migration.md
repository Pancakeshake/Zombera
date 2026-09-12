# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival game with survivors, squad management, and zombies.
- Target Platform: Standalone Windows 64
- Render Pipeline: Universal Render Pipeline (URP)

# Equipment & Migration Implementation
This plan covers the implementation of socket-based equipment visual mapping and save/load migration from legacy UMA fields.

## Key Assets & Context
- `Assets/Scripts/Inventory/EquipmentSystem.cs`: Authoritative system for equipment visuals.
- `Assets/Scripts/Core/CharacterSelectionState.cs`: Handles runtime character selection and migration.
- `Assets/Scripts/Systems/SaveManager.cs`: Orchestrates save/load persistence and migration.
- `Assets/Scripts/UI/Menus/CharacterCreation/AppearanceMigrationUtility.cs`: New utility for migrating legacy appearance data.

## Implementation Steps

### 1. Equipment Visual Mapping (Socket/Bone based)
- **File**: `Assets/Scripts/Inventory/EquipmentSystem.cs`
- **Work**:
    - Verify `ResolveHumanoidBoneSocket` supports all slots including `Head`, `Back`, `Belt`, `LeftHand`, `RightHand`.
    - Ensure `ApplyVisualAttachment` is the sole source of truth for equipment visuals.
    - Explicitly ignore `appearanceWardrobeRecipeName` for equipment items to fulfill the "no wardrobe recipe dependency" requirement.

### 2. Save/Load Migration Rules
- **File**: `Assets/Scripts/UI/Menus/CharacterCreation/AppearanceMigrationUtility.cs`
- **Work**:
    - Create a static utility to convert legacy UMA recipe names (e.g., "HumanMaleRecipe") to modern race names ("HumanMale").
    - Implement `MigrateLegacyUmaToProfile(string recipe, out string profileJson)`.
- **File**: `Assets/Scripts/Core/CharacterSelectionState.cs`
- **Work**:
    - In `LoadProfileDefaults`, if `SelectedAppearanceRecipe` is present but `SelectedAppearanceProfileJson` is empty, perform migration.
- **File**: `Assets/Scripts/Systems/SaveManager.cs`
- **Work**:
    - In `RestorePlayerState` and `RestoreSquadState`, add a check to migrate if `appearanceProfileJson` is missing but legacy data is available.

### 3. QA Checklist Creation
- **File**: `Assets/Plans/qa-checklist-appearance-equipment.md`
- **Work**:
    - Draft a comprehensive checklist covering player spawn, startup squad, survivor region spawn, zombie spawn, equip/unequip, combat, scene transitions, and save/load.

# Verification & Testing
- **Socket Attachment**: Equip a weapon and verify it attaches to the correct hand bone. Equip a backpack and verify it attaches to the chest/back bone.
- **Migration Test**: Simulate an old save with `SelectedAppearanceRecipe` set and verify a modern profile is generated on load.
- **No Wardrobe Dependency**: Clear `appearanceWardrobeRecipeName` from an item but keep `equippedVisualPrefab` and verify it still shows up correctly.
