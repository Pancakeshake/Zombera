# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with procedural world generation.
- Task: Replace the `Main Camera` setup in `WorldGenTest` with the `FreeFlyCamera` from the ACS (Advanced Camera System) asset to allow free navigation during overview testing.

# Game Mechanics
- Allows the developer to fly around the generated 3x3 tile grid using keyboard and mouse.
- Supports both normal and fast (Sprint) movement.

# Key Asset & Context
- `Assets/Scenes/Testing/WorldGenTest.unity`: The scene to modify.
- `Assets/ThirdParty/Jorjouto/ACS/Sample/Source/FreeFlyCamera.cs`: The script to add to the camera.
- `Assets/ThirdParty/Jorjouto/ACS/Sample/Input/ACS_InputSystem_Actions.inputactions`: The Input Action Asset for the camera.

# Implementation Steps

## 1. Scene Modification (WorldGenTest)
- **Identify and Update Main Camera**:
    - Open `WorldGenTest` scene.
    - Locate the `Main Camera`.
    - **Remove/Disable Old Components**: Ensure `PlayerFollowCamera` is removed or remains disabled.
    - **Add Fly Camera**: Add the `Jorjouto.AnimComposerSystem.Sample.FreeFlyCamera` component.
    - **Configure Settings**:
        - `MoveSpeed`: 50 (Higher for overview testing).
        - `FastMoveSpeed`: 200.
        - `LookSensitivity`: 0.2.
    - **Wire Inputs**:
        - Load `InputActionReference` assets from `ACS_InputSystem_Actions` and assign them to:
            - `CameraOrientationInputAxis`
            - `SprintButton`
            - `CameraMoveForwardButton`
            - `CameraMoveBackButton`
            - `CameraMoveLeftButton`
            - `CameraMoveRightButton`
            - `CameraMoveUpButton`
            - `CameraMoveDownButton`

## 2. Maintain Initial Viewpoint
- Ensure the `Main Camera` starts at the previously set overview position:
    - **Position**: (400, 2000, 500)
    - **Rotation**: (90, 0, 0)

# Verification & Testing
1. **Play Mode Test**: Enter Play Mode in `WorldGenTest.unity`.
2. **Navigation**: Use W/A/S/D to move horizontally, Q/E to move vertically, and Mouse to look around.
3. **Speed**: Verify holding Shift increases movement speed.
4. **Logs**: Ensure no "Missing Input Action" warnings appear.
