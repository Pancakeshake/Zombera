# Project Overview
- Game Title: Zombera
- High-Level Concept: A zombie survival game with character customization and squad management.
- Players: Single player
- Render Pipeline: URP
- Input System: New Input System

# Problem Analysis
The project uses UMA (Unity Multipurpose Avatar) for characters. The `CharacterCreatorController` and `UmaSpawnStylingService` scripts have aggressive logic to ensure that UMA avatars use an `UMA_GLIB` (UMA Context/Generator) from the *same* scene. This was likely intended to avoid conflicts when multiple scenes (e.g., MainMenu and World) are loaded additively in the editor or during transitions.

However, the user wants to use a single, centralized `UMA_GLIB` located in a `Boot` scene. When they move the `UMA_GLIB` there and delete the one in the `MainMenu` or `World` scene:
1. `OnValidate` in `CharacterCreatorController` strips the "cross-scene" reference to the `UMA_GLIB`.
2. `EnsurePreviewAvatarGeneratorBoundToCurrentScene` (at runtime) fails to find a local context and explicitly sets the avatar's context and generator to `null`.
3. Without a context/generator, the UMA avatar fails to build or reverts to a default "customisable character" state (likely the raw DCA prefab without its library bindings).

# Proposed Changes

## 1. CharacterCreatorController.cs
- Relax the `OnValidate` logic to allow references to `context` and `umaGenerator` from other scenes. These are typically global singletons and should be allowed to persist even if they aren't in the same scene as the UI.
- Update `EnsurePreviewAvatarGeneratorBoundToCurrentScene` to try finding a local context first (maintaining support for scene-specific overrides) but falling back to `UMAContextBase.Instance` if no local one is found.
- Update `FindUmaContextInCurrentScene` and `FindUmaGeneratorInCurrentScene` to optionally return the first available instance if no scene-local instance exists.

## 2. UmaSpawnStylingService.cs
- Update `RebindAvatarToOwnerScene` to only overwrite the avatar's context and generator if a local one is found in the owner scene. If no local one is found, it should preserve the existing reference (which likely points to the global `UMA_GLIB` from the `Boot` scene).

# Implementation Steps

## Step 1: Update CharacterCreatorController.cs
- **File**: `Assets/Scripts/UI/Menus/CharacterCreatorController.cs`
- **Change**: 
    - Modify `OnValidate` (lines 151-163) to remove or comment out the cross-scene nullification for `context` and `umaGenerator`.
    - Modify `EnsurePreviewAvatarGeneratorBoundToCurrentScene` (lines 500-521) to use `UMAContextBase.Instance` as a fallback.
    - Modify `FindUmaContextInCurrentScene` and `FindUmaGeneratorInCurrentScene` to allow a global search if local search fails.

## Step 2: Update UmaSpawnStylingService.cs
- **File**: `Assets/Scripts/Characters/UmaSpawnStylingService.cs`
- **Change**:
    - Modify `RebindAvatarToOwnerScene` (lines 163-187) to avoid clearing the context/generator if no local replacement is found.

# Verification & Testing
1. **Editor Check**: Open the `MainMenu` scene. Ensure the `CharacterCreatorController` on the UI root can now hold a reference to the `UMA_GLIB` in the `Boot` scene (if loaded additively) without it being cleared by `OnValidate`.
2. **Runtime Check**: Start the game from the `Boot` scene.
3. **Character Creation**: Navigate to the Character Creator.
    - Verify the preview avatar builds correctly using the presets.
    - Verify customization (hair, beard, skin tone) works and updates the avatar.
4. **Scene Transition**: Start a new game.
    - Verify the player character spawns with the selected appearance in the `World` scene.
    - Verify no UMA errors appear in the console regarding missing context or generators.
