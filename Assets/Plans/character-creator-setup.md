# Project Overview
- Game Title: Zombera
- High-Level Concept: Character creator integration into the main menu flow.
- Current Status: `CharacterCreatorPanel` exists but is missing critical references (Preview Avatar, Camera, Display) in the `MainMenu` scene.

# Character Creator Setup & Wiring
The goal is to link the 3D preview avatar and camera to the UI panel so players can see their customization changes in real-time.

## Key Assets & Context
- `Assets/Prefabs/UI/Menus/CharacterCreatorPanel.prefab`: The UI panel.
- `Assets/Prefabs/Player/Player.prefab`: The source for the preview avatar.
- `MainMenu` Scene: The target for the character creator setup.

## Implementation Steps

### 1. Create a Preview Room in MainMenu
- **Task**: Set up a dedicated area for the character preview.
- **Components**:
    - **Player_Preview**: A copy of the `Player` prefab placed at a remote location (e.g., `(500, 0, 0)`).
    - **Preview_Camera**: A camera dedicated to rendering the `Player_Preview`.
    - **Preview_Light**: A light source (Directional or Spot) to illuminate the avatar.

### 2. Configure Render Texture (Optional but Recommended)
- **Task**: Render the preview character into the UI.
- **Steps**:
    - Create a `RenderTexture` (e.g., `RT_CharacterPreview`).
    - Set the **Preview_Camera**'s `Target Texture` to this `RenderTexture`.
    - (Wait, the existing code has `ResolvePreviewAvatar` which might imply a direct viewport approach or a runtime RT creation. I will stick to the most robust method: Runtime RT handled by `CharacterCreatorController` if possible, otherwise manual assignment).

### 3. Wire CharacterCreatorController
- **Task**: Assign references in the `MainMenu` scene instance of `CharacterCreatorPanel`.
- **Assignments**:
    - `creatorRefs.previewAvatar` -> `Player_Preview`.
    - `creatorRefs.customizationController.previewCamera` -> `Preview_Camera`.
    - (Ensure `customizationController` is assigned to its own object if not already).

### 4. Setup Customization Controller
- **Task**: Ensure the `CharacterCreatorCustomizationController` can find its UI elements.
- **Action**: Run the `BuildOrResolveRuntimeUi()` logic or manually assign the `customizationPanelRoot`.

## Automation Tool
I will provide a script `Assets/Scripts/Editor/CharacterCreatorSetupTool.cs` that automates this entire setup in the `MainMenu` scene.

# Verification & Testing
1. **Open MainMenu Scene**: Run the setup tool.
2. **Launch Game**: Enter the Character Creator from the Main Menu.
3. **Check Visuals**:
    - Does the character appear?
    - Do the hair/beard/body sliders update the 3D model?
    - Does "Confirm" correctly save the profile?
4. **Log Validation**: Ensure no "NULL" reference warnings appear in the console when opening the panel.
