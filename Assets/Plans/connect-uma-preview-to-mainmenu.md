# Project Overview
- **Game Title**: Zombera
- **High-Level Concept**: Survival RPG with UMA character customization.
- **MainMenu Setup**: The Main Menu features a character creation screen that uses a 3D preview room.
- **Goal**: Connect the new UMA-based Player system to the existing Main Menu preview room so that customization correctly reflects on the preview avatar.

# Game Mechanics
## Character Preview
The preview avatar in the Main Menu should be a fully functional UMA character, allowing players to see changes to DNA (height, body shape) and wardrobe (clothing, hair) in real-time before starting the game.

# UI
- **CharacterCreatorPanel**: The main UI for customization.
- **UMACharacterPanel**: A sub-panel that contains the `RawImage` displaying the 3D preview.
- **Preview_Camera**: Renders the 3D room to a `RenderTexture`.

# Key Asset & Context
- `Assets/Scenes/MainMenu.unity`: The scene to be modified.
- `Assets/Prefabs/Player/Player.prefab`: The source for the preview avatar.
- `Assets/Prefabs/UI/Menus/UMA_GLIB.prefab`: The UMA global configuration.
- `RT_CharacterPreview.renderTexture`: The texture used by the preview camera.

# Implementation Steps
## 1. Scene Setup: MainMenu.unity
- **Add UMA Global Setup**: Instantiate `Assets/Prefabs/UI/Menus/UMA_GLIB.prefab` into the `MainMenu` scene. This provides the `UMAGenerator` and `UMAContext` needed for UMA characters to build.
- **Clean up UMA_GLIB**: Disable the default `UMAPreviewAvatar` and `UMAPreviewCamera` inside the instantiated `UMA_GLIB` to avoid conflicts with the existing `CharacterPreviewRoom`.
- **Assign Preview Avatar**: On the `CharacterCreatorPanel` object, find the `CharacterCreatorRefs` component and assign the `Player_Preview` object (found in `CharacterPreviewRoom`) to the `previewAvatar` field.
- **Fix UI Texture Link**: Update the `RawImage` component on `UmaPreviewRawImage` (nested under `UMACharacterPanel`) to use the `RT_CharacterPreview` RenderTexture. This ensures the UI displays the camera output from the preview room.

## 2. Scripted Initialization (Optional but Recommended)
- Ensure the `CharacterCreatorController` correctly triggers a build on the preview avatar during its `Initialize` phase. (Already handled by the previous UMA integration changes in `AppearanceProfileService`).

# Verification & Testing
1. **Scene Check**: Open the `MainMenu` scene in the editor and verify that `UMA_GLIB` is present and `CharacterCreatorRefs` is correctly linked.
2. **Runtime Test**: Play the game from the `MainMenu` (or `Boot`).
3. **Customization Test**: Open the Character Creator and verify that:
    - The preview avatar is visible.
    - Changing sliders (Height, Skin Tone) updates the 3D model.
    - Changing wardrobe options (Hair, Beard) swaps the UMA recipes.
4. **Resolution Test**: Verify the preview quality is acceptable and the `RawImage` matches the 3D room's lighting.
