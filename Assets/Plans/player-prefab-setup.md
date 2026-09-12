# Project Overview
- Game Title: Zombera
- High-Level Concept: Catalog-driven character appearance and survival system.
- Flow: Boot -> MainMenu -> Loading -> World.

# Player Prefab & Visual Spawning Fixup
This plan ensures that the player prefab is fully compatible with the character appearance system and that the MainMenu is correctly wired to capture choices.

## Key Assets & Context
- `Assets/Prefabs/Player/Player.prefab`: The target player prefab.
- `Assets/Scenes/MainMenu.unity`: The scene where character selection occurs.
- `AppearanceProfileService`: The service responsible for applying hair/beard/colors.

## Implementation Steps

### 1. Player Prefab Hierarchy Setup (Step 3)
The `AppearanceProfileService` toggles GameObjects under containers named "Hair" and "Beard". We will ensure the `Player` prefab has this structure.
- **Task**: Verify/Modify the `Player` prefab hierarchy.
- **Requirement**:
    - Under the avatar root (same object as `Animator`), create empty children: **Hair** and **Beard**.
    - Move all hairstyle meshes into the **Hair** container.
    - Move all beard meshes into the **Beard** container.
    - Rename the meshes to match the names used in the `CharacterAppearanceCatalog` (e.g., "Mohawk", "ShortCut").

### 2. MainMenu Wiring Verification (Step 4)
We need to ensure the `CharacterCreatorController` in the `MainMenu` scene is fully wired to the UI.
- **Task**: Check `CharacterCreatorController` references.
- **Required Assignments**:
    - `confirmButton`: Must be assigned to the UI Confirm button.
    - `customizationController`: Must be assigned to the `CharacterCreatorCustomizationController` instance.
    - `creatorRefs`: Ensure the `refs` struct on the controller has the correct UI elements (Character Name input, portrait preview, etc.).

### 3. Automated Setup Utilities
I will provide an editor script to automate these checks and fixups.
- **File**: `Assets/Scripts/Editor/CharacterSystemValidator.cs`
- **Work**:
    - `FixPlayerPrefab()`: Finds the Player prefab and ensures "Hair" and "Beard" containers exist.
    - `ValidateMainMenu()`: Loads the MainMenu scene and logs any missing references on the `CharacterCreatorController`.

# Verification & Testing
1. **Run Validator**: Run the `CharacterSystemValidator` from the Unity menu (Tools > Zombera > Validate Character System).
2. **Boot Flow**: Start from `Boot` scene.
3. **Capture Check**: Verify that `[CharacterCreatorController] CaptureAppearanceProfileJson` logs a non-empty string when Confirm is clicked.
4. **Visual Check**: Verify that the spawned player in `World` matches the selection.
