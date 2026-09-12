# Project Overview
- **Issue**: Characters are invisible in the World scene.
- **Root Causes**:
    1. `SpawnAppearanceStylingService.cs` returns early if no profile JSON is found, skipping UMA initialization.
    2. `NpcAppearanceVariantSpawner.cs` has syntax errors (extra braces) that likely prevent it from functioning correctly.
    3. `Player` prefab may not have a default race assigned, leading to "build aborted" when runtime assignment is skipped.

# Implementation Steps

## 1. Fix SpawnAppearanceStylingService.cs
- Update `ApplySelectedAppearanceProfile` to provide a default "HumanMale" profile if the input JSON is empty.

## 2. Repair NpcAppearanceVariantSpawner.cs
- Remove the redundant closing braces at the end of the file.

## 3. Configure Player Prefab
- Programmatically set the default race to "HumanMale" on the `DynamicCharacterAvatar` component within the `Player` prefab.

# Verification & Testing
1. **Compilation Check**: Ensure no errors in the console.
2. **Runtime Test**: Start the game from `Boot` or `MainMenu`. Verify the player and squad members build their meshes and are visible in the world.
3. **Log Check**: Confirm "No activeRace set" warnings are gone.
