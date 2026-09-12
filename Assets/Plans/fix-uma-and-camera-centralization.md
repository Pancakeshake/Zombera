# Project Overview
- Game Title: Zombera
- High-Level Concept: Zombie survival with character customization.
- Players: Single player
- Input System: New Input System
- Centralization Strategy: Moving core infrastructure (UMA_GLIB, AudioListener, Main Camera) to a persistent `Boot` scene.

# Problem Analysis
The project has several scripts that enforce "scene-locality" for core components. 
1. **UMA centralization**: Moving `UMA_GLIB` to `Boot` causes the `CharacterCreatorController` and `UmaSpawnStylingService` to nullify references because they are cross-scene. This reverts the character to a default "customisable character" (missing its library context).
2. **Audio/Camera centralization**: Moving the `AudioListener` and `Camera` to `Boot` triggers warnings (when they are missing in other scenes) or breaks UI logic (like the Character Creator) because it filters for cameras in the *current* scene only.

# Proposed Changes

## 1. CharacterCreatorController.cs
- **Cross-Scene UMA**: Update `OnValidate` and `EnsurePreviewAvatarGeneratorBoundToCurrentScene` to allow and fall back to global `UMAContextBase.Instance` and `UMAGeneratorBase` even if they are in different scenes.
- **Cross-Scene Camera**: Update `TryResolvePreviewRenderTarget` to search globally for cameras if a scene-local one isn't found. This allows the `Boot` camera to be used if needed, or a persistent `PreviewCamera`.
- **Reference Management**: Ensure that `DontDestroyOnLoad` objects from `Boot` aren't accidentally destroyed or cleared during scene transitions.

## 2. UmaSpawnStylingService.cs
- **Flexible Rebinding**: Update `RebindAvatarToOwnerScene` to prioritize local UMA components but fall back to global ones rather than clearing the references.

## 3. GameManager.cs
- **Global Listener**: Refine `EnsureSingleAudioListener` to prioritize the persistent listener from the `Boot` scene if one exists, ensuring no duplicate warnings or conflicts during additive loads.

# Implementation Steps

## Step 1: Update CharacterCreatorController.cs
- **File**: `Assets/Scripts/UI/Menus/CharacterCreatorController.cs`
- **Changes**:
    - Relax `OnValidate` (line 145+) to permit cross-scene `context` and `umaGenerator`.
    - Update `EnsurePreviewAvatarGeneratorBoundToCurrentScene` (line 500+) to fall back to `UMAContextBase.Instance`.
    - Update `TryResolvePreviewRenderTarget` (line 900+) and `FindUmaContextInCurrentScene` / `FindUmaGeneratorInCurrentScene` to perform a global search if the scene-local search yields no results.

## Step 2: Update UmaSpawnStylingService.cs
- **File**: `Assets/Scripts/Characters/UmaSpawnStylingService.cs`
- **Changes**:
    - Update `RebindAvatarToOwnerScene` (line 163+) to preserve existing references if no local scene-specific `UMA_GLIB` is found.

## Step 3: Update GameManager.cs
- **File**: `Assets/Scripts/Core/GameManager.cs`
- **Changes**:
    - In `EnsureSingleAudioListener`, ensure it correctly identifies and preserves the listener from the `Boot` scene (which is likely part of the `DontDestroyOnLoad` root).

# Verification & Testing
1. **Setup**: Ensure `UMA_GLIB`, `Main Camera`, and `AudioListener` are in the `Boot` scene.
2. **Boot**: Start the game from `Boot`.
3. **Character Creation**: Open the Character Creator in `MainMenu`.
    - Verify the avatar builds (using `UMA_GLIB` from `Boot`).
    - Verify the preview displays (finding the camera/RenderTexture correctly).
4. **Gameplay**: Enter the `World` scene.
    - Verify the player spawns correctly.
    - Verify audio works and no "no audio listener" warnings appear.
